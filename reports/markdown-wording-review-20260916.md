# Markdown用語・意味保持のレビュー

## 判定

**要修正（fail）。指摘11件：medium 10件、low 1件。** lintの成功と文章の意味・読みやすさは別に判定する。指摘の修正、用語登録、製品コードやworkflowの変更は行っていない。

## 対象と実施範囲

- 対象: ibis-ssl/Duck PR #20。通常の初回レビュー。日時: 2026-09-15T21:09:34.730713+00:00。
- reviewed_implementation_head: `42e063760f90987262e397b43ba084c9ee60b944`。
- base: main / `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`。
- PRブランチ: `docs/runtimehost-readme-appsettings`。この判定は上記レビューHEADに適用する。
- 利用者指定: 無理な日本語化の確認、変更前に使われていた許可済み英語・片仮名語との照合、フィールド・observer/event・設定プロファイル・キャプチャーの検討。
- RDCのLinux端末で既存作業を読み取り、Git blobをSHA固定で比較した。他タスクの作業ツリーは変更しない。報告用の別作業ツリーに本報告・照合結果・引き継ぎだけを保存する。
- 変更103パスを列挙。通常対象Markdown 19文書、許可一覧301項目と別表記を機械照合。11主要文書と関連履歴・引用を対象に、問題箇所の前後の本文と直接依存する実装を確認した。全履歴reportの再監査や全候補の個別採否が完了したとは扱わない。
- 通常レビュー担当として実施し、独立最終レビュー・合格証明とは扱わない。TDDは実装を行わないため対象外。

## 変更前後の代表例

| 変更前・元の概念 | 現在の表記 | 必要な対応 | 指摘 |
| --- | --- | --- | --- |
| source frame number | 入力元の追跡結果の番号 | source frame number（入力の観測フレーム番号） | WR-001 |
| raw vision | 未加工映像 | raw vision（SSL-Visionの検出情報） | WR-002 |
| observer/event | 通知イベント契約／通知を受け取る処理契約 | イベントと通知先の契約 | WR-003 |
| profile switch | 設定組の切り替え | 設定プロファイルの切り替え | WR-004 |
| field | 競技場 | フィールド | WR-005 |
| packet capture／既存の傍受 | 受信記録／傍受 | 通信内容の取得・保存を指す箇所はキャプチャー | WR-006 |
| Kalman filter | カルマン状態推定 | 許可済みのKalman filterを保持 | WR-007 |
| process noise | プロセス側の揺らぎ | 許可済みのprocess noiseを保持 | WR-007 |
| measurement noise | 観測側の揺らぎ | 許可済みのmeasurement noiseを保持 | WR-007 |
| timeline scrubber | 再生位置操作部 | 許可済みのtimeline scrubberを保持 | WR-007 |
| render snapshot | 描画時点の記録等 | 許可済みのrender snapshotを保持 | WR-007 |
| Vision Input / Tracker Output | 映像入力とトラッカー出力 | 画面の実名を保持 | WR-008 |

「設定プロファイル」「フィールド」「キャプチャー」は今回の利用者指定として扱う。現在の許可一覧に自動的に登録済みとはみなさない。単独の「プロファイル」「キャプチャ」など、別表記・別意味を同時に無制限許可する提案ではない。

## 詳細指摘

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

## 許可済み語の照合方法

`reports/markdown-wording-audit-20260916.json` に301項目すべての term・aliases と、変更前後の対象文書内の出現行を保存した。大文字・小文字を同一視し、英数字識別子の境界を確認する。コード、リンク、脚注を含む文字列上の照合で、複合語が重なる場合もある。語が消えた件数を、そのまま誤訳件数とは扱わない。誤った行同士の自動対応表は根拠に使っていない。

修正時は、変更前の各出現について、同じ意味の許可済み名称を保持したか、今回指定の表記へ揃えたか、別の自然な日本語を採用する理由があるかを記録する。本文の別の場所に同じ単語が残っているだけでは、当該箇所の対応完了とはしない。

## 検証

| 検証 | 結果 | 対象 |
| --- | --- | --- |
| 全範囲 `npm run lint:md` | 終了値0。cspell 19文書、指摘0。textlint・許可一覧も成功 | `42e063760f90987262e397b43ba084c9ee60b944` |
| `git diff --check f5482ab85d49832a21ef6029d6aa3354f6c7c4f4 42e063760f90987262e397b43ba084c9ee60b944` | 終了値0 | レビュー差分 |
| 指摘根拠の文字列・構造確認 | 12項目一致。不具合の根拠が再現することの確認であり、製品テスト成功ではない | レビューHEAD |
| 製品ソース等の変更確認 | .cs / .razor / .proto / .csproj / .sln の変更なし | baseからレビューHEAD |
| ローカル.NETテスト | 今回のレビューでは再実行していない | 該当なし |
| .NET tests / pull_request | run 35019194011、completed / success、head_sha完全一致 | `42e063760f90987262e397b43ba084c9ee60b944` |

