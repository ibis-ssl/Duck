# Sub-agent実行レポート

## タスク

RuntimeHost / DebugHost 分離 scope の残タスク切り出し draft を作成する。

## sub-agentを使う理由

`task-breakdown-planner` の large-scope delegation 条件に従い、残作業が 5 task 以上かつ verification / implementation / review の依存関係を整理する必要があるため。

## 対象範囲

- `Tracker/Design/tasks-status.md`
- `Tracker/Design/phases-status.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- RuntimeHost / DebugHost 分離 scope の残作業

## 対象外

- tracking file の直接編集
- 実装、test 作成、build / test 実行
- commit / PR 更新
- `RAW-VISION-*` / `TRACKER-*` の新規追加
- AutoRef logic 実装
- 旧ログ完全互換の復活

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/task-breakdown-planner/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `rg -n "RuntimeHost|DebugHost|RUNTIME-HOST|RAW-VISION|TRACKER-" /home/ibis/.codex/memories/MEMORY.md`
- `sed -n '1,260p' reports/runtime-host-task-breakdown-draft-20260514162526.md`
- `sed -n '1,260p' Tracker/Design/tasks-status.md`
- `sed -n '1,220p' Tracker/Design/phases-status.md`
- `sed -n '1,260p' Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `sed -n '1,260p' Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `sed -n '1,280p' Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `rg --files Tracker/Design/Core`
- `rg -n "RuntimeHost|DebugHost|diagnostics sample|operation loop|publish|Web UI|project reference|AutoRef|legacy|旧" Tracker/Design/Core/*.md Tracker/Design/RuntimeHost/runtime-host-plan.md Tracker/Design/DebugHost/*.md`
- `git status --short`
- `find Tracker -maxdepth 2 -type f \( -name '*.csproj' -o -name '*.sln' \) | sort`
- `sed -n '300,335p' Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `sed -n '285,310p' Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `sed -n '1,130p' Tracker/Design/Core/tracker-architecture-plan.md`

## 対象ファイル

変更したファイル:

- `reports/runtime-host-task-breakdown-draft-20260514162526.md`

参照した主なファイル:

- `/home/ibis/AI/CodexSkill/skills/task-breakdown-planner/SKILL.md`
- `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `Tracker/Design/tasks-status.md`
- `Tracker/Design/phases-status.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `Tracker/Design/Core/tracker-architecture-plan.md`
- `/home/ibis/.codex/memories/MEMORY.md`

## 指摘事項

### 固定残タスク一覧 draft

以下は `RUNTIME-HOST-002` 以降を、1 task ごとに TDD / implementation / review / commit / PR update まで進められる粒度へ切り直す draft である。`RAW-VISION-*` / `TRACKER-*` は追加しない。AutoRef logic 実装と旧 diagnostics log / 旧 render snapshot sidecar の完全互換は scope に戻さない。

#### RUNTIME-HOST-002

- Title: RuntimeHost / DebugHost project dependency boundary contract を追加する
- Phase: verification
- Status 初期値: pending
- Dependencies: RUNTIME-HOST-001
- Size: small
- Exit Criteria:
  - `Tracker.RuntimeHost` が `Tracker.DebugHost` / `Tracker.Server` / Web UI / diagnostics replay UI project を参照しないことを project reference / dependency test で固定する。
  - RuntimeHost 側 code が diagnostics logging / replay / Blazor UI namespace を直接呼ばないことを contract test で固定する。
  - DebugHost が tracker operation loop を主実行責務として持たず、latest immutable snapshot または published output を読む側であることを test 名と assertion で固定する。
  - Red test として意図通り失敗する evidence、task 専用 review、commit、Draft PR update が揃う。

#### RUNTIME-HOST-003

- Title: diagnostics sample boundary と legacy degraded contract を追加する
- Phase: verification
- Status 初期値: pending
- Dependencies: RUNTIME-HOST-002
- Size: medium
- Exit Criteria:
  - diagnostics sample tick が tracker committed frame cadence / `WorldFrameCommitted` に依存しないことを failing regression test で固定する。
  - 新規 capture の Diagnostics `Vision Input` が legacy render snapshot sidecar ではなく diagnostics sample sidecar の latest raw snapshot から復元されることを test で固定する。
  - 同じ diagnostics sample tick で latest tracker snapshot または latest-before tracker snapshot を比較対象にできる contract を固定する。
  - 旧 render snapshot sidecar だけを持つ session は unsupported / degraded legacy として扱い、高コストな完全互換 fallback や tick/scrub ごとの sidecar 全再読込を主経路にしないことを固定する。
  - Red test evidence、task 専用 review、commit、Draft PR update が揃う。

#### RUNTIME-HOST-004

