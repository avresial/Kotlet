using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Application.Recipes;
using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.Recipes;

/// <summary>MCP tools, resources, and prompts for household recipes.</summary>
[McpServerToolType]
[McpServerResourceType]
[McpServerPromptType]
public sealed class RecipeMcp
{
    [McpServerTool(Name = "get_recipes", ReadOnly = true, OpenWorld = false),
     Description("Searches household recipes and returns links to their complete MCP resources.")]
    public static async Task<IReadOnlyList<ResourceLinkBlock>> GetRecipes(
        RecipeService service,
        ICurrentUser currentUser,
        [Description("Page number, starting at 1.")] int page = 1,
        [Description("Recipes per page, from 1 to 100.")] int pageSize = 20,
        [Description("Optional text to search for in recipes.")] string? search = null,
        CancellationToken cancellationToken = default) =>
        (await service.ListAsync(RequireHouse(currentUser), page, pageSize, search, null, null, cancellationToken)).Items
        .Select(recipe => Link(
            $"kotlet://recipes/{recipe.Id}", recipe.Title,
            $"Recipe for {recipe.Servings} serving(s) with {recipe.IngredientCount} ingredient(s)."))
        .ToList();

    [McpServerTool(Name = "get_recipe", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns one complete household recipe: Markdown description with preparation steps, servings, and the full ingredient list with quantities.")]
    public static async Task<RecipeDetailResponse> GetRecipe(
        [Description("Recipe ID from get_recipes.")] Guid recipeId,
        RecipeService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        await service.GetByIdAsync(recipeId, RequireHouse(currentUser), cancellationToken)
        ?? throw new McpException("Recipe not found.");

    [McpServerTool(Name = "add_recipe", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Creates one new household recipe. This is an add-only one-shot tool: gather ingredient IDs first (via get_ingredients, creating missing ones with create_ingredient), include quantities, units, optional notes, servings, and a Markdown description with preparation steps before calling it. When importing a recipe from the internet, cite the source URL at the end of the Markdown description. Read the kotlet://recipes/new-recipe-guide resource for the full workflow.")]
    public static Task<RecipeOperationResult> AddRecipe(
        [Description("Complete recipe to create. DescriptionMarkdown should include a concise overview and numbered cooking steps. Ingredients must use existing ingredient IDs from get_ingredients or kotlet://ingredients/{ingredientId} resources.")]
        CreateRecipeRequest request,
        RecipeService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.CreateAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerResource(UriTemplate = "kotlet://recipes/new-recipe-guide", Name = "new-recipe-guide",
        Title = "New recipe creation guide", MimeType = "text/markdown"),
     Description("Instructions for creating a new Kotlet recipe through MCP without editing existing recipes.")]
    public static string NewRecipeGuide() =>
        """
        # New recipe creation flow

        Use this resource before calling the `add_recipe` tool. The MCP server intentionally exposes recipe creation only; it does not expose an edit recipe tool.

        1. Understand the requested recipe and decide on a title, servings, and a Markdown description.
           When the recipe comes from the internet (a website, video, or blog), review it with the user
           first: extract the title, servings, ingredient quantities, and steps from the source yourself.
        2. Write `descriptionMarkdown` with a short overview followed by numbered preparation/cooking steps.
           For imported recipes, cite the source URL on the last line, e.g. `Source: <url>`.
        3. Resolve every ingredient with `get_ingredients` (search by name), then use `get_ingredient` or
           `kotlet://ingredients/{ingredientId}` when full measurement details are needed.
        4. If an ingredient does not exist in Kotlet, create it once with `create_ingredient`. The
           ingredient catalog is shared by all households, so search thoroughly (including simpler or
           singular name forms) before creating, and prefer generic names ("Soy sauce", not a brand).
        5. Call `add_recipe` exactly once when the recipe is complete. Include each ingredient's existing
           `ingredientId`, positive `quantity`, `unit`, and optional `note`.
        6. Do not attempt to edit an existing recipe. If the result has validation errors, report them to the user instead of guessing a second creation attempt unless the user explicitly asks you to try again.
        """;

    [McpServerResource(UriTemplate = "kotlet://recipes/{recipeId}", Name = "recipe",
        Title = "Kotlet recipe", MimeType = "application/json"),
     Description("Complete household recipe, including description, servings, ingredients, and images.")]
    public static async Task<string> Recipe(
        Guid recipeId, RecipeService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        Json(await service.GetByIdAsync(recipeId, RequireHouse(currentUser), cancellationToken)
             ?? throw new KeyNotFoundException("Recipe not found."));

    [McpServerPrompt(Name = "create_recipe_flow"),
     Description("Explains how an agent should create a new Kotlet recipe in one shot through MCP.")]
    public static IReadOnlyList<ChatMessage> CreateRecipeFlow() =>
    [
        new(ChatRole.User,
            """
            You can add new Kotlet recipes, but you cannot edit recipes through MCP. Treat recipe creation as a one-shot operation.

            Required flow:
            1. Gather the user's recipe intent, including title, serving count, ingredients, quantities, and any ingredient-specific notes.
               When the user points at an internet source (website, video, blog), extract those details from the source yourself and confirm them with the user before saving.
            2. Use `get_ingredients` to find existing Kotlet ingredient IDs. If a match is ambiguous or you need measurement details, call `get_ingredient`.
            3. If an ingredient genuinely does not exist yet, create it once with `create_ingredient`. The ingredient catalog is shared by all households: search thoroughly first and prefer generic ingredient names over brands.
            4. Compose `descriptionMarkdown` yourself. It must include a concise description and numbered preparation/cooking steps. For imported recipes, end it with a `Source: <url>` line.
            5. Call `add_recipe` once with a complete `CreateRecipeRequest`:
               - `title`: non-empty recipe title.
               - `servings`: positive serving count.
               - `descriptionMarkdown`: overview plus numbered steps.
               - `ingredients`: each item must include an existing `ingredientId`, positive `quantity`, `unit`, and optional `note`.
            6. If `add_recipe` returns validation errors, explain those errors to the user. Do not call an edit recipe tool; none is exposed.
            """)
    ];
}
