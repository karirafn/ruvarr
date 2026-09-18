# Re-Queue Manual Refresh After In-Flight Pass

## Context

`QueueNotifier<TItem>.PriorityEnqueue` previously silently dropped any refresh request targeting an item whose `IsProcessing` flag was set.
This meant a user clicking "refresh" while a program was being processed received no feedback and no follow-up action — the request was lost.

## Decision

Replace the silent no-op with a `RefreshAgain` boolean flag stored on each queue entry (the `_items` dictionary value tuple is widened from `(TItem Item, LinkedListNode<int> Node)` to `(TItem Item, LinkedListNode<int> Node, bool RefreshAgain)`).

When `PriorityEnqueue` targets a Processing item it sets `RefreshAgain = true` and returns without touching `_order` or `_read`, so the in-flight pass is completely undisturbed and the item is not re-leaseable mid-pass.
Repeated calls on the same processing item are idempotent because the flag is a bool, not a counter.

When `MarkComplete` removes a Processing entry whose `RefreshAgain` flag is set, it re-inserts the item as a fresh `Pending` entry at the front of the queue (using `CreatePending`) and clears the read-set entry, making it immediately leaseable for a follow-up pass.
When the flag is not set, `MarkComplete` removes the item exactly as before.

`ProgramRefreshNotifier.MarkComplete` is unchanged: it calls `base.MarkComplete` and then checks `Items.Count`.
Because a re-queued item keeps `Items.Count > 0`, batch finalisation defers naturally until the follow-up pass truly completes.

## Considered Options

- **Return a discriminated result from `PriorityEnqueue`** — lets callers surface feedback immediately, but adds API surface to a component that is consumed by a Blazor UI polling loop; the UI already re-reads `Items` on every `QueueChangedEvent` broadcast, so status is visible through the existing channel. Deferred to a later UI step if the UX review requires it.
- **Re-enqueue immediately on `PriorityEnqueue`** — simpler flag-free logic, but requires pre-empting the in-flight pass, which breaks the single-program unit-of-work guarantee established in [ADR 0001](0001-per-program-unit-of-work-in-episode-sync.md).

## Consequences

A manual refresh requested during an in-flight pass is honoured exactly once after that pass completes.
The item appears as Processing in the UI for the duration of the original pass, then transitions to Pending for the follow-up pass; no request is silently lost.
Batch statistics (total, duration) count both passes and finalise only when the follow-up pass also completes, preserving the accuracy of `LastRunTotal`.
