# Duck PR #73 独立レビュー報告（2026-10-19 JST）

## 対象と判定

- 対象: https://github.com/ibis-ssl/Duck/pull/73 （Issue #72 / ASPIRE-006C2）
- レビューモード: 初回独立レビュー（実装・修正は行っていない）
- 実装レビュー対象HEAD: `452d2682cc0fd3f7b5554bd640f84fc4c2ea9d41`
- 比較元: `design/issue18-aspire-test-orchestration`、merge-base `08b765844d73aee7af4e504c5524a10109bf21d5`
- 判定: **要修正（fail）**。差分接続の主要経路と7テストは動作するが、Issue #72の計測・結合試験の受入条件は一部未充足。
- 実装対象HEADにのみ判定を適用する。レビュー報告は独立した `review/pr73-20261019-452d268` ブランチに置き、PRの実装HEADを変更しない。

## レビュー範囲

変更8ファイル（515行追加、18行削除）を全件確認。接続計画、差分評価器、役割解決結果の受け渡し、health endpoint、設定、テスト2ファイル、性能記録を対象とした。直接依存の `TrackerComparisonSourceResolver`、`TrackerComparisonReadiness`、`ExternalTrackerSnapshotStore`、`TrackerDifferenceCalculator`、`Program.cs` も確認した。

| 観点 | 状態 | 根拠・補足 |
|---|---|---|
| 設計・要件適合 | checked_finding | 以下R01〜R03。Issue #72と接続計画に対して計測・負の試験が未充足 |
| 比較経路・順序・同一identity選択 | checked_no_finding | 3役割に解決後、3組を固定順で計算。同一identity複数endpointでは最新受信を選ぶ |
| Robot/Ballの差分、missing/unmatched | checked_no_finding | 差分計算器へ委譲。基本例・Robot欠損・Ball未対応がfocused testで成功 |
| role欠損、identity衝突、frame欠損 | checked_finding | role欠損のみ結合テストあり。frame欠損の3状態・衝突は結合試験なし（R03） |
| APIと設定・互換性 | checked_no_finding | 既存healthレスポンスへPairsを追加。既存の状態項目を保持。新規ゲート既定値0.5m |
| エラー処理・診断 | held | 不正設定値や非有限座標によるHTTPシリアライズ失敗は未再現。現時点で欠陥と断定しない |
| セキュリティ | held | healthに差分情報を返すが、実際の公開境界・認証設定は受入環境未起動のため未判定 |
| テスト・CI・性能 | checked_finding | focused 7成功、全体ビルド成功、指定HEADのCI成功。ただしR01〜R03 |
| 文書・管理台帳 | held | 設計書は英文混在が目立つ。Markdown語彙検査は依存不足で未実施、後述 |
| 変更範囲と既存機能影響 | checked_no_finding | 変更は8ファイル。UIのSplit/Overlay共有配線に変更なし |
| Linux全体受入 | held | 対象外として明示・CI該当jobはskipped。実機の全経路は未検証 |

## 指摘

### R01 / 中（受入条件）— 実際の更新頻度による測定ではない

- 位置: `Tracker/Tracker.Tests/TrackerDifferenceSnapshotEvaluatorTests.cs:144-158`、`reports/ASPIRE-006C2-performance.md`、`Tracker/Design/Testing/aspire-006c2-integration-plan.md:19`。
- 根拠: 測定では `ScenarioCallsPerSecond = 60` を定数として記録している。報告も「60 calls/sは仮定であり実測source cadenceではない」と明記する。実際に受信した更新頻度と測定時間の組は存在しない。
- 影響: Issue #72と設計が求める「代表的な実入力数と呼出頻度」「実測source update cadenceを併記」の受入条件を満たしたとは判定できない。23,226回/秒のベンチマーク処理能力は実際の入力更新頻度の証拠にならない。
- 必要対応: 実際の代表的な入力または再生可能な取得記録から入力件数・更新回数・観測区間を計測し、更新頻度を算出する。仮定の60Hzと実測値は別々に表示し、取得できない場合は未測定のまま受入保留を明示する。

### R02 / 中（計測境界）— 製品のsnapshot取得・複製コストが除外されている

- 位置: `Tracker/Tracker.DebugHost/Tracking/DebugHostHealthEndpoints.cs:18-23`、`Tracker/Tracker.DebugHost/Tracking/ExternalTrackerSnapshotStore.cs:31-42`、`Tracker/Tracker.Tests/TrackerDifferenceSnapshotEvaluatorTests.cs:105-157`。
- 根拠: 実際のhealth経路は `trackerSnapshots.GetSnapshot()` で全保存packetを `Clone()` してから評価器を呼ぶ。性能テストは事前構築した `ExternalTrackerReadSideSnapshot[]` に評価器を繰り返し呼ぶだけで、storeからの読み取り・複製を含まない。
- 影響: 報告値（中央値41µs、p95 48.7µs、33,728 bytes/呼出）は評価器部分の値であり、実際の接続経路全体の時間・割当量を示さない。cloneコストが計測値に反映されていない。
- 必要対応: manager/storeを含む実際の組み立て経路で `GetSnapshot()` から評価器までの時間・割当量を追加測定し、現在の評価器単体値と区別して記録する。UDP受信やHTTPシリアライズを含める範囲は明記する。

