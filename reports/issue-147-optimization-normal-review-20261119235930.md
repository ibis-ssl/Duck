# 課題147 最適化の通常レビュー

## タスク

- 目的: PR149の近接重複判定と固定した回帰試験を独立して通常レビューする。
- タスク種別: initial review。対象76908b12b09f845c7a4df52753723b6e43208c36、暫定base90f934b50b6bf0b17b92e79a9352eb2441ac1b05。

## sub-agentを使う理由

- 理由: 実装担当と親をレビュー担当の代用にしない。

## 対象範囲

- 対象: private近接重複判定、全呼出元、境界・順序・割り当て試験、実装前後の証拠と計画。先行PR確定後の取り込み差分と実測は同じ担当が追加確認する。

## 対象外

- 対象外: 製品修正、全体検証、正式再生、先行ツールの再レビュー、Git/Issue/PR操作。

## Dispatch profile

- selection inputs: review / judgment_heavy / uncertainty medium / local / ordinary / single。
- selection source: review-enforcer、sub-agent-task-manager、本人指定6.2 Sol High。
- observed decomposability: independent_workstreams。
- decomposition policy / disposition: forbidden / prohibited_by_review_lifecycle。single_agent。
- requested profile: gpt-6.2-sol / high / fork none。
- agent role / default-role plan: runtime default role。確認した構成にagentsによるprofile変更なし。
- planned runtime profile: gpt-6.2-sol / high。
- applied profile: null。
- application status: spawn_succeeded_profile_unverified。通常担当 /root/issue147_optimization_review の最終profileは観測できていない。
- runtime profile observability: final_profile_hidden。
- reviewer continuity: この担当が通常の指摘と修正確認を継続する。
- fork policy: none。
- reasons / constraints: 別作業木で先行PRの通常レビューと並行し、負荷計測は行わない。FA780の既知承認経路のみ、MCP/RDMCP/WSL禁止。

## 実行コマンド

レビュー担当は `/root/issue147_optimization_review`。実装担当 `/root/issue147_optimization_impl` および親とは別の担当として実施した。`review-enforcer`、`review-worker`、`work-context-manager`、ソース配置・文書化規約、`report-writer`、`report-output-manager` を確認した。

| コマンドと対象 | 終了値 | 確認結果 |
| --- | ---: | --- |
| FA780 の承認経路で `whoami`、`git --no-optional-locks rev-parse --show-toplevel HEAD`、`git symbolic-ref --short HEAD` | 0 | `fa780\donabe`、`task-23/Duck-opt`、`perf/issue147-near-duplicate`、`76908b12b09f845c7a4df52753723b6e43208c36` を確認。 |
| `git diff --name-status`、`git diff --check`、`git diff`、`git log` で暫定 base `90f934b50b6bf0b17b92e79a9352eb2441ac1b05` と対象 HEAD を比較 | 0 | 2 コミット、4 ファイル。差分の空白エラーなし。 |
| `git grep`、`git show`、`git ls-tree` で呼び出し経路、定数、距離式、既存試験、プロジェクト定義、設定とワークフローを確認 | 0 | 製品呼び出し箇所は `AddRobotObservations` 内の一箇所。黄・青両チームで同じ経路を通る。 |
| `git hash-object --no-filters` と `git rev-parse <head>:<path>`、`git diff --exit-code`、PowerShell の `Get-FileHash` と `SHA256.HashData` | 0 | 作業木上の対象と Git blob が一致。開始コミット `cfd684787960581a7eeb87944b626b7b12310de5` は暫定 base と製品・試験が同一。メモリ上で変更メソッドと XML 説明を逆置換して開始版の SHA-256 と一致を確認。 |
| PowerShell の `Get-Content`、`Compare-Object`、`Get-FileHash` で Red/Green の TRX、終了値、標準出力、ソース記録を照合 | 0 | 31 件の試験名と両試験ファイルの指紋が一致。Red の失敗は割り当て試験の 1 件。 |
| 厳格な UTF-8 デコード、先頭バイト、CR と末尾 LF、Markdown の見出しとコードフェンスの確認 | 0 | 対象 C# と Markdown は BOM なし UTF-8、LF。 |
| `node tools/lint/scripts/list-markdown-targets.js --files` に対象の計画・実装報告と本報告を指定 | 0 | 対象 0 件。`tools/lint/README.md` の `reports/**` 除外と一致。 |

