# TRACKER-049 design / tracking sync

## 対象

- `TRACKER-049`: diagnostics comparison の design / tracking を再同期する

## 同期内容

- `Tracker.CaptureReplay` の CLI 比較実装を agent / 自動検証 / 調査用として保持する方針を設計へ明記した。
- `/diagnostics` UI で tracker snapshot comparison をユーザーが確認できることを PR ready 前の必須タスクに戻した。
- diagnostics UI comparison の比較基準を、選択中 diagnostics entry に対応する ibis own snapshot の `TrackedFrame.timestamp` とし、source filter 後の 3rdparty tracker snapshot を nearest timestamp で並べる契約にした。
- metadata / sidecar 欠落、`IsCreated=false`、record count 0、読み取り error を既存 diagnostics 表示の blocker にしない status として扱う契約を追加した。
- 固定残タスクを `TRACKER-049` から `TRACKER-053` まで再定義し、旧 `TRACKER-050` の PR ready 化は `TRACKER-053` へ後ろ倒しした。

## 根拠

- task breakdown report: `reports/tracker-049-diagnostics-comparison-task-breakdown-20260512200244.md`
- design audit report: `reports/tracker-049-diagnostics-playback-design-audit-20260512170903.md`
- review report: `reports/tracker-049-review-20260512165902.md`
- CLI 比較実装 report: `reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md`

## 変更ファイル

- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`

## 検証

- design / tracking 文書のみの同期。code / test は未実行。
- gpt-5.5 high review を別途実施して、blocking finding がないことを確認してから commit する。

## 残リスク

- `Tracker.Server/README.md` の既存 docs 差分は `TRACKER-052` の入力として扱う。UI comparison 実装完了前に final docs として commit しない。
- `TRACKER-050` 以降の実装では、既存 CLI の `trackerSnapshot` / `trackerComparison` 出力互換を壊さない。