ローカルlintは `/home/ibis/.local/share/duck-lint-worktree-chat2` で実行し、実行前後のHEAD一致と追跡対象差分なしを確認した。既存のnode_modulesと共有検査器を利用し、依存環境は変更していない。lint・差分検査の標準出力、標準エラー、結果は `/home/ibis/.local/share/duck-wording-review-evidence-20260916` に保存した。lintの出力と原文のSHA-256一覧はaudit JSONにも含めた。この端末上のパスをChat環境のダウンロードリンクとしては扱わない。

CIはGitHub connectorから取得し、runのhead_shaがレビューHEADと一致することを確認した。報告だけのコミットでPR HEADが更新された場合、その新HEADのCIは別途確認し、PRコメントへ記録する。ここに記した旧HEADの成功を新HEADの成功として使わない。

## 確認範囲と未確認事項

| 観点 | 判定 | 根拠 |
| --- | --- | --- |
| 要件・設計との適合 | `checked_finding` | WR-003〜WR-008。利用者指定4点、許可済み語の保持、コード・画面名との対応を確認。 |
| 意味の正確さと境界条件 | `checked_finding` | WR-001〜WR-003。入力番号と出力番号、検出情報と映像、通知先とイベントを照合。 |
| 変更範囲と無関係な変更 | `checked_finding` | WR-010、WR-011。引用改変と検証履歴の情報削除。 |
| 変更ファイルと直接依存 | `checked_finding` | 103変更パスを列挙。19対象Markdownと許可一覧301項目を機械照合し、本文およびITrackerEngine、ITrackerObserver、TrackerEvent、FrameCommit、Diagnostics.razorを重点照合。 |
| API・データ・設定・互換性 | `checked_finding` | 製品の.cs/.razor/.proto/.csproj/.sln変更は0。説明上の契約変更はWR-001〜WR-004。 |
| 失敗診断とworkflow | `held` | 既存runのsuccessは確認。報告が指すworkflowファイルはレビュー対象Git treeに無く、同SHAのGitHub取得も404。失敗診断設定の内容はこのレビューでは確認できない。 |
| セキュリティ・秘密値 | `not_applicable` | 今回の用語レビューは実行コード・認証設定を変更しない。全履歴reportの秘密値監査は実施していない。 |
| 検証の妥当性 | `checked_finding` | 同HEADで19文書lint成功だが11件の指摘は残る。原文・意味を人が照合する工程が必要。 |
| current-HEAD CI | `checked_no_finding` | レビューHEAD42e0637と完全一致するpull_request run35019194011のsuccessをGitHub connectorで確認。 |
| 報告・進捗・記録の正確さ | `checked_finding` | WR-010、WR-011。lint成功を文章妥当性の証明には扱わない。 |
| 回帰・保守性 | `checked_finding` | WR-001、WR-007、WR-008。実装識別子・許可語・画面名との対応消失。 |

### 保留: 診断workflowの出典

reports/markdown-lint-completion-20260916.md:55は.github/workflows/dotnet-test.ymlの診断artifact経路を確認済みと記すが、対象HEADのGit treeと同SHAのGitHub内容取得では見つからない。Actions runのpathには同パスが記録されている。 実際に実行されたworkflowの由来と失敗時保存内容は未確認。runのsuccessを否定するものではない。 担当: 実装担当。

### 未確認: 過去の全reportの内容・過去のテスト実行の再監査

今回の主対象は用語・意味保持の差分レビュー。既存の調査reportをすべて再現したわけではない。 過去報告の全記述の正しさは保証しない。 全面承認はしない。確認済みの要修正指摘は有効。

### 未確認: 機械抽出した全候補の語義別の最終採否

全301項目の変更前後出現行は記録したが、同梱一覧の全行を個別の不具合として確定したものではない。 リンク・コード・重なる複合語の出現や、自然な日本語化が候補に含まれる。 WR-007の修正時に個別の対応表を完成させる。

## 次の対応

実装担当がWR-001〜WR-011を修正し、各指摘の必要な対応・修正箇所・変更前後の対照・検証結果を揃えて再レビューへ渡す。用語登録を変えた場合は、許可すべき意味と拒否すべき別語・別意味の検証も含める。明確な識別名を保持しながら説明文を読み直し、lint成功だけで完了としない。

本レビューでは報告類以外をコミットしない。既存の未コミット変更は保持する。マージは行わない。報告公開コミットはレビュー対象の実装修正でも独立最終レビューの合格証明でもない。
