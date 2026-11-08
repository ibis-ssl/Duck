# Issue #53 通常レビュー

## タスク

- 目的：Issue #53 の実装を受け入れ条件と設計に照らしてレビューする。
- タスク種別：初回通常レビュー。
- 判定対象 HEAD：`eafb5ef9b2ce3a2aaff5af2eb289866f7b4d4ea4`。
- 対象ブランチ：`task/issue53-aspire-006g3`。
- PR：#121、base `task/issue50-aspire-006g`。

## sub-agent の実行コンテキスト

- reviewer：本レビュー用に新規起動する reviewer。
- 実行者：`fa780\donabe`。
- 検証経路：ローカル実行可能。レビュー自体ではテストを追加・実行しない。
- 作業ツリー：起動前に clean であることを確認。

- observed reviewer: `/root/issue53_normal_review_eafb5ef`; fresh reviewer, single reviewer, no implementation participation or delegation in this round. This is not an independent final review.
- observed working directory: `C:\Users\donabe\Documents\Codex\2026-11-07\task\Duck-issue53-aspire-006g3`; machine/user `FA780` / `fa780\donabe`; PowerShell executor. `git` / `gh` reads succeeded; `Get-Command dotnet` resolved `C:\Program Files\dotnet\dotnet.exe`. Classification: `local_execution_available`; SDK/dependency usability was not exercised by this reviewer. Docker/JDK availability was not checked.
- observed source state at start: committed implementation at the requested HEAD; only this parent-created untracked report was present. Allowed write: this report only. No implementation edit, test, Docker operation, commit, push, PR edit/comment, or merge was performed.
- runtime-profile observation: `spawn_succeeded_profile_unverified`; the requested profile is known, but final applied model/reasoning metadata is not observable. The parent-owned Dispatch profile below is preserved verbatim.

## 対象範囲

- Issue #53 の現行要件、承認済み設計、変更差分、関連する既存記録。
- 判定対象は上記 HEAD と base の差分。別 HEAD の過去レビューを本レビューの結果に流用しない。
- #54 の POST_GAME は対象外。Issue #53 が要求していない raw packet capture を追加の必須条件にしない。

## 対象外

- #54 の試合終了・POST_GAME 条件。
- Issue に指定のない raw packet capture の追加要求。
- 実装、テスト、Docker 起動、push、PR 本文やコメントの変更。

## Dispatch profile

<!-- この節は親が管理する。reviewer は変更しない。 -->

- selection inputs: task_kind=review; work_class=judgment_heavy; uncertainty=medium; change_radius=cross_system; criticality=high; repetition=single; decomposability=independent_workstreams。
- selection source: current user instruction and repository `sub-agent-task-manager` policy.
- decomposition policy / disposition: forbidden / prohibited_by_caller_policy; reviewer lifecycle is single-agent.
- requested profile: `gpt-6.1-sol`, reasoning `high`, `fork_turns=none`.
- approval status: user-authorized profile for normal and independent review.
- agent role / default-role plan: runtime default; no explicit role argument exists in the available spawn interface.
- role config evidence / profile effect: no role mapping found in inspected user config; runtime default-role effect on model is unverified.
- planned runtime profile: unknown/null because the effect of the runtime default role could not be inspected; the requested profile was nevertheless passed as `gpt-6.1-sol` with `high` in the actual spawn arguments under the user's explicit instruction.
- applied profile: null until exact runtime evidence exists.
- application status / observability: `spawn_succeeded_profile_unverified`; final applied model/reasoning metadata is not exposed to the parent.
- fork policy: `none`.
- reasons / constraints: preserve user-specified `gpt-6.1-sol high`; do not infer applied profile from request alone.

## 実行コマンド

- コードレビュー。テスト実行は依頼されていないため実施しない。