- Title: `Tracker.Server` を `Tracker.DebugHost` project / namespace / 起動経路へ rename する
- Phase: implementation
- Status 初期値: pending
- Dependencies: RUNTIME-HOST-002
- Size: medium
- Exit Criteria:
  - 現 `Tracker.Server` の Web UI / diagnostics / replay / capture viewer 責務が `Tracker.DebugHost` の project / namespace / 起動名として表現される。
  - 既存 debug normal path、raw vision viewer、diagnostics page、capture / replay の起動導線を壊さない。
  - README / launch settings / solution / project reference の名称が DebugHost 方針と矛盾しない。
  - AutoRef logic や RuntimeHost operation loop 実装は含めない。
  - focused tests / `Tracker.DebugHost` build、task 専用 review、commit、Draft PR update が揃う。

#### RUNTIME-HOST-005

- Title: tracker operation loop の共有 runtime boundary を抽出する
- Phase: implementation
- Status 初期値: pending
- Dependencies: RUNTIME-HOST-004
- Size: medium
- Exit Criteria:
  - SSL-Vision input、tracker update、official tracker packet publish、latest tracker snapshot 公開の境界が RuntimeHost から再利用できる形で DebugHost 固有 UI / diagnostics logging から分離される。
  - 抽出先は `Tracker.Core` または UI 非依存の共有層に限定し、Blazor / diagnostics replay API へ依存しない。
  - DebugHost の既存 normal path は adapter 経由で維持し、profile switch、publisher 設定、snapshot store 更新順を変えない。
  - RUNTIME-HOST-002 の dependency contract が green へ向かう最小実装を含める。
  - focused tests、task 専用 review、commit、Draft PR update が揃う。

#### RUNTIME-HOST-006

- Title: DebugHost live display を read-side snapshot 境界へ寄せる
- Phase: implementation
- Status 初期値: pending
- Dependencies: RUNTIME-HOST-005
- Size: medium
- Exit Criteria:
  - DebugHost live display は UI render tick ごとに raw / tracked / 3rd party tracker の latest immutable snapshot を固定して描画する。
  - Web rendering tick が tracker operation loop を駆動しないことを focused tests で確認できる。
  - DebugHost は RuntimeHost または published tracker output を読む側の設計に寄せ、tracker operation loop の主責務を持たない。
  - raw vision viewer、split / overlay、diagnostics の既存表示を壊さない。
  - focused tests / `Tracker.DebugHost` build、task 専用 review、commit、Draft PR update が揃う。

#### RUNTIME-HOST-007

- Title: DebugHost diagnostics sample sidecar fast path を実装する
- Phase: implementation
- Status 初期値: pending
- Dependencies: RUNTIME-HOST-003, RUNTIME-HOST-006
- Size: medium
- Exit Criteria:
  - diagnostics logging / replay processing は独立した diagnostics sample tick で latest raw snapshot と latest tracker snapshot を固定し、diagnostics sample sidecar に保存できる。
  - metadata から diagnostics sample sidecar の path / status を辿れる。
  - 新規 capture の diagnostics replay / comparison / Field source は diagnostics sample sidecar と alignment を優先し、旧 diagnostics log / 旧 render snapshot sidecar は legacy / best-effort / degraded 表示に留める。
  - tick / scrub / playback / Field source selector 変更で sidecar 全体を再読込しない bounded lookup の contract を満たす。
  - RUNTIME-HOST-003 の red tests が green になる。
  - focused tests、task 専用 review、commit、Draft PR update が揃う。

#### RUNTIME-HOST-008

- Title: `Tracker.RuntimeHost` headless project scaffold と configuration を追加する
- Phase: implementation
- Status 初期値: pending
- Dependencies: RUNTIME-HOST-005
- Size: small
- Exit Criteria:
  - `Tracker.RuntimeHost` project、Program / options / DI bootstrap、solution entry が追加される。
  - RuntimeHost は Web UI / diagnostics replay / capture viewer を持たず、headless host として起動できる。
  - mode 境界は `tracker only` と将来 `tracker + AutoRef` を表現できるが、AutoRef logic は実装しない。
  - project reference contract により DebugHost / Web UI 依存が入らない。
  - `Tracker.RuntimeHost` build、focused tests、task 専用 review、commit、Draft PR update が揃う。

#### RUNTIME-HOST-009

- Title: RuntimeHost tracker operation loop と official packet publish normal path を実装する
- Phase: implementation
- Status 初期値: pending
- Dependencies: RUNTIME-HOST-007, RUNTIME-HOST-008
- Size: medium
- Exit Criteria:
  - RuntimeHost が SSL-Vision input を受け、tracker engine / coordinator boundary を通して tracker state を更新し、official tracker packet を publish できる。
  - RuntimeHost は latest tracker snapshot を DebugHost が購読または参照できる形で公開するが、DebugHost UI / diagnostics logging を直接呼ばない。
  - performance 優先のため、旧 diagnostics log 完全互換や旧 render snapshot sidecar fallback は RuntimeHost loop に入れない。
  - fake receiver / fake publisher 等で正常系を検証できる focused tests がある。
  - `Tracker.RuntimeHost` build、task 専用 review、commit、Draft PR update が揃う。

#### RUNTIME-HOST-010

