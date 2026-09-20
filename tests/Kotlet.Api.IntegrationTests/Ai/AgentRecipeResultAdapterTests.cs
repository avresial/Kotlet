using Kotlet.Api.Ai;
using Kotlet.Api.Mcp;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Ai;

public sealed class AgentRecipeResultAdapterTests
{
    [Fact]
    public void Adapt_MapsTheLatestRecipeToolResultToSharedCardData()
    {
        var recipeId = Guid.NewGuid();
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent("call-1", "get_recipes", new Dictionary<string, object?>()),
            new FunctionResultContent("call-1", new McpRecipeSearchResponse(
                [new McpRecipeSummary(
                    recipeId,
                    "Tomato soup",
                    4,
                    "dinner",
                    [new McpRecipePlanningIngredient(Guid.NewGuid(), "Tomato")],
                    $"kotlet://recipes/{recipeId}",
                    "A warm soup.",
                    "/api/recipes/recipe-1/images/cover/content")],
                1))
        ]));

        var result = AgentRecipeResultAdapter.Adapt(
            response, "https://api.example", "https://app.example");

        var structured = Assert.Single(result);
        var card = Assert.Single(structured.Recipes);
        Assert.Equal("recipes", structured.Type);
        Assert.Equal(1, structured.TotalCount);
        Assert.Equal(recipeId, card.Id);
        Assert.Equal("Tomato soup", card.Title);
        Assert.Equal("https://api.example/api/recipes/recipe-1/images/cover/content", card.ImageUrl);
        Assert.True(card.CanEdit);
    }

    [Fact]
    public void Adapt_IgnoresUnrelatedToolResults()
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant,
        [
            new FunctionCallContent("call-1", "get_ingredients", new Dictionary<string, object?>()),
            new FunctionResultContent("call-1", new object())
        ]));

        Assert.Empty(AgentRecipeResultAdapter.Adapt(
            response, "https://api.example", "https://app.example"));
    }
}
