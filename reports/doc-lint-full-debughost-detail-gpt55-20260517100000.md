# Sub-agent実行レポート

## タスク

DebugHost 詳細設計 2 ファイルの Markdown lint 指摘だけを対象に修正案を作る。

## sub-agentを使う理由

DebugHost の同系統文書をまとめ、`gpt-5.5 high` の worker に任せるため。

## 対象範囲

`Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
`Tracker/Design/DebugHost/debug-host-maintainability-design.md`

## 対象外

ホワイトリストの直接編集、lint に出ていない箇所の言い換え、機械的な全文置換。

## 実行コマンド

- `sed -n '1,240p' reports/doc-lint-full-debughost-detail-gpt55-20260517100000.md`
- `git status --short -- Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md Tracker/Design/DebugHost/debug-host-maintainability-design.md reports/doc-lint-full-debughost-detail-gpt55-20260517100000.md tools/lint/markdown-whitelist.yaml`
- `node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md --list-unknown`
- `./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js Tracker/Design/DebugHost/debug-host-maintainability-design.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/DebugHost/debug-host-maintainability-design.md --list-unknown`
- `./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules Tracker/Design/DebugHost/debug-host-maintainability-design.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --stdin Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md --list-unknown < Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --stdin Tracker/Design/DebugHost/debug-host-maintainability-design.md --list-unknown < Tracker/Design/DebugHost/debug-host-maintainability-design.md`

補足: 直接 `textlint` は PATH 上に無かったため、ローカル依存の `./node_modules/.bin/textlint` を使った。`run-cspell-markdown.js --issues 5000 ...` も試したが、cspell 9.2.1 では `--issues` が unknown option のため lint 判定から除外した。

## 対象ファイル

- 確認対象:
  - `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
  - `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
- 変更対象:
  - `reports/doc-lint-full-debughost-detail-gpt55-20260517100000.md`
- 変更しなかったファイル:
  - `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
  - `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
  - `tools/lint/markdown-whitelist.yaml`

## 指摘事項

- `debug-host-cli-ui-detail-design.md`
  - `run-cspell-markdown.js` は unknown word を 100 件表示して失敗した。代表例は `official`、`capture`、`ibis`、`packet`、`session`、`metadata`、`diagnostics`、`snapshot`、`sidecar`、`alignment`、`source`、`tracked frame` 周辺の用語。
  - `check-markdown-whitelist.js --files ... --list-unknown` は失敗した。対象本文だけを `--stdin ... --list-unknown` で確認すると unknown term は 340 種だった。
  - `textlint` は指摘なしで通過した。
- `debug-host-maintainability-design.md`
  - `run-cspell-markdown.js` は unknown word を 100 件表示して失敗した。代表例は `diagnostics`、`project`、`namespace`、`profile switch`、`capture`、`receiver`、`class`、`property`、`method`、`entrypoint`、`partial class`、`exit code` 周辺の用語。
  - `check-markdown-whitelist.js --files ... --list-unknown` は失敗した。対象本文だけを `--stdin ... --list-unknown` で確認すると unknown term は 108 種だった。
  - `textlint` は指摘なしで通過した。
- `--files ... --list-unknown` は whitelist description も同時に検査する実装のため、本文だけの候補確認には `--stdin` を併用した。

## 結果

- 設計本文 2 ファイルは変更しなかった。
- textlint 指摘は無かった。
- cspell / whitelist 指摘は、通常の技術名、設計上意味が固定された複合語、UI ラベル、保存形式名、状態名が大半だった。機械的な置換、英単語の雑なカタカナ化、意味を壊す翻訳、lint 回避目的のバッククォート化は禁止事項に抵触するため実施しなかった。
- `tools/lint/markdown-whitelist.yaml` は既存の作業ツリー変更がある状態で、かつ今回の禁止事項でも直接編集不可のため編集しなかった。
- ホワイトリスト候補:
  - Capture / 保存系: `packet capture`、`capture metadata`、`session folder`、`sidecar JSONL`、`snapshot sidecar`、`alignment sidecar`、`render snapshot`、`diagnostics sidecar`、`diagnostics sample sidecar`、`raw payload`、`round-trip`。
  - トラッカー比較系: `official tracker packet`、`3rdparty tracker packet`、`tracker packet snapshot`、`tracker snapshot alignment`、`tracker snapshot comparison`、`semantic summary`、`source identity`、`source role`、`source label`、`aggregate source`、`remote endpoint`、`tracked frame`。
  - 再生 / UI 系: `diagnostics log`、`diagnostics replay`、`diagnostics playback`、`replay timeline`、`timeline scrubber`、`playback tick`、`Field source selector`、`comparison panel`、`profile switch`、`live receiver`、`multicast endpoint`、`publish endpoint`、`best-effort`、`degraded legacy`。
  - 保守性設計系: `diagnostics UI`、`project`、`namespace`、`entrypoint`、`orchestration`、`markup`、`view state`、`option parsing`、`partial class`、`type-owned folder`、`observable output`、`XML documentation comment`、`exit code`、`error message`。
  - 日本語カタカナ語: `ファイル`、`コメント`、`ログ`、`レビュー`、`タスク`、`リスク`。通常語として残すか、文書側で漢語へ寄せるかは親判断が必要。

## リスク

- 現状のままでは対象 2 ファイルの cspell / whitelist gate は未通過。
- ホワイトリスト追加なしに通過させるには、設計本文全体の大規模な用語置換が必要になり、今回の「機械的な置換禁止」「lint に出ていない箇所を言い換えない」「意味固定の複合語は無理に訳さない」という条件と衝突しやすい。
- `tools/lint/markdown-whitelist.yaml` は直接編集していないため、上記候補は利用者または親による意味確認が必要。
