using System.ComponentModel.DataAnnotations;
using CafeManagement.Domain.Enums;

namespace CafeManagement.Api.Contracts;

/// <summary>Employee profile creation; the system generates the employee code and starts the employee as Working.</summary>
public sealed record CreateEmployeeRequest
{
    [Required, MaxLength(150)] public required string FullName { get; init; }
    [Required, MaxLength(100)] public required string Position { get; init; }
    [Phone, MaxLength(30)] public string? Phone { get; init; }
    [EmailAddress, MaxLength(254)] public string? Email { get; init; }
}

/// <summary>Employee profile and work status update.</summary>
public sealed record SaveEmployeeRequest
{
    [Required, MaxLength(10)] public required string Code { get; init; }
    [Required, MaxLength(150)] public required string FullName { get; init; }
    [Required, MaxLength(100)] public required string Position { get; init; }
    [Phone, MaxLength(30)] public string? Phone { get; init; }
    [EmailAddress, MaxLength(254)] public string? Email { get; init; }
    [EnumDataType(typeof(EmployeeStatus))] public EmployeeStatus Status { get; init; } = EmployeeStatus.Working;
}

/// <summary>Employee and optional account linkage, without sensitive account fields.</summary>
public sealed record EmployeeResponse(string Id, string Code, string FullName, string Position, string? Phone,
    string? Email, EmployeeStatus Status, string? UserId, bool? AccountIsActive, bool? AccountIsLocked);
