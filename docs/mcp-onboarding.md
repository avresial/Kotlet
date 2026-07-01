# Connect an AI client to Kotlet (MCP)

Kotlet hosts a Model Context Protocol (MCP) server so AI clients such as Claude
and ChatGPT can read your recipes and help plan meals. You authorize access with
an OAuth login — there are no API keys to copy or store.

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
  "scopes_supported": ["mcp"]
}
```

This is a lightweight, client-friendly pointer. The standards-based
`/.well-known/openid-configuration` and `/.well-known/oauth-protected-resource`
documents are also served and are what OAuth-aware clients rely on for the full
authorization-server metadata.

## Claude

1. Open Claude and go to **Settings → Connectors**.
2. Add a custom connector and paste the Kotlet MCP server URL (`.../mcp`).
3. Complete the Kotlet login when Claude opens the authorization window.

Claude runs the Authorization Code + PKCE flow against Kotlet's OAuth endpoints
and stores its own short-lived token. You never handle a token yourself.

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

Troubleshooting:

- If the connection fails, make sure you completed the Kotlet login.
- Access tokens are short-lived. If your client reports an expired or
  unauthorized session, reconnect / re-authorize.
- An unauthenticated or invalid-token request to `/mcp` returns `401` with a
  `WWW-Authenticate` header pointing at the resource metadata, which compliant
  clients use to re-run the OAuth flow.
