# Sub-agent実行レポート

## タスク

- 目的: 旧 `temporary-doc-lint-terms` 一覧から、whitelist 登録せず本文を日本語へ直すべき一般英語を分類する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指定により、旧一時許可一覧を複数カテゴリに分けてサブエージェントで分担するため。

## 対象範囲

- 対象:
  - `git show HEAD:tools/lint/markdown-whitelist.yaml` 内の旧 `temporary-doc-lint-terms`
  - 非 `reports/**` Markdown 内の実使用箇所

## 対象外

- 対象外:
  - `tools/lint/markdown-whitelist.yaml` の編集
  - Markdown 本文の編集
  - lint script の変更

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - `sed -n '1,240p' reports/doc-lint-whitelist-proposal-japanese-20260517085723.md`
  - `git show HEAD:tools/lint/markdown-whitelist.yaml | sed -n '/term: temporary-doc-lint-terms/,/description: 一時許可語/p'`
  - `rg --files -g '*.md' -g '!reports/**'`
  - `python3 - <<'PY' ... PY`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/doc-lint-whitelist-proposal-japanese-20260517085723.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - 確認: `tools/lint/markdown-whitelist.yaml` の `HEAD` 版旧 `temporary-doc-lint-terms`
  - 確認: 非 `reports/**` Markdown の通常文出現箇所

## 指摘事項

- 必ず日本語化:
  - `action`: 通常文では「操作」「処理」「対応」に置換する。`action button` は「操作ボタン」。
  - `active`: 通常文では「有効」「現在の」「稼働中」に置換する。`active tracking` は「現在の tracking」ではなく「現行の追跡管理」などへ寄せる。
  - `address`: 通常文では「アドレス」または「宛先」に置換する。`multicast address` は本文上も「マルチキャストアドレス」とし、構成要素の `multicast` / `address` を alias で単独許可しない。
  - `aggregate`: 通常文では「集約」「集約元」「代表」に置換する。集約表示の固有概念は「集約ソース」などの日本語設計語で固定し、構成要素を alias で単独許可しない。
  - `alert`: 「警告表示」または「通知」に置換する。
  - `anchor`: 「基準」「基準時刻」に置換する。
  - `angle`: 「角度」に置換する。
  - `apply`: 「適用」に置換する。
  - `approved`: 「承認済み」に置換する。
  - `area`: 「領域」に置換する。
  - `arrival`: 「到着」「受信到着順」に置換する。
  - `assignment`: 「割り当て」に置換する。
  - `available`: 「利用可能」に置換する。
  - `background`: 「背景」に置換する。
  - `body`: 「本体」に置換する。`robot body` は「ロボット本体」。
  - `button`: 通常文では「ボタン」に置換する。UI 部品名として複合語登録する場合も単独登録は避ける。
  - `change` / `changed`: 「変更」「変更済み」に置換する。
  - `choice`: 「選択肢」に置換する。
  - `clear`: 動詞なら「消去」「初期化」「解除」に置換する。識別子やメソッド名だけなら対象外。
  - `color`: 「色」に置換する。`accent color` は「強調色」に日本語化する。
  - `comparison`: 通常文では「比較」に置換する。
  - `condition`: 「条件」に置換する。
  - `current`: 「現在の」「現行」に置換する。
  - `data`: 通常文では「データ」に置換する。単独英語登録はしない。
  - `default`: 「既定」「既定値」に置換する。
  - `detail` / `details`: 「詳細」に置換する。
  - `display`: 「表示」に置換する。
  - `done`: 「完了」に置換する。
  - `empty`: 「空」「未設定」「空状態」に置換する。
  - `entry`: 「項目」「エントリ」に置換する。ログの単位概念は「ログ項目」を優先する。
  - `error`: 「エラー」に置換する。
  - `event`: 「イベント」に置換する。単独英語登録はしない。
  - `evidence`: 「証跡」に置換する。
  - `external`: 「外部」に置換する。
  - `field`: 通常文では「フィールド」に置換する。SSL-Vision の field 概念は複合語で扱う。
  - `file`: 「ファイル」に置換する。
  - `final`: 「最終」に置換する。
  - `future`: 「未来」「後続」に置換する。
  - `goal`: 「ゴール」または「目的」に置換する。競技フィールド要素なら「ゴール」。
  - `group`: 「グループ」に置換する。
  - `hard`: 「厳格」「強い」「困難」など文脈に合わせて置換する。
  - `health`: 「健全性」に置換する。
  - `hidden`: 「非表示」に置換する。
  - `index`: 「索引」「インデックス」に置換する。
  - `input`: 「入力」に置換する。
  - `kind`: 「種類」に置換する。
  - `label`: 「ラベル」に置換する。
  - `last`: 「最後の」「直近の」に置換する。
  - `latest`: 「最新」に置換する。
  - `layer`: 「レイヤー」に置換する。`Layer A/B` は固有の表示名として別管理。
  - `layout`: 「レイアウト」に置換する。
  - `left` / `right`: UI 側の説明では「左」「右」に置換する。
  - `local`: 「ローカル」または「局所」に置換する。
  - `manual`: 「手動」に置換する。
  - `metadata`: 「メタデータ」に置換する。
  - `missing`: 「欠落」「候補なし」に置換する。
  - `mode`: 「モード」に置換する。
  - `new`: 「新規」に置換する。
  - `normal`: 「通常」に置換する。
  - `old`: 「旧」「旧形式」に置換する。
  - `output`: 「出力」に置換する。
  - `panel`: 「パネル」に置換する。
  - `path`: 「パス」に置換する。
  - `pending`: 「保留中」「未確定」に置換する。
  - `ready`: 「準備完了」「利用可能」に置換する。
  - `record`: 「記録」「レコード」に置換する。
  - `remote`: 「リモート」「遠隔」に置換する。
  - `report`: 「レポート」に置換する。
  - `reset`: 「リセット」「初期化」に置換する。
  - `review`: 「レビュー」に置換する。
  - `role`: 「役割」に置換する。
  - `source`: 「ソース」または「入力元」に置換する。`source identity` などは「入力元識別情報」のような日本語設計語へ寄せる。
  - `status`: 「状態」に置換する。
  - `target`: 「対象」に置換する。
  - `tick`: 「tick」として意味が固定された複合語以外は「時点」「刻み」に置換する。
  - `timestamp`: 「タイムスタンプ」に置換する。
  - `type`: 「型」「種別」に置換する。
  - `value`: 「値」に置換する。
  - `view`: 「表示」「ビュー」に置換する。
  - `window`: 「時間窓」「ウィンドウ」に置換する。
