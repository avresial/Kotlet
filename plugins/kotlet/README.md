# Kotlet Codex plugin (proof of concept)

Packages the **hosted Kotlet MCP server** plus reusable recipe workflow skills as a
[Codex plugin](https://developers.openai.com/codex/plugins/build). It is configuration and
documentation only — no backend or frontend code is involved, and the MCP server itself is
unchanged.

Scope of this PoC: validate plugin discovery, installation, the OAuth connection to the hosted
MCP endpoint, and skill-driven orchestration. Meal-planning and shopping-list skills, marketplace
submission, and production branding are deliberately out of scope.

## Contents

```text
plugins/kotlet/
├── .codex-plugin/plugin.json   # plugin manifest (metadata + interface)
├── .mcp.json                   # remote Kotlet MCP server, OAuth-protected
├── .app.json                   # app-connector binding — intentionally empty, see below
├── skills/import-recipe/       # recipe-import workflow skill
├── skills/update-recipe/       # safe full-replacement update workflow
└── assets/icon.png             # 256×256 icon, extracted from the app favicon
```

The repo-local marketplace entry that points Codex at this folder lives in
[`.agents/plugins/marketplace.json`](../../.agents/plugins/marketplace.json).

## Install locally

The repo ships a marketplace catalogue named `kotlet`, so Codex can install the plugin straight
from a clone. From the repository root:

```bash
codex plugin marketplace add .          # or: codex plugin marketplace add avresial/Kotlet
codex plugin install kotlet@kotlet      # <plugin>@<marketplace>
```

Inside a Codex session the same steps are `/plugin marketplace add .` and
`/plugin install kotlet@kotlet`. Marketplace commands need Codex CLI v0.121.0 or newer.

Then connect the MCP server — Codex opens a browser for the Kotlet OAuth login:

```bash
codex mcp login kotlet
```

`.mcp.json` carries only the endpoint URL and the OAuth resource indicator; no client id or
secret is stored. Codex registers its own public client through Kotlet's Dynamic Client
Registration endpoint (`/connect/register`, RFC 7591) and runs Authorization Code + PKCE.

If a Codex build does not attempt dynamic registration, fall back to the manual client values
documented in [`docs/chatgpt-mcp-setup.md`](../../docs/chatgpt-mcp-setup.md) (pre-shared client
`kotlet-chatgpt`, no secret, scopes `mcp offline_access`), and register the callback URL Codex
displays in `OAuth:RedirectUris` in `src/backend/Kotlet.Api/appsettings.json`. See
[`docs/mcp-onboarding.md`](../../docs/mcp-onboarding.md) for the full discovery-metadata story.

Verify the connection:

```bash
codex mcp list          # kotlet should be listed and logged in
```

## The `import-recipe` skill

`skills/import-recipe/SKILL.md` restates the workflow the server already publishes through the
`kotlet://recipes/new-recipe-guide` resource and the `create_recipe_flow` prompt: extract →
review with the user → `check_recipe_exists` → one batched `get_ingredients` → confirm before
`create_ingredient` → a single `add_recipe`. It adds no new tools and no new rules.

## The `update-recipe` skill

`skills/update-recipe/SKILL.md` follows the server's
`kotlet://recipes/edit-recipe-guide` resource and `update_recipe_flow` prompt. It requires
`get_recipe` first, applies only the requested change to that complete baseline, and sends one
full replacement through `update_recipe`. Playable films use the recipe's native `videoUrl` and
`videoThumbnailUrl` fields rather than a Markdown link. Recipe images, ownership, and AI-assisted
provenance remain unchanged.

## Manual test

1. Install the plugin and complete `codex mcp login kotlet` as above.
2. Ask Codex: `Import this recipe into Kotlet: <recipe URL>`.
3. Confirm the skill activates and that Codex:
   - shows the extracted recipe before saving,
   - calls `check_recipe_exists` first,
   - resolves ingredients in one `get_ingredients` call,
   - asks before creating any missing ingredient,
   - calls `add_recipe` once.
4. Open the recipe in Kotlet and check the title, servings, ingredients, and source link.
5. Ask for the **same** recipe again. Codex must report the existing recipe and must not create a
   duplicate.
6. Ask Codex to attach a film to the imported recipe. Confirm it reads the recipe first, preserves
   all existing fields and ingredients, sets `videoUrl` (and `videoThumbnailUrl` when available),
   leaves the Markdown description free of the film link, and calls `update_recipe` once.

## Why `.app.json` is empty

A Codex app binding needs a real app/connector id issued for the workspace. Kotlet's existing
ChatGPT registration is not one of those — it is an OAuth client (`kotlet-chatgpt`) with a
ChatGPT-generated callback (`https://chatgpt.com/connector/oauth/zmq7vhFJDnVV`), which is a
different namespace. Rather than ship a made-up id, the file is present with an empty `apps`
object and is not referenced from `plugin.json`. When a real connector id exists, fill it in and
add `"apps": "./.app.json"` to the manifest:

```json
{
  "apps": {
    "kotlet": { "id": "<codex-connector-id>", "category": "Productivity" }
  }
}
```

The plugin is fully functional without it: `.mcp.json` alone provides the authenticated
connection.

## Validating changes

Codex ships a validator for the plugin contract:

```bash
python3 scripts/validate_plugin.py plugins/kotlet   # from the codex plugin-creator skill
```

The package passes it as committed.
