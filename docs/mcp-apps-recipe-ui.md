# MCP Apps recipe UI (PoC)

Proof of concept for [issue #250](https://github.com/avresial/Kotlet/issues/250): an interactive
[MCP Apps (SEP-1865)](https://modelcontextprotocol.io/seps/1865-mcp-apps-interactive-user-interfaces-for-mcp)
UI for recipes, served by the existing ASP.NET Core MCP server.

## What it does

1. The user asks an MCP-Apps-capable host (e.g. a compatible MCP client UI) to show recipes.
2. The `show_recipes` tool returns the recipe list twice: as structured content for the UI and as a
   plain text list for hosts without MCP Apps support. Its tool definition carries
   `_meta.ui.resourceUri = "ui://kotlet/recipes"`.
3. The host reads the `ui://kotlet/recipes` resource (`text/html;profile=mcp-app`) and renders it in
   a sandboxed iframe, delivering the tool result to it via the MCP Apps postMessage bridge.
4. The UI renders recipe cards (name, image when available, meal type, servings, ingredient count).
5. "View recipe" calls the existing `get_recipe` MCP tool through the bridge (`tools/call`) and
   renders the detail view (image, ingredients with quantities, Markdown method, servings) without
   another chat message.

All recipe data flows through the existing `RecipeService` application layer and the MCP tool
surface — the embedded UI never talks to the REST API or the database. The only direct HTTP the
iframe performs is loading recipe images from the API's anonymous image-content endpoint, which the
resource declares via `_meta.ui.csp.resourceDomains` so the host's CSP allows it.

## Code layout

| File | Purpose |
| --- | --- |
| `src/backend/Kotlet.Api/Recipes/RecipeUiMcp.cs` | `show_recipes` tool + `ui://kotlet/recipes` resource, registered manually because both carry `_meta.ui` metadata that attribute scanning cannot express. |
| `src/backend/Kotlet.Api/Recipes/RecipeUiApp.html` | The entire UI: one self-contained HTML document (embedded resource) with inline CSS/JS and a hand-rolled MCP Apps bridge. |
| `tests/Kotlet.Api.IntegrationTests/Mcp/McpRecipeUiTests.cs` | Verifies the tool metadata, resource MIME type, structured content, and text fallback. |

## Trying it out

Build and run the API as usual (see the repository README); the MCP server needs no extra steps —
the UI ships inside the API assembly.

- **In a compatible MCP host**: connect the host to the Kotlet MCP endpoint (see
  `docs/mcp-onboarding.md` / `docs/chatgpt-mcp-setup.md`), then ask it to "show my recipes". Hosts
  that negotiate the `io.modelcontextprotocol/ui` extension render the cards; others print the text
  list.
- **Without a host**: `tests/Kotlet.Api.IntegrationTests/Mcp/McpRecipeUiTests.cs` covers the
  protocol surface, and the UI can be exercised standalone by iframing `RecipeUiApp.html` from a
  small harness page that answers `ui/initialize`, pushes `ui/notifications/tool-result`, and
  answers `tools/call` with canned data.

## Styling

The UI intentionally reuses the main Angular frontend's design language: the CSS custom properties
are copied from `src/frontend/src/styles.scss` (light and dark palettes), and the card/detail markup
mirrors `recipe-list-page` and `recipe-detail-page` class-for-class. The host's reported theme
(`hostContext.theme`) switches the palette, falling back to `prefers-color-scheme`.

## PoC decision point: Blazor WASM vs. lightweight HTML/JS

The issue proposed a Blazor WebAssembly PoC. While evaluating the hosting constraints, the PoC was
implemented as a single self-contained HTML document instead, for these reasons:

- **Bundle size and startup**: a minimal Blazor WASM app ships a multi-megabyte `_framework` payload
  (dotnet runtime + assemblies) and needs a visible startup delay inside every conversation turn
  that renders the UI. The HTML document here is ~13 KB and renders immediately.
- **CSP and asset hosting**: the default MCP Apps CSP is `default-src 'none'` with only inline
  scripts/styles allowed. Blazor's `_framework` assets would have to be hosted on a public endpoint
  and allow-listed via `_meta.ui.csp`, adding an asset pipeline and cache-busting concerns to the
  API for no functional gain at this scope.
- **JS interop**: Blazor would still need the same postMessage JSON-RPC bridge, written in
  JavaScript and called through `IJSRuntime` interop — the interop layer is the bridge, so Blazor
  adds a hop without removing any JavaScript.
- **Styling reuse**: the main frontend is Angular, so Blazor components could not be reused either
  way. Plain HTML/CSS reuses the frontend's exact palette and markup structure directly.

**Recommendation**: for future Kotlet MCP interfaces, keep this pattern — small self-contained
HTML/TypeScript documents per feature (built with the official `@modelcontextprotocol/ext-apps` SDK
bundled inline once a build step is worth it), sharing the frontend's CSS variables. Blazor WASM is
not a good fit for embedded MCP Apps under current host CSP rules.

## Out of scope (per the issue)

Creating/editing/deleting recipes, meal-planner and shopping-list integration, search/pagination UI
beyond the first page, and production-ready accessibility/styling.
