# 05B1 liveness evidence summary v3

This generation records the current dirty-source verification for Protocol.cs SHA `44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18f`. It preserves all earlier manifests, logs, tests and metadata companion files; no old evidence was overwritten.

## Red/Green history

- `fatal-cleanup-red-v1` and `hosted-supervision-red-v2` are valid historical runtime Reds. The former independently records `DisposeAsync` propagation of `SocketException`; the failed assertion occurs before the test prints socket-path state, so it is not direct raw proof that cleanup failed. Existing code order showed the risk. `server-cleanup-green-v1` verifies exception propagation is allowed while owned path cleanup occurs. The corrected expectation is not represented as identical fixed Red/Green assertions.
- `noncooperative-io-red-v1`: exit 1, 0/4 successful, old Protocol.cs SHA `b96d255bc7e52fd017bcc9676df994fc5c2331575b1d14cec281d27de7bf67cf`; sync-blocking and cancellation-token-ignoring read/write/flush stayed pending beyond the injected deadline until fixture release.
- `noncooperative-io-green-v1`: exit 0, 4/4, Protocol.cs SHA `44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18f`; bounded caller completion works for the same noncooperative cases.
- The separate hosted supervision Green and prior 11-case boundary suite remain distinct observations. Existing 4-case liveness Green and the new 4-case noncooperative Green cover different behaviors.

## Current-source verification

All commands used `--no-restore -p:EnableSourceControlManagerQueries=false`, sequentially, under `/home/donabe/Duck-issue81-aspire-desktop-bridge`:

| Evidence record | Result |
| --- | --- |
| `protocol-suite-v2` | 5/5 pass, skip 0, exit 0 |
| `boundary-suite-v2` | 11/11 pass, skip 0, exit 0 |
| `liveness-suite-v2` | 4/4 pass, skip 0, exit 0 |
| `noncooperative-io-suite-v2` | 4/4 pass, skip 0, exit 0 |
| `full-unfiltered-v2` | 805/805 pass, skip 0, exit 0 |
| `runtimehost-build-v2` | exit 0; 0 warnings, 0 errors |
| `apphost-build-v2` | exit 0; existing ASPIRE010 warning 1, 0 errors |

The initial `protocol-suite-v2.json` was created before the test runner source-hash list included `RuntimeIdentityControlProtocolTests.cs`; its stdout proves 5/5, while a preflight sha256 and later run records establish that test file SHA as `a9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06`. Subsequent runner records contain all six source/test hashes. No rerun was needed solely to rewrite this metadata.

Current product/test SHA-256 values:

- `Tracker/Tracker.RuntimeHost/RuntimeIdentityControlProtocol.cs`: `44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18f`.
- `Tracker/Tracker.RuntimeHost/Program.cs`: `4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117b`.
- `RuntimeIdentityControlProtocolTests.cs`: `a9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06`.
- `RuntimeIdentityControlBoundaryTests.cs`: `58c304d7d96c7812a816bd8407c316f1ceb58f22539a1572718446f714ab8993`.
- `RuntimeIdentityControlLivenessTests.cs`: `2d136c3d9ad3320c3fcf61fba38e88da56ede44bd309c637b05643b152731745`.
- `RuntimeIdentityControlNonCooperativeLivenessTests.cs`: `c8251b75203047b8555d0c649a3d0b4b148e9778e5fc3c4a2535a83671a7c543`.

## Limits

The formal normal review covers prior source generation `b96d255...`, not this `44d8dc7...` local candidate. The current test/build results are not an exact PR CI result and do not prove Docker/DCP engine equality, exec, container lifecycle, or actual identity handoff. NR005 remains open. A previous duplicate-test audit confirmed process termination but did not recover the original tool exit; this audit gap remains. Its recorded `lstart` timezone was unspecified and is not represented as UTC. `metadata-companion-v1.json` is separate from and does not overwrite malformed original metadata. Earlier `full-unfiltered-green-v1` (801 tests) is historical and is not substituted for current 805/805 evidence.
