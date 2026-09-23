using System.Text.Json;
using System.Text.Json.Nodes;
using Kotlet.Api.Mcp;
using Kotlet.Api.Recipes;
using Kotlet.Application.Recipes;
using Microsoft.Extensions.AI;

namespace Kotlet.Api.Ai;

/// <summary>Maps Agent tool results to the framework-agnostic structured UI contract.</summary>
internal static class AgentRecipeResultAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<AgentStructuredResult> Adapt(
        ChatResponse response, string apiOrigin, string frontendOrigin)
    {
        var toolNames = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionCallContent>()
            .GroupBy(call => call.CallId)
            .ToDictionary(group => group.Key, group => group.Last().Name);

        var result = response.Messages
            .SelectMany(message => message.Contents)
            .OfType<FunctionResultContent>()
            .Where(functionResult => toolNames.TryGetValue(functionResult.CallId, out var toolName)
                && toolName is "get_recipes" or "get_recipe")
            .Select(functionResult => Map(
                toolNames[functionResult.CallId], functionResult.Result, apiOrigin, frontendOrigin))
            .OfType<AgentStructuredResult>()
            .LastOrDefault();

        return result is null ? [] : [result];
    }

    private static AgentStructuredResult? Map(
        string toolName, object? result, string apiOrigin, string frontendOrigin)
    {
        if (toolName == "get_recipes" && TryDeserialize<McpRecipeSearchResponse>(result) is { } search)
        {
            var cards = search.Recipes
                .Select(recipe => new RecipeUiPresentationCard(
                    recipe.Id,
                    recipe.Title,
                    recipe.Description,
                    recipe.MealType,
                    recipe.Servings,
                    recipe.Ingredients.Count,
                    RecipeUiMcp.ToAbsoluteUrlOrNull(apiOrigin, recipe.ImageUrl),
                    CanEdit: true))
                .ToList();

            return new AgentStructuredResult("recipes", cards, search.TotalCount);
        }

        if (toolName == "get_recipe" && TryDeserialize<RecipeDetailResponse>(result) is { } detail)
        {
            var presentation = RecipeUiDetail.From(detail, apiOrigin, frontendOrigin);
            var card = new RecipeUiPresentationCard(
                presentation.Id,
                presentation.Title,
                RecipeUiMcp.SummaryText(presentation.Description),
                presentation.MealType,
                presentation.Servings,
                presentation.Ingredients.Count,
                presentation.Image?.Url,
                presentation.CanEdit,
                presentation.IsAiAssisted);
            return new AgentStructuredResult("recipes", [card], 1);
        }

        return null;
    }

    private static T? TryDeserialize<T>(object? value)
    {
        try
        {
            return value switch
            {
                T typed => typed,
                JsonElement element => element.Deserialize<T>(JsonOptions),
                JsonNode node => node.Deserialize<T>(JsonOptions),
                string json => JsonSerializer.Deserialize<T>(json, JsonOptions),
                null => default,
                _ => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)
            };
        }
        catch (JsonException)
        {
            return default;
        }
        catch (NotSupportedException)
        {
            return default;
        }
    }
}

public sealed record AgentStructuredResult(
    string Type,
    IReadOnlyList<RecipeUiPresentationCard> Recipes,
    int TotalCount);
