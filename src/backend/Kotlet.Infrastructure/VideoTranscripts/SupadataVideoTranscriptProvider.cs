using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Kotlet.Application.VideoTranscripts;

namespace Kotlet.Infrastructure.VideoTranscripts;

internal sealed class SupadataVideoTranscriptProvider(
    HttpClient httpClient,
    SupadataOptions options) : IVideoTranscriptProvider
{
    private const int MaxPollAttempts = 10;
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(250);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<VideoTranscriptResult> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        if (!options.IsConfigured)
        {
            return new(VideoTranscriptStatus.NotConfigured,
                Message: "The video transcript provider is not configured.");
        }

        try
        {
            using var transcriptRequest = CreateRequest("transcript", url, "text=true");
            using var transcriptResponse = await httpClient.SendAsync(transcriptRequest, cancellationToken);
            var transcriptOutcome = await ReadTranscriptResponseAsync(transcriptResponse, cancellationToken);
            if (transcriptOutcome.Result is not null)
            {
                return transcriptOutcome.Result;
            }

            if (transcriptOutcome.JobId is not null)
            {
                transcriptOutcome = await PollTranscriptAsync(transcriptOutcome.JobId, cancellationToken);
                if (transcriptOutcome.Result is not null)
                {
                    return transcriptOutcome.Result;
                }
            }

            var transcript = transcriptOutcome.Content;
            if (string.IsNullOrWhiteSpace(transcript))
            {
                return new(VideoTranscriptStatus.NoTranscript,
                    Message: "No transcript was found for this video.");
            }

            using var metadataRequest = CreateRequest("metadata", url);
            using var metadataResponse = await httpClient.SendAsync(metadataRequest, cancellationToken);
            if (!metadataResponse.IsSuccessStatusCode)
            {
                return MapStatus(metadataResponse.StatusCode, "The video metadata request failed.");
            }

            var metadata = await JsonSerializer.DeserializeAsync<MetadataResponse>(
                await metadataResponse.Content.ReadAsStreamAsync(cancellationToken), JsonOptions, cancellationToken);
            if (metadata is null)
            {
                return new(VideoTranscriptStatus.Failed, Message: "The video metadata response was empty.");
            }

            return new(VideoTranscriptStatus.Success, new VideoContent(
                transcript,
                metadata.Title,
                metadata.Description,
                metadata.Author?.DisplayName,
                ParsePlatform(metadata.Platform, url),
                url,
                transcriptOutcome.Language));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(VideoTranscriptStatus.Failed, Message: "The video transcript request failed.");
        }
    }

    private HttpRequestMessage CreateRequest(string endpoint, Uri url, string? query = null)
    {
        var encodedUrl = Uri.EscapeDataString(url.ToString());
        var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}?url={encodedUrl}{(query is null ? "" : $"&{query}")}");
        request.Headers.TryAddWithoutValidation("x-api-key", options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<TranscriptOutcome> PollTranscriptAsync(string jobId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxPollAttempts; attempt++)
        {
            if (attempt > 0)
            {
                await Task.Delay(PollDelay, cancellationToken);
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, $"transcript/{Uri.EscapeDataString(jobId)}");
            request.Headers.TryAddWithoutValidation("x-api-key", options.ApiKey);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var outcome = await ReadTranscriptResponseAsync(response, cancellationToken);
            if (outcome.Result is not null || outcome.Content is not null)
            {
                return outcome;
            }
        }

        return new TranscriptOutcome(Result: new(VideoTranscriptStatus.Failed,
            Message: "The video transcript is still processing."));
    }

    private static async Task<TranscriptOutcome> ReadTranscriptResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.PartialContent)
        {
            return new(Result: MapStatus(response.StatusCode, "The video transcript request failed."));
        }

        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            return new(Result: new(VideoTranscriptStatus.NoTranscript,
                Message: "No transcript was found for this video."));
        }

        using var document = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.TryGetProperty("jobId", out var jobId))
        {
            return new(JobId: jobId.GetString());
        }

        var status = root.TryGetProperty("status", out var statusElement)
            ? statusElement.GetString()
            : null;
        if (status is "queued" or "active")
        {
            return new();
        }

        if (status == "failed")
        {
            return new(Result: new(VideoTranscriptStatus.Failed, Message: "Supadata could not transcribe this video."));
        }

        return new(
            ReadContent(root),
            root.TryGetProperty("lang", out var language) ? language.GetString() : null);
    }

    private static string? ReadContent(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content))
        {
            return null;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            return string.Join(" ", content.EnumerateArray()
                .Where(item => item.TryGetProperty("text", out _))
                .Select(item => item.GetProperty("text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
        }

        return null;
    }

    private static VideoTranscriptResult MapStatus(HttpStatusCode statusCode, string message) =>
        new(statusCode switch
        {
            HttpStatusCode.Forbidden or HttpStatusCode.NotFound => VideoTranscriptStatus.PrivateVideo,
            HttpStatusCode.PaymentRequired => VideoTranscriptStatus.OutOfCredits,
            HttpStatusCode.TooManyRequests => VideoTranscriptStatus.RateLimited,
            HttpStatusCode.PartialContent => VideoTranscriptStatus.NoTranscript,
            _ => VideoTranscriptStatus.Failed
        }, Message: message);

    private static Platform ParsePlatform(string? platform, Uri url) =>
        string.Equals(platform, "tiktok", StringComparison.OrdinalIgnoreCase) ||
        url.Host.Contains("tiktok", StringComparison.OrdinalIgnoreCase)
            ? Platform.TikTok
            : Platform.YouTube;

    private sealed record TranscriptOutcome(
        string? Content = null,
        string? Language = null,
        string? JobId = null,
        VideoTranscriptResult? Result = null);

    private sealed record MetadataResponse(
        string? Platform,
        string? Title,
        string? Description,
        MetadataAuthor? Author);

    private sealed record MetadataAuthor(string? DisplayName);
}
