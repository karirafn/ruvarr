# 0013. Dashboard cards bind to background jobs by Quartz job-key name

**Date:** 2026-10-20
**Status:** Accepted

## Context

Dashboard job cards must surface each background job's last outcome (failure time,
truncated error, consecutive-failure count) so a silently-failing Quartz job becomes
visible, and the episode-sync card must name a failing program refresh as the cause of
a stall (#430). A single global `IJobListener` records outcomes into an in-memory
singleton keyed by the only identity the listener receives: `context.JobDetail.Key.Name`.
The Quartz registration already sets each key to `nameof(<JobType>)`, and the dashboard
handler already queries triggers by `new JobKey(nameof(<JobType>))`.

## Decision

The job-outcome notifier is keyed by the job-key name string. A dashboard card opts into
the failing display by reading `notifier.GetOutcome(nameof(<JobType>))` in
`GetDashboardHandler`. There is no separate job-identity enum or per-job reporting code;
the single global listener covers every registered job by construction. Causation
(`StallReason`) is computed in the handler, never in the razor.

## Consequences

Adding a new job requires no change to the notifier or listener — it is monitored
automatically. A card binds to its job by naming `nameof(<JobType>)`, which the compiler
validates (the type must exist), so the mapping cannot silently drift. The cost is that
the binding is a convention (a matching string on both sides) rather than a typed link;
a card for a job that is never registered would simply always show no outcome. Outcomes
are in-memory and reset on restart — accepted, since a still-broken job re-fails within
one trigger interval and the signal reappears without operator action.
