# Issue #42 - Independent final review

## Identity and verdict

- Report type: independent_final_review_report.
- Repository: ibis-ssl/Duck; Issue #42: https://github.com/ibis-ssl/Duck/issues/42; Draft PR #105: https://github.com/ibis-ssl/Duck/pull/105.
- Branch: `task/issue42-aspire-006e-captureon-same-tick`.
- Base: `003339084d1c07c275cf9653118fff882782b713` (`design/issue18-aspire-test-orchestration`).
- `reviewed_implementation_head`: `3f3bcfe1e189b983800390961de2b352fbe97c1f`.
- Reviewed range: `003339084d1c07c275cf9653118fff882782b713..3f3bcfe1e189b983800390961de2b352fbe97c1f`.
- Verdict: **pass_with_held**. Open P1: **0**. Open P2: **0**. Historical independent finding: **IFR42-001, P2, closed**.
- Held item: exact-HEAD external CI is pending; parent `/root` owns follow-up. No pending or skipped job is represented as passed.

The technical verdict applies to the exact reviewed implementation HEAD above. It is an independent code review conclusion, not approval, merge authorization, or a claim that external acceptance execution completed.

## Reviewer independence and profile

- Reviewer identity: `/root/issue42_independent_review`, retained throughout this Issue #42 review and bounded finding closure.
- This reviewer did not implement the change or fixes and did not serve as the normal reviewer. It did not use Issue #41 reviewer identity or conclusions.
- Initial independent inspection preceded reading the Issue #42 normal review. The normal report was inspected only after the independent technical pass and finding closure, as the final documentation delta.
- Requested profile: `gpt-6.1-sol`, reasoning `high`. Applied runtime profile: **unknown**; exact model/reasoning metadata is not observable. No application claim is inferred from the request.
- Single reviewer execution; no reviewer subagents or optional extra sweep. The related code, fixtures, specification, and evidence were consolidated into this report.
- No product edits, test execution, GitHub mutation, approval, commit, push, merge, or deployment was performed by this reviewer.
- Skills applied: work-context-manager, review-worker, report-writer, and repository review-enforcer/report-output-manager instructions. The parent owns lifecycle orchestration and the eventual attestation commit.

## Accepted contract and bounded scope

For each diagnostics entry, prefer an eligible ready saved alignment for that same diagnostics-entry tick. Reject resolved saved snapshots whose tracked data timestamp exceeds the ibis diagnostics data timestamp. If no eligible saved alignment exists, use the latest non-own tracker snapshot at or before that timestamp. Use own candidates only when there are no non-own snapshots. If no eligible candidate exists, omit the comparison. Fallback reports `latest-before-timestamp`.

The parent clarified during review that the no-future data timestamp rule applies to saved alignment too. IFR42-001 records the resulting required correction. The final review follows that explicit contract; an earlier preliminary no-finding observation was not transferred to the changed implementation.

Authority: Issue #42 acceptance, the parent-supplied resolved scope, `Tracker/Design/DebugHost/raw-vision-viewer-plan.md` (saved alignment, latest-before, and no-future clauses), `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`, and `Tracker/Design/Testing/tracker-comparison-debug-design.md` (CaptureOn/replay and ASPIRE-006E clauses). Existing UI selected-timeline behavior was inspected as related context; this PR changes the capture replay summary reader. Live Docker/Aspire and real three-tracker network acceptance were not performed or claimed.

## Inspected paths and conclusions

| Path or group | Evidence and conclusion |
| --- | --- |
| `Tracker/Tracker.DebugHost/Tracking/TrackerSnapshotReplayReader.cs` | Entire PR diff and surrounding read, sort, record-index, timestamp extraction, alignment, fallback, payload, and summary code. |
| `Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs` | Entire test diff and fixture helpers; saved preference, same-line render-row exclusion, timestamp basis, future fallback, and future saved fallback. |
| `Tracker/Tracker.Tests/CaptureReplayTests.cs` | Changed future-only assertion and its metadata, snapshot, and saved alignment fixture. |
| `Tracker/Tracker.DebugHost/Tracking/TrackerSnapshotAlignmentRecord.cs`, `TrackerSnapshotAlignmentLogReader.cs`, `TrackerSnapshotAlignmentLogWriter.cs` | Schema, normalization, ready status, timeline kinds, line-number propagation, and record-index references. |
| `Tracker/Tracker.DebugHost/Tracking/TrackerPacketSnapshotLogReader.cs`, `TrackerDiagnosticsLogReader.cs` | Snapshot enumeration and physical diagnostics-line parsing used by the changed reader. |
| `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs` | Direct summary consumer; emits the rule and tolerates absent comparison lines. |
| `Tracker/Tracker.DebugHost/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`, `TrackerDiagnosticsReplayTimelineIndex.cs` | Related UI selected-timeline and latest-before paths; checked to distinguish capture-time selection from this data-time summary API. |
| `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs` | Related selected-timeline, missing-source, and future-only regression cases inspected as context. |
| Design paths listed above | Existing replay contract, no-future policy, same source/tick expectations, and legacy path boundaries. |
| `reports/issue-42-intake-note-20261103074706.md`, `reports/issue-42-red-verification-20261103075131.md` | Scope and source identity, Red/Green sequence, final results, failed build, and separate authorized rerun. |
| `reports/issue42-normal-review-20261103085000.md` | Entire final documentation delta; reviewed source identity, preserved P2 finding/closure, evidence counts, and pending CI statements are consistent. |
| All changed `reports/evidence/issue42-*` files in the reviewed range | Commands, manifests, source diff artifacts, three Red TRXs, historical/final Green TRXs, and final run/exit records. |
| `.github/workflows/dotnet-test.yml` | Unchanged PR CI entry point and test job context. |

