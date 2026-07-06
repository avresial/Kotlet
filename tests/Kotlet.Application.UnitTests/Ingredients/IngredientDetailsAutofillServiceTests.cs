using Kotlet.Application.Ai;
using Kotlet.Application.Ingredients;
using Kotlet.Domain.Ingredients;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Kotlet.Application.UnitTests.Ingredients;

public sealed class IngredientDetailsAutofillServiceTests
{
    [Fact]
    public async Task Suggest_ParsesKnownTaxonomyValues()
    {
        var service = new IngredientDetailsAutofillService(new UnusedRepository(), new Resolver(new Client()), NullLogger<IngredientDetailsAutofillService>.Instance);

        var result = await service.SuggestAsync("Apple", CancellationToken.None);

        Assert.Equal(new IngredientDetailsSuggestion(FoodCategory.Fruit, Allergen.None, FoodAttribute.PlantOrigin,
            DietarySuitability.Vegan | DietarySuitability.Vegetarian), result);
    }

    [Fact]
    public async Task Backfill_SavesEveryTenItemsAndFinalRemainder()
    {
        var repository = new Repository(Enumerable.Range(0, 11).Select(i => Ingredient($"Apple {i}")).ToArray());
        var service = new IngredientDetailsAutofillService(repository, new Resolver(new Client()), NullLogger<IngredientDetailsAutofillService>.Instance);

        var written = await service.BackfillAsync(CancellationToken.None);

        Assert.Equal(11, written);
        Assert.Equal(2, repository.SaveCount);
    }

    private static Ingredient Ingredient(string name) => new() { Id = Guid.NewGuid(), Name = name, MeasurementUnit = "g" };

    private sealed class Resolver(IChatClient client) : IApplicationChatClientResolver
    {
        public IChatClient? Resolve() => client;
    }

    private sealed class Client : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => Task.FromResult(new ChatResponse(new ChatMessage(
                ChatRole.Assistant, "{\"category\":\"Fruit\",\"allergens\":[],\"attributes\":[\"PlantOrigin\"],\"suitability\":[\"Vegan\",\"Vegetarian\"]}")));
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class UnusedRepository : IIngredientRepository
    {
        public Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsInUseAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public void Add(Ingredient ingredient) => throw new NotSupportedException();
        public void Remove(Ingredient ingredient) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Repository(params Ingredient[] ingredients) : IIngredientRepository
    {
        public int SaveCount { get; private set; }
        public Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Ingredient>>(ingredients);
        public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken ct) => Task.FromResult(ingredients.SingleOrDefault(x => x.Id == id));
        public Task SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.CompletedTask; }
        public Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsInUseAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public void Add(Ingredient ingredient) => throw new NotSupportedException();
        public void Remove(Ingredient ingredient) => throw new NotSupportedException();
    }
}
