# Issue #42 - NORMAL review

## Target and verdict

- Review mode: initial NORMAL review followed by bounded fix verification of I42-NR-001.
- Repository: ibis-ssl/Duck.
- Issue: https://github.com/ibis-ssl/Duck/issues/42
- Draft PR: https://github.com/ibis-ssl/Duck/pull/105
- Branch: `task/issue42-aspire-006e-captureon-same-tick`.
- Base: `003339084d1c07c275cf9653118fff882782b713` (`design/issue18-aspire-test-orchestration`).
- Exact reviewed implementation HEAD: `0cb05eb405a8c1ccce23d53d092a7a827ca41df2`.
- Reviewed range: `003339084d1c07c275cf9653118fff882782b713..0cb05eb405a8c1ccce23d53d092a7a827ca41df2`.
- Initial technical review target: `bdfc36122d6b467c2a31cf0cdb2d56fe94c1b147`; the parent updated the target to `44e0ef03d91a5a6833edea0724ec2644b986ab69` for its intake prose repair, and then to the final HEAD above for the finding fix.
- Verdict: **pass_with_held**. No open required findings. External CI status is held for the parent; no matching run is claimed as passed.
- Open P1 findings: **0**. Open P2 findings: **0**.
- Historical required findings: **1 P2, I42-NR-001, closed at the exact final reviewed HEAD**.

The technical verdict applies only to the exact reviewed implementation HEAD. This NORMAL report is ordinary pre-final-review persistence, not an independent-final-review attestation or approval.

## Reviewer and dispatch profile

- Reviewer identity: `/root/issue42_normal_review`, dedicated to Issue #42 and reused for its fix verification.
- The reviewer did not implement product changes or fixes, run tests, modify GitHub, approve, commit, push, or merge.
- Requested profile supplied by the parent: `gpt-6.1-sol`, reasoning `high`.
- Applied runtime profile: **unknown**; exact model/reasoning metadata was not exposed to this reviewer.
- Runtime profile observability: unverified. No hidden profile or authorization state is inferred.
- Execution: one reviewer; no reviewer subagents or optional review workstreams were created.
- Skills used: review-worker, work-context-manager, report-writer, and repository report-output-manager instructions.

## Accepted contract and scope

For each diagnostics sample tick, use an eligible saved diagnostics-entry alignment before fallback. Both saved selection and fallback must reject a tracker snapshot whose tracked data timestamp exceeds the ibis diagnostics data timestamp. Fallback selects the latest non-own candidate at or before that timestamp, using own candidates only when no non-own candidates exist. Omit the comparison when no eligible candidate exists. The fallback matching rule is `latest-before-timestamp`.

The existing no-future requirement is documented in `Tracker/Design/DebugHost/raw-vision-viewer-plan.md` and the replay test contract in `Tracker/Design/Testing/tracker-comparison-debug-design.md`. No design change was required. Review remained within the reader, affected tests, directly related writer/reader/CLI APIs, and Issue #42 evidence. Optional compatibility/performance review, unrelated cleanup, and live Docker/Aspire acceptance were outside this assignment.

## Covered paths and conclusions

| Path or group | Coverage |
| --- | --- |
| `Tracker/Tracker.DebugHost/Tracking/TrackerSnapshotReplayReader.cs` | Entire production diff and surrounding timestamp extraction, input ordering, record-index resolution, saved selection, fallback, omission, and summary output. |
| `Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs` | Entire test diff and fixture helpers; saved preference, timeline kind distinction, data timestamp basis, future fallback, future saved fallback, source order, and payload summary behavior. |
| `Tracker/Tracker.Tests/CaptureReplayTests.cs` | Changed future-only saved fixture expectation and its directly related capture/metadata/alignment fixture. |
| `Tracker/Tracker.DebugHost/Tracking/TrackerSnapshotAlignmentRecord.cs` | Timeline kinds, normalization, ready state, source key, and saved record-index contract. |
| `Tracker/Tracker.DebugHost/Tracking/TrackerSnapshotAlignmentLogReader.cs` | Schema handling and normalization used by the changed reader. |
| `Tracker/Tracker.DebugHost/Tracking/TrackerSnapshotAlignmentLogWriter.cs` | Diagnostics/render/tracker timeline kinds and line-number propagation explaining the original same-line defect. |
| `Tracker/Tracker.DebugHost/Tracking/TrackerPacketSnapshotLogReader.cs`, `TrackerPacketSnapshotLogWriter.cs` | Input sequence/record indexing and latest-by-source capture behavior relevant to saved resolution. |
| `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs` | Direct CLI formatter consumes comparison summaries and matching rule without requiring a comparison for every diagnostics line. |
| `reports/issue-42-intake-note-20261103074706.md`, `reports/issue-42-red-verification-20261103075131.md` | Scope, target identities, commands, reported Red/Green results, and explicit failed-build/rerun distinction. |
| All changed `reports/evidence/issue42-*` paths in the reviewed range | Command/source-identity records, three Red TRXs, initial/final Green TRXs, source diffs, final full-project run and exit records. |

