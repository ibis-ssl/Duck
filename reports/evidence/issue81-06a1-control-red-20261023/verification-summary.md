# 06A1 control protocol evidence summary

- Fixed test source SHA-256: `a9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06`. Protocol source SHA-256: `8506a43d3fac16be8d2d7e543d9a848a9586c4e2c511980856a5821cf034e183`.
- Original 2-test Red: build succeeded, 1 pass / 1 fail, exit 1. Extended matrix Red: build succeeded, 2 pass / 2 fail, exit 1. Raw command/stdout/stderr are retained separately.
- Implementation results: focused 5/5 exit 0 (`green-v6`); full suite 786/786, 0 skipped, exit 0 (`full-tests-v2`). A prior full run `full-tests-v1` was 785/786 with a hosted-service startup race; it remains preserved. Two intermediate build failures (`green-v2`, `green-v3`) were compile/setup issues, not functional Red.
- Separate no-restore RuntimeHost and AppHost builds exit 0. AppHost emitted one existing ASPIRE010 warning.
- Hosted service test #5 was added after initial Red; no pre-implementation Red exists for that test. AppHost/DCP resource-event-to-exec wiring and real DCP/container/UDP behavior remain unimplemented/unverified. Hardcoded ResourceId `duck` and random AppHost RunId env values were withdrawn.
- Resource key must use actual `ResourceEvent.ResourceId`; role name remains distinct. Current AppHost does not inject identity or issue exec.
