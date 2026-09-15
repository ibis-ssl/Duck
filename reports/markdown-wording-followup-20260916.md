# Markdown用語レビュー指摘への対応報告

## 状態と対象

指摘対応は部分完了。修正を公開したが、全体lintと全許可語の出現単位照合は未完了であり、再レビュー合格とは扱わない。

対象は ibis-ssl/Duck PR #20、作業 `DOC-LINT-003`。PRブランチは `docs/runtimehost-readme-appsettings`、baseは `main`。元レビュー対象は `42e063760f90987262e397b43ba084c9ee60b944`、対応開始時点は `53796bcfa789ced28f1034f445c2d24dff6d175a`。本文・履歴保全の公開HEADは `a19ecc0dd5057480cdcea243527153b050f1ded1`。

## 実施内容

新たに許可した表記は「フィールド」「設定プロファイル」「キャプチャー」の3種。既存の許可表記は維持し、「競技フィールド」は別表記として残した。「プロファイル」「キャプチャ」「サブエージェント」を新たに許可していない。

入力番号は source frame number（入力の観測フレームの番号）に戻した。raw vision は SSL-Vision の検出情報であり、画像・動画そのものではないと説明した。observer/event は「イベントと通知先の契約」とし、`TrackerEvent` と `ITrackerObserver` の双方を明記した。

設定プロファイル、フィールド、通信内容のキャプチャーの表記を本文へ反映した。Kalman filter、process noise、measurement noise、timeline scrubber、render snapshot、replay timeline、diagnostics sample tick、Field source の名称を該当説明へ戻した。画面の実名 `Vision Input` / `Tracker Output` を保持し、重複した語句も修正した。製品のソースコード、設定キー、型名は変更していない。

直接引用は「次回からレビューレポートの編集許可をサブエージェントに渡すようにしてください」へ復元した。引用をlint都合で改変しない。

履歴の要約だけでは元の情報を復元できないため、Core / DebugHost の tasks / phases の変更前原文4ファイルを `reports/history/` にバイト単位で保存した。元の履歴文書から参照できる。基準は `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`、合計93,712 bytes。原文中の条件・検証結果・参照先を省略していない。現在の履歴本文は要約を含むため、要約自体の完全性や、この保存方法の採否は再レビューが必要。

## 指摘別の対応状況

| 指摘 | 重要度 | 反映と残件 |
| --- | --- | --- |
| WR-001 | medium | 入力のsource frame numberを復元。製品の入力番号・出力番号は変更なし。 |
| WR-002 | medium | raw visionの名称と意味説明を復元。映像との混同を修正。 |
| WR-003 | medium | イベントと通知先を区別し、両方の型名を明記。 |
| WR-004 | medium | 設定プロファイルを登録し、設定組から変更。設定キー・表示実名は維持。 |
| WR-005 | medium | フィールドを登録して本文へ反映。既存の競技フィールドも許可表記として維持。 |
| WR-006 | medium | キャプチャーを登録・本文へ反映。ただし未承認の短い表記を検査器が拒否しない問題が残る。 |
| WR-007 | medium | 代表的な許可済み名称を復元。全301項目・元の全出現箇所についての採否表は未完成。 |
| WR-008 | medium | `Vision Input` / `Tracker Output` の実表示名を復元。 |
| WR-009 | low | 約約、状態推定の重複等を修正。 |
| WR-010 | medium | 直接引用を原文に戻した。引用中のサブエージェントが許可一覧検査に残る。 |
| WR-011 | medium | 原文4ファイルを欠落なく保存し参照を追加。要約本文の全面的な再翻訳ではない。 |

上の状況は実装担当の反映報告であり、元指摘の重要度・判定を変更しない。元レビュー全文は [レビュー報告](markdown-wording-review-20260916.md)、全出現箇所は [照合元JSON](markdown-wording-audit-20260916.json) を参照する。

## 検証結果

