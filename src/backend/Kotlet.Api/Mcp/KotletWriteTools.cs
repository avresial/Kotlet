using System.ComponentModel;
using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Application.Ingredients;
using Kotlet.Application.MealPlanner;
using Kotlet.Application.Pantry;
using Kotlet.Application.Recipes;
using Kotlet.Application.Shopping;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.Pantry;
using ModelContextProtocol.Server;

namespace Kotlet.Api.Mcp;

[McpServerToolType]
public sealed class KotletWriteTools
{
    [McpServerTool(Name = "add_weekly_meal_plan", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Adds up to 21 meals within one seven-day period. Existing identical meals are skipped; existing meals are never replaced.")]
    public static Task<WeeklyMealPlannerOperationResult> AddWeeklyMealPlan(
        [Description("The week start and meals to add. Every meal date must fall within the seven-day period.")]
        AddWeeklyMealPlanRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.AddWeekAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "add_meal_to_plan", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Adds one meal to a single slot on a single day of the household meal plan. A meal is " +
                "either a recipe or a bare ingredient — provide EXACTLY ONE of recipeId or ingredientId, never both. " +
                "IMPORTANT: to plan a recipe you must first look it up — call get_recipes (optionally with a search " +
                "term) to find the recipe and copy its id into recipeId. Never invent or guess a recipe id. " +
                "Likewise resolve a bare ingredient with get_ingredients first. Use add_weekly_meal_plan instead when " +
                "adding several meals across a week. The returned status is Success, NotFound, Conflict, or " +
                "ValidationFailed; on ValidationFailed inspect validationErrors for the offending field.")]
    public static Task<MealPlannerOperationResult> AddMealToPlan(
        [Description("The meal to add. date is yyyy-MM-dd. slot is one of: breakfast, second-breakfast, dinner, " +
                     "snack, supper. Set recipeId to a recipe id obtained from get_recipes, OR ingredientId to an " +
                     "ingredient id from get_ingredients — supply exactly one. note is optional free text.")]
        AddMealPlanItemRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.AddItemAsync(RequireUser(currentUser), RequireHouse(currentUser), request, cancellationToken);

    [McpServerTool(Name = "add_meal_participants", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Adds one or more household members (\"users\") to an already-planned meal, on top of anyone " +
                "already assigned. Obtain the meal's id from get_meal_plan and the member ids from the " +
                "kotlet://meal-plans/members resource (or get_meal_plan_members). Ids that are already assigned are " +
                "ignored, so the call is safe to repeat. Every id must belong to your household or the call returns " +
                "ValidationFailed. This does NOT replace the participant list — use it to grow it.")]
    public static Task<MealPlannerOperationResult> AddMealParticipants(
        [Description("Meal id (the meal-plan item id) from get_meal_plan.")] Guid mealId,
        [Description("Household member ids to add to the meal, from the kotlet://meal-plans/members resource " +
                     "or get_meal_plan_members.")]
        IReadOnlyList<Guid> userIds,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken) =>
        service.AddParticipantsAsync(RequireUser(currentUser), RequireHouse(currentUser), mealId, userIds, cancellationToken);

    [McpServerTool(Name = "remove_meal_from_plan", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Permanently removes one planned meal from the household meal plan. Identify the meal with " +
                "get_meal_plan first — each planned meal carries its own id. This only unplans the meal; the " +
                "underlying recipe and ingredients are left untouched. Returns { removed: true } when a meal was " +
                "deleted and { removed: false } when no meal with that id exists in your household.")]
    public static async Task<object> RemoveMealFromPlan(
        [Description("Meal id (the meal-plan item id) from get_meal_plan.")] Guid mealId,
        MealPlannerService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        new { Removed = await service.RemoveItemAsync(RequireHouse(currentUser), mealId, cancellationToken)
            is MealPlannerOperationStatus.Success };

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

    [McpServerTool(Name = "add_shopping_list_item", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Adds an ingredient to the authenticated household's shopping list. Repeating an existing ingredient does not create a duplicate.")]
    public static Task<ShoppingListOperationResult> AddShoppingListItem(
        [Description("Ingredient ID and positive quantity in the ingredient's measurement unit.")]
        CreateShoppingListItemCommand request,
        ShoppingListService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        service.CreateAsync(RequireHouse(currentUser), request, language.Language, cancellationToken);

    [McpServerTool(Name = "update_shopping_list_item", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Changes the quantity or purchased state of one shopping-list item.")]
    public static Task<ShoppingListOperationResult> UpdateShoppingListItem(
        [Description("Shopping-list item ID from the kotlet://shopping-list resource.")] Guid itemId,
        [Description("New positive quantity and purchased state.")] UpdateShoppingListItemCommand request,
        ShoppingListService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        service.UpdateAsync(itemId, RequireHouse(currentUser), request, language.Language, cancellationToken);

    [McpServerTool(Name = "remove_shopping_list_item", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Permanently removes one item from the authenticated household's shopping list.")]
    public static async Task<object> RemoveShoppingListItem(
        [Description("Shopping-list item ID from the kotlet://shopping-list resource.")] Guid itemId,
        ShoppingListService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        new { Removed = await service.DeleteAsync(itemId, RequireHouse(currentUser), cancellationToken)
            is ShoppingListOperationStatus.Success };

    [McpServerTool(Name = "clear_purchased_shopping_items", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Permanently removes every purchased item from the authenticated household's shopping list.")]
    public static async Task<object> ClearPurchasedShoppingItems(
        ShoppingListService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        new { Removed = await service.ClearPurchasedAsync(RequireHouse(currentUser), cancellationToken) };

    [McpServerTool(Name = "create_ingredient", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Creates a new ingredient in the Kotlet catalog. The catalog is shared by every household, so ALWAYS search get_ingredients first and only create an ingredient when no existing one matches. Typical use: importing a recipe that needs an ingredient Kotlet does not know yet.")]
    public static async Task<McpIngredientOperationResult> CreateIngredient(
        [Description("Complete ingredient to create. Category, allergens, attributes, and suitability use the documented names, not numbers.")]
        CreateIngredientMcpRequest request,
        IngredientService service,
        ILanguageContext language,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!McpEnum.TryParse(request.Category, FoodCategory.Unknown, out var category))
            errors["category"] = [$"Unknown category '{request.Category}'. Use one of: {string.Join(", ", Enum.GetNames<FoodCategory>())}."];
        if (!McpEnum.TryParseFlags<Allergen>(request.Allergens, out var allergens, out var badAllergens))
            errors["allergens"] = [$"Unknown allergen(s): {string.Join(", ", badAllergens)}. Use: {string.Join(", ", Enum.GetNames<Allergen>().Where(n => n != nameof(Allergen.None)))}."];
        if (!McpEnum.TryParseFlags<FoodAttribute>(request.Attributes, out var attributes, out var badAttributes))
            errors["attributes"] = [$"Unknown attribute(s): {string.Join(", ", badAttributes)}. Use: {string.Join(", ", Enum.GetNames<FoodAttribute>().Where(n => n != nameof(FoodAttribute.None)))}."];
        if (!McpEnum.TryParseFlags<DietarySuitability>(request.Suitability, out var suitability, out var badSuitability))
            errors["suitability"] = [$"Unknown suitability value(s): {string.Join(", ", badSuitability)}. Use: {string.Join(", ", Enum.GetNames<DietarySuitability>().Where(n => n != nameof(DietarySuitability.None)))}."];
        if (errors.Count > 0)
            return new(nameof(IngredientOperationStatus.ValidationFailed), ValidationErrors: errors);

        var command = new SaveIngredientCommand(
            request.Name, request.MeasurementUnit, request.IsCountable, request.MeasurementUnitsPerPiece,
            request.CaloriesPer100BaseUnits, request.PricePer100BaseUnits,
            Category: category, Allergens: allergens, Attributes: attributes, Suitability: suitability);
        return McpIngredientOperationResult.From(
            await service.CreateAsync(command, language.Language, cancellationToken));
    }

    [McpServerTool(Name = "add_pantry_item", ReadOnly = false, Destructive = false,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true),
     Description("Adds an ingredient to the authenticated household's pantry. Fails with a conflict when the ingredient is already stored; use update_pantry_item to change its quantity instead.")]
    public static async Task<McpPantryOperationResult> AddPantryItem(
        [Description("Ingredient ID from get_ingredients.")] Guid ingredientId,
        [Description("Quantity on hand, in the ingredient's base measurement unit.")] decimal quantity,
        PantryService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken,
        [Description("Optional expiration date in yyyy-MM-dd format.")] DateOnly? expirationDate = null,
        [Description("Optional storage location: Refrigerator, Freezer, or Cabinet.")] string? storageLocation = null)
    {
        StorageLocation? location = null;
        if (!string.IsNullOrWhiteSpace(storageLocation))
        {
            if (!McpEnum.TryParse(storageLocation, StorageLocation.Cabinet, out var parsed))
                return new(nameof(PantryOperationStatus.ValidationFailed), ValidationErrors: new Dictionary<string, string[]>
                {
                    ["storageLocation"] = [$"Unknown storage location '{storageLocation}'. Use: {string.Join(", ", Enum.GetNames<StorageLocation>())}."]
                });
            location = parsed;
        }
        return McpPantryOperationResult.From(await service.CreateAsync(
            RequireHouse(currentUser),
            new SavePantryItemCommand(ingredientId, quantity, expirationDate, location),
            language.Language, cancellationToken));
    }

    [McpServerTool(Name = "update_pantry_item", ReadOnly = false, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Sets the stored quantity of one pantry item.")]
    public static async Task<McpPantryOperationResult> UpdatePantryItem(
        [Description("Pantry item ID from get_pantry.")] Guid itemId,
        [Description("New quantity on hand, zero or positive, in the ingredient's base measurement unit.")] decimal quantity,
        PantryService service, ICurrentUser currentUser, ILanguageContext language,
        CancellationToken cancellationToken) =>
        McpPantryOperationResult.From(await service.UpdateAsync(
            itemId, RequireHouse(currentUser), quantity, language.Language, cancellationToken));

    [McpServerTool(Name = "remove_pantry_item", ReadOnly = false, Destructive = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true),
     Description("Permanently removes one item from the authenticated household's pantry.")]
    public static async Task<object> RemovePantryItem(
        [Description("Pantry item ID from get_pantry.")] Guid itemId,
        PantryService service, ICurrentUser currentUser, CancellationToken cancellationToken) =>
        new { Removed = await service.DeleteAsync(itemId, RequireHouse(currentUser), cancellationToken)
            is PantryOperationStatus.Success };

    private static Guid RequireUser(ICurrentUser currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("The authenticated user is unavailable.");

    private static Guid RequireHouse(ICurrentUser currentUser) =>
        currentUser.HouseId ?? throw new UnauthorizedAccessException(
            "No active household is available. Select a household in Kotlet and reconnect this MCP server.");
}
