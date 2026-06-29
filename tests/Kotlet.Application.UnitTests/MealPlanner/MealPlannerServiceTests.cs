using Kotlet.Application.Ingredients;
using Kotlet.Application.MealPlanner;
using Kotlet.Application.Recipes;
using Kotlet.Domain.Ingredients;
using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Recipes;
using Xunit;

namespace Kotlet.Application.UnitTests.MealPlanner;

public sealed class MealPlannerServiceTests
{
    private static readonly Guid HouseId = Guid.NewGuid();
    private static readonly Guid CurrentUserId = Guid.NewGuid();
    private static readonly Guid OtherUserId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 6, 29);
    private static readonly Recipe SoupRecipe = MakeRecipe("Tomato Soup");
    private static readonly Ingredient Bread = MakeIngredient("Bread");

    // ---- AddItem ----

    [Fact]
    public async Task AddItem_WithRecipe_AddsBreakfastEntry()
    {
        var (service, meals) = CreateService();

        var result = await service.AddItemAsync(CurrentUserId, HouseId,
            new AddMealPlanItemRequest(Today, "breakfast", SoupRecipe.Id, null, " note "), CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.Success, result.Status);
        Assert.NotNull(result.Item);
        Assert.Equal("recipe", result.Item.Type);
        Assert.Equal("Tomato Soup", result.Item.DisplayName);
        Assert.Equal("note", result.Item.Note);
        var stored = Assert.Single(meals.Items);
        Assert.Equal(MealSlot.Breakfast, stored.Slot);
    }

    [Fact]
    public async Task AddItem_WithIngredient_AddsEntry()
    {
        var (service, _) = CreateService();

        var result = await service.AddItemAsync(CurrentUserId, HouseId,
            new AddMealPlanItemRequest(Today, "dinner", null, Bread.Id, null), CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.Success, result.Status);
        Assert.Equal("ingredient", result.Item!.Type);
        Assert.Equal("Bread", result.Item.DisplayName);
    }

    [Fact]
    public async Task AddItem_AssignsIncrementingSortOrderWithinSlot()
    {
        var (service, _) = CreateService();
        var request = new AddMealPlanItemRequest(Today, "breakfast", SoupRecipe.Id, null, null);

        var first = await service.AddItemAsync(CurrentUserId, HouseId, request, CancellationToken.None);
        var second = await service.AddItemAsync(CurrentUserId, HouseId, request, CancellationToken.None);

        Assert.Equal(0, first.Item!.SortOrder);
        Assert.Equal(1, second.Item!.SortOrder);
    }

    [Fact]
    public async Task AddItem_WithInvalidSlot_FailsValidation()
    {
        var (service, _) = CreateService();

        var result = await service.AddItemAsync(CurrentUserId, HouseId,
            new AddMealPlanItemRequest(Today, "brunch", SoupRecipe.Id, null, null), CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("slot"));
    }

    [Fact]
    public async Task AddItem_WithNeitherRecipeNorIngredient_FailsValidation()
    {
        var (service, _) = CreateService();

        var result = await service.AddItemAsync(CurrentUserId, HouseId,
            new AddMealPlanItemRequest(Today, "breakfast", null, null, null), CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("item"));
    }

    [Fact]
    public async Task AddItem_WithBothRecipeAndIngredient_FailsValidation()
    {
        var (service, _) = CreateService();

        var result = await service.AddItemAsync(CurrentUserId, HouseId,
            new AddMealPlanItemRequest(Today, "breakfast", SoupRecipe.Id, Bread.Id, null), CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("item"));
    }

    [Fact]
    public async Task AddItem_WithUnknownRecipe_FailsValidation()
    {
        var (service, _) = CreateService();

        var result = await service.AddItemAsync(CurrentUserId, HouseId,
            new AddMealPlanItemRequest(Today, "breakfast", Guid.NewGuid(), null, null), CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("recipeId"));
    }

    [Fact]
    public async Task AddItem_WithUnknownIngredient_FailsValidation()
    {
        var (service, _) = CreateService();

        var result = await service.AddItemAsync(CurrentUserId, HouseId,
            new AddMealPlanItemRequest(Today, "dinner", null, Guid.NewGuid(), null), CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("ingredientId"));
    }

    [Fact]
    public async Task AddWeek_AddsAtomicallyAndSkipsDuplicatesOnRetry()
    {
        var (service, meals) = CreateService();
        var request = new AddWeeklyMealPlanRequest(Today,
        [
            new(Today, "breakfast", SoupRecipe.Id, null, null),
            new(Today.AddDays(1), "dinner", null, Bread.Id, null)
        ]);

        var first = await service.AddWeekAsync(CurrentUserId, HouseId, request, CancellationToken.None);
        var retry = await service.AddWeekAsync(CurrentUserId, HouseId, request, CancellationToken.None);

        Assert.Equal(2, first.Plan!.Added.Count);
        Assert.Equal(0, first.Plan.Skipped);
        Assert.Empty(retry.Plan!.Added);
        Assert.Equal(2, retry.Plan.Skipped);
        Assert.Equal(2, meals.Items.Count);
    }

    [Fact]
    public async Task AddWeek_WhenAnyMealIsInvalid_AddsNothing()
    {
        var (service, meals) = CreateService();
        var request = new AddWeeklyMealPlanRequest(Today,
        [
            new(Today, "breakfast", SoupRecipe.Id, null, null),
            new(Today.AddDays(7), "dinner", SoupRecipe.Id, null, null)
        ]);

        var result = await service.AddWeekAsync(CurrentUserId, HouseId, request, CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.ValidationFailed, result.Status);
        Assert.Empty(meals.Items);
    }

    // ---- GetForDate ----

    [Fact]
    public async Task GetForDate_GroupsItemsBySlotOrderedBySortOrder()
    {
        var (service, meals) = CreateService();
        meals.SeedItem(Today, MealSlot.Breakfast, SoupRecipe.Id, sortOrder: 1);
        meals.SeedItem(Today, MealSlot.Breakfast, SoupRecipe.Id, sortOrder: 0);
        meals.SeedItem(Today, MealSlot.Dinner, SoupRecipe.Id, sortOrder: 0);

        var response = await service.GetForDateAsync(CurrentUserId, HouseId, Today, CancellationToken.None);

        Assert.Equal("2026-06-29", response.Date);
        var breakfast = response.Meals["breakfast"];
        Assert.Equal(2, breakfast.Count);
        Assert.Equal(0, breakfast[0].SortOrder);
        Assert.Equal(1, breakfast[1].SortOrder);
        Assert.Single(response.Meals["dinner"]);
        Assert.Empty(response.Meals["supper"]);
    }

    // ---- GetOverview ----

    [Fact]
    public async Task GetOverview_ReportsPlannedSlotsPerDay()
    {
        var (service, meals) = CreateService();
        meals.SeedItem(Today, MealSlot.Breakfast, SoupRecipe.Id, 0);
        meals.SeedItem(Today, MealSlot.Supper, SoupRecipe.Id, 0);
        meals.SeedItem(Today.AddDays(1), MealSlot.Dinner, SoupRecipe.Id, 0);

        var overview = await service.GetOverviewAsync(HouseId, Today, 3, CancellationToken.None);

        Assert.Equal(3, overview.Count);
        // Slots come back ordered alphabetically.
        Assert.Equal(2, overview[0].PlannedSlots.Count);
        Assert.Equal("breakfast", overview[0].PlannedSlots[0]);
        Assert.Equal("supper", overview[0].PlannedSlots[1]);
        Assert.Equal("dinner", Assert.Single(overview[1].PlannedSlots));
        Assert.Empty(overview[2].PlannedSlots);
    }

    // ---- Remove ----

    [Fact]
    public async Task RemoveItem_RemovesEntry()
    {
        var (service, meals) = CreateService();
        var item = meals.SeedItem(Today, MealSlot.Breakfast, SoupRecipe.Id, 0);

        var status = await service.RemoveItemAsync(HouseId, item.Id, CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.Success, status);
        Assert.Empty(meals.Items);
    }

    [Fact]
    public async Task RemoveItem_ForUnknownItem_ReturnsNotFound()
    {
        var (service, _) = CreateService();

        var status = await service.RemoveItemAsync(HouseId, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.NotFound, status);
    }

    // ---- SetParticipants ----

    [Fact]
    public async Task SetParticipants_AssignsHouseMembersAndDrivesServings()
    {
        var (service, meals) = CreateService();
        var item = meals.SeedItem(Today, MealSlot.Dinner, SoupRecipe.Id, 0);

        var result = await service.SetParticipantsAsync(CurrentUserId, HouseId, item.Id,
            [CurrentUserId, OtherUserId], CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.Success, result.Status);
        Assert.Equal(2, result.Item!.Participants.Count);
        // No explicit override => effective servings derived from headcount.
        Assert.Equal(2, result.Item.Servings);
        Assert.False(result.Item.ServingsOverridden);
        // Current user is surfaced first.
        Assert.True(result.Item.Participants[0].IsCurrentUser);
    }

    [Fact]
    public async Task SetParticipants_RemovesOmittedMembers()
    {
        var (service, meals) = CreateService();
        var item = meals.SeedItem(Today, MealSlot.Dinner, SoupRecipe.Id, 0);
        item.Participants.Add(new MealPlanItemParticipant { MealPlanItemId = item.Id, UserId = CurrentUserId });
        item.Participants.Add(new MealPlanItemParticipant { MealPlanItemId = item.Id, UserId = OtherUserId });

        var result = await service.SetParticipantsAsync(CurrentUserId, HouseId, item.Id, [CurrentUserId], CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.Success, result.Status);
        Assert.Single(item.Participants);
        Assert.Equal(CurrentUserId, item.Participants.Single().UserId);
    }

    [Fact]
    public async Task SetParticipants_WithNonMember_FailsValidation()
    {
        var (service, meals) = CreateService();
        var item = meals.SeedItem(Today, MealSlot.Dinner, SoupRecipe.Id, 0);

        var result = await service.SetParticipantsAsync(CurrentUserId, HouseId, item.Id,
            [Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("userIds"));
    }

    [Fact]
    public async Task SetParticipants_ForUnknownItem_ReturnsNotFound()
    {
        var (service, _) = CreateService();

        var result = await service.SetParticipantsAsync(CurrentUserId, HouseId, Guid.NewGuid(),
            [CurrentUserId], CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.NotFound, result.Status);
    }

    // ---- SetServings ----

    [Fact]
    public async Task SetServings_AppliesExplicitOverride()
    {
        var (service, meals) = CreateService();
        var item = meals.SeedItem(Today, MealSlot.Dinner, SoupRecipe.Id, 0);

        var result = await service.SetServingsAsync(CurrentUserId, HouseId, item.Id, 6, CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.Success, result.Status);
        Assert.Equal(6, result.Item!.Servings);
        Assert.True(result.Item.ServingsOverridden);
    }

    [Fact]
    public async Task SetServings_WithNull_ClearsOverride()
    {
        var (service, meals) = CreateService();
        var item = meals.SeedItem(Today, MealSlot.Dinner, SoupRecipe.Id, 0);
        item.Servings = 4;

        var result = await service.SetServingsAsync(CurrentUserId, HouseId, item.Id, null, CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.Success, result.Status);
        Assert.False(result.Item!.ServingsOverridden);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task SetServings_OutOfRange_FailsValidation(int servings)
    {
        var (service, meals) = CreateService();
        var item = meals.SeedItem(Today, MealSlot.Dinner, SoupRecipe.Id, 0);

        var result = await service.SetServingsAsync(CurrentUserId, HouseId, item.Id, servings, CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("servings"));
    }

    [Fact]
    public async Task SetServings_ForUnknownItem_ReturnsNotFound()
    {
        var (service, _) = CreateService();

        var result = await service.SetServingsAsync(CurrentUserId, HouseId, Guid.NewGuid(), 4, CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.NotFound, result.Status);
    }

    // ---- SetGuests ----

    [Fact]
    public async Task SetGuests_AddsToHeadcountForServings()
    {
        var (service, meals) = CreateService();
        var item = meals.SeedItem(Today, MealSlot.Dinner, SoupRecipe.Id, 0);
        item.Participants.Add(new MealPlanItemParticipant { MealPlanItemId = item.Id, UserId = CurrentUserId });

        var result = await service.SetGuestsAsync(CurrentUserId, HouseId, item.Id, 3, CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.Success, result.Status);
        Assert.Equal(3, result.Item!.Guests);
        // 1 participant + 3 guests, no override.
        Assert.Equal(4, result.Item.Servings);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task SetGuests_OutOfRange_FailsValidation(int guests)
    {
        var (service, meals) = CreateService();
        var item = meals.SeedItem(Today, MealSlot.Dinner, SoupRecipe.Id, 0);

        var result = await service.SetGuestsAsync(CurrentUserId, HouseId, item.Id, guests, CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("guests"));
    }

    [Fact]
    public async Task SetGuests_ForUnknownItem_ReturnsNotFound()
    {
        var (service, _) = CreateService();

        var result = await service.SetGuestsAsync(CurrentUserId, HouseId, Guid.NewGuid(), 2, CancellationToken.None);

        Assert.Equal(MealPlannerOperationStatus.NotFound, result.Status);
    }

    // ---- House members ----

    [Fact]
    public async Task GetHouseMembers_ReturnsConfiguredMembers()
    {
        var (service, _) = CreateService();

        var members = await service.GetHouseMembersAsync(HouseId, CancellationToken.None);

        Assert.Equal(2, members.Count);
        Assert.Contains(members, m => m.UserId == CurrentUserId);
    }

    private static (MealPlannerService Service, FakeMealPlanRepository Meals) CreateService()
    {
        var meals = new FakeMealPlanRepository(
            new MealHouseMember(CurrentUserId, "Alice"),
            new MealHouseMember(OtherUserId, "Bob"));
        var service = new MealPlannerService(
            meals,
            new FakeRecipeRepository(SoupRecipe),
            new FakeIngredientRepository(Bread));
        return (service, meals);
    }

    private static Recipe MakeRecipe(string title) => new()
    {
        Id = Guid.NewGuid(), Title = title, Slug = title.ToLowerInvariant().Replace(' ', '-'),
        OwnerUserId = Guid.NewGuid()
    };

    private static Ingredient MakeIngredient(string name) => new()
    {
        Id = Guid.NewGuid(), Name = name, MeasurementUnit = "g"
    };

    private sealed class FakeMealPlanRepository(params MealHouseMember[] members) : IMealPlanRepository
    {
        public List<MealPlanItem> Items { get; } = [];

        public MealPlanItem SeedItem(DateOnly date, MealSlot slot, Guid recipeId, int sortOrder)
        {
            var item = new MealPlanItem
            {
                Id = Guid.NewGuid(), HouseId = HouseId, UserId = CurrentUserId, Date = date,
                Slot = slot, RecipeId = recipeId, SortOrder = sortOrder,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            };
            Items.Add(item);
            return item;
        }

        public Task<IReadOnlyList<MealPlanItem>> GetByDateAsync(Guid houseId, DateOnly date, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MealPlanItem>>(Items.Where(i => i.HouseId == houseId && i.Date == date).ToList());

        public Task<IReadOnlyList<MealPlanItem>> GetByDateRangeAsync(Guid houseId, DateOnly from, DateOnly to, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MealPlanItem>>(Items.Where(i => i.HouseId == houseId && i.Date >= from && i.Date <= to).ToList());

        public Task<MealPlanItem?> GetByIdAsync(Guid id, Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(i => i.Id == id && i.HouseId == houseId));

        public Task<IReadOnlyList<MealHouseMember>> GetHouseMembersAsync(Guid houseId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MealHouseMember>>(members.ToList());

        public void Add(MealPlanItem item) => Items.Add(item);
        public void Remove(MealPlanItem item) => Items.Remove(item);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeRecipeRepository(params Recipe[] recipes) : IRecipeRepository
    {
        public Task<(IReadOnlyList<Recipe> Items, int TotalCount)> GetPagedAsync(
            Guid ownerUserId, int page, int pageSize, string? search, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Recipe>> GetRecentAsync(Guid ownerUserId, int limit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Recipe?> GetByIdAsync(Guid id, Guid ownerUserId, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(recipes.SingleOrDefault(r => r.Id == id));

        public Task<bool> SlugExistsAsync(Guid ownerUserId, string slug, Guid? excludedId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void Add(Recipe recipe) => throw new NotSupportedException();
        public void Remove(Recipe recipe) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeIngredientRepository(params Ingredient[] ingredients) : IIngredientRepository
    {
        public Task<IReadOnlyCollection<Ingredient>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Ingredient>>(ingredients);

        public Task<IReadOnlyDictionary<Guid, Ingredient>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Ingredient>>(ingredients.Where(x => ids.Contains(x.Id)).ToDictionary(x => x.Id));

        public Task<Ingredient?> GetByIdAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(ingredients.SingleOrDefault(x => x.Id == id));

        public Task<bool> IsInUseAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Add(Ingredient ingredient) => throw new NotSupportedException();
        public void Remove(Ingredient ingredient) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
