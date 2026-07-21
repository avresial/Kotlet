using Kotlet.Application.Images;
using Kotlet.Domain.PreparedMeals;

namespace Kotlet.Application.PreparedMeals;

public sealed record PreparedMealImageResponse(
    Guid Id,
    Guid PreparedMealId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? AltText,
    int SortOrder,
    string ContentUrl,
    DateTimeOffset CreatedAtUtc);

public sealed class PreparedMealImageService(IPreparedMealImageRepository repository, StoredImageService imageStorage)
{
    public const long MaxFileSizeBytes = StoredImageService.MaxFileSizeBytes;
    public const int MaxImages = StoredImageService.MaxImagesPerOwner;

    public async Task<(
        PreparedMealOperationStatus Status,
        PreparedMealImageResponse? Image,
        IReadOnlyDictionary<string, string[]>? Errors)> AddAsync(
        Guid mealId,
        Guid houseId,
        string fileName,
        string contentType,
        byte[] content,
        string? altText,
        CancellationToken ct)
    {
        if (!await repository.MealExistsAsync(mealId, houseId, ct))
            return (PreparedMealOperationStatus.NotFound, null, null);

        var images = await repository.ListAsync(mealId, ct);
        if (images.Count >= MaxImages)
        {
            return (PreparedMealOperationStatus.ValidationFailed, null,
                new Dictionary<string, string[]>
                {
                    ["file"] = [$"A prepared meal cannot have more than {MaxImages} images."]
                });
        }

        var stored = await imageStorage.CreateAsync(fileName, contentType, content, altText, null, ct);
        if (stored.Errors is not null)
            return (PreparedMealOperationStatus.ValidationFailed, null, stored.Errors);

        var image = new PreparedMealImage
        {
            Id = stored.Image!.Id,
            PreparedMealId = mealId,
            Image = stored.Image,
            SortOrder = images.Count
        };
        repository.Add(image);
        await repository.SaveChangesAsync(ct);
        return (PreparedMealOperationStatus.Success, ToResponse(image), null);
    }

    public async Task<IReadOnlyList<PreparedMealImageResponse>?> ListAsync(
        Guid mealId,
        Guid houseId,
        CancellationToken ct)
    {
        if (!await repository.MealExistsAsync(mealId, houseId, ct))
            return null;

        return (await repository.ListAsync(mealId, ct)).Select(ToResponse).ToList();
    }

    public async Task<StoredImageContent?> GetContentAsync(
        Guid mealId,
        Guid imageId,
        Guid houseId,
        CancellationToken ct)
    {
        if (!await repository.MealExistsAsync(mealId, houseId, ct)
            || await repository.GetAsync(mealId, imageId, ct) is null)
        {
            return null;
        }

        return await imageStorage.GetContentAsync(imageId, ct);
    }

    public async Task<PreparedMealOperationStatus> UpdateAsync(
        Guid mealId,
        Guid imageId,
        Guid houseId,
        string? altText,
        CancellationToken ct)
    {
        if (!await repository.MealExistsAsync(mealId, houseId, ct)
            || await repository.GetAsync(mealId, imageId, ct) is null)
        {
            return PreparedMealOperationStatus.NotFound;
        }

        return await imageStorage.UpdateAltTextAsync(imageId, altText, ct)
            ? PreparedMealOperationStatus.Success
            : PreparedMealOperationStatus.ValidationFailed;
    }

    public async Task<PreparedMealOperationStatus> ReorderAsync(
        Guid mealId,
        Guid houseId,
        IReadOnlyList<Guid> ids,
        CancellationToken ct)
    {
        if (!await repository.MealExistsAsync(mealId, houseId, ct))
            return PreparedMealOperationStatus.NotFound;

        var images = await repository.ListAsync(mealId, ct);
        if (ids.Count != images.Count
            || ids.Distinct().Count() != ids.Count
            || ids.Any(id => images.All(image => image.Id != id)))
        {
            return PreparedMealOperationStatus.ValidationFailed;
        }

        await repository.UpdateSortOrdersAsync(mealId, ids, ct);
        return PreparedMealOperationStatus.Success;
    }

    public async Task<PreparedMealOperationStatus> DeleteAsync(
        Guid mealId,
        Guid imageId,
        Guid houseId,
        CancellationToken ct)
    {
        if (!await repository.MealExistsAsync(mealId, houseId, ct)
            || await repository.GetAsync(mealId, imageId, ct) is null)
        {
            return PreparedMealOperationStatus.NotFound;
        }

        await imageStorage.DeleteAsync(imageId, ct);
        await repository.UpdateSortOrdersAsync(
            mealId,
            (await repository.ListAsync(mealId, ct)).Select(image => image.Id).ToList(),
            ct);
        return PreparedMealOperationStatus.Success;
    }

    private static PreparedMealImageResponse ToResponse(PreparedMealImage image) => new(
        image.Id,
        image.PreparedMealId,
        image.Image.FileName,
        image.Image.ContentType,
        image.Image.FileSizeBytes,
        image.Image.AltText,
        image.SortOrder,
        $"/api/prepared-meals/{image.PreparedMealId}/images/{image.Id}/content",
        image.Image.CreatedAtUtc);
}
