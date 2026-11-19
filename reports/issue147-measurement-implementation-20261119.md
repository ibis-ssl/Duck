# 課題147 計測基盤の実装報告

## タスク

- 目的: PR #148 に固定入力の最小計測ツールと再現手順を追加する。
- タスク種別: 実装・局所検証。基準 d215ec0、実装前計画 90f934b、ブランチ perf/issue147-measurement。

## sub-agentを使う理由

- 理由: CLI、入力検証、計測、試験にまたがる実装を一つの担当へ委譲し、検証実行を親から分離する。

## 対象範囲

- 対象: tools 内の .NET 計測CLI、必要なテストと日本語の手順、当該報告。

## 対象外

- 対象外: Coreの最適化、既定設定、通信構成、Docker起動、生ログの複製、Git操作、課題・PR・進捗文書の更新。

## Dispatch profile

- selection inputs: implementation / bounded_technical / uncertainty medium / cross_module / ordinary / single。
- selection source: codex-delegation-executor、sub-agent-task-manager、本人指定の6.2 Sol High。
- observed decomposability: sequential_dependencies。
- decomposition policy / disposition: forbidden / 同じ最小CLIと試験の一貫性を保つ。
- requested profile: model gpt-6.2-sol、reasoning high、fork none。
- agent role / default-role plan: runtime default role。構成の対象行にagentsによるprofile変更は検出せず。
- planned runtime profile: gpt-6.2-sol / high。
- applied profile: null。
- application status: spawn_succeeded_profile_unverified。実装担当 /root/issue147_measurement_impl が完了し、最終profileの値は観測できていない。
- runtime profile observability: final_profile_hidden。
- fork policy: none。
- reasons / constraints: 追加依存なし、.NET、MCP/RDMCP/WSL禁止。FA780はコマンド単位の既知承認経路を使う。

## 実行コマンド

FA780 の PowerShell 7 で、コマンドごとの承認を経て `whoami` が `fa780\donabe` であること、ブランチと HEAD が一致することを確認した。.NET SDK は `10.0.404`。既存の NuGet キャッシュと空の復元元を使用した。コマンドの終了値とログは作業ツリー外の `issue147-measurement-evidence/` に保存した。

| コマンド | 結果とローカル証拠 |
| --- | --- |
| `gh issue view 147 --repo ibis-ssl/Duck --json number,title,body,state,url` | 終了値 0。固定入力、品質一致、前後比較の完了条件と積み重ねの範囲を確認。 |
| `dotnet build tools/Tracker.Performance/Tracker.Performance.csproj -c Release` | 終了値 0、警告 0、エラー 0。`cli-build.*`。 |
| `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -c Release --filter=FullyQualifiedName~TrackerPerformanceTests` | 終了値 0、17 件成功、失敗・スキップ 0。`cli-focused.trx`、診断、標準出力、標準エラー、ビルドログを保存。変更対象以外のプロジェクトに既存の警告がある。 |
| `dotnet tools/Tracker.Performance/bin/Release/net10.0/Tracker.Performance.dll --archive "$archive" --settings Tracker/Tracker.RuntimeHost/appsettings.json --profile sim --source-revision "$revision" --iterations 1` | Release の実記録の動作確認。修正後の CLI と照合コマンドはそれぞれ終了値 0。`cli-replay-smoke.*`、`cli-replay-smoke-assertions.exit-code`。 |

ビルドと試験には空の `RestoreSources`、`-p:NuGetAudit=false`、`-m:1`、`/nr:false` および各ログの保存先を指定した。試験の順序は実装後の局所検証。課題と分割計画の検証項目を適用し、実装前失敗の履歴は要求されていない。

## 対象ファイル

