# Sub-agent実行レポート

## タスク

`Tracker/Design/Core/tracker-architecture-plan.md` の Markdown lint 指摘だけを対象に修正案を作る。

## sub-agentを使う理由

対象が大きいため、`gpt-5.5 high` の worker に狭い範囲で任せるため。

## 対象範囲

`Tracker/Design/Core/tracker-architecture-plan.md`

## 対象外

ホワイトリストの直接編集、lint に出ていない箇所の言い換え、機械的な全文置換。

## 実行コマンド

- 初回確認:
  - `node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js Tracker/Design/Core/tracker-architecture-plan.md`
  - `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/Core/tracker-architecture-plan.md --list-unknown`
  - `./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules Tracker/Design/Core/tracker-architecture-plan.md`
- 修正後確認:
  - `node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js Tracker/Design/Core/tracker-architecture-plan.md`
  - `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/Core/tracker-architecture-plan.md --list-unknown`
  - `./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules Tracker/Design/Core/tracker-architecture-plan.md`

## 対象ファイル

- 変更: `Tracker/Design/Core/tracker-architecture-plan.md`
- 変更: `reports/doc-lint-full-architecture-gpt55-20260517100000.md`
- 未変更: `tools/lint/markdown-whitelist.yaml`

## 指摘事項

- 初回 cspell は対象ファイルで 100 件を報告した。主な内容は `debug`、`diagnostics`、`official`、`detection`、`button`、`scope`、`logic` のように自然な日本語へ直せる語と、`packet snapshot`、`alignment sidecar`、`camera-local track` など意味が固定された複合語の混在だった。
- 初回 whitelist は unknown 語を一覧出力した。主な高頻度語は `ball` 90、`packet` 73、`snapshot` 59、`profile` 56、`track` 58、`robot` 43、`filter` 40、`raw` 39、`geometry` 34、`camera` 33、`diagnostics` 30、`capture` 28、`event` 28、`kick` 28、`detection` 27。
- textlint は初回、修正後ともに指摘なし。
- 修正対象は lint に出た語を含む箇所だけに限定した。`debug` / `diagnostics`、`official`、`button`、`scope`、`logic`、`module` / `naming` など、本文中で普通語として混在していた箇所を自然な日本語に直した。
- `source` は「原典」などには訳さず、既存の `source role` / `source identity` の意味を維持した。
- cspell / whitelist に残った指摘は、ホワイトリスト未承認の固定語が主因。ホワイトリスト候補は次の通り。

| term | aliases | description | 理由 |
| --- | --- | --- | --- |
| `packet snapshot` | `tracker packet snapshot`, `snapshot log`, `TrackedFrame snapshot` | 受信または生成したトラッカー packet を後続比較用に保存した記録。 | 単語単体では広すぎるため、保存・比較の概念を表す複合語として登録するのが適切。 |
| `alignment sidecar` | `tracker snapshot alignment sidecar`, `alignment log`, `alignment status` | CaptureOn 保存時の診断 entry、render snapshot、トラッカー snapshot の対応関係を保存する副次ファイル。 | 診断再生のファイル形式を表す固定語で、日本語化すると既存設計との対応が読みにくくなる。 |
| `camera-local track` | `camera-local ball track`, `camera-local robot track` | camera 単位で保持する局所追跡状態。 | `camera-local` が追跡段の境界を表す固定語で、単なる `camera` 登録より範囲が狭い。 |
| `world snapshot` | `world frame`, `tracked world` | 複数 camera の観測を統合した world 側の追跡状態。 | トラッカー内部モデルの層を表す固定語。 |
| `source identity` | `source role`, `source label`, `source summary` | トラッカー出力元を role / label / uuid / endpoint で識別する情報。 | `source` は「原典」ではなく入出力元識別の意味で使われるため、複合語登録が必要。 |
| `official tracker proto` | `official proto`, `tracker proto` | SSL 公式トラッカー出力の Protocol Buffers 形式。 | `official` は一部日本語化したが、`proto` を含む仕様名として残る箇所がある。 |
| `primary ball` | `secondary ball`, `kicked ball` | トラッカー出力で主対象、補助対象、kick 済み ball を区別する用語。 | AutoRef / tracker の出力仕様に紐づく固定語で、`ball` 単体登録より意味を限定できる。 |
| `ball left field` | `left field`, `field interior`, `goal interior` | ball が field 外へ出た状態とその位置種別。 | ルール判定用状態名と対応する固定表現。 |
| `event time` | `arrival order`, `reorder window`, `merge window` | 到着順ではなく観測時刻に基づいて frame を確定する時系列契約。 | 時系列契約の中核概念で、単語単体より複合語として承認したい。 |
| `replay timeline` | `capture-time`, `session-relative`, `latest-before`, `nearest-after` | capture の受信時刻を基準に診断再生・比較を行うための時系列。 | 診断再生の保存・表示仕様に結びついた固定語。 |
| `runtime profile` | `active profile`, `in-flight request`, `pending request`, `desired target snapshot` | 実行時の設定セット切替と未適用要求を管理する状態群。 | 設定切替契約の用語で、日本語へ崩すと実装名との対応が弱くなる。 |
| `diagnostics log` | `diagnostics entry`, `diagnostics viewer`, `diagnostics playback` | 診断ログ、診断表示、診断再生の一連の機能。 | 一部は日本語化したが、ファイル名や機能名に近い複合語として残る。 |

## 結果

- `Tracker/Design/Core/tracker-architecture-plan.md` のうち、lint で実際に出た ordinary English 混在箇所だけを手作業で修正した。
- 修正後の textlint は通過。
- 修正後の cspell は未通過。出力は引き続き 100 件で、先頭側では `geometry`、`multicast`、`proto`、`world`、`snapshot`、`event`、`packet`、`capture`、`replay`、`loop`、`logging`、`viewer`、`primary`、`ball` などが残った。
- 修正後の whitelist は未通過。上記ホワイトリスト候補に挙げた複合語を中心に未承認語が残っている。

## リスク

- ホワイトリストを編集していないため、cspell / whitelist は完了ゲートとしては未通過のまま。
- `ball`、`robot`、`packet`、`snapshot` のような単語単体を雑に登録すると許容範囲が広がりすぎるため、レビュー時は複合語単位での登録可否を確認する必要がある。
- 既存 worktree には作業開始時点で `Tracker/Design/tasks-status.md` と `tools/lint/markdown-whitelist.yaml` の変更があった。今回の worker では触れていない。
