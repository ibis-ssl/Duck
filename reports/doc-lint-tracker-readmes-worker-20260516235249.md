# Sub-agent実行レポート

## タスク

Tracker README 群の lint 修正案と本文修正を実行する。

## sub-agentを使う理由

ユーザー指定により、lint 修正作業をサブエージェントで分担するため。

## 対象範囲

- `Tracker/README.appsettings.md`
- `Tracker/Tracker.CaptureReplay/README.md`
- `Tracker/Tracker.DebugHost/README.md`
- `Tracker/Tracker.RuntimeHost/README.md`

## 対象外

- `tools/lint/markdown-whitelist.yaml` の編集
- `reports/**`
- `Tracker/Design/**`
- root docs

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md reports/doc-lint-tracker-readmes-worker-20260516235249.md`
- `textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md`（`textlint` が PATH になく失敗）
- `node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md --list-unknown`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md`

## 対象ファイル

- 変更: `Tracker/README.appsettings.md`
- 変更: `Tracker/Tracker.CaptureReplay/README.md`
- 変更: `Tracker/Tracker.DebugHost/README.md`
- 変更: `Tracker/Tracker.RuntimeHost/README.md`
- 追記: `reports/doc-lint-tracker-readmes-worker-20260516235249.md`

## 指摘事項

- `textlint`: 担当ファイル限定で成功。
- `cspell`: 担当ファイル限定で失敗。残件は whitelist 未登録語、Markdown 記法由来の語、ファイル名 / パス由来の語。
- `check-markdown-whitelist.js --files`: 担当ファイル限定で失敗。本文として残すべき固有名詞、略語、自然なカタカナ語が未登録。
- whitelist 候補:
  - 固有名詞 / 技術名: `SSL-Vision`, `Vision`, `ibis`, `CaptureOn`, `ER-Force`, `Docker`, `ASP.NET Core`, `.NET SDK`, `protobuf`。
  - 通信 / 形式 / 単位 / 略語: `UDP`, `HTTP`, `HTTPS`, `API`, `UI`, `CLI`, `JSON`, `JSONL`, `UUID`, `ID`, `NIC`, `OS`, `I/O`, `ns`, `ms`, `mm`, `rad`, `Hz`。
  - 自然なカタカナ語: `トラッカー`, `トラッカーエンジン`, `トラッカーパケット`, `プロファイル`, `パケット`, `キャプチャ`, `キャプチャファイル`, `キャプチャセッション`, `メタデータ`, `セッション`, `セッションフォルダ`, `フレーム`, `フィールド`, `ロボット`, `ボール`, `キック`, `チップ`, `マルチキャスト`, `ユニキャスト`, `アドレス`, `ポート`, `インターフェイス`, `カメラ`, `ログ`, `パネル`, `タイムライン`, `フィルター`, `モード`, `ラベル`, `フォルダ`, `ファイル`, `ディレクトリ`, `ブラウザ`, `コンソール`, `エラー`, `ノイズ`, `リセット`, `カルマン`, `シミュレータ`。
- whitelist 候補から除外したもの:
  - Markdown link target、URL、ファイルパス、コマンド引数、inline code 内のパス由来の指摘。
  - Markdown footnote label など、表示本文ではない記法由来の指摘。

## 結果

- 英語の一般語や説明文に混ざっていた `tracked frame`, `robot tracker`, `ball tracker`, `kick detector`, `reorder window`, `event time`, `timestamp`, `world frame`, `capture file`, `summary`, `detail`, `latency analysis`, `option`, `source filter`, `sidecar status` などを日本語本文へ修正。
- UI 表示名、CLI 名、設定キー、ファイル名、プロジェクト固有名は必要な範囲で保持。
- `tools/lint/markdown-whitelist.yaml`、`cspell.config.jsonc`、`Tracker/Design/**`、root docs は編集していない。

## リスク

- whitelist は未編集のため、担当ファイル限定の `cspell` と `check-markdown-whitelist.js --files` は未登録語で失敗する。
- リンク先 URL、Markdown link target、ファイルパス、コマンド引数、inline code 内のパス由来の lint 指摘は、別 worker の lint 側修正待ちとして扱い、本レポートの whitelist 候補には含めていない。
- Markdown footnote label など表示本文ではない記法由来の語も lint 対象に残っている。本文修正ではなく lint 側の扱い確認が必要。

