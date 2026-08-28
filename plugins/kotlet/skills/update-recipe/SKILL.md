---
name: update-recipe
description: Update or correct an existing Kotlet recipe, including attaching a natively playable recipe video, changing its source, instructions, servings, meal type, or ingredients. Use when asked to edit, update, correct, append to, or change a recipe already stored in Kotlet.
---

# Update a recipe in Kotlet

Safely replaces one existing household recipe through the `kotlet` MCP server. The authoritative
workflow is published as `kotlet://recipes/edit-recipe-guide` and the `update_recipe_flow` prompt.

## Flow

1. **Identify the recipe.** Use `get_recipes` to find its id when the user did not provide one.
   Ask when multiple candidates remain plausible.
2. **Read the complete recipe** with `get_recipe`. Treat this response as the replacement baseline.
3. **Apply only the requested change.** Preserve the title, description, servings, meal type,
   source URL, video URL, video thumbnail URL, and every ingredient that should remain unchanged.
4. **Resolve newly added ingredients** in one `get_ingredients` call. Accept only genuinely
   equivalent matches and ask before using `create_ingredient` for anything missing.
5. **Confirm ambiguous or subtractive changes.** Show the exact replacement when the request could
   remove or replace recipe data. A clear request to attach a supplied playable video does not need
   another confirmation.
6. **Call `update_recipe` exactly once** with the recipe id and complete replacement details.

## Rules

- Never construct an update from a compact `get_recipes` result; always call `get_recipe` first.
- Never omit an existing ingredient unless the user asked to remove it.
- To attach a film, set `videoUrl` to the direct browser-playable media URL and set
  `videoThumbnailUrl` to its poster image when one is available.
- Keep the source page or social post in `sourceUrl`. Never put that page URL in `videoUrl`, and
  never append the film link to `descriptionMarkdown`.
- Recipe images, ownership, and AI-assisted provenance are preserved by the server and must not be
  recreated.
- If validation fails, report the error. Do not retry with guessed values unless the user asks.
- Everything is scoped to the user's active household.
