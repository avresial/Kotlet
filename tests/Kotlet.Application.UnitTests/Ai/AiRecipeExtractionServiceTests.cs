using Kotlet.Application.Ai;
using Kotlet.Application.VideoTranscripts;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kotlet.Application.UnitTests.Ai;

public sealed class AiRecipeExtractionServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly VideoContent Content = new(
        "Mix flour and eggs. Bake until golden.",
        "Golden cake",
        "Use 2 cups flour and 3 eggs.",
        "Chef",
        Platform.YouTube,
        new Uri("https://youtube.com/watch?v=abc"));

    [Fact]
    public async Task ExtractAsync_ParsesDraftAndAppendsSource()
    {
        var client = new FakeClient("""
            {"isRecipe":true,"title":"Golden cake","servings":4,"ingredients":[{"name":"flour","quantity":2,"unit":"cups"},{"name":"eggs","quantity":3,"unit":""}],"steps":["Mix ingredients.","Bake until golden."],"gaps":[]}
            """);

        var result = await CreateService(client).ExtractAsync(UserId, Content, CancellationToken.None);

        Assert.Equal(RecipeExtractionStatus.Extracted, result.Status);
        Assert.NotNull(result.Draft);
        Assert.Equal("Golden cake", result.Draft.Title);
        Assert.Equal(4, result.Draft.Servings);
        Assert.Equal(2, result.Draft.Ingredients[0].Quantity);
        Assert.Contains("1. Mix ingredients.", result.Draft.InstructionsMarkdown);
        Assert.EndsWith("Imported from [Golden cake](https://youtube.com/watch?v=abc)", result.Draft.InstructionsMarkdown);
    }

    [Fact]
    public async Task ExtractAsync_IncludesDescriptionInPrompt()
    {
        var client = new FakeClient("""
            {"isRecipe":true,"title":"Golden cake","servings":1,"ingredients":[{"name":"flour","quantity":2,"unit":"cups"}],"steps":["Bake."],"gaps":[]}
            """);

        await CreateService(client).ExtractAsync(UserId, Content, CancellationToken.None);

        Assert.Contains(Content.Description!, client.LastUserMessage);
    }

    [Fact]
    public async Task ExtractAsync_WhenProviderIsNotConfigured_ReturnsNotConfigured()
    {
        var result = await new AiRecipeExtractionService(new Resolver(null))
            .ExtractAsync(UserId, Content, CancellationToken.None);

        Assert.Equal(RecipeExtractionStatus.NotConfigured, result.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExtractAsync_WithBlankTranscript_ReturnsInvalidRequest(string transcript)
    {
        var result = await CreateService(new FakeClient("{}"))
            .ExtractAsync(UserId, Content with { Transcript = transcript }, CancellationToken.None);

        Assert.Equal(RecipeExtractionStatus.InvalidRequest, result.Status);
    }

    [Fact]
    public async Task ExtractAsync_WithOversizedTranscript_ReturnsInvalidRequest()
    {
        var result = await CreateService(new FakeClient("{}"))
            .ExtractAsync(UserId, Content with { Transcript = new string('x', 30_001) }, CancellationToken.None);

        Assert.Equal(RecipeExtractionStatus.InvalidRequest, result.Status);
    }

    [Fact]
    public async Task ExtractAsync_WhenContentIsNotARecipe_ReturnsNotARecipe()
    {
        var result = await CreateService(new FakeClient("{\"isRecipe\":false,\"ingredients\":[],\"steps\":[]}"))
            .ExtractAsync(UserId, Content, CancellationToken.None);

        Assert.Equal(RecipeExtractionStatus.NotARecipe, result.Status);
    }

    [Fact]
    public async Task ExtractAsync_WithMalformedJson_ReturnsFailed()
    {
        var result = await CreateService(new FakeClient("not json"))
            .ExtractAsync(UserId, Content, CancellationToken.None);

        Assert.Equal(RecipeExtractionStatus.Failed, result.Status);
    }

    [Fact]
    public async Task ExtractAsync_WhenProviderThrows_ReturnsFailed()
    {
        var result = await CreateService(new FakeClient(throwOnCall: true))
            .ExtractAsync(UserId, Content, CancellationToken.None);

        Assert.Equal(RecipeExtractionStatus.Failed, result.Status);
    }

    private static AiRecipeExtractionService CreateService(IChatClient client) =>
        new(new Resolver(client));

    private sealed class Resolver(IChatClient? client) : IUserChatClientResolver
    {
        public Task<IChatClient?> ResolveAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(client);
    }

    private sealed class FakeClient : IChatClient
    {
        private readonly string responseText;
        private readonly bool throwOnCall;

        public FakeClient(string responseText = "", bool throwOnCall = false)
        {
            this.responseText = responseText;
            this.throwOnCall = throwOnCall;
        }

        public string LastUserMessage { get; private set; } = "";

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (throwOnCall) throw new InvalidOperationException("test failure");
            LastUserMessage = messages.Last().Text;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
