using Kotlet.Domain.AgentMemory;
using AgentMemoryEntity = Kotlet.Domain.AgentMemory.AgentMemory;

namespace Kotlet.Application.AgentMemory;

public sealed record AgentMemoryResponse(
    Guid Id,
    string? Title,
    string Content,
    string? Category,
    string Source,
    decimal? Confidence,
    string ReviewStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ExpiresAt,
    long Version,
    long Revision,
    string? CreatedByClient,
    string? UpdatedByClient,
    bool Deleted)
{
    public static AgentMemoryResponse From(AgentMemoryEntity memory) => new(
        memory.Id,
        memory.IsDeleted ? null : memory.Title,
        memory.IsDeleted ? string.Empty : memory.Content,
        memory.IsDeleted ? null : memory.Category,
        memory.Source.ToString(),
        memory.Confidence,
        memory.ReviewStatus.ToString(),
        memory.CreatedAt,
        memory.UpdatedAt,
        memory.ExpiresAt,
        memory.Version,
        memory.Revision,
        memory.CreatedByClient,
        memory.UpdatedByClient,
        memory.IsDeleted);
}

public sealed record CreateAgentMemoryRequest(
    string Content,
    string? Title = null,
    string? Category = null,
    string Source = "ExplicitRequest",
    decimal? Confidence = null,
    string? ReviewStatus = null,
    DateTimeOffset? ExpiresAt = null,
    string? ClientId = null);

public sealed record UpdateAgentMemoryRequest(
    long ExpectedVersion,
    string Content,
    string? Title = null,
    string? Category = null,
    string Source = "ExplicitRequest",
    decimal? Confidence = null,
    string? ReviewStatus = null,
    DateTimeOffset? ExpiresAt = null,
    string? ClientId = null);

public sealed record AgentMemoryListResponse(
    long Revision,
    IReadOnlyList<AgentMemoryResponse> Memories,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);

public sealed record AgentMemoryChangesResponse(long Revision, IReadOnlyList<AgentMemoryResponse> Changes);

public sealed record AgentMemoryExportResponse(long Revision, string Markdown);

public sealed record AgentMemoryOperationResult(
    string Status,
    AgentMemoryResponse? Memory = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    string? Message = null)
{
    public static AgentMemoryOperationResult From(
        AgentMemoryOperationStatus status,
        AgentMemoryEntity? memory = null,
        IReadOnlyDictionary<string, string[]>? errors = null,
        string? message = null) => new(
        status.ToString(),
        memory is null ? null : AgentMemoryResponse.From(memory),
        errors,
        message);
}

public enum AgentMemoryOperationStatus
{
    Success,
    NotFound,
    Conflict,
    ValidationFailed
}

public sealed record AgentMemoryBootstrap(
    long Revision,
    string Markdown,
    string Instructions);
