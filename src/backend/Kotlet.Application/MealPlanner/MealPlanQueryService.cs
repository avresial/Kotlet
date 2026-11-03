using Kotlet.Application.Ingredients;
using Kotlet.Application.PreparedMeals;
using Kotlet.Application.Recipes;
using Kotlet.Domain.MealPlanner;

namespace Kotlet.Application.MealPlanner;

/// <summary>Reads the household meal plan as day-by-day slots and as a compact overview.</summary>
public sealed class MealPlanQueryService(
    IMealPlanRepository repository,
    IRecipeRepository recipeRepository,
    IIngredientRepository ingredientRepository,
    IPreparedMealRepository? preparedMealRepository = null)
{
    private readonly MealPlanResponseBuilder _responses =
        new(repository, recipeRepository, ingredientRepository, preparedMealRepository);

    public async Task<DailyMealPlanResponse> GetForDateAsync(
        Guid userId, Guid houseId, DateOnly date, CancellationToken cancellationToken) =>
        (await GetForRangeAsync(userId, houseId, date, 1, cancellationToken))[0];

    public async Task<IReadOnlyList<DailyMealPlanResponse>> GetForRangeAsync(
        Guid userId, Guid houseId, DateOnly from, int days, CancellationToken cancellationToken)
    {
        var members = await _responses.GetMemberNamesAsync(houseId, cancellationToken);
        var items = await repository.GetByDateRangeAsync(houseId, from, from.AddDays(days - 1), cancellationToken);
        var recipes = items.Any(item => item.RecipeId.HasValue)
            ? (await recipeRepository.GetAllForDuplicateCheckAsync(houseId, cancellationToken))
                .ToDictionary(recipe => recipe.Id, recipe => recipe.Title)
            : [];
        var ingredientIds = items.Where(item => item.IngredientId.HasValue)
            .Select(item => item.IngredientId!.Value).Distinct().ToArray();
        var ingredients = ingredientIds.Length == 0
            ? []
            : (await ingredientRepository.GetByIdsAsync(ingredientIds, cancellationToken))
                .ToDictionary(entry => entry.Key, entry => entry.Value.Name);
        var preparedMeals = items.Any(item => item.PreparedMealId.HasValue) && preparedMealRepository is not null
            ? (await preparedMealRepository.ListAsync(houseId, includeArchived: true, cancellationToken))
                .ToDictionary(meal => meal.Id, meal => meal.Name)
            : [];

        return Enumerable.Range(0, days).Select(offset =>
        {
            var date = from.AddDays(offset);
            var responses = items.Where(item => item.Date == date).Select(item =>
                MealPlanResponseBuilder.ToResponse(item, userId, members, MealPlanResponseBuilder.ResolveDisplay(item, recipes, ingredients, preparedMeals)))
                .ToList();
            return new DailyMealPlanResponse(
                date.ToString("yyyy-MM-dd"),
                new Dictionary<string, IReadOnlyList<MealPlanItemResponse>>
                {
                    ["breakfast"] = responses.Where(r => r.Slot == "breakfast").OrderBy(r => r.SortOrder).ToList(),
                    ["second-breakfast"] = responses.Where(r => r.Slot == "second-breakfast").OrderBy(r => r.SortOrder).ToList(),
                    ["dinner"] = responses.Where(r => r.Slot == "dinner").OrderBy(r => r.SortOrder).ToList(),
                    ["snack"] = responses.Where(r => r.Slot == "snack").OrderBy(r => r.SortOrder).ToList(),
                    ["supper"] = responses.Where(r => r.Slot == "supper").OrderBy(r => r.SortOrder).ToList()
                });
        }).ToList();
    }

    public async Task<IReadOnlyList<MealHouseMember>> GetHouseMembersAsync(
        Guid houseId, CancellationToken cancellationToken) =>
        await repository.GetHouseMembersAsync(houseId, cancellationToken);

    public async Task<IReadOnlyList<MealPlanOverviewDay>> GetOverviewAsync(
        Guid userId, Guid houseId, DateOnly from, int days, CancellationToken cancellationToken)
    {
        var plans = await GetForRangeAsync(userId, houseId, from, days, cancellationToken);
        return plans
            .Select(plan =>
            {
                var meals = plan.Meals
                    .Where(pair => pair.Value.Count > 0)
                    .ToDictionary(
                        pair => pair.Key,
                        pair => (IReadOnlyList<string>)pair.Value.Select(item => item.DisplayName).ToList());
                return new MealPlanOverviewDay(plan.Date, meals.Keys.Order().ToList(), meals);
            })
            .ToList();
    }
}