| 検証 | 結果 |
| --- | --- |
| 全19文書の `npm run lint:md` | 終了値1。全体は未通過。 |
| textlint / cspell | 終了値0。cspellは19文書・指摘0件。 |
| 引用文書以外の18文書の許可一覧検査 | 終了値0。全体成功の代用ではない。 |
| 許可一覧の説明文検査 | 終了値0。 |
| `git diff --check` | 終了値0。 |
| 内容保全・許可範囲等の補助検証 | 39項目中38項目成功、1項目失敗。製品テスト件数ではない。 |
| 短い「キャプチャ」の拒否 | 期待終了値1に対し実際は0。未承認の別表記を許可する検査器の制限として残る。 |
| ローカル.NETテスト | この文書修正では実行していない。 |

全体lintの残りは `feedback-points/feedback-points.md:12` の原文引用内「サブエージェント」1件。引用の変更、未承認語追加、引用文書の恒久除外は行っていない。短いキャプチャを拒否するprh設定も試したが、textlint経由の検証に通らなかったため公開せず、設定を変更前へ戻した。

検証はRDCのLinux端末 `75840a70-dea0-4d8c-9bb4-fe97c78bc11e`、`/bin/bash`、`/home/ibis/.local/share/duck-pr20-review-followup-20260916` で実行した。分離した作業ブランチは `work/pr20-review-followup-20260916`。他作業のセッションや作業ツリーを変更・終了していない。コマンド投入前にlist_sessionsを確認した。

検証時点の基準HEADは `d635575b879966171832b27082b130174985076f` だが、未コミットの本文・進捗・履歴変更を含む。内容識別値は `dc7f4540c6c7580451cd01be5e01d059b95bee8e379cabe89d7d454e8bea23ff`。各公開グループは検証JSONのファイル別SHA-256との一致を確認してからコミット・pushした。基準HEADだけの検証成功とは扱わない。

## 診断記録とCI

結果、標準出力、標準エラー、内容識別一覧は `reports/diagnostics/markdown-wording-followup-20260916/` に保存する。検証スクリプトと、成功・失敗のログを含む。端末上の途中記録は `/home/ibis/.local/share/duck-pr20-review-followup-evidence-20260916/resume/` に残す。この端末パスはチャットのダウンロードリンクではない。

npm依存物と共有検査器は既存環境への参照を利用した。Pythonは既存の専用仮想環境を利用した。共有検査器そのものを検証実行時の内容識別一覧へ含めていないため、完全に固定された独立環境での再現を証明するものではない。

開始時に確認した診断workflowは `origin/main` の `d9ca3eef62cc644a37adb8b643cc1cea7ad4a171` にある `.github/workflows/dotnet-test.yml`。テスト失敗時にTRX、stdout、stderr、VSTest診断、MSBuild binlog、終了値、環境情報とソースを保存する設定を確認した。一方、PRのHEADツリーには同workflowが無く、Actions runには同パスが記録される。実際に選択されたworkflowの由来は未確認事項として残す。今回はworkflowを変更していない。

報告・進捗同期のコミットはこの本文作成時点では `commit_pending` / `push_pending`。技術HEADと行政的な記録の親は `a19ecc0dd5057480cdcea243527153b050f1ded1`。最終公開HEADに完全一致する `pull_request` runだけをCI確認対象とし、結果は公開後のPRコメントに記録する。旧HEADの成功を代用しない。

## 未完了事項と次の対応

WR-007は、元の許可一覧301項目について各出現箇所の同義対応・採否・理由を記録する作業が残る。文書中に同じ語がどこか一箇所あるという補助検証だけでは、元の全出現箇所の対応完了を意味しない。

WR-006は、未承認の短いキャプチャを許可する共有検査器の挙動が残る。短い表記を追加承認されたものとしては扱わない。WR-010は引用原文を維持したまま未登録語をどう扱うかの判断が必要。現時点の承認は3表記だけであり、勝手に許可範囲を広げない。

WR-011の原文保全方法と本文の要約、ほかの修正も再レビュー未実施。指摘なしや独立レビュー合格を宣言しない。全301項目の照合と引用の扱いが決まった後、全対象のlint、同一HEADのCI、再レビューが必要。

製品ソース、共有CodexSkill、認証設定、他作業ツリーは変更していない。元レビュー報告と既存の検証履歴を上書きしていない。マージは行っていない。

## 元レビュー指摘（原文を保持）


### WR-001 [medium] source frame number を追跡結果の番号へ変えており、入力と出力を取り違えている

