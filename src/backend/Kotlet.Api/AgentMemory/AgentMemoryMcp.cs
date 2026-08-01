using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Api.Mcp;
using Kotlet.Application.AgentMemory;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.AgentMemory;

[McpServerToolType]
[McpServerResourceType]
public sealed class AgentMemoryMcp
{
    [McpServerTool(Name = "memory_create", ReadOnly = false, Destructive = false, Idempotent = false,
        OpenWorld = false, UseStructuredContent = true),
     Description("Creates one concise, user-owned memory. Search first to avoid duplicates. Never store passwords, tokens, full transcripts, or speculative facts.")]
    public static Task<AgentMemoryOperationResult> Create(
        [Description("Memory content, a concise durable fact or preference, at most 10000 characters.")] string content,
        [Description("Optional short title, at most 200 characters.")] string? title,
        [Description("Optional category such as food, household, workflow, finance, or development.")] string? category,
        [Description("One of ExplicitRequest, UserCorrection, UserProvidedFact, AgentInference.")] string source,
        [Description("Optional confidence from 0 to 1; use it for inferred memories.")] decimal? confidence,
        [Description("One of Confirmed, Inferred, NeedsReview. Inferences should normally be Inferred or NeedsReview.")] string? reviewStatus,
        [Description("Optional expiry timestamp in ISO-8601 format.")] DateTimeOffset? expiresAt,
        [Description("Optional MCP client or agent identifier.")] string? clientId,
        AgentMemoryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        service.CreateAsync(RequireUser(currentUser), RequireHouse(currentUser),
            new CreateAgentMemoryRequest(content, title, category, source, confidence, reviewStatus, expiresAt, clientId), cancellationToken);

    [McpServerTool(Name = "memory_search", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Searches the current user's household memories by text and optional category/source/review filters. Deleted and expired memories are omitted by default.")]
    public static Task<AgentMemoryListResponse> Search(
        [Description("Natural-language or exact text to search in memory titles and content.")] string query,
        [Description("Optional category filter.")] string? category,
        [Description("Optional source filter: ExplicitRequest, UserCorrection, UserProvidedFact, AgentInference.")] string? source,
        [Description("Optional review filter: Confirmed, Inferred, NeedsReview.")] string? reviewStatus,
        AgentMemoryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        service.ListAsync(RequireUser(currentUser), RequireHouse(currentUser), query, category, source, reviewStatus, false, cancellationToken);

    [McpServerTool(Name = "memory_list", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Lists all current memories with stable ids, metadata, versions, expiry, and revision. Use memory_search for routine context retrieval.")]
    public static Task<AgentMemoryListResponse> List(
        [Description("Optional category filter.")] string? category,
        [Description("Optional source filter: ExplicitRequest, UserCorrection, UserProvidedFact, AgentInference.")] string? source,
        [Description("Optional review filter: Confirmed, Inferred, NeedsReview.")] string? reviewStatus,
        [Description("Include expired memories when true.")] bool includeExpired,
        AgentMemoryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        service.ListAsync(RequireUser(currentUser), RequireHouse(currentUser), null, category, source, reviewStatus, includeExpired, cancellationToken);

    [McpServerTool(Name = "memory_get", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns one memory by its stable id.")]
    public static async Task<AgentMemoryResponse> Get(
        [Description("Stable memory id returned by memory_list or memory_search.")] Guid id,
        AgentMemoryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        await service.GetAsync(id, RequireUser(currentUser), RequireHouse(currentUser), cancellationToken)
        ?? throw new McpException("Memory not found.");

    [McpServerTool(Name = "memory_update", ReadOnly = false, Destructive = false, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true),
     Description("Updates one memory using its expected version. A stale version returns Conflict instead of silently overwriting a newer user edit.")]
    public static Task<AgentMemoryOperationResult> Update(
        [Description("Stable memory id.")] Guid id,
        [Description("Version returned by memory_get/list; required for optimistic concurrency.")] long expectedVersion,
        [Description("Replacement memory content.")] string content,
        [Description("Optional replacement title.")] string? title,
        [Description("Optional replacement category.")] string? category,
        [Description("One of ExplicitRequest, UserCorrection, UserProvidedFact, AgentInference.")] string source,
        [Description("Optional confidence from 0 to 1.")] decimal? confidence,
        [Description("One of Confirmed, Inferred, NeedsReview.")] string? reviewStatus,
        [Description("Optional expiry timestamp in ISO-8601 format.")] DateTimeOffset? expiresAt,
        [Description("Optional MCP client or agent identifier.")] string? clientId,
        AgentMemoryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, RequireUser(currentUser), RequireHouse(currentUser),
            new UpdateAgentMemoryRequest(expectedVersion, content, title, category, source, confidence, reviewStatus, expiresAt, clientId), cancellationToken);

    [McpServerTool(Name = "memory_delete", ReadOnly = false, Destructive = true, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true),
     Description("Deletes one memory by id using its expected version. The deletion remains as a revision tombstone for incremental synchronization.")]
    public static Task<AgentMemoryOperationResult> Delete(
        [Description("Stable memory id.")] Guid id,
        [Description("Version returned by memory_get/list; required for optimistic concurrency.")] long expectedVersion,
        [Description("Optional MCP client or agent identifier.")] string? clientId,
        AgentMemoryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        service.DeleteAsync(id, RequireUser(currentUser), RequireHouse(currentUser), expectedVersion, clientId, cancellationToken);

    [McpServerTool(Name = "memory_changes_since", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns memory records and deletion tombstones changed after a known global revision.")]
    public static Task<AgentMemoryChangesResponse> ChangesSince(
        [Description("Last global memory revision known by the client, or zero for a full change stream.")] long sinceRevision,
        AgentMemoryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        sinceRevision < 0
            ? throw new McpException("sinceRevision must be zero or greater.")
            : service.ChangesAsync(RequireUser(currentUser), RequireHouse(currentUser), sinceRevision, cancellationToken);

    [McpServerTool(Name = "memory_export_markdown", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Exports the current structured memories as deterministic Markdown with ids, metadata, versions, and untrusted-content delimiters.")]
    public static Task<AgentMemoryExportResponse> Export(
        AgentMemoryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        service.ExportAsync(RequireUser(currentUser), RequireHouse(currentUser), cancellationToken);

    [McpServerTool(Name = "memory_bootstrap", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Fallback bootstrap for MCP clients that do not automatically read kotlet://agent-memory. Returns the complete Markdown memory document, revision, and storage guidance.")]
    public static Task<AgentMemoryBootstrap> Bootstrap(
        AgentMemoryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        service.BootstrapAsync(RequireUser(currentUser), RequireHouse(currentUser), cancellationToken);

    [McpServerResource(UriTemplate = "kotlet://agent-memory", Name = "agent-memory", Title = "Agent memory",
        MimeType = "application/json"),
     Description("Current user-controlled structured memory export, global revision, and guidance for safe memory handling.")]
    public static async Task<string> Resource(
        AgentMemoryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        McpHelpers.Json(await service.BootstrapAsync(RequireUser(currentUser), RequireHouse(currentUser), cancellationToken));
}
