# Sub-agent実行レポート

## タスク

Tracker Core / status 系 Markdown の lint 修正案と本文修正を実行する。

## sub-agentを使う理由

ユーザー指定により、lint 修正作業をサブエージェントで分担するため。

## 対象範囲

- `Tracker/Design/Archive/Core/phases-status.md`
- `Tracker/Design/Archive/Core/tasks-status.md`
- `Tracker/Design/Core/tracker-architecture-plan.md`
- `Tracker/Design/Core/tracker-core-engine-detail-design.md`
- `Tracker/Design/Core/tracker-history-000-038.md`
- `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
- `Tracker/Design/phases-status.md`
- `Tracker/Design/tasks-status.md`

## 対象外

- `tools/lint/markdown-whitelist.yaml` の編集
- `reports/**`
- DebugHost / RuntimeHost design docs
- Tracker README 群
- root docs

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/Design/Archive/Core/phases-status.md Tracker/Design/Archive/Core/tasks-status.md Tracker/Design/Core/tracker-architecture-plan.md Tracker/Design/Core/tracker-core-engine-detail-design.md Tracker/Design/Core/tracker-history-000-038.md Tracker/Design/Core/tracker-test-maintainability-detail-design.md Tracker/Design/phases-status.md Tracker/Design/tasks-status.md`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files <担当ファイル> --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files <担当ファイル> --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files <担当ファイル>`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files <担当ファイル> --list-unknown`
- `git diff --stat -- <担当ファイルと本report>`

## 対象ファイル

- 変更あり:
  - `Tracker/Design/Archive/Core/phases-status.md`
  - `Tracker/Design/Archive/Core/tasks-status.md`
  - `Tracker/Design/Core/tracker-architecture-plan.md`
  - `Tracker/Design/Core/tracker-core-engine-detail-design.md`
  - `Tracker/Design/phases-status.md`
  - `Tracker/Design/tasks-status.md`
  - `reports/doc-lint-tracker-design-core-worker-20260516235249.md`
- 確認のみ:
  - `Tracker/Design/Core/tracker-history-000-038.md`
  - `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`

## 指摘事項

- `textlint`: 担当 8 ファイルでは指摘なし。
- `cspell`: 担当 8 ファイルで未登録語により失敗。出力は 800 件で上限到達。
- `check-markdown-whitelist.js`: 未登録語により失敗。表示上は先頭 200 件と追加 8,101 件、`--list-unknown` では 745 unique term。
- 主な未解消語のうち、自然文として表示される本文由来:
  - プロジェクト / 固有名詞: `Tracker`, `RuntimeHost`, `DebugHost`, `AutoRef`, `CaptureOn`, `CaptureReplay`, `Tigers`, `Ibis`, `Duck`, `ER-Force`, `Vision`, `Field`
  - tracker 領域語: `diagnostics`, `raw`, `packet`, `frame`, `snapshot`, `sidecar`, `replay`, `capture`, `geometry`, `detection`, `camera`, `timestamp`, `cadence`, `alignment`, `profile`, `Kalman`
  - 進捗・検証語: `review`, `passed`, `focused`, `blocking`, `findings`, `done`, `PR`, `evidence`, `validation`, `draft`, `ready`
  - コード / テスト語: `class`, `method`, `property`, `namespace`, `fixture`, `helper`, `contract`, `test`, `tests`, `build`
  - UI / 操作語: `UI`, `CLI`, `Play`, `Fast`, `Forward`, `Stop`, `active`, `selected`
  - 自然なカタカナ語: `コメント`, `タスク`, `ファイル`, `ログ`, `セット`, `レポート`, `テスト`, `ユーザー`, `フェーズ`, `レビュー`, `ブロッカー`, `モデル`
- lint 側修正待ちとして whitelist 候補から除外するもの:
  - Markdown link target、URL、ローカルファイルパスに含まれる語。例: `home`, `ibis`, `ssl`, `IbisDuck`, `reports`, `tracker-history-000-038`, `appsettings`
  - コマンド引数や inline code 内のパス由来の語。例: `FullyQualifiedName`, `nr`, `false`, `filter`, `csproj`, `jsonl` がパスまたはコマンド構造として出たもの。
  - `path` / `file` / `folder` / `directory` は自然文の概念として出ている箇所もあるが、今回の report ではリンク先・パス由来の混入があるため、本文候補へは含めない。
- whitelist 候補:
  - 固有名詞・製品名・タスク名は whitelist 候補にする。例: `Tracker`, `RuntimeHost`, `DebugHost`, `AutoRef`, `CaptureOn`, `CaptureReplay`, `Tigers`, `ER-Force`, `Vision`, `Field`
  - 設計・診断ドメイン語は whitelist 候補にする。例: `diagnostics`, `raw`, `packet`, `snapshot`, `sidecar`, `replay`, `cadence`, `alignment`, `profile`, `Kalman`
  - 進捗管理で値として使う語は whitelist 候補にする。例: `review`, `passed`, `focused`, `blocking`, `findings`, `done`, `draft`, `ready`
  - 一般的なカタカナ語は whitelist 候補にする。例: `コメント`, `タスク`, `ファイル`, `ログ`, `レポート`, `テスト`, `ユーザー`, `フェーズ`, `レビュー`, `ブロッカー`

## 結果

- 対象ファイルの確認は指定の `list-markdown-targets.js --files` で実施し、担当 8 ファイルだけが対象になることを確認した。
- 本文修正では、自然文として表示される説明文中の一般英語を日本語へ寄せた。例: `merge` を `マージ`、`canonical design root` を `正本の設計ルート`、`blocking findings` を `ブロッカー指摘`、`source of truth` を `正本`、`button` を `ボタン` へ変更した。
- リンク先 URL、Markdown link target、ファイルパス、コマンド引数、inline code 内のパス由来の lint 指摘は本文修正や whitelist 候補に含めない方針へ追加修正した。
- ステータス値、タスク ID、クラス名、ファイル名、CLI 引数、UI 表示名は、意味や追跡形式を壊さないため必要に応じて残した。
- `tools/lint/markdown-whitelist.yaml`、`cspell.config.jsonc`、対象外 report、DebugHost / RuntimeHost design docs、README 群、root docs は編集していない。
- 最終結果:
  - `textlint`: pass
  - `cspell`: fail。未登録語が残るため。
  - `check-markdown-whitelist.js`: fail。未登録語が残るため。

## リスク

- whitelist を編集していないため、担当範囲の Markdown lint 全体はまだ通らない。残語は report の whitelist 候補をレビューしてから `tools/lint/markdown-whitelist.yaml` へ登録する必要がある。
- リンク先 URL、Markdown link target、ファイルパス、コマンド引数、inline code 内のパス由来の語が lint 対象に混入する不具合は別途修正待ち。これらは本文修正対象でも whitelist 候補でもなく、lint 側修正後に再集計が必要。
- `tasks-status.md` / `phases-status.md` は進捗ファイルとして英語キーや状態値を含むため、本文だけで全未登録語を消すと追跡形式を壊す可能性がある。
- `tracker-history-000-038.md` と保守性設計書は履歴・設計識別子が多く、本文を過度に置換すると過去証跡の意味が変わるため、今回の修正は明らかな一般語に限定した。
