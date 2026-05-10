# Tracker handover 2026-05-11 05:57 JST

## 目的

この handover は、次のチャットで `/home/ibis/ssl/IbisDuck` の Tracker 作業を同じ前提で再開するための引き継ぎである。

現在の最終目的は、SSL-Vision raw 入力から official tracker packet / tracked viewer までの通常系を安定させ、複数 ball、stale object、表示振動、field geometry 差分、camera 間 robot outlier による表示飛びを潰した状態を維持すること。

## 次チャットにそのまま貼る依頼

```text
$development-orchestrator

/home/ibis/ssl/IbisDuck の Tracker 作業を再開してください。

まず `Tracker/Tracker.Core/Design/tasks-status.md` と `Tracker/Tracker.Core/Design/phases-status.md` を確認してください。現在は TRACKER-031 まで done、次の調査タスクは none のはずです。作業前に live tracking file と git status を必ず確認してください。

ブランチは `feat/tracker-capture-replay-tool`、PR は https://github.com/ibis-ssl/Duck/pull/5 です。最新 pushed commit は `3b02687 fix(tracker): robotの遠方outlier mergeを抑制する` です。

未 staging の既存差分として、`SslProto/src/external/ssl-game-controller`、`Tracker/Tracker.Server/appsettings.json`、`reports/topic-tracker-handover-20260510213445.md` が残っています。これらは作業対象に含める指示がない限り触らないでください。

新しい不具合や改善を進める場合は、development-orchestrator に従い、必要なら task-consistency-manager / task-breakdown-planner で TRACKER-032 以降を追跡ファイルへ先に追加してください。実装前に failing/regression test を作り、テストには「何を確認しているか」の日本語コメントを入れてください。review は sub-agent `gpt-5.5 high` を使い、review report を `reports/` に残してください。
```

## 作業場所と状態

- Repository: `/home/ibis/ssl/IbisDuck`
- Branch: `feat/tracker-capture-replay-tool`
- Remote tracking: `origin/feat/tracker-capture-replay-tool`
- PR: #5 `Tracker capture replay CLI を追加`
- PR URL: `https://github.com/ibis-ssl/Duck/pull/5`
- PR state: `OPEN`
- Draft: `false`
- Latest commit: `3b02687 fix(tracker): robotの遠方outlier mergeを抑制する`
- Current tracking task: `TRACKER-031`
- Current phase state: `done`
- Next investigation task: `none`

## 重要な作業ルール

- この repo では最初に `development-orchestrator` を使う。
- Tracker 作業の live truth は `Tracker/Tracker.Core/Design/tasks-status.md` と `Tracker/Tracker.Core/Design/phases-status.md`。
- 進捗ファイルは最後にまとめて更新しない。作業の実態に合わせて都度同期する。
- 設計または契約の gap を見つけた場合は、実装前に設計・追跡ファイルを更新する。
- テストには、何を確認しているテストかを日本語コメントで書く。
- review は task ごとに dedicated review を回す。ユーザー指定により reviewer は `gpt-5.5 high`。
- review/evidence/handover report は `reports/` に残す。
- evidence 取得だけの sub-agent は使わない。sub-agent は review 中心に使う。
- `Tracker.Server/appsettings.json` など user-owned diff は、明示指示がない限り stage / revert しない。

## 現在の tracking file

`Tracker/Tracker.Core/Design/tasks-status.md`:

- 現在のタスク: `TRACKER-031`
- Title: camera 間の同一 robot ID 遠方 outlier で robot が瞬間移動する問題を修正する
- Status: `done`
- 次の調査タスク: `none`
- TRACKER-031 evidence: `reports/tracker-031-evidence-20260510223916.md`
- TRACKER-031 review: `reports/tracker-031-review-20260510223916.md`

`Tracker/Tracker.Core/Design/phases-status.md`:

- 現在のフェーズ: `done`
- 現在のタスク: `TRACKER-031`
- 残りフェーズ: `none`
- engine / verification / review は done

## 直近の時系列

1. `TRACKER-028`: capture 1680 付近で ball が複数になる問題を解析して修正した。
   - 指定 diagnostics log の trackedFrame 1680 では、raw ball は近接 2 観測だったが、tracked output に stale secondary `#112` が残っていた。
   - 原因は、一度 grown-up した secondary ball が `ObservationCount >= 3` だけで出力され、fresh observation を失っても `TrackedFrame.Balls` に残ることだった。
   - 修正: primary 以外の ball は `HasFreshObservation == true` かつ grown-up の場合だけ出力する。
   - Commit: `bcd790b fix(tracker): stale secondary ballの出力を抑制する`
   - Evidence: `reports/tracker-028-evidence-20260510215726.md`
   - Review: `reports/tracker-028-review-20260510215726.md`
   - Verification: focused 4 passed、capture replay `maxBalls=1`、full `Tracker.Tests` 125 passed。

2. `TRACKER-029`: tracked object の小刻みな振動を抑制した。
   - 原因は Kalman update が数 mm から十数 mm の raw jitter を過剰に信頼し、速度として学習しやすい tuning だったこと。
   - 修正: measurement variance を `(MeasurementNoise / confidence)^2 * MeasurementNoiseVarianceScale` とし、`InitialVelocityVariance` / `KalmanProcessNoiseScale` を調整した。
   - `KalmanInitialVelocityVariance`、`KalmanProcessNoiseScale`、`MeasurementNoiseVarianceScale` を profile 設定として外出しした。
   - Commit: `9efb9c4 fix(tracker): tracked objectの振動を抑制する`
   - Evidence: `reports/tracker-029-evidence-20260510221200.md`
   - Review: `reports/tracker-029-review-20260510221200.md`
   - Verification: focused 7 passed、capture replay `maxBalls=1`、full `Tracker.Tests` 127 passed。

