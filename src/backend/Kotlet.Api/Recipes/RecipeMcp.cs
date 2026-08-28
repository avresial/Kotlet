using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Api.Mcp;
using Kotlet.Api.Localization;
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
    [McpServerTool(Name = "get_recipes", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns compact recipe candidates for meal planning. Each result includes its ID, servings, meal type, ingredient IDs/names, and full-detail resource URI. Filter by title, meal type, and/or ingredients; ingredientIds means the recipe must contain every supplied ingredient.")]
    public static async Task<McpRecipeSearchResponse> GetRecipes(
        RecipeService service,
        ICurrentUser currentUser,
        [Description("Page number, starting at 1.")] int page = 1,
        [Description("Compact results per page, from 1 to 20. Keep this small; default 10.")] int pageSize = 10,
        [Description("Optional recipe-title search.")] string? search = null,
        [Description("Optional slot: breakfast, second-breakfast, dinner, snack, or supper.")] string? mealType = null,
        [Description("Optional ingredient IDs from get_ingredients. Results contain every supplied ingredient.")] IReadOnlyList<Guid>? ingredientIds = null,
        CancellationToken cancellationToken = default) =>
        McpRecipeSearchResponse.From(await service.ListForPlanningAsync(
            RequireHouse(currentUser), page, pageSize, search, mealType, ingredientIds, cancellationToken));

    [McpServerTool(Name = "get_recipe", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Returns one complete household recipe: Markdown description with preparation steps, servings, the full ingredient list with quantities, source URL, and optional playable video metadata.")]
    public static async Task<RecipeDetailResponse> GetRecipe(
        [Description("Recipe ID from get_recipes.")] Guid recipeId,
        RecipeService service,
        ICurrentUser currentUser,
        ILanguageContext language,
        CancellationToken cancellationToken) =>
        await service.GetByIdAsync(recipeId, RequireHouse(currentUser), cancellationToken, language.Language)
        ?? throw new McpException("Recipe not found.");

    [McpServerTool(Name = "check_recipe_exists", ReadOnly = true, OpenWorld = false, UseStructuredContent = true),
     Description("Checks whether a household recipe already exists before adding it, so imports do not create duplicates. Provide the source URL and/or the title; the strongest signal is a matching recipe source URL, then an exact title match, then similar titles. Call this before add_recipe when importing a recipe.")]
    public static async Task<McpRecipeExistenceResult> CheckRecipeExists(
        RecipeDuplicateDetectionService service,
        ICurrentUser currentUser,
        [Description("Recipe title to check. Matching is case-insensitive and tolerant of punctuation.")]
        string? title = null,
        [Description("Source URL of the recipe being imported, e.g. the web page it came from.")]
        string? sourceUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(sourceUrl))
            throw new McpException("Provide at least one of title or sourceUrl.");
        return McpRecipeExistenceResult.From(
            await service.CheckExistsAsync(RequireHouse(currentUser), title, sourceUrl, cancellationToken));
    }

    [McpServerTool(Name = "add_recipe", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Creates one new household recipe. This is an add-only one-shot tool: find every ingredient with get_ingredients first, create genuinely missing ones only after user confirmation, then call this once with quantities, units, optional notes, servings, and a Markdown description with preparation steps. When importing a recipe from the internet, set sourceUrl to the source page, set isAiAssisted to true, and use videoUrl plus videoThumbnailUrl when a direct playable video and poster are available. Read the kotlet://recipes/new-recipe-guide resource for the full workflow.")]
    public static Task<RecipeOperationResult> AddRecipe(
        [Description("Complete recipe to create. DescriptionMarkdown should include a concise overview and numbered cooking steps. Ingredients must use existing ingredient IDs from get_ingredients or kotlet://ingredients/{ingredientId} resources.")]
        CreateRecipeRequest request,
        RecipeService service,
        ICurrentUser currentUser,
        ILanguageContext language,
        CancellationToken cancellationToken) =>
        service.CreateAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken, language.Language);

    [McpServerTool(Name = "update_recipe", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Replaces the editable details and complete ingredient list of one household recipe. " +
                 "Call get_recipe first and carry forward every field that should remain unchanged. " +
                 "Use videoUrl and videoThumbnailUrl for native playback instead of adding a video link to the Markdown description. " +
                 "Images, ownership, and AI-assisted provenance are preserved.")]
    public static Task<RecipeOperationResult> UpdateRecipe(
        [Description("Recipe ID from get_recipes or get_recipe.")] Guid recipeId,
        [Description("Complete replacement recipe details. Include every ingredient that should remain on the recipe.")]
        UpdateRecipeRequest request,
        RecipeService service,
        ICurrentUser currentUser,
        ILanguageContext language,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(recipeId, RequireHouse(currentUser), request, cancellationToken, language.Language);

    [McpServerResource(UriTemplate = "kotlet://recipes/new-recipe-guide", Name = "new-recipe-guide",
        Title = "New recipe creation guide", MimeType = "text/markdown"),
     Description("Instructions for creating a new Kotlet recipe through MCP and correcting it later when needed.")]
    public static string NewRecipeGuide() =>
        """
        # New recipe creation flow

        Use this resource before calling the `add_recipe` tool. Recipe creation is one-shot, but an
        existing recipe can be corrected later with `update_recipe` after reading its complete state.

        1. Understand the requested recipe and decide on a title, servings, and a Markdown description.
           When the recipe comes from the internet (a website, video, or blog), review it with the user
           first: extract the title, servings, ingredient quantities, and steps from the source yourself.
        2. Check for duplicates with `check_recipe_exists`, passing the source URL (when importing) and
           the title. If it reports a match, tell the user instead of adding the recipe again.
        3. Write `descriptionMarkdown` with a short overview followed by numbered preparation/cooking steps.
           For imported recipes, set `sourceUrl` to the source page and set `isAiAssisted` to `true`
           so the recipe is marked accordingly in the app. When the source exposes a direct browser-
           playable video file, set `videoUrl` to that media URL and, when available,
           `videoThumbnailUrl` to its poster image. Keep social-post and web-page URLs in `sourceUrl`;
           never put them in `videoUrl` and never append a video link to `descriptionMarkdown`.
        4. Search all ingredients in ONE call with `get_ingredients`. It returns the closest catalog
           name for each input across all languages, including its language, measurement unit, resource
           URI, exact-match status, edit distance, and normalized similarity. Use the returned ingredient
           only when the name is genuinely equivalent.
           Prefer generic names ("Soy sauce", not a brand); the catalog is shared by all households.
        5. Check the result before adding the recipe:
           - If every closest match is correct, proceed to step 6.
           - If a result is not the same ingredient, list that input to the user and ask whether to add it.
             Only when the user agrees, add it with `create_ingredient`. Do not invent ingredients the
             user has not approved.
        6. Call `add_recipe` exactly once when every ingredient is resolved. Include each ingredient's
           `ingredientId`, positive `quantity`, the `unit` (use the resolved `measurementUnit`), and an
           optional `note`.
        7. If the result has validation errors, report them to the user instead of guessing a second creation attempt unless the user explicitly asks you to try again.
        8. For a later correction, call `get_recipe` first, preserve every field and ingredient that
           should remain unchanged, obtain user approval for the exact replacement, then call
           `update_recipe` once with the complete recipe details. Images, ownership, and AI-assisted
           provenance are preserved automatically.
        """;

    [McpServerResource(UriTemplate = "kotlet://recipes/edit-recipe-guide", Name = "edit-recipe-guide",
        Title = "Recipe editing guide", MimeType = "text/markdown"),
     Description("Instructions for safely editing an existing Kotlet recipe through MCP.")]
    public static string EditRecipeGuide() =>
        """
        # Existing recipe update flow

        `update_recipe` replaces all editable recipe fields and the complete ingredient list.

        1. Call `get_recipe` and treat its result as the replacement baseline.
        2. Apply only the change the user requested. Preserve the title, description, servings,
           meal type, source URL, native video URL, video thumbnail URL, and every ingredient that
           should remain unchanged.
        3. Resolve any newly added ingredient with `get_ingredients`. Ask before creating a missing
           shared-catalog ingredient with `create_ingredient`.
        4. Show the exact replacement to the user when the requested change is ambiguous or would
           remove or replace recipe data.
        5. Call `update_recipe` once. Include the recipe id and a complete `UpdateRecipeRequest`.

        To attach a playable film, set `videoUrl` to a direct browser-playable media URL and optionally
        set `videoThumbnailUrl` to its poster. Keep the source page in `sourceUrl`; never append the
        film link to `descriptionMarkdown`. Recipe images, ownership, and AI-assisted provenance are
        not part of the request and remain unchanged. If validation fails, report the error instead
        of retrying with guessed values.
        """;

    [McpServerResource(UriTemplate = "kotlet://recipes/{recipeId}", Name = "recipe",
        Title = "Kotlet recipe", MimeType = "application/json"),
     Description("Complete household recipe, including description, servings, ingredients, images, and optional playable video metadata.")]
    public static async Task<string> Recipe(
        Guid recipeId, RecipeService service, ICurrentUser currentUser, ILanguageContext language, CancellationToken cancellationToken) =>
        Json(await service.GetByIdAsync(recipeId, RequireHouse(currentUser), cancellationToken, language.Language)
             ?? throw new KeyNotFoundException("Recipe not found."));

    [McpServerPrompt(Name = "create_recipe_flow"),
     Description("Explains how an agent should create a new Kotlet recipe in one shot through MCP.")]
    public static IReadOnlyList<ChatMessage> CreateRecipeFlow() =>
    [
        new(ChatRole.User,
            """
            Treat recipe creation as a one-shot operation. Existing recipes can be corrected later with
            `update_recipe`, but only after reading their complete state with `get_recipe`.

            Required flow:
            1. Gather the user's recipe intent, including title, serving count, ingredients, quantities, and any ingredient-specific notes.
               When the user points at an internet source (website, video, blog), extract those details from the source yourself and confirm them with the user before saving.
            2. Call `check_recipe_exists` with the source URL and/or title first; if the recipe already exists, report the match to the user instead of adding a duplicate.
            3. Search all ingredient names in one call with `get_ingredients`. It returns the closest match across all languages with its measurement unit, exact-match status, edit distance, and normalized similarity. Accept only genuinely equivalent names. For any input whose closest result is a different ingredient, ask the user whether to create it; only after approval use `create_ingredient`. Prefer generic ingredient names over brands.
            4. Compose `descriptionMarkdown` yourself. It must include a concise description and numbered preparation/cooking steps.
            5. Call `add_recipe` once with a complete `CreateRecipeRequest`:
               - `title`: non-empty recipe title.
               - `servings`: positive serving count.
               - `descriptionMarkdown`: overview plus numbered steps.
               - `ingredients`: each item must include an existing `ingredientId`, positive `quantity`, `unit`, and optional `note`.
               - For imported recipes: `sourceUrl` set to the source page, and `isAiAssisted` set to `true`.
               - When available: `videoUrl` set to a direct browser-playable media URL and
                 `videoThumbnailUrl` set to its poster image. Never put a social-post or page URL in
                 `videoUrl`, and never append a video link to `descriptionMarkdown`.
            6. If `add_recipe` returns validation errors, explain those errors to the user rather than retrying with guessed values.
            """)
    ];

    [McpServerPrompt(Name = "update_recipe_flow"),
     Description("Explains how an agent should safely replace an existing recipe through MCP.")]
    public static IReadOnlyList<ChatMessage> UpdateRecipeFlow() =>
    [
        new(ChatRole.User,
            """
            To update an existing Kotlet recipe:
            1. Call `get_recipe` first and use its complete result as the baseline.
            2. Apply only the requested change. Preserve every other editable field and ingredient,
               including `sourceUrl`, `videoUrl`, and `videoThumbnailUrl`.
            3. Resolve newly added ingredients with `get_ingredients`; ask before creating a missing one.
            4. To attach a film, set `videoUrl` to a direct browser-playable media URL and optionally
               set `videoThumbnailUrl` to its poster. Keep the source page in `sourceUrl`; never append
               the film link to `descriptionMarkdown`.
            5. Call `update_recipe` once with the recipe id and a complete replacement request.
            Recipe images, ownership, and AI-assisted provenance are preserved automatically.
            If validation fails, report the error instead of retrying with guessed values.
            """)
    ];
}
