# Stage Downloads in a Flat Non-Configurable Incomplete Directory, Move on Complete

## Context

Failed or crashed downloads left truncated files, `.tmp.mp4` crumbs, and empty `<Series>` subdirectories in the Sonarr-scanned completed folder. Sonarr would scan these artefacts and either fail to import them or import partial content silently.

## Decision

Stage every download under `<DownloadsRoot>/incomplete` (a flat, non-configurable directory), then move the file to the completed directory in a single rename once the download is fully written and trimmed. Presence in the completed directory is therefore a guarantee that the file is final and ready for Sonarr to import.

The directory is non-configurable specifically to guarantee that the source and destination of the rename share a filesystem, making it an atomic `rename(2)` rather than a cross-device copy. A separate `DownloadsRoot` setting already exists for user-controlled placement; `incomplete` is derived from it automatically.

A startup sweep handles orphans left behind by a crash — any `DownloadQueueItem` still in `Downloading` status at startup has its file cleaned up and the item reset.

## Considered Options

**Configurable incomplete directory**: Allow the user to point the incomplete path anywhere. Rejected because a path on a different filesystem turns the move into a copy-then-delete, losing atomicity and reintroducing the partial-file window this approach exists to close.

**`.tmp` extension rename in place**: Write to `<completed>/<file>.tmp.mp4` then rename to `.mp4`. Rejected because the completed directory is Sonarr-scanned; a scanner race during download would import a partial file.

## Consequences

On a system where `DownloadsRoot` and the completed directory are on different filesystems (e.g., Unraid shfs spanning multiple disks), the kernel falls back from `rename(2)` to a copy-then-delete. This is the same constraint every *arr application accepts on Unraid and is considered acceptable.
