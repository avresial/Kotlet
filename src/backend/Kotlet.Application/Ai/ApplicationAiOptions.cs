namespace Kotlet.Application.Ai;

/// <summary>
/// Application-wide AI credentials, distinct from the per-user provider configuration. These back
/// features Kotlet runs on its own behalf — such as the ingredient-translation worker — rather than
/// on behalf of a signed-in user. Bound from the <c>Ai:Application</c> configuration section; when
/// <see cref="ApiKey"/> is blank the application AI features stay dormant.
/// </summary>
public sealed class ApplicationAiOptions
{
    public const string SectionName = "Ai:Application";

    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
