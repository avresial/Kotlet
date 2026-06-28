# Plan: Surface & act on incoming home invitations

## Problem

A user who already belongs to a home has **no way to accept or reject an invitation**
to another home. Today incoming invitations can only be acted on from the
`home-setup-page`, which is shown *only* to users with no home (it redirects to
`/dashboard` when `currentUser().hasHome` is true).

Everything needed already exists except the UI for the "already has a home" case:

- **Backend** — `GET /api/houses/invitations`, `POST .../invitations/{id}/accept`,
  `POST .../invitations/{id}/decline` are all implemented in
  `src/backend/Kotlet.Api/Houses/HouseEndpoints.cs`. Accept/decline both delete the
  invitation, so both users see the state change on next load.
- **Frontend service** — `HomeService.listMyInvitations()`, `.accept()`, `.decline()`
  already exist (`src/frontend/src/app/features/home/home.service.ts`).
- **i18n** — `home.invite.accept/decline/from/acceptError/declineError` already exist.

The account menu already loads `invitationCount` and shows a badge, but the badge
links to `/home` (the manage page), which only lists **outgoing** pending invites.

## Goal

In the user-options popup (account menu in `app-header`):

1. Show a **"New home invite"** entry when the user has one or more incoming invitations.
2. Clicking it opens a larger detail popup (modal) showing, per invitation:
   - the home name,
   - who invited them (`invitedByName`),
   - when (`invitedAtUtc`),
   - (optional, see below) how many members the home has.
3. Two actions per invitation: **Join** and **Reject**. Both remove the invitation
   (server-side delete) so both parties see the updated state.
   - **Join** = accept → user becomes a member; reload current user/homes.
   - **Reject** = decline → invitation removed; list refreshes.

## Scope

Frontend only. No backend change required for the core feature. One small **optional**
backend enhancement (member count in the detail) is called out separately below.

## Implementation steps

### 1. Account-menu entry (`app-header`)

Files: `src/frontend/src/app/layout/app-header/app-header.html`, `.ts`, `.scss`

- The component already has `invitationCount` and loads it in `loadHomes()`. Keep the
  full invitation list instead of just the count:
  - Add `readonly invitations = signal<IncomingInvitation[]>([])`.
  - In `loadHomes()`, set both `invitations` and derive count (or a computed
    `hasInvitations = computed(() => this.invitations().length > 0)`).
- In the popup template, add a menu item shown only when `invitations().length > 0`,
  rendered above/near the existing `/home` link:
  - Label: a new i18n key `home.invite.inboxTitle` = `"New home invite"`
    (plural-friendly; use count badge for multiples).
  - Clicking it sets `isInviteModalOpen.set(true)` (and does **not** close the menu via
    document-click handler — stop propagation).
- Keep the existing badge logic, but point it at this new entry rather than `/home`
  (or leave the `/home` badge and add a separate one — decision below).

### 2. Invitation detail modal

New component (recommended for reuse & clean change detection):
`src/frontend/src/app/features/home/components/invitation-inbox/` with
`invitation-inbox.ts/.html/.scss`, or inline in `app-header` if we want to avoid a new
component. **Recommendation: new standalone component**, inputs:
`invitations` (from parent) + outputs `joined`, `closed`.

Modal contents (reuse `home-setup-page` markup as the template seed):
- Backdrop + centered card (`role="dialog"`, `aria-modal="true"`, Escape to close,
  click-backdrop to close).
- Heading: `home.invite.inboxTitle`.
- For each invitation:
  - `<strong>{{ invitation.houseName }}</strong>`
  - `{{ 'home.invite.from' | t }} {{ invitation.invitedByName }}`
  - formatted `invitedAtUtc` (use existing date formatting/pipe if one exists; else
    `| date`).
  - Actions: **Join** (`home.invite.join`) and **Reject** (`home.invite.reject`),
    disabled while `busyInvitationId() !== null`.
- Error line bound to `home.invite.acceptError` / `home.invite.declineError`.

Behavior:
- **Join**: `homeService.accept(id)`. On success: remove from local list; if list now
  empty, close modal; refresh the home switcher (`loadHomes`) so the new home appears.
  Note `accept()` already calls `applyAndReload` (re-reads user + may activate the new
  home if it was the user's first). Optionally navigate/reload the current route like
  `switchHome` does, so active-home-scoped data refreshes.
- **Reject**: `homeService.decline(id)`. On success: remove from local list; close if
  empty.

### 3. i18n keys

Add to both `src/frontend/public/i18n/en.json` and `pl.json`:

| key | en | pl |
|-----|----|----|
| `home.invite.inboxTitle` | `New home invite` | `Nowe zaproszenie do domu` |
| `home.invite.inboxEmpty` | `No pending invitations.` | `Brak oczekujących zaproszeń.` |
| `home.invite.join` | `Join` | `Dołącz` |
| `home.invite.reject` | `Reject` | `Odrzuć` |
| `home.invite.invitedOn` | `Invited on` | `Zaproszono` |
| `home.invite.close` | `Close` | `Zamknij` |

(Reuse existing `home.invite.from`, `home.invite.acceptError`, `home.invite.declineError`.)

### 4. State-sync detail

- After Join, the active home may or may not change. To guarantee both users "see the
  state changed", the inviter sees the pending invite disappear on their next
  `getHome()` load (already handled server-side). The invitee's switcher refreshes via
  `loadHomes()`.

## Optional backend enhancement (nice-to-have details in modal)

To show "how many members the home has" in the detail popup, extend
`IncomingInvitationResponse` (`src/backend/Kotlet.Api/Houses/HouseContracts.cs`) and the
`ListMyInvitations` projection (`HouseEndpoints.cs`) with `int MemberCount` =
`i.House.Memberships.Count`. Mirror the field in `IncomingInvitation`
(`home.models.ts`). Skip if we want to keep this PR frontend-only.

## Out of scope

- Real-time/push notifications (still poll on menu open).
- Email notifications for invitations.

## Test / verification

- Manual: User A invites User B (who already has a home). User B opens the account menu
  → sees "New home invite" with badge → opens modal → sees A's name + home name → Join →
  new home appears in switcher; A's manage page no longer lists the pending invite.
- Repeat with Reject → invitation gone for both.
- Existing integration tests in `tests/Kotlet.Api.IntegrationTests` cover the endpoints;
  no backend change means they stay green (unless the optional enhancement is taken,
  then update the projection test/assertions).
