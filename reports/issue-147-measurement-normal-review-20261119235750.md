# 課題147 計測基盤の通常レビュー

## タスク

- 目的: PR148の計測基盤を基準mainから独立して通常レビューする。
- タスク種別: initial review。対象 be5e34e46b4a72bd8e014b79bc88a57b0ae335cd、base d215ec0c3b9daef978bf889c15addbe97623da59。

## sub-agentを使う理由

- 理由: 実装担当と親の確認を通常レビューの代用にしない。

## 対象範囲

- 対象: 全変更ファイルと直接依存、入力選択、設定解決、周期・割り当て・件数・ハッシュの境界、試験、文書と証拠。

## 対象外

- 対象外: 製品修正、外部実装の性能比較、Issue/PR/Git操作、全体ゲートの先行実行。

## Dispatch profile

- selection inputs: review / judgment_heavy / uncertainty medium / cross_module / ordinary / single。
- selection source: review-enforcer、sub-agent-task-manager、本人指定6.2 Sol High。
- observed decomposability: independent_workstreams。
- decomposition policy / disposition: forbidden / prohibited_by_review_lifecycle。single_agent。
- requested profile: gpt-6.2-sol / high / fork none。
- agent role / default-role plan: runtime default role。確認した構成にagentsによるprofile変更なし。
- planned runtime profile: gpt-6.2-sol / high。
- applied profile: null。
- application status: spawn_succeeded_profile_unverified。通常担当 /root/issue147_measurement_review の最終profileは観測できていない。
- runtime profile observability: final_profile_hidden。
- reviewer continuity: この担当が通常の指摘と修正確認を継続する。
- fork policy: none。
- reasons / constraints: FA780の既知承認経路のみ。MCP/RDMCP/WSL禁止、設定や依存の追加なし。

## 実行コマンド

FA780 の PowerShell 7。承認付きのコマンドで `whoami` が `fa780\donabe`、作業木が `C:\Users\donabe\Documents\Codex\2026-11-19\task-23\Duck`、ブランチが `perf/issue147-measurement`、対象が `be5e34e46b4a72bd8e014b79bc88a57b0ae335cd` であることを確認した。実行前後の追跡済みファイルに編集はなく、未追跡は親が予約したこの報告のみ。検証経路は `local_execution_available`。対象はローカルでコミット済み。PR #148 の公開先は計画コミット `90f934b50b6bf0b17b92e79a9352eb2441ac1b05` で、レビュー対象の送信と同一コミットの CI 確認は未着手。保存先は作業木外の `issue147-measurement-normal-review-evidence/`。実装証拠は `issue147-measurement-evidence/`。

| コマンド | 終了値と証拠 |
| --- | --- |
| `git rev-parse HEAD`、`git diff --name-status d215ec0 be5e34e`、`git diff --check d215ec0 be5e34e` | 0。対象固定、全21ファイル、空白検査成功。 |
| `gh issue view 147 --repo ibis-ssl/Duck` と `gh pr view 148`・`gh pr view 149`（同じリポジトリ、対象の JSON 項目を指定） | 0。#147 は未完了。#148 は下書き、統合先が main、作業ブランチが perf/issue147-measurement。#149 は下書き、前者の作業ブランチを統合先とする。#148 は関連参照のみ。既存 CI 成功は公開済みの計画コミットに属する。 |
| `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -c Release --filter=FullyQualifiedName~TrackerPerformanceTests` | 0。17 件成功、失敗・スキップ 0。SDK 10.0.404、既存 NuGet キャッシュ、空の復元元、`-p:NuGetAudit=false -m:1 /nr:false` 使用。変更対象外に既存警告あり。対象コミットを `focused-source.txt` に固定。`focused.trx`、`focused.binlog`、`focused-vstest.log`、標準出力・標準エラー・終了値を保存。 |
| Release DLL を `--duration-seconds 1 --iterations 1` と同梱設定で合成記録に対して実行 | 3 実行とも 0。複合件数は `synth-counters-v2.*`、配信ゼロは `synth-geometry-only-v2.*`、ビルド識別の不一致は `synth-source-mismatch.*`、照合結果は `synthetic-assertions.json`。DLL の版に対象コミットがあることを確認。初回は合成入力の生成時に PowerShell の数値字句で失敗し、CLI 到達前のため再生成して照合した。実記録は使用していない。 |
| `npm run lint:md:targets -- --files` と `npm run lint:md:text -- --files`（README と進捗文書2件） | 0、0。対象3件と実行結果は `lint-targets.*`、`lint-text.*`。 |
| `npm run lint:md:spell -- --files`（同じ3件）と既存仮想環境の `python.exe tools/lint/scripts/check-markdown-whitelist-sudachi.py --files` | 1、1。対象全件の出力、Sudachi の個別の出力、固定 main の文書・検査設定を作業木外へ展開した比較の出力を `lint-*` に保存。node_modules の一時リンクは自分で作成したものだけを解除し、参照先が残ることを確認。 |
| `git ls-files --eol`、`git cat-file blob`、.NET の厳密な UTF-8 復号と SHA-256 | 0。全21ファイルが LF かつ有効な UTF-8。既存テストのプロジェクトファイルは基準版同様に BOM 付き。実装時の明細と照合し、復元した BOM 3 バイトを除いても検証前のハッシュが一致。設定ハッシュは基準版と一致。 |

