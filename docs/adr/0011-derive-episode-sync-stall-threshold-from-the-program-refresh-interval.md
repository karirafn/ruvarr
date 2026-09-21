# 0011. Derive episode-sync stall threshold from the program-refresh interval

**Date:** 2026-09-21
**Status:** Accepted

## Context
The dashboard's Episode Sync card must warn when episode ingest stalls. Episode sync fires every
5 seconds, so "last run" is not a health signal; the program-refresh job (interval 1 h) feeds the
queue and a wedge (#409) freezes `LastCompletedAt`. A hardcoded stall threshold silently becomes
wrong if the refresh interval changes.

## Decision
Introduce `RefreshSchedule.ProgramRefreshIntervalHours` as the single source for the Quartz refresh
trigger interval and derive the stall threshold as `2 × ProgramRefreshIntervalHours` in the
dashboard handler. Key the stall signal on `LastCompletedAt` (falling back to the notifier's process
`StartedAt` when nothing has completed since startup). Compute the boundary using an injected
`TimeProvider` so the threshold is deterministic under test.

## Consequences
Changing the refresh cadence moves the threshold automatically. The health signal reflects actual
ingest completion, not the 5 s scheduler tick, so a wedge surfaces as a stall. The handler gains a
`TimeProvider` dependency; tests must supply a fake clock. `ProgramRefreshNotifier` keeps all
queue-lifecycle state (it is the queue's notifier, not the refresh job's), and the read model — not
the notifier — partitions telemetry between the two cards.
