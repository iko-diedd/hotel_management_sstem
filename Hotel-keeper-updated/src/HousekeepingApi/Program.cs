using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using HousekeepingApi.Data;
using HousekeepingApi.Domain;
using HousekeepingApi.Infrastructure;
using HousekeepingApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var isDev = builder.Environment.IsDevelopment();

var connectionString = BuildConnectionString(config);
var serverVersion = ServerVersion.Create(
    Version.Parse(config["Database:ServerVersion"] ?? "8.0.36"),
    Enum.Parse<ServerType>(config["Database:ServerType"] ?? "MySql", ignoreCase: true));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(connectionString, serverVersion, my => my.EnableRetryOnFailure(3)));

var jwt = config.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.Key) || Encoding.UTF8.GetByteCount(jwt.Key) < 32)
    throw new InvalidOperationException("Jwt:Key must be set (environment variable Jwt__Key) to a random secret of at least 32 characters.");
builder.Services.Configure<JwtOptions>(config.GetSection("Jwt"));
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<RoomWorkflow>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = jwt.Issuer,
            ValidateAudience = true, ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        o.Events = new JwtBearerEvents
        {
            // revalidate user
            OnTokenValidated = async ctx =>
            {
                var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var id = int.Parse(ctx.Principal!.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var tokenRole = ctx.Principal.FindFirstValue(ClaimTypes.Role);
                var user = await db.Users.AsNoTracking().Where(u => u.Id == id)
                    .Select(u => new { u.IsActive, u.Role }).FirstOrDefaultAsync();
                if (user is null || !user.IsActive) ctx.Fail("Account is disabled.");
                else if (user.Role.ToString() != tokenRole) ctx.Fail("Role changed; please sign in again.");
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    // secure default
    o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();

builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
{
    var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
    if (origins.Length > 0)
        p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("public", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
});

// Enable only when running behind a trusted reverse proxy (nginx, IIS, a load balancer).
if (config.GetValue("ForwardedHeaders:Enabled", false))
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hotel Housekeeping Room Status API",
        Version = "v1",
        Description = "Room status, availability and housekeeping notices. Roles: RoomAssistant, Housekeeper, Admin."
    });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Obtain a token from POST /api/auth/login, then paste it here."
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

var app = builder.Build();

if (config.GetValue("ForwardedHeaders:Enabled", false)) app.UseForwardedHeaders();
if (!isDev) app.UseHsts();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (config.GetValue("Swagger:Enabled", false) || app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => { o.SwaggerEndpoint("/swagger/v1/swagger.json", "Housekeeping API v1"); o.DocumentTitle = "Housekeeping API"; });
}

if (config.GetValue("Https:Redirect", !isDev)) app.UseHttpsRedirection();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await DbInitializer.InitializeAsync(scope.ServiceProvider, config, logger);
}

app.Run();

static string BuildConnectionString(IConfiguration c)
{
    var cs = c.GetConnectionString("Default");
    if (!string.IsNullOrWhiteSpace(cs)) return cs;

    var name = c["DB_NAME"]; var user = c["DB_USER"]; var pass = c["DB_PASSWORD"];
    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(user) || pass is null)
        throw new InvalidOperationException(
            "No database configured. Set ConnectionStrings__Default, or DB_HOST/DB_PORT/DB_NAME/DB_USER/DB_PASSWORD environment variables.");

    return new MySqlConnector.MySqlConnectionStringBuilder
    {
        Server = c["DB_HOST"] ?? "localhost",
        Port = uint.TryParse(c["DB_PORT"], out var port) ? port : 3306,
        Database = name, UserID = user, Password = pass
    }.ConnectionString;
}

public partial class Program { }
