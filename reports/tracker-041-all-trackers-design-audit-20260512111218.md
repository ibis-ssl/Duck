# Sub-agent実行レポート

## タスク

`TRACKER-041` 全 tracker 保存方針に対する設計文書の現状確認。

## sub-agentを使う理由

親エージェントは実装・調査を行わず、サブエージェントの報告を読んで判断するため。

## 対象範囲

- CaptureOn 比較ログ設計で self 除外がどう扱われているか確認する
- 全 tracker packet 保存・自身ログとの重複保持方針とのズレを確認する
- 修正が必要な設計文書と tracking を特定する

## 対象外

- ファイル修正
- 実装変更
- テスト変更
- PR の ready 化

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `sed -n '1,260p' reports/tracker-041-all-trackers-design-audit-20260512111218.md`
- `rg -n "self|自身|自分|除外|tracker|Tracker|3rd|third|party|packet|Packet|snapshot|Snapshot|CaptureReplay|diagnostics|playback|再生|保存|重複" Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `rg --files reports | rg 'reports/tracker-04[01]-'`
- `git status --short`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '1,130p'`
- `nl -ba Tracker/Tracker.Core/Design/tasks-status.md | sed -n '1,45p'`
- `nl -ba Tracker/Tracker.Core/Design/phases-status.md | sed -n '1,25p'`
- `rg -n "CaptureReplay|comparison|sidecar|3rdparty|self|除外|snapshot|render snapshot|diagnostics / replay|replay|再生|TrackerConnectionLib" Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '108,146p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '460,558p'`
- `rg -n "self|除外|3rdparty|third|tracker packet|sidecar|session folder|snapshot|CaptureReplay|diagnostics|replay|再生|保存|重複|all tracker|全 tracker" reports/tracker-040-*.md reports/tracker-041-*.md`
- `sed -n '1,220p' reports/tracker-041-all-trackers-implementation-audit-20260512111218.md`

## 対象ファイル

- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `reports/tracker-040-design-review-20260512094448.md`
- `reports/tracker-040-design-review-r2-20260512102542.md`
- `reports/tracker-040-design-separation-fix-20260512100723.md`
- `reports/tracker-040-session-folder-design-fix-20260512101934.md`
- `reports/tracker-040-r2-progress-sync-20260512102917.md`
- `reports/tracker-041-tdd-tests-20260512105825.md`
- `reports/tracker-041-implementation-20260512110523.md`
- `reports/tracker-041-all-trackers-implementation-audit-20260512111218.md`

## 指摘事項

1. 現在の設計は「全 tracker 保存」ではなく「3rdparty tracker 比較ログ + self 除外」として書かれている。
   - `tracker-server-cli-ui-detail-design.md` は目的で「ibis tracker と同時に存在する 3rdparty tracker の official `TrackerWrapperPacket` を保存」としており、対象範囲も 3rdparty tracker packet 傍受と比較 sidecar 参照に限定している。
   - 同文書の `source 識別と self除外` は、`Tracker:Uuid` と `Tracker:SourceName` の両方が ibis runtime identity と一致する packet を比較対象にしない、と明記している。
   - `tracker-architecture-plan.md` も 3rdparty tracker packet を別系統で保存する設計で、`self除外` により ibis 自身の packet は sidecar 保存対象から外れる前提になっている。

2. 「存在する tracker packet はすべて保存し、自身の詳細ログとは重複保持を許容する」方針と矛盾する箇所がある。
   - `tracker-server-cli-ui-detail-design.md` の `source 識別と self除外` は、self を保存対象から外す設計に読めるため、全 tracker 保存方針と矛盾する。
   - `tracker-architecture-plan.md` の data flow では、傍受した `TrackerWrapperPacket` を ibis 自身の `uuid` / `sourceName` と照合して self 除外し、他 tracker の packet だけを sidecar JSONL へ保存するとしているため、同じく矛盾する。
   - `tasks-status.md` の `TRACKER-041` は「ibis 自身の packet は除外」「self除外 constructor」「self除外を実装」としており、tracking 上も全 tracker 保存方針に合っていない。
   - `phases-status.md` も `TRACKER-041` の現在状態として self除外を通過済み前提にしている。

