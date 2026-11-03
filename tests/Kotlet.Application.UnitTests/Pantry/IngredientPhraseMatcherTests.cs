using Kotlet.Application.Pantry;
using Kotlet.Application.Translations;
using Kotlet.Domain.Ingredients;
using Xunit;

namespace Kotlet.Application.UnitTests.Pantry;

public sealed class IngredientPhraseMatcherTests
{
    private static readonly Ingredient Milk = new() { Id = Guid.NewGuid(), Name = "Milk", MeasurementUnit = "ml" };
    private static readonly Ingredient Butter = new() { Id = Guid.NewGuid(), Name = "Butter", MeasurementUnit = "g" };

    [Theory]
    [InlineData("  whole-milk, 3.2% ", "WHOLE MILK 3 2")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void NormalizePhrase_UppercasesAndCollapsesNonAlphanumerics(string? phrase, string expected)
    {
        Assert.Equal(expected, IngredientPhraseMatcher.NormalizePhrase(phrase));
    }

    [Fact]
    public void Match_WithExactName_ReturnsMatchedWithFullConfidence()
    {
        var matcher = new IngredientPhraseMatcher([Milk, Butter], new Dictionary<string, string>());

        var match = matcher.Match(IngredientPhraseMatcher.NormalizePhrase("milk"));

        Assert.Equal(PhraseMatchKind.Matched, match.Kind);
        Assert.Equal(Milk.Id, match.Candidates[0].Id);
        Assert.Equal(1m, match.Candidates[0].Confidence);
    }

    [Fact]
    public void Match_WithTranslatedName_ReturnsIngredient()
    {
        var language = TranslationKeys.TranslatedLanguages[0];
        var translations = new Dictionary<string, string>
        {
            [TranslationKeys.Ingredient(Butter.Id, language)] = "Masło"
        };
        var matcher = new IngredientPhraseMatcher([Milk, Butter], translations);

        var match = matcher.Match(IngredientPhraseMatcher.NormalizePhrase("masło"));

        Assert.Equal(PhraseMatchKind.Matched, match.Kind);
        Assert.Equal(Butter.Id, match.Candidates[0].Id);
        Assert.Equal("Masło", match.Candidates[0].Name);
    }

    [Fact]
    public void Match_WithUnrelatedPhrase_ReturnsUnmatched()
    {
        var matcher = new IngredientPhraseMatcher([Milk, Butter], new Dictionary<string, string>());

        var match = matcher.Match(IngredientPhraseMatcher.NormalizePhrase("ketchup"));

        Assert.Equal(PhraseMatchKind.Unmatched, match.Kind);
    }

    [Fact]
    public void Match_WithTwoCloseCandidates_ReturnsAmbiguous()
    {
        var oatMilk = new Ingredient { Id = Guid.NewGuid(), Name = "Oat milk", MeasurementUnit = "ml" };
        var goatMilk = new Ingredient { Id = Guid.NewGuid(), Name = "Goat milk", MeasurementUnit = "ml" };
        var matcher = new IngredientPhraseMatcher([oatMilk, goatMilk], new Dictionary<string, string>());

        var match = matcher.Match(IngredientPhraseMatcher.NormalizePhrase("boat milk"));

        Assert.Equal(PhraseMatchKind.Ambiguous, match.Kind);
        Assert.Equal(2, match.Candidates.Count);
    }

    [Fact]
    public void Similarity_UsesEditDistanceOverLongestLength()
    {
        Assert.Equal(1m, IngredientPhraseMatcher.Similarity("MILK", "MILK"));
        Assert.Equal(0.75m, IngredientPhraseMatcher.Similarity("MILK", "SILK"));
    }
}
