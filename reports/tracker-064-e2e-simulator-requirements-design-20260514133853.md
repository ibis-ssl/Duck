# Sub-agent実行レポート

## タスク

- 目的: issue #14 の要件定義書 `Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md` を提案形式で作成する。
- タスク種別: 設計文書作成

## sub-agentを使う理由

- 理由: ユーザー指定によりマネージャーは委譲と裁定に徹し、設計文書作成は gpt-5.5 high の sub-agent に委譲するため。

## 対象範囲

- 対象: issue #14、TRACKER-064 tracking、3本の調査 report、`raw-vision-viewer-plan.md` の脚注形式を踏まえた要件定義書作成。

## 対象外

- 対象外: 実装、TDD test authoring、Docker 起動、PR 作成、既存 `Tracker/Tracker.Server/appsettings.json` の変更。

## 実行コマンド

- 実行コマンド:
  - `rg -n "TRACKER-064|issue #14|dockerでシミュレーター|e2e simulator|simulator" /home/ibis/.codex/memories/MEMORY.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `sed -n '460,490p' /home/ibis/.codex/memories/MEMORY.md`
  - `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
  - `sed -n '1,260p' Tracker/Tracker.Core/Design/phases-status.md`
  - `sed -n '1,260p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `tail -n 120 Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `sed -n '1,240p' reports/tracker-064-er-force-docker-assets-investigation-20260514133853.md`
  - `sed -n '1,260p' reports/tracker-064-er-force-simulator-protocol-investigation-20260514133853.md`
  - `sed -n '1,260p' reports/tracker-064-duck-e2e-comparison-investigation-20260514133853.md`
  - `sed -n '1,240p' reports/tracker-064-e2e-simulator-requirements-design-20260514133853.md`
  - `test -f Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md && sed -n '1,240p' Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md || true`
  - `git status --short`
  - `git diff -- Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md reports/tracker-064-e2e-simulator-requirements-design-20260514133853.md`
  - `git diff --check -- Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md reports/tracker-064-e2e-simulator-requirements-design-20260514133853.md`
  - `rg -n "[ \t]$" Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md reports/tracker-064-e2e-simulator-requirements-design-20260514133853.md`
  - `git status --short -- Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md reports/tracker-064-e2e-simulator-requirements-design-20260514133853.md Tracker/Tracker.Server/appsettings.json`
  - Docker build / 起動、dotnet test、長時間処理はユーザー制約により未実行。

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md`
  - 変更: `reports/tracker-064-e2e-simulator-requirements-design-20260514133853.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - 確認: `Tracker/Tracker.Core/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.Core/Design/phases-status.md`
  - 確認: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - 確認: `reports/tracker-064-er-force-docker-assets-investigation-20260514133853.md`
  - 確認: `reports/tracker-064-er-force-simulator-protocol-investigation-20260514133853.md`
  - 確認: `reports/tracker-064-duck-e2e-comparison-investigation-20260514133853.md`
  - 未変更: `Tracker/Tracker.Server/appsettings.json`

## 指摘事項

- 指摘要約または「指摘なし」:
  - Blocking: 既存 `simulator-cli` から観測ノイズなし真値を外部 file / UDP へ出す標準経路は調査 report 上では未確認。要件定義書では `world::SimulatorState` / `sendRealData` を file output または UDP output へ接続する simulator 改造案を要件候補として明記した。
  - Blocking: ER-Force tracker / Tigers tracker は fallback として有用だが、観測ノイズ込み SSL-Vision 由来の tracker 推定値であり真値ではない。要件定義書では「真値比較」ではなく「参照 tracker 差分」として扱うよう明記した。
  - Blocking: Duck 側には CaptureOn / diagnostics / tracker receiver / snapshot sidecar / alignment / CaptureReplay の基盤がある一方、scenario definition、scenario command sender、session manifest、truth sidecar、metric engine は未整備。要件定義書では不足要求として分離した。
  - Non-blocking: Docker / multicast / external process は環境依存のため、初期は手元確認と opt-in automation を優先し、常時 CI 必須にはしない方針にした。
  - Non-blocking: grSim 関連資産は存在するが、今回の調査・設計・実装対象外として明示した。

## 結果

- 結果:
  - `Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md` を新規作成した。
  - 要件定義書は日本語、提案形式、段階 / 未確定 / 確認事項を明示する構成にした。
  - 推奨構成どおり、目的、背景、非目的、提案する全体像、真値と参照値の優先順位、初期シナリオ、E2E 観測・保存・比較の要求、差分指標、Spec driven / TDD、Docker / 手元確認方針、段階的ロードマップ、未確定事項 / ユーザー確認事項、脚注を記載した。
  - ER-Force simulator protocol でボール / ロボット移動を制御し、その観測から ibis tracker と他 tracker の挙動差を測る目的を明記した。
  - ER-Force simulator の通常 SSL-Vision は観測ノイズ込み detection data であり、観測ノイズなし真値ではないことを明記した。
  - 評価基準の優先順位を、ER-Force simulator 内部 / 改造で取れる観測ノイズなし真値、ER-Force tracker、Tigers tracker の順に整理した。
  - 観測ノイズなし真値が取れない場合の fallback は真値ではなく参照 tracker 差分として扱うよう記載した。
  - `world::SimulatorState` / `sendRealData` を file output または UDP output へ接続する simulator 改造案を要件候補に入れた。
  - 初期シナリオとして、ボール静止、ロボット静止、ボール直線運動、ロボット直線運動、ロボット ID 近接を記載した。
  - Duck 側の前提資産として CaptureOn、diagnostics、tracker receiver、snapshot sidecar、alignment sidecar、CaptureReplay を整理した。
  - metrics 候補として position error、velocity error、orientation error、ID switch、missing frame、latency / timestamp delta、count mismatch を記載した。
  - TRACKER-066 以降で最初に作るべき TDD 候補を記載した。
  - 専門用語・略語は `raw-vision-viewer-plan.md` と同様に脚注形式で説明した。
  - `git diff --check` と trailing whitespace 直接確認で問題なしを確認した。
  - `Tracker/Tracker.Server/appsettings.json` はユーザー所有差分があるため触っていない。

## リスク

- 未解決のリスクまたは後続対応:
  - ER-Force simulator の truth output は未確定であり、TRACKER-065 以降で既存 binary / image / source 差分を確認する必要がある。
  - simulator 改造を許容するか、file output と UDP output のどちらを先にするかはユーザー確認が必要。
  - ER-Force tracker fallback と Tigers tracker fallback の具体的な service / binary / endpoint は TRACKER-065 以降で固定する必要がある。
  - velocity error は現状の semantic summary だけでは不足する可能性があり、raw payload decode と summary 拡張のどちらを採用するか未確定。
  - Docker image、multicast、localhost、port `10020` / `11010` / `11003` は環境差があるため、初期 normal path は手元確認を優先し、通常 unit test / CI からは分離する必要がある。
  - ER-Force framework 資産の schema / code / Dockerfile を Duck repo へコピーまたは改変する場合は license 境界の確認が必要。
  - 本 sub-agent 実行中の `git status --short` では、既存の `tasks-status.md`、`phases-status.md`、`Tracker.Server/appsettings.json`、他 TRACKER-064 report が見えているが、本タスクでは指定された設計ファイルと本 report 以外は編集していない。
