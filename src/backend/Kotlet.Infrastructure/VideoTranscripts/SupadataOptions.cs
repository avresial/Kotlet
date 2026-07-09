namespace Kotlet.Infrastructure.VideoTranscripts;

public sealed class SupadataOptions
{
    public const string SectionName = "Transcripts:Supadata";
    public const string DefaultBaseUrl = "https://api.supadata.ai/v1";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
