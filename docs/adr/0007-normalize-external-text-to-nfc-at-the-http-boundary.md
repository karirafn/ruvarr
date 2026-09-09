# Normalize External Text to NFC at the HTTP Boundary

## Context

RÚV serves episode titles in Unicode NFD (`o` followed by U+0308 COMBINING DIAERESIS) while serving program names in NFC (a precomposed U+00E6) — mixed forms within a single API response. TVDB's Icelandic translations are whichever form the contributor happened to type. Two strings that are semantically identical then compare unequal under `StringComparison.Ordinal`, and `StringExtensions.Sanitized` makes it worse: its `[^\p{L}\p{N}]` filter treats combining marks (category `Mn`) as punctuation and replaces them with spaces, so `Skjaldbökustrákur` in NFD sanitizes to `Skjaldbo kustra kur`. The Bluey episode *Turtleboy* went unmatched for five lookup cycles because of this, while three sibling episodes matched only because their TVDB translations happened to be NFD too.

The defect is not confined to episode matching. NFC literals in `RuvEpisode.PrefixRegex`, `RuvProgram.GenericEpisodeTitlePattern`, and the `"þáttur"` comparisons silently fail to match NFD input, so prefix stripping and generic-episode-number parsing no-op without any error.

## Decision

NFC is the canonical form for all text inside Ruvarr, established at the point text enters the process.

A `JsonConverter<string>` normalizes every deserialized string to NFC, wired through a single shared `RuvarrJson.Default` options instance consumed by `ApiClient` and `TvdbClient`. `Sanitized` normalizes to NFC as its first step, before the character-class filter, so the two acknowledged gaps in boundary coverage — `TMDbLib`'s own serializer and Blazor user input — still compare correctly. Because titles are write-once today, the RÚV sync additionally overwrites a changed episode title and resets the lookup backoff on unmatched episodes, which repairs existing rows without a one-shot backfill job.

NFC over NFD because it is the form the W3C Character Model specifies for the web, the form C# source literals are saved in, and the form the majority of both upstream APIs already emit — normalizing to NFD would rewrite far more text than it fixes.

## Considered Options

- **Swap `Ordinal` for `InvariantCulture` at comparison sites.** Culture-sensitive comparison is normalization-insensitive and would fix `IsMatch`, but it reaches neither the regex literals nor `Sanitized`'s mark-shredding, and it leaves the invalid state representable.
- **Normalize only inside `Sanitized`/`IsMatch`.** Smallest diff and it closes the reported bug, but NFD keeps landing in the database, where it continues to defeat the regexes and the filename derivation.
- **A `DelegatingHandler` normalizing whole response bodies.** A genuinely single seam per pipeline, and it would cover `TMDbLib`. Rejected because it buffers every response into a string and defeats streaming deserialization — including HLS playlists — to fix a problem that only affects text fields.
- **One-shot backfill job plus a settings flag**, mirroring `TvdbIslTranslationBackfillJob`. Rejected in favour of the self-healing sync: the backfill leaves a permanent flag and a job that can never run again, and the rows it would uniquely reach — those whose program RÚV no longer serves — are deleted by the sync anyway.

## Consequences

The converter runs on every deserialized string from every client, including IDs, slugs, and URLs. `string.IsNormalized` short-circuits, so ASCII costs a scan and no allocation.

Six `ReadFromJsonAsync` call sites must pass `RuvarrJson.Default` by convention; a seventh added later without it silently reintroduces the defect. Folding `TvdbClient` onto `ApiClient` would reduce this to one site and is tracked separately.

`ToFilename` output changes for NFD-titled unmatched episodes, from `Blæja.III.Skjaldbo.kustra.kur-RUV.mp4` to the correct spelling. A file already completed under the old name will not be found by `CompletedFileExists` and would be re-downloaded. No such rows exist at the time of writing.

Tests asserting this behaviour must express NFD with explicit `\u0308`-style escapes. A raw literal is normalized by the editor or by git's text handling, leaving both sides of the assertion in NFC — the test then passes against unfixed code.
