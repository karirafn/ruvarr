# 0014. The Sonarr webhook is a nudge, and its callback URL is found by probing

**Date:** 2026-11-10
**Status:** Accepted

## Context

The Sonarr episode status (#440) is refreshed by the hourly RÚV episode sync, so a file Sonarr
imports, upgrades, renames, or deletes can take up to an hour to show in Ruvarr. Sonarr can post a
webhook on those events (#441). Two decisions shape how Ruvarr uses it.

First, a webhook payload carries enough to write a status directly (`series.tvdbId`,
`episodes[].tvdbId`, event type). The endpoint has no authentication because Ruvarr and Sonarr share a
private network, and Sonarr does not retry a failed delivery (`WebhookProxy` has no retry).

Second, Sonarr must be given a URL it can reach Ruvarr on. Ruvarr runs in a container whose bridge IP
can change on every deploy, and Ruvarr cannot tell from inside which of its addresses Sonarr can
reach. The registration must need no user action.

## Decision

**A delivery is a nudge, not a data source.** For `Download`, `EpisodeFileDelete`, and `Rename`
deliveries, the endpoint finds every program whose TVDB series matches `series.tvdbId`, clears the
cached Sonarr missing list, and queues those programs with `ProgramRefreshNotifier.PriorityEnqueue`.
The queued sync reads the status from Sonarr's API as it always does. Nothing from the payload is
stored. Every other event, including Sonarr's `Test`, is acknowledged with 2xx and ignored.

**The callback URL is discovered by probing through Sonarr's notification test.** Ruvarr builds
candidate URLs on its listening HTTP port: first the local address of a socket routed toward the
configured Sonarr host, then its host name. It asks Sonarr to test each with
`POST api/v3/notification/test` and registers the first one that passes. A reconcile runs once the
host is listening at startup (always re-probing, since the bridge IP may have changed), every
10 minutes (recreating or restoring the `Ruvarr` entry when deleted or changed), and when settings
are saved. The outcome is kept in memory and shown on the dashboard's Sonarr Webhook card.

The notification test reports an unreachable callback as either 400 or 500 depending on how the call
failed. Ruvarr therefore treats Sonarr as reachable once listing notifications succeeds, and any
later failed test as that candidate being unreachable.

## Consequences

Status keeps a single source (Sonarr's API), and the unauthenticated endpoint can do no more harm than
queue a re-sync of existing programs. A lost delivery delays the status by at most the hourly sync,
which stays the backstop. A season pack import produces many deliveries for one series, which
`PriorityEnqueue` collapses into one sync plus at most one follow-up.

No setting or environment variable is needed for the callback URL. A topology where no candidate is
reachable from Sonarr surfaces as a "Sonarr could not reach Ruvarr" failure on the dashboard, listing
the URLs tried; a manual override can be added then if needed. Each probe costs Sonarr one test call
per candidate, which happens only at startup, after a settings save, or when the entry needs fixing.
