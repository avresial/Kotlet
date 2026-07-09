using Kotlet.Application.VideoTranscripts;

namespace Kotlet.Application.Ai;

internal static class AiRecipeExtractionPrompts
{
    public const string SystemPrompt =
        "You extract recipes from cooking-video content. Reply with valid JSON only, with no markdown " +
        "fences or commentary. Use exactly these fields: isRecipe (boolean), title (string), servings " +
        "(positive integer or null), ingredients (array of objects with name, quantity, unit, note), " +
        "steps (array of strings), and gaps (array of strings). Set isRecipe to false when the content " +
        "does not describe a cookable recipe. Never invent quantities: prefer exact quantities in the " +
        "description when the transcript omits them, otherwise use null and explain the gap. Return " +
        "ordered, actionable cooking steps.";

    public static string BuildUserMessage(VideoContent content) =>
        $"Video title: {content.Title ?? "(unknown)"}\n" +
        $"Author: {content.Author ?? "(unknown)"}\n" +
        $"Description:\n{content.Description ?? "(none)"}\n\n" +
        $"Transcript:\n{content.Transcript}";
}
