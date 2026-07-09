namespace Kotlet.Application.VideoTranscripts;

public enum Platform
{
    YouTube,
    TikTok
}

public enum VideoTranscriptStatus
{
    Success,
    InvalidUrl,
    NotConfigured,
    PrivateVideo,
    NoTranscript,
    RateLimited,
    OutOfCredits,
    Failed
}

public sealed record VideoContent(
    string Transcript,
    string? Title,
    string? Description,
    string? Author,
    Platform Platform,
    Uri? SourceUrl = null,
    string? Language = null);

public sealed record VideoTranscriptResult(
    VideoTranscriptStatus Status,
    VideoContent? Content = null,
    string? Message = null);

public interface IVideoTranscriptProvider
{
    Task<VideoTranscriptResult> GetAsync(Uri url, CancellationToken cancellationToken);
}