- `tools/Tracker.Performance/` に CLI、入力の読み取り、設定と再生計測、配信データの照合、日本語の再現手順を追加した。既存 RuntimeHost の設定解決、入力バッファ、周期ループと Core の追跡を使用する。
- `Tracker/Tracker.Tests/TrackerPerformanceTests.cs` に、区間の端、記録形式、時刻逆行と別種別の時刻、無入力、引数、件数と配信ハッシュのテストを追加した。`Tracker/Tracker.Tests/Tracker.Tests.csproj` は CLI のプロジェクト参照を追加した。

## 指摘事項

実記録の初回確認では、別種別のレコードを含めて受信時刻の単調性を要求したため、CLI が終了値 1 で停止した。窓の終端と単調性の対象を再生する type 4 に限定し、別種別に区間外の時刻があっても区間内の type 4 を選ぶテストを追加した。`initial-cli-replay-smoke.stderr.log` と終了値を保存している。独立レビューと判定は親が実施する。

## 結果

基準コミットは `d215ec0c3b9daef978bf889c15addbe97623da59`、検証時のローカル HEAD は `90f934b50b6bf0b17b92e79a9352eb2441ac1b05`。検証対象は未コミットのため、追加・変更したファイル、設定、主要な Release アセンブリの SHA-256 を `validated-source-manifest.json` に保存した。局所検証時点の明細の SHA-256 は `F8D5E8BE3C7EE8107065DCCDD3CF40D1AE4BF9E79302A0F5D26AF5B024FF424A`。検証後に既存のテストプロジェクトの UTF-8 BOM を復元した。先頭の 3 バイト以外は検証時点と同じであり、XML と LF 改行も一致した。試験は再実行していない。復元後の差分を追記した明細の SHA-256 は `D7CFC113C998509E3827EF7C01E3A9001C3DE6DFB39EE7F00AA7F71B517D3A33`。

既存の実記録から先頭 30 秒を選んだ。圧縮記録、区間ペイロード、設定の SHA-256 は [再現手順](../tools/Tracker.Performance/README.md) 記載値に一致した。`sim` 設定、16 ms 周期、並べ替え窓 10 ms、統合窓 20 ms。1 カメラから 1,876 入力で、1 入力あたり最大 22 ロボットと 1 ボール。1,876 仮想周期、1,733 エンジン更新、1,732 検出確定、908 フレーム確定と 908 配信を記録した。配信は合計 1,005,937 バイト。最新値の置換 143 件、遅着破棄 0 件、末尾の保留 1 件、配信失敗 0 件だった。品質再生 2 回の出力 SHA-256 は `330BA050FA6C52A393F68A9FD8DFD079AEF9D489304108C7987CF97D2A4DE420` で一致した。

暖機 5.03 秒・79 パス後の動作確認 1 パスでは、周期 p50/p95/p99 が 1.2/46.6/50.9 µs、入力復号・保存の p50/p95/p99 が 3.3/3.8/4.6 µs、最新検出の受信から仮想確定までの p50/p95/p99 が 17.285242/32.961469/33.860638 ms。周期内割り当ては 88,223,496 バイト、入力復号・保存は 20,531,080 バイト、世代 0/1/2 の GC は 13/0/0 回、再生の経過時間は 48.8311 ms、プロセス CPU 時間は 31.25 ms。計測を再現できることを確かめた 1 パスの値であり、変更前後の正式な 7 パス比較にはコミットした両版を使う。

## リスク

- UDP 入出力、実時間待機、実時間での過負荷、多カメラ負荷、追跡精度と外部実装との同条件比較が未検証。16 ms は設定周期。
- 局所検証を完了した。リポジトリ全体の検証、レビュー対象コミット、通常・独立最終レビュー、公開前の同一 HEAD の検証と CI が残る。既存記録は作業ツリー外に保持した。

## 通常レビューの修正

[通常レビュー](issue-147-measurement-normal-review-20261119235750.md) の対象 `be5e34e46b4a72bd8e014b79bc88a57b0ae335cd` について、必須3件を修正した。レビュー後の変更は未コミット。コードと文書の対象5ファイルおよび主要な Release アセンブリのハッシュを作業ツリー外の `issue147-measurement-fix-evidence/validated-fix-source-manifest.json` に保存した。明細の SHA-256 は `64C8FEA497F0DD020AB839EE7099202528BD68035FD67363704F906B582447F7`。