実記録に関する実装済み証拠は、区間内入力 1,876 件、カメラ 1 台、確定・配信 908 件、出力 SHA-256 が `330BA050FA6C52A393F68A9FD8DFD079AEF9D489304108C7987CF97D2A4DE420`。修正後の動作確認と照合の終了値は 0。別種別の時刻逆行で初回に終了値 1 となった実装時のログも確認した。実記録の結果は作業中コミットと内容ハッシュで対応付けられた実装証拠で、今回の対象コミットで再実行した結果は合成記録と17件の局所試験。

## 対象ファイル

基準 `d215ec0c3b9daef978bf889c15addbe97623da59` からの差分を行単位で読み、次の21ファイルをすべて確認した。

- `tools/Tracker.Performance/` の `CliOptions.cs`、`Program.cs`、`Quantiles.cs`、`README.md`、`ReplayConfiguration.cs`、`ReplayCounters.cs`、`ReplayCounts.cs`、`ReplayEngine.cs`、`ReplayPass.cs`、`ReplayPublisher.cs`、`ReplayRunner.cs`、`Tracker.Performance.csproj`、`VisionInput.cs`、`VisionLogReader.cs`、`VisionWindow.cs`。
- `Tracker/Tracker.Tests/TrackerPerformanceTests.cs` と `Tracker/Tracker.Tests/Tracker.Tests.csproj`。
- `Tracker/Design/tasks-status.md`、`Tracker/Design/phases-status.md`、`reports/issue147-measurement-plan-20261119.md`、`reports/issue147-measurement-implementation-20261119.md`。

直接依存として、RuntimeHost の設定バインドと解決、同梱 JSON、受信サービス、最新値バッファ、周期処理と登録を確認した。Core のエンジンと入力時刻換算、順序付け、確定、形状変更、更新結果・検出元、コーディネーター、配信時の集計、UDP 用配信と比較してのメモリー配信、protobuf パケット生成を確認した。変更対象のプロジェクト参照と CI の局所・全体テスト経路、比較構成を指す AppHost と比較用ワークフロー、課題56の Windows 受入報告、英字・片仮名の文書検査設定と許可一覧、改行規約も参照した。

## 指摘事項

### 必須

