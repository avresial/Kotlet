using Kotlet.Application.Ai;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kotlet.Application.UnitTests.Ai;

public sealed class AiTranslationServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Translate_WithEmptyText_ReturnsInvalidRequest()
    {
        var service = new AiTranslationService(new NullResolver());

        var result = await service.TranslateAsync(UserId, "   ", "pl", CancellationToken.None);

        Assert.Equal(AiTranslationStatus.InvalidRequest, result.Status);
    }

    [Fact]
    public async Task Translate_WithEmptyTargetLanguage_ReturnsInvalidRequest()
    {
        var service = new AiTranslationService(new NullResolver());

        var result = await service.TranslateAsync(UserId, "flour", "", CancellationToken.None);

        Assert.Equal(AiTranslationStatus.InvalidRequest, result.Status);
    }

    [Fact]
    public async Task Translate_WithOverlyLongText_ReturnsInvalidRequest()
    {
        var service = new AiTranslationService(new NullResolver());

        var result = await service.TranslateAsync(UserId, new string('a', 501), "pl", CancellationToken.None);

        Assert.Equal(AiTranslationStatus.InvalidRequest, result.Status);
    }

    [Fact]
    public async Task Translate_WhenNoProviderConfigured_ReturnsNotConfigured()
    {
        var service = new AiTranslationService(new NullResolver());

        var result = await service.TranslateAsync(UserId, "flour", "pl", CancellationToken.None);

        Assert.Equal(AiTranslationStatus.NotConfigured, result.Status);
        Assert.Null(result.Translation);
    }

    // A user with no enabled, fully configured provider resolves to no client.
    private sealed class NullResolver : IUserChatClientResolver
    {
        public Task<IChatClient?> ResolveAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<IChatClient?>(null);
    }
}
