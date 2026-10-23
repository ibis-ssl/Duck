06A2 pure projection evidence (2026-10-23)

Target: fixed Aspire 13.5.4 public ResourceEvent + CustomResourceSnapshot projection only. No DCP/daemon/container/exec operation was performed.

Corrected fixed test source SHA-256: 33493d7830dcf88bc375fc1162cb59848c245cccb24e2f52192c159ee9ba8f94. Final implementation source SHA-256: 3449ae230e73f6dc0315ea4658c9eee22d9e3994613dc85edbcff13e158e1f3d.

TDD chronology: first compile failed because the fixture omitted required ResourceType (setup failure, not functional Red). Next fixture initially named the ContainerResource debug-host while expecting role DebugHost; the resulting two failures were invalid fixture setup and are not counted. Product code was already written before this fixture error was corrected. With the corrected resource Name=DebugHost and event ResourceId=debug-host, a later compileable false-scaffold sensitivity run produced 8 pass / 2 positive-assert failures, exit 1; this reconstructed Red is not evidence of a pre-implementation Red. Restored implementation passed the exact corrected 10-case fixture, then AppHost build and existing AppHostApplicationModelTests passed. This TDD history is an execution gap, not a waived exception.

Accepted inputs: event ResourceId is compared to registeredResourceId; Resource.Name is compared to role; public snapshot state uses ResourceStateSnapshot.Text; only exactly one public property named literal container.id with a 64-character hex string is accepted. Observation and current-state events must agree on resource reference, event key, running state, and full ID. No event ordering or owner/engine authority is claimed. Same-ID process restart requires the separate BootId contract.

Commands/raw output and stdout/stderr hashes are in red-scaffold, green, apphost-build, apphost-regression, and api-reflection subdirectories; each run.json binds cwd, argv, exit, source hashes, and raw log hashes. Initial compile/setup and invalid-fixture outputs were observed in the tool run but not persisted as raw files; their exact classification is retained above.