- Read `.agents/skills/{work-context-manager,review-worker,report-writer,report-output-manager}/SKILL.md` and the pre-created report. The initial read used the enclosing workspace and failed; subsequent reads used the target worktree. Native output encoding was then set to UTF-8 for Japanese text. Missing-path/search errors were not counted as validation successes.
- `git status --short`, `git rev-parse HEAD`, `git remote -v`, `git merge-base origin/task/issue50-aspire-006g eafb5ef`, `git diff --stat/--name-only origin/task/issue50-aspire-006g...eafb5ef`, complete per-file diff and targeted `Get-Content` / `rg` reads. Base/merge-base: `9756f9dd35ce05cf18d6ad91eaf277ad917fcfe9`; range: `9756f9dd35ce05cf18d6ad91eaf277ad917fcfe9..eafb5ef9b2ce3a2aaff5af2eb289866f7b4d4ea4`.
- `gh issue view 53 --repo ibis-ssl/Duck --json title,body,comments`; `gh pr view 121 --repo ibis-ssl/Duck --json title,body,baseRefName,headRefName,headRefOid,statusCheckRollup`. The remote PR head equals the reviewed HEAD. Issue/PR bodies contain pre-existing garbled historical Japanese and stale evidence paragraphs; current local task/design and dated evidence were also read.
- `gh run view 42481939519` and `42483036726 --repo ibis-ssl/Duck --json headSha,status,conclusion,jobs,event,url`; their logs; `gh api repos/ibis-ssl/Duck/actions/runs/42481939519/artifacts`. Existing extracted artifact read from `%TEMP%\issue53-run-42481939519\aspire-full-stack-42481939519-1`; API lists matching artifact `aspire-full-stack-42481939519-1`, ID `13618068793`, unexpired. Extraction was already present; this reviewer did not independently redownload/hash the archive.
- Read artifact `match/participant-status.json`, `match/autoref/*.input-summary.json`, `capture-status.txt`, `container-stop-status.txt`, `match-acceptance.json`, `motion-acceptance.json`, AutoRef inspect/startup and Sumatra startup logs. No archive decoder or validation program was executed in this review.
- Read pinned upstream AutoRef source through `gh api .../contents/<path>?ref=3617de906213971972aa89da6982623b6058caf8`: `config/moduli/autoreferee.xml`, `GameLogRecorder.java`, `SSLVisionCam.java`, `Referee.java`. Native recorder writes parsed vision/referee messages and closes its writer in `stopModule`; upstream config lacks the recorder module added by this PR. Image-to-source correspondence remains based on the recorded upstream build/registry audit, not an embedded source-revision label.

## 対象ファイル

- All 19 changed files were inspected (full PR diff, not only the last commit):
  - `.github/workflows/dotnet-test.yml`;
  - `Testing/Duck.Testing.AppHost/AutoRefGameLogAgent/AutoRefGameLogAgent.java`, `BaseSimulationResourceRegistration.cs`, `Duck.Testing.AppHost.csproj`, `Fixtures/match/autoreferee.xml`, `MatchParticipantStatusProjection.cs`, `Program.cs`;
  - `Tracker/Design/phases-status.md`, `Tracker/Design/tasks-status.md`;
  - `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs`, `MatchParticipantStatusProjectionTests.cs`;
  - `reports/issue53-autoref-native-gamelog-20261108.md`, `issue53-autoref-source-audit-20261108.md`, `issue53-implementation-20261108.md`, `issue53-intake-note-20261108.md`, `issue53-intake-verification-20261108.md`, `issue53-match-42479742584.md`, `issue53-normal-review-cb0bab5.md`;
  - `scripts/build_autoref_game_log_agent.sh`.
