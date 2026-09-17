# Sub-agent実行レポート

## タスク

root / workflow / lint 説明 Markdown の lint 修正案と本文修正を実行する。

## sub-agentを使う理由

ユーザー指定により、lint 修正作業をサブエージェントで分担するため。

## 対象範囲

- `AGENTS.md`
- `README.md`
- `tools/lint/README.md`
- `feedback-points/feedback-points.md`

## 対象外

- `tools/lint/markdown-whitelist.yaml` の編集
- `reports/**`
- Tracker 配下の Markdown

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files AGENTS.md README.md tools/lint/README.md feedback-points/feedback-points.md reports/doc-lint-root-docs-worker-20260516235249.md`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files AGENTS.md README.md tools/lint/README.md feedback-points/feedback-points.md --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files AGENTS.md README.md tools/lint/README.md feedback-points/feedback-points.md --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files AGENTS.md README.md tools/lint/README.md feedback-points/feedback-points.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --stdin <file> --list-unknown < <file>`

## 対象ファイル

- `AGENTS.md`
- `README.md`
- `tools/lint/README.md`
- `feedback-points/feedback-points.md`
- `reports/doc-lint-root-docs-worker-20260516235249.md`

## 指摘事項

- `textlint`: 担当4ファイルで通過。
- `cspell`: 担当4ファイルのうち `feedback-points/feedback-points.md` と `tools/lint/README.md` は通過。`AGENTS.md` は `Codex`, `Serena` が未登録。`README.md` は `Duck`, `SSL-Vision`, `Tracker`, `CaptureOn`, `ASP.NET Core`, `Codex` が未登録。
- `check-markdown-whitelist.js --files`: 担当ファイル内の未登録語に加え、スクリプト仕様により `tools/lint/markdown-whitelist.yaml` の既存説明文も検査されるため、`AutoRef`, `diagnostics`, `replay`, `snapshot`, `overlay`, `tick` など対象外ファイル由来の未登録語も残る。
- 担当ファイル由来の主な whitelist 候補:
  - 固有名詞・ツール名: `Codex`, `Serena`, `MCP`, `.NET`, `CLI`, `NuGet`, `Duck`, `SSL`, `SSL-Vision`, `Tracker`, `CaptureOn`, `ASP.NET Core`, `SDK`, `API`, `npm`, `Git`, `codex`, `exec`
  - 一般的なカタカナ語: `スキル`, `ユーザー`, `プロジェクト`, `シンボリックリンク`, `サンドボックス`, `ビルド`, `キャッシュ`, `プロジェクトローカル`, `ホームディレクトリ`, `ノード`, `サブエージェント`, `セッション`, `ツール`, `ワークスペース`, `レビュー`, `レポート`, `リポジトリ`, `マークダウン`, `スクリプト`, `コミット`, `ローカル`, `ディレクトリ`, `フォルダー`, `ファイル`, `パターン`, `バッククォート`, `インラインコード`, `コードブロック`, `コマンド`, `ファイルパス`, `ラベル`, `プロファイル`, `トラッカー`, `トラッカーパケット`, `パケット`, `データ`, `ビューアー`, `サーバー`, `キャプチャー`, `ライブラリ`, `コンポーネント`, `プロトコル`, `テスト`, `ロボット`

## 結果

- 英語の一般語や説明文に混ざった不要な英語を日本語へ置換した。
- `tools/lint/markdown-whitelist.yaml` と `cspell.config.jsonc` は編集していない。
- Tracker 配下の Markdown と他 worker の変更には触れていない。
- `AGENTS.md` は作業対象として本文修正済みだが、現在の Git 管理対象には含まれていないため `git status --short -- AGENTS.md` には表示されない。

## リスク

- whitelist 候補を `tools/lint/markdown-whitelist.yaml` に追加するまでは、担当範囲の `cspell` と `check-markdown-whitelist.js` は失敗する。
- `check-markdown-whitelist.js --files` は既存 whitelist 説明文も検査するため、担当ファイルの候補を追加しても、既存 whitelist 説明文由来の未登録語を別途整理しない限り全体の whitelist 検査は失敗し得る。
- `feedback-points/feedback-points.md` は通常は専用スキル経由で更新する台帳だが、今回はユーザー指定の対象範囲に含まれていたため lint 本文修正に限定して編集した。
