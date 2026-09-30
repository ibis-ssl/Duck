# PR #30 Markdown lint skill 参照導入報告

日付: 2026-10-01

## 目的

Markdown 文書の厳格な許可一覧検査を、`CodexSkill` を単一の正本として参照しながら Windows と Linux の両方で同じ手順で実行できるようにする。Duck へ skill 本体は複製せず、`.agents/skills` のディレクトリ `symlink` で接続する。

## 構成

- `.agents/skills`
  - 隣接して取得した `CodexSkill` の `skills/` をディレクトリ `symlink` で参照する。
  - `review-enforcer` の実体は Duck に複製せず、`CodexSkill` を正本とする。
  - `CodexSkill` PR #89 の `scripts/link_consumer_skills.py` が OS 共通のリンク作成入口を担当する。
  - CSpell は `review-enforcer` 側で `--no-default-configuration` を使用し、Windows でも JavaScript CLI を Node から直接起動する。
- `tools/lint/scripts`
  - `run-markdown-targets.js`: 対象列挙後に textlint / CSpell を OS 非依存で起動する。
  - `run-python-script.js`: `.venv` を優先し、利用可能な Python 3 を OS ごとに解決する。
  - `setup-python-lint.js`: `.venv` 作成と Python 依存導入を行う。
  - `run-markdown-lint-ci.js`: セットアップと lint の標準出力・標準エラー・終了値・判定結果を artifact 用に保存する。
- `package.json`
  - `npm run lint:md:setup` を追加した。
  - `lint:md:link-skills` を追加し、隣接する `CodexSkill` checkout から `.agents/skills` を作成する。
  - `lint:md:text` / `lint:md:spell` / `lint:md:whitelist` は symlink 先の `review-enforcer` と OS 共通ラッパーを使用する。
- `tools/lint/requirements.txt`
  - 厳格 lint に必要な `sudachipy==0.6.11`、`sudachidict_core==20260428`、`PyYAML==6.0.3` に限定した。
  - ChikkarPy は許可一覧検査に不要で、現行 Python で配布物の問題により導入できないため必須依存から外した。
- `.github/workflows/markdown-lint.yml`
  - Ubuntu と Windows の両方で同じ Markdown lint を実行する。
  - `CodexSkill` の CSpell Windows 修正を含む commit `583a9594d8157fc101dff3c6b3338238809d9779` を取得し、`.agents/skills` を作成してから lint を実行する。
  - 成否にかかわらず、判定結果、セットアップと lint の標準出力・標準エラー、終了値、Node / npm / Python / Git / npm / pip の診断情報を artifact として保存する。

## 対象範囲

`.agents` はローカル参照用のため Git 管理とプロジェクト Markdown 文書の検査対象から除外する。`review-enforcer` は `CodexSkill` 側の検査器そのものであり、Duck の設計書・README と同じ許可一覧を重ねて適用しない。

`.venv` も依存物の保存先であるため対象外とする。

## ローカル検証

Windows 実機で次を確認した。

- `npm run lint:md:link-skills`: 成功。
- `.agents/skills` は `C:\\Users\\donabe\\RemoteDesktopWorkspace\\CodexSkill\\skills` を指す `symlink` であることを確認。
- `npm run lint:md:setup`: 成功。
- `npm run lint:md`: 終了値 0。
- Markdown 対象: 18 文書。
- CSpell: 18 文書、指摘 0。
- SudachiPy 許可一覧検査: 成功。
- `git diff --check`: 成功。

初回検証では `.venv` 内の依存パッケージ Markdown が列挙されたため、`.venv` を対象外へ追加した。その後の全体検査は成功した。

## コミット

- `12fa959` `chore: vendor review-enforcer skill`（後続の symlink 化で取り消し）
- `319c010` `chore: make markdown lint cross-platform`
- `e90cbc5` `ci: verify markdown lint on Windows and Linux`
- `3e19024` `chore: link markdown lint skill from CodexSkill`

## PR 初回 CI

PR #30 の HEAD `e90cbc51767647129973e3fb275c055931dea948` と head SHA が一致する run だけを確認した。

- Markdown lint run `36726996756`: success。
  - `Markdown lint (ubuntu-latest)`: success。
  - `Markdown lint (windows-latest)`: success。
  - Linux artifact: `markdown-lint-Linux-36726996756-1`。
  - Windows artifact: `markdown-lint-Windows-36726996756-1`。
  - 両 artifact の `test-result.txt`: `status=passed`、`setup_exit_code=0`、`lint_exit_code=0`。
- `.NET tests` run `36726996189`: success。

別 SHA の run は判定に使用していない。

## 完了状態

`review-enforcer` を Duck 内へ複製せず、`CodexSkill` clone を `.agents/skills` symlink で参照して Windows と Linux の両方で厳格 Markdown lint を実行する構成になった。最終 report 追加後に HEAD が変わるため、最終 HEAD と一致する CI は PR コメントへ記録する。
