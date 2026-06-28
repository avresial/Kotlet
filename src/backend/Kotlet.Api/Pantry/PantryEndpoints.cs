using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Application.Pantry;

namespace Kotlet.Api.Pantry;

public static class PantryEndpoints
{
    public static IEndpointRouteBuilder MapPantryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var pantry = endpoints.MapGroup("/api/pantry").WithTags("Pantry").RequireAuthorization();
        pantry.MapGet("", async (ICurrentUser user, PantryService service, ILanguageContext language, CancellationToken ct) =>
            user.HouseId is { } houseId ? Results.Ok(await service.GetAllAsync(houseId, language.Language, ct)) : Results.Unauthorized()).WithName("GetPantry");
        pantry.MapPost("", Create).WithName("CreatePantryItem");
        pantry.MapPut("/{id:guid}", Update).WithName("UpdatePantryItem");
        pantry.MapDelete("/{id:guid}", Delete).WithName("DeletePantryItem");
        return endpoints;
    }

    private static async Task<IResult> Create(SavePantryItemCommand command, ICurrentUser user, PantryService service, ILanguageContext language, CancellationToken ct) =>
        user.HouseId is { } houseId ? ToHttpResult(await service.CreateAsync(houseId, command, language.Language, ct), true) : Results.Unauthorized();
    private static async Task<IResult> Update(Guid id, UpdatePantryQuantityCommand command, ICurrentUser user, PantryService service, ILanguageContext language, CancellationToken ct) =>
        user.HouseId is { } houseId ? ToHttpResult(await service.UpdateAsync(id, houseId, command.Quantity, language.Language, ct), false) : Results.Unauthorized();
    private static async Task<IResult> Delete(Guid id, ICurrentUser user, PantryService service, CancellationToken ct) =>
        user.HouseId is not { } houseId ? Results.Unauthorized() :
            await service.DeleteAsync(id, houseId, ct) == PantryOperationStatus.Success ? Results.NoContent() : Results.NotFound();
    private static IResult ToHttpResult(PantryOperationResult result, bool created) => result.Status switch
    {
        PantryOperationStatus.Success when created => Results.Created($"/api/pantry/{result.Item!.Id}", result.Item),
        PantryOperationStatus.Success => Results.Ok(result.Item),
        PantryOperationStatus.NotFound => Results.NotFound(),
        PantryOperationStatus.Conflict => Results.Conflict(new { result.Message }),
        PantryOperationStatus.ValidationFailed => Results.ValidationProblem(result.ValidationErrors!),
        _ => throw new InvalidOperationException()
    };
}

public sealed record UpdatePantryQuantityCommand(decimal Quantity);
