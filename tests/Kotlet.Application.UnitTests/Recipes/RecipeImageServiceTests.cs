using Kotlet.Application.Recipes;
using Kotlet.Domain.Recipes;
using Xunit;

namespace Kotlet.Application.UnitTests.Recipes;

public sealed class RecipeImageServiceTests
{
    private static readonly Guid RecipeId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly byte[] SampleContent = [1, 2, 3, 4];

    // ---- Add ----

    [Fact]
    public async Task Add_WithValidImage_PersistsAndAssignsSortOrder()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = new RecipeImageService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", SampleContent, "A dish", CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, result.Status);
        Assert.NotNull(result.Image);
        Assert.Equal(0, result.Image.SortOrder);
        Assert.Equal("A dish", result.Image.AltText);
        Assert.Equal($"/api/recipes/{RecipeId}/images/{result.Image.Id}/content", result.Image.ContentUrl);
        Assert.Single(repo.Images);
    }

    [Fact]
    public async Task Add_AssignsIncrementingSortOrder()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = new RecipeImageService(repo);

        await service.AddAsync(RecipeId, OwnerId, "a.png", "image/png", SampleContent, null, CancellationToken.None);
        var second = await service.AddAsync(RecipeId, OwnerId, "b.webp", "image/webp", SampleContent, null, CancellationToken.None);

        Assert.Equal(1, second.Image!.SortOrder);
    }

    [Fact]
    public async Task Add_ForUnknownRecipe_ReturnsNotFound()
    {
        var repo = new FakeRepository(recipeExists: false);
        var service = new RecipeImageService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.NotFound, result.Status);
        Assert.Empty(repo.Images);
    }

    [Fact]
    public async Task Add_WithEmptyContent_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = new RecipeImageService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", [], null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("file"));
    }

    [Fact]
    public async Task Add_WithOversizedContent_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = new RecipeImageService(repo);
        var tooBig = new byte[RecipeImageService.MaxFileSizeBytes + 1];

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", tooBig, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("file"));
    }

    [Fact]
    public async Task Add_WithUnsupportedContentType_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = new RecipeImageService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "doc.gif", "image/gif", SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("contentType"));
    }

    [Fact]
    public async Task Add_WithMismatchedExtension_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = new RecipeImageService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.png", "image/jpeg", SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("fileName"));
    }

    [Fact]
    public async Task Add_WithTooLongAltText_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = new RecipeImageService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", SampleContent, new string('x', 301), CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("altText"));
    }

    [Fact]
    public async Task Add_WhenLimitReached_ReturnsLimitExceeded()
    {
        var repo = new FakeRepository(recipeExists: true);
        for (var i = 0; i < RecipeImageService.MaxImages; i++)
            repo.SeedImage(RecipeId, i);
        var service = new RecipeImageService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.LimitExceeded, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("file"));
    }

    [Theory]
    [InlineData("image/jpeg", "p.jpg")]
    [InlineData("image/jpeg", "p.jpeg")]
    [InlineData("image/png", "p.png")]
    [InlineData("image/webp", "p.webp")]
    public async Task Add_AcceptsSupportedTypeAndExtensionPairs(string contentType, string fileName)
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = new RecipeImageService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, fileName, contentType, SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, result.Status);
    }

    // ---- List / Content ----

    [Fact]
    public async Task List_ForUnknownRecipe_ReturnsNull()
    {
        var repo = new FakeRepository(recipeExists: false);
        var service = new RecipeImageService(repo);

        Assert.Null(await service.ListAsync(RecipeId, OwnerId, CancellationToken.None));
    }

    [Fact]
    public async Task List_ReturnsImagesForRecipe()
    {
        var repo = new FakeRepository(recipeExists: true);
        repo.SeedImage(RecipeId, 0);
        repo.SeedImage(RecipeId, 1);
        var service = new RecipeImageService(repo);

        var result = await service.ListAsync(RecipeId, OwnerId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetContent_ReturnsRawBytes()
    {
        var repo = new FakeRepository(recipeExists: true);
        var image = repo.SeedImage(RecipeId, 0);
        var service = new RecipeImageService(repo);

        var content = await service.GetContentAsync(RecipeId, image.Id, OwnerId, CancellationToken.None);

        Assert.NotNull(content);
        Assert.Equal(image.ContentType, content.ContentType);
        Assert.Equal(image.Content, content.Content);
    }

    [Fact]
    public async Task GetContent_ForUnknownRecipe_ReturnsNull()
    {
        var repo = new FakeRepository(recipeExists: false);
        var service = new RecipeImageService(repo);

        Assert.Null(await service.GetContentAsync(RecipeId, Guid.NewGuid(), OwnerId, CancellationToken.None));
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ChangesAltText()
    {
        var repo = new FakeRepository(recipeExists: true);
        var image = repo.SeedImage(RecipeId, 0);
        var service = new RecipeImageService(repo);

        var result = await service.UpdateAsync(RecipeId, image.Id, OwnerId, "  New caption  ", CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, result.Status);
        Assert.Equal("New caption", result.Image!.AltText);
    }

    [Fact]
    public async Task Update_WithBlankAltText_ClearsIt()
    {
        var repo = new FakeRepository(recipeExists: true);
        var image = repo.SeedImage(RecipeId, 0);
        image.AltText = "old";
        var service = new RecipeImageService(repo);

        var result = await service.UpdateAsync(RecipeId, image.Id, OwnerId, "   ", CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, result.Status);
        Assert.Null(result.Image!.AltText);
    }

    [Fact]
    public async Task Update_WithTooLongAltText_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var image = repo.SeedImage(RecipeId, 0);
        var service = new RecipeImageService(repo);

        var result = await service.UpdateAsync(RecipeId, image.Id, OwnerId, new string('x', 301), CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("altText"));
    }

    [Fact]
    public async Task Update_ForUnknownImage_ReturnsNotFound()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = new RecipeImageService(repo);

        var result = await service.UpdateAsync(RecipeId, Guid.NewGuid(), OwnerId, "caption", CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.NotFound, result.Status);
    }

    // ---- Reorder ----

    [Fact]
    public async Task Reorder_AppliesNewSortOrder()
    {
        var repo = new FakeRepository(recipeExists: true);
        var first = repo.SeedImage(RecipeId, 0);
        var second = repo.SeedImage(RecipeId, 1);
        var service = new RecipeImageService(repo);

        var status = await service.ReorderAsync(RecipeId, OwnerId, [second.Id, first.Id], CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, status);
        Assert.Equal(0, second.SortOrder);
        Assert.Equal(1, first.SortOrder);
    }

    [Fact]
    public async Task Reorder_WithMismatchedIdCount_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var first = repo.SeedImage(RecipeId, 0);
        repo.SeedImage(RecipeId, 1);
        var service = new RecipeImageService(repo);

        var status = await service.ReorderAsync(RecipeId, OwnerId, [first.Id], CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, status);
    }

    [Fact]
    public async Task Reorder_WithUnknownId_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var first = repo.SeedImage(RecipeId, 0);
        repo.SeedImage(RecipeId, 1);
        var service = new RecipeImageService(repo);

        // Same count as stored, but one id does not belong to the recipe.
        var status = await service.ReorderAsync(RecipeId, OwnerId, [first.Id, Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, status);
    }

    [Fact]
    public async Task Reorder_ForUnknownRecipe_ReturnsNotFound()
    {
        var repo = new FakeRepository(recipeExists: false);
        var service = new RecipeImageService(repo);

        var status = await service.ReorderAsync(RecipeId, OwnerId, [Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.NotFound, status);
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_RemovesImageAndCompactsSortOrder()
    {
        var repo = new FakeRepository(recipeExists: true);
        var first = repo.SeedImage(RecipeId, 0);
        var second = repo.SeedImage(RecipeId, 1);
        var service = new RecipeImageService(repo);

        var status = await service.DeleteAsync(RecipeId, first.Id, OwnerId, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, status);
        Assert.Single(repo.Images);
        Assert.Equal(0, second.SortOrder);
    }

    [Fact]
    public async Task Delete_ForUnknownImage_ReturnsNotFound()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = new RecipeImageService(repo);

        var status = await service.DeleteAsync(RecipeId, Guid.NewGuid(), OwnerId, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.NotFound, status);
    }

    [Fact]
    public async Task Delete_ForUnknownRecipe_ReturnsNotFound()
    {
        var repo = new FakeRepository(recipeExists: false);
        var service = new RecipeImageService(repo);

        var status = await service.DeleteAsync(RecipeId, Guid.NewGuid(), OwnerId, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.NotFound, status);
    }

    private sealed class FakeRepository(bool recipeExists) : IRecipeImageRepository
    {
        public List<RecipeImage> Images { get; } = [];

        public RecipeImage SeedImage(Guid recipeId, int sortOrder)
        {
            var image = new RecipeImage
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                FileName = $"image-{sortOrder}.jpg",
                ContentType = "image/jpeg",
                FileSizeBytes = SampleContent.LongLength,
                Content = SampleContent,
                SortOrder = sortOrder,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            Images.Add(image);
            return image;
        }

        public Task<bool> RecipeExistsAsync(Guid recipeId, Guid ownerUserId, CancellationToken cancellationToken) =>
            Task.FromResult(recipeExists);

        public Task<int> CountAsync(Guid recipeId, CancellationToken cancellationToken) =>
            Task.FromResult(Images.Count(i => i.RecipeId == recipeId));

        public Task<IReadOnlyList<RecipeImage>> ListAsync(Guid recipeId, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RecipeImage>>(Images.Where(i => i.RecipeId == recipeId).OrderBy(i => i.SortOrder).ToList());

        public Task<IReadOnlyDictionary<Guid, Guid>> GetFirstImageIdsAsync(IReadOnlyList<Guid> recipeIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(Images
                .Where(i => recipeIds.Contains(i.RecipeId))
                .GroupBy(i => i.RecipeId)
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.SortOrder).First().Id));

        public Task<RecipeImage?> GetAsync(Guid recipeId, Guid imageId, bool includeContent, CancellationToken cancellationToken) =>
            Task.FromResult(Images.SingleOrDefault(i => i.RecipeId == recipeId && i.Id == imageId));

        public Task<int> UpdateAltTextAsync(Guid recipeId, Guid imageId, string? altText, DateTimeOffset updatedAtUtc, CancellationToken cancellationToken)
        {
            var image = Images.SingleOrDefault(i => i.RecipeId == recipeId && i.Id == imageId);
            if (image is null) return Task.FromResult(0);
            image.AltText = altText;
            image.UpdatedAtUtc = updatedAtUtc;
            return Task.FromResult(1);
        }

        public Task<int> DeleteAsync(Guid recipeId, Guid imageId, CancellationToken cancellationToken) =>
            Task.FromResult(Images.RemoveAll(i => i.RecipeId == recipeId && i.Id == imageId));

        public Task UpdateSortOrdersAsync(Guid recipeId, IReadOnlyList<Guid> imageIds, CancellationToken cancellationToken)
        {
            for (var index = 0; index < imageIds.Count; index++)
            {
                var image = Images.SingleOrDefault(i => i.RecipeId == recipeId && i.Id == imageIds[index]);
                if (image is not null) image.SortOrder = index;
            }
            return Task.CompletedTask;
        }

        public void Add(RecipeImage image) => Images.Add(image);
        public void Remove(RecipeImage image) => Images.Remove(image);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
