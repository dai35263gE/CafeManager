namespace CafeManagement.Api.Contracts;

public sealed record ApiResponse<T>(
    bool Success, string Message, T? Data, IReadOnlyDictionary<string, string[]>? Errors = null);
