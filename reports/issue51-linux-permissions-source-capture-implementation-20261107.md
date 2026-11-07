# Sub-agent's report

## Task

- Purpose: fix the Linux acceptance startup failure for #51 and add direct evidence for the unique UDP 11003 packet source.
- Scope: author regression tests first for the read-only bind mounts and packet-source capture contract; stop before production/workflow implementation until the parent supplies Red evidence.
- Task type: bounded implementation, sequential test-first lifecycle.

## Why the sub-agent is used

- The change spans AppHost host-file permissions, acceptance scripting, workflow wiring, and tests. A single isolated implementation owner keeps those contracts aligned; validation and review remain independently owned.

## Scope

- Inspect the current worktree and the #51 accepted criteria.
- Add focused tests only for (a) Linux container-write access to the Game Controller state store and match output/recorder directories and (b) actual UDP 11003 source tuple capture/uniqueness.
- Do not edit production code, workflow wiring, the existing #51 acceptance report, or unrelated files in this task. Do not run tests or commit/push.
- After the tests are written, return the exact changed files and commands needed for independent Red verification; wait for parent direction before implementation.

## Out of scope

- Docker settings, dependencies, host networking changes, package additions, product behavior, issue closure, PR publication, merge, or any deletion of artifacts/containers.
- Claiming Linux acceptance or 11003 uniqueness before matching evidence exists.

## Dispatch profile

- selection inputs: implementation; bounded technical with cross-system boundary; medium uncertainty; cross-module change radius; high criticality for real-network acceptance evidence; single repetition; sequential dependencies; fresh context.
- selection source: automatic.
- observed decomposability: sequential_dependencies.
- decomposition policy / disposition: forbidden for this sequential test-first work; one implementation owner.
- proposed profile: none.
- approval status / evidence: not required.
- requested profile: gpt-6.1-sol, high reasoning.
- agent role / default-role plan: default role; no explicit role override.
- role config evidence / profile effect: collaboration dispatch exposes model/reasoning only; profile effect treated as unchanged.
- planned runtime profile: gpt-6.1-sol / high.
- applied profile: null until observable.
- application status: pending runtime result.
- runtime profile observability: pending.
- fork policy: none.
- reasons / constraints: cross-layer diagnosis and actual packet evidence require careful scoped changes. No Sol xhigh/max proposal and no Astra proposal.

## Commands

- No test, build, Red, container-start, commit, push, comment, or merge command was run. Only instruction/source reads, read-only Git inspection, and the authorized test/report file writes were performed.
- Independent Red commands below are suggestions only, to run from the exact changed source on a native Linux verifier. Keep stdout, stderr, exit status, and TRX with that verifier's source identity; these are not execution evidence.

```bash
timeout 180s python3 -B -m unittest discover -s scripts/tests -p 'test_match_referee_source_capture.py' -v
timeout 600s dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter 'FullyQualifiedName~Tracker.Tests.MatchLinuxBindPermissionsTests|FullyQualifiedName~Tracker.Tests.RefereeSenderTupleCaptureTests' --logger 'trx;LogFileName=issue51-permissions-source-capture-red.trx' --results-directory /tmp/issue51-permissions-source-capture-red
```

## Files

New untracked regression test files, authored in this phase:

- `Tracker/Tracker.Tests/MatchLinuxBindPermissionsTests.cs`: four target-mount mode cases, three container host-group cases, and one preservation case; Linux only. The tests compose the AppHost model without building/starting its resources. Each composition uses fresh run, stack, lock, and manifest identities.
- `Tracker/Tracker.Tests/RefereeSenderTupleCaptureTests.cs`: one actual UDP round trip test through the existing `UdpRefereeDatagramReceiver`, using three datagrams and two real source sockets. Repeated packets retain their source tuple; different source ports on the same IP remain distinct. This is a new untracked file; no existing tracked tuple test was replaced or duplicated. No new receiver or capture implementation was added.
- `scripts/tests/test_match_referee_source_capture.py`: five verifier tests, including eleven invalid-capture subcases. All Docker inventory/log process boundaries are replaced by test fixtures. Its source capture inputs are synthetic and are not acceptance evidence.

Normal persistence uses this pre-created report, `reports/issue51-linux-permissions-source-capture-implementation-20261107.md`. Only its child-owned Commands, Files, Handoff, Results, and Risks sections were filled; the parent-owned Dispatch profile section is preserved.

## Handoff