- 起点: `introduced_by_change`。箇所: `Tracker/Design/Core/tracker-core-engine-detail-design.md:355`。
- 内容: 変更前の source frame number が「入力元の追跡結果の番号」へ変更された。ホワイトリストでは入力元の検出単位の番号と定義し、追跡器が出す番号とは区別している。
- 影響: 入力の並べ替え条件を出力の追跡番号と誤読させ、保守時に別の番号を使う危険がある。
- 根拠: tools/lint/markdown-whitelist.yaml:83-84。Tracker/Tracker.Core/Engine/TrackerEngine/DetectionBuffer.cs:11-19 は SSL_DetectionFrame.FrameNumber を SourceFrameNumber として保持し、FrameCommit.cs:21-27 は EventTimestampNs、CameraId、SourceFrameNumber の順で並べる。出力番号は FrameCommit.cs:183 の nextCommittedFrameNumber++。
- 必要な対応: source frame number を維持し、必要なら「入力の観測フレーム番号」を説明として添える。入力番号と追跡番号を区別した本文へ修正し、同種の frame の訳を横断確認する。

### WR-002 [medium] raw vision を未加工映像とすると、座標などの検出情報と画像・映像を混同させる

- 起点: `introduced_by_change`。箇所: `Tracker/Design/Core/tracker-architecture-plan.md:268,347,474,487`。
- 内容: 許可済みの raw vision が「未加工映像」「未加工映像パケット」へ変更された。追跡エンジンが受け取るのは SSL_WrapperPacket であり、本文が扱う検出情報を映像データそのもののように表している。
- 影響: トラッカーがカメラ映像を直接処理する設計に読め、SSL-Vision と Tracker.Core の責務境界が不明確になる。
- 根拠: Tracker/Tracker.Core/Engine/ITrackerEngine.cs:9-16 の Update は SSL_WrapperPacket を受け取る。DetectionBuffer.cs:11-19 は SSL_DetectionFrame の Balls、RobotsYellow、RobotsBlue を取り込む。ホワイトリストには raw vision と raw vision viewer が登録されている。
- 必要な対応: 許可済みの raw vision を残し、初出で「SSL-Vision の検出情報」と説明する。「未加工映像」は実際に画像や動画を意味する場合だけに使う。関連 README、設計、脚注も同じ意味へ揃える。

### WR-003 [medium] observer-event の通知先とイベントデータの区別が訳から落ちている

- 起点: `introduced_by_change`。箇所: `Tracker/Design/Core/tracker-history-000-038.md:15; Tracker/Design/Core/tracker-architecture-plan.md:1130,1154`。
- 内容: 履歴では「通知イベント契約」、設計のタスク説明では「通知を受け取る処理契約」へ変わっている。前者はイベント中心、後者は通知先中心であり、同じ observer/event 契約を一貫して表していない。単に完全な誤訳とは断定しないが、両方を表すには不足する。
- 影響: ITrackerObserver の受信側契約と TrackerEvent の通知データ契約のどちらを実装・維持すべきか曖昧になる。
- 根拠: ITrackerObserver.cs:6-36 は OnProfileSwitched などの通知先メソッドを定義する。TrackerUpdateResult.cs:16,38-54 は EmittedEvents と TrackerEvent を別に定義する。変更前の履歴も domain event、observer 契約を別々に挙げていた。
- 必要な対応: 「イベントと通知先の契約」とし、必要な箇所で ITrackerObserver と TrackerEvent を明記する。イベントの発行順序と通知先への配送順序も混同しない。

### WR-004 [medium] profile switch / profile を設定組へ置き換えず、設定プロファイルで統一する

- 起点: `introduced_by_change`。箇所: `README.md:56,58-75; Tracker/Tracker.DebugHost/README.md:73-78,420-424; tools/lint/markdown-whitelist.yaml:187-190`。
- 内容: 実際の HEAD の表記は主に「設定組」「設定組の切り替え」「設定組切り替え」であり、利用者が例示した「設定組入れ替え」という完全一致文字列ではない。profile switch と runtime profile は許可済みだが本文から説明名が消えている。
- 影響: Profiles、ActiveProfileName、Profile Control と本文の対応を追いにくくし、許可後も不自然な造語を強いる状態になっている。
- 根拠: ホワイトリスト:187-190、DebugHost README:75 の Profile Control、:281 の ActiveProfileName、:285 の Profiles と本文を照合した。
- 必要な対応: 利用者指定の「設定プロファイル」を意味付きで登録し、名詞は設定プロファイル、操作は設定プロファイルの切り替えに統一する。profile switch を併記する場合は既存許可を利用する。設定キー、型名、画面の実名は変えない。

