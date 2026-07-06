using Kotlet.Application.Ai;
using Kotlet.Application.Ingredients;
using Kotlet.Application.Translations;
using Kotlet.Domain.Ingredients;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kotlet.Application.UnitTests.Ingredients;

public sealed class IngredientTranslationServiceTests
{
    private const string Polish = "pl";

    [Fact]
    public async Task Backfill_WhenNoApplicationCredentials_IsNoOp()
    {
        var translations = new FakeTranslationRepository();
        var service = new IngredientTranslationService(
            new FakeIngredientRepository(Ingredient("Flour")),
            translations,
            new StubResolver(null), NullLogger<IngredientTranslationService>.Instance);

        var result = await service.BackfillMissingTranslationsAsync(CancellationToken.None);

        Assert.False(result.ProviderConfigured);
        Assert.Equal(0, result.Written);
        Assert.Empty(translations.Entries);
    }

    [Fact]
    public async Task Backfill_TranslatesIngredientsMissingTranslations()
    {
        var flour = Ingredient("Flour");
        var sugar = Ingredient("Sugar");
        var translations = new FakeTranslationRepository();
        var service = new IngredientTranslationService(
            new FakeIngredientRepository(flour, sugar),
            translations,
            new StubResolver(new StubChatClient()), NullLogger<IngredientTranslationService>.Instance);

        var result = await service.BackfillMissingTranslationsAsync(CancellationToken.None);

        Assert.True(result.ProviderConfigured);
        Assert.Equal(2, result.Written);
        Assert.Equal("Flour (pl)", translations.Entries[TranslationKeys.Ingredient(flour.Id, Polish)]);
        Assert.Equal("Sugar (pl)", translations.Entries[TranslationKeys.Ingredient(sugar.Id, Polish)]);
        Assert.Equal(1, translations.SaveCount);
    }

    [Fact]
    public async Task Backfill_SkipsIngredientsThatAlreadyHaveTranslations()
    {
        var flour = Ingredient("Flour");
        var translations = new FakeTranslationRepository();
        translations.Entries[TranslationKeys.Ingredient(flour.Id, Polish)] = "Mąka";
        var service = new IngredientTranslationService(
            new FakeIngredientRepository(flour),
            translations,
            new StubResolver(new StubChatClient()), NullLogger<IngredientTranslationService>.Instance);

        var result = await service.BackfillMissingTranslationsAsync(CancellationToken.None);

        Assert.Equal(0, result.Written);
        // The existing human translation is left untouched, and nothing is committed.
        Assert.Equal("Mąka", translations.Entries[TranslationKeys.Ingredient(flour.Id, Polish)]);
        Assert.Equal(0, translations.SaveCount);
    }

    [Fact]
    public async Task Backfill_SkipsIngredientsWithUnknownCanonicalName()
    {
        var translations = new FakeTranslationRepository();
        var service = new IngredientTranslationService(
            new FakeIngredientRepository(Ingredient("Unknown")),
            translations,
            new StubResolver(new StubChatClient()), NullLogger<IngredientTranslationService>.Instance);

        var result = await service.BackfillMissingTranslationsAsync(CancellationToken.None);

        Assert.Equal(0, result.Written);
        Assert.Empty(translations.Entries);
    }

    [Fact]
    public async Task Backfill_CountsProviderFailuresWithoutWriting()
    {
        var translations = new FakeTranslationRepository();
        var service = new IngredientTranslationService(
            new FakeIngredientRepository(Ingredient("Flour")),
            translations,
            new StubResolver(new StubChatClient(throwOnCall: true)), NullLogger<IngredientTranslationService>.Instance);

        var result = await service.BackfillMissingTranslationsAsync(CancellationToken.None);

        Assert.Equal(0, result.Written);
        Assert.Equal(1, result.Failed);
        Assert.Empty(translations.Entries);
        Assert.Equal(0, translations.SaveCount);
    }

    private static Ingredient Ingredient(string name) =>
        new() { Id = Guid.NewGuid(), Name = name, MeasurementUnit = "g" };

    private sealed class StubResolver(IChatClient? client) : IApplicationChatClientResolver
    {
        public IChatClient? Resolve() => client;
    }

    // Echoes back the source name tagged with the target language so the test can assert exactly what
    // was written. Parses the "Text: {name}" line the service sends.
    private sealed class StubChatClient(bool throwOnCall = false) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (throwOnCall)
                throw new InvalidOperationException("provider failure");

            var userText = messages.Last(message => message.Role == ChatRole.User).Text;
            var name = userText[(userText.IndexOf("Text: ", StringComparison.Ordinal) + "Text: ".Length)..];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, $"{name} (pl)")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class FakeIngredientRepository(params Ingredient[] ingredients) : IIngredientRepository
    {
        private readonly List<Ingredient> _ingredients = [.. ingredients];

        public Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Ingredient>>(_ingredients.ToArray());

        public Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Ingredient>>(_ingredients.Where(x => ids.Contains(x.Id)).ToDictionary(x => x.Id));

        public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(_ingredients.SingleOrDefault(x => x.Id == id));

        public Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(Ingredient ingredient) => _ingredients.Add(ingredient);
        public void Remove(Ingredient ingredient) => _ingredients.Remove(ingredient);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeTranslationRepository : ITranslationRepository
    {
        public Dictionary<string, string> Entries { get; } = [];
        public int SaveCount { get; private set; }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(Entries));

        public Task SetAsync(string key, string value, CancellationToken cancellationToken)
        {
            Entries[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string keyPrefix, CancellationToken cancellationToken)
        {
            foreach (var key in Entries.Keys.Where(k => k.StartsWith(keyPrefix)).ToList())
                Entries.Remove(key);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