3. 3rd party tracker packet を「保存する」だけでなく「snapshot として保持し、CaptureReplay / diagnostics / playback で再生できる」設計としては不足している。
   - 現行 sidecar record は `payload base64 または ball/robot count などの再比較に必要な summary` とされ、payload を必須にしていない。summary だけを許すと 3rdparty tracker snapshot を後から復元・再生できない。
   - diagnostics / replay 互換追加は、metadata から comparison sidecar を解決して「追加情報を読む」「比較出力を確認する」設計であり、3rdparty tracker frame を時系列 snapshot として選択・描画・playback する UI/CLI 契約までは定義していない。
   - `Tracker.CaptureReplay` の既存説明は保存済み raw packet capture を `TrackerEngine` へ再投入する CLI であり、3rdparty official tracker sidecar を replay source として扱う契約、または diagnostics playback の timeline に重ねる契約がない。
   - diagnostics viewer の render snapshot は raw source detection と tracked frame の描画 snapshot であり、3rdparty tracker packet snapshot の再生・描画対象としては定義されていない。

4. 修正候補は設計文書と tracking の両方にまたがる。
   - `tracker-server-cli-ui-detail-design.md`: `source 識別と self除外` を `source 識別と role分類` へ寄せ、self packet も保存するが比較時には `role=self` として扱い、ibis 詳細ログとの重複保持を許容する方針へ変更する。sidecar record は replay 可能な payload または復元可能な snapshot 情報を必須にし、summary-only は比較集計用の追加情報に落とす。
   - `tracker-architecture-plan.md`: 3rdparty 限定と self 除外の記述を、all tracker official packet capture / role分類 / Core 非流入 / Server 統合層へ更新する。`Tracker.CaptureReplay` と diagnostics viewer が all tracker snapshot sidecar を metadata から読み、source identity / role ごとの時系列再生または比較表示をできる入力契約を追記する。
   - `tasks-status.md`: `TRACKER-041` のタイトル、Exit Criteria、タスク一覧を self除外契約から all tracker 保存・role分類契約へ更新する必要がある。既に self除外前提で TDD/実装済みと書かれているため、レビュー前に tracking を実態と新方針へ戻すか、追加修正タスクを切る判断が必要。
   - `phases-status.md`: comparison-logging の phase 完了条件を「self除外付き」から「存在する tracker packet をすべて保存し、self/3rdparty role を保持し、3rdparty snapshot を CaptureReplay / diagnostics / playback で再生可能」に更新する必要がある。
   - `TRACKER-042` 以降: metadata に all tracker snapshot sidecar の relative path、source identity 一覧、role、replay/playback 用 index または timestamp 対応規則を持たせる設計へ補強する。`TRACKER-043` は summary 保存ではなく replay 可能な official tracker snapshot 保存を条件化し、`TRACKER-044` は比較だけでなく CaptureReplay / diagnostics / playback の再生契約を含める必要がある。

## 結果

未完了ではなく、設計監査としては完了。

現設計の結論は、`TRACKER-040` 時点の設計・tracking は「3rdparty tracker comparison sidecar」を中心にしており、ibis 自身の official tracker packet は self 除外される。これは、ユーザー追加方針の「存在する tracker packet はすべて保存」「自身の詳細ログと重複保持を許容」「3rd party tracker も snapshot を保持し再生できるようにする」と一致しない。

設計修正は必要。最低限、`tracker-server-cli-ui-detail-design.md`、`tracker-architecture-plan.md`、`tasks-status.md`、`phases-status.md` を更新対象にするべき。特に `TRACKER-041` は現在 self除外の contract / 実装完了方向に進んでいるため、親エージェントは review gate に進める前に、設計方針を all tracker 保存・role分類・snapshot replay へ切り替えるか判断する必要がある。

## リスク

- self 除外前提のまま `TRACKER-041` review を通すと、後続の `TRACKER-043` / `TRACKER-044` が全 tracker 保存方針と逆向きの contract に依存するリスクがある。
- sidecar record が summary-only を許す設計のままだと、3rdparty tracker packet を snapshot として再生できない実装が正常扱いになるリスクがある。
- `CaptureReplay` と diagnostics / playback の契約が「比較情報を読む」だけだと、ユーザー要件の「3rd party tracker もスナップショットを保持し、再生」を満たしたか判定できない。
- self packet を保存する場合、ibis 詳細ログと official tracker packet の二重保存が仕様になるため、metadata 上の source role、重複保持の説明、UI 表示名、容量増加時の運用を設計で明示しないと後続 review で解釈が割れる。