### WR-005 [medium] 競技場をフィールドへ揃え、許可一覧も本文と整合させる

- 起点: `introduced_by_change`。箇所: `Tracker/Design/DebugHost/raw-vision-viewer-plan.md:278,292,316; tools/lint/markdown-whitelist.yaml:50-52,747-748`。
- 内容: 通常の field 表示が多数の箇所で「競技場」「競技場描画」に変わっている。現在の許可一覧にある片仮名表記は「競技フィールド」で、単独の「フィールド」は登録されていない。
- 影響: フィールド表示という対象を大がかりな競技施設のように表し、利用者が指定した表記にも合わない。単に本文だけを戻すと現行の許可一覧に合わない。
- 根拠: 変更された非 report Markdown 19文書の文字列集計で、競技場は base の1出現から HEAD の288出現になり、ホワイトリスト説明にも17出現ある。集計は出現数であり、不具合件数ではない。
- 必要な対応: 利用者指定どおり競技領域・描画領域の意味で「フィールド」を許可し、その意味の「競技場」を変更する。データ構造の field はデータ項目として分ける。Field source など実際の画面名や許可済み名称は保持する。

### WR-006 [medium] キャプチャーの追加承認を反映し、傍受や受信記録への過剰な言い換えを整理する

- 起点: `pre_existing`。箇所: `Tracker/Design/Core/tracker-architecture-plan.md:123,125,526; Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md:12,26,32,42; tools/lint/markdown-whitelist.yaml:113-116`。
- 内容: 「傍受」は変更前にも存在しており、この PR がキャプチャーを傍受へ新しく訳したとは言えない。一方、許可済み packet capture は「受信記録本体」などへ変わり、キャプチャーは未登録である。今回は利用者が明示的に追加を承認した。
- 影響: 通信内容を取得・保存する操作と、単なる受信ログが同じ呼称になり、保存対象や操作が読み取りにくい。
- 根拠: architecture-plan.md:526 は受信した TrackerWrapperPacket を記録用フォルダの JSONL に保存する説明。変更前後とも傍受と書いている。cli-ui-detail-design.md:42 は packet capture 本体から受信記録本体へ変更。
- 必要な対応: 「キャプチャー」を通信内容の取得・保存の意味で追加し、該当する傍受・packet capture の説明へ使う。受信そのものや診断ログまで一律にキャプチャーへ変えない。キャプチャという別表記の追加は自動的な承認とみなさない。

### WR-007 [medium] 許可済み語を許可後も別の説明へ置き換えており、名称と本文が一致していない

- 起点: `introduced_by_change`。箇所: `Tracker/Design/Core/tracker-architecture-plan.md:596,628-634,719,752,758,854; Tracker/Tracker.DebugHost/README.md:83; Tracker/Design/DebugHost/debug-host-maintainability-design.md:14`。
- 内容: 代表例は Kalman filter → カルマン状態推定、process noise → プロセス側の揺らぎ、measurement noise → 観測側の揺らぎ、timeline scrubber → 再生位置操作部、render snapshot → 描画時点の記録。許可済みの名称を避ける必要はない。
- 影響: 推定方式、設定項目、記録形式や画面部品の名前をコード・許可一覧と結び付けにくくする。状態推定のような広い言葉では、方式名という識別情報も弱くなる。
- 根拠: whitelist:467-476 は Kalman filter / process noise / measurement noise をソース識別子付きで許可。:173-174 は timeline scrubber、:353-357 は render snapshot を許可。before/after の全登録語の出現行は同梱の audit JSON に記録。
- 必要な対応: 変更前に現れる許可済み英語・片仮名語を audit JSON から追い、同じ意味なら許可済みの標準表記・別表記を残す。まず上記5種と profile、raw vision、source frame number、Field source、replay timeline、diagnostics sample tick を本文に対応付ける。日本語の説明は名称を消す代替ではなく補足として添える。自然な日本語まで機械的に不具合とは数えず、未対応の語は個別に採否と根拠を記録する。

