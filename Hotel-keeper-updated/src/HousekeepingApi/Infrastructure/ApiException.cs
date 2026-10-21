namespace HousekeepingApi.Infrastructure;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public ApiException(int statusCode, string message) : base(message) => StatusCode = statusCode;

    public static ApiException NotFound(string what) => new(404, $"{what} was not found.");
    public static ApiException Conflict(string message) => new(409, message);
    public static ApiException BadRequest(string message) => new(400, message);
    public static ApiException Forbidden(string message) => new(403, message);
}
