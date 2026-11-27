# Phases Status

Rule: This file may be updated only through `task-breakdown-planner`, `task-consistency-manager`, or `progress-sync-manager`.

## Overall State

- Active Phase: design
- Active Task: RAW-VISION-017
- Remaining Phases: design, verification, implementation, review

## Phases

| Phase | Status | Exit Criteria |
| --- | --- | --- |
| preparation | complete | Design plan and tracking files exist before implementation. |
| implementation | pending | PR #15 までの implementation は complete。`RAW-VISION-019` で diagnostics logging loop isolation を実装し、tracker loop から render snapshot を直接保存する経路を新規 capture では置き換え、別 loop が latest raw / latest own tracker / latest external tracker snapshot を読み取って diagnostics 保存・alignment/replay に接続する。 |
| verification | pending | PR #15 までの verification は complete。`RAW-VISION-018` で diagnostics sampling loop / latest snapshot boundary の TDD contract を追加し、`RAW-VISION-020` で対象 capture または同等ログにより raw/latest snapshot cadence と replay `Vision Input` cadence の改善を説明できる evidence を残す。 |
| review | pending | PR #15 は `2026-05-14T03:29:25Z` に merge 済み。`RAW-VISION-020` で diagnostics loop isolation の dedicated gpt-5.5 high review、progress sync、commit / PR ready を完了する。 |
| design | in-progress | PR #15 までの design は complete。`RAW-VISION-017` で `raw-vision-viewer-plan.md` に tracker 処理ループ、server live 表示ループ、diagnostics logging / replay ループの分離、diagnostics sample tick、旧 render snapshot sidecar 互換を非要件とする性能優先方針、固有名詞脚注を追記し、gpt-5.5 high design review で blocking findings がないことを確認する。 |
