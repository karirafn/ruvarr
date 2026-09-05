# Resume Interrupted Downloads as Pending

## Context

A scheduler shutdown cancels an in-flight download while the queue item is `Downloading`. Since Quartz cancellation tokens were propagated through the job call chains (#385), the cooperative-cancellation `OperationCanceledException` now reaches the `DownloadQueueProcessor` catch-alls, which carry `when (ex is not OperationCanceledException)` and therefore let it propagate — leaving the item stuck in `Downloading`, a state the processor's `Pending`-only query never re-selects. A hard crash (kill/power loss) reaches the same stuck state via no catch block at all.

## Decision

An interrupted `Downloading` item resumes as `Pending`. A new `DownloadQueueItem.MarkInterrupted()` transition moves `Downloading → Pending` **without** touching the retry budget (`RetryCount`, `NextRetryAt`, `FailureReason` unchanged), because a shutdown is not a download failure. The reset is performed at startup by `IncompleteDownloadCleanupService`, which already deletes the orphan's incomplete file — so "file gone → re-download from scratch as Pending" is coherent, and one reclamation path covers both graceful shutdown and hard crash. The in-job catch-alls and the `outcomeWrite`/`CancellationToken.None` construct stay: a genuine ffmpeg/move *failure* coinciding with shutdown must still persist its `Failed` outcome.

## Considered Options

**Mark interrupted items `Failed`**: `MarkFailed()` increments `RetryCount`, so repeated restarts would burn the bounded retry budget from [ADR 0004](0004-bounded-download-retry-with-exhausted-terminal-state.md) and eventually strand a healthy download as `Exhausted`, with a misleading `FailureReason`. A shutdown is not a failure.

**A distinct `Interrupted` status**: would require a new re-pickup branch in the processor query (or retry job), `/downloads` rendering, and a migration — net new surface for no behavioural gain over `Pending`, which the processor already consumes.

**Handle cancellation in an in-job catch**: does nothing for a hard crash, so the startup sweep is required anyway; adds a redundant second reset path and risks a DbContext/host-disposal race when saving during shutdown.

## Consequences

- Interrupted downloads resume automatically on restart with no manual action and no retry-budget cost.
- One reclamation path (startup sweep) covers both cancellation and hard crash.
- `IncompleteDownloadCleanupService` now loads tracked and mutates status; its prior "delete files only, never touch status" contract is intentionally replaced.
- Import-phase cancellation (item already `Complete`, file on disk) is out of scope and deferred to the retry/visibility work.
