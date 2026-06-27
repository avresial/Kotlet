# Frontend architecture

Kotlet's Angular application uses feature-first vertical slices. A feature owns its
routes, pages, and feature-specific presentation. Cross-cutting application
infrastructure remains outside feature folders.

```text
src/app/
  core/          # singleton state, HTTP integration, guards, global models
  features/      # vertical business slices such as auth, menus, and recipes
    <feature>/
      pages/      # routed components that coordinate a use case
      components/ # presentation used only by this feature
      <feature>.routes.ts
  layout/        # persistent application shell: header, navigation, footer
  shared/ui/     # reusable, business-agnostic UI primitives
```

## Boundaries

- A **page** is a route destination. It coordinates state and services, reads route
  data, performs navigation, and composes the UI.
- A **component** represents a meaningful part of a page. Prefer inputs and outputs
  over direct access to routing or global state.
- Keep a component beside its page when it is used once. Move it to the feature's
  `components` folder when multiple pages in that feature use it. Move it to
  `shared/ui` only when unrelated features use the same business-agnostic UI.
- Put application-wide infrastructure in `core`, and application-shell components
  in `layout`.
- Do not extract small markup speculatively. Extract repeated UI, non-trivial
  behavior, or a unit with a useful independent test boundary.

## Naming

- Use PascalCase for types: `LoginPage`, `AuthService`, `CurrentUser`.
- Use kebab-case for files and directories: `login-page.ts`, `auth.service.ts`.
- Use the `app-` prefix for component selectors: `app-login-page`.
- Name booleans as questions: `isLoading`, `isAuthenticated`, `hasError`.
- Suffix observables with `$`; use noun phrases for signals.
- Use request/response names for transport models and avoid `I` interface prefixes.
- Keep tests beside their implementation as `*.spec.ts`.

Angular standalone components are the default. Root routes lazy-load feature routes.
Feature state should remain private unless the rest of the application genuinely
needs it.

## Authentication slice

Authentication pages live in `features/auth`. Session state and HTTP integration
live in `core/auth` because routing, navigation, and future protected features all
consume them. Guards make routing decisions only; session and HTTP behavior stays in
`AuthService`.

The current backend uses an HTTP-only secure cookie. The frontend never reads or
stores credentials or tokens and restores the session with `GET /api/auth/me`.
If the backend later adopts short-lived bearer tokens, that transport concern can be
added to the service/interceptor without changing feature pages.