1. **M147-NR-001（P2、medium）** — `tools/Tracker.Performance/Program.cs:139`。追跡出力のない再生を、品質照合の成功から区別する。合成した正常な SSL ログで形状情報を持つ type 4 が2件あり、指定窓に到達したが検出・確定・配信は0件。二つの品質再生は空列の SHA-256 `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` で一致し、両品質判定が true のまま CLI が 0 で終了した。検出が存在しても選択窓が短く全件が保留になる場合に同じ問題が起きる。課題147の出力一致を証明できない結果で計測比較が成立したように扱える。件数と識別値を診断として残し、確定・配信が0件の場合に比較対象として使えないことを終了値または機械可読の状態と明確な診断で示す。README に合否判定方法を記し、CLI を通す合成試験で情報だけの入力と末尾まで未確定の境界を確認する。
2. **M147-NR-002（P2、medium）** — `tools/Tracker.Performance/README.md:27`、同45行、`tools/Tracker.Performance/Program.cs:85`。前後の照合手順に実行バイナリの識別の合否条件を設ける。局所試験で生成した Release の3アセンブリすべての版が `be5e34e46b4a72bd8e014b79bc88a57b0ae335cd` を持つときに `--source-revision d215ec0c3b9daef978bf889c15addbe97623da59` を指定しても、CLI は 0 で終了し、`source.sourceRevision` には基準版と表示された。CLI が引数を保存することは説明済みだが、45行の前後比較の照合項目に、想定コミットと実測 DLL の版を突き合わせる手順がない。同じビルドを比較していても基準版と候補版の計測として扱えてしまう。明示状態を出して比較を拒否するか、少なくとも README の比較手順で想定 SHA・各アセンブリの版・Release 構成を照合し、一致しない結果を棄却する条件を示す。版に SHA が埋め込まれない環境と未コミットの作業には別途内容明細が必要なことを記載する。比較対象の両方で計測ツールを含む対応するコミットを使う旨も明示する。自動判定を追加する場合は、誤った SHA での実行も局所試験で確認する。
3. **M147-NR-003（P2、medium）** — `tools/Tracker.Performance/README.md:7`、同23・25・27・31・35・37・39・41・45行、`Tracker/Design/phases-status.md:3`、`Tracker/Design/tasks-status.md:11`、同12・14行。変更文書の用語違反を増やしている。cspell では README に8件、進捗2文書の追加行に3件の新規診断。Sudachi は README 32件、進捗の追加行が2件と10件。基準 main も既存文書で cspell 1、Sudachi 1 を返すため、既存の課題144に属する診断をこの指摘から分離した。
   - README の英字は PowerShell、type、MiB、GC、Release。片仮名の指摘はパス、プロファイル、レコード、ペイロード、バージョン、モジュール、メモリー、フレーム、サンプリングなど。種別番号、16,777,216 バイト、世代別の回収回数、入力の保存場所と再生回、有効な設定、記録単位、通信データ、版、確定した追跡結果、標本採取など、意味を保った正文で置き換えられる。起動シェルを特定する必要があれば実コマンド名 `pwsh`、構成値は実際の引数 `-c Release` として示せる。
   - 進捗文書の英字は base、head、main、Release。日本語の「統合先」「作業ブランチ」「課題を閉じない」を使い、main を特定する箇所は実際のブランチ名 `main` として扱う。本文を囲って検査から隠さない。現在の変更行の行内コードは識別子・引数・実値として使われており回避は見つからなかった。
   - 同一の設定で対象3文書に対する textlint・cspell・Sudachi を再実行し、新規違反が消えたことを基準との比較で示す。元からの違反が残ると全体の終了値は 1 になる。辞書・許可一覧・対象除外は変更不要。

### 保留

- **M147-NR-H01** — 全体の .NET ゲート、同一コミットの CI、正式な7回の前後比較は通常レビュー収束後に親が行う。CPU の標本採取と UDP 対向構成での比較はこの基盤のレビュー対象外。
- **M147-NR-H02** — 既存文書の検査違反は課題144で別途追跡される。既存の全体検査の失敗を成功と扱わない。作業範囲で語の許可追加を求める理由は見つからなかった。

## 結果

通常レビューの判定は **fail**。必須指摘3件。対象コミットは `be5e34e46b4a72bd8e014b79bc88a57b0ae335cd`。担当は `/root/issue147_measurement_review`。実装担当と分離し、製品変更・Git 書き込み・Issue/PR の変更は行っていない。修正確認時は同じ担当が、指摘ごとの修正内容・実際に組み合わせた試験・対象コミットの証拠を照合する。