| 指摘 | 要求と対応 | 製品の経路 | 実際に組み合わせた検証 | 局所証拠 |
| --- | --- | --- | --- | --- |
| M147-NR-001（P2、medium） | 確定または配信が0件の再生を比較から外す。`comparisonEligible=false`、診断文、終了値1を返し、件数と出力ハッシュを JSON に保持する。配信があるときも、再生間の件数とハッシュの一致、配信失敗0件を要件とする。 | `Program.Run` から製品の記録読取、設定解決、仮想周期、メモリー配信、JSON 出力を実行。正式な暖機は実際の単調時計を使い、試験の時計では実時間待機を省いた。 | 合成 gzip と RuntimeHost JSON を実ファイルで作り、1秒窓、16 ms 周期、並べ替え窓5 ms、統合窓0、反復1回で CLI を実行。形状情報のみ、検出1件が末尾で保留、検出2件から1件確定・配信、の3条件を検証。 | 修正前は新規3件が失敗した。形状情報のみと保留1件の両方は終了値0を返し、配信ありは新しい状態項目が未定義だった。修正後は既存17件と新規3件の計20件成功、失敗・スキップ0。`cli-red.*`、`cli-focused.*`。 |
| M147-NR-002（P2、medium） | 前後の該当コミットを `-c Release` でビルドし、3アセンブリの版・構成を指定した完全なコミット識別子と突き合わせる。結果を破棄する条件、変更のない作業場所での再ビルド、未コミット変更やコミットを埋め込めない環境での明細を README に明記。 | CLI の `source.tool`、`source.core`、`source.runtimeHost` の `configuration` と `informationalVersion` を使う利用手順。 | 通常レビュー時の合成記録を実際に実行した出力を利用。指定値は `d215ec0`、Release の3アセンブリはすべて `be5e34e`。 | README の手順に従う照合で3アセンブリすべて不一致、比較対象から除外。`source-identity-document-check.json`。 |
| M147-NR-003（P2、medium） | README と進捗2文書の課題147追加部分の語句を修正した。 | `tools/Tracker.Performance/README.md`、`Tracker/Design/tasks-status.md` の3行、`Tracker/Design/phases-status.md` の1行。既存の検査設定を使用。 | README と進捗2文書の全文を対象として標準の3検査を実行し、README と課題147追加節を個別にも確認。基準の通常レビューで保存した診断を追加行数で補正して比較。 | README は cspell・Sudachi とも0、課題147追加節も新規診断0。3文書の textlint は0。3文書の cspell・Sudachi が返す1は基準と同じ診断に対応する。`document-baseline-comparison.json` と `doc-*`。 |

FA780 の PowerShell 7 で承認経路を使い、`whoami`、ブランチ、HEAD を確認した。SDK は `10.0.404`。既存 NuGet キャッシュと空の復元元を使用。`dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -c Release --filter=FullyQualifiedName~Cli_ReplayReportsWhetherTrackedOutputCanBeCompared` は修正前に終了値1、`--filter=FullyQualifiedName~TrackerPerformanceTests` は修正後に終了値0。`dotnet build tools/Tracker.Performance/Tracker.Performance.csproj -c Release` は終了値0、警告・エラー0。文書は `npm run lint:md:targets`、`lint:md:text`、`lint:md:spell` と、既存環境の `tools/lint/scripts/check-markdown-whitelist-sudachi.py` を使い、各コマンドの標準出力・標準エラー・終了値を保存した。最初の文書検査の診断も `initial-doc-*` に保持している。

全体の .NET ゲート、正式な前後比較、同じレビュアーによる修正確認は後続工程。この修正では元の実記録を再生していない。
