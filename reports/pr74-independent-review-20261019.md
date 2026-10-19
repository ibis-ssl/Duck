# PR #74 独立レビュー報告

- 実施日: 2026-10-19 (JST)
- 対象: https://github.com/ibis-ssl/Duck/pull/74
- 固定 HEAD: `845b08b4ad9ae057213640de31946e7627a2de06`
- 基準ブランチ: `design/issue18-aspire-test-orchestration`
- 範囲: 変更8ファイル、追加639行・削除18行。コード、テスト、Markdown、対応するCIを独立確認。
- 判定: **要修正（P2 2件）**。今回のレビューではP0/P1に相当する確定事項は見つかっていない。

## 指摘1 [P2] 数値差分のボール距離ゲートが既存設定に従わない

- 対象: `Tracker/Tracker.DebugHost/Components/Pages/Home.razor:700`。
- 新規画面経路は `Tracker:Comparison:BallDistanceGateMeters` を読み込む。
- 一方、実際の `Tracker/Tracker.DebugHost/appsettings.json:2-4` および `Tracker/Tracker.DebugHost/Tracking/DebugHostHealthEndpoints.cs:23` は `TrackerComparison:BallDistanceGateMeters` を使用する。`Tracker/Tracker.Tests/DebugHostHealthEndpointTests.cs:62` も後者で設定している。
- そのため既存設定で距離ゲートを 5 m に変更しても、ライブ画面側は指定されない別キーの既定値 0.5 m を使用する。2球が 1 m 離れる場合、比較APIと画面とで対応付け結果が一致しない。
- 対応: 既存の設定キーを共通利用し、既定値以外の値で実際の `CreateComparisonViewState` 経路を通す回帰テストを追加する。

## 指摘2 [P2] 選択中の外部 tracker が消えると未受信ではなくRawへ切り替わる

- 対象: `Tracker/Tracker.DebugHost/Vision/VisionLiveDisplaySnapshotProvider.cs:72-90`、`Tracker/Tracker.DebugHost/Vision/VisionLiveComparisonViewState.cs:267-279`。
- 例: Layer B に TIGERs を選択した状態で TIGERs だけのスナップショットが一覧から消え、ER-FORCE は残る。
- source options は消えた TIGERs のキーを保持しないため、`FindSourceOption` は `options.First()` すなわち Raw Aggregate を返す。新規 `TrackerDifference` は `MissingComparisonFrame` ではなく `UnsupportedSource` を返し、画面のレイヤーも意図せずRawへ変わる。
- 同問題の基となる汎用selectorのフォールバックは既存コードだが、今回追加した数値差分の欠落判定がその実経路を扱えていない。新規テストは `composer.CreateViewState(...)` から手動で欠落源を選択しており、実際の `LiveDisplaySnapshotProvider.CreateComparisonViewState` を通した選択継続を確認しない。
- 対応: 欠落した選択キーを未受信オプションとして保持するか、選択内容を変更しない状態モデルにし、一方のみ受信停止・復帰する回帰テストを追加する。

## 確認できた実装

- `VisionLiveDisplaySnapshotProvider.CaptureRenderTickSnapshot` がraw/tracked/externalのread-sideを採取し、`VisionLiveComparisonSnapshotComposer` が同じ `RenderTickId` のスナップショットを作る。
- `Home.razor` のSplit/Overlayは比較スナップショット由来のレイヤーを利用し、数値差分も同じ `comparisonViewState` の選択ペアから計算する。
- Duck内部 `TrackerFrame` は新規 `TrackerDifferenceCalculator` overloadで公式proto相当の単位・並びに変換され、外部 `TrackerWrapperPacket` は採取時に `Clone()` される。
- 新規テストで採取後のDuck/TIGERs/ER-FORCEの最新フレーム更新に対し、元フレーム番号、時刻、ロボットX・ボールXの差分が固定されることを確認。テストが持つ通常のフレーム差し替え操作を対象とする。

## 検証結果

- CI: https://github.com/ibis-ssl/Duck/actions/runs/39195095790 は対象HEADとrunのhead SHAが完全一致。`test` 成功。`ASPIRE-NET-002 Linux packet flow` と `ASPIRE full-stack Linux acceptance` はskipであり成功実行と解釈しない。
- 独立テスト: .NET SDK `10.0.402`、Windows。固定SHAの別クローンで必要submoduleを初期化後、`dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj -c Release --filter "FullyQualifiedName~VisionLiveComparisonViewStateTests|FullyQualifiedName~TrackerDifferenceCalculatorTests"` を実行。**23件成功、失敗0件、skip0件**。TRXは作業ディレクトリの `Duck-pr74-test-results/pr74-focused-after-submodules.trx`。
- 最初の試行は独立クローンのsubmodule未初期化でproto型解決エラー（CS0246）。初期化後の再実行で成功しており、PRの実装不具合と判断しない。
- 失敗診断artifact: `.github/workflows/dotnet-test.yml` にテスト結果・標準出力・標準エラー・関連ログの保存手順が既存。workflow変更は不要。
- Markdown: 変更文書 `reports/issue41-aspire-006d-plan-20261019.md` に固定版 `yomiyasu_lint.py --json` を実行。**95/100、警告1件** (`excess_list`、箇条書き8/14行＝57.1%)。report類は正式なMarkdown lint対象外。

## 継続課題・制限

- `Tracker/Design/Testing/tracker-comparison-debug-design.md:115` の『時刻差が大きい場合は設定閾値で警告する』機能は今回未実装。作業担当も認めている。今後のIssueに紐づくかは未確認であり、実装または明確な追跡が必要。
- ライブのUDP入力を伴うブラウザ確認、Linux full-stack受入試験は独立に実施していない。数値表示の実画面一致まで断定しない。
- 本レビューは対象PRのソースに変更を加えない。レビュー報告は別ブランチに保存し、元PRのHEADとCIの対応を保持する。
