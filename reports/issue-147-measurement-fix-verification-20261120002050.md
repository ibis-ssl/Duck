# 課題147 計測基盤の修正確認

## タスク

- 目的: M147-NR-001〜003の必須修正と、追加差分を同じ通常担当が確認する。
- タスク種別: fix verification。対象3149ac94b5565d9b97324c0021a1e88f82ec227f、旧対象be5e34e46b4a72bd8e014b79bc88a57b0ae335cd。

## sub-agentを使う理由

- 理由: 同じ通常担当による指摘の継続確認。

## 対象範囲

- 対象: 比較不可判定と3ケースの実構成試験、版照合手順、文書差分、完全性の表と局所証拠。

## 対象外

- 対象外: 製品修正、独立最終レビュー、正式再生・全体ゲートの先行実行、Git/PR/Issueの更新。

## Dispatch profile

- requested profile: 元の gpt-6.2-sol / high / fork none を継続。
- agent role / default-role plan: 元のruntime default roleを保持。
- applied profile: null。
- application status: reused_existing_agent_profile。
- runtime profile observability: final_profile_hidden。
- reviewer continuity: /root/issue147_measurement_review。
- decomposition policy: forbidden / single_agent。
- reasons / constraints: 元の通常レビューと同じFA780承認経路・書込境界を保持。

## 実行コマンド

FA780 の PowerShell 7。コマンド単位の承認を使い、`whoami` が `fa780\donabe`、ブランチが `perf/issue147-measurement`、対象が `3149ac94b5565d9b97324c0021a1e88f82ec227f` であることを確認した。検証経路は `local_execution_available`。レビュー対象はローカルでコミット済み。同一コミットの公開および CI はこの修正確認では未確認。実装証拠は `issue147-measurement-fix-evidence/`、今回の再検証は `issue147-measurement-fix-review-evidence/`。

| コマンド | 終了値と証拠 |
| --- | --- |
| `git rev-parse HEAD`、`git diff --check`、旧対象からの全差分と基準からの範囲確認 | 0。修正の7ファイルを読み、すべて UTF-8/LF。Core、RuntimeHost、同梱設定、文書検査設定に基準からの差分なし。 |
| `Get-FileHash -Algorithm SHA256` と `validated-fix-source-manifest.json` の照合 | 0。明細は `64C8FEA497F0DD020AB839EE7099202528BD68035FD67363704F906B582447F7`。Program.cs、テスト、README の内容は検証時明細と一致。進捗2文書の最終版は後から合意された語句や見積もりを含み、旧明細の版と異なるため、該当コミットで文書検査を再実行。 |
| 実装担当の `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -c Release --filter=FullyQualifiedName~Cli_ReplayReportsWhetherTrackedOutputCanBeCompared` | 修正前は1、追加3件すべて失敗。形状情報のみと末尾保留は終了値が期待1に対し0、配信ありは比較可否の項目なし。実装証拠の `cli-red.trx` とログを確認。 |
| `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -c Release --filter=FullyQualifiedName~TrackerPerformanceTests` | 固定コミットで再実行し0。20件成功、失敗・スキップ0。SDK 10.0.404、既存 NuGet キャッシュ、空の復元元、`-p:NuGetAudit=false -m:1 /nr:false`。 `focused-source.txt`、`focused.trx`、`focused.binlog`、`focused-vstest.log`、標準出力・標準エラー・終了値。実装担当の局所20件成功とツールの Release ビルド0・警告0も確認。 |
| Release DLL と既存の合成記録のみを使用し、`--duration-seconds 1 --iterations 1` を各条件で指定 | 形状情報のみは1、追跡出力ありは0。両方とも実時計で暖機5秒超かつ3回以上、固定コミットの Release 3アセンブリを使用。旧出力に対する件数・ハッシュの一致を `synthetic-assertions.json` に保存。各 CLI の JSON、診断、終了値も保存。 |
| PowerShell で通常レビューの合成版不一致 JSON の3アセンブリを README の条件で照合 | 照合自体は0。ツール、Core、RuntimeHost の3件とも指定値 `d215ec0c3b9daef978bf889c15addbe97623da59` に対し版は `be5e34e46b4a72bd8e014b79bc88a57b0ae335cd`。すべて Release でも不一致のため棄却。実装証拠の `source-identity-document-check.json` と一致。 |
| `npm run lint:md:targets -- --files`、`lint:md:text`、`lint:md:spell`、既存の `check-markdown-whitelist-sudachi.py` | 対象列挙0、textlint 0。README 単独は cspell・Sudachi とも0、進捗2文書の課題147追加節も Sudachi 0。3文書全文の cspell・Sudachi はともに1だが、固定 main の診断を行位置補正して比較すると差分0。出力は `lint-*` と `document-baseline-comparison.json` に保存。今回用の依存リンクを解除し参照先は保持した。 |

