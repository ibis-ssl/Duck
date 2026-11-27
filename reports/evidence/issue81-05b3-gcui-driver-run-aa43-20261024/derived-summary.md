# Issue 81 05B3 safe runtime evidence (derived)

This summary is derived from the two copied safe files in this bundle. Private AppHost stdout/stderr logs were not read or copied.

- Source commit recorded by the safe run metadata: c4bccbf.
- Four-resource run: Duck readiness was HTTP 200/Ready; identity was present; decoded Vision count was 5,341 and failure count was 0.
- ServiceStarted and ServiceStopped stages were observed. Resource notification/current-state, projection, engine check, container inspect, and identity exec stages are summarized in service-stage.jsonl.
- GC UI was not opened. RefereeDriver application health/API was not queried. These are not verified as working.
- Stop: Ctrl+C was sent; outer shell result was exit 1. The native AppHost exit code was not collected, so graceful application exit is unverified. Subsequent safe ownership checks found no remaining containers for this run; this does not establish cleanup for unrelated resources.
- No new run is included in this bundle.

## Stage counts

- ContainerInspect:Accepted: 10
- ContainerInspect:Started: 10
- CurrentResourceState:Accepted: 20
- EngineCheck:Accepted: 10
- EngineCheck:Started: 10
- IdentityExec:Accepted: 10
- IdentityExec:Started: 10
- ResourceNotification:Accepted: 20
- ResourceProjection:Accepted: 10
- ResourceProjection:Rejected: 10
- ServiceStarted:Started: 1
- ServiceStopped:Accepted: 1
