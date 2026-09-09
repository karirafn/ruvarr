# Single Seam for JSON Options via Extension Method

## Context

The codebase uses a custom `JsonSerializerOptions` context (`RuvarrJson.Default`) that includes NFC string normalization converters. Before this change, every `ReadFromJsonAsync` call in `TvdbClient`, `ApiClient`, and any future HTTP client had to pass `RuvarrJson.Default` explicitly — creating multiple independent sites where the wrong options (or no options) could silently be used.

## Decision

Introduce `HttpContentJsonExtensions.ReadFromRuvarrJsonAsync<T>` as a single extension method that encapsulates the `RuvarrJson.Default` options. All deserialization of HTTP response bodies routes through this one method. `ApiClient` uses it exclusively; `TvdbClient` inherits it via `ApiClient`.

## Considered Options

- **Pass options at every call site** — rejected; each new method or client is a new defect opportunity, and code review cannot catch them all.
- **Inject `JsonSerializerOptions` via DI** — rejected; the options are not environment-specific and DI injection adds constructor complexity without benefit.

## Consequences

Adding a new NFC converter or changing deserialization behaviour requires editing one method. Omitting the seam at a new call site is a compile-time visible omission (the method name encodes the intent) rather than a silent wrong-options pass.
