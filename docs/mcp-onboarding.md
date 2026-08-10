# Connect an AI client to Kotlet (MCP)

Kotlet hosts a Model Context Protocol (MCP) server so AI clients such as Claude
and ChatGPT can read your recipes and help plan meals. You authorize access with
an OAuth login — there are no API keys to copy or store.

## What a connected agent can do

All data is scoped to your active household. Every capability below is exposed
as a plain MCP **tool**, so it works with clients that don't support MCP
resources (resources are additionally available for clients that do).

| Area | Browse | Change |
| --- | --- | --- |
| Recipes | `get_recipes` (compact planning search with ingredients), `get_recipe` (full detail), `check_recipe_exists` | `add_recipe` |
| Ingredients | `get_ingredients` (closest batch search across all languages, with similarity and measurement metadata) | `create_ingredient` |
| Prepared meals | `get_prepared_meals` (compact planning search), `get_prepared_meal` (full detail) | `add_prepared_meal`, `update_prepared_meal`, `remove_prepared_meal` |
| Shopping list | `get_shopping_list` | `add_shopping_list_item`, `update_shopping_list_item`, `remove_shopping_list_item`, `clear_purchased_shopping_items` |
| Pantry | `get_pantry` | `add_pantry_item`, `update_pantry_item`, `remove_pantry_item` |
| Meal planner | `get_meal_plan_overview`, `get_meal_plan`, `preview_meal_plan` (read-only UI draft) | `add_weekly_meal_plan` |

A typical flow: ask your agent to find a recipe on the internet (a website or a
video), review it together, and say "add it to Kotlet". The agent first checks
for duplicates with `check_recipe_exists` (by source URL and/or title), resolves
all ingredients against the shared catalog in a single `get_ingredients` call,
asks before creating genuinely missing ones, and saves the recipe once with `add_recipe`, citing the source URL in the
description. The server publishes
this workflow to agents through its MCP server instructions, the
`kotlet://recipes/new-recipe-guide` resource, and the `create_recipe_flow`
prompt, so well-behaved clients follow it without extra prompting.

For conversational meal planning, the agent resolves ingredient names once,
searches compact recipe/prepared-meal candidates, and calls
`preview_meal_plan` with the complete week. The preview does not write data and
uses the exact same payload as `add_weekly_meal_plan`; after approval, no
reconstruction is needed. MCP Apps hosts show a Kotlet-style weekly draft with
an explicit **Add to Kotlet** button. See
[Fast MCP meal planning](./mcp-meal-planning.md).

## In-app onboarding page

Signed-in users can open **Settings → AI client access (MCP)**, or navigate
directly to `/connect/mcp`. The page provides:

- the hosted MCP server URL (copyable),
- guided connect steps for Claude and ChatGPT,
- a downloadable/copyable MCP manifest,
- a manual-configuration fallback with troubleshooting notes.

## Discovery metadata

Clients that support automatic discovery can read the well-known document:

```text
https://<kotlet-host>/.well-known/mcp.json
```

Example response:

```json
{
  "name": "Kotlet",
  "version": "1.0.0",
  "description": "Kotlet recipe MCP server",
  "mcp_endpoint": "https://<kotlet-host>/mcp",
  "authorization_endpoint": "https://<kotlet-host>/connect/authorize",
  "token_endpoint": "https://<kotlet-host>/connect/token",
  "client_id": "kotlet-chatgpt",
  "scopes_supported": ["mcp"]
}
```

This is a lightweight, client-friendly pointer. The standards-based
authorization-server metadata is also served, on both
`/.well-known/openid-configuration` and `/.well-known/oauth-authorization-server`
(RFC 8414), alongside `/.well-known/oauth-protected-resource` (RFC 9728). These
are what OAuth-aware clients rely on for the full authorization-server metadata.

### Dynamic Client Registration

The authorization-server metadata advertises a `registration_endpoint`
(`/connect/register`) implementing OAuth 2.0 Dynamic Client Registration
(RFC 7591). Clients that follow the MCP authorization spec — Claude Code, Claude
Desktop, and the claude.ai web connector — register their own public client
(carrying their own redirect/callback URI) automatically before running the
Authorization Code + PKCE flow, so **no client ID or redirect URI has to be
pre-registered by hand** for them. Registration is open (no initial access
token); every registered client is public, PKCE-only, and limited to the `mcp`
scope and resource. ChatGPT does not perform dynamic registration and continues
to use the pre-shared `OAuth:ClientId` described below.

