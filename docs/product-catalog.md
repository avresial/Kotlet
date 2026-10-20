# Barcode products and pantry batches

The pantry supports two inventories: the existing **recipe ingredients**, and **packaged products**.
The latter have a household catalog, barcode identity, optional nutrition, and dated stock batches.
A barcode does not encode a name, calories, expiry, or amount. The user confirms product details and
stock quantity after lookup. An expiry date belongs to a batch, never to the reusable product.

## Architecture and compatibility

The feature follows the existing Domain / Application / Infrastructure / API structure and Angular UI.
`Product` is separate from the global `Ingredient`: a branded package must not overwrite recipe names,
measurement units, allergens or calories. Each house owns its catalog and stock; house members share it.
An optional `IngredientId` is a descriptive association. Product quantities are **not** automatically
added to ingredient quantities, recipe matches, meal plans, reconciliation, or shopping-list calculations.
This avoids counting the same physical stock twice or guessing conversions. Existing ingredient inventory
and shopping-list behavior is unchanged. Explicit conversion/consumption integration can be added separately.

EAN-8, UPC-A, EAN-13 and GTIN-14 are strings, checksum-validated and normalized to 14 digits; leading
zeros are retained. Equivalent UPC/EAN forms share one catalog entry per house. UPC-E and variable-weight
store codes requiring a retailer-specific decoder are outside this workflow.

The camera scanner decodes frames with a bundled, lazy-loaded software decoder (ZXing) rather than the
native `BarcodeDetector`, so it behaves the same in Chrome, Safari and other browsers. It needs
`getUserMedia` on HTTPS or localhost, starts the camera only after an explicit click, and reads EAN-8,
EAN-13, UPC-A and ITF-14. See [camera browser compatibility](changes/camera-browser-compatibility.md).

A product's stock quantity is in `g`, `ml` or `pcs`. Package count is converted to that unit using a
confirmed `PackageQuantity`. Unknown package sizes default to **1 piece**, and require user review;
free-text package descriptions are not parsed into guessed weights. Nutrition is optional and explicitly
per 100 g or per 100 ml; missing data remains null. Imported quantities are proposals, not verified facts.
A product's unit and package size cannot change while any stock row exists, including a zero-quantity row.
This prevents silently changing the meaning of existing stock. Name, brand, description, ingredient link
and nutrition can be corrected without losing the original source attribution.

Batches are unique by product, expiry (including no expiry), and location (including no location).
Confirmed additions increment the matching batch; a different date/location creates another batch.
Editing a batch sets its amount/date/location; a collision returns 409 rather than destroying either row.
Database uniqueness plus revision tokens and bounded retries protect concurrent additions from duplicate
rows and lost increments. Exhausted conflicts return 409 so callers can reload and retry. A confirmation
is one intended stock addition: POST is additive and must not be automatically replayed after a lost
HTTP response without first checking the current stock. The UI prevents duplicate in-flight submissions.

## HTTP interface

All routes require authentication and an active house; all product and stock access is scoped to it.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/products` | Household catalog, including products with no stock |
| GET | `/api/products/lookup/{barcode}` | Local first, otherwise OFF prefill with optional USDA nutrition fallback; `?refreshNutrition=true` enriches an incomplete saved entry |
| PUT | `/api/products/{barcode}` | Validate and save confirmed catalog data |
| GET | `/api/products/export` | Catalog and license attribution, without stock/house member data |
| GET | `/api/product-stock` | Product stock with batch details |
| POST | `/api/product-stock` | Add base-unit quantity to a batch |
| PUT | `/api/product-stock/{id}` | Set quantity, expiry and location |
| DELETE | `/api/product-stock/{id}` | Delete one batch; catalog entry remains reusable |

Lookup returns `local`, `open_food_facts`, `food_data_central`, `not_found`, `unavailable`, or `rate_limited` with a nullable
product draft. Only the explicit save writes a catalog entry. Unknown codes, outages and limits allow
manual entry. Invalid input returns 400; missing house access 401; foreign stock/product identifiers 404;
concurrent writes and batch collisions 409. Stock quantities have at most three decimal places.

## Open Food Facts operations

The backend calls the [OFF v3 product endpoint](https://openfoodfacts.github.io/documentation/docs/Product-Opener/v3/products/get-api-v3-product-code/)
with selected fields and `User-Agent: Kotlet/1.0 (+https://github.com/avresial/Kotlet)`. It does not search
as the user types, upload images, or send household stock to OFF. The fixed HTTPS host, disabled redirects,
10-second total request timeout, 1 MiB payload limit, bounded JSON depth, barcode matching, field types and
numeric ranges constrain external input. OFF's per-100-base-unit nutrients are used only when the package
unit identifies g or ml; a known nutrition basis can also be entered manually. Per-serving values are not
mistaken for per-100 values. No images are imported.

A singleton client serializes misses, coalesces repeated lookups, and permits at most 15 outbound calls
in a rolling minute, matching the [documented product read limit](https://openfoodfacts.github.io/documentation/docs/Product-Opener/api/).
HTTP 429 honors `Retry-After` across codes. Failed/invalid responses are distinct from a confirmed 404.
The bounded, 512-entry memory cache retains successful drafts for 24 hours, confirmed misses for 15 minutes,
and transient failures for 30 seconds. Saved local products are persistent and always take precedence;
OFF never silently refreshes user corrections. Restarting clears only the external cache and rate budget.

The limiter is process-local. Multiple app replicas sharing an egress IP need a shared outbound limiter
(or a budget divided between replicas) before scaling. Operators should register the use case using the
API usage form linked in OFF's documentation. Automated tests use fixtures, never the production service;
use OFF's documented staging host and basic authentication for any separate live integration experiment.

## Licensing and data provenance

Imported and subsequently corrected OFF data retain `Source = OpenFoodFacts`, the product source URL and
retrieval timestamp. The UI links to OFF and ODbL. Manual-only products are marked separately. Catalog
export includes attribution and a license link; it excludes pantry amounts, dates, locations and members.

OFF's database is [ODbL 1.0](https://opendatacommons.org/licenses/odbl/1-0/); individual contents are under
[DbCL](https://opendatacommons.org/licenses/dbcl/1-0/). Follow the [OFF terms](https://world.openfoodfacts.org/terms-of-use)
and [license guidance](https://openfoodfacts.github.io/openfoodfacts-server/api/tutorials/license-be-on-the-legal-side/).
Public use/distribution of an adapted database may require offering that adapted database under ODbL,
including modifications. Attribution alone is not the entire license obligation. Do not assume that
mixing manually entered records into an OFF-derived catalog removes share-alike requirements. Review
those obligations before distributing a combined catalog; the application does not publish private catalogs
or transmit user corrections to OFF. The software repository's license does not replace the data licenses.

## Database rollout

`AddProductCatalogAndStock` adds only `products` and `product_stock`, their foreign keys and unique indexes.
It does not migrate or rewrite existing pantry/recipe/shopping data. PostgreSQL uses the normal EF migration
startup path. SQLite follows the existing repository convention (`EnsureCreated`) for development/tests;
an already-created SQLite development database needs to be recreated or deliberately upgraded, as
`EnsureCreated` does not apply schema changes. Never delete an existing database without preserving its data.

## Supplemental nutrition

Missing calorie or macro values can be supplemented from the free USDA FoodData Central API after OFF, using exact barcode matching and review before save. Existing catalog entries remain local-first; an explicit nutrition search can enrich an incomplete saved entry. See [free nutrition fallback and activation](changes/free-nutrition-fallback.md).