- Title: RuntimeHost / DebugHost split の focused validation と manual evidence を揃える
- Phase: review
- Status 初期値: pending
- Dependencies: RUNTIME-HOST-009
- Size: small
- Exit Criteria:
  - RuntimeHost / DebugHost の focused tests と build を通す。
  - diagnostics sample evidence と、legacy / degraded 表示が主経路へ昇格していない証跡を report に残す。
  - DebugHost の既存 UI normal path と RuntimeHost の headless normal path の両方について、最低限の手動または CLI evidence を残す。
  - evidence report、task 専用 review、commit、Draft PR update が揃う。

#### RUNTIME-HOST-011

- Title: RuntimeHost / DebugHost split の final review / tracking sync / PR ready を完了する
- Phase: review
- Status 初期値: pending
- Dependencies: RUNTIME-HOST-010
- Size: small
- Exit Criteria:
  - gpt-5.5 high review を実施し、blocking finding があれば修正、再検証、r2 review まで完了する。
  - `Tracker/Design/tasks-status.md` と `Tracker/Design/phases-status.md` を実態に同期する。
  - report references、validation evidence、commit 履歴、Draft PR description を最新化する。
  - PR ready にできる状態か、残るなら残理由と次の固定 task が明示される。

### 既存 RUNTIME-HOST-002..005 の分割・置換案

- 既存 `RUNTIME-HOST-002` は、新 `RUNTIME-HOST-002` と新 `RUNTIME-HOST-003` に分割する。project dependency / host responsibility contract と diagnostics sample / legacy degraded contract を別 task に分け、Red test の焦点を分離する。
- 既存 `RUNTIME-HOST-003` は、新 `RUNTIME-HOST-004` と新 `RUNTIME-HOST-006` に分割する。rename / startup path と、DebugHost を read-side snapshot 境界へ寄せる実装を分ける。
- 既存 `RUNTIME-HOST-004` は、新 `RUNTIME-HOST-005`、新 `RUNTIME-HOST-008`、新 `RUNTIME-HOST-009` に分割する。共有 operation loop boundary、RuntimeHost project scaffold、RuntimeHost normal path 実装を分ける。
- 既存 `RUNTIME-HOST-005` は、新 `RUNTIME-HOST-010` と新 `RUNTIME-HOST-011` に分割する。validation / manual evidence と、final review / tracking sync / PR ready を別 gate にする。
- 新 `RUNTIME-HOST-007` は、既存 002 の diagnostics sample boundary と既存 005 の validation 対象に埋もれていた実装 task を明示化するために追加する。

### phase 更新案

- `verification`: `RUNTIME-HOST-002` と `RUNTIME-HOST-003` を含める。完了条件は、RuntimeHost / DebugHost dependency boundary と diagnostics sample boundary の Red tests が揃い、意図した failing state、review、commit、Draft PR update が完了していること。
- `implementation`: `RUNTIME-HOST-004` から `RUNTIME-HOST-009` を含める。完了条件は、DebugHost rename、共有 operation loop boundary、DebugHost read-side 化、diagnostics sample sidecar fast path、RuntimeHost scaffold、RuntimeHost normal path が focused tests / build / task review 付きで green になっていること。
- `review`: `RUNTIME-HOST-010` と `RUNTIME-HOST-011` を含める。完了条件は、focused validation、RuntimeHost / DebugHost build、diagnostics evidence、legacy degraded evidence、gpt-5.5 high review、必要な r2 review、tracking sync、commit / PR ready が揃っていること。

## 結果

`RUNTIME-HOST-002` 以降の固定残タスク draft を、`RUNTIME-HOST-002` から `RUNTIME-HOST-011` までの 10 task に切り直した。

この draft は tracking file 本体へは未反映であり、親 agent が採否を確認した後に `Tracker/Design/tasks-status.md` / `Tracker/Design/phases-status.md` へ反映する前提である。作業中の workspace では、この report file 以外の編集は行っていない。

## リスク

- 実 code の詳細調査は最小限に留めたため、実装時に既存 `Tracker.Server` 内の責務分離が想定より大きい場合、`RUNTIME-HOST-005` または `RUNTIME-HOST-006` はさらに小さくする必要がある。
- `Tracker.Server` から `Tracker.DebugHost` への物理 rename は build / launch / docs / namespace に広く影響するため、`RUNTIME-HOST-004` は実行前に差分範囲を再確認する必要がある。
- diagnostics sample sidecar の具体 schema 名は設計上「実装 task で固定」とされているため、`RUNTIME-HOST-007` 開始時に schema 名と metadata field を task 内で確定する必要がある。
- 旧 diagnostics log / 旧 render snapshot sidecar の degraded 表示範囲を広げすぎると性能最優先方針と衝突する。legacy fallback は表示救済に留め、最新 capture / 最新 logging の bounded lookup を優先する判断を維持する必要がある。
- この report は draft であり、tracking file 本体の ID 置換、phase 更新、PR description 更新は親 agent の採用後に別 task として実施する必要がある。
