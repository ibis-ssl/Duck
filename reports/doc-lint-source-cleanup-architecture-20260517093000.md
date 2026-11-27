# Sub-agent実行レポート

## タスク

`Tracker/Design/Core/tracker-architecture-plan.md` の `source` 単語単体利用を見直す。

## sub-agentを使う理由

ファイルごとに作業を分担し、用語修正の範囲を混ぜないため。

## 対象範囲

`Tracker/Design/Core/tracker-architecture-plan.md`

## 対象外

ホワイトリスト定義、他の Markdown ファイル、コード変更。

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/Core/tracker-architecture-plan.md --list-unknown`
- `rg -n -i '\bsource\b' Tracker/Design/Core/tracker-architecture-plan.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/Core/tracker-architecture-plan.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files reports/doc-lint-source-cleanup-architecture-20260517093000.md --list-unknown`

## 対象ファイル

- 変更: `Tracker/Design/Core/tracker-architecture-plan.md`
- 変更: `reports/doc-lint-source-cleanup-architecture-20260517093000.md`
- 対象外維持: `tools/lint/markdown-whitelist.yaml`

## 指摘事項

- `source` 単語単体は本文 prose から除去した。残る `source` は `source_name`、`sourceName`、`sourceRole`、`sourceLabel`、`sourceUuid`、`Field source` のような識別子または UI 表示名に限定した。
- `source` は「原典」へ機械翻訳していない。表示対象の由来は `表示元`、入力系列は `入力元` として文脈別に分けた。
- `tracked frame` は追加注意に従い、日本語へ無理に置き換えず本文に残した。現行 whitelist では複合語未登録のため、`frame` が未知語として残る。
- whitelist 追加候補: `tracked frame`。理由: トラッカー出力の追跡済みフレームを指す設計上の複合語であり、単独の `frame` を許可するより受け入れ範囲が狭い。

## 結果

- `--list-unknown` の最終結果は `frame 9`。該当箇所はいずれも `tracked frame` の一部。
- 通常実行の最終結果は失敗。理由は `tracked frame` 複合語を本文に残したことによる `frame` 未登録。
- `source` 単語単体は残っていない。

## リスク

- `tracked frame` が whitelist に追加されるまでは、所有ファイル単体の Markdown whitelist lint は失敗する。
- 本作業では whitelist を編集していないため、複合語登録の要否は親側確認が必要。
