namespace SpendWell.Api.DTOs;

public record PointsTransactionDto(
    int Id,
    DateTime CreatedAt,
    string Type,
    int Points,
    decimal MultiplierApplied,
    string SourceType,
    int? SourceId,
    string Description);

public record PointsLotDto(
    int Id,
    int OriginalPoints,
    int RemainingPoints,
    DateTime EarnedAt,
    DateTime ExpiresAt,
    bool IsExpired);
