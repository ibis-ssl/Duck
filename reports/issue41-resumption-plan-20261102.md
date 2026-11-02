# Issue #41 再開計画・検証

- 対象: https://github.com/ibis-ssl/Duck/issues/41 (`ASPIRE-006D`)
- 基準: `design/issue18-aspire-test-orchestration` HEAD `0b86a73ac2782603379ce183a250ee8bbf1d9a17`。このHEADにはPR #74の統合コミット `0139a08b675483637083c48367176fdfc44be60b` が含まれる。
- 原条件: 1回のUI render tickで固定した同じsnapshot pairをSplit / Overlay表示と数値差分が共有し、時刻差を別表示する。
- 分割判断: 同じrender snapshotを使う表示・数値の接続が一つの範囲で、約30分粒度に分ける独立作業は見つからないためSub-issueは作らない。

## 現HEADのコード確認

- `Home.razor` は一度採取した `liveDisplaySnapshot` から表示レイヤーと比較状態を作り、時刻差を物体差分表と分けて表示する。
- `VisionLiveDisplaySnapshotProvider` は同じrender snapshotからview stateを作る。
- `VisionLiveComparisonViewState` はfrozen snapshot内で選択したA/Bの数値差分・時刻差とSplit / Overlayの各layerを構成する。
- `TrackerDifferenceSnapshotEvaluator` はHomeの選択ペア経路では使われないが、その経路はfrozen snapshotから直接数値差分を計算している。Issue条件上の不足とは判断しない。
- 指定対象テストにも固定pair、RenderTickId、Split / Overlay、時刻差、およびstore更新後も捕捉済みpairが保持される確認がある。

## 検証

- 実行HEAD: `08c65a923626ff42a9c1ca1eb1bbb866a973dd4d`（計画コミットのみ。製品コードは変更していない）。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~VisionLiveComparisonViewStateTests|FullyQualifiedName~RuntimeHostDebugHostReadSideSnapshotBoundaryTests" -m:1 /nr:false` は21/21成功。現在のPR HEAD `c2596dc88eb273a3df2a146a60f49661acd00997` でも、同じ対象を `--no-build --no-restore` で実行し21/21成功した。
- 最初のsandbox内実行は`obj`の一時ファイル書込み拒否（MSB3491）でテスト開始前のビルド中に停止した。同じコマンドを許可済みPowerShell経路で再実行して成功した。stdout/stderrは `reports/evidence/issue41-focused-test-20261102/` に保存した。
- 統合試験は未実施。統合失敗時のAspire/コンテナ診断ログ保存条件は未発生。
- 旧PR #74のtest/review履歴は今回の結果に流用していない。
- 追加したこのMarkdownに対する `npm run lint:md:text -- --files reports/issue41-resumption-plan-20261102.md` と `npm run lint:md:spell -- --files reports/issue41-resumption-plan-20261102.md` は成功した。

## 次の作業

この後、通常レビューと独立レビューを現PR HEADに対して行う。具体的な不足がレビューで示された場合だけ、Issue原条件に必要な最小修正をテスト先行で追加し、未実装時の失敗と実装を別コミットにする。現時点では実装変更なし。
