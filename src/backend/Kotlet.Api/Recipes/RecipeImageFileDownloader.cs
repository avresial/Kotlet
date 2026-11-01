using System.Net;
using System.Net.Sockets;
using System.Text.Json.Serialization;
using Kotlet.Application.Recipes;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using OpenTelemetry;

namespace Kotlet.Api.Recipes;

public sealed class RecipeImageFileOptions
{
    public const string SectionName = "McpRecipeImages";
    public string[] AllowedDownloadHosts { get; set; } = [];
}

public sealed record RecipeImageFile(
    [property: JsonPropertyName("download_url")] string DownloadUrl,
    [property: JsonPropertyName("file_id")] string FileId,
    [property: JsonPropertyName("mime_type")] string? MimeType = null,
    [property: JsonPropertyName("file_name")] string? FileName = null);

public sealed class RecipeImageFileDownloader(HttpClient httpClient, IOptions<RecipeImageFileOptions> options)
{
    internal static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(10);
    private static readonly IPNetwork GlobalIpv6 = IPNetwork.Parse("2000::/3");
    // Conservative exclusions of private, special-use, documentation and transition ranges.
    private static readonly IPNetwork[] NonPublicNetworks =
    [
        IPNetwork.Parse("0.0.0.0/8"), IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"), IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"), IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.0.0.0/24"), IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("192.88.99.0/24"), IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("198.18.0.0/15"), IPNetwork.Parse("198.51.100.0/24"),
        IPNetwork.Parse("203.0.113.0/24"), IPNetwork.Parse("224.0.0.0/3"),
        IPNetwork.Parse("2001::/23"), IPNetwork.Parse("2001:db8::/32"),
        IPNetwork.Parse("2002::/16"), IPNetwork.Parse("3ffe::/16"), IPNetwork.Parse("3fff::/20")
    ];

    public async Task<RecipeImageContent> DownloadAsync(RecipeImageFile file, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(file.FileId))
        {
            throw new McpException("A file_id from the client is required. Pass the generated or selected file, not a sandbox path.");
        }
        if (!Uri.TryCreate(file.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 ||
            uri.HostNameType != UriHostNameType.Dns || uri.Host.EndsWith('.') || file.DownloadUrl.Length > 8192)
        {
            throw new McpException("download_url must be an HTTPS file URL on port 443 without credentials or a fragment. Local paths are not supported.");
        }
        if (!options.Value.AllowedDownloadHosts.Contains(uri.IdnHost, StringComparer.OrdinalIgnoreCase))
        {
            throw new McpException("The file download host is not allowed. The operator must configure McpRecipeImages:AllowedDownloadHosts with verified exact hostnames.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DownloadTimeout);
        try
        {
            using var telemetry = SuppressInstrumentationScope.Begin();
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new McpException("The file could not be downloaded. Redirects are not followed; ask the client for a fresh file reference.");
            }
            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            var extension = contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",
                _ => throw new McpException("The download must have a JPEG, PNG, or WebP Content-Type.")
            };
            if (file.MimeType is not null && !string.Equals(file.MimeType, contentType, StringComparison.OrdinalIgnoreCase))
            {
                throw new McpException("The file mime_type does not match the download Content-Type.");
            }
            if (response.Content.Headers.ContentLength > RecipeImageService.MaxFileSizeBytes)
            {
                throw new McpException("Image file cannot exceed 5 MB.");
            }
            await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            while (true)
            {
                var read = await input.ReadAsync(buffer, timeout.Token);
                if (read == 0)
                {
                    break;
                }
                if (output.Length + read > RecipeImageService.MaxFileSizeBytes)
                {
                    throw new McpException("Image file cannot exceed 5 MB.");
                }
                await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }
            var fileName = string.IsNullOrWhiteSpace(file.FileName) ? $"recipe-image{extension}" : file.FileName;
            return new(fileName, contentType, output.ToArray());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new McpException("The file download timed out. Ask the client for a fresh file reference and try again.");
        }
        catch (HttpRequestException)
        {
            // Transport exceptions can contain signed URLs. Expose only a fixed message.
            throw new McpException("The file download failed or its destination is not public. Ask the client for a fresh file reference.");
        }
        catch (IOException)
        {
            throw new McpException("The file download was interrupted. Ask the client for a fresh file reference.");
        }
    }

    internal static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        Credentials = null,
        ConnectCallback = ConnectAsync,
        ActivityHeadersPropagator = null
    };

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
        {
            throw new HttpRequestException("The file destination must resolve only to public addresses.");
        }
        // Connect to the checked address, not the hostname, so a second DNS resolution cannot rebind it.
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException)
            {
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
        throw new HttpRequestException("Could not connect to the file destination.");
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }
        return (address.AddressFamily == AddressFamily.InterNetwork || GlobalIpv6.Contains(address)) &&
            !NonPublicNetworks.Any(network => network.Contains(address));
    }
}
