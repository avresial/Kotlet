---
name: import-recipe
description: Import a recipe into Kotlet from a web page, video, blog post, screenshot, or pasted text. Use when asked to add, save, import, or copy a recipe into Kotlet, to extract a recipe from a URL and store it, or to check whether a recipe is already in the Kotlet kitchen before saving it.
---

# Import a recipe into Kotlet

Saves one recipe into the user's Kotlet household through the `kotlet` MCP server.

Recipe creation is **add-only and one-shot**: the server exposes no edit tool, so everything
must be correct before the single `add_recipe` call. The authoritative version of this flow is
published by the server itself — the `kotlet://recipes/new-recipe-guide` resource and the
`create_recipe_flow` prompt. Read the resource if anything below is ambiguous.

## Flow

1. **Extract the recipe** from the supplied source (URL, video, or pasted text). Pull out the
   title, servings, every ingredient with its quantity and unit, and the preparation steps.
   Do not guess quantities that the source does not state.
2. **Show the extracted recipe to the user** and let them correct it before anything is saved.
3. **Check for duplicates** with `check_recipe_exists`, passing the `sourceUrl` (when importing
   from the internet) and the `title`. If it reports a match, stop and tell the user which
   recipe already exists instead of adding it again.
4. **Resolve every ingredient in one batch** with a single `get_ingredients` call containing all
   the names. It returns the closest catalogue match per input across all languages, with its
   measurement unit, exact-match status, edit distance, and similarity. Accept a match only when
   it is genuinely the same ingredient. Prefer generic names ("Soy sauce", not a brand) — the
   catalogue is shared by every household.
5. **Ask before creating anything missing.** List the inputs with no equivalent match and ask
   the user whether to add them. Only after explicit approval, create each one with
   `create_ingredient`. Never invent ingredients the user has not approved.
6. **Call `add_recipe` exactly once**, with:
   - `title` — the confirmed title,
   - `servings` — a positive serving count,
   - `descriptionMarkdown` — a short overview followed by numbered preparation steps, citing the
     source URL,
   - `ingredients` — each with the resolved `ingredientId`, a positive `quantity`, the `unit`
     (use the resolved `measurementUnit`), and an optional `note`,
   - `sourceUrl` — the page or video the recipe came from, for imports,
   - `isAiAssisted` — `true` for imports, so the app marks the recipe accordingly.

Then report back with the created recipe's title and id.

## Rules

- Never create an ingredient without the user's explicit confirmation.
- Never call `add_recipe` more than once for the same recipe. A duplicate call creates a
  duplicate recipe that cannot be edited away through MCP.
- Recipes cannot be edited through MCP. If `add_recipe` returns validation errors, report those
  errors to the user rather than retrying with guessed values, unless the user asks you to retry.
- Everything is scoped to the user's active household; there is no cross-household access.
