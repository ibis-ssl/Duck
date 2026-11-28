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

実行環境は `/workspace/Duck-pr176` の Linux 作業環境、.NET SDK `10.0.404`。ビルド後の結果は終了コード 1、**1713成功・3失敗・4未実行（合計1720）**。テストランナーの表示は4 `Skipped` で、内訳はLinuxで実行されない2件の native namespace probe とWindows固有のファイルロック2件。

失敗は次のとおり。

1. `LauncherProcessContractTests.LogWriteFailureTerminatesTheChildAndDescendant` — `AssertProcessExitedAsync` が `ArgumentException` を期待したが例外が出なかった。親から受領した同一基点比較記録でも `d5755699924b47c5e6e96846f10dd6abc60b9f59` で同じ失敗が再現している。
2. `CraneLaunchOverlayTests.StopAndReapForceKillsGroupThatIgnoresTerminate` — `OwnedChildNotReapedException: Could not reap owned ROS process group`。今回、Linux 6.18.57 x86_64 / .NET SDK `10.0.404` の同一環境で、公開基点と候補 `2ed8739a2b49ee334bbd0f6a4ae18acff43cab4f` の別々の隔離 checkout を比較した。両方で以下の同一フィルターを一度実行し、同じ例外と `Testing/Duck.Testing.CraneLaunchOverlay/Program.cs:338` の発生箇所を確認した。

```bash
DOTNET_CLI_HOME=/workspace/.duck-env/dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 NUGET_PACKAGES=/workspace/.duck-dev/nuget /workspace/.duck-dev/dotnet/dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~Tracker.Tests.CraneLaunchOverlayTests.StopAndReapForceKillsGroupThatIgnoresTerminate|FullyQualifiedName~Tracker.Tests.CraneLaunchOverlayTests.StopAndReapTerminatesOnlyTheOwnedProcessGroup' --logger 'console;verbosity=minimal'
```

テスト対象・実装ファイルは両 checkout で同じ Git blob (`878fae9ddf1dadd7b819bbb2d1441439d532a0b0`, `25d0d526be40b70545103822c4503a7b5703d19c`) だった。
3. `CraneLaunchOverlayTests.StopAndReapTerminatesOnlyTheOwnedProcessGroup` — 同じ `OwnedChildNotReapedException`。上記と同じ基点比較で、基点・候補の双方に同じ失敗があることを確認した。

この3件は公開基点にも存在するため、今回の候補が導入した新規回帰を示す証拠ではない。特に2件の Crane 失敗の正確な根本原因がOS環境にあるかまでは、この比較だけでは確定しない。標準ローカル試験の結果は **1713成功・3失敗・4未実行** のまま保持する。

実際の Proto 取得、Docker 統合、Linux packet-flow / full-stack acceptance、Windows 実機検証はこの実行に含まれない。

## 基点との差分と GitHub 上の証拠

候補は公開試行基点からの子孫で、上記5ファイルを変更する。差分記録の更新後は、記録文書と task / phase 状態表だけを加える別の文書差分にする。コード候補の通常再レビュー結果と後続文書差分を混同せず、この文書差分を通常レビュー対象から落とさない。

GitHub 読み取りで PR #176 を確認したところ、現在の公開 head は基点 `d5755699924b47c5e6e96846f10dd6abc60b9f59` であり、ローカル候補とは一致しない。GitHub Actions の PR workflow run 一覧と combined status を候補 `2ed8739a2b49ee334bbd0f6a4ae18acff43cab4f` およびこの記録の親 `af4235df6337d07c65d950a2891175d8833571c4` について読み取った結果は、いずれも run 0件・status 0件だった。したがって、今回の候補または記録親に一致する既存CI成功証拠は確認できない。

検証ルートは次のとおり。Duck には独立した全ローカル等価ゲートのスクリプト / target はなく、プロジェクトの通常ローカル入口は上記 `Tracker.Tests` コマンド（`issue-158-independent-review-fixes-20261124.md` §「ローカル全等価ゲートと準備確認」も参照）。CodexSkill `development-orchestrator` は最終候補でリポジトリ定義の full local equivalence gate を一度実行するよう求め、`review-enforcer` §「Pre-freeze gate」は local route でそのゲートを通すことを前提にしている。Skill に失敗時の例外や、local route を remote CI で置き換える経路は記載されていない。remote CI を正式根拠とするのは `remote_ci_only` の場合であり、この作業でその能力区分への変更・承認はない。したがって、この結果のまま独立レビュー前の凍結へ進める正式な例外は確認できず、候補で成功したとは扱わない。現状では独立レビュー開始・報告枠予約は行わない。

この記録作成時点で GitHub への書き込み、CI 手動起動、公開、マージはしていない。確認済みの独立レビュー予約や verdict はなく、本記録は独立最終レビュー報告ではない。

## 次の扱い

標準ローカル全テストは失敗であり、全等価確認の成功とは記録しない。3失敗はいずれも公開基点で同じ失敗を確認したため新規回帰とは認めないが、根本原因が環境依存だと断定する証拠もない。全等価ゲート成功の代替となる承認済みCI・環境・例外は確認されず、CodexSkillのローカル前凍結条件が未達のままなので独立レビューに進めない。新しい記録・状態表の文書差分は通常レビュー対象に含める。固定入力の実取得、Docker・実機、手動 CI、独立レビューの予約・開始、マージは未実施。
