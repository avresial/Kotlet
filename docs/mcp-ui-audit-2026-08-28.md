# Kotlet MCP UI audit

Audited 2026-08-28 from the `develop` baseline at `b55bde0`.

The audit artifacts are intentionally limited to this report and the screenshots in [`docs/screenshots/mcp-ui-audit`](screenshots/mcp-ui-audit/). No application source was changed.

## Executive summary

- All 57 advertised MCP tools were exercised locally. The run made 60 tool calls, including stateful setup and cleanup calls.
- Every tool returned HTTP 200. One result, `memory_search`, was an MCP error (`isError: true`) rather than a successful tool result.
- Screens were captured at a 390 × 844 mobile viewport in the dark host context.
- Most operation, recipe, meal-plan, pantry, shopping-list, and memory screens are readable. The main UI risk is the generic table renderer on narrow screens.

## Findings

### F-01 — Flat tables collapse into one-character columns on mobile

**Severity:** High · **Area:** `data-v3` responsive rendering

`pantry.resolve_observations` renders seven columns inside a 366 px card. The observed header widths were approximately 40–68 px, so labels and values break one character at a time. This is the same failure mode shown in the supplied mobile screenshots.

Evidence: [`pantry.resolve_observations`](screenshots/mcp-ui-audit/pantry-resolve-observations.jpg)

![F-01 mobile table wrapping](screenshots/mcp-ui-audit/pantry-resolve-observations.jpg)

