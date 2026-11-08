# Issue #54 実装レポート（作業中）

## 対象

- Issue #54 / ASPIRE-006G4。base `task/issue50-aspire-006g`。
- 実装開始 HEAD: `c9f693e5d32da89e8a986609016bdd088f110dbd`。
- 受入条件と既存 artifact の確認: `reports/issue54-intake-20261108.md`。

## Dispatch profile

<!-- Parent-owned. Implementer must leave this section unchanged. -->

- selection inputs: task_kind=implementation; work_class=bounded_technical; uncertainty=medium; change_radius=cross_module; criticality=ordinary; repetition=single; context_need=bounded_history.
- selection source: `codex-delegation-executor` and `sub-agent-task-manager`.
- observed decomposability: sequential_dependencies; decomposition policy=allowed; no parallel split.
- requested profile: `gpt-6-luna` / `high` (existing task-family instruction).
- approval status/evidence: no gated profile proposed.
- agent role/default-role plan: default implementation role; explicit role override unavailable in spawn interface.
- role config evidence / profile effect: spawn tool exposes requested model and reasoning; final runtime application remains unobservable unless returned by runtime.
- planned runtime profile: `gpt-6-luna` / `high`.
- applied profile: pending; record exact application only when observable.
- application status: pending.
- runtime profile observability: pending.
- fork policy: full history.
- reasons / constraints: preserve Crane/planner/protocol behavior; no new Python implementation or local Python execution; no Docker run for implementation worker; do not mark MaximumTimeReached as passed; implementer inspects own complete diff and reports all findings/limitations.

## 実装目的

ASPIRE-MATCH-005 の試合終了・結果・SSL記録・Crane recording data、および全起動対象の標準出力／標準エラーを、失敗時にも同一 run artifact に保存する。既存 resource が teardown で消える前に証拠を回収する。

## 実装者の報告

<!-- Implementer fills the following sections only; preserve this heading and parent-owned profile. -->

### 変更したファイルと理由

- `.github/workflows/dotnet-test.yml`: run-owned resource logs, match result files, SSL recorder finalization, and Crane rosbag evidence are retained in the acceptance artifact before AppHost/DCP teardown can remove containers. Per-resource collection status records captured, error, or missing state.
- The manual `workflow_dispatch` match path now requires `fullMatchPassed=true`, `stageReached=post_game`, and `terminationKind=PostGame`. The gate runs before the always-conditioned artifact upload so failed-match evidence remains available.
- For manual `workflow_dispatch` with `mode=match` only, the MatchController maximum duration is 15 minutes and its workflow verifier wait is 20 minutes. Run 42488259296 reached `NORMAL_SECOND_HALF` when the previous five-minute maximum expired; the new limit leaves time for both halves plus a five-minute result/finalization window. The existing PR readiness path keeps its five-minute MatchController default and 15-minute verifier wait. A maximum-time outcome remains a failure.

### 実装上の判断

- Container selection still requires the existing `duck.aspire.owner=duck-apphost`, current stack ID, pre-run container snapshot, and match run label. Logs are captured with `docker logs --timestamps` before sending SIGINT to AppHost; Docker provides combined stdout/stderr, which is retained under `*.stdout-stderr.log`. The existing sanitizer is applied before artifact writes.
- After AppHost shutdown, any owned containers that remain are re-captured and their full sanitized logs replace the pre-shutdown snapshot to include terminal output. The early captured-ID list is preserved; cleanup appends newly discovered owned IDs only when absent. Each ID receives a `post-shutdown-capture-status.tsv` row for recaptured, removed-before-capture, present-but-not-recaptured, or inventory-error state.
- A status row is written for each discovered container and each expected resource without a captured container. Match capture accounts for all nine launched resources: simulator, Game Controller, `cm4-sim`, Crane, Duck, TIGERs, AutoRef, SSL recorder, and MatchController; the participant readiness set remains the existing seven. Container IDs are included in log filenames to avoid collisions. Match participant status, match result, and referee source capture are copied both before and after AppHost shutdown to preserve whichever terminal files exist.
- At cleanup, the owned Crane container's `ros2 bag record` process receives SIGINT so rosbag2 can close its files. The existing Crane output is discovered by searching `/root` for `metadata.yaml` (depth capped at 8); each bag directory is copied, archived as `.tar.gz`, gzip-checked, and SHA-256 hashed. Discovery, missing-container, missing-bag, and copy/archive failures are retained as explicit status.
- No Crane launch command, planner, packet protocol, or Python implementation was changed. Existing artifact sanitization remains in place; rosbag binary payloads are packaged as `.tar.gz` so the text sanitizer does not rewrite them.

### 自己 diff 点検


- Inspected the complete workflow diff. The pre-shutdown snapshot is necessary because the prior post-shutdown collection found no containers after DCP teardown. The cleanup loop no longer truncates the early captured-ID list; it deduplicates IDs discovered later, then reports each retained ID as recaptured, already removed, still present but not recaptured due to an error/ownership mismatch, or indeterminate due to an inventory error. The full-match gate is limited to manual `mode=match` and precedes artifact upload, whose `always()` condition permits upload after the gate fails.
- Resource logs are combined stdout/stderr from Docker. If a resource was already removed before cleanup, inventory is incomplete, or the Docker log read fails, the status file preserves that limitation instead of implying capture succeeded.
- Crane recording discovery is based on `metadata.yaml` files under `/root`; the current run's exact bag path was not available for live confirmation. The archive/copy path must be confirmed by the next full-stack run.
- No independent review verdict is issued.

### 実行したコマンドと結果

- `git diff --check` — passed with no whitespace errors.
- Extracted the AppHost start, readiness, cleanup, and manual full-match shell blocks and ran `bash.exe -n` on each — passed (61, 176, 506, and 17 lines respectively). This checks shell syntax only; the workflow was not executed.
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~Tracker.Tests.MatchControllerWorkerTests` — returned no output or test summary; no test result is claimed.
- `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -v:minimal` — failed with `NETSDK1004`: `Tracker.Tests/obj/project.assets.json` is absent. No package restore was performed.
- No Docker/full-stack run or local Python command was performed. Final technical HEAD remains `27b7b83940ddff18c62920315ee18df2d8201c69`; changes are uncommitted and unpushed.

### 未実施・失敗・取得できなかった証拠

- No live validation of current-run Crane rosbag paths, Docker log collection timing, SSL archive finalization, or the `POST_GAME` gate was possible without a full Docker acceptance run. Local .NET tests could not be validated because restored assets are absent.
- The existing run 42488259296 remains failure evidence: `MaximumTimeReached` at the five-minute harness limit, no `POST_GAME`, complete per-resource stdout/stderr logs absent, and Crane rosbag absent. Its SSL archive and eight readiness-time startup-log tails do not satisfy full-match acceptance.

### リスクと次の操作


- Run the existing workflow manually with `mode=match` and confirm `POST_GAME`, `fullMatchPassed=true`, the SSL archive, all owned resource logs, and Crane rosbag archive in the uploaded artifact. Missing or failed captures remain explicit and should not be treated as acceptance.
- The 15-minute harness limit is evidence-based headroom rather than a guarantee of match duration; if the match does not reach `POST_GAME`, the gate fails and the observed result stays a failure.
- Parent integration/review, commit, push, and PR actions remain pending. No commit, push, issue/PR edit, or merge was performed.

