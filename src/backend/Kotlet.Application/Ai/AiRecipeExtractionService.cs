using System.Text.Json;
using Kotlet.Application.VideoTranscripts;
using Microsoft.Extensions.AI;

namespace Kotlet.Application.Ai;

/// <summary>Extracts a non-persisted recipe draft from video transcript content using the requesting user's AI provider.</summary>
public sealed class AiRecipeExtractionService(IUserChatClientResolver resolver)
{
    private const int MaxTranscriptLength = 30_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RecipeExtractionResult> ExtractAsync(
        Guid userId, VideoContent? content, CancellationToken cancellationToken)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.Transcript))
        {
            return new(RecipeExtractionStatus.InvalidRequest,
                Message: "Video transcript content is required.");
        }

        if (content.Transcript.Length > MaxTranscriptLength)
        {
            return new(RecipeExtractionStatus.InvalidRequest,
                Message: $"Video transcript cannot exceed {MaxTranscriptLength} characters.");
        }

        using var client = await resolver.ResolveAsync(userId, cancellationToken);
        if (client is null)
        {
            return new(RecipeExtractionStatus.NotConfigured,
                Message: "Configure an AI provider before importing a recipe.");
        }

        try
        {
            var response = await client.GetResponseAsync(
                [
                    new(ChatRole.System, AiRecipeExtractionPrompts.SystemPrompt),
                    new(ChatRole.User, AiRecipeExtractionPrompts.BuildUserMessage(content))
                ],
                new ChatOptions { Temperature = 0f },
                cancellationToken);

            var parsed = JsonSerializer.Deserialize<ExtractionResponse>(response.Text ?? "", JsonOptions);
            if (parsed is null || parsed.IsRecipe == false || parsed.Ingredients is null ||
                parsed.Ingredients.Count == 0 || parsed.Steps is null || parsed.Steps.Count == 0)
            {
                return new(RecipeExtractionStatus.NotARecipe,
                    Message: "The video does not contain a complete cookable recipe.");
            }

            var ingredients = parsed.Ingredients
                .Where(ingredient => !string.IsNullOrWhiteSpace(ingredient.Name))
                .Select(ingredient => new DraftIngredient(
                    ingredient.Name.Trim(), ingredient.Quantity, NullIfBlank(ingredient.Unit),
                    NullIfBlank(ingredient.Note)))
                .ToArray();
            var steps = parsed.Steps.Where(step => !string.IsNullOrWhiteSpace(step)).Select(step => step.Trim()).ToArray();
            if (ingredients.Length == 0 || steps.Length == 0)
            {
                return new(RecipeExtractionStatus.NotARecipe,
                    Message: "The video does not contain a complete cookable recipe.");
            }

            var title = string.IsNullOrWhiteSpace(parsed.Title)
                ? (string.IsNullOrWhiteSpace(content.Title) ? "Imported recipe" : content.Title.Trim())
                : parsed.Title.Trim();
            var instructions = string.Join(Environment.NewLine, steps.Select((step, index) => $"{index + 1}. {step}"));
            if (content.SourceUrl is not null)
            {
                instructions += $"{Environment.NewLine}{Environment.NewLine}Imported from [{title}]({content.SourceUrl})";
            }

            var gaps = parsed.Gaps?.Where(gap => !string.IsNullOrWhiteSpace(gap)).Select(gap => gap.Trim()).ToArray() ?? [];
            return new(RecipeExtractionStatus.Extracted, new RecipeDraft(
                title,
                parsed.Servings is > 0 ? parsed.Servings.Value : 1,
                ingredients,
                instructions,
                gaps));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new(RecipeExtractionStatus.Failed, Message: "The AI recipe extraction request failed.");
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ExtractionResponse(
        bool? IsRecipe,
        string? Title,
        int? Servings,
        List<ExtractionIngredient>? Ingredients,
        List<string>? Steps,
        List<string>? Gaps);

    private sealed record ExtractionIngredient(
        string Name,
        decimal? Quantity,
        string? Unit,
        string? Note);
}
