using Kotlet.Application.Images;
using Kotlet.Application.Recipes;
using Kotlet.Domain.Recipes;
using Kotlet.Domain.Images;
using Kotlet.Domain.Sources;
using Xunit;

namespace Kotlet.Application.UnitTests.Recipes;

public sealed class RecipeImageServiceTests
{
    private static readonly Guid RecipeId = Guid.NewGuid();
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly byte[] SampleContent = [1, 2, 3, 4];
    private static readonly byte[] ProcessedContent = [9, 8, 7];

    private static RecipeImageService CreateService(FakeRepository repo, FakeImageProcessor? processor = null) =>
        new(repo, new StoredImageService(repo, processor ?? new FakeImageProcessor()));

    // ---- Add ----

    [Fact]
    public async Task Add_WithValidImage_PersistsAndAssignsSortOrder()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", SampleContent, "A dish", CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, result.Status);
        Assert.NotNull(result.Image);
        Assert.Equal(0, result.Image.SortOrder);
        Assert.Equal("A dish", result.Image.AltText);
        Assert.Equal($"/api/recipes/{RecipeId}/images/{result.Image.Id}/content", result.Image.ContentUrl);
        Assert.Single(repo.Images);
    }

    [Fact]
    public async Task Add_WithSource_PersistsImageAttribution()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo);
        var source = new RecipeImageSourceData(
            "Pexels", "42", "https://www.pexels.com/photo/42", "Ada", "https://pexels.com/@ada");

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.webp", "image/webp", SampleContent,
            "Pasta", CancellationToken.None, source);

        Assert.Equal(RecipeImageOperationStatus.Success, result.Status);
        var persistedSource = Assert.Single(Assert.Single(repo.Images).Image.Sources).Source;
        Assert.Equal(SourceType.ExternalImage, persistedSource.Type);
        Assert.Equal("Pexels", persistedSource.Provider);
        Assert.Equal("42", persistedSource.ExternalId);
        Assert.Equal("https://www.pexels.com/photo/42", persistedSource.Url);
        Assert.Equal("Ada", persistedSource.AuthorName);
    }

    [Fact]
    public async Task Add_AssignsIncrementingSortOrder()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo);

        await service.AddAsync(RecipeId, OwnerId, "a.png", "image/png", SampleContent, null, CancellationToken.None);
        var second = await service.AddAsync(RecipeId, OwnerId, "b.webp", "image/webp", SampleContent, null, CancellationToken.None);

        Assert.Equal(1, second.Image!.SortOrder);
    }

    [Fact]
    public async Task Add_ForUnknownRecipe_ReturnsNotFound()
    {
        var repo = new FakeRepository(recipeExists: false);
        var service = CreateService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.NotFound, result.Status);
        Assert.Empty(repo.Images);
    }

    [Fact]
    public async Task Add_WithEmptyContent_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", [], null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("file"));
    }

    [Fact]
    public async Task Add_WithOversizedContent_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo);
        var tooBig = new byte[RecipeImageService.MaxFileSizeBytes + 1];

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", tooBig, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("file"));
    }

    [Fact]
    public async Task Add_WithUnsupportedContentType_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "doc.gif", "image/gif", SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("contentType"));
    }

    [Fact]
    public async Task Add_WithMismatchedExtension_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.png", "image/jpeg", SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("fileName"));
    }

    [Fact]
    public async Task Add_WithTooLongAltText_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo);

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
        var service = CreateService(repo);

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
        var service = CreateService(repo);

        var result = await service.AddAsync(RecipeId, OwnerId, fileName, contentType, SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, result.Status);
    }

    [Fact]
    public async Task Add_StoresProcessedWebpInsteadOfOriginal()
    {
        var repo = new FakeRepository(recipeExists: true);
        var processor = new FakeImageProcessor();
        var service = CreateService(repo, processor);

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, result.Status);
        Assert.Equal(RecipeImageService.ProcessedMaxWidth, processor.LastOptions!.MaxWidth);
        Assert.Equal(RecipeImageService.ProcessedMaxHeight, processor.LastOptions.MaxHeight);
        var stored = Assert.Single(repo.Images);
        Assert.Equal("image/webp", stored.Image.ContentType);
        Assert.Equal(ProcessedContent, stored.Image.Content);
        Assert.Equal(ProcessedContent.LongLength, stored.Image.FileSizeBytes);
        Assert.Equal("photo.webp", stored.Image.FileName);
        Assert.Equal("image/webp", result.Image!.ContentType);
        Assert.Equal(ProcessedContent.LongLength, result.Image.FileSizeBytes);
    }

    [Fact]
    public async Task Add_WithInvalidImageContent_FailsValidationAndPersistsNothing()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo, new FakeImageProcessor(throwInvalidImage: true));

        var result = await service.AddAsync(RecipeId, OwnerId, "photo.jpg", "image/jpeg", SampleContent, null, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("file"));
        Assert.Empty(repo.Images);
    }

    // ---- List / Content ----

    [Fact]
    public async Task List_ForUnknownRecipe_ReturnsNull()
    {
        var repo = new FakeRepository(recipeExists: false);
        var service = CreateService(repo);

        Assert.Null(await service.ListAsync(RecipeId, OwnerId, CancellationToken.None));
    }

    [Fact]
    public async Task List_ReturnsImagesForRecipe()
    {
        var repo = new FakeRepository(recipeExists: true);
        repo.SeedImage(RecipeId, 0);
        repo.SeedImage(RecipeId, 1);
        var service = CreateService(repo);

        var result = await service.ListAsync(RecipeId, OwnerId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task List_MapsPrimarySourceAttribution()
    {
        var repo = new FakeRepository(recipeExists: true);
        var image = repo.SeedImage(RecipeId, 0);
        image.Image.Sources.Add(new RecipeImageSource
        {
            RecipeImageId = image.Id,
            SourceId = Guid.NewGuid(),
            Source = new Source
            {
                Id = Guid.NewGuid(),
                Type = SourceType.AiAssisted,
                Provider = "Pexels",
                Url = "https://www.pexels.com/photo/1/",
                AuthorName = "Jane Doe",
                AuthorUrl = "https://www.pexels.com/@jane/",
                ExternalId = "1",
                Title = "A dish",
                RetrievedAtUtc = DateTimeOffset.UtcNow
            }
        });
        var service = CreateService(repo);

        var result = await service.ListAsync(RecipeId, OwnerId, CancellationToken.None);

        var source = Assert.Single(result!).Source;
        Assert.NotNull(source);
        Assert.Equal("Pexels", source.Provider);
        Assert.Equal("Jane Doe", source.AuthorName);
        Assert.Equal("https://www.pexels.com/@jane/", source.AuthorUrl);
        Assert.Equal("https://www.pexels.com/photo/1/", source.Url);
    }

    [Fact]
    public async Task List_WithoutSources_ReturnsNullAttribution()
    {
        var repo = new FakeRepository(recipeExists: true);
        repo.SeedImage(RecipeId, 0);
        var service = CreateService(repo);

        var result = await service.ListAsync(RecipeId, OwnerId, CancellationToken.None);

        Assert.Null(Assert.Single(result!).Source);
    }

    [Fact]
    public async Task GetContent_ReturnsRawBytes()
    {
        var repo = new FakeRepository(recipeExists: true);
        var image = repo.SeedImage(RecipeId, 0);
        var service = CreateService(repo);

        var content = await service.GetContentAsync(RecipeId, image.Id, OwnerId, CancellationToken.None);

        Assert.NotNull(content);
        Assert.Equal(image.Image.ContentType, content.ContentType);
        Assert.Equal(image.Image.Content, content.Content);
    }

    [Fact]
    public async Task GetContent_ForUnknownRecipe_ReturnsNull()
    {
        var repo = new FakeRepository(recipeExists: false);
        var service = CreateService(repo);

        Assert.Null(await service.GetContentAsync(RecipeId, Guid.NewGuid(), OwnerId, CancellationToken.None));
    }

    // ---- Update ----

    [Fact]
    public async Task Update_ChangesAltText()
    {
        var repo = new FakeRepository(recipeExists: true);
        var image = repo.SeedImage(RecipeId, 0);
        var service = CreateService(repo);

        var result = await service.UpdateAsync(RecipeId, image.Id, OwnerId, "  New caption  ", CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, result.Status);
        Assert.Equal("New caption", result.Image!.AltText);
    }

    [Fact]
    public async Task Update_WithBlankAltText_ClearsIt()
    {
        var repo = new FakeRepository(recipeExists: true);
        var image = repo.SeedImage(RecipeId, 0);
        image.Image.AltText = "old";
        var service = CreateService(repo);

        var result = await service.UpdateAsync(RecipeId, image.Id, OwnerId, "   ", CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, result.Status);
        Assert.Null(result.Image!.AltText);
    }

    [Fact]
    public async Task Update_WithTooLongAltText_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var image = repo.SeedImage(RecipeId, 0);
        var service = CreateService(repo);

        var result = await service.UpdateAsync(RecipeId, image.Id, OwnerId, new string('x', 301), CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors!.ContainsKey("altText"));
    }

    [Fact]
    public async Task Update_ForUnknownImage_ReturnsNotFound()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo);

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
        var service = CreateService(repo);

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
        var service = CreateService(repo);

        var status = await service.ReorderAsync(RecipeId, OwnerId, [first.Id], CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, status);
    }

    [Fact]
    public async Task Reorder_WithUnknownId_FailsValidation()
    {
        var repo = new FakeRepository(recipeExists: true);
        var first = repo.SeedImage(RecipeId, 0);
        repo.SeedImage(RecipeId, 1);
        var service = CreateService(repo);

        // Same count as stored, but one id does not belong to the recipe.
        var status = await service.ReorderAsync(RecipeId, OwnerId, [first.Id, Guid.NewGuid()], CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.ValidationFailed, status);
    }

    [Fact]
    public async Task Reorder_ForUnknownRecipe_ReturnsNotFound()
    {
        var repo = new FakeRepository(recipeExists: false);
        var service = CreateService(repo);

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
        var service = CreateService(repo);

        var status = await service.DeleteAsync(RecipeId, first.Id, OwnerId, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.Success, status);
        Assert.Single(repo.Images);
        Assert.Equal(0, second.SortOrder);
    }

    [Fact]
    public async Task Delete_ForUnknownImage_ReturnsNotFound()
    {
        var repo = new FakeRepository(recipeExists: true);
        var service = CreateService(repo);

        var status = await service.DeleteAsync(RecipeId, Guid.NewGuid(), OwnerId, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.NotFound, status);
    }

    [Fact]
    public async Task Delete_ForUnknownRecipe_ReturnsNotFound()
    {
        var repo = new FakeRepository(recipeExists: false);
        var service = CreateService(repo);

        var status = await service.DeleteAsync(RecipeId, Guid.NewGuid(), OwnerId, CancellationToken.None);

        Assert.Equal(RecipeImageOperationStatus.NotFound, status);
    }

    private sealed class FakeImageProcessor(bool throwInvalidImage = false) : IImageProcessor
    {
        public ImageProcessingOptions? LastOptions { get; private set; }

        public Task<ImageProcessingResult> ProcessAsync(Stream image, ImageProcessingOptions options, CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            if (throwInvalidImage) throw new InvalidImageException("Not an image.");
            return Task.FromResult(new ImageProcessingResult(ProcessedContent, "image/webp", 1200, 900));
        }
    }

    private sealed class FakeRepository(bool recipeExists) : IRecipeImageRepository, IStoredImageRepository
    {
        public List<RecipeImage> Images { get; } = [];

        public RecipeImage SeedImage(Guid recipeId, int sortOrder)
        {
            var image = new RecipeImage
            {
                Id = Guid.NewGuid(),
                RecipeId = recipeId,
                Image = new StoredImage
                {
                    Id = Guid.NewGuid(), FileName = $"image-{sortOrder}.jpg", ContentType = "image/jpeg",
                    FileSizeBytes = SampleContent.LongLength, Content = SampleContent, CreatedAtUtc = DateTimeOffset.UtcNow
                },
                SortOrder = sortOrder,
            };
            Images.Add(image);
            return image;
        }

        public Task<bool> RecipeExistsAsync(Guid recipeId, Guid ownerUserId, CancellationToken cancellationToken) =>
            Task.FromResult(recipeExists);

        public Task<int> CountAsync(Guid recipeId, CancellationToken cancellationToken) =>
            Task.FromResult(Images.Count(i => i.RecipeId == recipeId));

        public Task<IReadOnlyList<RecipeImage>> ListAsync(Guid recipeId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RecipeImage>>(Images.Where(i => i.RecipeId == recipeId).OrderBy(i => i.SortOrder).ToList());

        public Task<IReadOnlyDictionary<Guid, Guid>> GetFirstImageIdsAsync(IReadOnlyList<Guid> recipeIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, Guid>>(Images
                .Where(i => recipeIds.Contains(i.RecipeId))
                .GroupBy(i => i.RecipeId)
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.SortOrder).First().Id));

        public Task<RecipeImage?> GetAsync(Guid recipeId, Guid imageId, CancellationToken cancellationToken) =>
            Task.FromResult(Images.SingleOrDefault(i => i.RecipeId == recipeId && i.Id == imageId));

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
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<StoredImage?> GetAsync(Guid id, bool includeContent, CancellationToken cancellationToken) =>
            Task.FromResult(Images.SingleOrDefault(image => image.Id == id)?.Image);
        public void Add(StoredImage image) { }
        public Task UpdateAltTextAsync(Guid id, string? altText, DateTimeOffset updatedAt, CancellationToken cancellationToken)
        {
            var image = Images.SingleOrDefault(value => value.Id == id)?.Image;
            if (image is not null) { image.AltText = altText; image.UpdatedAtUtc = updatedAt; }
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken) { Images.RemoveAll(image => image.Id == id); return Task.CompletedTask; }
    }
}