3. `TRACKER-030`: tracked field 表示を raw Vision field geometry と揃えた。
   - ユーザー指摘: defense area などの線が引かれていない、Vision の画面と表示が違う。
   - 原因: tracked view は `TrackerGeometrySnapshot` から `SSL_GeometryFieldSize` を再構成していたが、snapshot に `PenaltyAreaDepth`、`PenaltyAreaWidth`、`CenterCircleRadius`、generated `FieldLines` / `FieldArcs` がなかった。
   - 修正: `TrackerGeometrySnapshot` に defense / center circle 寸法と line / arc snapshot を追加し、tracked view / diagnostics replay で復元するようにした。
   - Commit: `cf8ab64 fix(tracker): tracked field geometryを保持する`
   - Evidence: `reports/tracker-030-evidence-20260510222529.md`
   - Review: `reports/tracker-030-review-20260510222529.md`
   - Verification: focused 2 passed、full `Tracker.Tests` 127 passed。

4. `TRACKER-031`: yellow robot 1 が瞬間移動する問題を解析して修正した。
   - ユーザー指定時刻: `2026-05-10 22:33:16.070 JST`
   - 対象 log: `Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260510T133304710Z-161dd8def1fd47d788383113fa8b6203.tracker-diagnostics.log`
   - 対応する UTC log time: `2026-05-10T13:33:16.0703052+00:00`
   - 原因: 同一 frame の raw yellow robots に `Y1` が 2 件あり、遠方誤 ID 観測 `x=-5275.8,y=-2255.7` と正常観測 `x=5161.2,y=-1997.1` が同じ `team + robot id` として world merge された。
   - tracked Y1 は `13:33:15.975` に `x=5160.5,y=-1999.5`、`13:33:16.070` に `x=4321.6,y=-2019.5`、`13:33:16.102` に `x=5160.4,y=-1998.8` と一瞬だけ引っ張られた。
   - 修正: 同じ `team + robot id` に複数 camera 観測がある場合、既存 camera-local track の予測位置に近い観測を anchor とし、anchor から movement gate より遠い別 camera 観測を far outlier として camera-local update から除外する。
   - 単一観測しかない場合、または全 camera 観測が既存 track から遠い場合は従来どおり reset を許す。
   - Commit: `3b02687 fix(tracker): robotの遠方outlier mergeを抑制する`
   - Evidence: `reports/tracker-031-evidence-20260510223916.md`
   - Review: `reports/tracker-031-review-20260510223916.md`
   - Verification: focused 4 passed、target capture replay `maxRobots=22` / `max-robots<=22` OK、full `Tracker.Tests` 128 passed。

## 確定事項

- `TRACKER-028` から `TRACKER-031` は実装、検証、review、commit、push 済み。
- `TRACKER-031` の review では code blocker はなかった。
- review で指摘された `tasks-status.md` の current task 不整合は修正済み。
- `Tracker/Tracker.Core/Design/tasks-status.md` と `phases-status.md` は TRACKER-031 done / next none に同期済み。
- PR #5 は open で draft ではない。
- 最新 commit `3b02687` は remote branch へ push 済み。

## 未解決または注意が必要なこと

- 次の Tracker task は現時点で未設定。新しい現象や改善要求が来たら `TRACKER-032` 以降として tracking file に先に追加する。
- worktree には以下の未 staging 差分が残っている。明示指示なしに stage / revert しない。
  - `SslProto/src/external/ssl-game-controller`: submodule dirty
  - `Tracker/Tracker.Server/appsettings.json`: `Tracker.SourceName` / `Tracker.Uuid` が `ibisduck-tracker` から `ibis` に変更されている user diff
  - `reports/topic-tracker-handover-20260510213445.md`: untracked legacy handover report
- `TRACKER-029` の振動抑制は stationary jitter を抑える方向の tuning なので、短時間の急加速に対する初期追従は以前より少し丸くなる可能性がある。
- `TRACKER-030` の geometry snapshot は描画に必要な generated line / arc 情報を保持するが、SSL-Vision proto に未知 field が増えた場合は追加対応が必要。
- `TRACKER-031` は既存 track に近い camera 観測を anchor として優先する。複数 camera が大きく矛盾する特殊ケースでは、この設計判断を再評価する余地がある。

## 主要ファイル

- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Core/TrackerModelContracts.cs`
- `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- `Tracker/Tracker.Server/Tracking/TrackerOptions.cs`
- `Tracker/Tracker.Server/Tracking/TrackerConfigurationResolver.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `Tracker/Tracker.Server/Components/Vision/TrackedVisionViewState.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`

## 検証コマンドの型

dotnet は repo-local cache を使う。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false
```

capture replay の例。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260510T133304710Z-161dd8def1fd47d788383113fa8b6203.jsonl.gz \
  --settings Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260510T133304710Z-161dd8def1fd47d788383113fa8b6203.metadata.json \
  --profile sim \
  --expect max-robots\<=22 \
  --max-details 10
```

## 次に作業する場合の判断

- 新しい diagnostics log が提示されたら、まず raw detection と tracked output の同時刻比較から原因を切り分ける。
- 既存 task 完了後の新規不具合なら、`TRACKER-032` として `tasks-status.md` / `phases-status.md` に追加してから実装する。
- UI / field 表示差分なら `Tracker.Server` の view-state と `VisionFieldLines` の入力 geometry を確認する。
- tracking engine の挙動なら `TrackerExecutionContracts.cs` と `TrackerEngineTemporalContractTests.cs` に regression を追加してから修正する。
- review 前に full test と task-specific focused test、必要なら capture replay を report に記録する。