- Requirements/design: Issue #53, `tasks-status.md:210`, current `Tracker/Design/Testing/aspire-simulation-test-environment.md`, history MATCH-004 table, dated phases records. Accepted scope: Sumatra 11010 output; AutoRef 10020/11003 input and 11010 output; Duck 11010 non-send evidence with run/count/log attribution. Older history describes AutoRef tracker input; current task and source audit resolve 11010 as output. No extra 11010 receiver or raw capture requirement is imposed.
- Direct dependencies inspected: `TigersTrackerOutputReadiness.cs`, current-target/resource-ID mapping and `ContainerIdentityExecHandoff.cs`, `ServiceReceiveDiagnostics.cs` / snapshot identity contract, `SslLogRecordReader.cs`, `LinuxMatchBindAccess.cs`, `Program.cs` MATCH registration and publisher configuration, `Tracker.Core/Runtime/UdpTrackerPacketPublisher.cs`, `scripts/verify_aspire_match_acceptance.py`, `scripts/sanitize_aspire_artifacts.py`; matching artifact path and CI invocation/build/copy/cleanup flow. Existing publisher returns before UDP send when `PublishUdp=false`.

## 判定

- verdict: **`fail`** — one required medium finding. The technical verdict applies only to `reviewed_implementation_head = eafb5ef9b2ce3a2aaff5af2eb289866f7b4d4ea4`. The newly added self-source detection capability rejects the actual allocated receiver identity. This is not evidence that Duck transmitted, and it does not invalidate the separately retained full source list or AutoRef/Sumatra positive counts.
- findings: **1 required finding**, ordered by severity below. No other required finding was identified. Severity reclassification/errata: none. Initial normal review; finding closure/completeness matrix is not yet applicable. Independent initial/closure HEADs: none.

### I53-NR-EAFB-001 — medium — Use the allocated receiver resource ID for Duck self-source filtering

- origin: this initial normal review of the full Issue #53 PR diff.
- location: `Testing/Duck.Testing.AppHost/MatchParticipantStatusProjection.cs:664` (caller), `:1079` (exact identity predicate); regression fixture `Tracker/Tracker.Tests/MatchParticipantStatusProjectionTests.cs:79` and `:103`.
- description: `ExecuteAsync` passes `duckResource.Name` to `SelectCurrentDecodedSourcesFromAddress` as `expectedResourceId`. That name is `duck`, while the diagnostics identity comes from the current allocated target resource ID. The same production file correctly uses `duckTarget.ResourceId` for positive publisher readiness. The helper demands exact equality, so a correctly identified self-address packet is discarded whenever allocated ID differs from the model name.
- impact: `duckSelfDecodedTrackerSources=[]` cannot distinguish no Duck-address source from an identity mismatch and cannot detect a positive self-source in the deployed DCP naming arrangement. This breaks the new negative-send diagnostic capability used in the #53 evidence narrative. The complete `duckTrackerReceiveSources` remains available and the current retained list independently contains only the two other container IPs; the finding does not assert an actual send or demand raw packet capture.
- evidence: run `42481939519` artifact has resource row `resourceId=duck`, `currentResourceId=duck-wdmzzrcw`, container `57347a693920037c4439da046b3721778bf26c5eded092f555073d874dc05581`; every saved tracker diagnostic has `identity.resourceId=duck-wdmzzrcw`. `duckNetworkAddress=172.18.0.8`; derived self list is empty. `ContainerIdentityExecHandoff` constructs identity with `target.ResourceId`. Static inspection therefore establishes the rejection path; no hypothetical traffic or newly run test is presented as observed evidence. The added helper test supplies the same artificial ID to expected and actual identity and never composes the real caller with distinct model/allocated IDs.
- frequency assessment: the mismatch deterministically affects the derived field in the observed normal DCP naming configuration, including this run. There is no population, usage-rate, or unintended-send incidence dataset from which to assert a monthly occurrence among 1,000 users. The observed evidence supports a present capability defect, not a claimed monthly rate of actual traffic incidents. It is a required correction because that detector is part of the requested acceptance evidence and would silently miss its positive case.
- required action: retain/pass the verified current allocated Duck resource ID alongside the associated diagnostics/container/network identity; represent unavailable or mismatched observation distinctly from a valid empty match. Add a composed regression fixture with model name `duck`, a different allocated resource ID, and a successful Duck-address source, plus wrong-generation/identity rejection. Correct evidence descriptions to distinguish the old derived empty field from the independently inspectable full list; bind focused validation to the corrected HEAD. No raw capture or #54 work is required by this finding.

