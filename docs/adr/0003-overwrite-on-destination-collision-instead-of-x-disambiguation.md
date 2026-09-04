# Overwrite on Destination Collision Instead of `.X` Disambiguation

## Context

The previous implementation detected a filename collision in the completed directory and appended `.X` before the extension (e.g., `Show.S01E01-RUV.X.mp4`). A `.X` file is an unclaimable orphan: no `DownloadQueueItem` ever references `.X`-suffixed names, so Sonarr cannot import it and it accumulates indefinitely. This is the class of artefact the staging approach in [ADR 0002](0002-stage-downloads-in-incomplete-directory-move-on-complete.md) was designed to eliminate.

## Decision

Replace the `.X` disambiguation branch with `File.Move(overwrite: true)`. When two downloads resolve to the same destination path, the later one silently overwrites the earlier one. The `.X` branch and the `fileAlreadyExists` parameter on `RuvEpisode.ToFilePath` are deleted entirely.

## Considered Options

**Append a counter suffix** (`.1`, `.2`, …): Same problem as `.X` — any suffix other than the canonical filename is unclaimable by any `DownloadQueueItem` reference.

**Reject the second download**: Would require identifying the duplicate at enqueue time and surfacing an error. Deferred — the collision scenario (#232) is rare and the detection logic is non-trivial.

## Consequences

When two `RuvEpisode`s are matched to the same TVDB episode (a data-quality defect tracked in #232) and both are downloaded, the second download silently overwrites the first. This is considered acceptable as a temporary measure; #232 will prevent the duplicate match from occurring in the first place.
