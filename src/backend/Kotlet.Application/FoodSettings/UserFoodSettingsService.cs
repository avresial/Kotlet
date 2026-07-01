using Kotlet.Domain.FoodSettings;
using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.FoodSettings;

public sealed record UserFoodSettingsDto(Allergen AvoidedAllergens, FoodAttribute AvoidedAttributes,
    DietarySuitability RequiredSuitability, Guid[] ExcludedIngredientIds);
public sealed record SaveUserFoodSettingsCommand(Allergen AvoidedAllergens, FoodAttribute AvoidedAttributes,
    DietarySuitability RequiredSuitability, Guid[]? ExcludedIngredientIds);
public sealed record UserFoodSettingsResult(UserFoodSettingsDto? Settings = null,
    Dictionary<string, string[]>? ValidationErrors = null);

public sealed class UserFoodSettingsService(IUserFoodSettingsRepository repository)
{
    private static readonly Allergen KnownAllergens = Enum.GetValues<Allergen>().Aggregate((a, b) => a | b);
    private static readonly FoodAttribute KnownAttributes = Enum.GetValues<FoodAttribute>().Aggregate((a, b) => a | b);
    private static readonly DietarySuitability KnownSuitability = Enum.GetValues<DietarySuitability>().Aggregate((a, b) => a | b);

    public async Task<UserFoodSettingsDto> GetAsync(Guid userId, CancellationToken ct) =>
        ToDto(await repository.GetAsync(userId, false, ct));

    public async Task<UserFoodSettingsResult> SaveAsync(Guid userId, SaveUserFoodSettingsCommand command, CancellationToken ct)
    {
        var ids = (command.ExcludedIngredientIds ?? []).Distinct().ToArray();
        var errors = Validate(command);
        if (ids.Length > 100) errors["excludedIngredientIds"] = ["At most 100 ingredients can be excluded."];
        else if ((await repository.ExistingIngredientIdsAsync(ids, ct)).Length != ids.Length)
            errors["excludedIngredientIds"] = ["One or more excluded ingredients do not exist."];
        if (errors.Count > 0) return new(ValidationErrors: errors);

        var settings = await repository.GetAsync(userId, true, ct);
        if (settings is null)
        {
            settings = new UserFoodSettings { UserId = userId };
            repository.Add(settings);
        }
        settings.AvoidedAllergens = command.AvoidedAllergens;
        settings.AvoidedAttributes = command.AvoidedAttributes;
        settings.RequiredSuitability = command.RequiredSuitability;
        foreach (var excluded in settings.ExcludedIngredients.Where(x => !ids.Contains(x.IngredientId)).ToArray())
            settings.ExcludedIngredients.Remove(excluded);
        foreach (var id in ids.Where(id => settings.ExcludedIngredients.All(x => x.IngredientId != id)))
            settings.ExcludedIngredients.Add(new() { UserId = userId, IngredientId = id });
        await repository.SaveChangesAsync(ct);
        return new(ToDto(settings));
    }

    private static Dictionary<string, string[]> Validate(SaveUserFoodSettingsCommand command)
    {
        var errors = new Dictionary<string, string[]>();
        if ((command.AvoidedAllergens & ~KnownAllergens) != 0) errors["avoidedAllergens"] = ["Allergens contain unsupported values."];
        if ((command.AvoidedAttributes & ~KnownAttributes) != 0) errors["avoidedAttributes"] = ["Attributes contain unsupported values."];
        if ((command.RequiredSuitability & ~KnownSuitability) != 0) errors["requiredSuitability"] = ["Suitability contains unsupported values."];
        return errors;
    }

    private static UserFoodSettingsDto ToDto(UserFoodSettings? settings) => settings is null
        ? new(Allergen.None, FoodAttribute.None, DietarySuitability.None, [])
        : new(settings.AvoidedAllergens, settings.AvoidedAttributes, settings.RequiredSuitability,
            settings.ExcludedIngredients.Select(x => x.IngredientId).ToArray());
}
