using Kotlet.Application.Ai;
using Kotlet.Domain.Ai;
using Xunit;

namespace Kotlet.Application.UnitTests.Ai;

public sealed class UserAiProviderServiceTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static SaveAiProviderConfigurationCommand ValidCommand(
        string? providerName = "OpenAI",
        string? baseUrl = "https://api.openai.com/v1",
        string? defaultModel = "gpt-4o",
        bool isEnabled = true,
        string? apiKey = "sk-secret") =>
        new(providerName, baseUrl, defaultModel, isEnabled, apiKey);

    // ---- Get ----

    [Fact]
    public async Task Get_ReturnsNull_WhenNoConfiguration()
    {
        var service = new UserAiProviderService(new FakeRepository());

        Assert.Null(await service.GetAsync(UserId, CancellationToken.None));
    }

    [Fact]
    public async Task Get_DoesNotExposeApiKeyButReportsItsPresence()
    {
        var repo = new FakeRepository(Configuration(apiKey: "sk-secret"));
        var service = new UserAiProviderService(repo);

        var dto = await service.GetAsync(UserId, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.True(dto.HasApiKey);
        // The DTO has no property carrying the raw key value.
        Assert.DoesNotContain("sk-secret", System.Text.Json.JsonSerializer.Serialize(dto));
    }

    [Fact]
    public async Task Get_ReportsMissingApiKey_WhenKeyIsNull()
    {
        var repo = new FakeRepository(Configuration(apiKey: null));
        var service = new UserAiProviderService(repo);

        var dto = await service.GetAsync(UserId, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.False(dto.HasApiKey);
    }

    // ---- Save (create) ----

    [Fact]
    public async Task Save_WithValidData_CreatesConfiguration()
    {
        var repo = new FakeRepository();
        var service = new UserAiProviderService(repo);

        var result = await service.SaveAsync(UserId, ValidCommand(), CancellationToken.None);

        Assert.Null(result.ValidationErrors);
        Assert.NotNull(result.Configuration);
        Assert.Equal("OpenAI", result.Configuration.ProviderName);
        Assert.True(result.Configuration.HasApiKey);
        var stored = Assert.Single(repo.Items);
        Assert.Equal(UserId, stored.UserId);
        Assert.Equal("sk-secret", stored.ApiKey);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Save_TrimsProviderNameBaseUrlAndModel()
    {
        var repo = new FakeRepository();
        var service = new UserAiProviderService(repo);

        await service.SaveAsync(UserId,
            ValidCommand(providerName: "  OpenAI  ", baseUrl: "  https://api.openai.com  ", defaultModel: "  gpt-4o  "),
            CancellationToken.None);

        var stored = Assert.Single(repo.Items);
        Assert.Equal("OpenAI", stored.ProviderName);
        Assert.Equal("https://api.openai.com", stored.BaseUrl);
        Assert.Equal("gpt-4o", stored.DefaultModel);
    }

    [Fact]
    public async Task Save_StoresNullDefaultModel_WhenBlank()
    {
        var repo = new FakeRepository();
        var service = new UserAiProviderService(repo);

        await service.SaveAsync(UserId, ValidCommand(defaultModel: "   "), CancellationToken.None);

        Assert.Null(repo.Items.Single().DefaultModel);
    }

    [Fact]
    public async Task Save_DisabledProviderWithoutApiKeyOrBaseUrl_Succeeds()
    {
        var repo = new FakeRepository();
        var service = new UserAiProviderService(repo);

        var result = await service.SaveAsync(UserId,
            ValidCommand(baseUrl: null, isEnabled: false, apiKey: null), CancellationToken.None);

        Assert.Null(result.ValidationErrors);
        Assert.NotNull(result.Configuration);
        Assert.False(result.Configuration.HasApiKey);
    }

    // ---- Save (update) ----

    [Fact]
    public async Task Save_UpdatesExistingConfigurationInPlace()
    {
        var existing = Configuration(providerName: "Old", apiKey: "old-key");
        var repo = new FakeRepository(existing);
        var service = new UserAiProviderService(repo);

        var result = await service.SaveAsync(UserId,
            ValidCommand(providerName: "New", apiKey: "new-key"), CancellationToken.None);

        Assert.Null(result.ValidationErrors);
        Assert.Single(repo.Items);
        Assert.Equal("New", existing.ProviderName);
        Assert.Equal("new-key", existing.ApiKey);
    }

    [Fact]
    public async Task Save_WithBlankApiKey_KeepsExistingKey()
    {
        var existing = Configuration(apiKey: "existing-key");
        var repo = new FakeRepository(existing);
        var service = new UserAiProviderService(repo);

        var result = await service.SaveAsync(UserId, ValidCommand(apiKey: "   "), CancellationToken.None);

        Assert.Null(result.ValidationErrors);
        Assert.Equal("existing-key", existing.ApiKey);
        Assert.True(result.Configuration!.HasApiKey);
    }

    [Fact]
    public async Task Save_EnabledWithoutNewKeyButExistingKey_Succeeds()
    {
        var existing = Configuration(apiKey: "existing-key");
        var repo = new FakeRepository(existing);
        var service = new UserAiProviderService(repo);

        var result = await service.SaveAsync(UserId,
            ValidCommand(isEnabled: true, apiKey: null), CancellationToken.None);

        Assert.Null(result.ValidationErrors);
    }

    // ---- Validation ----

    [Fact]
    public async Task Save_WithoutProviderName_FailsValidation()
    {
        var repo = new FakeRepository();
        var service = new UserAiProviderService(repo);

        var result = await service.SaveAsync(UserId, ValidCommand(providerName: "  "), CancellationToken.None);

        Assert.NotNull(result.ValidationErrors);
        Assert.True(result.ValidationErrors.ContainsKey("providerName"));
        Assert.Empty(repo.Items);
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Save_WithTooLongProviderName_FailsValidation()
    {
        var service = new UserAiProviderService(new FakeRepository());

        var result = await service.SaveAsync(UserId,
            ValidCommand(providerName: new string('A', 101)), CancellationToken.None);

        Assert.True(result.ValidationErrors!.ContainsKey("providerName"));
    }

    [Fact]
    public async Task Save_EnabledWithoutBaseUrl_FailsValidation()
    {
        var service = new UserAiProviderService(new FakeRepository());

        var result = await service.SaveAsync(UserId,
            ValidCommand(baseUrl: null, isEnabled: true), CancellationToken.None);

        Assert.True(result.ValidationErrors!.ContainsKey("baseUrl"));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("/relative/path")]
    public async Task Save_WithNonHttpBaseUrl_FailsValidation(string baseUrl)
    {
        var service = new UserAiProviderService(new FakeRepository());

        var result = await service.SaveAsync(UserId, ValidCommand(baseUrl: baseUrl), CancellationToken.None);

        Assert.True(result.ValidationErrors!.ContainsKey("baseUrl"));
    }

    [Fact]
    public async Task Save_EnabledWithoutAnyApiKey_FailsValidation()
    {
        var service = new UserAiProviderService(new FakeRepository());

        var result = await service.SaveAsync(UserId,
            ValidCommand(isEnabled: true, apiKey: null), CancellationToken.None);

        Assert.True(result.ValidationErrors!.ContainsKey("apiKey"));
    }

    [Fact]
    public async Task Save_WithTooLongApiKey_FailsValidation()
    {
        var service = new UserAiProviderService(new FakeRepository());

        var result = await service.SaveAsync(UserId,
            ValidCommand(apiKey: new string('k', 4097)), CancellationToken.None);

        Assert.True(result.ValidationErrors!.ContainsKey("apiKey"));
    }

    [Fact]
    public async Task Save_WithTooLongDefaultModel_FailsValidation()
    {
        var service = new UserAiProviderService(new FakeRepository());

        var result = await service.SaveAsync(UserId,
            ValidCommand(defaultModel: new string('m', 201)), CancellationToken.None);

        Assert.True(result.ValidationErrors!.ContainsKey("defaultModel"));
    }

    // ---- Delete ----

    [Fact]
    public async Task Delete_RemovesConfigurationAndReturnsTrue()
    {
        var repo = new FakeRepository(Configuration());
        var service = new UserAiProviderService(repo);

        var deleted = await service.DeleteAsync(UserId, CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(repo.Items);
        Assert.Equal(1, repo.SaveCount);
    }

    [Fact]
    public async Task Delete_ReturnsFalse_WhenNothingToDelete()
    {
        var repo = new FakeRepository();
        var service = new UserAiProviderService(repo);

        var deleted = await service.DeleteAsync(UserId, CancellationToken.None);

        Assert.False(deleted);
        Assert.Equal(0, repo.SaveCount);
    }

    private static UserAiProviderConfiguration Configuration(
        string providerName = "OpenAI", string? apiKey = "sk-secret") => new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            ProviderName = providerName,
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = apiKey,
            DefaultModel = "gpt-4o",
            IsEnabled = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

    private sealed class FakeRepository(UserAiProviderConfiguration? seed = null) : IUserAiProviderRepository
    {
        public List<UserAiProviderConfiguration> Items { get; } = seed is null ? [] : [seed];
        public int SaveCount { get; private set; }

        public Task<UserAiProviderConfiguration?> GetAsync(Guid userId, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(x => x.UserId == userId));

        public void Add(UserAiProviderConfiguration configuration) => Items.Add(configuration);
        public void Remove(UserAiProviderConfiguration configuration) => Items.Remove(configuration);
        public Task SaveChangesAsync(CancellationToken cancellationToken) { SaveCount++; return Task.CompletedTask; }
    }
}
