# Free supplemental nutrition lookup

New product lookups check the household catalog first, then Open Food Facts. If OFF has no product or any calorie/protein/carbohydrate/fat value is missing, an optional USDA FoodData Central client searches branded foods by the exact EAN/UPC/GTIN. A returned code must match in both search results and food details. Similar names are never used as matches.

Only missing values are filled; known zeros, corrections, names and package sizes are retained. Incompatible mass/volume nutrition bases are not merged. USDA serving-label values are converted to 100 g or 100 ml using the declared serving amount and unit. Mass-based USDA nutrients are never relabeled as volume-based values. A serving is not an entire package: USDA-only drafts default to one piece until the user confirms the package size.

Imported nutrition is visible in the confirmation form. Saving persists the nutrient values, USDA record id and timestamp, and retains OFF attribution on mixed records. USDA credit links use the validated numeric record id; USDA-only data are not labeled ODbL. Saved products remain immediately available without external requests. For an existing incomplete catalog entry, **Find missing nutrition values** explicitly repeats the external lookup; the enriched draft still requires saving, preserves catalog corrections and never changes stock quantities.

Failures, malformed responses, missing API credentials and rate limits leave manual entry available. Public responses are size/depth/type checked, nutrient ranges are bounded, and a process-wide cache, outbound budget and Retry-After cooldown limit API use. Caller cancellation is propagated.

## Activate the free API

1. Obtain a free key from [FoodData Central](https://fdc.nal.usda.gov/api-key-signup/) / [api.data.gov](https://api.data.gov/signup/).
2. In the **backend** Azure App Service configuration, set `FoodDataCentral__ApiKey` to that key, then save/restart the application. For local work, use .NET user secrets or a server environment variable with the same name.
3. Never put the key in source code, browser settings, frontend environment files, screenshots, logs, chat or PR descriptions. Requests send it in the `X-Api-Key` header, not the URL.

The setting is empty by default; no USDA requests are made until configured. Do not use the public exploratory `DEMO_KEY` in production. This integration makes no paid API calls and cannot upgrade an account or incur API charges. USDA's normal free limit is 1,000 requests/hour/IP; the client also limits outbound calls (including searches and detail fetches) to 15/minute. Search misses and failures are cached, and HTTP 429 triggers a shared cooldown.

A successful response is cached for 24 hours, a missing product for 15 minutes, and a transient failure for 30 seconds. External lookup results are drafts: only user confirmation writes the catalog and stock.

USDA is an additional catalog, not universal coverage of Polish products. If no exact record or usable nutrient value is available, the field stays empty. Labels and formulations can change; the confirmation form remains editable.

## Data terms and validation

- [USDA API guide, key requirements and CC0/public-domain data terms](https://fdc.nal.usda.gov/api-guide/).
- [USDA branded-food documentation](https://fdc.nal.usda.gov/GBFPD_Documentation/).
- [api.data.gov header-based authentication](https://api.data.gov/docs/developer-manual/).
- Open Food Facts data retain their existing ODbL attribution and export notices.

Automated tests use controlled provider responses, not live network calls. The development API protocol was checked against USDA using its public exploratory key; production activation requires the operator's own free key.