The ready alignment filter requires `DiagnosticsEntryTimelineKind`. The writer also emits render/tracker rows with a last-known diagnostics line; excluding them prevents a different timeline tick from winning the saved match. Record indices are assigned before snapshot sorting and remain resolvable through the dictionary.

The final saved loop skips own, missing-index, unresolved, and future rows. It continues through subsequent saved rows, so a rejected first row does not hide a later valid saved alignment. Only an eligible row sets the applied flag. Otherwise fallback executes. The guard uses the actual resolved input timestamp rather than trusting duplicated alignment metadata.

`ReadSession` sorts by tracked timestamp and then receive time. Filtering non-own inputs preserves that order. The upper-bound binary search admits equality, returns the last candidate at or before the target, and returns null for an empty/all-future candidate set. The caller omits the comparison in that case. These boundary and sibling conclusions are from direct control-flow inspection; no new reviewer tests are claimed.

The summary record shape and formatter field names are preserved. The changed fallback rule string is deliberate and covered by tests. No schema, configuration, workflow, credential, or network feature changed.

## Independent finding and closure

### IFR42-001 - P2 - Saved alignment could emit future data

- Origin: this independent review, recorded against `44e0ef03d91a5a6833edea0724ec2644b986ab69` after the explicit no-future scope clarification; its product/test tree equaled `bdfc36122d6b467c2a31cf0cdb2d56fe94c1b147`.
- Original location: `Tracker/Tracker.DebugHost/Tracking/TrackerSnapshotReplayReader.cs:161`, saved-alignment emission branch.
- Description: a ready diagnostics-entry alignment with a resolvable record index was emitted without comparing its resolved tracked data timestamp to the ibis diagnostics timestamp.
- Impact: future state bypassed the corrected latest-before fallback and violated the clarified Issue #42 acceptance rule.
- Original evidence: `Tracker/Tracker.Tests/CaptureReplayTests.cs:293` expected snapshot `88,001,000,000` for ibis timestamp `88,000,000,000`. The new Red test independently selected actual `saved-future` instead of expected `latest-before`.
- Required action: reject future saved inputs, continue to any later eligible saved row, then use latest-before fallback or omit if no eligible candidate exists; revise the contradictory future-only fixture and provide regression evidence.
- Red evidence: `reports/evidence/issue42-red-futuresaved-20261102T230952Z/issue42-red.trx`, one expected failure.
- Fix: the saved loop checks `alignedInput.TrackedFrameTimestampNs > ibisDataTimestampNs` before emission and continues when true. Fallback remains inclusive and no-future. The revised CLI fixture requires no `trackerComparison ` line for its future-only source.
- Closure evidence: direct fix inspection at `0cb05eb405a8c1ccce23d53d092a7a827ca41df2`, final focused/full TRXs, and unchanged product/test tree at final reviewed `3f3bcfe1e189b983800390961de2b352fbe97c1f`.
- Disposition: **closed**. Severity: **P2 preserved**. No severity reclassification or erratum.

No additional required finding remains in the bounded final review. The separate normal finding `I42-NR-001` is preserved under that report's own identity; it is not substituted for this independent finding.

## Validation assessment

The reviewer read existing artifacts and performed read-only identity/diff/hash checks. It did not run tests.

| Evidence | Directly observed result |
| --- | --- |
| `reports/evidence/issue42-red-20261103-20261102T225737Z/` | 7 tests, 6 passed, 1 expected failure: `future-nearest` instead of `latest-before`. |
| `reports/evidence/issue42-red-tick-kind-20261102T230155Z/` | 1 expected failure: `render-snapshot` instead of diagnostics `saved-alignment`. |
| `reports/evidence/issue42-red-futuresaved-20261102T230952Z/` | 1 expected failure: `saved-future` instead of `latest-before`. |
| `reports/evidence/issue42-green-full-20261102T230311Z/` | Historical first fix: focused 7/7, full 1,157 passed and 2 skipped. Not substituted for final fix evidence. |
| `reports/evidence/issue42-final-green-20261102T231209Z/focused/focused.trx` | 20 passed, 0 failed, 0 skipped. |
| `reports/evidence/issue42-final-green-20261102T231209Z/full-project-elevated/full-project.trx` | 1,158 passed, 0 failed, 1,160 total; 2 not executed/skipped. |
| Final `full-project-elevated/run.txt`, `exit.txt` | Same source fingerprint, authorized `fa780\donabe` route, full command, and `dotnet_test_exit=0`. |

