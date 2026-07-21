using Kotlet.Api.Auth;
using Kotlet.Application.PreparedMeals;
using Microsoft.AspNetCore.Mvc;

namespace Kotlet.Api.PreparedMeals;

public static class PreparedMealEndpoints
{
    public static IEndpointRouteBuilder MapPreparedMealEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/prepared-meals").WithTags("PreparedMeals").RequireAuthorization();
        group.MapGet("", List); group.MapGet("/{id:guid}", Get); group.MapPost("", Create); group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Archive); group.MapPost("/{id:guid}/restore", Restore);
        group.MapPost("/{id:guid}/images", UploadImage).DisableAntiforgery();
        group.MapGet("/{id:guid}/images", ListImages);
        group.MapGet("/{id:guid}/images/{imageId:guid}/content", GetImageContent);
        group.MapPatch("/{id:guid}/images/{imageId:guid}", UpdateImage);
        group.MapPut("/{id:guid}/images/order", ReorderImages);
        group.MapDelete("/{id:guid}/images/{imageId:guid}", DeleteImage);
        return endpoints;
    }
    private static async Task<IResult> List(bool includeArchived, ICurrentUser user, PreparedMealService service, CancellationToken ct) => user.HouseId is { } houseId ? Results.Ok(await service.ListAsync(houseId, includeArchived, ct)) : Results.Unauthorized();
    private static async Task<IResult> Get(Guid id, ICurrentUser user, PreparedMealService service, CancellationToken ct) => user.HouseId is not { } houseId ? Results.Unauthorized() : await service.GetAsync(id, houseId, ct) is { } meal ? Results.Ok(meal) : Results.NotFound();
    private static async Task<IResult> Create(SavePreparedMealRequest request, ICurrentUser user, PreparedMealService service, CancellationToken ct) => user.HouseId is not { } houseId ? Results.Unauthorized() : ToResult(await service.CreateAsync(houseId, request, ct), true);
    private static async Task<IResult> Update(Guid id, SavePreparedMealRequest request, ICurrentUser user, PreparedMealService service, CancellationToken ct) => user.HouseId is not { } houseId ? Results.Unauthorized() : ToResult(await service.UpdateAsync(id, houseId, request, ct), false);
    private static async Task<IResult> Archive(Guid id, ICurrentUser user, PreparedMealService service, CancellationToken ct) => user.HouseId is { } houseId && await service.SetArchivedAsync(id, houseId, true, ct) == PreparedMealOperationStatus.Success ? Results.NoContent() : Results.NotFound();
    private static async Task<IResult> Restore(Guid id, ICurrentUser user, PreparedMealService service, CancellationToken ct) => user.HouseId is { } houseId && await service.SetArchivedAsync(id, houseId, false, ct) == PreparedMealOperationStatus.Success ? Results.NoContent() : Results.NotFound();
    private static IResult ToResult(PreparedMealOperationResult result, bool created) => result.Status switch { PreparedMealOperationStatus.Success when created => Results.Created($"/api/prepared-meals/{result.Meal!.Id}", result.Meal), PreparedMealOperationStatus.Success => Results.Ok(result.Meal), PreparedMealOperationStatus.NotFound => Results.NotFound(), _ => Results.ValidationProblem(result.ValidationErrors!) };

    private static async Task<IResult> UploadImage(Guid id, IFormFile file, [FromForm] string? altText, ICurrentUser user, PreparedMealImageService service, CancellationToken ct)
    {
        if (user.HouseId is not { } houseId) return Results.Unauthorized();
        if (file.Length > PreparedMealImageService.MaxFileSizeBytes) return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Image file cannot exceed 5 MB."] });
        using var memory = new MemoryStream(); await file.CopyToAsync(memory, ct);
        var result = await service.AddAsync(id, houseId, file.FileName, file.ContentType, memory.ToArray(), altText, ct);
        return result.Status switch { PreparedMealOperationStatus.Success => Results.Created(result.Image!.ContentUrl, result.Image), PreparedMealOperationStatus.NotFound => Results.NotFound(), _ => Results.ValidationProblem(result.Errors!) };
    }
    private static async Task<IResult> ListImages(Guid id, ICurrentUser user, PreparedMealImageService service, CancellationToken ct) => user.HouseId is not { } houseId ? Results.Unauthorized() : await service.ListAsync(id, houseId, ct) is { } images ? Results.Ok(images) : Results.NotFound();
    private static async Task<IResult> GetImageContent(Guid id, Guid imageId, ICurrentUser user, PreparedMealImageService service, HttpContext context, CancellationToken ct) { if (user.HouseId is not { } houseId) return Results.Unauthorized(); var image = await service.GetContentAsync(id, imageId, houseId, ct); if (image is null) return Results.NotFound(); context.Response.Headers.CacheControl = "private,max-age=86400"; return Results.File(image.Content, image.ContentType, image.FileName); }
    private static async Task<IResult> UpdateImage(Guid id, Guid imageId, UpdatePreparedMealImageRequest request, ICurrentUser user, PreparedMealImageService service, CancellationToken ct) => user.HouseId is not { } houseId ? Results.Unauthorized() : await service.UpdateAsync(id, imageId, houseId, request.AltText, ct) switch { PreparedMealOperationStatus.Success => Results.NoContent(), PreparedMealOperationStatus.NotFound => Results.NotFound(), _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["altText"] = ["Alt text cannot exceed 300 characters."] }) };
    private static async Task<IResult> ReorderImages(Guid id, ReorderPreparedMealImagesRequest request, ICurrentUser user, PreparedMealImageService service, CancellationToken ct) => user.HouseId is not { } houseId ? Results.Unauthorized() : await service.ReorderAsync(id, houseId, request.ImageIds, ct) switch { PreparedMealOperationStatus.Success => Results.NoContent(), PreparedMealOperationStatus.NotFound => Results.NotFound(), _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["imageIds"] = ["Image ids must contain every image exactly once."] }) };
    private static async Task<IResult> DeleteImage(Guid id, Guid imageId, ICurrentUser user, PreparedMealImageService service, CancellationToken ct) => user.HouseId is { } houseId && await service.DeleteAsync(id, imageId, houseId, ct) == PreparedMealOperationStatus.Success ? Results.NoContent() : Results.NotFound();
}

public sealed record UpdatePreparedMealImageRequest(string? AltText);
public sealed record ReorderPreparedMealImagesRequest(IReadOnlyList<Guid> ImageIds);
