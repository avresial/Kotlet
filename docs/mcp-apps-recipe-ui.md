# MCP Apps recipe UI (PoC)

Proof of concept for [issue #250](https://github.com/avresial/Kotlet/issues/250): an interactive
[MCP Apps (SEP-1865)](https://modelcontextprotocol.io/seps/1865-mcp-apps-interactive-user-interfaces-for-mcp)
UI for recipes, served by the existing ASP.NET Core MCP server.

## What it does

1. The user asks an MCP-Apps-capable host (e.g. a compatible MCP client UI) to show recipes.
2. The `show_recipes` tool returns the recipe list twice: as structured content for the UI and as a
   plain text list for hosts without MCP Apps support. Its tool definition carries
   `_meta.ui.resourceUri = "ui://kotlet/recipes-v2"`.
3. The host reads the `ui://kotlet/recipes-v2` resource (`text/html;profile=mcp-app`) and renders it in
   a sandboxed iframe, delivering the tool result to it via the MCP Apps postMessage bridge.
4. The UI renders recipe cards (name, image when available, meal type, servings, ingredient count).
5. "View recipe" calls the existing `get_recipe` MCP tool through the bridge (`tools/call`) and
   renders the detail view (image, ingredients with quantities, Markdown method, servings) without
   another chat message.

All recipe data flows through the existing `RecipeService` application layer and the MCP tool
surface — the embedded UI never talks to the REST API or the database. The iframe loads its shared
recipe-card asset from the Angular frontend and recipe images from the API's anonymous image-content
endpoint; both origins are declared via `_meta.ui.csp.resourceDomains` so the host's CSP allows them.

## Code layout

| File | Purpose |
| --- | --- |
| `src/backend/Kotlet.Api/Recipes/RecipeUiMcp.cs` | `show_recipes` tool + `ui://kotlet/recipes-v2` resource, registered manually because both carry `_meta.ui` metadata that attribute scanning cannot express. |
| `src/backend/Kotlet.Api/Recipes/RecipeUiApp.html` | The embedded recipe UI shell with inline CSS/JS and a hand-rolled MCP Apps bridge; cards come from the shared frontend custom element. |
| `src/frontend/src/app/shared/ui/recipe-card/recipe-card.js` | Framework-agnostic recipe-card custom element used by the MCP shell and Angular Agent page. |
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
are copied from `src/frontend/src/styles.scss` (light and dark palettes), and the detail markup
mirrors `recipe-detail-page`, while the shared card custom element owns card markup and styling. The host's reported theme
(`hostContext.theme`) switches the palette, falling back to `prefers-color-scheme`.

Every embedded MCP UI header uses a transparent wrapper, a title color bound to the document's theme
foreground token, and a theme-aware foreground color for its eyebrow label. Keep this convention in
`DataUiApp.html`, `MealPlanUiApp.html`, `MealPlannerUiApp.html`, and `RecipeUiApp.html`; the strict
`default-src 'none'` CSP and embedded-resource model do not provide a shared stylesheet or header
partial.

Language works the same way: English and Polish both ship inside the document, and the app picks
one from the tool result's `_meta["kotlet/locale"]` (the language the server negotiated from
`Accept-Language`), then `hostContext.locale`, then the browser. Each candidate is normalized to
its language subtag, so `pl-PL` selects `pl`; a tag with no dictionary (`fr-FR`) is skipped in
favour of the next signal, and English is the final fallback. See
[§8 of the MCP UI tutorial](./mcp-ui-tutorial.md#8-localizing-the-ui).

## PoC decision point: Blazor WASM vs. lightweight HTML/JS

The issue proposed a Blazor WebAssembly PoC. The recipe UI remains a lightweight HTML shell, while
the card itself is a framework-agnostic custom element shared with the built-in Agent. This keeps
the two recipe-result surfaces consistent without coupling the MCP resource to Angular's runtime:

- **Bundle size and startup**: a minimal Blazor WASM app ships a multi-megabyte `_framework` payload
  (dotnet runtime + assemblies) and needs a visible startup delay inside every conversation turn
  that renders the UI. The HTML document here is ~13 KB and renders immediately.
- **CSP and asset hosting**: the default MCP Apps CSP is `default-src 'none'`; the resource
  explicitly allow-lists the frontend origin for one small shared card asset. Blazor's `_framework`
  assets would still require a much larger runtime and asset pipeline.
- **JS interop**: Blazor would still need the same postMessage JSON-RPC bridge, written in
  JavaScript and called through `IJSRuntime` interop — the interop layer is the bridge, so Blazor
  adds a hop without removing any JavaScript.
- **Styling reuse**: the custom element is plain HTML/CSS/JS, so Angular hosts it through the browser
  custom-element contract and the MCP shell loads the same file without an Angular runtime.

**Recommendation**: keep MCP shells small and framework-independent, and extract UI that must look
the same in the Angular app and an MCP iframe into browser-native shared assets. Blazor WASM is not
a good fit for this embedded surface at the current scope.

## Out of scope (per the issue)

Creating/editing/deleting recipes, shopping-list integration, search/pagination UI beyond the first
page, and production-ready accessibility/styling. Meal-plan drafting now has its own MCP App; see
[Fast MCP meal planning](./mcp-meal-planning.md).
