using System.ComponentModel;
using System.Text.Json;
using Kotlet.Api.Auth;
using Kotlet.Api.Mcp;
using Kotlet.Application.Pantry;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.Pantry;

/// <summary>MCP tools for resolving fridge observations and safely reconciling pantry state.</summary>
[McpServerToolType]
public sealed class PantryReconciliationMcp
{
    [McpServerTool(Name = "pantry.resolve_observations", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Resolves a complete, deduplicated list of observations from one or more fridge photos against Kotlet's ingredient catalogue. Returns matched, ambiguous, and unmatched observations separately; never invents an item id. The response includes pantryVersion for the subsequent pantry.reconcile call.")]
    public static async Task<CallToolResult> ResolveObservations(
        PantryResolveObservationsRequest request,
        PantryReconciliationService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        Result(
            await service.ResolveAsync(RequireHouse(currentUser), request, cancellationToken),
            response =>
                $"Resolved {response.Matched.Count} observation(s): {response.Ambiguous.Count} ambiguous, "
                + $"{response.Unmatched.Count} unmatched, {response.UnrecognizedCount} not recognized. "
                + $"Use pantryVersion {response.PantryVersion} when reconciling.");

    [McpServerTool(Name = "pantry.reconcile", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Atomically reconciles resolved fridge observations into the authenticated household pantry. The default merge mode adds or sets observed items and never removes items absent from photos. Use reconcile_visible_snapshot only with full coverage, or replace_location only with full coverage and explicit confirmation. Supply the pantryVersion returned by pantry.resolve_observations, a unique operationId, and safe normalized quantities. Low-confidence quantities are returned in needsReview without changing pantry quantities. The complete diff and shared MCP UI resource are returned in this same call.")]
    public static async Task<CallToolResult> Reconcile(
        PantryReconcileRequest request,
        PantryReconciliationService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        Result(
            await service.ReconcileAsync(RequireHouse(currentUser), request, cancellationToken),
            response =>
                $"Pantry reconciliation {response.Status}: {response.Added.Count} added, "
                + $"{response.Increased.Count} increased, {response.Decreased.Count} decreased, "
                + $"{response.Removed.Count} removed, {response.NeedsReview.Count} needs review, "
                + $"{response.Unmatched.Count} not matched, {response.Ambiguous.Count} ambiguous, "
                + $"{response.UnrecognizedCount} not recognized.");

    [McpServerTool(Name = "pantry.undo_reconcile", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Undoes changes from a successful pantry.reconcile call using its undoToken. Undo is rejected when the pantry changed after that reconciliation, so a newer scan cannot be overwritten accidentally.")]
    public static async Task<CallToolResult> UndoReconcile(
        [Description("undoToken returned by pantry.reconcile when changes can be undone.")] string undoToken,
        PantryReconciliationService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        Result(
            await service.UndoAsync(RequireHouse(currentUser), undoToken, cancellationToken),
            response => $"Pantry undo {response.Status}; pantryVersion is now {response.PantryVersion}.");

    private static CallToolResult Result<T>(T value, Func<T, string> summary) => new()
    {
        Content = [new TextContentBlock { Text = summary(value) }],
        StructuredContent = JsonSerializer.SerializeToElement(value, JsonSerializerOptions.Web)
    };
}
