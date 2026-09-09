# TVDB Bearer Auth via DelegatingHandler with Single-Flight Login

## Context

`TvdbClient` originally owned its own bearer-token acquisition: it held `IMemoryCache` and `ISettingsStore` dependencies, ran a `SemaphoreSlim`-guarded login flow, and attached the `Authorization` header manually to every `HttpRequestMessage`. This made `TvdbClient` responsible for both authentication plumbing and business-level HTTP operations, and it prevented `TvdbClient` from inheriting the shared `ApiClient` base class (different constructor shape).

## Decision

Extract the login flow into `TvdbAuthenticationHandler`, a `DelegatingHandler` registered as transient on the TVDB typed client. The handler owns `IMemoryCache`, `ISettingsStore`, the semaphore (static so single-flight survives the transient lifetime), token caching, and `Authorization` header injection. `TvdbClient` derives from `ApiClient(ILogger, HttpClient)` and delegates all HTTP to the inherited `GetAsync<T>` helper; it no longer touches auth at all.

The handler derives the login endpoint's absolute URI from the incoming request's authority, so it remains correct regardless of the configured `BaseAddress`.

## Considered Options

- **Keep auth in TvdbClient** — rejected; couples auth plumbing to business operations, prevents `ApiClient` inheritance, and scatters `SemaphoreSlim` lifecycle management into a class that should not own it.
- **Dedicated auth service injected into TvdbClient** — rejected; achieves separation without the HTTP pipeline integration and requires callers to manually thread tokens, reproducing the header-injection concern.

## Consequences

`TvdbClient` becomes a thin wrapper over `ApiClient`, consistent with `SonarrClient` and `RuvClient`. Auth behaviour (caching duration, re-login on key change, single-flight) is tested independently in `TvdbAuthenticationHandlerTests`. New TVDB methods require no auth-plumbing code. See [ADR 0008](0008-single-seam-for-json-options-via-extension-method.md) for the companion JSON-options seam that enabled the base-class move.