### Coverage dispositions

| Criterion | Disposition | Evidence / result |
| --- | --- | --- |
| Requirement/design conformance | `checked_finding` | Positive routes supported; self-source capability fails I53-NR-EAFB-001. Accepted output direction followed; #54 excluded. |
| Correctness and edge cases | `checked_finding` | Model versus allocated identity mismatch; sibling positive-readiness path uses actual target ID. |
| Scope and unrelated changes | `checked_no_finding` | MATCH-only native recorder activation/config; diagnostic persistence; no Crane/planner/protocol changes. |
| Every changed file/direct dependencies | `checked_finding` | 19-file inventory above; finding in projection/test composition. |
| API/data/config/workflow compatibility | `checked_finding` | Additive optional JSON fields; recorder asset and mounts match workflow; derived identity defect as above. |
| Error handling/failure diagnostics | `checked_no_finding` | Java startup timeout diagnostics; missing JAR startup guard; archive/sanitization failure outputs; nullglob avoids literal missing-file glob. |
| Security/secrets/cleanup boundaries | `checked_no_finding` | Read-only config/agent mounts, run-specific writable directory; owner/stack/resource/run and pre-existing snapshot checks before stop; sanitizer precedes upload. No secret values copied into this report. |
| Tests/validation adequacy | `checked_finding` | Current CI passes; helper round-trip fixture misses actual allocated-ID caller composition. Reviewer ran no tests. |
| Matching current-HEAD CI | `checked_no_finding` | 42483036726 exact HEAD success, 1,359 passed / 4 skipped; Docker jobs skipped. |
| Live evidence/source/config identity | `checked_no_finding` | 42481939519 explicitly belongs to a881af3; only report content changed through eafb5ef. No claim that it ran the reviewed SHA. |
| Reports/tracking accuracy | `held` | Latest match report distinguishes native counts/observer/raw egress and #54. Historical intake has incorrect send wording; phases and PR retain stale dated state. Parent should reconcile current narrative while preserving history. Derived-list statement needs correction with I53-NR-EAFB-001. |
| Regression/maintainability | `checked_finding` | One identity-composition defect. Java reflective access is tied to the pinned image and reports startup failure explicitly. |
| Independent final review/merge | `not_applicable` | This round stops after normal report; no independent review, publish, or merge. |

### Validation assessment

