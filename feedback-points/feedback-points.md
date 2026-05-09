# Feedback Points

Canonical active feedback-point ledger.

Update rule:

- このファイルは `feedback-points-manager` または `feedback-points-sanitizer` を通してのみ更新する
- それ以外の経路で直接追記・修正しない

| FP | 記録起点 | 内容 | カテゴリ | 重複グループ | 指摘回数 | skill化状態 | 関連skill | 状態 | 記録日 | 直近指摘日 | 最終更新日 | 次アクション対応 | 根拠リンク |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FP-001 | ユーザー指示 | sub-agent に pre-created report を渡すときは、report の直接編集許可を prompt に明示し、見出し順と既存テキストを保ったまま空欄だけを埋めさせる | delegation | allow_subagent_report_edits | 1 | 未整理 | `sub-agent-task-manager`, `review-enforcer`, `codex-delegation-executor` | 対応中 | 2026-05-09 | 2026-05-09 | 2026-05-09 | 現在の sub-agent prompt へ反映済み。関連 skill の wording 反映要否を後続で判断する | user instruction 2026-05-09: 「次回からレビューレポートの編集許可をサブエージェントに渡すようにしてください」 |
| FP-002 | ユーザー指示 | sub-agent に nested `codex exec` / `codex exec review` を実行させず、このセッションの通常 tool と workspace 読み取りだけで review / verification を完結させる | delegation | forbid_nested_codex_exec_in_subagents | 1 | 未整理 | `sub-agent-task-manager`, `codex-delegation-executor` | 対応中 | 2026-05-09 | 2026-05-09 | 2026-05-09 | 現在の sub-agent prompt へ禁止事項として反映済み。関連 skill の wording 反映要否を後続で判断する | user instruction 2026-05-09: 「サブエージェントにcodex exec実行させるのやめろ」 |