最初の Git blob 照合は PowerShell の参照文字列の組み立てを誤って終了値 1 だった。参照を単一の引数に修正した再照合で上記の一致を確認した。初回の行番号検索は Windows のワイルドカードの指定を修正して再取得した。

実装担当の局所試験は FA780 上の PowerShell 7.6.10 と .NET SDK 10.0.404 で、`dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -c Release --filter 'FullyQualifiedName~TrackerEngineCloseRobotDuplicateContractTests|FullyQualifiedName~TrackerEngineRobotTrackingContractTests'` を実行した。完全な指定は実装報告にある。下表は本レビューで読み戻した実行記録。

| 実装担当の Release 試験 | 終了値 | 成功 / 失敗 | 割り当て 20,000 呼び出し | ローカル証拠 |
| --- | ---: | ---: | ---: | --- |
| Red | 1 | 30 / 1 | 3,040,000 バイト | `task-23/validation/issue147-red/issue147-robot-contracts-red.trx`、`source.txt`、`exit-code.txt`、`test-only.diff` |
| Green | 0 | 31 / 0 | 0 バイト | `task-23/validation/issue147-green/issue147-robot-contracts-green.trx`、`source.txt`、`exit-code.txt` |

両ディレクトリに標準出力・標準エラー、VSTest 診断と MSBuild バイナリログがある。両ビルドの `ASPIRE010` は局所試験の実行を妨げていない。Red の製品 SHA-256 は `846752e37c6c9b44f4e0c15fe15f2515a7598cee126eddd0b8bf17741e91cbc8`、Green は `c9860a2438c971b86f84c8e4ef3446964c1415f51e1320c90c4c1457c2eb0840`。両者の追加試験 SHA-256 は `8def14819e03ec0f03eacb485f7a5189242b6cd2642809d000fba1a3432f4b0f`、既存試験 SHA-256 は `4c7e9da0ad6acefdcf22ea161e3ec9a6a47a990cf2c9cd341460c7c49ed06c85`。Green が記録した Red TRX と Red 試験差分の指紋も実ファイルに一致した。

## 対象ファイル

- `Tracker/Tracker.Core/Engine/TrackerEngine/RobotTracking.cs`。該当箇所は 255–277 行。呼び出しと更新順は 47–80、238–253、279–294 行。`FrameCommit.cs` のコミット経路、`Settings.cs` の平面距離、`TrackerEngine.cs` の 90 mm × 1.5 の定数を確認。
- `Tracker/Tracker.Tests/Contracts/TrackerEngineCloseRobotDuplicateContractTests.cs`。公開 `Update` のケース、辞書への挿入順を逆にした private 判定、割り当て試験を確認。直接依存する `TrackerContractFixture.cs`、`TrackerContractTestData.cs` と既存 `TrackerEngineRobotTrackingContractTests.cs` を確認。
- `reports/issue147-optimization-plan-20261119.md` と `reports/issue147-optimization-implementation-20261119.md`。Issue #147・PR #148 の参照、実装を先行した判断、実測の範囲、予備 A/B と製品候補への適用範囲を確認。

## 指摘事項

修正必須の指摘はない。修正確認マトリクスと重大度の変更は該当なし。

## 結果

**`pass_with_held`**。通常レビューの判定は暫定 base `90f934b50b6bf0b17b92e79a9352eb2441ac1b05` から固定 HEAD `76908b12b09f845c7a4df52753723b6e43208c36` までの差分に適用する。

