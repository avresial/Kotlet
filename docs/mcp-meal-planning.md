# Fast conversational meal planning over MCP

The meal-planning tools separate compact agent reasoning from rich user
presentation. This keeps discovery payloads small while still giving the user
a useful visual draft before anything is saved.

## Recommended agent flow

1. Gather the date range, desired slots, candidate ingredients/recipes, and
   constraints such as repeats or ingredient reuse.
2. Resolve every supplied ingredient name in one `get_ingredients` call.
3. Query `get_recipes` with a small `pageSize` (default 10). Use
   `ingredientIds` when the user wants recipes containing specific or shared
   ingredients. If a full-title search returns no result, retry with
   distinctive title terms and ingredient IDs resolved from `get_ingredients`.
   Choose the closest real catalog recipe by its returned ID. Results contain
   only planning fields: ID, title, servings, meal type, ingredient IDs/names,
   and a detail resource URI.
4. Query `get_prepared_meals` only when ready meals are relevant. Its compact
   results include nutrition plus required/default add-ons; instructions and
   package detail remain behind `get_prepared_meal`.
5. Compose one `AddWeeklyMealPlanRequest` (up to 35 meals) and call
   `preview_meal_plan`. Repeating a recipe on multiple days is valid.
6. Let the user review the draft. The UI highlights ingredients reused across
   meals. The preview is read-only.
7. On approval, pass the unchanged request to `add_weekly_meal_plan`. In MCP
   Apps hosts the user can do this directly with **Add to Kotlet**.

## Request shared by preview and save

```json
{
  "request": {
    "weekStart": "2026-08-03",
    "meals": [
      {
        "date": "2026-08-03",
        "slot": "dinner",
        "recipeId": "00000000-0000-0000-0000-000000000000",
        "note": "Cook enough for Wednesday"
      },
      {
        "date": "2026-08-05",
        "slot": "dinner",
        "recipeId": "00000000-0000-0000-0000-000000000000"
      }
    ]
  }
}
```

Each meal sets exactly one of `recipeId`, `ingredientId`, `preparedMealId`, or
`freeText`. Catalog recipes must use `recipeId`; never use `freeText` for a
renamed or translated catalog recipe. A free-text meal is allowed only after
the user explicitly approves an uncatalogued meal and the request sets
`confirmUncatalogued: true`. A prepared meal also carries configured
required/default `addons` returned by `get_prepared_meals`.

## Performance properties

- Ingredient resolution is batched.
- Recipe candidates are projected without descriptions, images, provenance,
  or cooking steps.
- Prepared-meal candidates omit package and preparation detail.
- Weekly preview and save batch-load distinct recipes, ingredients, and
  prepared meals rather than querying once per planned item.
- One preview payload becomes the one save payload, avoiding another agent
  planning pass and preventing draft/save drift.

## UI resource

`preview_meal_plan` advertises
`ui://kotlet/meal-plan-preview-v1` with MIME type
`text/html;profile=mcp-app`. The resource is a dependency-free embedded HTML
document, follows Kotlet's light/dark palette, is responsive, and uses the MCP
Apps bridge for the explicit save action. Hosts without MCP Apps receive a
plain-text weekly preview and can ask for confirmation in chat.
