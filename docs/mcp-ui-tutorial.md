# Building an MCP UI (MCP Apps / SEP-1865) — a .NET + Angular tutorial

This is a conceptual walkthrough of how to add an **interactive UI to an MCP server**,
the way Kotlet does it. The goal is understanding, not a line-by-line copy. It assumes a
stack like Kotlet's: an **ASP.NET Core** MCP server plus an **Angular** web frontend.

## 1. What "MCP UI" actually is

Normally an MCP tool returns text or JSON, and the AI host (Claude, ChatGPT, …) renders
that itself. **MCP Apps** ([SEP-1865](https://modelcontextprotocol.io/seps/1865-mcp-apps-interactive-user-interfaces-for-mcp))
lets a tool return a piece of your own UI instead. The flow:

1. A tool (e.g. `show_recipes`) runs and returns data **plus a pointer to an HTML resource**.
2. The host reads that resource — a self-contained HTML document you serve — and renders it
   in a **sandboxed iframe**.
3. The host hands your data to the iframe, and the iframe can **call other MCP tools back**
   through a `postMessage` bridge (e.g. "View recipe" → calls `get_recipe`).

```
Host (Claude/ChatGPT)
  ├─ calls tool: show_recipes  ──►  your MCP server returns { data, ui://your-app/recipes }
  ├─ reads resource ui://your-app/recipes  ──►  your server returns the HTML document
  └─ renders HTML in iframe, posts data in, relays iframe's tools/call back to server
```

Two building blocks live on your server:

- **A tool** that returns structured data and advertises a UI resource in its metadata.
- **A resource** — one self-contained HTML file (inline CSS + JS, no external assets) — that
  is the UI.

## 2. Why plain HTML, not Blazor / an Angular app

The host renders your resource under a strict Content-Security-Policy — by default
`default-src 'none'`, inline scripts/styles only, no external network. That rules out a
framework that downloads a runtime or bundle at load time (Blazor WASM's multi-MB
`_framework`, an Angular SPA build, external CDN scripts).

**So the UI is a single hand-written HTML document** (~10–15 KB) with inline `<style>` and
`<script>`. It renders instantly and satisfies the CSP with zero configuration. You reuse
your Angular app's *look* by copying its CSS custom properties (color palette) into the file,
not by reusing components. If you later want a build step, the official
`@modelcontextprotocol/ext-apps` SDK can be bundled *inline* — but hand-rolling the bridge is
a few dozen lines (see §6).

## 3. Server side — the tool

The tool returns data twice: once as **structured content** (for the UI) and once as
**plain text** (fallback for hosts without MCP Apps). The magic is the `_meta.ui.resourceUri`
key linking the tool to its UI resource.

```csharp
McpServerTool.Create(ShowRecipes, new McpServerToolCreateOptions
{
    Name = "show_recipes",
    UseStructuredContent = true,
    OutputSchema = /* JSON schema describing the data shape */,
    Meta = new JsonObject
    {
        // SEP-1865: link tool → UI resource
        ["ui"] = new JsonObject { ["resourceUri"] = "ui://kotlet/recipes-v2" },
        // ChatGPT's Apps SDK uses its own metadata key for the same link
        ["openai/outputTemplate"] = "ui://kotlet/recipes-v2",
        ["openai/toolInvocation/invoking"] = "Loading recipes...",
        ["openai/toolInvocation/invoked"] = "Recipes ready"
    }
});
```

The handler returns both representations:

```csharp
return new CallToolResult
{
    Content = [new TextContentBlock { Text = FallbackText(cards) }],   // no-UI hosts
    StructuredContent = JsonSerializer.SerializeToElement(listData)     // the UI
};
```

> **Angular-only stack?** The same idea holds — your API endpoint returns JSON, and the tool
> layer just adds the `_meta.ui` pointer. The UI resource is served as static HTML regardless
> of what framework your main app uses.

## 4. Server side — the UI resource

The resource is the HTML document, plus metadata telling the host how to sandbox it.

```csharp
McpServerResource.Create(() => AppHtml, new McpServerResourceCreateOptions
{
    UriTemplate = "ui://kotlet/recipes-v2",
    MimeType = "text/html;profile=mcp-app",   // the MCP Apps content type
    Meta = new JsonObject
    {
        ["ui"] = new JsonObject
        {
            // CSP the host enforces on the iframe. Only list what the UI truly needs.
            ["csp"] = new JsonObject
            {
                ["connectDomains"]  = new JsonArray(),                 // fetch/XHR/WS targets
                ["resourceDomains"] = new JsonArray(apiOrigin),        // <img>/static assets
                ["frameDomains"]    = new JsonArray()                  // nested iframes
            },
            ["domain"] = apiOrigin,           // host derives a stable sandbox origin from this
            ["prefersBorder"] = true
        },
        // ChatGPT reads the same info from its snake_case namespace — provide both.
        ["openai/widgetCSP"] = new JsonObject {
            ["connect_domains"]  = new JsonArray(),
            ["resource_domains"] = new JsonArray(apiOrigin) },
        ["openai/widgetDomain"] = apiOrigin
    }
});
```

The HTML itself is stored as an **embedded resource** in the assembly (`<EmbeddedResource>` in
the `.csproj`) and read once at startup. In an Angular/Node backend you'd serve the same file
from disk or a string constant.

**Metadata cheat-sheet — what's required vs. nice-to-have:**

| Key | Where | Purpose |
| --- | --- | --- |
| `_meta.ui.resourceUri` | tool | **Required.** Links tool result to the UI resource. |
| MimeType `text/html;profile=mcp-app` | resource | **Required.** Marks the resource as an MCP App. |
| `_meta.ui.csp.{connect,resource,frame}Domains` | resource | Allow-lists for the iframe sandbox. Keep minimal. |
| `_meta.ui.domain` | resource | Stable sandbox origin (required by ChatGPT). |
| `openai/*` twins | tool + resource | Only if you also target ChatGPT's Apps SDK. |
| `UseStructuredContent` / `OutputSchema` | tool | Data the UI consumes; text `Content` is the fallback. |

### Registering when metadata is dynamic

Kotlet normally auto-registers MCP tools by scanning the assembly. But `_meta.ui` here depends
on the runtime API origin (for CSP), which attributes can't express — so these UI primitives
are registered manually as singletons:

```csharp
services.AddSingleton(RecipeUiMcp.CreateShowRecipesTool);
services.AddSingleton<McpServerResource>(_ =>
    RecipeUiMcp.CreateRecipesUiResource(apiOrigin));
```

## 5. The iframe ↔ host bridge

Inside the sandbox there is no direct network to your server. The UI talks to the **host**
via `window.parent.postMessage`, using **JSON-RPC 2.0**. The host relays tool calls to your
server (already authenticated — see §7). A minimal bridge:

```js
const post = (m) => window.parent.postMessage(m, "*");
// request/response with pending-promise map keyed by id …
const callTool = (name, args) => request("tools/call", { name, arguments: args });
```

Lifecycle the UI must handle:

1. On load, send `ui/initialize` (with `protocolVersion`, `appInfo`, `appCapabilities`),
   apply the returned `hostContext` (e.g. light/dark theme), then notify
   `ui/notifications/initialized`.
2. Listen for `ui/notifications/tool-result` — this is the host delivering your tool's
   structured content. Render it.
3. On user action, `callTool("get_recipe", { recipeId })` and render the response — **no new
   chat message needed**.
4. Respond to host-initiated requests (e.g. teardown, theme change) with an empty ack.

```js
bridge.on("ui/notifications/tool-result", (p) => renderList(p.structuredContent));
const detail = await bridge.callTool("get_recipe", { recipeId });   // reuses an existing tool
```

Note the UI **reuses existing MCP tools** (`get_recipe`) rather than inventing a private API.
All data flows through the MCP tool surface; the iframe never hits your REST API directly
(the one exception in Kotlet is loading `<img>` recipe photos, which is why `apiOrigin` is in
`resourceDomains`).

## 6. Security notes for the UI

- The document runs under a tight CSP; keep everything inline.
- You build DOM from tool data, so **HTML-escape all interpolated values** and render any
  Markdown through a small inert renderer. Treat all data as untrusted.
- Only widen CSP domains for things you genuinely load (e.g. an image host). Leave
  `connectDomains`/`frameDomains` empty if the UI only talks through the bridge.

## 7. How auth works

This is the part people underestimate: **the UI never authenticates itself.** Authentication
happens once, at the MCP connection level, and the iframe rides on it.

1. **The MCP endpoint is OAuth-protected.** Kotlet protects `/mcp` with an authorization
   policy requiring an authenticated user and the `mcp` scope:

   ```csharp
   endpoints.MapMcp("/mcp").RequireAuthorization("Mcp");
   ```

2. **The host does the OAuth dance, not you.** An unauthenticated call to `/mcp` returns
   `401` with a `WWW-Authenticate` header pointing at the protected-resource metadata. The
   host then runs **Authorization Code + PKCE** against your OAuth server and stores a
   short-lived token. Standard discovery docs make this automatic:
   - `/.well-known/oauth-protected-resource` (RFC 9728) — points to the auth server.
   - `/.well-known/oauth-authorization-server` / `openid-configuration` (RFC 8414).
   - `/.well-known/mcp.json` — a lightweight client-friendly pointer (endpoint, auth/token
     URLs, client id, scopes).

3. **Dynamic Client Registration (RFC 7591).** Clients like Claude register their own public,
   PKCE-only client at `/connect/register` automatically — no client ID pre-shared by hand.
   Clients that don't (ChatGPT today) use a pre-configured public client ID and a
   pre-registered redirect URI.

4. **Every tool call is already authenticated.** When the iframe does
   `callTool("get_recipe", …)`, the host sends it over the *same authenticated MCP session*.
   Your handler resolves the current user/household from that token — the same
   `ICurrentUser` you'd use in a normal request. The sandbox has **no token and needs none**.

```
User authorizes host  →  host holds OAuth token
      │
      ▼
host ── authenticated tools/call ──► your MCP server (RequireAuthorization + scope check)
      ▲
      │ postMessage (no token)
   iframe UI
```

In Kotlet the OAuth server is OpenIddict, exposed via config (`OAuthOptions`: `Issuer`,
`Resource`, `ClientId`, `RedirectUris`, `RequirePkce`). If you're on Node/Angular, any
OAuth2/OIDC provider that supports Auth-Code + PKCE and (ideally) Dynamic Client Registration
works the same way — the MCP spec is provider-agnostic.

## 8. Putting it together — a checklist

1. Write the MCP tool that returns structured content **and** a text fallback; add
   `_meta.ui.resourceUri`.
2. Write one self-contained HTML file (inline CSS/JS, reuse your app's palette).
3. Register it as an MCP resource with MIME `text/html;profile=mcp-app` and a minimal CSP.
4. Implement the `postMessage`/JSON-RPC bridge: `ui/initialize`, consume
   `ui/notifications/tool-result`, `tools/call` for interactions.
5. Protect `/mcp` with OAuth (Auth-Code + PKCE) and publish the discovery/well-known docs.
6. Bump your server version when the UI/tool surface changes so caching hosts re-fetch.
7. Add integration tests over the protocol surface (tool metadata, resource MIME, structured
   content, fallback text) so the UI can be exercised without a live host.

## Reference files in this repo

| Concern | File |
| --- | --- |
| Tool + resource (dynamic `_meta.ui`) | `src/backend/Kotlet.Api/Recipes/RecipeUiMcp.cs` |
| The UI document + bridge | `src/backend/Kotlet.Api/Recipes/RecipeUiApp.html` |
| Reusable UI for any tool result | `src/backend/Kotlet.Api/Mcp/DataUiMcp.cs` |
| MCP server + auth wiring | `src/backend/Kotlet.Api/Mcp/DiExtension.cs` |
| Discovery `/.well-known/mcp.json` | `src/backend/Kotlet.Api/Mcp/McpEndpoints.cs` |
| Client onboarding & auth flow | `docs/mcp-onboarding.md` |
| Deeper PoC write-up | `docs/mcp-apps-recipe-ui.md` |
</content>
</invoke>
