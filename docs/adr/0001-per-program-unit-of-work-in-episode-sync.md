# Per-Program Unit of Work in RÚV Episode Sync

## Context

`RuvEpisodesSyncJob` processes a batch of queued programs. The original implementation loaded all programs in a single up-front query, then looped over the in-memory list. A single unhandled exception (e.g., a `SaveChanges` constraint violation from a titleless episode) would abort the entire batch, leaving the in-progress item stuck in **Processing** and abandoning all remaining programs.

## Decision

Replace the single batch query with a lightweight up-front projection (RuvId + Name only, `AsNoTracking`) to determine the sorted work list, then load each program individually via `FindRuvProgramAsync` inside the loop. Each iteration runs inside a `try/catch (Exception)` that calls `ChangeTracker.Clear()`, marks the item complete, and continues to the next program.

This trades one tracked batch load for N per-program tracked loads. The tradeoff is accepted because:

- Fault isolation requires that each program's dirty state (tracked mutations + pending inserts) never carries over into another program's `SaveChanges`. A shared tracked batch load makes this impossible without manual detach bookkeeping.
- `ChangeTracker.Clear()` is the idiomatic EF Core way to discard all tracked state, but it requires that no other program's state is currently in the tracker — which is only guaranteed when each program is loaded fresh per iteration.
- The extra DB round-trips (N queries vs. 1) are acceptable: the bottleneck in this job is the RÚV HTTP call per program, not the DB queries.

## Considered Options

**Batch load + manual detach**: Load all programs in one query, then after a failure, call `dbContext.Entry(program).State = EntityState.Detached` for each affected entity. Rejected because it requires tracking which entities are dirty and detaching them precisely — fragile to future mutation additions.

**Separate DbContext per program**: Instantiate a new `DbContext` per iteration via a factory. Rejected because the job receives a single scoped `DbContext` via DI; introducing a factory would require changing the constructor and DI registration, which is a broader change than the scope of this fix.
