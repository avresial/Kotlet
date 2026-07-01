using Kotlet.Application.Shopping;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.Shopping;

public static class DiExtension
{
    public static IServiceCollection AddShoppingInfrastructure(this IServiceCollection services) =>
        services.AddScoped<IShoppingListRepository, ShoppingListRepository>();
}
