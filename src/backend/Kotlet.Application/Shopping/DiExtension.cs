using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Shopping;

public static class DiExtension
{
    public static IServiceCollection AddShoppingApplication(this IServiceCollection services) =>
        services.AddScoped<ShoppingListService>();
}