- Stop here for independent Red verification. No production/workflow implementation is authorized in this phase.
- Expected assertion-level Red: the current copied Game Controller state store and result/recorder directories lack explicit group write preparation under standard Linux permissions. Engine config is input only: it requires group-read and must not gain group-write. Current composition also lacks the tests' host-GID access contract for the three bind consumers. The tests do not require an explicit --user or any UID change; existing image users were not independently verified.
- Expected assertion-level Red: current `_check_evidence` does not emit `uniqueObservedSenderTupleVerified: true` for the synthetic unique-source capture, and it accepts the configured publisher without checking missing, conflicting, stale, empty, incomplete, malformed, or invalid-receiver-epoch capture evidence. The positive observed-tuple assertion and negative `RuntimeError` assertions expose those gaps without requiring a missing new production API. The fixture models Game Controller host networking with no container-specific IP; it requires process/container attribution to remain unverified.
- The real UDP endpoint extraction and fixture/shared-temp preservation cases are supporting regressions and may already pass. No Red or Green outcome has been observed.
- New source-summary contract proposed by the tests: `referee-source-capture.json`, schema version 1, bound to stack/run and current receiver resource/container/epoch, with observation start/end, completion flag, destination `224.5.23.1:11003`, total observed datagrams, and per-source address/port/count. This proves a unique observed source tuple during that capture window only. Host networking supplies no per-container source IP or publishing-process identity, so `uniqueOnWireSenderAttributionVerified` must remain false for the fixture and cannot be inferred from configured publisher count. The positive report contract uses `uniqueObservedSenderTupleVerified`. Source port is received evidence and need not be 11003; two ports on the same address are two sources. Count totals must reconcile. Preserve the capture artifact on success and conflicting-source failure.
- The bounded safe permission route encoded by the tests is a shared host GID with group read/write on the mutable state-store file, group-read without group-write on the engine configuration input, and group read/write/traverse on the two mutable directories. Primary or supplementary host group membership is accepted without requiring a UID override or changing the images' configured users. World write is rejected; repository fixture contents/modes and shared temp-root mode must remain unchanged. No broad or recursive chmod/chown is required by this test contract.

## Results

- Phase: tests authored; independent Red verification pending.
- Repository: `ibis-ssl/Duck`; issue `#51`; PR `#117`; branch `task/issue51-aspire-006g1`.
- Starting and final technical HEAD: `b2fd92ec7dae7e913c22714920001066e72c789a`; no commit created. Base ref not independently resolved in this bounded phase.
- Execution environment: Windows; PowerShell via `exec_command`; working directory `C:\Users\donabe\RemoteDesktopWorkspace\Duck-issue51-aspire-006g1`; source ownership current task. Shell/file tools are available; actual .NET/Python execution and native Linux capability were not checked. Therefore test execution capability remains unverified for this phase. No Linux validation or matching-current-HEAD CI result was obtained.
- Read before editing: `.agents/skills/work-context-manager/SKILL.md`, `.agents/skills/implementation-executor/SKILL.md`, `.agents/skills/implementation-worker/SKILL.md`, and this reserved report. For persistence, also read `.agents/skills/report-output-manager/SKILL.md` and `.agents/skills/report-writer/SKILL.md`. No root or nested `AGENTS.md` was found in the target repository; the parent had already notified the user that root instructions were absent.
- Target composition inspected: `BaseSimulationResourceRegistration.cs` copies writable `/tmp/state-store.json.stream` and `/tmp/match-engine.json`; `Program.cs` creates and mounts `/match-results` for match-controller and `/data` for ssl-log-recorder. Those targets, copy/directory operations, and current runtime arguments were read before selecting intended Red assertions. Existing `RefereeUdpReceiverFactory` and its regression test pin destination `224.5.23.1:11003`; the endpoint test uses the existing receive adapter.
- Existing production code, workflow files, dependency/environment configuration, tracked tests, and `reports/issue51-match-acceptance-20261107.md` remain untouched. No changes were made to `run-42356804056-artifacts/`, other artifacts, or any containers.
- Commit state: pending/unauthorized in this phase. Push state: not performed/unauthorized. CI wait: not performed. Full local equivalence gate: not started. No independent review verdict is issued by this author.
- Next action: parent dispatches an independent verifier against the exact three untracked test files and unchanged technical HEAD, then supplies Red evidence before implementation resumes.

## Risks

- Supplied historical Linux CI run `42357169977` failed with permission denied on the Game Controller state store and recorder output. That historical result is task context, not verification of these new tests or matching-current-HEAD Green evidence.
- Native Linux is required for permission/identity assertions. The Linux cases explicitly skip elsewhere; a Windows-only run cannot establish this Red or eventual Linux acceptance.
- The real UDP test uses ephemeral loopback sockets and synthetic payloads to test endpoint extraction safely. It does not observe a live Game Controller, join a live multicast group, bind live port 11003, prove production source-summary writing, or establish Linux match acceptance. The source-summary collector and its production wiring still need implementation and independent verification. The host-networked Game Controller fixture deliberately supplies no per-container source address; unique observed tuples cannot establish process/container attribution.
- The source-summary JSON is a test-defined contract rather than an already implemented artifact. The parent should assess this interface during independent Red verification before authorizing implementation. Observed uniqueness is bounded by the recorded capture window; it does not by itself prove sender identity for every packet of a longer recorder run or full-match completion.
- The permission assertions enforce targeted group access and target-specific least privilege. They do not assert configured image UID values or require an explicit --user; the currently built images' user identities were not independently verified in this scope. Existing image-specific UID/GID compatibility and real cross-user host bind writes remain unverified until later implementation and Linux acceptance.
- No test/build/Red command was run. Test compilation, discovery, runtime behavior, and failure diagnostics remain unverified. No remaining tool denial or pending approval blocks this completed authoring phase.
