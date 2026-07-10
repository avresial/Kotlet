using Kotlet.Application.Recipes;
using Kotlet.Domain.Recipes;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Recipes;

internal sealed class RecipeImportJobRepository(KotletDbContext dbContext) : IRecipeImportJobRepository
{
    public Task<RecipeImportJob?> GetAsync(Guid id, Guid? houseId, bool tracked, CancellationToken cancellationToken)
    {
        var query = tracked ? dbContext.RecipeImportJobs : dbContext.RecipeImportJobs.AsNoTracking();
        return query.SingleOrDefaultAsync(x => x.Id == id && (houseId == null || x.HouseId == houseId), cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListActiveIdsAsync(CancellationToken cancellationToken) =>
        await dbContext.RecipeImportJobs.AsNoTracking()
            .Where(x => x.Status != RecipeImportJobStatus.ReadyForReview && x.Status != RecipeImportJobStatus.Failed)
            .Select(x => x.Id).ToListAsync(cancellationToken);

    public void Add(RecipeImportJob job) => dbContext.RecipeImportJobs.Add(job);
    public void Remove(RecipeImportJob job) => dbContext.RecipeImportJobs.Remove(job);
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
