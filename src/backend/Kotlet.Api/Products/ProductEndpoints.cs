using Kotlet.Api.Auth;
using Kotlet.Application.Products;

namespace Kotlet.Api.Products;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var products = endpoints.MapGroup("/api/products").WithTags("Products").RequireAuthorization();
        products.MapGet("", async (ICurrentUser user, ProductService service, CancellationToken ct) =>
            user.HouseId is { } houseId ? Results.Ok(await service.GetProductsAsync(houseId, ct)) : Results.Unauthorized());
        products.MapGet("/lookup/{barcode}", async (string barcode, bool? refreshNutrition, ICurrentUser user, ProductService service, CancellationToken ct) =>
        {
            if (user.HouseId is not { } houseId)
            {
                return Results.Unauthorized();
            }
            var result = refreshNutrition == true
                ? await service.EnrichNutritionAsync(houseId, barcode, ct)
                : await service.LookupAsync(houseId, barcode, ct);
            return result is null ? Results.ValidationProblem(new Dictionary<string, string[]> { ["barcode"] = ["Enter a valid EAN/GTIN barcode, including its check digit."] }) : Results.Ok(result);
        });
        products.MapPut("/{barcode}", async (string barcode, SaveProductCommand command, ICurrentUser user, ProductService service, CancellationToken ct) =>
            user.HouseId is { } houseId ? ToHttpResult(await service.SaveAsync(houseId, barcode, command, ct)) : Results.Unauthorized());
        products.MapGet("/export", async (ICurrentUser user, ProductService service, CancellationToken ct) =>
            user.HouseId is { } houseId ? Results.Ok(new
            {
                attribution = "Contains information from Open Food Facts, available under the Open Database License (ODbL). May also contain USDA FoodData Central data (public domain / CC0).",
                license = "https://opendatacommons.org/licenses/odbl/1-0/",
                source = "https://world.openfoodfacts.org",
                products = await service.GetProductsAsync(houseId, ct)
            }) : Results.Unauthorized());

        var stock = endpoints.MapGroup("/api/product-stock").WithTags("Product stock").RequireAuthorization();
        stock.MapGet("", async (ICurrentUser user, ProductService service, CancellationToken ct) =>
            user.HouseId is { } houseId ? Results.Ok(await service.GetStockAsync(houseId, ct)) : Results.Unauthorized());
        stock.MapPost("", async (SaveProductStockCommand command, ICurrentUser user, ProductService service, CancellationToken ct) =>
            user.HouseId is { } houseId ? ToHttpResult(await service.AddStockAsync(houseId, command, ct)) : Results.Unauthorized());
        stock.MapPut("/{id:guid}", async (Guid id, UpdateProductStockCommand command, ICurrentUser user, ProductService service, CancellationToken ct) =>
            user.HouseId is { } houseId ? ToHttpResult(await service.UpdateStockAsync(houseId, id, command, ct)) : Results.Unauthorized());
        stock.MapDelete("/{id:guid}", async (Guid id, ICurrentUser user, ProductService service, CancellationToken ct) =>
            user.HouseId is not { } houseId ? Results.Unauthorized() :
            await service.DeleteStockAsync(houseId, id, ct) ? Results.NoContent() : Results.NotFound());
        return endpoints;
    }

    private static IResult ToHttpResult<T>(ProductResult<T> result) => result.Status switch
    {
        "success" => Results.Ok(result.Value),
        "not_found" => Results.NotFound(),
        "conflict" => Results.Conflict(new { result.Message }),
        "validation_failed" => Results.ValidationProblem(result.Errors!),
        _ => throw new InvalidOperationException("Unknown product operation status.")
    };
}
