# 10. HTTP resilience pipeline owns timeouts

**Date:** 2026-09-18
**Status:** Accepted

## Context

Outbound calls to RÚV, Sonarr, and TVDB had no retry or circuit breaker; a transient
blip cost up to an hour of staleness (#411). We adopt `Microsoft.Extensions.Http.Resilience`
`AddStandardResilienceHandler` (retry 3× exponential+jitter over 5xx/408/429/timeout,
circuit breaker, `Retry-After`). The standard handler's total-request timeout defaults to
30s — identical to the existing `ApiClientTimeouts.Default` set as `HttpClient.Timeout`.
`HttpClient.Timeout` is an outer cancellation wrapping the whole pipeline, so with both at
30s they race and can cancel a request mid-retry.

## Decision

Set `HttpClient.Timeout = Timeout.InfiniteTimeSpan` on the three in-scope clients and make
the resilience pipeline the single authority on timeout: `TotalRequestTimeout` 30s (the
prior budget), `AttemptTimeout` 10s. Timeout values are centralised in `ApiClientTimeouts`
and applied through a shared `AddRuvarrResilience` helper. `RuvStreamInspector` is out of
scope and keeps its own 10s `HttpClient.Timeout` with no resilience handler.

## Consequences

Easier: one place (`ApiClientTimeouts` + the helper) governs retry, breaker, and timeout for
all outbound clients; retries can no longer be truncated by a racing `HttpClient.Timeout`.
Harder: `HttpClient.Timeout` no longer reflects the effective call budget — readers must know
the pipeline owns it (guarded by the registration tests asserting `Timeout.InfiniteTimeSpan`).
Re-introducing a finite `HttpClient.Timeout` on these clients silently restores the race.