| 観点 | 判定 | 根拠 |
| --- | --- | --- |
| 要求・設計、スコープ | `checked_no_finding` | #147 の近接重複判定だけを変更。計画の優先順の変更は実装報告に記載され、Red を先行した証拠がある。#128、通信経路、依存関係、設定、ワークフロー、GitHub 設定に関わるファイル差分なし。 |
| 判定、境界、順序 | `checked_no_finding` | `Dictionary` の同じエントリを走査し、カメラ一致、チーム一致、別 ID、同じ距離式と厳密な `< 135 mm` の順で短絡する。空の辞書と一致なしは `false`。最初の一致で `true`。辞書の変更は判定終了後。1 検出ごとに辞書を作る範囲、信頼度降順・同率 ID 昇順、同じキーへの登録の後勝ちを維持。 |
| API、データ、設定既定、互換性 | `checked_no_finding` | 具体型への変更は private メソッドの引数に限られ、製品唯一の呼び出し先には元から同じ具体型を渡す。定数、オプション、公開型と境界、シリアル化と出力生成に差分なし。 |
| エラー処理と診断 | `checked_no_finding` | 呼び出し元が生成した辞書は非 null。新たな例外経路、共有状態、I/O は増えない。既存の Red/Green の失敗診断と終了値を確認。 |
| セキュリティと機密の取扱い | `checked_no_finding` | 認証や入力境界への変更なし。公開予定差分に実記録、トレース、生の試験ログ、機密情報の追加なし。 |
| 試験、回帰、割り当て試験の安定性 | `checked_no_finding` | 新規 15 件と既存 16 件。公開 `Update` は等号・前後、負方向と斜めの等号、別チーム・別カメラ、信頼度と同率、同 ID の座標を検証。別検出にまたがるケースは既存試験で検証。直接判定の挿入順試験は、正例とともにカメラ・チーム・同 ID の条件を落とすと誤検出する負例を含む。割り当て試験は現在スレッドの同期区間を読み、準備・式のコンパイル・暖機・アサーション・出力を測定の外に置き、4,096 バイトの余裕を設けている。 |
| 文書・文字コード・保守性、証拠帰属 | `checked_no_finding` | 既存の責務別 partial ファイルを使用。変更した private 境界、新規試験クラス、各 `[Fact]` と `[Theory]` に日本語の XML 説明がある。BOM なし UTF-8 と LF を確認。予備 A/B は `task-23/ab-check/ab-result-20261119.md` に明記された別コピー上の周期内割り当て約 6.49% 減で、局所試験の値および製品の正式結果から区別している。 |
| 先行 PR 確定差分、正式な再生比較と実機区間 | `held` | 親が先行 PR #148 の確定 head を取り込んだ後、同じ通常レビュー担当が差分と正式資料を確認する。UDP、実時間の待機、多カメラ、追跡精度、および他実装との同条件測定も現時点で未確認。 |
| リポジトリ全体のローカル検証、対象 HEAD に一致する CI | `held` | 親の後続工程。実行又は成功の扱いは保留。 |

検証経路は `local_execution_available`。指定された主体と SDK による既存 Release 局所実行と、固定 HEAD の対象 blob との一致を確認した。このレビューで局所試験を再実行する必要はなかった。技術コミットは固定済み。今回のレビュー報告のコミット、公開、CI の照合は親の工程。独立最終レビュー用の証明コミットは本ラウンドの対象外。

## リスク

- 局所測定は、固定した 1 観測の辞書に近接・遠方を各 10,000 回入力した結果。周期全体やカメラ入力から配信までの改善幅は、先行 PR 取り込み後の正式 A/B で判断する。
- `Expression.Compile` を使う private 境界試験はメソッド名と形に依存する。対象の .NET 10 テストプロジェクトで実行証拠があり、公開 `Update` からも振る舞いを試験している。Linux CI は未照合。
- 権限上の保留は GitHub の PR メタデータと CI の再取得。作業木の情報と親の引き継ぎで Issue・PR の関係を扱った。レビューに必要なローカル読み取りと報告書の更新に阻害はなかった。
- 次回は同じ担当が #148 取り込み差分、正式な固定入力の結果、全体検証と関連証拠を確認する。課題 #147 全体の完了判定は後続。
