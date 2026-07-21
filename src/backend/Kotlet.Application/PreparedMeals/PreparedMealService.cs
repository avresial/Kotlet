using Kotlet.Application.Ingredients;
using Kotlet.Domain.Common;
using Kotlet.Domain.PreparedMeals;

namespace Kotlet.Application.PreparedMeals;

public sealed class PreparedMealService(IPreparedMealRepository repository, IIngredientRepository ingredients)
{
    public async Task<IReadOnlyList<PreparedMealResponse>> ListAsync(Guid houseId, bool includeArchived, CancellationToken ct) =>
        (await repository.ListAsync(houseId, includeArchived, ct)).Select(ToResponse).ToList();

    public async Task<PreparedMealResponse?> GetAsync(Guid id, Guid houseId, CancellationToken ct) =>
        await repository.GetAsync(id, houseId, false, ct) is { } meal ? ToResponse(meal) : null;

    public async Task<PreparedMealOperationResult> CreateAsync(Guid houseId, SavePreparedMealRequest request, CancellationToken ct)
    {
        var errors = await ValidateAsync(request, ct);
        if (errors.Count > 0) return new(PreparedMealOperationStatus.ValidationFailed, ValidationErrors: errors);
        var now = DateTimeOffset.UtcNow;
        var meal = new PreparedMeal { Id = Guid.NewGuid(), HouseId = houseId, Name = request.Name.Trim(), CreatedAtUtc = now, UpdatedAtUtc = now };
        Apply(meal, request);
        repository.Add(meal);
        await repository.SaveChangesAsync(ct);
        return new(PreparedMealOperationStatus.Success, ToResponse(meal));
    }

    public async Task<PreparedMealOperationResult> UpdateAsync(Guid id, Guid houseId, SavePreparedMealRequest request, CancellationToken ct)
    {
        var meal = await repository.GetAsync(id, houseId, true, ct);
        if (meal is null) return new(PreparedMealOperationStatus.NotFound);
        var errors = await ValidateAsync(request, ct);
        if (errors.Count > 0) return new(PreparedMealOperationStatus.ValidationFailed, ValidationErrors: errors);
        meal.Addons.Clear();
        Apply(meal, request);
        meal.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(ct);
        return new(PreparedMealOperationStatus.Success, ToResponse(meal));
    }

    public async Task<PreparedMealOperationStatus> SetArchivedAsync(Guid id, Guid houseId, bool archived, CancellationToken ct)
    {
        var meal = await repository.GetAsync(id, houseId, true, ct);
        if (meal is null) return PreparedMealOperationStatus.NotFound;
        meal.IsArchived = archived;
        meal.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync(ct);
        return PreparedMealOperationStatus.Success;
    }

    private async Task<Dictionary<string, string[]>> ValidateAsync(SavePreparedMealRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors["name"] = ["Name is required."];
        if (request.Servings <= 0) errors["servings"] = ["Servings must be greater than zero."];
        if (request.Price < 0) errors["price"] = ["Price cannot be negative."];
        if (request.CaloriesPerServing < 0) errors["caloriesPerServing"] = ["Calories cannot be negative."];
        if (request.PackageQuantity <= 0) errors["packageQuantity"] = ["Package quantity must be greater than zero."];
        if (request.Addons.GroupBy(a => a.IngredientId).Any(g => g.Count() > 1)) errors["addons"] = ["Duplicate ingredients are not allowed."];
        for (var i = 0; i < request.Addons.Count; i++)
        {
            var addon = request.Addons[i];
            if (addon.Quantity <= 0) errors[$"addons[{i}].quantity"] = ["Quantity must be greater than zero."];
            if (string.IsNullOrWhiteSpace(addon.Unit)) errors[$"addons[{i}].unit"] = ["Unit is required."];
            if (await ingredients.GetByIdAsync(addon.IngredientId, false, ct) is null) errors[$"addons[{i}].ingredientId"] = ["Ingredient not found."];
        }
        if (request.ShoppingIngredientId is { } id && await ingredients.GetByIdAsync(id, false, ct) is null)
            errors["shoppingIngredientId"] = ["Ingredient not found."];
        return errors;
    }

    private static void Apply(PreparedMeal meal, SavePreparedMealRequest request)
    {
        meal.Name = request.Name.Trim(); meal.Description = request.Description?.Trim(); meal.Brand = request.Brand?.Trim();
        meal.Store = request.Store?.Trim(); meal.Category = request.Category?.Trim(); meal.PackageQuantity = request.PackageQuantity;
        meal.PackageUnit = request.PackageUnit?.Trim(); meal.Servings = request.Servings;
        meal.CaloriesPerServing = request.CaloriesPerServing;
        meal.Price = request.Price;
        meal.PreparationInstructions = request.PreparationInstructions?.Trim(); meal.ShoppingIngredientId = request.ShoppingIngredientId;
        foreach (var addon in request.Addons.OrderBy(a => a.SortOrder)) meal.Addons.Add(new PreparedMealAddon
        { Id = Guid.NewGuid(), IngredientId = addon.IngredientId, DefaultQuantity = Quantity.FromAmount(addon.Quantity), Unit = addon.Unit.Trim(), IsSelectedByDefault = addon.IsSelectedByDefault || addon.IsRequired, IsRequired = addon.IsRequired, SortOrder = addon.SortOrder });
    }

    private static PreparedMealResponse ToResponse(PreparedMeal meal) => new(meal.Id, meal.Name, meal.Description, meal.Brand,
        meal.Store, meal.Category, meal.PackageQuantity, meal.PackageUnit, meal.Servings, meal.CaloriesPerServing,
        meal.Price, meal.PreparationInstructions, meal.ShoppingIngredientId, meal.IsArchived,
        meal.Addons.OrderBy(a => a.SortOrder).Select(a => new PreparedMealAddonResponse(a.Id, a.IngredientId,
            a.Ingredient?.Name ?? "Unknown ingredient", a.DefaultQuantity.Amount, a.Unit, a.IsSelectedByDefault, a.IsRequired, a.SortOrder)).ToList());
}
