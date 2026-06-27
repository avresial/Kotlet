using Kotlet.Application.Menu.GetMenu;
using Kotlet.Domain.Menu;

namespace Kotlet.Infrastructure.Menu;

internal sealed class InMemoryMenuReader : IMenuReader
{
    private static readonly IReadOnlyCollection<MenuEntry> Entries =
    [
        new(Guid.Parse("f42f01bb-c920-47dc-9fb4-c01c9507a8c8"), DateOnly.FromDateTime(DateTime.Today), "Kotlet schabowy")
    ];

    public Task<IReadOnlyCollection<MenuEntry>> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Entries);
}
