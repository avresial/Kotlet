namespace Kotlet.Application.VideoTranscripts;

public sealed class VideoTranscriptService(IVideoTranscriptProvider provider)
{
    public Task<VideoTranscriptResult> GetAsync(Uri url, CancellationToken cancellationToken) =>
        IsSupported(url)
            ? provider.GetAsync(url, cancellationToken)
            : Task.FromResult(new VideoTranscriptResult(
                VideoTranscriptStatus.InvalidUrl,
                Message: "Only YouTube and TikTok video URLs are supported."));

    private static bool IsSupported(Uri? url)
    {
        if (url is null || !url.IsAbsoluteUri ||
            (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var host = url.Host.TrimEnd('.').ToLowerInvariant();
        return host is "youtube.com" or "www.youtube.com" or "m.youtube.com" or "youtu.be" or
            "www.youtu.be" or "tiktok.com" or "www.tiktok.com" or "vm.tiktok.com" or "vt.tiktok.com";
    }
}