### R03 / 中（テスト不足）— 設計で要求した衝突・frame欠損の結合試験がない

- 位置: `Tracker/Design/Testing/aspire-006c2-integration-plan.md:11-14`、`Tracker/Tracker.Tests/TrackerDifferenceSnapshotEvaluatorTests.cs:15-105`、`Tracker/Tracker.Tests/DebugHostHealthEndpointTests.cs:45-82`。
- 根拠: 新規評価器テスト5件は正常3役割、同一identityの最新packet選択、role欠損、Robot/Ball欠損、性能測定である。health側の追加1件も正常3役割のみ。重複する論理役割で別identityが競合する場合、片方・両方の `TrackedFrame` がnullの場合の新規結合試験がない。
- 影響: 新設した `MissingReferenceFrame` / `MissingComparisonFrame` / `MissingBothFrames` の伝播、および競合時に差分を計算しないことを、この接続経路で検証できていない。既存のreadiness単体試験は結合試験の代替にならない。
- 必要対応: 各frame欠損状態とrole/identity衝突について、resolver→readiness→evaluator（必要に応じhealth）までのfocused testを追加する。必要なHTTPステータスの意味も明示する。

## 検証結果と証拠

- リポジトリは `C:\Users\donabe\RemoteDesktopWorkspace\Duck-pr73-review-20261019` に専用複製し、対象実装HEADに固定。`git status --short --branch` は開始時・検証後とも追跡ファイル変更なし。
- 初回ローカルfocused testはclone後のサブモジュール未初期化によりProtobuf生成型の `CS0246` が発生（環境由来）。`git submodule update --init --recursive` で各固定SHAを取得した後に再実行し、**7/7成功、失敗0、スキップ0**。
- 成功したコマンド: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -c Release --filter "FullyQualifiedName~TrackerDifferenceSnapshotEvaluatorTests|FullyQualifiedName~DebugHostHealthEndpointTests" --logger "trx;LogFileName=focused-with-submodules.trx"`。
- 全体検証: `dotnet build Duck.slnx -c Release` 成功。既存依存のMessagePack `NU1902` とAspire `ASPIRE010` 警告あり。製品変更による警告増加の判定は未実施。
- 検証環境: Windows、.NET SDK 10.0.402。Linux Docker受入は未実施。
- ログ場所（PC内、repository外）: `C:\Users\donabe\RemoteDesktopWorkspace\review-artifacts\Duck-pr73-20261019\`。`focused-with-submodules.trx`、`focused-with-submodules.stdout.log`、`focused-with-submodules.stderr.log`、`full-build.stdout.log`、`full-build.stderr.log`、`submodules.*.log`、初回失敗時の `focused.*.log`。
- Markdown語彙検査: 対象文書は `list-markdown-targets.js` に含まれる。ただし `npm run lint:md:whitelist` はPython依存 `yaml` が見つからず終了値2。規則違反の有無は未判定。設計書内の `source update cadence` 等の過度な英語表現は自然な日本語へ見直す余地がある。
- GitHub Actions: `.NET tests`、run `39165797863`（2026-10-18 20:59 UTC開始）、event `pull_request`、`headSha=452d2682cc0fd3f7b5554bd640f84fc4c2ea9d41`、`test` success。`ASPIRE-NET-002 Linux packet flow` と `ASPIRE full-stack Linux acceptance` はskipped。CI runの総合successはLinux受入成功を意味しない。

## 未実施・保留

- UI描画でSplit/Overlayと差分が同一pairを共有する配線（Issue #41）は対象外。未実装を本PRの欠陥と混同しない。
- Linux全体起動と通信の受入、health APIの実HTTPシリアライズ、非有限座標の実測挙動、非正常設定値、他OS上の性能は未検証。
- 既存のMessagePack脆弱性警告は別の依存管理課題として扱う。
- テストが先行して失敗したことを示すTDDのred証跡は確認できていない。現在HEADのgreen結果と区別する。

## 次の対応と引継ぎ

1. R01: 実入力を使った観測頻度の測定を追加し、実測と仮定を分離する。
2. R02: 実際のstore→evaluatorの処理時間・割当量を測定する。
3. R03: 衝突とframe欠損の結合テストを追加する。
4. 修正後は新しい実装HEADのtest/buildおよびそのHEADに一致するCI runを確認し、同じ指摘IDで再レビューする。

判定は指定HEADに限定する。マージ操作は行わない。
