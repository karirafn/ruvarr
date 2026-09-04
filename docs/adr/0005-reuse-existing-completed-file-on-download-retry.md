# Reuse an Existing Completed File on Download Retry

## Context

When a download succeeded but the subsequent Sonarr import failed, a retry would re-run the full ffmpeg download even though the completed file was already present on disk — wasting time and bandwidth on a file that was already final.

## Decision

Before starting the ffmpeg download, the processor checks whether the target file already exists in the completed directory via `DownloadFileStore.CompletedFileExists`.
[ADR 0002](0002-stage-downloads-in-incomplete-directory-move-on-complete.md) guarantees that presence in the completed directory means the file is fully written, trimmed, and ready for import — the incomplete-to-complete move is atomic, so a partial file can never appear there.
When the file is found, the processor skips ffmpeg, trim, and move entirely and proceeds directly to Sonarr import.
When the file is absent, a fresh download runs as normal.

The naming convention from [ADR 0003](0003-overwrite-on-destination-collision-instead-of-x-disambiguation.md) ensures that `CompletedFileExists` probes the canonical filename — the same path the Sonarr import call references — so no aliasing issue arises.

## Consequences

- Retries after a failed import are near-instant: no re-download, no re-trim.
- Correctness of the skip path rests entirely on ADR 0002's atomicity invariant.
  If that invariant were ever broken (e.g., a copy-then-delete fallback on a cross-filesystem move that crashes mid-copy), the retry would import a partial file.
  No independent integrity check is added; the invariant is the only guard.
