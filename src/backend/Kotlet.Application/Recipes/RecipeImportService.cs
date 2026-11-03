using System.Text.Json;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

/// <summary>Creates and reads recipe import jobs; processing and acceptance live in their own services.</summary>
public sealed class RecipeImportService(
    IRecipeImportJobRepository jobs,
    IRecipeImportSignal signal)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RecipeImportOperationResult> CreateJobAsync(
        Guid houseId, Guid userId, string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Validation("url", "URL must be an absolute http(s) URL.");
        }

        var now = DateTimeOffset.UtcNow;
        var job = new RecipeImportJob
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            UserId = userId,
            Url = uri.ToString(),
            Status = RecipeImportJobStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        jobs.Add(job);
        await jobs.SaveChangesAsync(cancellationToken);
        signal.Enqueue(job.Id);
        return new(RecipeImportOperationStatus.Success, job.Id);
    }

    public async Task<RecipeImportJobResponse?> GetJobAsync(
        Guid id, Guid houseId, Guid userId, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, houseId, false, cancellationToken);
        if (job is null || job.UserId != userId)
        {
            return null;
        }
        var draft = job.DraftJson is null ? null : JsonSerializer.Deserialize<RecipeImportDraft>(job.DraftJson, JsonOptions);
        return new(job.Id, job.Status, draft, job.ErrorReason);
    }

    public async Task<RecipeImportOperationResult> CreateReviewJobAsync(
        Guid houseId,
        Guid userId,
        string sourceUrl,
        RecipeImportDraft draft,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var job = new RecipeImportJob
        {
            Id = Guid.NewGuid(),
            HouseId = houseId,
            UserId = userId,
            Url = sourceUrl,
            Status = RecipeImportJobStatus.ReadyForReview,
            DraftJson = JsonSerializer.Serialize(draft, JsonOptions),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        jobs.Add(job);
        await jobs.SaveChangesAsync(cancellationToken);
        return new(RecipeImportOperationStatus.Success, job.Id);
    }

    private static RecipeImportOperationResult Validation(string key, string message) =>
        new(RecipeImportOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]> { [key] = [message] });
}
