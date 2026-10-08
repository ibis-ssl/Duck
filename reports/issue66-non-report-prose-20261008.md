# レポート以外の文書の文章改善 実装報告

## 実施結果

Duck のレポート以外の Markdown 19 文書を分担して全文確認し、15 文書の 84 箇所に文章の改善を適用した。原文と改善案を照合する実装補助の点検で 4 案を補正し、その補正を含めて採用した。残る 4 文書は全文を確認したうえで原文を維持した。仕様や履歴を推測で具体化しないために保持した項目は、文書数とは別に 10 件記録した。

ここでいう「84 箇所」は、採用案の文・段落・表セルなどを単位とした置換件数である。84 段落という意味ではない。今回の作業状況の追記、報告書、診断記録は、この 84 件に含めていない。

件数、対象、原文、採用した置換文、保持理由は [採用案記録](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json) に記録している。構造検査の集計は [提案の整合性検査記録](diagnostics/issue66-non-report-prose-20261008/proposal-integrity.json) に残した。

実装コミットは `506c5342f646e0392532ff02f429196254e8f670` で、変更量は 15 ファイル、183 行追加、104 行削除である。その後、進捗の追記に含まれた未登録の 2 語を既存語へ修正した。修正後の全文 18 件に対するホワイトリスト、`textlint`、`CSpell` と `git diff --check` はすべて終了コード 0 だった。

製品テストの再実行は 329 件中 318 件成功、11 件失敗、スキップ 0 件だった。11 件はいずれも、開いている補助ファイルの読み取りで発生した Windows のファイル共有に関する `System.IO.IOException` である。文書検査は成功、製品テストは失敗として記録する。初回の文書検査失敗と、未初期化の依存による初回のコンパイルエラーも履歴に残した。変更前の HEAD との比較結果は後述の比較欄で扱う。([修正後の文書検査](diagnostics/issue66-non-report-prose-20261008/final-fixed/results.json)、[製品テストの再実行](diagnostics/issue66-non-report-prose-20261008/product-gate-retry/results.json)、[製品テストの失敗一覧](diagnostics/issue66-non-report-prose-20261008/product-failures.json))

本書と診断記録を保存した時点では、管理用コミット、push、公開後の対象 HEAD に対する CI 確認は未完了である。これらの後続結果は、対象 SHA と証跡を添えて PR コメントに記録する。

本書は文章改善の実装報告である。改善案の相互点検と構造検査は実装を補助するために実施した。正式な通常レビュー、独立レビュー、利用者の受入、merge は実施していない。

## 対象の識別