## Claude

1. Open Claude and go to **Settings → Connectors**.
2. Add a custom connector and paste the Kotlet MCP server URL (`.../mcp`).
3. Complete the Kotlet login when Claude opens the authorization window.

Claude discovers the metadata, dynamically registers its own OAuth client
(RFC 7591) with the redirect URI it wants to use, then runs the Authorization
Code + PKCE flow against Kotlet's OAuth endpoints and stores its own short-lived
token. You never handle a token or a client ID yourself. The same automatic flow
works from Claude Code (`claude mcp add`) and Claude Desktop.

## Codex

The repository ships a Codex plugin that bundles this MCP connection with an
`import-recipe` workflow skill, so Codex users do not enter any connection
details by hand:

```bash
codex plugin marketplace add .      # from a clone of this repository
codex plugin install kotlet@kotlet
codex mcp login kotlet
```

The plugin package and its manual test are documented in
[`plugins/kotlet/README.md`](../plugins/kotlet/README.md). It is a proof of
concept: it reuses the hosted server and adds no server-side capability.

## ChatGPT — current limitation

ChatGPT does **not** currently offer a public one-click MCP install. You may need
to enable developer mode / custom connector setup and enter the connection
details by hand. See [ChatGPT setup](./chatgpt-mcp-setup.md) for the exact
values, including the ChatGPT-specific OAuth client and callback URL.

The discovery endpoint and onboarding page make Kotlet ready for a future
one-click flow, but until ChatGPT exposes one, the manual connector setup above
is the supported path.

## Manual configuration (fallback)

For any client that needs values entered by hand:

| Setting | Value |
| --- | --- |
| Server URL | `https://<kotlet-host>/mcp` |
| Transport | HTTP (streamable) |
| Authentication | OAuth 2.0 (Authorization Code + PKCE) |
| Scope | `mcp` |
| OAuth client ID | `kotlet-chatgpt` (the value of `OAuth:ClientId`; served as `client_id` in `/.well-known/mcp.json`) |
| OAuth client secret | none — public client, token endpoint auth method `none` |

Only clients that ask for a **user-defined OAuth client** (such as ChatGPT) need
the client ID entered by hand. Clients that discover it automatically (such as
Claude) only need the server URL. The client ID is a public identifier, not a
secret; the server accepts exactly the one client ID configured in
`OAuth:ClientId`, and each client's redirect/callback URL must be pre-registered
in `OAuth:RedirectUris`.

## Protocol Negotiation and Stateless Transport

- **Stateless HTTP Transport**: Kotlet processes MCP requests statelessly over HTTP POST at `/mcp`. Each request carries an OAuth `Bearer` token in the `Authorization` header and receives an HTTP 200 response with JSON-RPC SSE framing (`data: ...`).
- **Protocol Versioning**: Requests specify the target protocol version via the `MCP-Protocol-Version` header. The server standardizes on MCP protocol version `2026-07-28`. That version also requires the matching `Mcp-Method` header and per-request `_meta` values for protocol version, client capabilities, and client identity; named tool, prompt, and resource requests send the matching `Mcp-Name` header.
- **Backwards Compatibility**: Legacy clients sending protocol version `2025-11-25` remain fully supported according to SDK behavior.
- **MCP Apps Handshake (SEP-1865)**: Interactive UI HTML documents served from `ui://kotlet/...` resources perform an in-iframe `ui/initialize` handshake supplying `appInfo` and the negotiated `protocolVersion`.

Troubleshooting:

- If the connection fails, make sure you completed the Kotlet login.
- Access tokens are short-lived. If your client reports an expired or
  unauthorized session, reconnect / re-authorize.
- An unauthenticated or invalid-token request to `/mcp` returns `401` with a
  `WWW-Authenticate` header pointing at the resource metadata, which compliant
  clients use to re-run the OAuth flow.
