using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.MealPlanner;

public static class DiExtension
{
    public static IServiceCollection AddMealPlannerApplication(this IServiceCollection services) =>
        services.AddScoped<MealPlanQueryService>()
            .AddScoped<MealPlanItemService>()
            .AddScoped<MealPlanCopyService>()
            .AddScoped<MealPlanSlotMutationService>()
            .AddScoped<MealParticipantService>();
}
