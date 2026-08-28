# Kotlet MCP UI audit

Audited 2026-08-28 from the `develop` baseline at `b55bde0`.

The initial audit artifacts and the remediation evidence live in this report and the screenshots in [`docs/screenshots/mcp-ui-audit`](screenshots/mcp-ui-audit/).

## Executive summary

- All 57 advertised MCP tools were exercised locally. The run made 60 tool calls, including stateful setup and cleanup calls.
- The initial sweep returned HTTP 200 for every tool, but `memory_search` was an MCP error (`isError: true`) rather than a successful tool result.
- Screens were captured at a 390 × 844 mobile viewport in the dark host context.
- The five findings below were fixed in this remediation revision and have paired Before/After screenshots.

## Findings

### F-01 — Flat tables collapse into one-character columns on mobile — fixed

**Severity:** High · **Area:** `data-v3` responsive rendering

**Status:** Fixed by switching flat tables to labeled cards below the mobile breakpoint.

Before: [`pantry.resolve_observations`](screenshots/mcp-ui-audit/pantry-resolve-observations.jpg)

![F-01 before: mobile table wrapping](screenshots/mcp-ui-audit/pantry-resolve-observations.jpg)

The original seven-column collection collapsed into one-character labels and values inside a 366 px card.

After: [`pantry.resolve_observations`](screenshots/mcp-ui-audit/after-pantry-resolve-observations.jpg)

![F-01 after: labeled mobile card](screenshots/mcp-ui-audit/after-pantry-resolve-observations.jpg)

The renderer still uses a table on wider screens, but the mobile CSS exposes each cell label beside a readable value. The long reason and identifiers no longer force the other fields into narrow columns.

### F-02 — Recommendation table overflows its card — fixed

**Severity:** High · **Area:** `data-v3` responsive rendering

**Status:** Fixed by using the same mobile card treatment and constraining URI chips to the value column.

Before: [`meal_plan_recommend_replacement`](screenshots/mcp-ui-audit/meal-plan-recommend-replacement.jpg)

![F-02 before: recommendation table overflow](screenshots/mcp-ui-audit/meal-plan-recommend-replacement.jpg)

The original table’s content width was 498 px inside a 364 px viewport; the URI column alone was about 402 px.

After: [`meal_plan_recommend_replacement`](screenshots/mcp-ui-audit/after-meal-plan-recommend-replacement.jpg)

![F-02 after: contained recommendation cards](screenshots/mcp-ui-audit/after-meal-plan-recommend-replacement.jpg)

Recommendations are now separate mobile cards. The URI remains available through its link/title while staying inside the card.

### F-03 — `get_prepared_meals` falls through to the generic result renderer — fixed

**Severity:** Medium · **Area:** renderer/data contract

**Status:** Fixed by recognizing `suggestedAddons` and rendering the list with the prepared-meal presentation.

Before: [`get_prepared_meals`](screenshots/mcp-ui-audit/get-prepared-meals.jpg)

![F-03 before: generic prepared-meal result](screenshots/mcp-ui-audit/get-prepared-meals.jpg)

The live list used `suggestedAddons`, so it previously displayed as generic data.

After: [`get_prepared_meals`](screenshots/mcp-ui-audit/after-get-prepared-meals.jpg)

![F-03 after: prepared-meal presentation](screenshots/mcp-ui-audit/after-get-prepared-meals.jpg)

The page now has the `Prepared meals` heading, meal card, nutrition stats, and the suggested add-on chip.

### F-04 — `memory_search` fails against the local SQLite provider — fixed

**Severity:** High · **Area:** tool execution, not styling

**Status:** Fixed by using SQLite-compatible `LIKE` matching while retaining PostgreSQL `ILIKE` matching.

Before: [`memory_search`](screenshots/mcp-ui-audit/memory-search.jpg)

![F-04 before: memory search error](screenshots/mcp-ui-audit/memory-search.jpg)

The initial local run returned HTTP 200 with `isError: true` and the text `An error occurred invoking 'memory_search'.`

After: [`memory_search`](screenshots/mcp-ui-audit/after-memory-search.jpg)

![F-04 after: successful memory search](screenshots/mcp-ui-audit/after-memory-search.jpg)

The same local flow now returns one matching memory. The provider-specific query is implemented in [`AgentMemoryRepository.cs:24-32`](../src/backend/Kotlet.Infrastructure/AgentMemory/AgentMemoryRepository.cs#L24).

### F-05 — Some valid results have low-information generic chrome — fixed

**Severity:** Low · **Area:** discoverability/polish

**Status:** Fixed for household-member results with contextual heading, count, cards, and expandable identifiers.

Before: [`get_meal_plan_members`](screenshots/mcp-ui-audit/get-meal-plan-members.jpg)

![F-05 before: generic result chrome](screenshots/mcp-ui-audit/get-meal-plan-members.jpg)

The original result used the generic `Tool result` heading and showed only the display name.

After: [`get_meal_plan_members`](screenshots/mcp-ui-audit/after-get-meal-plan-members.jpg)

![F-05 after: contextual member cards](screenshots/mcp-ui-audit/after-get-meal-plan-members.jpg)

The result now identifies itself as `Household members`, shows the count, and keeps the user id available under expandable technical details.

## Post-fix verification

- The five after screenshots use the same captured payloads and dark 390 × 844 viewport as the baseline; `memory_search` uses a fresh local call after the SQLite fix.
- The UI test suite covers mobile table labels, suggested add-ons, and contextual member cards.
- The MCP integration suite covers successful text search through the actual MCP tool path.

## MCP surface coverage

The local inventory and read coverage was:

| Surface | Advertised | Exercised/read |
| --- | ---: | ---: |
| Tools | 57 | 57 unique tools / 60 calls |
| Resources | 11 | 11 list entries and 11 concrete reads |
| Resource templates | 5 | 5 template instances read |
| Prompts | 2 | 2 prompts read |

The four MCP App resources were also loaded by the local UI harness: `ui://kotlet/data-v3`, `ui://kotlet/recipes-v2`, `ui://kotlet/meal-plan-v1`, and `ui://kotlet/meal-plan-preview-v1`.

## Initial method-by-method evidence (before fixes)

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
