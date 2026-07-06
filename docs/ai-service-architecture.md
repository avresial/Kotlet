# AI service architecture

Kotlet needs an AI client available inside the API so future features — AI chat,
ingredient translation — can call a language model without each feature re-solving
provider setup, tool calling, streaming, and telemetry. This document records the
chosen abstraction and the boundaries around it.

## Decision

Use **`Microsoft.Extensions.AI`** and its `IChatClient` interface as the in-process
AI abstraction. Do not hand-roll a bespoke `IAiService` over raw HTTP: `IChatClient`
is the abstraction, and rolling our own would mean re-implementing the tool-invocation
loop, streaming, and OpenTelemetry integration the pipeline already provides.

The first backing provider is an **OpenAI-compatible endpoint**, via
`Microsoft.Extensions.AI.OpenAI` and the OpenAI SDK's `.AsChatClient()`. This matches
what `UserAiProviderConfiguration` already stores (`BaseUrl`, `ApiKey`, `DefaultModel`)
and keeps the door open to Azure OpenAI, local gateways, or Anthropic later without
touching call sites.

```text
Kotlet.Application/Ai/
  IChatClientFactory       # builds a configured IChatClient from a provider config
  AiTranslationService     # structured single-shot calls, no tools (first slice)
  AiChatService            # multi-turn chat with tools (later slice)
  KotletTools              # AIFunctionFactory.Create over Application services

Kotlet.Infrastructure/Ai/  # existing provider config storage + factory implementation
Kotlet.Api/Ai/             # existing provider-config endpoints
Kotlet.Api/Mcp/            # existing MCP server (external agents) — unchanged
```

## Provider resolution is per user

Provider settings are stored per user (`UserAiProviderConfiguration`), so a single
DI-registered singleton `IChatClient` does not fit. Instead an `IChatClientFactory`
builds a pipeline from a supplied provider config on each request:

```text
OpenAIClient(BaseUrl, ApiKey).AsChatClient(model)
  → UseFunctionInvocation()   # only for tool-using features
  → UseOpenTelemetry()        # reuses the existing OTel stack
  → (UseDistributedCache())   # optional
```

- **AI chat** uses the calling user's stored config and API key.
- **AI translation** is application-level and uses an app-configured key, falling
  back to that rather than requiring every user to configure a provider.

The factory is the seam. Features depend on `IChatClientFactory` and `ChatOptions`,
never on a concrete provider SDK.

## Tools vs. MCP

This is the load-bearing boundary. Kotlet both *is* an MCP server and *hosts*
AI features, and the two must not be confused.

- **In-process AI features do not call Kotlet's own MCP server.** The MCP server is
  an HTTP + OAuth endpoint. An in-process agent reaching its own tools through an
  authenticated HTTP round-trip back into the same process is pure overhead and a
  security detour. MCP is a protocol for crossing a process, network, or trust
  boundary — not for talking to code in the same assembly.
- **In-process features expose `Kotlet.Application` services as `AIFunction` tools**
  (`AIFunctionFactory.Create(...)`) passed directly in `ChatOptions.Tools`.
  `FunctionInvokingChatClient` runs the call-model → invoke-tool → feed-result loop.
- **The existing MCP server stays the boundary for external agents** (Claude Desktop,
  third parties). It is unchanged by this work.
- **A remote MCP server** (e.g. a third-party nutrition-data server) is the one place
  "call MCP directly" applies: the `ModelContextProtocol` client converts remote MCP
  tools into `AIFunction`s that join the same `ChatOptions.Tools` list.

| Consumer | Mechanism |
| --- | --- |
| In-process chat / translation | `AIFunction` tools over Application services |
| External agents | Existing MCP server (`Kotlet.Api/Mcp`) |
| Remote third-party tools | MCP *client* → `AIFunction` → `ChatOptions.Tools` |

The in-process `AIFunction`s and the MCP server tools wrap the **same
`Kotlet.Application` service methods**. `McpModels.cs` already separates agent-facing
shapes from REST DTOs; those handlers back both surfaces. One source of truth, two
transports.

## Ingredient translation worker

Ingredient names are stored in the default language on the entity; other languages live in the
translation dictionary (`Ingredients_{id}_{lang}`). The `IngredientTranslationService` finds every
ingredient/language pair that has no dictionary entry and fills it in with a single structured
translation call, committing the whole pass at once.

It runs from a `BackgroundService` (`IngredientTranslationWorker`) that triggers **on API startup**
(once migrations and seeding are ready) and **on every ingredient add**. The add trigger is an
in-process signal (`IIngredientTranslationSignal`, a capacity-one channel): `IngredientService`
`Notify()`s after a successful create, the worker awaits it, and bursts collapse into one pass because
the worker always re-scans for *all* missing translations.

Because translation is application-level rather than per-user, the worker uses
**application AI credentials** (`ApplicationAiOptions`, bound from the `Ai:Application` configuration
section) resolved through `IApplicationChatClientResolver` — the same `IChatClientFactory` seam, but
fed app configuration instead of a user's stored provider. When no application API key is configured
the worker is a quiet no-op, so the feature is opt-in via configuration and never blocks startup.

## Feature shapes

- **Translation** is a single structured call: `ChatOptions` with a JSON response
  format, no tools. This is the first slice — simplest, and it exercises the factory,
  the pipeline, and OpenTelemetry without the tool loop.
- **Chat** is multi-turn with tools: the same `IChatClient`, a `List<ChatMessage>`
  conversation, and `ChatOptions.Tools` populated from `KotletTools`.

## Boundaries

- Features depend on `IChatClientFactory`, `IChatClient`, and `ChatOptions` from
  `Microsoft.Extensions.AI` — never on a provider SDK type.
- Tool definitions live once in `KotletTools` and wrap Application services; they are
  shared with the MCP server rather than duplicated.
- Raw API keys stay in the provider-config service and the factory. The existing
  `ponytail` note in `UserAiProviderService` about encrypting stored keys still
  applies and is out of scope for this document.
- Telemetry flows through the pipeline's `UseOpenTelemetry()` into the existing OTel
  exporters; AI calls appear in the same traces as HTTP and database activity.

## Packages

- `Microsoft.Extensions.AI` — `IChatClient`, `ChatClientBuilder`, `AIFunctionFactory`,
  `FunctionInvokingChatClient`, the OpenTelemetry/caching/logging middleware.
- `Microsoft.Extensions.AI.OpenAI` — `.AsChatClient()` over an OpenAI-compatible
  endpoint.
- `ModelContextProtocol` (client) — only if/when consuming a remote MCP server; not
  required for the in-process features.

## First step

Add the two packages, implement `IChatClientFactory` over the existing
`UserAiProviderConfiguration`, and wire `AiTranslationService` as the first vertical
slice. Chat and the shared `KotletTools` follow once the factory and pipeline are
proven.