- 文脈次第:
  - `adapter`, `component`, `controller`, `provider`, `reader`, `renderer`, `store`, `writer`: 型名・責務名・ファイル名なら対象外。通常文なら「アダプタ」「コンポーネント」「制御部」「提供部」「読み取り部」「描画部」「保存部」「書き込み部」へ置換する。
  - `algorithm`, `architecture`, `contract`, `protocol`, `schema`, `timeline`: 設計上の固定概念なら脚注で日本語の設計語を定義し、その設計語を本文で使う。単独で一般語として出る場合は「アルゴリズム」「構成」「契約」「プロトコル」「スキーマ」「時系列」へ置換する。
  - `alignment`, `comparison`, `diagnostics`, `replay`, `snapshot`, `sidecar`: このリポジトリの概念名として使う場合も、構成要素の alias 許可には逃がさない。通常文では「対応付け」「比較」「診断」「再生」「スナップショット」「副ファイル」に置換し、固定概念が必要なら「診断再生」「対応付け副ファイル」などの日本語設計語で扱う。
  - `ball`, `robot`, `camera`, `field`, `goal`: SSL-Vision / RoboCup の領域語として残す場合も、本文では「ボール」「ロボット」「カメラ」「フィールド」「ゴール」へ寄せる。英語構成要素の alias 単独許可はしない。
  - `cache`, `buffer`, `queue`, `packet`, `port`, `socket`: 実装概念として残す場合も単独登録せず、本文では「キャッシュ」「バッファ」「キュー」「パケット」「ポート」「ソケット」へ寄せる。
  - `playback`, `scrub`, `scrubber`, `selector`, `toggle`, `viewport`: UI 概念として固定する場合も、本文では「再生」「移動」「スクラバー」「選択部」「切り替え」「表示領域」へ置換する。英語構成要素を alias で単独許可しない。
  - `split mode` / `overlay mode`: 本文に単独概念として出るため、alias 許可に逃がさない。通常文では「分割表示モード」「重ね合わせ表示モード」に日本語化する。UI 表示値として `Split` / `Overlay` を残す場合は、本文では「UI 表示値 `Split`」「UI 表示値 `Overlay`」のように設計語として明確化する。
  - `source identity`, `source key`, `stable key`: 既存設計の固定概念として残す場合も、alias 許可ではなく「入力元識別情報」「入力元キー」「安定キー」などの日本語設計語へ寄せる。説明本文では英語名だけで完結させない。
- カタカナ登録可:
  - `アルゴリズム`, `インターフェース`, `エンジン`, `クラス`, `コード`, `コマンド`, `コンポーネント`, `タイミング`, `データ`, `データフロー`, `テスト`, `ドキュメント`, `ネットワーク`, `パラメータ`, `ファイル`, `フィルタ`, `フレーム`, `ページ`, `ボタン`, `メタデータ`, `モデル`, `ユーザー`, `ラベル`, `レイアウト`, `レビュー`, `レポート`, `ローカル`, `ログ`, `ログファイル`
  - 上記は技術文書で自然なカタカナ一般語として登録してよい。ただし、意味を狭めたい場合は alias に構成要素を足すのではなく、本文側で「診断ログ」「比較レポート」などの日本語表記へ寄せる。
