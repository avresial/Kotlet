using Kotlet.Domain.Ingredients;

namespace Kotlet.Application.Ingredients;

/// <summary>
/// Resolves a batch of ingredient names against the shared catalog in one pass, so agents
/// importing a recipe do not need one search (and possibly one create) call per ingredient.
/// Exact matches win; uncertain matches are reported as ambiguous instead of guessed.
/// </summary>
public sealed class IngredientBatchResolutionService(IngredientService ingredientService)
{
    private const int MaxAmbiguousMatches = 10;
    private const decimal DefaultMeasurementUnitsPerPiece = 100;

    public async Task<IngredientBatchResolutionResult> ResolveAsync(
        IReadOnlyList<IngredientResolutionCandidate> candidates,
        bool createMissing,
        string languageCode,
        CancellationToken cancellationToken)
    {
        // One catalog load serves the whole batch; ingredients created along the way are
        // appended so duplicate names within the same batch resolve to the same ingredient.
        var catalog = (await ingredientService.GetAllAsync(languageCode, cancellationToken)).ToList();

        var resolved = new List<ResolvedIngredientEntry>();
        var ambiguous = new List<AmbiguousIngredientEntry>();
        var missing = new List<MissingIngredientEntry>();

        foreach (var candidate in candidates)
        {
            var inputName = candidate.Name?.Trim() ?? string.Empty;
            if (inputName.Length == 0)
            {
                missing.Add(new(candidate.Name ?? string.Empty, "Ingredient name is required."));
                continue;
            }

            var exactMatches = catalog.Where(dto => IsExactMatch(dto, inputName)).ToList();
            if (exactMatches.Count == 1)
            {
                resolved.Add(new(inputName, exactMatches[0].Id, exactMatches[0].Name, IngredientResolutionStatus.Existing));
                continue;
            }
            if (exactMatches.Count > 1)
            {
                ambiguous.Add(ToAmbiguous(inputName, exactMatches));
                continue;
            }

            var partialMatches = catalog.Where(dto => IsPartialMatch(dto, inputName)).ToList();
            if (partialMatches.Count > 0)
            {
                ambiguous.Add(ToAmbiguous(inputName, partialMatches));
                continue;
            }

            if (!createMissing)
            {
                missing.Add(new(inputName, "Not found and createMissing=false."));
                continue;
            }

            var creation = await CreateMissingAsync(candidate, inputName, languageCode, cancellationToken);
            if (creation.Created is not null)
            {
                catalog.Add(creation.Created);
                resolved.Add(new(inputName, creation.Created.Id, creation.Created.Name, IngredientResolutionStatus.Created));
            }
            else
            {
                missing.Add(new(inputName, creation.FailureReason!));
            }
        }

        return new(resolved, ambiguous, missing);
    }

    private async Task<(IngredientDto? Created, string? FailureReason)> CreateMissingAsync(
        IngredientResolutionCandidate candidate, string inputName, string languageCode, CancellationToken cancellationToken)
    {
        var (unit, isCountable, unitsPerPiece) = MapExpectedUnit(candidate);
        var category = ParseCategoryHint(candidate.CategoryHint);
        var command = new SaveIngredientCommand(
            inputName, unit, isCountable, unitsPerPiece,
            candidate.CaloriesPer100BaseUnits ?? 0, PricePer100BaseUnits: 0,
            Category: category);

        var result = await ingredientService.CreateAsync(command, languageCode, cancellationToken);
        if (result.Status == IngredientOperationStatus.Success)
            return (result.Ingredient, null);

        var reason = result.ValidationErrors is { Count: > 0 }
            ? string.Join(" ", result.ValidationErrors.SelectMany(error => error.Value))
            : result.Message ?? $"Ingredient could not be created ({result.Status}).";
        return (null, reason);
    }

    /// <summary>
    /// Maps the optional unit hint to the catalog's base units. "pcs" ingredients are stored
    /// as countable grams; without a per-piece weight a common-sense default keeps piece-based
    /// recipe quantities usable, matching the issue's "default when data is incomplete" rule.
    /// </summary>
    private static (string Unit, bool IsCountable, decimal? UnitsPerPiece) MapExpectedUnit(
        IngredientResolutionCandidate candidate) =>
        candidate.ExpectedUnit?.Trim().ToLowerInvariant() switch
        {
            "ml" => ("ml", false, null),
            "pcs" or "piece" or "pieces" => ("g", true, candidate.MeasurementUnitsPerPiece ?? DefaultMeasurementUnitsPerPiece),
            _ => ("g", false, null)
        };

    private static FoodCategory ParseCategoryHint(string? hint)
    {
        // A hint is best-effort: unknown names fall back to Unknown instead of failing the batch.
        if (string.IsNullOrWhiteSpace(hint) || long.TryParse(hint, out _))
            return FoodCategory.Unknown;
        return Enum.TryParse<FoodCategory>(hint, ignoreCase: true, out var category) && Enum.IsDefined(category)
            ? category
            : FoodCategory.Unknown;
    }

    private static AmbiguousIngredientEntry ToAmbiguous(string inputName, List<IngredientDto> matches) =>
        new(inputName, matches
            .Take(MaxAmbiguousMatches)
            .Select(dto => new IngredientNameMatch(dto.Id, dto.Name))
            .ToList());

    private static bool IsExactMatch(IngredientDto dto, string inputName) =>
        PluralInsensitiveEquals(dto.Name, inputName) || PluralInsensitiveEquals(dto.DefaultName, inputName);

    // "Chickpea" should find "Chickpeas" without dragging in genuinely different ingredients.
    private static bool PluralInsensitiveEquals(string catalogName, string inputName)
    {
        var name = catalogName.Trim();
        return Equals(name, inputName)
            || Equals(name, inputName + "s") || Equals(name, inputName + "es")
            || Equals(name + "s", inputName) || Equals(name + "es", inputName);

        static bool Equals(string left, string right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPartialMatch(IngredientDto dto, string inputName) =>
        ContainsEitherWay(dto.Name, inputName) || ContainsEitherWay(dto.DefaultName, inputName);

    private static bool ContainsEitherWay(string catalogName, string inputName)
    {
        var name = catalogName.Trim();
        return name.Contains(inputName, StringComparison.OrdinalIgnoreCase)
            || inputName.Contains(name, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record IngredientResolutionCandidate(
    string Name,
    string? ExpectedUnit = null,
    string? CategoryHint = null,
    decimal? CaloriesPer100BaseUnits = null,
    decimal? MeasurementUnitsPerPiece = null);

public enum IngredientResolutionStatus
{
    Existing,
    Created
}

public sealed record ResolvedIngredientEntry(
    string InputName, Guid IngredientId, string MatchedName, IngredientResolutionStatus Status);

public sealed record IngredientNameMatch(Guid IngredientId, string Name);

public sealed record AmbiguousIngredientEntry(string InputName, IReadOnlyList<IngredientNameMatch> Matches);

public sealed record MissingIngredientEntry(string InputName, string Reason);

public sealed record IngredientBatchResolutionResult(
    IReadOnlyList<ResolvedIngredientEntry> Resolved,
    IReadOnlyList<AmbiguousIngredientEntry> Ambiguous,
    IReadOnlyList<MissingIngredientEntry> Missing);
