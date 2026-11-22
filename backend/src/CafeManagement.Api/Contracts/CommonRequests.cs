using System.ComponentModel.DataAnnotations;

namespace CafeManagement.Api.Contracts;

/// <summary>Bounded pagination and optional text search.</summary>
public sealed record ListRequest
{
    [Range(1, int.MaxValue)] public int Page { get; init; } = 1;
    [Range(1, 100)] public int PageSize { get; init; } = 20;
    [MaxLength(150)] public string? Search { get; init; }
}

/// <summary>A page of resource DTOs.</summary>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

/// <summary>Required optimistic concurrency token.</summary>
public sealed record VersionRequest
{
    [Required] public required string RowVersion { get; init; }
}

/// <summary>An operation acknowledgement.</summary>
public sealed record MessageResponse(string Detail);

/// <summary>API liveness, independent of database connectivity.</summary>
public sealed record HealthResponse(int Phase);
