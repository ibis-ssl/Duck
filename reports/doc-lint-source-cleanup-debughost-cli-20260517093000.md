# Sub-agent実行レポート

## タスク

`Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md` の `source` 単語単体利用を見直す。

## sub-agentを使う理由

ファイルごとに作業を分担し、用語修正の範囲を混ぜないため。

## 対象範囲

`Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`

## 対象外

ホワイトリスト定義、他の Markdown ファイル、コード変更。

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md --list-unknown`
- `rg -n "(^|[^A-Za-z])source([^A-Za-z]|$)" Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `git status --short -- Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md reports/doc-lint-source-cleanup-debughost-cli-20260517093000.md tools/lint/markdown-whitelist.yaml`

## 対象ファイル

- 変更: `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- 変更: `reports/doc-lint-source-cleanup-debughost-cli-20260517093000.md`
- 確認のみ: `tools/lint/markdown-whitelist.yaml` は作業開始時点で変更済みだったが、所有外のため編集していない。

## 指摘事項

- `source` 単語単体は残っていない。
- `source` は「原典」と訳していない。文脈に応じて表示元または入力元を使った。
- 追加指示に従い、`tracked frame` は本文で無理に日本語化していない。
- 所有ファイル単体の Markdown whitelist lint は `frame` 13 件で失敗する。いずれも `tracked frame` 複合語の構成語である。
- ホワイトリスト追加候補: `tracked frame`。追跡器が出力する追跡済み時点を指す設計語で、`tracked frame の番号`、`tracked frame の時刻`、`tracked frame 欠落` のように仕様上の単位として使うため。

## 結果

- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md` の一般英単語と一般カタカナ語を文脈に合わせて日本語へ寄せた。
- UI 表示名、型名、設定キー、経路などは既存どおりバッククォート内の識別子として維持した。
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md --list-unknown` は `frame 13` を出して exit code 1 で完了した。これは `tracked frame` のホワイトリスト追加待ちとして扱う。

## リスク

- 用語置換の範囲が広く、設計意図は維持したが文章表現は大きく変わっている。
- `tracked frame` が未登録の間は、対象ファイル単体の Markdown whitelist lint は通らない。
- `tools/lint/markdown-whitelist.yaml` は所有外の既存変更があるため、この作業では内容確認と編集をしていない。
