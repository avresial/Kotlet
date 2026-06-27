using Kotlet.Application.Menu.GetMenu;
using Kotlet.Domain.Menu;

namespace Kotlet.Application.UnitTests.Menu.GetMenu;

public sealed class GetMenuQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEntriesFromReader()
    {
        var expected = new MenuEntry(Guid.NewGuid(), new DateOnly(2026, 6, 27), "Kotlet schabowy");
        var reader = new StubMenuReader([expected]);
        var handler = new GetMenuQueryHandler(reader);

        var result = await handler.Handle(new GetMenuQuery(), CancellationToken.None);

        var entry = Assert.Single(result);
        Assert.Equal(expected, entry);
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToReader()
    {
        using var cts = new CancellationTokenSource();
        var reader = new StubMenuReader([]);
        var handler = new GetMenuQueryHandler(reader);

        await handler.Handle(new GetMenuQuery(), cts.Token);

        Assert.Equal(cts.Token, reader.LastToken);
    }

    private sealed class StubMenuReader(IReadOnlyCollection<MenuEntry> entries) : IMenuReader
    {
        public CancellationToken LastToken { get; private set; }

        public Task<IReadOnlyCollection<MenuEntry>> GetAsync(CancellationToken cancellationToken)
        {
            LastToken = cancellationToken;
            return Task.FromResult(entries);
        }
    }
}
