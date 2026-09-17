# Sub-agent実行レポート

## タスク

`Tracker/Design/Core/tracker-history-000-038.md` の Markdown lint 指摘だけを対象に修正案を作る。

## sub-agentを使う理由

履歴表の用語判断を独立させ、`gpt-5.5 high` の worker に任せるため。

## 対象範囲

`Tracker/Design/Core/tracker-history-000-038.md`

## 対象外

ホワイトリストの直接編集、lint に出ていない箇所の言い換え、機械的な全文置換。

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js Tracker/Design/Core/tracker-history-000-038.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/Core/tracker-history-000-038.md --list-unknown`
- `./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules Tracker/Design/Core/tracker-history-000-038.md`

## 対象ファイル

- 変更: `Tracker/Design/Core/tracker-history-000-038.md`
- 変更: `reports/doc-lint-full-core-history-gpt55-20260517100000.md`
- 変更禁止として維持: `tools/lint/markdown-whitelist.yaml`

## 指摘事項

- 初回の `run-cspell-markdown.js` は、履歴表内の英語説明語を中心に `Unknown word` を検出した。代表例は `tracking`、`preparation`、`done`、`contracts`、`packet`、`generator`、`primary`、`secondary`、`timestamp`、`engine`、`profile`、`robot`、`ball`、`review`。
- 初回の `check-markdown-whitelist.js --list-unknown` は、英語語彙と片仮名語を検出した。本文説明で安全に置換できるものは日本語へ寄せた。
- 最終時点の `textlint` は指摘なし。
- 最終時点で残った `run-cspell-markdown.js` 指摘は、履歴表の段階/状態列値および固有名に限定した。該当語は `preparation`、`contracts`、`engine`、`integration`、`verification`、`review`、`documentation`、`investigation`、`maintenance`、`done`、`Tigers`。
- 最終時点で残った `check-markdown-whitelist.js --list-unknown` 指摘は、`contracts`、`documentation`、`done`、`engine`、`integration`、`investigation`、`maintenance`、`preparation`、`review`、`Tigers`、`verification`、`XML`、`ボール`、`ロボット`。
- ホワイトリスト候補:
  - 候補化の方針: 単独語は許可範囲が広くなるため、原則として `field geometry`、`raw geometry`、`render snapshot geometry` のように意味を絞れる複合語で提案する。単独候補は、競技・画面表示の基礎語として頻出し、単独登録の理由を説明できる場合に限る。
  - `preparation`、`contracts`、`engine`、`integration`、`verification`、`review`、`documentation`、`investigation`、`maintenance`: 履歴表の段階列値で、過去の `phases-status.md` 系の意味を保つため本文置換より許可語候補が適切。
  - `done`: 履歴表の状態列値で、完了状態を表す固定値として扱われているため許可語候補が適切。
  - `Tigers`: 入力データ/由来を示す固有名であり、日本語化すると出所の意味が曖昧になるため許可語候補が適切。
  - `XML`: 注釈形式の固有技術名であり、日本語化しない方が意味が明確なため許可語候補が適切。
  - `ボール`、`ロボット`: ロボカップ競技・画面表示・追跡処理の基礎対象として履歴全体で頻出し、単独でも意味が明確なため単独許可語候補として扱える。機械的に別語へ置換すると対象物の意味が崩れる。

## 結果

- `Tracker/Design/Core/tracker-history-000-038.md` の lint 指摘箇所に限定して、英語説明語と汎用片仮名語を日本語へ置換した。
- 履歴表の段階/状態列値、固有名、領域用語は意味保持を優先し、本文変更ではなくホワイトリスト候補として残した。
- `textlint` は対象ファイル単体で通過した。
- `run-cspell-markdown.js` と `check-markdown-whitelist.js --list-unknown` は、上記ホワイトリスト候補分の指摘が残る。

## リスク

- `tools/lint/markdown-whitelist.yaml` は禁止指示に従って未編集。残指摘を解消するには、利用者確認後に上記候補を許可語として扱う必要がある。
- 追加 sub-agent 起動は禁止指示のため、`review-enforcer` が通常要求する別 sub-agent レビューは実行していない。
