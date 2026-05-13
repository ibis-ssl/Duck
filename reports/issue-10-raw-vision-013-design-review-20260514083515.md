# Sub-agent実行レポート

## タスク

- 目的: RAW-VISION-013 の設計・tracking・report 差分を専用レビューし、Issue #10 の Vision split / overlay と diagnostics latest-before time-sync 方針が実装前設計として十分か確認する。
- タスク種別: レビュー

## sub-agentを使う理由

- 理由: `review-enforcer` とユーザー指示により、レビューは gpt-5.5 high の sub-agent に委譲し、親はマネージャーとして report を裁定する。

## 対象範囲

- 対象: RAW-VISION-013 の設計差分、tracking 差分、調査・設計 report、Issue #10 のユーザー回答との整合。

## 対象外

- 対象外: production code、test code、README、PR本文、RAW-VISION-014 以降の実装詳細。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `rg -n "RAW-VISION|raw-vision|Issue #10|diagnostics|same-tick|geometry|latest-before" /home/ibis/.codex/memories/MEMORY.md`
  - `sed -n '1,220p' /home/ibis/ssl/IbisDuck/reports/issue-10-raw-vision-013-design-review-20260514083515.md`
  - `git status --short`
  - `git diff --name-status 15e2afc..HEAD`
  - `git diff --stat 15e2afc..HEAD`
  - `git diff --check 15e2afc..HEAD`
  - `nl -ba Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Design/tasks-status.md | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Design/phases-status.md | sed -n '1,220p'`
  - `git remote -v`
  - `nl -ba reports/issue-10-vision-overlay-investigation-20260514080106.md | sed -n '1,260p'`
  - `nl -ba reports/issue-10-live-same-tick-investigation-20260514081135.md | sed -n '1,260p'`
  - `nl -ba reports/issue-10-diagnostics-time-sync-audit-20260514081730.md | sed -n '1,260p'`
  - `nl -ba reports/issue-10-vision-overlay-design-20260514082233.md | sed -n '1,220p'`
  - `nl -ba reports/issue-10-vision-overlay-design-r2-20260514082755.md | sed -n '1,240p'`
  - `gh issue view 10 --repo ibis-ssl/Duck --json title,body,state,url,comments`
  - `nl -ba Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md | sed -n '260,380p'`
  - `git diff --unified=80 15e2afc..HEAD -- Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `git diff --unified=60 15e2afc..HEAD -- Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md`
  - `rg -n "Raw Aggregate|Raw Camera|Tracked|3rd party tracker|UI render tick|immutable snapshot|MultiTrackerManager|TrackerPacketAdapter|ReplayTimelineIndex|saved-session-alignment|latest-before|CandidateMissing|NoCandidateSnapshot|VisionFieldCanvas|source key|SourceKey|SampledAt|delta|future|later" Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md reports/issue-10-*.md`
  - `sed -n '1,260p' reports/issue-10-raw-vision-013-design-review-20260514083515.md`
  - `git diff --check --no-index /dev/null reports/issue-10-raw-vision-013-design-review-20260514083515.md`
  - `git diff -- reports/issue-10-raw-vision-013-design-review-20260514083515.md`
  - `git status --short`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/issue-10-raw-vision-013-design-review-20260514083515.md`
  - 確認: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - 確認: `Tracker/Tracker.Server/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.Server/Design/phases-status.md`
  - 確認: `reports/issue-10-vision-overlay-investigation-20260514080106.md`
  - 確認: `reports/issue-10-live-same-tick-investigation-20260514081135.md`
  - 確認: `reports/issue-10-diagnostics-time-sync-audit-20260514081730.md`
  - 確認: `reports/issue-10-vision-overlay-design-20260514082233.md`
  - 確認: `reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - 確認: GitHub Issue #10 (`gh issue view 10 --repo ibis-ssl/Duck`)
  - 非対象維持: `Tracker/Tracker.Server/appsettings.json` の既存差分は触っていない。

## 指摘事項

- 指摘要約または「指摘なし」:
  - Blocking normal-path problems: なし。
  - User-confirmation-required capability gaps: なし。
  - Non-blocking held concern: `raw-vision-viewer-plan.md` は主要な固有名詞を `用語` に追加しているが、`Layer A/B`、`source key`、`SampledAt`、`stale`、`ExternalTrackerSnapshotStore` などの補助語は本文中の説明に留まり、用語節には個別登録されていない。現時点では RAW-VISION-014/015 の normal path を壊す不足ではないが、TDD/実装時に読者が迷うなら同じ設計節へ追記する余地がある。参照: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md:206`, `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md:213`, `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md:217`, `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md:300`
  - Non-blocking held concern: `phases-status.md` の implementation phase は high-level に `Raw / Tracked / 3rd party tracker sources` とまとめており、`Raw Aggregate` と `Raw Camera` の分離までは明記していない。詳細な task exit criteria と設計本文では固定済みなので blocking ではない。参照: `Tracker/Tracker.Server/Design/phases-status.md:16`, `Tracker/Tracker.Server/Design/tasks-status.md:14`, `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md:189`

## 結果

- 結果:
  - Review outcome: pass / blocking findings なし。
  - Issue #10 は open、本文は `重ね合わせ描画してほしい。`、コメントは 0 件だった。設計は追加ユーザー回答で固定された source 候補 `Raw Aggregate` / `Raw Camera` / `Tracked` / `3rd party tracker` と矛盾していない。
  - live Vision 方針は、厳密な同一 packet timestamp や全 source 共通 callback を要求せず、1 回の `UI render tick` で各 source の latest immutable snapshot を固定する設計になっている。これは調査 report の採用方針と一致する。
  - geometry 方針は raw geometry 優先、tracked fallback、3rd party tracker packet から geometry 復元しない方針として明記されている。
  - split / overlay UI は diagnostics 寄せの Layer A/B、split / overlay mode、legend、visibility、same-source 1 layer 化、missing layer でも ready layer を残す挙動として設計されている。
  - diagnostics replay / comparison は selected replay timeline tick を基準にし、対象 source が selected tick に無い場合は selected tick 以前の同一 source `latest-before snapshot` を hold 表示/比較し、selected time を source ごとにスライドせず、future/later snapshot を使わない方針になっている。
  - RAW-VISION-014 の TDD exit criteria と RAW-VISION-015 の implementation exit criteria は、設計本文の latest-before / missing-only / future-later 不採用 / immutable snapshot store 境界と整合している。
  - Disposition: RAW-VISION-013 は blocking 修正なしで親裁定へ戻せる。held concern は RAW-VISION-014/015 で実装者が迷った場合に設計追記する扱いでよい。

## リスク

- 未解決のリスクまたは後続対応:
  - held: 用語節に未登録の補助語が残る。現時点では本文と report で意味を追えるため blocking ではない。
  - held: phase-level summary は Raw source の内訳を省略している。task-level tracking と設計本文が canonical なので blocking ではない。
  - residual: RAW-VISION-014 では、`latest-before snapshot` 採用時に selected replay timeline tick / selected time が動かないこと、delta が selected tick と held source snapshot `receivedAt` の差になること、future/later snapshot が候補に入らないことを failing test で固定する必要がある。
  - residual: RAW-VISION-015 では、`MultiTrackerManager<TrackerPacketAdapter>` の mutable state を UI が直接保持しない store / composer 境界を実装で崩さない必要がある。
