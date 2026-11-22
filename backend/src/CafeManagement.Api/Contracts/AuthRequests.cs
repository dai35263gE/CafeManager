using System.ComponentModel.DataAnnotations;

namespace CafeManagement.Api.Contracts;

/// <summary>Username/password credentials.</summary>
public sealed record LoginRequest
{
    [Required, MaxLength(100)] public required string Username { get; init; }
    [Required, MaxLength(128)] public required string Password { get; init; }
}

/// <summary>Authenticated password change.</summary>
public sealed record ChangePasswordRequest
{
    [Required, MaxLength(128)] public required string CurrentPassword { get; init; }
    [Required, MinLength(8), MaxLength(128)] public required string NewPassword { get; init; }
}

/// <summary>Account details without password or security stamps.</summary>
public sealed record UserResponse(string Id, string Username, string FullName, string Role,
    string EmployeeId, bool IsActive, DateTimeOffset? LastLoginAt, bool IsLocked);

/// <summary>JWT and current account details.</summary>
public sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt, UserResponse User);

/// <summary>Admin account creation, optionally linked to an existing employee.</summary>
public sealed record CreateUserRequest
{
    [Required, MaxLength(100)] public required string Username { get; init; }
    [Required, MinLength(8), MaxLength(128)] public required string Password { get; init; }
    [Required, MaxLength(150)] public required string FullName { get; init; }
    [Required, EmailAddress, MaxLength(254)] public required string Email { get; init; }
    [Required] public required string Role { get; init; }
    public string? EmployeeId { get; init; }
}

/// <summary>Account creation for a previously registered working employee.</summary>
public sealed record CreateEmployeeAccountRequest
{
    [Required, MaxLength(100)] public required string Username { get; init; }
    [Required, MinLength(8), MaxLength(128)] public required string Password { get; init; }
    [Required] public required string Role { get; init; }
}

/// <summary>Admin account profile and role update.</summary>
public sealed record UpdateUserRequest
{
    [Required, MaxLength(150)] public required string FullName { get; init; }
    [Required, EmailAddress, MaxLength(254)] public required string Email { get; init; }
    [Required] public required string Role { get; init; }
    public string? EmployeeId { get; init; }
}

/// <summary>Enable or disable an account immediately, invalidating issued tokens.</summary>
public sealed record LockUserRequest(bool Locked);