The ready alignment filter now requires `DiagnosticsEntryTimelineKind`, preventing render/tracker rows that carry a last-known diagnostics line from substituting for that sample tick. Record indices remain assigned before sorting and resolve through the index dictionary.

The saved loop skips own, unresolved, and future entries, then tries the next saved row. Only an eligible saved row sets `savedAlignmentApplied`; otherwise execution reaches fallback. This also verifies the sibling case of a rejected first row followed by another eligible saved row by direct control-flow inspection. No separate new test for that sibling case was run or claimed.

`ReadSession` sorts inputs by tracked timestamp and then receive time. Filtering that sequence preserves the order required by the upper-bound binary search. The search admits equality, returns the last candidate at or before the target, and returns null if every candidate is future. A future-only non-own set causes omission rather than self-comparison, consistent with the accepted candidate policy.

## Finding continuity and closure

### I42-NR-001 - P2 - Future saved alignment bypassed the no-future invariant

- Origin: this NORMAL review at `44e0ef03d91a5a6833edea0724ec2644b986ab69` (same product/test tree as `bdfc361`).
- Original location: `Tracker/Tracker.DebugHost/Tracking/TrackerSnapshotReplayReader.cs:161`, saved-alignment summary branch.
- Description: a ready diagnostics-entry row with a resolvable record index was emitted without checking the resolved snapshot data timestamp against the ibis diagnostics timestamp.
- Impact: a saved future snapshot could bypass the corrected latest-before fallback and introduce future state into a comparison.
- Original supporting evidence: `Tracker/Tracker.Tests/CaptureReplayTests.cs:293` expected `nearestTs=88001000000` for `ibisTs=88000000000`; its saved fixture pointed to that future snapshot.
- Required action: reject future saved input, continue to another eligible saved alignment when available, otherwise use latest-before fallback or omit when no eligible non-own candidate exists; correct the contradictory CLI fixture and provide regression evidence.
- Red evidence: `reports/evidence/issue42-red-futuresaved-20261102T230952Z/issue42-red.trx` records the new single test failing with actual `saved-future`, expected `latest-before`.
- Fix verification: final reader tests `alignedInput.TrackedFrameTimestampNs > ibisDataTimestampNs` before emission and continues within the saved loop. The new focused test selects `latest-before`; the existing future-only CLI fixture now asserts that no `trackerComparison ` line is emitted.
- Closure: **closed** at `0cb05eb405a8c1ccce23d53d092a7a827ca41df2`.
- Severity continuity: **P2 preserved**, no reclassification.

No additional required finding was identified during the bounded fix review or the complete final diff review.

## Validation evidence assessment

The reviewer inspected existing evidence and did not run tests.

| Evidence | Directly observed result |
| --- | --- |
| `issue42-red-20261103-20261102T225737Z/issue42-red.trx` | 7 tests; 6 pass, 1 expected failure: future nearest snapshot selected instead of latest-before. |
| `issue42-red-tick-kind-20261102T230155Z/issue42-red.trx` | 1 expected failure: render-snapshot selected instead of saved diagnostics alignment. |
| `issue42-red-futuresaved-20261102T230952Z/issue42-red.trx` | 1 expected failure: saved-future selected instead of latest-before. |
| `issue42-green-full-20261102T230311Z/` | Earlier implementation evidence: focused 7/7; full 1,157 passed and 2 skips. Historical, not the final fix result. |
| `issue42-final-green-20261102T231209Z/focused/focused.trx` | Final combined reader and CaptureReplay classes: 20 passed, 0 failed. |
| `issue42-final-green-20261102T231209Z/full-project-elevated/full-project.trx` | Final full project: 1,158 passed, 0 failed, 1,160 total; the two skipped namespace acceptance cases are explicitly documented. |
| `issue42-final-green-20261102T231209Z/full-project-elevated/exit.txt`, `run.txt` | Authorized-user rerun recorded `dotnet_test_exit=0`, source fingerprint, command, execution identity, and result counts. |