The current renderer selects a normal table when a collection has seven or fewer flat keys, then applies aggressive wrapping to table cells ([`DataUiApp.html:168-169`](../src/backend/Kotlet.Api/Mcp/DataUiApp.html#L168)). On mobile, this should become a card layout or a genuinely scrollable table with useful minimum column widths. A table that technically fits but is unreadable is not an acceptable responsive fallback.

### F-02 — Recommendation table overflows its card

**Severity:** High · **Area:** `data-v3` responsive rendering

`meal_plan_recommend_replacement` renders a recommendation table whose content width was 498 px while the table viewport was 364 px. The `Resource URI` column alone measured about 402 px because the URI chip is non-wrapping. The right side can therefore be clipped or extend beyond the card on a narrow host.

Evidence: [`meal_plan_recommend_replacement`](screenshots/mcp-ui-audit/meal-plan-recommend-replacement.jpg)

![F-02 recommendation table overflow](screenshots/mcp-ui-audit/meal-plan-recommend-replacement.jpg)

Long resource identifiers need a responsive treatment: cards, a wrapped/ellipsized URI, or a table with intentional horizontal scrolling and a visible affordance. The current generic table path does not provide that ([`DataUiApp.html:169`](../src/backend/Kotlet.Api/Mcp/DataUiApp.html#L169)).

### F-03 — `get_prepared_meals` falls through to the generic result renderer

**Severity:** Medium · **Area:** renderer/data contract

The live `get_prepared_meals` result contains `suggestedAddons`, but the prepared-meal renderer is selected only when the payload contains `addons` ([`DataUiApp.html:188`](../src/backend/Kotlet.Api/Mcp/DataUiApp.html#L188), [`DataUiApp.html:201`](../src/backend/Kotlet.Api/Mcp/DataUiApp.html#L201)). The result therefore displays the generic `Tool result` heading instead of the prepared-meal presentation.

Evidence: [`get_prepared_meals`](screenshots/mcp-ui-audit/get-prepared-meals.jpg)

![F-03 generic prepared-meal result](screenshots/mcp-ui-audit/get-prepared-meals.jpg)

The UI predicate and the MCP output contract should agree. Either the list payload should use the renderer’s expected shape, or the renderer should recognize the list shape actually returned by the tool.

### F-04 — `memory_search` fails against the local SQLite provider

**Severity:** High · **Area:** tool execution, not styling

`memory_search` returned HTTP 200 with `isError: true` and the text `An error occurred invoking 'memory_search'.` The other memory methods exercised in the same run completed successfully, including create, list, get, update, changes-since, export, bootstrap, and delete.

Evidence: [`memory_search`](screenshots/mcp-ui-audit/memory-search.jpg)

![F-04 memory search error](screenshots/mcp-ui-audit/memory-search.jpg)

The likely cause is the SQLite execution path for `EF.Functions.ILike` in [`AgentMemoryRepository.cs:24-29`](../src/backend/Kotlet.Infrastructure/AgentMemory/AgentMemoryRepository.cs#L24). This is an inference from the observed local-provider failure and the query implementation; the exception was intentionally not exposed in the MCP result.

### F-05 — Some valid results have low-information generic chrome

**Severity:** Low · **Area:** discoverability/polish

Several valid payloads, including `get_meal_plan_members` and simple mutation results, use the generic `Tool result` heading. Technical identifiers are intentionally hidden by the renderer, which keeps the screen cleaner but can leave a result with little visible context.

Evidence: [`get_meal_plan_members`](screenshots/mcp-ui-audit/get-meal-plan-members.jpg)

![F-05 generic result chrome](screenshots/mcp-ui-audit/get-meal-plan-members.jpg)

This is not a functional failure. It is a follow-up polish item after the high-severity table and renderer issues are addressed.

## MCP surface coverage

The local inventory and read coverage was:

| Surface | Advertised | Exercised/read |
| --- | ---: | ---: |
| Tools | 57 | 57 unique tools / 60 calls |
| Resources | 11 | 11 list entries and 11 concrete reads |
| Resource templates | 5 | 5 template instances read |
| Prompts | 2 | 2 prompts read |

The four MCP App resources were also loaded by the local UI harness: `ui://kotlet/data-v3`, `ui://kotlet/recipes-v2`, `ui://kotlet/meal-plan-v1`, and `ui://kotlet/meal-plan-preview-v1`.

## Method-by-method evidence

`OK` means the local MCP call returned a normal result. `MCP error` means the transport returned HTTP 200 but marked the tool result as an MCP error. The renderer column identifies the MCP App resource used for the screenshot; `data-v3` includes its specialized branches for ingredients, pantry, meal plans, prepared meals, and operations.

| Method | Result | Renderer | Screenshot |
| --- | --- | --- | --- |
| `add_meal_participants` | OK | data-v3 | [open](screenshots/mcp-ui-audit/add-meal-participants.jpg) |
| `add_meal_to_plan` | OK | data-v3 | [open](screenshots/mcp-ui-audit/add-meal-to-plan.jpg) |
| `add_pantry_item` | OK | data-v3 | [open](screenshots/mcp-ui-audit/add-pantry-item.jpg) |
| `add_prepared_meal` | OK | data-v3 | [open](screenshots/mcp-ui-audit/add-prepared-meal.jpg) |
| `add_recipe` | OK | data-v3 | [open](screenshots/mcp-ui-audit/add-recipe.jpg) |
| `add_shopping_list_item` | OK | data-v3 | [open](screenshots/mcp-ui-audit/add-shopping-list-item.jpg) |
| `add_weekly_meal_plan` | OK | data-v3 | [open](screenshots/mcp-ui-audit/add-weekly-meal-plan.jpg) |
| `check_recipe_exists` | OK | data-v3 | [open](screenshots/mcp-ui-audit/check-recipe-exists.jpg) |
| `clear_purchased_shopping_items` | OK | data-v3 | [open](screenshots/mcp-ui-audit/clear-purchased-shopping-items.jpg) |
| `copy_meal_plan_day` | OK | data-v3 | [open](screenshots/mcp-ui-audit/copy-meal-plan-day.jpg) |
| `copy_meal_plan_week` | OK | data-v3 | [open](screenshots/mcp-ui-audit/copy-meal-plan-week.jpg) |
| `create_ingredient` | OK | data-v3 | [open](screenshots/mcp-ui-audit/create-ingredient.jpg) |
| `get_ingredients` | OK | data-v3 | [open](screenshots/mcp-ui-audit/get-ingredients.jpg) |
| `get_meal_plan` | OK | data-v3 | [open](screenshots/mcp-ui-audit/get-meal-plan.jpg) |
| `get_meal_plan_members` | OK | data-v3 | [open](screenshots/mcp-ui-audit/get-meal-plan-members.jpg) |
| `get_meal_plan_overview` | OK | data-v3 | [open](screenshots/mcp-ui-audit/get-meal-plan-overview.jpg) |
| `get_pantry` | OK | data-v3 | [open](screenshots/mcp-ui-audit/get-pantry.jpg) |
| `get_prepared_meal` | OK | data-v3 | [open](screenshots/mcp-ui-audit/get-prepared-meal.jpg) |
| `get_prepared_meals` | OK | data-v3 | [open](screenshots/mcp-ui-audit/get-prepared-meals.jpg) |
| `get_recipe` | OK | recipes-v2 | [open](screenshots/mcp-ui-audit/get-recipe.jpg) |
| `get_recipes` | OK | recipes-v2 | [open](screenshots/mcp-ui-audit/get-recipes.jpg) |
| `get_shopping_list` | OK | data-v3 | [open](screenshots/mcp-ui-audit/get-shopping-list.jpg) |
| `meal_plan_apply_replacement` | OK | data-v3 | [open](screenshots/mcp-ui-audit/meal-plan-apply-replacement.jpg) |
| `meal_plan_clear_slot` | OK | data-v3 | [open](screenshots/mcp-ui-audit/meal-plan-clear-slot.jpg) |
| `meal_plan_get_range` | OK | data-v3 | [open](screenshots/mcp-ui-audit/meal-plan-get-range.jpg) |
| `meal_plan_move` | OK | data-v3 | [open](screenshots/mcp-ui-audit/meal-plan-move.jpg) |
| `meal_plan_recommend_replacement` | OK | data-v3 | [open](screenshots/mcp-ui-audit/meal-plan-recommend-replacement.jpg) |
| `meal_plan_replace` | OK | data-v3 | [open](screenshots/mcp-ui-audit/meal-plan-replace.jpg) |
| `meal_plan_swap` | OK | data-v3 | [open](screenshots/mcp-ui-audit/meal-plan-swap.jpg) |
| `memory_bootstrap` | OK | data-v3 | [open](screenshots/mcp-ui-audit/memory-bootstrap.jpg) |
| `memory_changes_since` | OK | data-v3 | [open](screenshots/mcp-ui-audit/memory-changes-since.jpg) |
| `memory_create` | OK | data-v3 | [open](screenshots/mcp-ui-audit/memory-create.jpg) |
| `memory_delete` | OK | data-v3 | [open](screenshots/mcp-ui-audit/memory-delete.jpg) |
| `memory_export_markdown` | OK | data-v3 | [open](screenshots/mcp-ui-audit/memory-export-markdown.jpg) |
| `memory_get` | OK | data-v3 | [open](screenshots/mcp-ui-audit/memory-get.jpg) |
| `memory_list` | OK | data-v3 | [open](screenshots/mcp-ui-audit/memory-list.jpg) |
| `memory_search` | MCP error | data-v3 | [open](screenshots/mcp-ui-audit/memory-search.jpg) |
| `memory_update` | OK | data-v3 | [open](screenshots/mcp-ui-audit/memory-update.jpg) |
| `move_meal_in_plan` | OK | data-v3 | [open](screenshots/mcp-ui-audit/move-meal-in-plan.jpg) |
| `pantry.reconcile` | OK | data-v3 | [open](screenshots/mcp-ui-audit/pantry-reconcile.jpg) |
| `pantry.resolve_observations` | OK | data-v3 | [open](screenshots/mcp-ui-audit/pantry-resolve-observations.jpg) |
| `pantry.undo_reconcile` | OK | data-v3 | [open](screenshots/mcp-ui-audit/pantry-undo-reconcile.jpg) |
| `preview_meal_plan` | OK | meal-plan-preview-v1 | [open](screenshots/mcp-ui-audit/preview-meal-plan.jpg) |
| `remove_meal_from_plan` | OK | data-v3 | [open](screenshots/mcp-ui-audit/remove-meal-from-plan.jpg) |
| `remove_pantry_item` | OK | data-v3 | [open](screenshots/mcp-ui-audit/remove-pantry-item.jpg) |
| `remove_prepared_meal` | OK | data-v3 | [open](screenshots/mcp-ui-audit/remove-prepared-meal.jpg) |
| `remove_shopping_list_item` | OK | data-v3 | [open](screenshots/mcp-ui-audit/remove-shopping-list-item.jpg) |
| `set_meal_guests` | OK | data-v3 | [open](screenshots/mcp-ui-audit/set-meal-guests.jpg) |
| `set_meal_participant_portion` | OK | data-v3 | [open](screenshots/mcp-ui-audit/set-meal-participant-portion.jpg) |
| `set_meal_participants` | OK | data-v3 | [open](screenshots/mcp-ui-audit/set-meal-participants.jpg) |
| `set_meal_servings` | OK | data-v3 | [open](screenshots/mcp-ui-audit/set-meal-servings.jpg) |
| `show_meal_plan` | OK | meal-plan-v1 | [open](screenshots/mcp-ui-audit/show-meal-plan.jpg) |
| `show_recipes` | OK | recipes-v2 | [open](screenshots/mcp-ui-audit/show-recipes.jpg) |
| `update_pantry_item` | OK | data-v3 | [open](screenshots/mcp-ui-audit/update-pantry-item.jpg) |
| `update_prepared_meal` | OK | data-v3 | [open](screenshots/mcp-ui-audit/update-prepared-meal.jpg) |
| `update_recipe` | OK | data-v3 | [open](screenshots/mcp-ui-audit/update-recipe.jpg) |
| `update_shopping_list_item` | OK | data-v3 | [open](screenshots/mcp-ui-audit/update-shopping-list-item.jpg) |

## Verification

The following checks were run during the audit:

- `dotnet test tests/Kotlet.Api.IntegrationTests/Kotlet.Api.IntegrationTests.csproj --filter 'FullyQualifiedName~Mcp'` — **38 passed, 0 failed**.
- Comprehensive local MCP capture — **1 passed, 0 failed**, covering the complete tool surface and cleanup.
- `node --test tests/mcp-*.test.mjs` — **21 passed, 0 failed**.
- `git diff --check` — passed.

A direct standalone API/OAuth attempt reached OpenIddict request validation and token persistence but hung before returning the authorization redirect. The complete matrix therefore used Kotlet’s local in-process TestServer with SQLite, while the UI resource and screenshots were served locally. This keeps the method results reproducible without treating the host-level OAuth hang as an MCP method failure.

## Recommended follow-up order

1. Fix the narrow-screen collection strategy for F-01 and F-02.
2. Align the prepared-meal list contract with the `data-v3` renderer for F-03.
3. Capture the SQLite exception or add a provider-compatible search path for F-04, then rerun the full matrix.
4. Improve generic result context for F-05 if the MCP host’s compact presentation needs more discoverability.