## 2026-05-17 追記: Markdown アドレス部除外後の再確認

追加指示に従い、最新の `/home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md` と `/home/ibis/AI/CodexSkill/skills/review-enforcer/scripts/*` を読み直した。`review-enforcer` には Markdown link address を spelling / whitelist 対象から外す規則が追加され、`run-cspell-markdown.js` と `check-markdown-whitelist.js` にも inline link / reference link の address 部分を除外する処理が入っていることを確認した。

再実行コマンド:

- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/scripts/check-markdown-whitelist.js`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/review-enforcer/scripts/run-cspell-markdown.js`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/review-enforcer/scripts/list-markdown-targets.js`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md reports/doc-lint-tracker-readmes-worker-20260516235249.md`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md --list-unknown`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --stdin <file> --list-unknown < <file>` を担当 README 4 件に個別実行し、既存 whitelist description 由来語を除いた担当本文側の候補を確認。

再確認結果:

- `list-markdown-targets.js --files`: 担当 README 4 件のみ対象。report は `reports/**` 対象外のため列挙されない。
- `textlint`: 担当 README 4 件で成功。
- `run-cspell-markdown.js`: 担当 README 4 件で失敗。残件は本文に表示される未登録語、footnote label 由来語、表示リンクテキストのファイル名など。
- `check-markdown-whitelist.js --files`: 担当 README 4 件で失敗。`--files` でも既存 whitelist description も検査されるため、担当本文以外の未登録語も混ざる。

Markdown link target / URL / address 除外後の whitelist 候補は、以下を現時点の候補として扱う。前段の候補リストよりこちらを優先する。

- 固有名詞 / 技術名: `SSL-Vision`, `Vision`, `ibis`, `CaptureOn`, `ER-Force`, `Docker`, `ASP.NET Core`, `.NET SDK`, `protobuf`。
- 通信 / 形式 / 単位 / 略語: `UDP`, `HTTP`, `HTTPS`, `API`, `UI`, `CLI`, `JSON`, `JSONL`, `UUID`, `ID`, `NIC`, `OS`, `I/O`, `ns`, `ms`, `mm`, `rad`, `Hz`, `CI`, `AND`, `URL`。
- 自然なカタカナ語: `トラッカー`, `トラッカーエンジン`, `トラッカーパケット`, `トラッカープロファイル`, `プロファイル`, `パケット`, `パケットキャプチャ`, `キャプチャ`, `キャプチャファイル`, `キャプチャセッション`, `キャプチャメタデータ`, `メタデータ`, `セッション`, `セッションフォルダ`, `フレーム`, `フィールド`, `ロボット`, `ボール`, `キック`, `チップ`, `マルチキャスト`, `ユニキャスト`, `アドレス`, `ポート`, `インターフェイス`, `カメラ`, `ログ`, `パネル`, `タイムライン`, `フィルター`, `モード`, `ラベル`, `フォルダ`, `ファイル`, `ファイルサイズ`, `ファイルパス`, `ディレクトリ`, `ブラウザ`, `コンソール`, `エラー`, `ノイズ`, `リセット`, `カルマン`, `シミュレータ`, `コマンドライン`, `ツール`, `データ`, `ペイロード`, `ソケット`, `モーダル`, `タブ`, `ボタン`, `カウンタ`, `リポジトリ`, `ルートディレクトリ`, `ローカル`。

候補から除外した残件:

- Markdown link target / URL / address 部分由来の語。今回の script 更新後は担当 README の主要なリンク先パス由来語は対象から外れている。
- `appsettings.json` など、表示リンクテキストまたはファイル名として出ている語。
- `tracker-snapshot`, `tracker-packet-snapshot`, `unified-replay-timeline`, `comparison-display-items`, `sidecar-status`, `reorder-window`, `tracked-frame` などの footnote label / footnote reference 由来語。
- コマンド引数、パス、inline code 内の識別子由来語。

残リスク:

- `tools/lint/markdown-whitelist.yaml` は未編集のため、担当 README 限定 lint は引き続き失敗する。
- `check-markdown-whitelist.js --files` は既存 whitelist description も検査するため、担当 README 以外の description 由来未登録語が混ざる。担当本文のみの候補整理には `--stdin` 個別実行の結果を使った。
- footnote label / footnote reference 由来語は、表示本文ではないが現行 lint では残っている。本文 whitelist 候補には入れていない。
