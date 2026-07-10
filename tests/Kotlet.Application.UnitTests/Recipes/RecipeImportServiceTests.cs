using Kotlet.Application.Ingredients;
using Kotlet.Application.Measurements;
using Kotlet.Application.Recipes;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Recipes;
using Kotlet.Domain.Sources;
using Xunit;

namespace Kotlet.Application.UnitTests.Recipes;

public sealed class RecipeImportServiceTests
{
    [Fact]
    public async Task CreateJobAsync_ValidatesAndQueuesPersistedJob()
    {
        var jobs = new FakeJobs();
        var signal = new FakeSignal();
        var service = CreateService(jobs, signal);

        var invalid = await service.CreateJobAsync(Guid.NewGuid(), Guid.NewGuid(), "not a url", default);
        var valid = await service.CreateJobAsync(Guid.NewGuid(), Guid.NewGuid(), "https://youtu.be/test", default);

        Assert.Equal(RecipeImportOperationStatus.ValidationFailed, invalid.Status);
        Assert.Equal(RecipeImportOperationStatus.Success, valid.Status);
        Assert.Equal(valid.Id, jobs.Job!.Id);
        Assert.Equal(valid.Id, signal.JobId);
        Assert.Equal(1, jobs.SaveCount);
    }

    [Fact]
    public async Task AcceptAsync_RejectsJobThatIsNotReady()
    {
        var jobs = new FakeJobs { Job = NewJob(RecipeImportJobStatus.Extracting) };
        var result = await CreateService(jobs, new FakeSignal()).AcceptAsync(
            jobs.Job.Id, jobs.Job.HouseId, jobs.Job.UserId,
            new RecipeImportDraft("Soup", 1, "Cook.", [], [], []), default);

        Assert.Equal(RecipeImportOperationStatus.InvalidState, result.Status);
    }

    [Fact]
    public async Task AcceptAsync_HidesAnotherUsersJob()
    {
        var jobs = new FakeJobs { Job = NewJob(RecipeImportJobStatus.ReadyForReview) };
        var result = await CreateService(jobs, new FakeSignal()).AcceptAsync(
            jobs.Job.Id, jobs.Job.HouseId, Guid.NewGuid(),
            new RecipeImportDraft("Soup", 1, "Cook.", [], [], []), default);

        Assert.Equal(RecipeImportOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task AcceptAsync_RecordsAiAssistedSourceOnCreatedRecipe()
    {
        var jobs = new FakeJobs { Job = NewJob(RecipeImportJobStatus.ReadyForReview) };
        var recipes = new FakeRecipes();
        var service = new RecipeImportService(
            jobs, recipes, null!, new FakeIngredients(), null!, new MeasurementMappingService(), null!, null!, new FakeSignal());
        var draft = new RecipeImportDraft("Soup", 2, "Cook.", [],
            [new("Tomato", 400, "g", null, null, null, IsProposedNew: true)], []);

        var result = await service.AcceptAsync(jobs.Job.Id, jobs.Job.HouseId, jobs.Job.UserId, draft, default);

        Assert.Equal(RecipeImportOperationStatus.Success, result.Status);
        var recipe = Assert.Single(recipes.Recipes);
        var association = Assert.Single(recipe.Sources);
        Assert.Equal(recipe.Id, association.RecipeId);
        Assert.Equal(SourceType.AiAssisted, association.Source.Type);
        Assert.Equal(RecipeImportService.AiSourceProvider, association.Source.Provider);
        Assert.Equal("https://youtu.be/test", association.Source.Url);
    }

    private static RecipeImportService CreateService(FakeJobs jobs, IRecipeImportSignal signal) =>
        new(jobs, null!, null!, null!, null!, null!, null!, null!, signal);

    private static RecipeImportJob NewJob(RecipeImportJobStatus status) => new()
    {
        Id = Guid.NewGuid(), HouseId = Guid.NewGuid(), UserId = Guid.NewGuid(), Url = "https://youtu.be/test",
        Status = status, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
    };

    private sealed class FakeSignal : IRecipeImportSignal
    {
        public Guid? JobId { get; private set; }
        public void Enqueue(Guid jobId) => JobId = jobId;
        public ValueTask<Guid> WaitAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeRecipes : IRecipeRepository
    {
        public List<Recipe> Recipes { get; } = [];
        public Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedAsync(
            Guid ownerUserId, int page, int pageSize, string? search, MealSlot? mealType,
            IReadOnlyCollection<Guid>? ingredientIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Recipe>> GetRecentAsync(Guid ownerUserId, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Recipe?> GetByIdAsync(Guid id, Guid ownerUserId, bool tracked, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Recipe?> GetPublicByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Recipe>> GetAllForDuplicateCheckAsync(Guid ownerUserId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<Recipe>> GetAllWithIngredientsAsync(Guid houseId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> SlugExistsAsync(Guid ownerUserId, string slug, Guid? excludedId, CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public void Add(Recipe recipe) => Recipes.Add(recipe);
        public void Remove(Recipe recipe) => Recipes.Remove(recipe);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeIngredients : IIngredientRepository
    {
        public Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Ingredient>>(new Dictionary<Guid, Ingredient>());
        public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public void Add(Ingredient ingredient) { }
        public void Remove(Ingredient ingredient) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeJobs : IRecipeImportJobRepository
    {
        public RecipeImportJob? Job { get; set; }
        public int SaveCount { get; private set; }
        public Task<RecipeImportJob?> GetAsync(Guid id, Guid? houseId, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(Job is not null && Job.Id == id && (houseId is null || Job.HouseId == houseId) ? Job : null);
        public Task<IReadOnlyList<Guid>> ListActiveIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public void Add(RecipeImportJob job) => Job = job;
        public void Remove(RecipeImportJob job) => Job = null;
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
    }
}
