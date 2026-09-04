# Bounded Download Retry with an Exhausted Terminal State

## Context

`Failed` was a dead-end terminal state with no automatic exit, violating the design rule that every degraded state must have an automatic exit.
A naive always-retry loop would hammer a broken download source indefinitely, providing no bounded recovery guarantee.

## Decision

Mirror the `LookupSchedule` backoff pattern via a `RetrySchedule` value object (1 h → 2 h → 4 h → 1 d → 7 d).
`MarkFailed(reason)` increments `RetryCount` and, when the budget is not yet spent, schedules `NextRetryAt` and sets status to `Failed`.
A `DownloadRetryJob` runs periodically and re-queues `Failed` items whose `NextRetryAt` has elapsed back to `Pending`, where the existing download processor picks them up.
Once `RetryCount` exceeds the budget, the item moves to a new terminal `Exhausted` state (`NextRetryAt = null`).
`Exhausted` items are always visible on the `/downloads` page, and a manual `RetryNow()` action is always reachable from there.
`RetryNow()` accepts both `Failed` and `Exhausted` items, ignores cooldown, preserves `RetryCount` (so a re-fail lands on the correct backoff rung or stays `Exhausted` rather than resetting the budget), and moves the item back to `Pending`.

Pre-existing `Failed` rows in production migrate with `RetryCount = 0` and `NextRetryAt = null`.
They are immediately manually retryable via `RetryNow()`, and auto-retry resumes only when the next failure sets `NextRetryAt`.

## Considered Options

**Always retry without a budget**: Provides unlimited automatic recovery but hammers a broken source indefinitely and gives no signal that human intervention may be needed.

**No automatic retry (status quo)**: Preserves the original dead-end `Failed` state that prompted issue #384.

## Consequences

- Downloads auto-recover within a bounded, exponentially-backed-off window, then surface for manual action — satisfying both the automatic-exit and the liveness-of-the-exit rules.
- The backoff switch duplicates logic already in `LookupSchedule`; this is the second occurrence, kept as-is per the rule-of-three (extract on third).
