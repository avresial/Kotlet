using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Kotlet.Api.Mcp;

/// <summary>Shared, schema-agnostic MCP App used to present every regular tool result.</summary>
public static class DataUiMcp
{
    public const string ResourceUri = "ui://kotlet/data-v1";
    public const string ResourceMimeType = "text/html;profile=mcp-app";

    private static readonly Lazy<string> AppHtml = new(() =>
    {
        var assembly = typeof(DataUiMcp).Assembly;
        const string name = "Kotlet.Api.Mcp.DataUiApp.html";
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static McpServerResource CreateResource(string apiOrigin) =>
        McpServerResource.Create(() => AppHtml.Value, new McpServerResourceCreateOptions
        {
            UriTemplate = ResourceUri,
            Name = "kotlet-data-ui",
            Title = "Kotlet data",
            Description = "Reusable cards and tables for Kotlet MCP tool results.",
            MimeType = ResourceMimeType,
            Meta = new JsonObject
            {
                ["ui"] = new JsonObject
                {
                    ["domain"] = apiOrigin,
                    ["prefersBorder"] = true,
                    ["csp"] = new JsonObject
                    {
                        ["connectDomains"] = new JsonArray(),
                        ["resourceDomains"] = new JsonArray(),
                        ["frameDomains"] = new JsonArray()
                    }
                },
                ["openai/widgetDescription"] = "Displays Kotlet results as compact cards and tables.",
                ["openai/widgetPrefersBorder"] = true,
                ["openai/widgetCSP"] = new JsonObject
                {
                    ["connect_domains"] = new JsonArray(),
                    ["resource_domains"] = new JsonArray()
                },
                ["openai/widgetDomain"] = apiOrigin
            }
        });

    /// <summary>Adds the shared UI template to tools that do not already own a specialized UI.</summary>
    public static void AttachTo(IList<Tool> tools)
    {
        foreach (var tool in tools)
        {
            tool.Meta ??= new JsonObject();
            if (tool.Meta.ContainsKey("openai/outputTemplate") ||
                tool.Meta["ui"] is JsonObject ui && ui.ContainsKey("resourceUri"))
                continue;

            tool.Meta["ui"] = new JsonObject { ["resourceUri"] = ResourceUri };
            tool.Meta["openai/outputTemplate"] = ResourceUri;
            tool.Meta["openai/toolInvocation/invoking"] = "Loading Kotlet data...";
            tool.Meta["openai/toolInvocation/invoked"] = "Kotlet data ready";
        }
    }
}
