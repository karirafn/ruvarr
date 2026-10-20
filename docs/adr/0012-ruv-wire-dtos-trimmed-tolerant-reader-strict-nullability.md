# 0012. RÚV wire DTOs are a trimmed Tolerant Reader with a strict nullability boundary

**Date:** 2026-10-20
**Status:** Accepted

## Context

RÚV began sending `"rating": null` on some episodes. `RuvTvEpisode.Rating` was a non-nullable `int`, and a single null failed deserialization of the entire featured feed, stalling `RuvProgramRefreshJob` and downstream episode sync. The RÚV models carried many fields no part of the app reads, and their nullability did not match the live payload. A null in any unconsumed non-nullable value-type field would reproduce the same whole-feed abort.

## Decision

Trim the RÚV DTOs (`Infrastructure/Ruv/Models/`) to only fields with a production consumer, confirmed solution-wide with reference analysis (Tolerant Reader). Correct kept-field nullability to the observed payload (`RuvTvProgram.ForeignTitle` → `string?`). Enforce the contract at the boundary with a RÚV-only `JsonSerializerOptions` (`Web` defaults + `NfcStringConverter` + `RespectNullableAnnotations = true`), threaded through an optional parameter on `ApiClient.GetAsync` that defaults to the shared `RuvarrJson.Default`. TVDB and Sonarr are untouched and keep `RuvarrJson.Default`. `JsonException` from a strict violation propagates uncaught from `GetAsync`, failing loud and naming the JSON path at the boundary.

## Consequences

- A future null in a *kept* non-nullable field aborts the feed with a path-named `JsonException` at the boundary instead of a silent null-typed value causing an NRE downstream — easier to diagnose, but the whole feed still fails until the annotation is corrected.
- Adding a newly-null field to a RÚV DTO is now a deliberate `string?`/`Uri?` edit, not a silent widening.
- The pattern (per-client strict options, DTOs trimmed to consumers) is available to TVDB/Sonarr but is **not** applied there — their annotations are unaudited, and enabling strict mode globally would subject them to the same failure class without the audit. Enabling it per-client later requires the same trim-and-verify work.
- Trimming the DTOs required routing all unit-test DTO construction through shared builders in `Ruvarr.Testing`, which is now the single place the DTO shape is defined for tests.
