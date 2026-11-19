# Failed Downloads Exit Automatically When Their Cause Clears

## Context

[ADR 0004](0004-bounded-download-retry-with-exhausted-terminal-state.md) made `Exhausted` terminal, with a manual `RetryNow()` as its only exit.
In production, three downloads exhausted their budget on a cause no retry could fix.
Ruvarr and Sonarr mounted the same share at different container paths, so every Sonarr manual-import scan of Ruvarr's folder came back without the file.
One of the three had meanwhile been imported into Sonarr by another route, so retrying it would only have imported a duplicate.
Both causes clear outside the retry loop, and neither the backoff schedule nor the budget observes them.

This ADR replaces 0004's clause that `Exhausted` is terminal with only a manual exit.
The rest of 0004 stands.

## Decision

A `Failed` or `Exhausted` item leaves that state automatically when the condition that caused it clears, in addition to the backoff schedule and the manual `RetryNow()`.

- **Superseded** — when the episode's combined Sonarr status is `Downloaded`, the item is dismissed.
  `DownloadRetryJob` checks this on every pass, before it re-queues anything, so a superseded item is never retried into a duplicate import.
  The check is level-triggered rather than raised on the status transition, so it also clears items that were already superseded and transitions Ruvarr did not observe.
- **Sonarr cannot see the download folder** — import failures carry a typed failure kind instead of only a free-text reason.
  The Sonarr folder check writes a fixed-name empty `.mkv` into the completed directory, looks for it in Sonarr's manual-import scan, then deletes it.
  Every passing check re-queues each `Failed` or `Exhausted` item whose kind is the folder-visibility failure, with `RetryNow()` semantics: `RetryCount` is preserved and the completed file is reused.
  The re-queue is level-triggered rather than keyed on a failing-to-passing transition, because the redeploy that fixes a mount also restarts the app and would lose an in-memory transition.
  An import that fails while the latest check is passing is recorded as an unclassified failure, since Sonarr can see the folder; this keeps a persistent non-path failure from being re-queued on every check.
  The check runs at startup, on settings save, and on the 10-minute Sonarr reconcile interval.

The superseded check takes precedence over the re-queue.

## Considered Options

**Re-queue on the failing-to-passing transition only**: held in memory, the transition is lost on the restart that a mount fix requires; persisting it adds state for no gain over the level-triggered rule.

**Warning only, keep exits manual**: the folder check surfaces the mismatch, but stranded items wait for their backoff rung or a manual retry after the cause is fixed.
This was rejected because the condition that clears the state is observable, so a manual-only exit leaves avoidable work for the operator.

**Edge-triggered dismiss on a `Downloaded` transition event**: mirrors `EpisodeMissingEvent`, but misses items that were already superseded and any transition that happened while Ruvarr was not syncing.

## Consequences

- A dismiss, whether automatic or manual, deletes Ruvarr's own completed and incomplete files at the paths recorded on the item.
  Otherwise auto-dismissed items would leave files that nothing else cleans up.
- Rows that failed before the typed failure kind existed keep their free-text reason and are not re-queued by the folder check.
  They recover through the manual retry.
