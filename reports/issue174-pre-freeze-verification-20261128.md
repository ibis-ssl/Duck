# 課題174 独立最終レビュー前の検証記録

## 対象と状態

- 対象：Duck Issue #174 / Draft PR #176。
- 公開試行基点：`d5755699924b47c5e6e96846f10dd6abc60b9f59`。
- ローカル技術候補：`2ed8739a2b49ee334bbd0f6a4ae18acff43cab4f`、ブランチ `codex/issue-174-confirmed-proto-preparation`。
- 検証開始時に作業木は clean。`git merge-base` は公開試行基点 `d5755699924b47c5e6e96846f10dd6abc60b9f59` を返した。候補との差分は `Testing/README.md`、`Testing/duck-development-udp-design.md`、`Tracker/Tracker.Tests/FixedInputPreparationTests.cs`、`Tracker/Tracker.Tests/LauncherProcessContractTests.cs`、`scripts/start-duck-simulation.cs` の5ファイル。
- 親から受領した通常再レビュー結果は候補 `2ed8739a2b49ee334bbd0f6a4ae18acff43cab4f` の合格。ここに記すローカル全テストの実行はその後の追加検証で、独立レビューやその予約ではない。

## ローカル標準テスト

Duck に独立した全ローカル等価ゲートのスクリプト / target は定義されていない。標準ローカル入口 `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore` を、検証開始時の候補 HEAD に一度だけ実行した。コマンドは次のとおり。

```bash
DOTNET_CLI_HOME=/workspace/.duck-env/dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 NUGET_PACKAGES=/workspace/.duck-dev/nuget /workspace/.duck-dev/dotnet/dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
```

実行環境は `/workspace/Duck-pr176` の Linux 作業環境、.NET SDK `10.0.404`。ビルド後の結果は終了コード 1、**1713成功・3失敗・4スキップ（合計1720）**。

失敗は次のとおり。

1. `LauncherProcessContractTests.LogWriteFailureTerminatesTheChildAndDescendant` — `AssertProcessExitedAsync` が `ArgumentException` を期待したが例外が出なかった。親から以前の同一基点比較で同じ失敗が再現した証拠を受領しているため、新規回帰とは扱わない。
2. `CraneLaunchOverlayTests.StopAndReapForceKillsGroupThatIgnoresTerminate` — `OwnedChildNotReapedException: Could not reap owned ROS process group`。この作業では基点比較をしておらず、変更との因果は判定しない。
3. `CraneLaunchOverlayTests.StopAndReapTerminatesOnlyTheOwnedProcessGroup` — 同じ `OwnedChildNotReapedException`。この作業では基点比較をしておらず、変更との因果は判定しない。

スキップはLinuxで実行されない2件の native namespace probe と、Windows固有のファイルロック2件。実際の Proto 取得、Docker 統合、Linux packet-flow / full-stack acceptance、Windows 実機検証はこの実行に含まれない。CI の current-HEAD 結果は確認していない。

## 基点との差分と GitHub 上の証拠

候補は公開試行基点からの子孫で、上記5ファイルを変更する。差分記録の更新後は、記録文書と task / phase 状態表だけを加える別の文書差分にする。コード候補の通常再レビュー結果と後続文書差分を混同せず、この文書差分を通常レビュー対象から落とさない。

この記録作成時点で GitHub への書き込み、CI 手動起動、公開、マージはしていない。確認済みの独立レビュー予約や verdict はなく、本記録は独立最終レビュー報告ではない。報告枠を予約せず、独立レビューは開始しない。

## 次の扱い

標準ローカル全テストは失敗であり、全等価確認の成功とは記録しない。既知の1件は既存基点再現として区別し、他2件はこの作業で基点照合していない失敗として保持する。新しい記録・状態表の文書差分を通常レビュー対象として扱った後、レビューと検証の結果を用いて独立レビュー前の候補確定を判断する。固定入力の実取得、Docker・実機、手動 CI、独立レビューの予約・開始、マージは未実施。