### WR-008 [medium] 画面の実際の表示名まで翻訳され、操作対象との対応が失われている

- 起点: `introduced_by_change`。箇所: `Tracker/Design/DebugHost/raw-vision-viewer-plan.md:292; Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor:404,421`。
- 内容: Vision Input / Tracker Output の文字列確認という説明が「映像入力とトラッカー出力の文字列の確認」に変更された。実画面の見出しは Vision Input / Tracker Output のままである。
- 影響: どの表示領域を確認する要件なのか識別しにくくなり、実際の表示名を残す方針に反する。
- 根拠: Diagnostics.razor:404,421 の h2 見出しを直接確認。Vision Input はホワイトリスト登録済み。Field source も実装の aria-label と許可一覧に存在する。
- 必要な対応: 画面表示名を引用するときは Vision Input / Tracker Output を保持する。Field source 等も実画面・識別名として参照する箇所では英語を残し、必要なら括弧で日本語を説明する。画面名ではない通常説明と分けて直す。

### WR-009 [low] 置き換えで語句が重複している

- 起点: `introduced_by_change`。箇所: `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md:138; Tracker/Design/Core/tracker-architecture-plan.md:885`。
- 内容: 「約約33.3 ms」と「カルマン状態推定の状態推定」が新しく生じている。
- 影響: lint が通っても読みやすさを満たしていないことを示す、明確な文章上の欠陥。
- 根拠: base の cli-ui:136 は「約33.3msごとの」、HEAD:138 は「約約33.3 msごとの」。architecture:885 は Kalman ベースの状態推定からカルマン状態推定の状態推定へ変更。
- 必要な対応: 重複を除き、許可済み名称を使って文章全体を読み直す。数値、単位、否定、条件、列挙範囲も置き換え後に維持されているか確認する。

### WR-010 [medium] 利用者発言の直接引用まで別の文章に書き換えている

- 起点: `introduced_by_change`。箇所: `feedback-points/feedback-points.md:12`。
- 内容: 引用内の「レビューレポート」「サブエージェント」が「点検報告書」「委譲先」に変わっている。用語整理のために原発言そのものを改変している。
- 影響: 将来の判断根拠となる引用が元の発言と一致しなくなり、指示履歴の信頼性を損なう。引用の周囲にある要約の変更とは異なる。
- 根拠: base:12 は「次回からレビューレポートの編集許可をサブエージェントに渡すようにしてください」。HEAD:12 は「次回から点検報告書の編集許可を委譲先に渡すようにしてください」。レビュー、レポート、エージェントは許可済み。
- 必要な対応: 直接引用は原文へ戻す。用語説明が必要なら引用の外に置く。引用内の未許可語を対応するときも原発言を改変せず、既存の承認方針に沿った扱いを別に決める。

### WR-011 [medium] 用語の置き換えを越えて、履歴の検証根拠と参照先が削除されている

- 起点: `introduced_by_change`。箇所: `Tracker/Design/Archive/Core/tasks-status.md:48-69; Tracker/Design/Archive/DebugHost/tasks-status.md`。
- 内容: Archive/Core/tasks-status.md では、TRACKER-054 の設定優先順位、対象外条件、テスト件数、詳細報告ファイルなどが「設定追加、文書更新、検証、確認まで完了。」へ縮約されている。他の複数行でも詳細な履歴が短い完了宣言へ変わった。
- 影響: 完了判断を追跡する具体的な根拠がなくなる。単なる英語・片仮名の言い換えではなく、情報削除を伴う変更。
- 根拠: 同文書の UTF-8 の大きさは base 51,320 bytes、HEAD 7,028 bytes。代表例は base:63 → HEAD:62 の TRACKER-054。Archive/DebugHost でも説明行が削られている。大きさの差自体ではなく、具体的な検証情報と参照の消失を指摘する。
- 必要な対応: 検証結果、対象外条件、報告の参照先を保持した用語修正に戻す。履歴を要約・移設する意図があるなら、用語整理とは分けて内容保持と参照先を提示する。
