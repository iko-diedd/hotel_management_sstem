using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HousekeepingApi.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                 ?? "Server=localhost;Port=3306;Database=housekeeping;User=root;Password=design-time-only;";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(cs, ServerVersion.Create(new Version(8, 0, 36), Pomelo.EntityFrameworkCore.MySql.Infrastructure.ServerType.MySql))
            .Options;
        return new AppDbContext(options);
    }
}