- Exact current-HEAD CI [42483036726](https://github.com/ibis-ssl/Duck/actions/runs/42483036726): `headSha=eafb5ef9b2ce3a2aaff5af2eb289866f7b4d4ea4`, `pull_request`, completed/success. Test job includes native/agent builds, .NET 1,359 passed / 4 skipped / 0 failed and Linux publish. ASPIRE-NET-002 and full-stack jobs are skipped. These results validate build/tests, not live self-source detection or MATCH-004 acceptance.
- Live Linux MATCH [42481939519](https://github.com/ibis-ssl/Duck/actions/runs/42481939519): `headSha=a881af369678be9f86d8c5fa4cf9a384bfb22c4f`, workflow_dispatch, success. `git diff a881af3..eafb5ef --name-only` contains only `reports/issue53-match-42479742584.md`; executable/config/workflow content is unchanged. This is comparable code evidence, not matching-current-HEAD live execution. Commit/push: implementation committed and remote PR head observed; reviewer performed neither. CI wait: completed existing runs read, no new run requested.
- Full-stack run uses Ubuntu 24.04 runner and pinned AutoRef digest `sha256:96dd8417c1fcf4025ee0a20c8210954068440991989587b68ecdea7d27db6b3d`; run/stack `gha-42481939519-1`. Artifact records full container identities and one DCP network `2f71e8e2286470c814ab25abd1118809dea40257ce9ba18b5a2eaffde1e06929`. Source/build mapping uses the separately documented upstream build audit; no fresh registry/image inspection was run here.
- Native archive summary: 5,250,922 compressed bytes, referee 16,427, vision detection 26,346, tracker 0, `ReachedDecompressedEof=true`, `ReadFailure=null`; startup log says `DUCK_AUTOREF_GAMELOG_STARTED=true`. These are recorder-decoded input records from AutoRef, not all raw datagrams. Tracker zero is not evidence against its output route.
- Publisher resource snapshots match Sumatra IP `172.18.0.5:54467`, UUID `288679d8-e7eb-4ee5-a838-e28979c9e8f7`, 33,427 decoded; AutoRef `172.18.0.6:55649`, UUID `56917106-fc43-4d2e-a4da-455709e0bf1e`, 33,061 decoded; failures zero. The full diagnostic list was fetched slightly later and has Sumatra 33,445. Counts refer to different retained snapshots, not contradictory measurements. Positive readiness requires a count increase under matching run/container/network/receiver epoch; both publisher startup logs identify destination 224.5.23.2:11010.
- Duck IP is `172.18.0.8`; the full saved diagnostic list contains no endpoint with that IP, and MATCH config specifies `Tracker__PublishUdp=false`. This supports the bounded observer/config inference; the broken derived list supplies no additional assurance. Arbitrary UDP egress absence is not claimed or required.
- Archive repair: `stop_exit_code=0`, `container_running=false`, `candidate_count=1`, capture status captured/one log. Cleanup now stops the selected AutoRef before AppHost/DCP removal, compresses run-local `.log`, summarizes every archive, and fails archive_success for missing/failed stop or summary. Sanitization/upload succeeded; failure-only archive step was skipped. Recorder report says finalized after stop; motion archive complete with six active windows. `match-acceptance.json` passed scope is explicitly MATCH-001/002; `fullMatchPassed=false` / MaximumTimeReached belongs to #54 and is not a new #53 failure criterion.
- Prior failed/missing-archive runs and old reviews remain historical evidence only. Their verdicts were not copied to this HEAD. No existing test failure was relabeled as flaky by this review.

## リスク・残件

- Required next action: implementation owner fixes I53-NR-EAFB-001 and supplies matching corrected-HEAD focused evidence; the normal reviewer can then verify the finding and affected sibling cases. No independent final review was started.
- Held H53-EAFB-001 (owner: parent/Issue #53): reconcile stale current-state statements in phases/PR and preserve the dated intake/native-log audit context. The latest match report is append-only historical narrative; its full source list remains useful despite the derived-field bug. This is non-blocking documentation upkeep apart from the required finding's evidence correction. No evidence establishes a user-facing monthly frequency for these stale statements.
- Held H53-EAFB-002 (owner: parent/Issue #53): exact-HEAD live MATCH was not run at eafb5ef, and the prior run differs only by a report. Keep those identities distinct in any acceptance decision. This review does not demand repeated Docker execution solely for that report change. Any implementation fix invalidates affected validation evidence and needs an appropriate fresh route.
- Limitations/unexplored: no test/build/Docker run; no independent native-archive decode, fresh artifact download/hash, binary-to-source reproducibility proof, raw egress capture, or runtime recovery/restart experiment. These were not silently marked passing. No required changed-code area was left unexplored; runtime limitations are explicit above. Actual unintended Duck sending is unobserved and its incidence is unknown.
- Persistence: `normal_persistence`, `repository_file`, persisted path `reports/issue53-normal-review-eafb5ef.md`; committed together with the finding fix and its verification record. `report_attestation_allowed=false`; independent-review administrative attestation is not applicable. This report grants no merge permission. Its technical verdict remains bound to `eafb5ef9b2ce3a2aaff5af2eb289866f7b4d4ea4`.