Final focused command:

`dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerReplayIntegrationTddTests|FullyQualifiedName~CaptureReplayTests" --no-restore -m:1 /nr:false`

Final full-project command:

`dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --logger "trx;LogFileName=full-project.trx" --results-directory C:\Users\donabe\AppData\Local\Temp\issue42-final-full-green-elevated-20261102`

The final source identity is baseline `5647fb65df4b0844e83f3a777adea21624615531` plus the retained diff SHA-256 `35ECD538444D212A0A497374D9E1845F73486AEBDE9493832650E1CF992AAC1E`. The retained diff matches the final reviewed production/test diff from that baseline after the evidence's line-concatenation serialization. The integration test addition is already in that baseline.

The first final full-project attempt failed during build because the non-elevated process was denied coverage-mapping output access. It remains recorded as a failed attempt, not a passing run. The separate authorized-user rerun on the same source fingerprint supplies the successful full-project evidence. No environment change or reviewer test execution is claimed.

## Required coverage dispositions

| Criterion | Disposition | Evidence or limit |
| --- | --- | --- |
| Requirement/design conformance | checked_no_finding | Absolute data-time no-future rule now enforced in saved and fallback branches. |
| Correctness and edge cases | checked_no_finding | Inclusive equality, no eligible candidate, future saved row, later eligible saved row, and preserved index/order inspected. |
| Scope discipline/unrelated changes | checked_no_finding | Whole PR diff inspected; changes remain Issue #42 reader/tests/evidence. |
| Changed files/direct dependencies | checked_no_finding | Paths listed above. |
| API/data/configuration/workflow effects | checked_no_finding | Existing summary shape retained; changed rule string handled by formatter; no config/workflow changes. |
| Error handling/failure diagnostics | checked_no_finding | Invalid/unresolved saved candidates skipped; existing parsing/error behavior inspected; failed build/rerun distinguished. |
| Security/secret handling | not_applicable | No authorization, credential, network boundary, or external execution feature changed. |
| Tests/validation adequacy | checked_no_finding | Three relevant Red failures and source-corresponding final focused/full Green evidence. |
| Current-HEAD external CI evidence | held | Parent owns CI follow-up; reviewer was not given a matching concluded run and claims none. |
| Reports/tracking/documentation accuracy | checked_no_finding | Existing contract, evidence fingerprints, final counts and failed-build history checked; CI/review statements are phase records. |
| Regression/maintainability risk | checked_no_finding | Saved preference, non-own policy, output consumption, ordering and binary search invariant inspected. |

## Held items, risks, and next action

- Held owner: parent `/root`. Confirm external CI for the final published review target as required by the parent workflow. CI has not been represented as success.
- No verdict-blocking unexplored area remains within this NORMAL review scope.
- No remaining product finding or acceptance blocker was identified.
- Live Docker/Aspire replay and the skipped native namespace acceptance cases were not performed here; this report relies on capture-sidecar reader/CLI fixtures for the accepted resolver scope.
- Next action: parent may persist this NORMAL report, then freeze and independently review its intended final implementation target. No approval or merge is authorized by this report.

## Persistence and preservation

- Report type: review_report.
- Persistence mode: normal_persistence / repository_file.
- Reserved report path: `reports/issue42-normal-review-20261103085000.md` (parent-specified path).
- Encoding: UTF-8 with BOM.
- Report-attestation head: null; `report_attestation_allowed: false` for this NORMAL report.
- Only this requested report was written by the reviewer.
- Unrelated report preservation hashes, checked before report persistence:
  - `reports/issue102-comparison-acceptance-implementation-20261028.md`: `9242670A202AA5AA045F51832A8924E3201779E141742E0E99BF8D3E6B591F07`.
  - `reports/issue103-normal-review-20261028.md`: `B72DBB9C63FC4C338BA3311858F08DDDA16C8DD768BD2040835FA09F18E20494`.
