# ASPIRE-006C2 live tracker comparison measurement

Measured: 2026-10-18 22:16 UTC in the successful Linux full-stack acceptance run, attempt 2 of [workflow run 39170608485](https://github.com/ibis-ssl/Duck/actions/runs/39170608485). The first attempt timed out waiting for AppHost resource readiness; attempt 2 passed the full-stack packet and active-motion acceptance.

## Observed input rates

The harness listened for 5.0023 seconds after draining pre-window datagrams. Rates below are measured datagrams/frames received in that window, divided by its actual duration; they are not assumed UI refresh rates.

| Source | Packets / tracked frames | Measured rate | Mean frame contents |
| --- | ---: | ---: | --- |
| Duck | 141 / 141 | 28.19 packets/s | 22 robots, 1 ball |
| TIGERs | 590 / 590 | 117.95 packets/s | 22 robots, 1 ball |
| ER-Force | 1,449 / 1,449 | 289.67 packets/s | 22 robots, 1 ball |
| Vision packets received by comparison endpoint | 290 | 58.00 packets/s | — |

The separate acceptance harness counters recorded 2,413 tracker and 320 Vision datagrams consumed over the test’s broader interval. They are whole-test counters and are not the 5-second rate sample above.

## Comparison endpoint cost

The measurement warmed up 100 calls, then invoked the real comparison health method 1,000 times against the resolved tracker snapshot store and Vision packet store. Total evaluation interval was 381.8097 ms.

| Metric | Result |
| --- | ---: |
| p50 per endpoint call | 257.466 µs |
| p95 per endpoint call | 896.112 µs |
| Managed allocation per endpoint call | 94,064 bytes |
| Mean throughput over the measurement interval | 2,619 calls/s |

Boundary includes `ExternalTrackerSnapshotStore.GetSnapshot()` protobuf clones, source resolution, difference evaluation, `VisionPacketStore.GetSnapshot()` clones, and `IResult` construction. It excludes HTTP serialization and network request/response. This is one Linux CI runner observation, not a universal cadence or performance target.

Runtime: .NET 10.0.13; Ubuntu 24.04.5 LTS; x64; 4 logical processors.

## Verification

- Linux full-stack Aspire acceptance passed on attempt 2, including direct tracker Vision input capture, live comparison measurement, packet checks, and active motion.
- CI .NET test job passed.
- Focused local comparison integration tests passed (12/12).
- `dotnet build Duck.slnx --no-restore --verbosity quiet` succeeded with 9 warnings (NU1900 and ASPIRE010).
- Focused full-stack acceptance build succeeded with 0 errors.

The earlier Windows evaluator-only synthetic numbers in this report’s prior version did not include live cadence, packet clone costs, or the endpoint path and are superseded by the measurement above for this integration.
