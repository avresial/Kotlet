# Meal planning review

Reviewed locally on 2026-07-16 using the full Aspire application with SQLite and the seeded two-person household.

## Verdict

The feature is broadly usable. Creating a plan, assigning people and guests, calculating servings/cost/calories, moving meals, copying a day, copying a week, and adding ingredients to the shopping list all work through the UI.

At review time it was not fully reliable because **copying a week silently dropped assigned household members**. That defect is now fixed as noted below.

## Implementation status

Implemented after this review: week-copy participant preservation with integration coverage, URL-backed selected dates and refreshed copy targets, honest repeat-shopping labels, chronological overview ordering, and minimal mobile day navigation. Serving overrides/notes and pantry-aware shopping remain deferred pending the product decisions described below.

## What was verified

| Flow | Result | Notes |
| --- | --- | --- |
| Empty planner and weekly overview | Pass | Five meal slots and seven days load correctly. |
| Add a catalogue ingredient | Pass | Search, keyboard selection, overview marker, and receipt update correctly. |
| Add a recipe | Pass | Search and selection work; recipe cost and calories load. |
| Assign one person / whole house | Pass | Servings and per-person calories recalculate correctly. |
| Add and remove guests | Pass | Headcount, cost, and calories scale correctly. |
| Move a meal between slots | Pass | Dragging Apple from Breakfast to Lunch persisted and updated the overview. |
| Add a planned item to shopping | Pass with UX risk | Correct quantity was added and totals matched. See finding 2. |
| Copy a day | Pass | Items, participants, guests, and calculations were preserved. |
| Copy a week | Pass after fix | Items, guests, and household participants are preserved. |
| Desktop layout | Pass | Weekly grid, planner cards, and receipt rail are readable. |
| Mobile layout (390 × 844) | Pass with limitation | Cards are usable, but the weekly overview disappears completely. |
| Browser console | Pass | No warnings or errors during tested flows. |

Temporary recipes, planned meals, and shopping items created for the review were removed afterward.

## Findings and proposals

### 1. P0 — Week copy lost household participants (fixed)

During the initial review, copying Jul 13–19 to Jul 20–26 changed an Apple meal with two household members and one guest into a meal with only the guest; a recipe assigned to one member became unassigned.

Root cause: `MealPlanRepository.GetByDateRangeAsync` does not load `Participants`, while `CopyWeekAsync` copies `original.Participants`. Unit tests use an in-memory fake whose navigation collection is already populated, so they miss the infrastructure-specific failure.

Implemented:

- Added `.Include(item => item.Participants)` to the range query.
- Added an API integration test that assigns a member, copies a week, then asserts copied participant IDs, guests, and effective servings.

### 2. P1 — “Added to shopping list” is not persistent or idempotent

After adding a meal to shopping, the button shows a success check only until the planner is reloaded or revisited. It then returns to “Add to shopping list”; pressing it again increases the existing quantity. The transient success state suggests the operation is idempotent when it is not.

Proposal:

- Short term: label the completed action clearly as “Added — add again” and state the quantity added.
- Long term, only if per-meal tracking is needed: persist meal-plan-item provenance on generated shopping entries and make the action replace/reconcile that contribution.
- Otherwise remove the per-meal action and use the existing date-range generator as the single, predictable workflow.

### 3. P1 — Selected date and copy targets drift apart

The selected date is component-only state. Reloading the planner returned from Jul 17 to today. After copying a day or week, the target inputs retained old values: the day target could point backward and the week target became the currently selected week, leaving the copy button disabled.

Proposal:

- Store the selected date in `?date=YYYY-MM-DD` so refresh, back/forward, and shared links preserve context.
- Whenever selection changes, default day copy to selected date + 1 and week copy to the next Monday.

### 4. P2 — The weekly grid is ordered opposite to the day editor

The weekly grid renders Dinner → Breakfast because it reverses `slots`; the editor and receipt render Breakfast → Dinner. This makes scanning between the overview and detail unnecessarily harder.

Proposal: use the same chronological order everywhere by removing the grid-only `.reverse()`.

### 5. P2 — Mobile loses weekly context

Below 42rem the weekly overview is hidden. The date input still works, but users cannot see which nearby days are planned or jump quickly between them.

Proposal: keep the full grid desktop-only, but add small Previous day / Today / Next day controls and a planned-meal indicator on mobile. A second calendar component is unnecessary.

### 6. P2 — Backend capabilities are unreachable in the UI

The API supports meal notes and explicit serving overrides, and the UI can display notes, but users cannot enter either from the planner. This leaves useful code effectively dead and makes it awkward to plan portions independently of named participants.

Proposal: decide one product rule before adding controls:

- If servings should always equal people + guests, remove the unused override path.
- If batch cooking and leftovers matter, add one compact servings input plus a reset-to-headcount action.
- Add a note field only when a real use case (for example “prep the night before”) is confirmed.

### 7. P3 — Shopping generation ignores pantry stock

The generated list aggregates the full planned quantities. It does not subtract matching pantry quantities, despite pantry data already existing in the product.

Proposal: first show “needed / in pantry / to buy” in the generation preview. Subtract stock only after unit compatibility and rounding rules are explicit; silent subtraction would be worse than the current predictable behavior.

## Recommended order

1. Fix week-copy participant loss and add the integration regression test.
2. Preserve the selected date and refresh copy targets.
3. Make shopping-list additions honest about repeat clicks.
4. Align slot ordering and add minimal mobile day navigation.
5. Decide whether servings overrides, notes, and pantry subtraction belong in the product before expanding the UI.

## Automated verification

- Full backend suite: **385 passed** (268 application, 8 infrastructure, and 109 API integration tests).
- Targeted API meal-planner integration tests: **11 passed**.
- Frontend tests: **93 passed across 24 files**.
- Frontend production build: **passed**.
- Live Aspire/SQLite browser verification: **passed**, including URL history/reload, mobile navigation, repeat shopping feedback, and participant/guest preservation after week copy.