## 対象ファイル

旧対象 `be5e34e46b4a72bd8e014b79bc88a57b0ae335cd` との差分7ファイルを確認した。実行部の `tools/Tracker.Performance/Program.cs`、実構成を組む `Tracker/Tracker.Tests/TrackerPerformanceTests.cs` と既存の形状情報用テストデータ、手順の `tools/Tracker.Performance/README.md`、進捗の `Tracker/Design/tasks-status.md`、`Tracker/Design/phases-status.md`。残りは、履歴として追加された旧通常レビュー報告と完全性表を追記した実装報告。旧通常レビューの指摘、判定、重大度は保持された。WarmupClock を経由する呼び出し先の記録読取、設定解決、製品の周期と直列化への接続を確認した。

## 指摘事項

新しい必須指摘なし。既存3件の重大度を維持し、すべて解消と判定した。実装報告末尾の完全性表で各要求、実際の経路、試験構成、局所の証拠が揃うことを確認した。親が最後に編集した進捗2文書は現在の固定コミットで追加検査した。

| 指摘 | 修正の必要条件と経路 | 実構成と確認証拠 | 判定 |
| --- | --- | --- | --- |
| M147-NR-001（P2、medium） | `Program.cs:86` で確定・配信の両方が正であることを既存のハッシュ一致、件数一致、配信失敗0に加える。`comparisonEligible` と件数・ハッシュを出力し、不可の場合は診断と終了値1。README に結果の利用条件を記載。 | ファイルとして作った合成 gzip と RuntimeHost JSON を読み、実際の `Program.Run` を呼ぶ追加3件を確認。形状情報のみ、入力検出が末尾保留、配信ありの全条件で赤から緑。単調時計のみを試験で差し替え、正式 CLI の `TimeProvider.System` とパス内の Stopwatch は分離。実際の CLI と実時計でも、0件は終了値1と比較不可、2件は終了値0と比較可。比較不可でも空列ハッシュと件数が保存され、両条件とも旧版の内容と一致した。 | resolved |
| M147-NR-002（P2、medium） | `README.md` の「変更前後の照合」で両方にツールを含む版をビルドし、3アセンブリの構成・版内の完全 SHA を照合。相違時は破棄し、変更のない対象版で Release を再ビルド。SHA が埋め込まれない環境と未コミット時は、ファイルとアセンブリを対応付けた明細を要求。 | 通常レビュー時に CLI が作った版不一致の JSON に手順を適用し、宣言値と3アセンブリすべてが不一致で棄却されることを再計算。今回の合成 CLI 出力では3アセンブリとも対象コミットと一致。採用した対策は利用手順によるゲート。 | resolved |
| M147-NR-003（P2、medium） | 対象3文書の用語を本文で修正し、許可一覧、辞書、除外を基準から維持。実コマンド、構成値、ブランチ名だけを行内コードで示す。 | 見積もりの「中」、局所20件、必須3件の修正、後続の判定と統合の記述を含めて現在の文書を検査。README の診断0、追加節の新規診断0。既存の cspell 115件、Sudachi のタスク25件と段階200件＋省略表示は位置補正した基準と同一。全文の終了値1は記録に残す。 | resolved |

## 結果

固定対象 `3149ac94b5565d9b97324c0021a1e88f82ec227f` の通常修正確認は **pass_with_held**。初回と同じ担当 `/root/issue147_measurement_review`。過去の判定は旧対象に対する履歴として有効。重大度の変更なし。

- `checked_no_finding` — 3件の必要条件と修正経路、比較可否と診断、版照合、3文書の差分、追加試験の日本語 XML、時計の分離、文字コードと差分範囲。
- `held` — 同一コミットの CI、全体.NETの検証、正式な前後比較。全文文書で残る既存診断は課題144の範囲。
- `not_applicable` — 認証、通信構成、製品の既定設定、Git・Issue・PR の更新。この回の未探索項目はない。

## リスク

本番と同一境界のUDP計測、外部トラッカーとの条件をそろえた比較、CPU標本、多カメラ品質は保留。今回確認したのは局所試験と合成記録であり、実記録で以前に得た1,876入力・908配信の結果を今回のコミットの再測定には数えない。後続は親による全体ゲート・正式比較と独立最終レビュー。
