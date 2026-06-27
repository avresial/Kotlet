using Kotlet.Domain.Menu;

namespace Kotlet.Application.Menu.GetMenu;

public sealed record GetMenuQuery;

public interface IMenuReader
{
    Task<IReadOnlyCollection<MenuEntry>> GetAsync(CancellationToken cancellationToken);
}

public sealed class GetMenuQueryHandler(IMenuReader menuReader)
{
    public Task<IReadOnlyCollection<MenuEntry>> Handle(
        GetMenuQuery query,
        CancellationToken cancellationToken) => menuReader.GetAsync(cancellationToken);
}