- カタカナだが本文修正推奨:
  - `オブジェクト`: C# の `object` 型以外は「対象」「物体」「要素」に置換する。
  - `ケース`: 「場合」「事例」に置換する。
  - `ゲート`: workflow の gate 以外は「判定」「関門」「完了条件」に置換する。
  - `コスト`: 「負荷」「費用」「実行量」に置換する。
  - `コンテキスト`: 「文脈」「状況」「実行文脈」に置換する。
  - `サイズ`: 「大きさ」「容量」に置換する。
  - `スキップ`: 「省略」「除外」に置換する。
  - `スコープ`: 「範囲」に置換する。
  - `セット`: 「組」「設定」「集合」に置換する。
  - `バランス`: 「釣り合い」「均衡」に置換する。
  - `フェーズ`: tracking の phase 以外は「段階」に置換する。
  - `フラグ`: boolean 変数名以外は「印」「状態値」に置換する。
  - `フォルダ`: リポジトリ用語として残すなら登録可だが、本文では「ディレクトリ」に統一する余地がある。
  - `ベース`: 「基準」「土台」に置換する。
  - `ボトルネック`: 「性能上の詰まり」「遅延要因」に置換する。
  - `メタ`: 「メタデータ」または具体的な対象語へ置換する。
  - `メモ`: 「メモ」は自然だが、成果物名なら「引き継ぎメモ」など複合語へ寄せる。
  - `リスク`: 「リスク」は自然だが、lint 方針上は「懸念」「未解決事項」へ寄せると単独登録を避けられる。
  - `ルール`: 「規則」「方針」に置換する。
  - `レベル`: 「段階」「水準」に置換する。
  - `ロジック`: 「処理」「判定処理」に置換する。

## 結果

- 次の本文修正で使う置換案:

| 区分 | 旧表記 | 推奨表記 |
| --- | --- | --- |
| 必ず日本語化 | `action button` | 操作ボタン |
| 必ず日本語化 | `active tracking` | 現行の追跡管理 |
| 必ず日本語化 | `multicast address` | マルチキャストアドレス |
| 必ず日本語化 | `aggregate source` | 集約ソース、代表ソース |
| 必ず日本語化 | `anchor event time` | 基準イベント時刻 |
| 必ず日本語化 | `angle / distance helper` | 角度 / 距離ヘルパー |
| 必ず日本語化 | `arrival order` | 到着順 |
| 必ず日本語化 | `available interface` | 利用可能なインターフェース |
| 必ず日本語化 | `button` | ボタン |
| 必ず日本語化 | `choice` | 選択肢 |
| 必ず日本語化 | `clear` | 初期化、消去、解除 |
| 必ず日本語化 | `current limitation` | 現行の制約 |
| 必ず日本語化 | `empty alert` | 空状態の警告表示 |
| 必ず日本語化 | `manual evidence` | 手動確認の証跡 |
| 必ず日本語化 | `missing reason` | 欠落理由 |
| 必ず日本語化 | `selected tick` | 選択中の時点 |
| 必ず日本語化 | `split mode` | 分割表示モード |
| 必ず日本語化 | `overlay mode` | 重ね合わせ表示モード |
| 文脈次第 | `alignment sidecar` | 対応付け sidecar、対応付け副ファイル |
| 文脈次第 | `comparison panel` | 比較パネル |
| 文脈次第 | `diagnostics replay` | 診断再生 |
| 文脈次第 | `latest-before snapshot` | 直前 snapshot、選択時点以前の最新 snapshot |
| 文脈次第 | `UI 表示値 Split / Overlay` | UI 表示値 `Split` / `Overlay` と明記し、本文概念は分割表示モード / 重ね合わせ表示モード |
| 文脈次第 | `source identity` | 入力元識別情報 |
| 文脈次第 | `stable key` | 安定キー |
| カタカナ登録可 | `algorithm` | アルゴリズム |
| カタカナ登録可 | `component` | コンポーネント |
| カタカナ登録可 | `metadata` | メタデータ |
| カタカナ登録可 | `parameter` | パラメータ |
| カタカナ登録可 | `review` | レビュー |
| カタカナ登録可 | `report` | レポート |
| カタカナ修正推奨 | `case` / `ケース` | 場合、事例 |
| カタカナ修正推奨 | `scope` / `スコープ` | 範囲 |
| カタカナ修正推奨 | `logic` / `ロジック` | 処理、判定処理 |
| カタカナ修正推奨 | `rule` / `ルール` | 規則、方針 |
| カタカナ修正推奨 | `risk` / `リスク` | 懸念、未解決事項 |

## リスク

- 未解決のリスクまたは後続対応:
  - 今回は本文を編集していないため、各置換案は次の本文修正時に文脈確認が必要。
  - 外部由来 Markdown と識別子・脚注用語は混在しているため、最終 whitelist では alias で構成要素を単独許可していないかを再確認する必要がある。
  - Markdown link address、パス、命令、inline code だけで出る語は本文修正対象から外したが、通常文と識別子が同じ行に混在する箇所は追加の目視確認が必要。
