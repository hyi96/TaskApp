namespace TaskApp.Services;

public sealed record CreateAccountRequest(string? DisplayName);

public sealed record AccountResponse(
    Guid Id,
    string DisplayName,
    DateTimeOffset CreatedAt);

public sealed record UpsertProfileSnapshotRequest(
    string ProfileName,
    TaskAppDataSnapshot Snapshot);

public sealed record ProfileSummaryResponse(
    Guid AccountId,
    Guid ProfileId,
    string ProfileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string SchemaVersion);

public sealed record ProfileSnapshotResponse(
    Guid AccountId,
    Guid ProfileId,
    string ProfileName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    TaskAppDataSnapshot Snapshot);

public sealed record HealthResponse(
    string Status,
    DateTimeOffset CheckedAt);
