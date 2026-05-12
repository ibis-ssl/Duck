# Sub-agent実行レポート

## タスク

`TRACKER-041` 全 tracker 保存・snapshot replay 方針への設計修正。

## sub-agentを使う理由

親エージェントは実装・調査を行わず、サブエージェントの報告を読んで判断するため。

## 対象範囲

- self 除外前提を取り下げる
- 存在する tracker packet をすべて保存し、自身の詳細ログとの重複保持を許容する設計へ変更する
- 3rd party tracker も snapshot を保持し、後で再生できる設計へ変更する
- tracking を設計変更後の TDD 順へ同期する
- PR #9 へ commit / push する

## 対象外

- production implementation
- テストコード変更
- PR の ready 化

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/progress-sync-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/task-consistency-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/git-commit-manager/SKILL.md`
- `sed -n '1,260p' reports/tracker-041-all-trackers-design-audit-20260512111218.md`
- `sed -n '1,260p' reports/tracker-041-all-trackers-implementation-audit-20260512111218.md`
- `sed -n '1,260p' reports/tracker-041-implementation-20260512110523.md`
- `sed -n '1,260p' reports/tracker-041-tdd-tests-20260512105825.md`
- `git status --short --branch`
- `gh pr view 9 --json number,title,state,isDraft,headRefName,baseRefName,url,headRepositoryOwner`
- `rg -n "TRACKER-041|TRACKER-042|TRACKER-043|TRACKER-044|self|self除外|3rdparty|thirdparty|tracker packet|snapshot|CaptureReplay|diagnostics|playback|sidecar|比較ログ|比較|保存|再生" Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md Tracker/Tracker.Server/README.md`
- `nl -ba Tracker/Tracker.Core/Design/tasks-status.md | sed -n '1,70p'`
- `nl -ba Tracker/Tracker.Core/Design/phases-status.md | sed -n '1,45p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '1,135p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '108,148p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '496,560p'`
- `git diff --check`
- `git diff --name-status`
- `git add Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `git diff --cached --name-status`
- `git diff --cached --check`
- `git commit -m "docs(tracker): 全tracker保存方針へ設計を修正" ...`
- `git push origin feat/tracker-captureon-compare-log`
- `git rev-parse HEAD`
- `gh pr view 9 --json number,title,state,isDraft,headRefName,baseRefName,url`

## 対象ファイル

- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `reports/tracker-041-all-trackers-design-fix-20260512111628.md`

## 指摘事項

- Blocking normal-path problems: no findings in this design/tracking fix scope.
- 現 self 除外 test / production 実装は新方針と矛盾する。次作業では review へ進めず、先に test contract を all tracker 保存へ修正する必要がある。
- `Tracker.Server/README.md` は未変更。README 反映は更新後の実装・UI/運用証跡と合わせて `TRACKER-045` の範囲で扱う。

## 結果

- 現在 branch: `feat/tracker-captureon-compare-log`
- PR #9: `https://github.com/ibis-ssl/Duck/pull/9`
  - state: `OPEN`
  - draft: `true`
  - base: `main`
  - head: `feat/tracker-captureon-compare-log`
- `tracker-server-cli-ui-detail-design.md` と `tracker-architecture-plan.md` から、self 除外を保存要件として扱う記述を取り下げた。
- 設計を、見えている official `TrackerWrapperPacket` をすべて tracker packet snapshot sidecar JSONL へ保存する方針へ更新した。ibis 自身の official packet と詳細ログの重複保持は許容する。
- self / 3rdparty / unknown の判別は、保存除外ではなく後続表示・比較用の role / label / metadata として扱う方針へ変更した。判別不能でも record は落とさない。
- 3rdparty tracker snapshot は `receivedAt`、remote endpoint、`uuid`、`sourceName`、role / label、tracked frame number / timestamp、raw payload 参照または復元可能情報、summary を持つ方針へ更新した。
- `Tracker.CaptureReplay`、diagnostics viewer、diagnostics playback が session folder 内の snapshot log を読み、3rdparty tracker snapshot を再生・比較表示できる入力契約を追記した。
- `tasks-status.md` / `phases-status.md` を、`TRACKER-041` は設計・tracking 修正、`TRACKER-042` は all tracker 保存 contract への test 修正、`TRACKER-043` 以降は metadata / snapshot 保存 / replay 表示の順に同期した。
- `git diff --check`: 問題なし。
- 設計/tracking commit hash: `9e9bddd4f964cbafa872a62f5b1150a4831952a8`
- push 結果: `71e7805..9e9bddd  feat/tracker-captureon-compare-log -> feat/tracker-captureon-compare-log`
- 設計/tracking push 後の `git status --short --branch`: `## feat/tracker-captureon-compare-log...origin/feat/tracker-captureon-compare-log`、未追跡 `reports/tracker-041-all-trackers-design-audit-20260512111218.md`、`reports/tracker-041-all-trackers-design-fix-20260512111628.md`、`reports/tracker-041-all-trackers-implementation-audit-20260512111218.md`。

## リスク

- 実装コード・テストコードは対象外のため未変更。現 `MultiTrackerManager` と `TrackerConnectionLibThirdPartyTrackerTests` は self 除外前提のまま残っている。
- 次作業で test contract を all tracker 保存へ修正するまで、既存 focused test 成功は新方針の成功証跡として扱えない。
- snapshot replay 実装は `TRACKER-043` 以降に残る。特に raw payload の保持方式、metadata index、diagnostics playback の timeline 対応は後続 review で確認が必要。
