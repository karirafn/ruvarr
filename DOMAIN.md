# Domain Glossary

The single source of truth for Ruvarr's ubiquitous language. Use these terms exactly in code identifiers — class names, methods, variables, events. When a concept needs clarification, update this file first, then the code.

## Core Entities

- **RuvProgram** — A TV program or movie published by RÚV (Icelandic public broadcaster). Optionally matched to a `TvdbSeries` (TV) or `TmdbMovie` (film). Owns its `RuvEpisode` collection.
- **RuvEpisode** — A single episode of a `RuvProgram`, sourced from the RÚV API. Optionally linked to one or more TVDB episodes once matched.
- **TvdbSeries** — TVDB series metadata (value object owned by a matched `RuvProgram`).
- **TvdbEpisode** — A link row recording that a `RuvEpisode` corresponds to a specific TVDB season/episode number. Its presence is what makes an episode "matched."

## Episode Matching

The process of linking each `RuvEpisode` to its TVDB season/episode so it can be downloaded and imported.

- **Matched episode** — A `RuvEpisode` that has at least one `TvdbEpisode` link row.
- **Unmatched episode** — A `RuvEpisode` with no `TvdbEpisode` link row. The lookup job repeatedly attempts to match these.
- **Partially-matched program** — A `RuvProgram` where some episodes are matched and some remain unmatched. Commonly arises when RÚV publishes later episodes after the earlier ones were already matched.
- **Matching strategy** — A focused rule that attempts to match unmatched episodes. Strategies run in sequence; each only touches episodes still unmatched after the previous ran:
  - **Episode Number matching** — Matches by episode number when the TVDB season's episode count equals the program's total episode count (a 1:1 season alignment). Handles **generic episode titles**.
  - **Part-One Sibling matching** — Matches a "part two" episode by locating its already-matched "part one" sibling and resolving the adjacent TVDB episode by name.
  - **Translation matching** — Matches by comparing the RÚV title against the episode's Icelandic (`isl`) TVDB title translation.
- **Matching season resolution** — Choosing which TVDB season a program's episodes belong to: the program's explicit season number when set, otherwise the unique TVDB season whose episode count equals the program's total episode count.
- **Default season** — The TVDB season the manual match dialog offers first: the lowest season already matched by the program's other episodes, falling back to the program's earliest available TVDB season. Distinct from **matching season resolution**, which is the automated strategy's rule — the default season only shapes what the UI presents.
- **Generic episode title** — A RÚV title of the form "Þáttur N af M" ("Episode N of M"), carrying the episode number but no descriptive name. Number-based matching is the only viable strategy for these.

## Lookup & Refresh

- **Lookup schedule** — The exponential backoff governing when an unmatched program or episode is next re-attempted (1h → 2h → 4h → 1d → 7d), tracked via `LookupCount` and `NextLookup`.
- **Manual refresh** — A user-initiated re-attempt from the UI. Priority-enqueues a fresh lookup that re-runs the matching strategies on all unmatched episodes immediately, ignoring the lookup schedule backoff.
