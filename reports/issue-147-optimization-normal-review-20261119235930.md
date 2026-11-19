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

## 追加通常レビュー — 先行 PR の取り込みと正式 A/B

**修正必須の指摘なし。差分と計測の追加通常レビューは `pass_with_held`。** 初回と同じ担当 `/root/issue147_optimization_review` が確認し、初回の対象と判定は上に保持した。

### 対象と帰属

- A は PR #148 の `perf/issue147-measurement`、`9c9cc3fe89393cec3ac702bc2f04769f1b9b4732`。B は PR #149 の `perf/issue147-near-duplicate`、`d5fdc7aad952b311d33fee7927b8e070686a9ab9`。`git merge-base A B` の結果は A。両作業木は再生の前後に clean を確認した。
- A..B は `RobotTracking.cs` の 18 行の差分、追加回帰試験、後続の報告 3 件の計 5 ファイル。`git diff --check A B` は終了値 0。初回の固定 HEAD `76908b12b09f845c7a4df52753723b6e43208c36` と B の製品対象、追加試験、既存ロボット契約試験の blob は一致した。既存 Red/Green の製品・試験 SHA-256 もそれぞれ A/B と一致した。
- `git ls-tree -r` に基づく参照プロジェクトの一覧と取得済みファイルの指紋を比較し、ビルド対象のソース差は `RobotTracking.cs` の 1 ファイルと確認した。`tools/Tracker.Performance` の全追跡ファイルと RuntimeHost の `appsettings.json` は A/B 同一。参照先の Core、RuntimeHost、SslProto、診断ライブラリ、TrackerConnectionLib、パッケージ定義を確認し、ビルドに使う 2 つのプロトコルサブモジュールも同一 SHA と確認した。B の未初期化 `reference/specs` は、このプロジェクトのビルド対象外。
- FA780 の承認経路で `whoami` は `fa780\donabe`。PowerShell 7.6.10、SDK 10.0.404、再生実行時は .NET 10.0.15、Windows 10.0.22631、16 論理プロセッサ、ワークステーション GC。各版の `Tracker.Performance.dll`、`Tracker.Core.dll`、`Tracker.RuntimeHost.dll` について、再生前のファイルから `Release`、`1.0.0+<各版の完全 SHA>`、MVID、SHA-256 を確認した。全実行の報告に含まれる構成・版・MVID と一致し、再生終了後の DLL 指紋も同じだった。

### 実行と証拠

各作業木で同じ SDK、既存 NuGet キャッシュ、作業領域の CLI home と空の復元元を使用した。事前に古い DLL の版が観測されたため、両版とも最初から Release の `Rebuild` を指定した。各ビルドは終了値 0、警告 0、エラー 0。環境変数は起動プロセス内で指定した。

```powershell
dotnet build tools/Tracker.Performance/Tracker.Performance.csproj --configuration Release `
  --nologo -t:Rebuild -m:1 /nr:false "-p:UseSharedCompilation=false" `
  "-p:RestoreSources=$feed" "-p:NuGetAudit=false" "-bl:$binlog"

dotnet tools/Tracker.Performance/bin/Release/net10.0/Tracker.Performance.dll `
  --archive $archive --settings Tracker/Tracker.RuntimeHost/appsettings.json `
  --profile sim --source-revision $revision
```

読み取り専用の既存の gzip 記録に対し、最初の SSL-Vision パケットから 30 秒、暖機は各プロセス 5 秒以上、既定の 7 計測パス。実行順は `A-B-B-A / A-B-B-A`、直列 8 プロセス、A/B 各 28 計測パスと各 8 品質再生。8 再生の終了値はすべて 0 で標準エラーは空。個別の時刻記録で実行が重複していないことを確認した。

証拠は作業木外の `task-23/validation/issue147-formal-ab/`。`commands.json` に作業ディレクトリ、実引数、時刻、終了値。`source-manifest.json` と `assembly-manifest.json` にソースとビルドの指紋。`run-01-A.json` から `run-08-A.json` までの個別 JSON、終了値、標準エラー、両ビルドのログと binlog、実行・集計用 PowerShell、`summary.json` を保存した。集計スクリプトの終了値は 0。`summary.json` の SHA-256 は `584BAD82B1A0510743888A80CC071BBD26AC2A474903A13D9F71DE37CA8EA0EE`。記録本体は元の場所から読み取った。

### 同一入力、設定、出力

