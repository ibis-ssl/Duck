# Sub-agent実行レポート

## タスク

Tracker DebugHost / RuntimeHost design Markdown の lint 修正案と本文修正を実行する。

## sub-agentを使う理由

ユーザー指定により、lint 修正作業をサブエージェントで分担するため。

## 対象範囲

- `Tracker/Design/Archive/DebugHost/phases-status.md`
- `Tracker/Design/Archive/DebugHost/tasks-status.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`

## 対象外

- `tools/lint/markdown-whitelist.yaml` の編集
- `reports/**`
- Core design docs
- Tracker README 群
- root docs

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/scripts/list-markdown-targets.js`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/scripts/run-cspell-markdown.js`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/scripts/check-markdown-whitelist.js`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/Design/Archive/DebugHost/phases-status.md Tracker/Design/Archive/DebugHost/tasks-status.md Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md Tracker/Design/DebugHost/debug-host-maintainability-design.md Tracker/Design/DebugHost/raw-vision-viewer-plan.md Tracker/Design/RuntimeHost/runtime-host-plan.md reports/doc-lint-tracker-design-hosts-worker-20260516235249.md`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/Design/Archive/DebugHost/phases-status.md Tracker/Design/Archive/DebugHost/tasks-status.md Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md Tracker/Design/DebugHost/debug-host-maintainability-design.md Tracker/Design/DebugHost/raw-vision-viewer-plan.md Tracker/Design/RuntimeHost/runtime-host-plan.md --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/Design/Archive/DebugHost/phases-status.md Tracker/Design/Archive/DebugHost/tasks-status.md Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md Tracker/Design/DebugHost/debug-host-maintainability-design.md Tracker/Design/DebugHost/raw-vision-viewer-plan.md Tracker/Design/RuntimeHost/runtime-host-plan.md --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/Archive/DebugHost/phases-status.md Tracker/Design/Archive/DebugHost/tasks-status.md Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md Tracker/Design/DebugHost/debug-host-maintainability-design.md Tracker/Design/DebugHost/raw-vision-viewer-plan.md Tracker/Design/RuntimeHost/runtime-host-plan.md --list-unknown`

## 対象ファイル

- 変更: `Tracker/Design/Archive/DebugHost/phases-status.md`
- 変更: `Tracker/Design/Archive/DebugHost/tasks-status.md`
- 変更: `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- 変更: `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
- 変更: `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- 変更: `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- 変更: `reports/doc-lint-tracker-design-hosts-worker-20260516235249.md`
- 未変更: `tools/lint/markdown-whitelist.yaml`
- 未変更: `cspell.config.jsonc`

## 指摘事項

- `textlint`: 担当 6 文書で成功。
- `cspell`: 担当 6 文書で失敗。最終実行では 504 件。リンク先 URL と Markdown link target は更新済みスクリプトで除外されていることを確認した。
- `check-markdown-whitelist.js --list-unknown`: 失敗。残件は自然文の技術用語、既存 whitelist description 内の英語、脚注ラベル、識別子由来の語が混在する。whitelist 候補は自然文として表示される語だけを下に整理する。
- whitelist 候補: `AutoRef`, `Blazor`, `CaptureOn`, `CaptureOff`, `CaptureReplay`, `DebugHost`, `Diagnostics`, `ER-FORCE`, `RuntimeHost`, `SSL-Vision`, `SSL_WrapperPacket`, `Tracker.Server`, `TrackerCoordinator`, `UI`, `CLI`, `JSON`, `JSONL`, `UDP`, `TDD`, `PR`, `README`。
- whitelist 候補: `alignment`, `best-effort`, `bounded`, `cadence`, `comparison`, `contract`, `degraded`, `diagnostics`, `endpoint`, `fallback`, `field`, `geometry`, `immutable`, `latest-before`, `overlay`, `packet`, `profile`, `raw`, `read-side`, `receiver`, `replay`, `sample`, `sidecar`, `snapshot`, `source`, `timeline`, `tracker`, `unsupported`。
- whitelist 候補: `button`, `cache`, `component`, `DTO`, `index`, `legend`, `marker`, `modal`, `options`, `pan`, `playback`, `scrub`, `scrubber`, `selector`, `store`, `toggle`, `viewport`, `zoom`。
- whitelist 候補: `キャプチャ`, `サイドカー`, `サンプル`, `スナップショット`, `セッション`, `デバッグ`, `トラッカー`, `パケット`, `プロファイル`, `メタデータ`, `ループ`, `ログ`, `レビュー`。
- whitelist 候補から除外: Markdown link target、URL、ファイルパス、コマンド引数、パス由来の inline code、脚注ラベルだけに出る語。

## 結果

- tracking archive の英語見出し、表ラベル、状態値、一般説明を日本語化した。
- DebugHost / RuntimeHost 設計文書では、自然文に混ざった `official`、`packet capture`、`render snapshot`、`metadata`、`session folder`、`project / namespace` などの説明語を可能な範囲で日本語へ寄せた。
- 設定名、型名、UI 表示名、タスク ID、ファイル名、既存の設計用語として必要な英語は残し、whitelist 候補として整理した。
- `npm run lint:md` 全体実行は対象外のため未実行。

## リスク

- whitelist 未追加のため、担当 6 文書の `cspell` と `check-markdown-whitelist.js` はまだ失敗する。
- `check-markdown-whitelist.js --files` は担当ファイルに加えて既存 whitelist description も検査するため、担当文書外由来の未登録語が `--list-unknown` に混ざる。
- 脚注ラベルや識別子由来の語は自然文候補から除外した。lint 出力上は残る場合があり、最終的な除外範囲は lint 側の仕様確認が必要。