| 観点 | 到達状態 | 根拠 |
| --- | --- | --- |
| 要求・設計、分割、影響範囲 | checked_no_finding | #147 の計測側の全差分を確認。Core、RuntimeHost、既定の設定、通信構成には差分なし。後続の最適化は別 PR。 |
| 入力形式、半開区間、時刻、失敗診断 | checked_no_finding | gzip のヘッダー・版・種別・長さ・受信時刻・EOF の実装と試験を確認。窓内の別種別の時刻は比較しない。区間外は最初の対象ヘッダーで停止し、圧縮ファイル全体のハッシュを記録する。 |
| 設定の解決、互換性 | checked_no_finding | 同梱 JSON と既存バインド・解決を利用。合成事例では指定を省略して sim と16 ms を使用。CLI の明示指定は既存の上書き処理を利用。配信先の接続は作らない。 |
| 周期・復号の計測境界、割り当て、CPU、GC | checked_no_finding | 最新値取出しからメモリーへの直列化までの周期計測と、復号・保存の計測を区別。周期外の集計は再生全体の時間に入る。確定待ち時間は仮想時刻の値。暖機が最低3回かつ5秒。 |
| 件数、受信対応、配信失敗 | checked_no_finding | 合成記録9件を6周期で再生。8更新、非検出置換1、遅着1、形状変更による保留破棄1、末尾保留1、確定検出3、確定・配信2、失敗0。重複したカメラ・番号・時刻の2入力は同一確定に収まり、仮想遅延は15/31 ms。形状変更を同時に持つ検出も受理と即時確定を確認。配信失敗は snapshot から判定する。 |
| 出力ハッシュ、比較できる結果 | checked_finding | 正常ケースで品質再生と時間計測が分離され、件数も照合される。空列との一致は M147-NR-001。 |
| コミットと実行バイナリの識別 | checked_finding | このレビューの局所証拠は対象コミットに紐付けた。CLI 利用者が用いる比較条件には M147-NR-002。 |
| 試験の充足、既存証拠 | checked_finding | 既存の17件成功と複合ケースを確認。M147-NR-001 の比較失効を確認する試験が必要。 |
| 文書、追跡状態、差分の用語検査 | checked_finding | 進捗と調査報告の記載、リンク、比較境界を読了。M147-NR-003 と M147-NR-002。 |
| 文字コード、配置、日本語 XML、維持性 | checked_no_finding | 全21ファイルの有効な UTF-8/LF、既存プロジェクトの BOM 維持を確認。既存のテストソースは BOM の有無が混在。追加型は責務別、テスト属性直前に日本語 XML の目的がある。 |
| 現行コミットの CI と全体試験 | held | gh が示す成功は計画コミット。固定対象の CI と全体ゲートを先行実行する指示なし。 |
| 認証・外部書き込み・生データの扱い | not_applicable | 新規の認証・依存追加・生ログ配布・製品の通信変更なし。CLIのJSONに入力パスや入力本文がないことを確認。 |
| 外部トラッカー、実時間 UDP の性能・多カメラ品質 | held | 既存の3送信元を識別する構成と今回の仮想再生との違いを文書で確認。実測比較なし。 |
| 未探索 | not_applicable | 今回の通常レビューで必須とされた観点に未探索のものはない。 |

## リスク

仮想周期の CPU・時間・割り当ては同一機・同一条件の変更前後の比較用。UDP の受信から配信までの実遅延、入力負荷時の欠落、OS 待機、外部製品、多カメラ入力の追跡精度は未検証。正式な性能計測とサンプリングの値はない。局所の合成記録から性能値の評価は行っていない。経過を指す PR #148 の本文は公開済みの計画コミット時点の記載で、同一対象の最終条件・証拠は親が公開時に同期する。

次は実装担当へ必須3件を返し、修正対象、組み合わせた試験、終了値、コミットを揃えて通常の修正確認を行う。通常レビューの報告をここに保存し、独立最終レビューやその報告の確定は後続工程で行う。