| 項目 | 値 |
| --- | --- |
| リポジトリ | `ibis-ssl/Duck` |
| 課題 | [Issue #66](https://github.com/ibis-ssl/Duck/issues/66) |
| 変更先 | [PR #67](https://github.com/ibis-ssl/Duck/pull/67) |
| 作業ブランチ | `docs/issue66-yomiyasu-pilot` |
| base | `main` |
| base SHA | `449296725fc69dc004818ede2e8ad59a52ef2d27` |
| 追加作業前 HEAD / 参照原本 | `c9c44913d443410ae4768b26faafd48fa367b2e2` |
| 文章改善の実装コミット | `506c5342f646e0392532ff02f429196254e8f670` |
| 初回候補検査時の dirty fingerprint | `a42183a2510e26cfd4132aab2e203f13aaeb7cd2d8793508bc35ad849864d1bf` |
| 修正後の文書検査・製品テスト再実行時の HEAD | `506c5342f646e0392532ff02f429196254e8f670` |
| 同検査時の dirty fingerprint | `b72b345b431a1b8fee9e1d8a4be91096108f249bb641a3e6576de9b9f268be90` |
| 本書の保存先 | `reports/issue66-non-report-prose-20261008.md` |
| 診断記録の保存先 | `reports/diagnostics/issue66-non-report-prose-20261008/` |

ブランチ、base SHA、実装コミット、検査対象の fingerprint、実装後の検査結果は、実装担当が確定した証跡に基づく。追加作業前の原本の識別とファイルの照合結果は、採用案記録と提案の整合性検査記録でも確認できる。修正後の検査対象は、コミットの HEAD と作業ツリーの fingerprint を併記して識別した。

## 依頼と実装範囲

利用者は対象の追加を次のように依頼した。

> 他にもレポート以外の文章があればそれも対応してほしい

対象リポジトリは、その後の発言で明示された。

> duckの中の話です

この依頼を受け、Duck 内の設計書、計画書、現在と過去の進捗文書、README、文書検査の説明、利用者の指摘記録を確認した。既存レポートの本文は今回の文章改善対象に含めていない。利用者の発言を直接引用している記録は、引用と記録の意味を保持した。

文章改善では、修飾語がどの対象に掛かるか、動作の主体と対象、条件と結果、列挙項目の所属を読み取りやすくした。自然に読める文章はそのまま残し、文体の機械的な統一や全文の書き換えは行っていない。

製品コード、テストコード、設定、文書検査規則、ホワイトリスト、用語登録、検査対象の除外設定、ワークフローは変更していない。許可済みの技術用語を無理に翻訳せず、検査を避けるために引用やバッククォートを追加する処理も行っていない。

仕様の条件・例外、否定、数量・単位、処理順序、義務の強さ、確度、識別子、コード、コマンド、参照先を保持した。履歴文書では、当時の状態、日付、タスク ID、承認状態、完了条件、成功・失敗件数を維持した。現在の情報へ読み替えることで過去の未着手・未確認・失敗を解消済みと見せる変更は行っていない。

変更の単位と保持すべき事実は各採用案の `preserved_facts`、担当者の全文確認記録、保留記録で追跡できる。([採用案と保持事項](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json))

## 初回試行との区別

| 区分 | 初回試行 | 今回の追加対応 |
| --- | --- | --- |
| 確認範囲 | 2 文書から各 4 段落、合計 8 段落 | レポート以外の 19 文書の全文 |
| 修正件数 | 3 段落を修正 | 15 文書、84 件の置換を採用 |
| 原文維持 | 試行対象の 5 段落 | 4 文書を全体として原文維持。意味を推測しないための保持事項は別に 10 件 |
| 報告書 | [初回試行の報告](issue66-yomiyasu-pilot-20261008.md) | 本書 |

初回試行の対象は `Tracker/Design/Core/tracker-architecture-plan.md` と `Tracker/Tracker.DebugHost/README.md` である。今回の追加対応は、その試行を含む追加作業前 HEAD を出発点にしている。初回の 3 段落と今回の 84 件は数える単位も範囲も異なるため、合算して成果件数にしない。初回報告の検証結果を今回の追加変更に対する検証結果として流用しない。

## 全文を確認した文書

以下は、採用案記録にある全 19 文書である。件数は今回の追加文章改善の採用案を示す。作業状況の追記は数えていない。

| 文書 | 採用件数 | 保持事項 | 文章改善の扱い |
| --- | ---: | ---: | --- |
| [README.md](../README.md) | 0 | 0 | 全文確認後、原文維持 |
| [Tracker/Design/Archive/Core/phases-status.md](../Tracker/Design/Archive/Core/phases-status.md) | 2 | 0 | 改善を適用 |
| [Tracker/Design/Archive/Core/tasks-status.md](../Tracker/Design/Archive/Core/tasks-status.md) | 8 | 0 | 改善を適用 |
| [Tracker/Design/Archive/DebugHost/phases-status.md](../Tracker/Design/Archive/DebugHost/phases-status.md) | 0 | 1 | 全文確認後、原文維持 |
| [Tracker/Design/Archive/DebugHost/tasks-status.md](../Tracker/Design/Archive/DebugHost/tasks-status.md) | 3 | 1 | 改善を適用 |
| [Tracker/Design/Core/tracker-architecture-plan.md](../Tracker/Design/Core/tracker-architecture-plan.md) | 18 | 2 | 改善を適用 |
| [Tracker/Design/Core/tracker-core-engine-detail-design.md](../Tracker/Design/Core/tracker-core-engine-detail-design.md) | 7 | 0 | 改善を適用 |
| [Tracker/Design/Core/tracker-history-000-038.md](../Tracker/Design/Core/tracker-history-000-038.md) | 1 | 0 | 改善を適用 |
| [Tracker/Design/Core/tracker-test-maintainability-detail-design.md](../Tracker/Design/Core/tracker-test-maintainability-detail-design.md) | 5 | 0 | 改善を適用 |
| [Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md](../Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md) | 10 | 2 | 改善を適用 |
| [Tracker/Design/DebugHost/debug-host-maintainability-design.md](../Tracker/Design/DebugHost/debug-host-maintainability-design.md) | 3 | 1 | 改善を適用 |
| [Tracker/Design/DebugHost/raw-vision-viewer-plan.md](../Tracker/Design/DebugHost/raw-vision-viewer-plan.md) | 7 | 2 | 改善を適用 |
| [Tracker/Design/RuntimeHost/runtime-host-plan.md](../Tracker/Design/RuntimeHost/runtime-host-plan.md) | 3 | 0 | 改善を適用 |
| [Tracker/Design/phases-status.md](../Tracker/Design/phases-status.md) | 2 | 0 | 改善を適用 |
| [Tracker/Design/tasks-status.md](../Tracker/Design/tasks-status.md) | 6 | 1 | 改善を適用 |
| [Tracker/Tracker.CaptureReplay/README.md](../Tracker/Tracker.CaptureReplay/README.md) | 1 | 0 | 改善を適用 |
| [Tracker/Tracker.DebugHost/README.md](../Tracker/Tracker.DebugHost/README.md) | 8 | 0 | 改善を適用 |
| [feedback-points/feedback-points.md](../feedback-points/feedback-points.md) | 0 | 0 | 全文確認後、原文維持 |
| [tools/lint/README.md](../tools/lint/README.md) | 0 | 0 | 全文確認後、原文維持 |

合計は、採用 84 件、保持事項 10 件である。全文確認は各担当者の記録を集約したもので、集約担当者が全 19 文書を改めて全文レビューしたという意味ではない。各文書の確認元とメモは [採用案記録の files](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json) に記録した。

原文を維持した 4 文書は、ルートの README、Archive/DebugHost の phases-status、利用者の指摘記録、文書検査の README である。利用者の指摘記録は全文確認の範囲に含むが、既存の文書検査除外はそのまま適用される。このため、全文確認の 19 文書と後述の全文検査の 18 件には 1 件の差がある。

## 代表的な変更

### 観測時刻の比較条件を明示する

`CORE-ARCH-007` では、長く重なっていた修飾を条件文として整理した。

変更前：

> すでに確定処理済みの観測時刻より古い遅れて到着したパケットは診断に記録し、状態更新には使わない

変更後：

> 遅れて到着したパケットの観測時刻が、すでに確定処理した観測時刻より古い場合は、そのパケットを診断に記録し、状態更新には使わない

遅れて到着したパケットの観測時刻を、すでに確定処理した観測時刻と比較する条件が読み取りやすくなった。「古い場合」という比較、診断へ記録すること、状態更新には使わないことを維持した。([採用案 CORE-ARCH-007](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json))

### 未処理である対象を明確にする

`PRIMARY-RUNTIME-02` では、周期処理が取り出すパケットの説明を局所的に変更した。

変更前の該当部分：

> 未処理のカメラごとの最新パケットを受信時刻順に

変更後の該当部分：

> カメラごとの最新パケットのうち、未処理のものを受信時刻順に

「未処理」がパケットに掛かることを明確にした。カメラごとに最新パケットを保持すること、異なるカメラのパケットを単一の保存先への上書きで落とさないこと、受信時刻順に処理すること、検証済み設定値から実行周期を決めることは保持した。([採用案 PRIMARY-RUNTIME-02](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json))

### 過去の操作仕様を具体的な動詞で述べる

`DHP-009` では、Archive/Core の履歴文書にある再生操作の説明を変更した。

変更前：

> 再生ボタンは選択速度を尊重し、倍率を選択している場合は `FastForward` として開始する。

変更後：

> 再生ボタンを押したときも選択した速度を維持し、倍率を選択している場合は `FastForward` として開始する。

「尊重する」を、ボタンを押したときに選択速度を維持する動作として表した。倍率を選択している場合の開始条件と `FastForward` は維持した。当時の操作仕様を現在の仕様へ更新する変更は加えていない。([採用案 DHP-009](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json))

### 長い表示・記録項目を、その所属を保って分ける

DebugHost の README では、一文に続いていた表示内容や手動検証で記録する情報を、親項目の下に列挙した。右側の表示については 6 項目を同じ親の下にまとめ、比較表示の情報は 11 項目に、手動検証の手順 7 の情報は 15 項目に分けた。

3 種類の補助ファイルに共通する状態と記録・省略・エラー件数は、共通の修飾が 3 種類すべてに掛かるよう一つの小項目にまとめた。手順 7 の時刻と時刻差の単位 `ns`、項目の順序、UI の `Restored` / `Missing` の意味を記録する要求も維持した。これらの表示項目・記録項目は原文にあった内容である。([採用案 PRIMARY-DEBUG-01 / 03 / 06](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json))

## 実装前に補正した 4 件

文章を分割したり修飾を置き換えたりした結果が、原文の意味を変えていないか、周辺の定義と照合した。次の 4 件は補正してから採用した。いずれも実装案を整えるための補助点検であり、正式レビューの指摘や合否判定には分類していない。

| ID | 初期案の問題 | 採用前の補正と保持した内容 |
| --- | --- | --- |
| `DH-C01` | 「構成に分けられる」では、設計として分ける記述が可能性の説明へ弱まる。 | 「構成に分ける」に修正した。snapshot sidecar を受信パケットの主記録、alignment sidecar を再生用の索引とする役割と、対応付けの欠落・破損時に元の保存結果を独立に診断できることを維持した。 |
| `PRIMARY-RUNTIME-03` | 二文に分けた定義から「実時間処理」が脱落していた。 | 「トラッカーの実時間処理と将来の AutoRef mode」を同一プロセスで動かす定義へ戻した。実運用を想定し、画面を持たないことも保持した。 |
| `PRIMARY-DEBUG-03` | 列挙の分割によって、共通の状態・件数が alignment sidecar だけに掛かる構造になっていた。 | 先頭の 3 種類の補助ファイルと共通の状態・件数を一つの小項目へまとめた。 |
| `PRIMARY-DEBUG-06` | 最後の小項目とその後の段落に UI の `Restored` / `Missing` の記録要求が重複していた。 | 最後の小項目は元のバイト列の復元可否だけとし、UI の記録要求は手順 7 の本文に 1 回だけ残した。15 項目、単位、順序を保持した。 |

初期案、補正後の全文、補正理由、根拠となった周辺本文は、採用案記録の `applied_peercheck_adjustments` と相互点検記録に保存した。問題のない案は点検を理由に再編集していない。

根拠：[採用案記録](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json)、[DebugHost 設計書の相互点検](diagnostics/issue66-non-report-prose-20261008/host-proposals-peercheck.json)、[RuntimeHost・README の相互点検](diagnostics/issue66-non-report-prose-20261008/primary-proposals-peercheck.json)。

## 原文を維持した 10 項目

以下は、意味を推測で具体化しないために原文を残した項目である。今回の実装で解消すべき不具合として新しく登録したものではなく、仕様変更や追加の完了条件を要求する記録でもない。

| ID | 文書 | 原文を維持した理由 |
| --- | --- | --- |
| `CORE-HOLD-001` | [Tracker/Design/Core/tracker-architecture-plan.md](../Tracker/Design/Core/tracker-architecture-plan.md) | 「近くを優先する探索半径」は読み取りにくいが、範囲制限と候補の優先順位の関係を本文だけでさらに具体化すると条件を変える可能性があるため、原文を維持する。 |
| `CORE-HOLD-002` | [Tracker/Design/Core/tracker-architecture-plan.md](../Tracker/Design/Core/tracker-architecture-plan.md) | 「内容上の挙動を壊さない」を内容不変や形式不変へ言い換えると保証範囲を限定または強化してしまうため、原文を維持する。 |
| `DH-H01` | [Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md](../Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md) | TRACKER-062 の固定速度タブと TRACKER-063 の可変倍率入力が履歴別に記載され、完了条件にも固定タブ表現がある。現行仕様へ一本化するには履歴・仕様の判断が必要なため、文章改善では変更しない。 |
| `DH-H02` | [Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md](../Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md) | 最速の source cadence へ合わせる履歴記載と、RUNTIME-HOST-007 以降の diagnostics sample tick を使う記載を、文体整理を理由に統合しない。 |
| `DH-H03` | [Tracker/Design/DebugHost/raw-vision-viewer-plan.md](../Tracker/Design/DebugHost/raw-vision-viewer-plan.md) | 「未加工入力の更新周期を失わない保存境界」は、100 ms の既定値との関係を文章だけから一意に具体化できない。全入力の保存やイベント駆動など、新しい仕様を補わない。 |
| `DH-H04` | [Tracker/Design/DebugHost/debug-host-maintainability-design.md](../Tracker/Design/DebugHost/debug-host-maintainability-design.md) | 「順序制御が密」を複雑さや特定の依存関係へ置き換えると、元の説明の意味を狭める可能性があるため維持する。 |
| `DH-H05` | [Tracker/Design/DebugHost/raw-vision-viewer-plan.md](../Tracker/Design/DebugHost/raw-vision-viewer-plan.md) | 課題番号入りの既存見出し、脚注識別子、表示名、許可済みの英語技術用語は参照と意味を保持するため変更しない。 |
| `DHH-001` | [Tracker/Design/Archive/DebugHost/phases-status.md](../Tracker/Design/Archive/DebugHost/phases-status.md) | 「境界」が区別するものを細分化すると、当時の完了条件へ仕様を足すおそれがある。RAW-VISION-018本文には追跡フレーム確定周期への非依存などが書かれているが、この短い表セルへ新しい条件を加えず維持する。 |
| `DHH-002` | [Tracker/Design/Archive/DebugHost/tasks-status.md](../Tracker/Design/Archive/DebugHost/tasks-status.md) | 接続対象と接続先の具体化には、省略された実装上の対応を別途確定する必要がある。現在の資料内の語から推測して補わず、接続元と経由する処理を保持する。 |
| `DHH-003` | [Tracker/Design/tasks-status.md](../Tracker/Design/tasks-status.md) | 「文章規則を検出」が規則違反の検出を意図しているようには読めるが、当時の完了条件を変更しない補助点検として、検出対象を言い換える提案は保留する。 |

保持事項の原文または場所、理由、処置は [採用案記録の held](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json) に記録した。厳密な原文が指定された 5 件は、構造検査で原本と候補本文に各 1 回存在することを確認した。場所と理由で指定された残る 5 件は、その場所と保持理由を引き継いでいる。後者について、厳密な原文一致を機械検査したという扱いにはしていない。([held_checks](diagnostics/issue66-non-report-prose-20261008/proposal-integrity.json))

## 検証

### 原本・採用案・候補本文の整合性

提案の整合性検査では、参照原本 29 ファイルのサイズと SHA256 を、取得時の記録と照合した。内訳は対象の Markdown 19 文書と、設定・依存・ワークフローなどの検査用入力 10 ファイルである。元のバイト列と提案の入力ファイルが検査中に変わっていないことも確認した。

本文比較時に限り、UTF-8 の先頭 BOM を取り除き、CRLF / CR を LF にそろえた。原本のバイト列自体は保存したままである。この比較用本文に 84 件の置換をメモリ上で適用し、原文の一致、置換範囲の非重複、見出し、リンク先、行内コード、URL、脚注識別子、数値、コードブロックを照合した。全 19 文書で、見出しと Markdown リンク先は一致し、保護対象の値と個数も一致した。検査の失敗件数は 0 件だった。

字句の順序変更は 2 件を意図した変更として記録した。`DH-C08` では `DiagnosticsFieldViewFactory` と `TrackerPacketSnapshotSemanticSummary` の説明上の語順を変えた。`PRIMARY-RUNTIME-01` では文の分割に伴い `headless-host` の脚注参照位置を前へ移した。識別子と参照先は保持し、処理の実行順序を変更したものとは扱っていない。

この検査は採用案の構造を確認するものであり、意味保持の機械的な証明ではない。候補本文はメモリ上だけで構築しており、この時点で PC のファイル適用、文書検査、製品テストを実行したという意味でもない。これらの実行結果は次節以降に分けて記録する。

`proposal-integrity.json` の `root_selection_status` は `pending_root_decision` のままである。これは実装担当による採用判断前に作成した検査記録の状態を保持したものだ。その後の採用判断は、`selected-proposals.json` の `accepted_for_application` に記録している。旧記録の状態を後から成功へ書き換えていない。

根拠：[整合性検査の全文](diagnostics/issue66-non-report-prose-20261008/proposal-integrity.json)、[採用判断と意味の再確認記録](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json)。

### 文書検査の実行範囲と結果

実装担当が実行証跡で確認した結果は、次のとおりである。旧作業環境での失敗と、追跡対象を複製した環境での結果を、それぞれの対象範囲とともに残す。

| 実行対象 | 全文検査の対象数 | 確定した結果 | 診断記録 |
| --- | ---: | --- | --- |
| 旧作業環境 | 205 件 | 3 種類の文書検査が失敗。作業用・外部文書 187 件が検査対象に混入していた。 | [before/results.json](diagnostics/issue66-non-report-prose-20261008/before/results.json) |
| 追加作業前の追跡対象を複製した環境 | 18 件 | 基準となる 3 種類の文書検査がすべて成功。 | [baseline-clean/results.json](diagnostics/issue66-non-report-prose-20261008/baseline-clean/results.json) |
| 改善候補の追跡対象を複製した環境 | 18 件 | 3 種類の文書検査と `git diff --check` がすべて成功。検査中に全追跡ソースの hash が変わっていないことも確認。 | [candidate-clean/results.json](diagnostics/issue66-non-report-prose-20261008/candidate-clean/results.json) |
| 進捗の追記を含む最終全文検査の初回実行 | 実行記録を参照 | `tasks-status.md` の新規追記に対してホワイトリスト検査だけが失敗。`textlint`、`CSpell`、`git diff --check` は成功。 | [final-clean/results.json](diagnostics/issue66-non-report-prose-20261008/final-clean/results.json) |
| 進捗の 2 語を修正した後の全文検査 | 18 件 | ホワイトリスト、`textlint`、`CSpell` と `git diff --check` がすべて終了コード 0。2026-10-08 12:38:21 UTC 完了。検査中の全追跡ソースは不変。 | [final-fixed/results.json](diagnostics/issue66-non-report-prose-20261008/final-fixed/results.json) |

複製環境は、検査に必要な追跡対象をバイト一致で複製し、既存の外部依存を再利用した。文書検査の設定や除外規則は変更していない。利用者の指摘記録には既存の除外を適用したため、対象は 18 件となる。

旧環境の 205 件には本来の追跡対象以外の 187 件が含まれていたが、この対象混入の確認を理由に旧環境の失敗を削除したり、成功へ読み替えたりはしない。コマンドと終了結果は各 `results.json` に、標準出力・標準エラーを含む全実行記録は [validation-logs.zip](diagnostics/issue66-non-report-prose-20261008/validation-logs.zip) にまとめる。

改善候補の検査対象の dirty fingerprint は `a42183a2510e26cfd4132aab2e203f13aaeb7cd2d8793508bc35ad849864d1bf` である。検査中の全追跡ソースの hash 不変確認は `true` だった。これは当該検査中に対象が変化していない証拠であり、後で報告書や診断記録を追加した状態に対する最終全文検査の結果とは区別する。

### 文章診断の指摘数と扱い

文章診断の指摘数は、追加作業前の基準で 224 件、改善候補で 225 件だった。改善候補には今回の進捗欄の追加があるため、採用した 84 件だけの変化を表す数として扱わない。新しく現れた該当箇所は、必要な否定・対比の説明と箇条書きの比率について文脈を確認し、保持した。

この指摘数は、意味保持や読みやすさの合否を示す値ではない。指摘を減らすために必要な条件や否定を削ったり、技術用語を追加登録したりはしていない。採用の根拠は、具体的な読みづらさを直せることと、原文の意味を保てることを、前後の本文と併せて確認した結果である。実行の詳細は基準と候補の診断記録を参照する。

### 進捗の用語修正と文書検査の成功

進捗記録の `tasks-status.md` に追加した「コード」「リンク先」が許可一覧に未登録だったため、最終全文検査の初回実行ではホワイトリスト検査が失敗した。同じ実行の `textlint`、`CSpell`、`git diff --check` は成功した。この結果は [final-clean/results.json](diagnostics/issue66-non-report-prose-20261008/final-clean/results.json) に残した。

実装担当は 2 語を既存語の「ソースコード」「URL」へ修正した。検査設定を維持して全文 18 件を再検査し、ホワイトリスト、`textlint`、`CSpell` と `git diff --check` はすべて終了コード 0 だった。再検査は 2026-10-08 12:38:21 UTC に完了した。実行時の HEAD は `506c5342f646e0392532ff02f429196254e8f670`、dirty fingerprint は `b72b345b431a1b8fee9e1d8a4be91096108f249bb641a3e6576de9b9f268be90` で、検査中の全追跡ソースの hash 不変確認は `true` だった。([final-fixed/results.json](diagnostics/issue66-non-report-prose-20261008/final-fixed/results.json))

この成功は、識別した 18 件の文書検査と差分の書式検査に対する結果である。製品テストの結果と、報告書・診断記録を含む後続の管理用コミットの検証はそれぞれ別に記録する。

### 製品テストの初回失敗と固定依存の初期化

製品テストの初回実行は、2026-10-08 12:30:16 UTC に終了コード 1 で終了した。コンパイル時に `CS0246` が発生した。実装担当は、`SslProto` が利用する 2 つの submodule が未初期化で、型の生成元が存在しないことを確認した。初回の失敗は [product-gate/results.json](diagnostics/issue66-non-report-prose-20261008/product-gate/results.json) に残した。

CI の submodule 取得方法に合わせて、自身の作業領域で固定 gitlink を初期化した。初期化は終了コード 0 だった。取得した対象は次のとおりである。

| submodule | 固定された SHA |
| --- | --- |
| `game-controller` | `9b0765fe7a07a55124f6ee957d4559348462d227` |
| `simulation` | `1e719a8d410cc84847ec69104da44fed13738291` |

初期化の終了状態と出力は [validation-logs.zip](diagnostics/issue66-non-report-prose-20261008/validation-logs.zip) に含める。文章改善の実装コミット `506c5342f646e0392532ff02f429196254e8f670` の差分は Markdown に限られ、製品のソースコード、テスト、依存の固定値、その他の既存の非 Markdown ファイルは変更していない。後続の管理用コミットでは、本書・診断記録・引継ぎを追加する。

### 製品テスト再実行の結果

固定依存を初期化した後の再実行は、2026-10-08 12:39:04 UTC に完了した。実行時の HEAD は `506c5342f646e0392532ff02f429196254e8f670`、dirty fingerprint は文書検査と同じ `b72b345b431a1b8fee9e1d8a4be91096108f249bb641a3e6576de9b9f268be90` だった。

| 実行数 | 成功 | 失敗 | スキップ | 判定 |
| ---: | ---: | ---: | ---: | --- |
| 329 | 318 | 11 | 0 | 製品テストは失敗 |

11 件はいずれも、開いている補助ファイルを読み取る際の `System.IO.IOException` であり、Windows のファイル共有に関する失敗だった。実行対象と集計は [product-gate-retry/results.json](diagnostics/issue66-non-report-prose-20261008/product-gate-retry/results.json)、個別の失敗は [product-failures.json](diagnostics/issue66-non-report-prose-20261008/product-failures.json)、標準出力・標準エラーと詳細な実行記録は [validation-logs.zip](diagnostics/issue66-non-report-prose-20261008/validation-logs.zip) にまとめる。

### 変更前の製品テストとの比較

比較対象は追加作業前の HEAD `c9c44913d443410ae4768b26faafd48fa367b2e2` である。同じテストを別の worktree で実行し、今回の 11 件と比較した。比較対象の実行結果は [product-baseline-final/results.json](diagnostics/issue66-non-report-prose-20261008/product-baseline-final/results.json)、対応付けと結論は [baseline-comparison.json](diagnostics/issue66-non-report-prose-20261008/baseline-comparison.json) を根拠とする。

変更前コミット `c9c44913d443410ae4768b26faafd48fa367b2e2` を独立した比較領域で検証したところ、329 件中 318 件成功、11 件失敗、スキップ 0 件だった。変更後と失敗したテスト名が全件一致し、いずれも開いている補助ファイルを読み取る際の `System.IO.IOException` だった。2つのコミット間で製品コード・テスト・設定は変わっていない。今回の文章変更以前から同じ Windows のファイル共有エラーが再現することを確認した。この失敗は成功へ読み替えず、製品コードの修正は今回の文章改善へ混ぜていない。比較の全文は [baseline-comparison.json](diagnostics/issue66-non-report-prose-20261008/baseline-comparison.json)、変更前の実行記録は [product-baseline-final/results.json](diagnostics/issue66-non-report-prose-20261008/product-baseline-final/results.json) を参照する。

### 検証状態と公開状態

| 項目 | 本書で確定している状態 |
| --- | --- |
| 採用案の整合性検査 | 成功。構造検査であり、意味保持の機械的な証明ではない。 |
| 修正後の文書 18 件の検査 | ホワイトリスト、`textlint`、`CSpell`、`git diff --check` はすべて終了コード 0。 |
| 製品テスト再実行 | 329 件実行、318 件成功、11 件失敗、スキップ 0 件。 |
| 文章改善の実装コミット | `506c5342f646e0392532ff02f429196254e8f670` を作成済み。 |
| 最終的な文書・報告・診断記録の保存と管理用コミット | 本書・診断・引継ぎを保存。管理用コミットは本書保存後に作成する。 |
| push | 未完了。 |
| 公開後の対象 HEAD に対する CI | 未確認。 |
| 正式な通常レビュー・独立レビュー | 未実施。 |
| 利用者の受入・merge | 未実施。 |

文書検査と製品テストを合わせたローカルの全検証を、成功とは判定していない。管理用コミット、push、公開後の対象 HEAD に対する CI は、後続の PR コメントで対象 SHA・実行・ジョブ・成果物と結果を対応付けて確定する。報告書の自己参照 SHA を追加するためのコミットは作成しない。

## 証跡の配置と残る作業

診断記録の保存先は `reports/diagnostics/issue66-non-report-prose-20261008/` である。結果 JSON は個別ファイルとして保存し、標準出力・標準エラーと実行記録は `validation-logs.zip` にまとめる。実装担当が保存後に実在するファイルと本書の参照先を照合する。各コマンドの全文は結果 JSON と ZIP 内の記録を参照する。

| 記録 | 内容 |
| --- | --- |
| [selected-proposals.json](diagnostics/issue66-non-report-prose-20261008/selected-proposals.json) | 全 19 文書の確認記録、84 件の採用案、4 件の補正、10 件の保持事項、実装担当の採用判断。 |
| [proposal-integrity.json](diagnostics/issue66-non-report-prose-20261008/proposal-integrity.json) | 29 ファイルの原本照合、置換範囲、保護対象、見出し・参照先、保持事項の構造検査と限界。 |
| [core-proposals.json](diagnostics/issue66-non-report-prose-20261008/core-proposals.json) / [host-proposals.json](diagnostics/issue66-non-report-prose-20261008/host-proposals.json) / [primary-proposals.json](diagnostics/issue66-non-report-prose-20261008/primary-proposals.json) / [history-proposals.json](diagnostics/issue66-non-report-prose-20261008/history-proposals.json) | 各担当の原提案、全文確認の範囲、提案理由と保持する事実。 |
| [core-proposals-peercheck.json](diagnostics/issue66-non-report-prose-20261008/core-proposals-peercheck.json) / [host-proposals-peercheck.json](diagnostics/issue66-non-report-prose-20261008/host-proposals-peercheck.json) / [primary-proposals-peercheck.json](diagnostics/issue66-non-report-prose-20261008/primary-proposals-peercheck.json) | 改善案を周辺の定義と照合した補助点検。 |
| [history-proposals-check.json](diagnostics/issue66-non-report-prose-20261008/history-proposals-check.json) | 履歴文書の提案に対する局所的な構造検査。途中の失敗と補正を含む。 |
| [before/results.json](diagnostics/issue66-non-report-prose-20261008/before/results.json) | 旧作業環境の 205 件を対象とした検査の失敗。 |
| [baseline-clean/results.json](diagnostics/issue66-non-report-prose-20261008/baseline-clean/results.json) | 追加作業前の追跡対象 18 件に対する文書検査。 |
| [candidate-clean/results.json](diagnostics/issue66-non-report-prose-20261008/candidate-clean/results.json) | 初回候補の追跡対象 18 件に対する文書検査。 |
| [final-clean/results.json](diagnostics/issue66-non-report-prose-20261008/final-clean/results.json) | 進捗の追記に含まれた 2 語によるホワイトリスト検査の失敗。 |
| [final-fixed/results.json](diagnostics/issue66-non-report-prose-20261008/final-fixed/results.json) | 2 語の修正後、文書 18 件の検査と `git diff --check` が成功した結果。 |
| [product-gate/results.json](diagnostics/issue66-non-report-prose-20261008/product-gate/results.json) | 製品テストの初回失敗。`CS0246` と終了コード 1。 |
| [product-gate-retry/results.json](diagnostics/issue66-non-report-prose-20261008/product-gate-retry/results.json) | 固定依存の初期化後の製品テスト。329 件中 318 件成功、11 件失敗。 |
| [product-failures.json](diagnostics/issue66-non-report-prose-20261008/product-failures.json) | 製品テストの個別の失敗。 |
| [product-baseline-final/results.json](diagnostics/issue66-non-report-prose-20261008/product-baseline-final/results.json) / [baseline-comparison.json](diagnostics/issue66-non-report-prose-20261008/baseline-comparison.json) | 変更前の製品テストの結果と、今回の失敗との比較。 |
| [validation-logs.zip](diagnostics/issue66-non-report-prose-20261008/validation-logs.zip) | 検査・初期化・製品テストなどの標準出力、標準エラー、詳細な実行記録。 |

本書が参照した主要な入力の SHA256 は次のとおりである。

- `selected-proposals.json`: `cf6f38aaaf16678cd339369434f43a28753aba2248e58c86c3afea001927f7f7`
- `proposal-integrity.json`: `8332be4ab126f739122e8860c1738e1e9ac3182e28c8bca12d729241a61bd2dc`

変更前の製品テストとの比較結果を反映し、報告と診断記録を保存した。残る作業は管理用コミットを作成すること、push 後に PR の対象 HEAD と CI の結果を対応付けることである。公開までの後続結果は PR コメントで確定し、初回の失敗を含む実行履歴を保持する。

## 実行場所と公開時の照合

実行は接続PCの専用作業領域で行った。本文の全文検査は、追跡対象の全ファイルをバイト一致で複製した領域を作業ディレクトリにした。依存と検査設定は各結果JSONに記録している。変更前比較では専用worktreeでビルドし、固定コミットで内容が変わっていない依存ソースを参照した。基準比較の準備時エラーは operational-diagnostics.json に区別して記録した。

最終公開前に、文書検査が実際に読んだ全ファイルのハッシュと公開する本文を照合する。報告、診断、引継ぎの追加は製品コードを変えず、既存の文書検査設定が定める18文書も変えない。新しい報告書の参照先、JSON形式、差分の空白は別途検査する。ローカル製品テストは前後で同じ11件が失敗しており、ローカル検証全体を成功とは記録しない。最終HEADに対応するLinuxのCI結果は、公開後のPRコメントに記録する。
