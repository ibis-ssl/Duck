# Sub-agent実行レポート

## タスク

- 目的: issue #14 / TRACKER-064 の要件定義書をレビューする。
- タスク種別: レビュー

## sub-agentを使う理由

- 理由: ユーザー指定によりレビューは gpt-5.5 high の専用 sub-agent が行い、結果を日本語 report に残すため。

## 対象範囲

- 対象: `Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md`、TRACKER-064 tracking、調査 report、設計作成 report。

## 対象外

- 対象外: 実装、Docker 起動、dotnet test、PR 作成、既存 `Tracker/Tracker.Server/appsettings.json` の変更。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `rg -n "TRACKER-064|issue #14|e2e simulator|ER-FORCE|requirements" /home/ibis/.codex/memories/MEMORY.md`
  - `sed -n '468,484p' /home/ibis/.codex/memories/MEMORY.md`
  - `nl -ba Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Core/Design/tasks-status.md | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Core/Design/phases-status.md | sed -n '1,260p'`
  - `nl -ba reports/tracker-064-er-force-docker-assets-investigation-20260514133853.md | sed -n '1,260p'`
  - `nl -ba reports/tracker-064-er-force-simulator-protocol-investigation-20260514133853.md | sed -n '1,260p'`
  - `nl -ba reports/tracker-064-duck-e2e-comparison-investigation-20260514133853.md | sed -n '1,260p'`
  - `nl -ba reports/tracker-064-e2e-simulator-requirements-design-20260514133853.md | sed -n '1,260p'`
  - `nl -ba reports/tracker-064-e2e-simulator-requirements-review-20260514133853.md | sed -n '1,260p'`
  - `git status --short -- reports/tracker-064-e2e-simulator-requirements-review-20260514133853.md Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md Tracker/Tracker.Server/appsettings.json`
  - Docker build / 起動、dotnet test、codex exec、nested Codex、追加 sub-agent はユーザー制約により未実行。

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/tracker-064-e2e-simulator-requirements-review-20260514133853.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - 確認: `Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md`
  - 確認: `Tracker/Tracker.Core/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.Core/Design/phases-status.md`
  - 確認: `reports/tracker-064-er-force-docker-assets-investigation-20260514133853.md`
  - 確認: `reports/tracker-064-er-force-simulator-protocol-investigation-20260514133853.md`
  - 確認: `reports/tracker-064-duck-e2e-comparison-investigation-20260514133853.md`
  - 確認: `reports/tracker-064-e2e-simulator-requirements-design-20260514133853.md`
  - 未変更: `Tracker/Tracker.Server/appsettings.json`

## 指摘事項

- 指摘要約または「指摘なし」:
  - Blocking: `Tracker/Tracker.Core/Design/tasks-status.md:17`、`Tracker/Tracker.Core/Design/tasks-status.md:26`、`Tracker/Tracker.Core/Design/tasks-status.md:79` は fallback tracker を「参照真値 fallback」と呼んでいる。一方、要件定義書は `Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md:15`、`Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md:57` で ER-Force tracker / Tigers tracker を観測ノイズ込み `SSL_DetectionFrame` 由来の推定値として扱い、「真値比較」ではなく「参照 tracker 差分」と明記している。調査 report も `reports/tracker-064-er-force-simulator-protocol-investigation-20260514133853.md:103`-`106` と `reports/tracker-064-duck-e2e-comparison-investigation-20260514133853.md:138`-`140` で同じ制約を示しているため、tracking の用語がこのままだと TRACKER-065/066 が fallback tracker を truth として実装するリスクがある。通常経路を壊す可能性があるため、tracking 側を「参照 tracker fallback」または「reference tracker delta」に同期する必要がある。
  - Blocking: `Tracker/Tracker.Core/Design/phases-status.md:18` は「Tigers または ER-Force tracker」と記載しており、ユーザー回答と要件定義書の優先順である ER-Force tracker -> Tigers tracker と逆または未順序に読める。要件定義書は `Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md:44`-`46` と `Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md:141`-`143` で優先順と後続タスクを固定しており、`tasks-status.md:17`、`tasks-status.md:26`、`tasks-status.md:79` も ER-Force -> Tigers の順である。phase tracking の完了条件が不一致なため、TRACKER-065 の調査・採用判断前に同期が必要。
  - Capability gap / ユーザー確認: 要件定義書は `Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md:150`-`159` で simulator 改造許容、truth file / UDP の優先、schema、fallback service / endpoint、license 境界を未確定事項として分離している。これは調査 report の `world::SimulatorState` / `sendRealData` 外部出力未確認、Duck 側 truth sidecar / metric engine 不足と整合しており、TRACKER-065 以降で capability gap として確認する扱いでよい。
  - Held concern: `Tracker/Tracker.Server/Design/tracker-e2e-simulator-requirements.md:5` は issue #14 の表現を「dockerでシミュレーターのケースを追加」としているが、今回のレビュー依頼では issue #14 を「ER-FORCEのシミュレータを使って Tigersのトラッカー、ER-FORCEのトラッカーと比較したい」としている。本文の目的、fallback 優先順位、grSim 不採用、初期シナリオはユーザー回答に沿っているため normal path blocker ではないが、traceability を上げるなら issue 文言を併記する余地がある。

## 結果

- 結果:
  - レビューは実施済み。blocking findings は 2 件、capability gap / ユーザー確認事項は 1 件、held concern は 1 件。
  - 要件定義書本体は、issue #14 の主旨、ER-Force simulator の観測ノイズ込み出力、観測ノイズなし真値優先、truth 取得不可時の ER-Force tracker -> Tigers tracker fallback、grSim 不採用、初期は手元確認、初期シナリオ、Spec driven / TDD、調査 report の重要事実、脚注説明に概ね合っている。
  - `world::SimulatorState` / `sendRealData` が内部にはあるが外部出力は未確認、truth output は simulator 改造候補、Duck 側は CaptureOn / diagnostics / sidecar / alignment / CaptureReplay が土台、metric engine 等は不足、という調査 report の重要事実と要件定義書の間に blocking な矛盾は見つからなかった。
  - blocking は要件定義書本体ではなく tracking 側の用語・優先順同期不足。後続 TRACKER-065/066 の実装判断に影響し得るため、TRACKER-064 完了前に tracking 同期で解消する必要がある。

## リスク

- 未解決のリスクまたは後続対応:
  - tracking 修正後の再レビューが未実施。
  - Docker build / 起動、dotnet test は制約により未実行。今回のレビューは文書・調査 report の静的照合のみ。
  - ER-Force simulator の truth output、fallback tracker の具体 service / endpoint、GPLv3 資産の取り込み境界は TRACKER-065 以降の確認事項として残る。
