# Sub-agent実行レポート

## タスク

- 目的: `Tracked` 単独表記を不許可にし、`Tracked 画面` / `Tracked 表示` を許可する whitelist 更新をレビューする。
- タスク種別: review

## sub-agentを使う理由

- 理由: `review-enforcer` に従い、whitelist 更新の完了判断前に独立した review sub-agent で確認するため。利用者指定により review sub-agent は `gpt-5.5 high` を使う。

## 対象範囲

- 対象:
  - `tools/lint/markdown-whitelist.yaml`
  - `reports/task-doc-lint-tracked-whitelist-update-20260628213011.md`
  - 必要に応じて `tools/lint/README.md`
  - 必要に応じて `reports/task-doc-lint-term-classification-result-20260628203849.md`

## 対象外

- 対象外:
  - `tools/lint/prh.yml` の編集
  - `Tracked` 以外の whitelist 候補レビュー
  - 設計書本文の広範な用語置換
  - PR 作成、commit 作成
  - nested Codex / `codex exec` / 追加 sub-agent 起動

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/markdown-word-checker/SKILL.md`
  - `sed -n '1,220p' reports/task-doc-lint-tracked-whitelist-review-20260628213441.md`
  - `tool_search` で `Serena` 関連ツールを検索。該当 callable tool なし。
  - `list_available_plugins_to_install` で install 候補を確認。Serena 該当なし。
  - `git status --short`
  - `git diff -- tools/lint/markdown-whitelist.yaml reports/task-doc-lint-tracked-whitelist-update-20260628213011.md reports/task-doc-lint-tracked-whitelist-review-20260628213441.md tools/lint/README.md reports/task-doc-lint-term-classification-result-20260628203849.md`
  - `nl -ba tools/lint/markdown-whitelist.yaml | sed -n '1,220p'`
  - `sed -n '1,260p' reports/task-doc-lint-tracked-whitelist-update-20260628213011.md`
  - `sed -n '1,220p' tools/lint/README.md`
  - `sed -n '1,420p' reports/task-doc-lint-term-classification-result-20260628203849.md`
  - `cat package.json`
  - `rg -n "term: Tracked($| )|Tracked 画面|Tracked 表示" tools/lint/markdown-whitelist.yaml reports/task-doc-lint-tracked-whitelist-update-20260628213011.md reports/task-doc-lint-term-classification-result-20260628203849.md tools/lint/README.md`
  - `rg -n "term: Tracked($| )|Tracked 画面|Tracked 表示" tools/lint/markdown-whitelist.yaml`
  - `printf 'Tracked 画面\nTracked 表示\n' | PATH="$PWD/.venv/bin:$PATH" npm run lint:md:whitelist -- --stdin /tmp/tracked-allowed.md`
  - `printf 'Tracked\n' | PATH="$PWD/.venv/bin:$PATH" npm run lint:md:whitelist -- --stdin /tmp/tracked-single.md`。単独 `Tracked` 検出により exit 1。
  - `PATH="$PWD/.venv/bin:$PATH" npm run lint:md:whitelist -- --files tools/lint/markdown-whitelist.yaml`。既存 description 語彙を多数検出して exit 1。
  - `npm run lint:md:whitelist:legacy -- --files tools/lint/markdown-whitelist.yaml`
  - `npm run -s lint:md:targets | rg '^reports/'`。一致なしのため exit 1。
  - `git diff --check`
  - `git diff -- tools/lint/prh.yml`
  - `PATH="$PWD/.venv/bin:$PATH" npm run lint:md`。既存語彙未整備により `lint:md:spell` で exit 123。
  - `rg -n "\bTracked\b" README.md tools/lint/README.md Tracker/Design Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md feedback-points/feedback-points.md AGENTS.md`

## 対象ファイル

- 変更または確認したファイル:
  - 確認: `tools/lint/markdown-whitelist.yaml`
  - 確認: `reports/task-doc-lint-tracked-whitelist-update-20260628213011.md`
  - 確認: `tools/lint/README.md`
  - 確認: `reports/task-doc-lint-term-classification-result-20260628203849.md`
  - 確認: `tools/lint/prh.yml`
  - 確認: `package.json`
  - 確認: `Tracker/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.DebugHost/README.md`
  - 確認: `Tracker/Design/RuntimeHost/runtime-host-plan.md`
  - 確認: `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
  - 確認: `Tracker/Design/Core/tracker-history-000-038.md`
  - 確認: `Tracker/Design/Core/tracker-architecture-plan.md`
  - 更新: `reports/task-doc-lint-tracked-whitelist-review-20260628213441.md`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。

## 結果

- 結果:
  - `tools/lint/markdown-whitelist.yaml:43` に `term: Tracked 画面`、`tools/lint/markdown-whitelist.yaml:45` に `term: Tracked 表示` が追加されている。
  - `rg -n "term: Tracked($| )|Tracked 画面|Tracked 表示" tools/lint/markdown-whitelist.yaml` は `Tracked 画面` / `Tracked 表示` の 2 件だけを返し、単独 `term: Tracked` は残っていない。
  - `tools/lint/markdown-whitelist.yaml:44` / `tools/lint/markdown-whitelist.yaml:46` の description は、既存 whitelist と同じく「この作業一式での意味」を短く説明している。`tools/lint/markdown-whitelist.yaml:46` は UI 表示名そのものの `` `Tracked` `` と本文用の `Tracked 表示` を分けており、本文表記の broad allowance には戻していない。
  - 実装 report は `Tracked 画面` / `Tracked 表示` の stdin pass、単独 `Tracked` の stdin fail、legacy whitelist pass、whitelist 自身の SudachiPy 失敗、reports 除外、`git diff --check` pass を記録している。
  - review 再実行でも `Tracked 画面` / `Tracked 表示` は `npm run lint:md:whitelist -- --stdin` で pass し、単独 `Tracked` は `'/tmp/tracked-single.md:1: 'Tracked' (english) is not in tools/lint/markdown-whitelist.yaml.'` として fail した。
  - `npm run lint:md:whitelist:legacy -- --files tools/lint/markdown-whitelist.yaml` は pass した。
  - `git diff -- tools/lint/prh.yml` は差分なしで、対象外の `prh.yml` 編集は混ざっていない。
  - `git diff --check` は pass した。

## リスク

- 未解決のリスクまたは後続対応:
  - Serena は利用者指示にあるが、この実行環境では callable tool / install 候補として見つからず、使用できなかった。
  - `PATH="$PWD/.venv/bin:$PATH" npm run lint:md` は `lint:md:spell` で exit 123。既存文書に未登録英字語が多数あり、全体 lint gate はこの whitelist 更新単独では pass しない。
  - full lint の失敗には、意図どおり単独 `Tracked` を検出する対象文書出現も含まれる。例: `Tracker/Design/tasks-status.md:139` の `未加工 / Tracked / 比較`。設計書本文の広範な用語置換は今回対象外のため、後続の本文修正または別途方針判断が必要。
  - `PATH="$PWD/.venv/bin:$PATH" npm run lint:md:whitelist -- --files tools/lint/markdown-whitelist.yaml` は、既存 description 内の日本語語彙を多数検出して exit 1。実装 report の残リスク記録と一致し、今回追加分だけの許可判定は stdin focused check で確認済み。
  - `reports/**` は通常 Markdown lint 対象外であり、この review report 自体は lint gate の直接対象ではない。
