using Kotlet.Application.Images;
using Kotlet.Application.PreparedMeals;
using Kotlet.Domain.PreparedMeals;
using Kotlet.Domain.Images;
using Xunit;

namespace Kotlet.Application.UnitTests.PreparedMeals;

public sealed class PreparedMealImageServiceTests
{
    [Fact]
    public async Task AddAsync_ProcessesAndStoresImage()
    {
        var repository = new FakeRepository();
        var service = new PreparedMealImageService(repository, new StoredImageService(repository, new FakeProcessor()));

        var result = await service.AddAsync(repository.MealId, repository.HouseId, "meal.png", "image/png", [1], " Dinner ", default);

        Assert.Equal(PreparedMealOperationStatus.Success, result.Status);
        var image = Assert.Single(repository.Images);
        Assert.Equal(("meal.webp", "image/webp", "Dinner"), (image.Image.FileName, image.Image.ContentType, image.Image.AltText));
    }

    private sealed class FakeProcessor : IImageProcessor
    {
        public Task<ImageProcessingResult> ProcessAsync(Stream image, ImageProcessingOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImageProcessingResult([2, 3], "image/webp", 10, 10));
    }

    private sealed class FakeRepository : IPreparedMealImageRepository, IStoredImageRepository
    {
        public Guid MealId { get; } = Guid.NewGuid();
        public Guid HouseId { get; } = Guid.NewGuid();
        public List<PreparedMealImage> Images { get; } = [];
        public Task<bool> MealExistsAsync(Guid mealId, Guid houseId, CancellationToken ct) => Task.FromResult(mealId == MealId && houseId == HouseId);
        public Task<IReadOnlyList<PreparedMealImage>> ListAsync(Guid mealId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PreparedMealImage>>(Images);
        public Task<PreparedMealImage?> GetAsync(Guid mealId, Guid imageId, CancellationToken ct) => Task.FromResult(Images.SingleOrDefault(image => image.Id == imageId));
        public void Add(PreparedMealImage image) => Images.Add(image);
        public Task UpdateSortOrdersAsync(Guid mealId, IReadOnlyList<Guid> ids, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<StoredImage?> GetAsync(Guid id, bool includeContent, CancellationToken ct) => Task.FromResult(Images.SingleOrDefault(image => image.Id == id)?.Image);
        public void Add(StoredImage image) { }
        public Task UpdateAltTextAsync(Guid id, string? altText, DateTimeOffset updatedAt, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
    }
}
