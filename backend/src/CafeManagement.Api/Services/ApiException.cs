namespace CafeManagement.Api.Services;

public sealed class ApiException(int statusCode, string message,
    IReadOnlyDictionary<string, string[]>? errors = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, string[]>? Errors { get; } = errors;
}
