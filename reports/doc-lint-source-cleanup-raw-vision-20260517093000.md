# Sub-agent実行レポート

## タスク

`Tracker/Design/DebugHost/raw-vision-viewer-plan.md` の `source` 単語単体利用を見直す。

## sub-agentを使う理由

ファイルごとに作業を分担し、用語修正の範囲を混ぜないため。

## 対象範囲

`Tracker/Design/DebugHost/raw-vision-viewer-plan.md`

## 対象外

ホワイトリスト定義、他の Markdown ファイル、コード変更。

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `rg -n "\\bsource\\b|Source|SOURCE" Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/DebugHost/raw-vision-viewer-plan.md --list-unknown`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/DebugHost/raw-vision-viewer-plan.md`

## 対象ファイル

- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `reports/doc-lint-source-cleanup-raw-vision-20260517093000.md`

## 指摘事項

- Markdown whitelist lint の未知語は対象ファイル単体で残っていない。
- `source` 単語単体は通常本文から除去した。残っている `source` は `SourceLabel`、`source key`、`source snapshot`、`source identity`、`source name`、`source selector`、`source label`、`same-source`、`Field source`、`source selection`、`source option` など、識別子または意味を持つ複合語としてコード表記にしたものだけである。
- `source` は機械的に「原典」へ訳していない。本文では文脈に応じて表示元、更新元などへ置き換えた。
- `tracked frame` は対象文書に出現しなかった。今後本文に必要になった場合は無理に日本語化せず、複合語のホワイトリスト登録候補として扱う。
- `tools/lint/markdown-whitelist.yaml` は対象外のため編集していない。

## 結果

- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md` は、一般英単語と一般カタカナ語を日本語へ寄せ、必要な識別子・設定キー・画面表示名・設計用語だけをコード表記へ整理した。
- 脚注参照名が lint 対象になるため、脚注で説明していた用語は本文説明へ統合した。
- 指定の whitelist lint は対象ファイル単体で成功した。
- ホワイトリスト追加候補は今回なし。

## リスク

- 大きめの文書整理になったため、原文の細かい脚注名は残していない。ただし設計上必要な説明は本文へ戻した。
- 追加 sub-agent 起動は禁止されているため、独立 reviewer による再レビューは実施していない。
