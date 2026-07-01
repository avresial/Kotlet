# Backend cleanup

Baseline: `dotnet test Kotlet.slnx --no-restore` passes 213 tests (143 unit, 70 integration).

## Audit

### Architecture

- [x] Move house workflows and EF Core access out of `Kotlet.Api/Houses/HouseEndpoints.cs`. HTTP/token-cookie handling remains in a small API adapter; business rules are in Application and persistence is in Infrastructure.
- [x] Move registration, login, profile, password, and current-account workflows out of `Kotlet.Api/Auth/AuthEndpoints.cs` into Application, with persistence and password hashing in Infrastructure.
- [x] Move refresh-token rotation/revocation persistence behind an Application session port. Bearer/cookie serialization remains in API.
- [x] Move admin user workflows and EF Core access out of `Kotlet.Api/Admin/AdminEndpoints.cs`.
- [x] Move OAuth account persistence out of `Kotlet.Api/Auth/OAuthEndpoints.cs`; OpenIddict host configuration and callback transport remain in API.
- [x] Keep `DatabaseMigrationWorker` and OpenIddict/EF registration in API. They are composition/hosting concerns, not application use cases.

### Complexity and splits

- [x] Split `HouseEndpoints` (formerly 245 lines, 46 branches) by extracting one `HouseService`; no per-endpoint service hierarchy was added.
- [x] Split `AuthEndpoints` (formerly 174 lines, 33 branches) by extracting `AccountService`; retain token/cookie response mapping in the endpoint adapter.
- [x] Review `MealPlannerService` (350 lines, 30 branches): keep it cohesive. Extract response loading only if query profiling or new behavior requires it.
- [x] Review `RecipeService` (289 lines, 32 branches): keep slugging, validation, and ingredient mapping together; they are cohesive and unit-tested.
- [x] Keep `IngredientService` (201 lines, 23 branches) intact. Translation and ingredient writes share one transaction and splitting them would weaken that boundary.

### Readability and naming

- [x] Expand compressed authentication helpers and replace one-letter request/error variables with descriptive names.
- [x] Replace unclear one-letter variables and entity lambda parameters in the cleaned Admin, Auth, and Houses features. Loop indices remain conventional.
- [x] Skip new formatting/analyzer dependencies. Existing formatting plus zero-warning builds are sufficient; add enforcement only if naming drift recurs.

## Verification policy

- Run focused tests after each extracted workflow.
- Run `dotnet test Kotlet.slnx --no-restore` after each architecture boundary is completed.
- Existing integration coverage characterizes Admin, Houses, Auth, and OAuth. Add tests before changing uncovered branches; do not duplicate covered endpoint behavior with service-only tests unless the extraction introduces logic.

## Dependency injection organization

- [x] Move Application service registrations into their vertical-slice `DiExtension` files; compose them in `AddApplication`.
- [x] Move Infrastructure adapters, persistence, caching, and seeders into their owning vertical-slice `DiExtension` files; compose them in `AddInfrastructure`.
- [x] Move `HouseSessionService` registration from the API root into the Houses feature extension.
- [x] Keep Domain free of dependency-injection references because it has no services to register.

## Ponytail audit

- `shrink:` compressed helpers in `AuthEndpoints` hide validation and token-family behavior; format and name them normally.
- `yagni:` do not split large cohesive Application services solely by line count.
- `native:` retain ASP.NET Core/EF Core/OpenIddict integrations; no custom replacement or new dependency is warranted.
- Net dependency reduction currently justified: 0. Expected line reduction is secondary to removing API-to-Infrastructure workflow coupling.
