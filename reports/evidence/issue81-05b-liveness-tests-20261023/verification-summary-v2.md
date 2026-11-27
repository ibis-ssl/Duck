# 05B1 liveness evidence summary v2

This generation supplements, and does not replace, `manifest-v1.tsv`, raw Red/Green logs, or the metadata companion. It adds the noncooperative I/O test Red and Green records and current source hashes. The summary distinguishes prior runs from the latest code state.

## New noncooperative I/O fixture

- Test file: `Tracker/Tracker.Tests/RuntimeIdentityControlNonCooperativeLivenessTests.cs`, SHA-256 `c8251b75203047b8555d0c649a3d0b4b148e9778e5fc3c4a2535a83671a7c543`.
- Four cases cover synchronous blocking and token-ignoring async `TextReader.ReadAsync`, `TextWriter.WriteLineAsync`, and `FlushAsync`. Each fixture releases its blocked worker after the bounded observation, avoiding a leaked test process.
- `noncooperative-io-red-v1.json`: exit 1 on source `RuntimeIdentityControlProtocol.cs` SHA `b96d255bc7e52fd017bcc9676df994fc5c2331575b1d14cec281d27de7bf67cf`; all four operations exceeded the injected deadline. This is a valid runtime Red, not setup failure.
- `noncooperative-io-green-v1.json`: exit 0, 4/4 on source SHA `44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18f`; bounded waits terminate even when the operation ignores cancellation. This is the currently edited local source, not yet an exact PR CI result.

## Current source hashes

- `RuntimeIdentityControlProtocol.cs`: `44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18f`.
- `Program.cs`: `4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117b`.
- Existing liveness test: `2d136c3d9ad3320c3fcf61fba38e88da56ede44bd309c637b05643b152731745`.
- Existing boundary test: `58c304d7d96c7812a816bd8407c316f1ceb58f22539a1572718446f714ab8993`.
- Existing protocol test: `a9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06`.

## Direct cleanup wording correction

The initial direct-cleanup test's Red measured propagation of the original `SocketException` from `DisposeAsync`; the raw failing assertion stops before separately printing the socket-path state. The prior source ordering awaited the accept task before cleanup, which supports the cleanup risk by inspection, but the Red log alone is not independent proof that the path remained. Green `server-cleanup-green-v1` confirms the corrected contract: the original exception may propagate while cleanup of the owned socket path still occurs. Do not describe the original cleanup case as an exact cleanup-path Red, and do not claim four identical fixed expectations across Red and Green.

## Historical results and limits

`full-unfiltered-green-v1` (801/801, skip 0) and the paired RuntimeHost/AppHost builds belong to an earlier source generation before the new four-case fixture and latest helper change. They are historical evidence, not verification of current source. The newer 4/4 noncooperative green does not replace the full-suite/build verification needed for this source. The stale duplicated test process audit still has process termination confirmed but original tool exit unavailable. No Docker lifecycle, DCP exec, image pull/build/start, or actual runtime handoff was performed; NR005 remains open. The old metadata JSON remains byte-preserved; `metadata-companion-v1.json` is the separate correction record.