Final focused command: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerReplayIntegrationTddTests|FullyQualifiedName~CaptureReplayTests" --no-restore -m:1 /nr:false`.

Final full command: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --logger "trx;LogFileName=full-project.trx" --results-directory C:\Users\donabe\AppData\Local\Temp\issue42-final-full-green-elevated-20261102`.

Final source identity is baseline `5647fb65df4b0844e83f3a777adea21624615531` plus retained diff SHA-256 `35ECD538444D212A0A497374D9E1845F73486AEBDE9493832650E1CF992AAC1E`. I recomputed that artifact hash. The artifact concatenates diff lines and is not a byte-identical patch; removing whitespace yields identical diff characters to the final three product/test paths. The replay integration changes are already in the baseline. The final normal-report commit changes no product/test file. The evidence therefore corresponds to the final implementation content without claiming a fresh test invocation at the later documentation commit SHA.

The first final full-project attempt failed during build because the sandbox could not write a coverage-mapping file. That failed attempt remains explicit in the verification report. A separate authorized-user rerun supplies the successful full result and exit record; the failed attempt is not relabeled as passed. Existing analyzer warnings and the two skipped native namespace acceptance cases remain limitations of the recorded run.

`git diff --check` for base through final reviewed HEAD passed during this review. The complete changed-path set is limited to the reader, its two test files, Issue #42 reports, and evidence.

## Required coverage dispositions

| Criterion | Disposition | Evidence or limitation |
| --- | --- | --- |
| Requirement and design conformance | checked_no_finding | Final saved and fallback branches enforce the accepted data-time contract. |
| Correctness and edge cases | checked_finding | IFR42-001 P2 closed; equality, all-future omission, later eligible saved row, ordering/index invariants inspected. |
| Scope discipline and unrelated changes | checked_no_finding | Complete PR changed-path set and all product/test diffs inspected. |
| Changed files and direct dependency impact | checked_no_finding | Covered paths above; CLI handles omitted summaries and the intended rule string. |
| API, data, configuration, workflow, compatibility | checked_no_finding | Summary shape/schema retained; output rule change intentional; no configuration/workflow change. |
| Error handling and failure diagnostics | checked_no_finding | Unresolvable/future saved rows skip safely; no-candidate omission; failed build/rerun distinguished. |
| Security and secret handling | not_applicable | No new auth, secret, network, or execution boundary; unchanged path/payload handling inspected. |
| Tests and validation adequacy | checked_no_finding | Three relevant Red failures, final focused/full Green, and source correspondence. |
| Current-HEAD external CI | held | Exact-head run in progress, owned by parent; not a passing claim. |
| Reports, tracking, documentation accuracy | checked_no_finding | Source identities, finding severity, closure, counts, scope, and phase records checked. |
| Regression and maintainability | checked_no_finding | Saved preference, candidate policy, inclusive search, omission, and formatter consumption checked. |

## Held items and remaining risks

- Exact-head CI observed via `gh pr view 105`: HEAD `3f3bcfe1e189b983800390961de2b352fbe97c1f`; `.NET tests` job `test` was `IN_PROGRESS` in run `41544508791`: https://github.com/ibis-ssl/Duck/actions/runs/41544508791/job/125616675144. `ASPIRE-NET-002 Linux packet flow` was `SKIPPED`. Parent `/root` owns external CI follow-up for the published head.
- Live Docker/Aspire and the two native namespace acceptance cases are not covered by this resolver review. Source clocks still require the accepted data timestamp basis; this change does not calibrate clocks.
- Equal timestamp, empty/all-future fallback, and invalid-first/valid-later saved rows were checked statically; there is no claim of separate new executable coverage for each boundary.
- No verdict-blocking unexplored area or open required product finding remains. No additional optional sweep is requested.

## Persistence and attestation boundary

- Reserved path: `reports/issue42-independent-final-review-20261103085000.md`, supplied by the parent before the final frozen target. It did not exist during the technical review.
- Encoding: UTF-8 with BOM. Persistence mode: `report_attestation_commit`; this report is intended for one administrative attestation commit after the passing review.
- `report_attestation_head`: null at report generation. The attestation SHA must be recorded externally after commit, not invented inside this report.
- `report_attestation_allowed`: true, conditional on the parent validating that exactly one commit follows reviewed HEAD, its first parent is `3f3bcfe1e189b983800390961de2b352fbe97c1f`, and only this reserved report path changes. No executable, design, workflow, configuration, tracking, Skill, feedback, handoff, or other report path may be included.
- Any later repository commit invalidates terminal completion unless the applicable review lifecycle is performed. The technical verdict is not automatically transferred to new implementation content.
- Parent owns the attestation commit, allowlist validation, any authorized publication, and CI follow-up. This reviewer writes only this report and does not merge or approve.
- Unrelated reports `issue102-comparison-acceptance-implementation-20261028.md` and `issue103-normal-review-20261028.md` were preserved, with SHA-256 respectively `9242670A202AA5AA045F51832A8924E3201779E141742E0E99BF8D3E6B591F07` and `B72DBB9C63FC4C338BA3311858F08DDDA16C8DD768BD2040835FA09F18E20494`.
