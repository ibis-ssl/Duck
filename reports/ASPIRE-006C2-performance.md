# ASPIRE-006C2 integration performance measurement

Date: 2026-10-18 (UTC)

## Workload and method

- Evaluated three resolved source snapshots (Duck, TIGERs, ER-Force).
- Each synthetic frame contained 12 robots (six per team) and one ball.
- One evaluator invocation resolved readiness, selected snapshots, and calculated all three unordered source pairs (Duck–TIGERs, Duck–ER-Force, TIGERs–ER-Force).
- Warm-up: 500 invocations. Measurement: 3,000 invocations on the current thread.
- Timing used Stopwatch per invocation; allocation used GC.GetAllocatedBytesForCurrentThread.
- The 60 calls/s figure is a scenario used to contextualize the measurement, not an observed live tracker cadence or a performance target.
- Boundary includes snapshot readiness/resolution and pair evaluation. It excludes UDP reception, live source cadence, HTTP serialization, and UI rendering.

## Result

- Throughput: 23,226 evaluator invocations/s.
- Median: 41.00 µs/invocation.
- p95: 48.70 µs/invocation.
- Managed allocation: 33,728 bytes/invocation.
- At the illustrative 60 calls/s scenario, p95 consumes 0.292% of a 16.67 ms frame budget per evaluator invocation.
- Runtime: .NET 10.0.13; OS: Microsoft Windows 10.0.22631; architecture: x64; logical processors: 16.

This is a focused synthetic measurement, not a live end-to-end latency or allocation profile.

## Verification

- TrackerDifferenceSnapshotEvaluatorTests and DebugHostHealthEndpointTests were run as focused tests.
- The dedicated measurement test passed with the results above.
- Full-stack Aspire/Linux acceptance was not run: the existing environment has no docker command available. No environment setup or new dependency was performed.