| 照合項目 | A/B の全実行で一致した値 |
| --- | --- |
| 圧縮記録 SHA-256 | `F1521FE40977CD97E2E886C6056BBB779C849C397A61A090A0003751CCA380FF` |
| 区間入力 SHA-256 | `ED03D2EF7771A2AA17CC9D0A8034D821A56CFB035A0D430B50BD0B90071DD023` |
| 設定ファイル SHA-256 | `0ECC3BC8A16224C62FBFB7CE85E2CAF238F713A044C9F3A29477BEAE09D108D1` |
| 実効設定 | `sim`。周期 16 ms、並べ替え窓 10 ms、統合窓 20 ms。解決済み設定全体も一致。 |
| 入力・周期 | 1,876 入力、1 カメラ、入力あたり最大 22 ロボットと 1 ボール。1,876 周期と 1,733 エンジン呼び出し。 |
| 確定と配信 | 1,732 確定検出、908 確定フレーム、908 出力、1,005,937 バイト。 |
| 欠落と末尾 | 検出の最新値置換 143、検出なし入力の置換 0、遅着破棄 0、形状リセット破棄 0、末尾保留 1、配信失敗 0。 |
| 連結出力 SHA-256 | `330BA050FA6C52A393F68A9FD8DFD079AEF9D489304108C7987CF97D2A4DE420` |

全 8 実行で `comparisonEligible`、`quality.deterministicWireOutput`、`quality.consistentCounts` は真。全 56 計測パスで同じ件数、全 16 品質再生で同じ出力ハッシュを確認した。

### 正式計測結果

各版 28 パスの中央値と最小–最大。p50、p95、p99 の行は各パス内の分位点を集計した値。

| 指標 | A 中央値［最小–最大］ | B 中央値［最小–最大］ |
| --- | ---: | ---: |
| 周期内割り当て（バイト／パス） | 88,223,496［88,223,496–88,296,136］ | 82,528,680［82,528,680–82,528,680］ |
| 入力側割り当て（バイト／パス） | 20,531,080［20,531,080–20,531,080］ | 20,531,080［20,531,080–20,531,080］ |
| 周期処理 p50（µs） | 0.90［0.8–1.3］ | 0.90［0.8–2.0］ |
| 周期処理 p95（µs） | 44.75［43.6–48.2］ | 36.95［36.4–47.2］ |
| 周期処理 p99（µs） | 49.25［47.6–55.3］ | 41.10［39.7–68.0］ |
| 入力処理 p50（µs） | 3.10［3.0–3.3］ | 3.20［3.0–3.7］ |
| 入力処理 p95（µs） | 3.40［3.3–3.9］ | 3.60［3.3–5.4］ |
| 入力処理 p99（µs） | 4.30［3.8–4.6］ | 4.25［3.6–7.2］ |
| 再生経過時間（ms／パス） | 46.6572［45.6083–50.1994］ | 40.00805［39.2310–48.2935］ |
| プロセス CPU 時間（ms／パス） | 46.875［31.25–78.125］ | 46.875［31.25–78.125］ |
| Gen0 回収（件／パス） | 13［13–14］ | 12［12–13］ |
| Gen1・Gen2 回収（件／パス） | それぞれ全パス 0 | それぞれ全パス 0 |
| 仮想確定遅延 p50 / p95 / p99（ms） | 全パス 17.285242 / 32.961469 / 33.860638 | 全パス 17.285242 / 32.961469 / 33.860638 |

周期内割り当ては中央値で 5,694,816 バイト、6.45% 減。周期 p95 と p99 の中央値はそれぞれ 17.43% と 16.55% 減り、再生経過時間の中央値は 14.25% 減。B の p99 の観測範囲の上端は 68.0 µs に広がった。28 パスのプロセス CPU 時間の合計は A 1,343.75 ms、B 1,203.125 ms。Windows の CPU 時計は粒度が粗く、中央値は両版 46.875 ms。Gen0 の回収回数の合計は 365→347。

周期処理は、最新入力の取り出しから追跡、パケット生成、主記憶への protobuf 直列化まで。入力側は復号から複製とバッファ保存まで。仮想確定遅延の始点は、当該確定結果に含まれる入力のうち最後に到着した記録上の受信時刻。空周期を含めて 16 ms 刻みの仮想時計で処理した。正式 A/B で使用した記録展開と品質ハッシュは時間測定区間外。以前の別コピーで行った予備 A/B の約 6.49% と、この正式 A/B の 6.45% は別の実行結果。

### 追加判定と保留

| 観点 | 判定 |
| --- | --- |
| 先行候補取り込み、変更範囲、製品・計測ソースと既存局所証拠の同一性 | `checked_no_finding` |
| 実行 DLL の帰属、固定入力、暖機、順序、出力と設定の比較 | `checked_no_finding` |
| 測定境界、集計、改善値のばらつきと変更の解釈 | `checked_no_finding` |
| 後続候補の全体 .NET ゲートと一致する CI、独立最終レビュー | `held` |
| UDP、OS の実時間待機、実時間過負荷、多カメラ、正解データを使う追跡精度、TIGERs Mannheim／ER-Force との同条件比較 | `held` |

このラウンドの CPU はプロセス CPU 時間を集計した。CPU サンプリングの生トレースは予備 A/B の別実行にあり、関数別の標本集計は未実施。全体 .NET ゲートと CI の照合は親の後続工程。ここでの一致と改善値は、上記コミット・設定・記録区間の再生に適用する。
