# Sub-agent実行レポート
## Findings
1. [High] `in-flight request` 完了後に残った新しい `pending request` を再度 drain する契約がなく、最新意図がアイドル時に適用されない可能性があります。`TrackerCoordinator` は `pending request` を最新要求で上書きできる一方で、raw packet が無い場合の control-only `Update` 即時実行は 1 回分しか明文化されていません（`Tracker/Tracker.Core/Design/tracker-architecture-plan.md:313`, `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:320`, `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:551`, `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:554`）。`A` を `in-flight` 中に UI が `B` へ更新したケースでは、`A` の `ProfileSwitched` 処理後に raw packet も追加 UI 操作も来なければ `B` を発火させる契機が消え、publisher/UI/engine が古い適用状態のまま止まります。`result` 処理完了時点で `pending request != null && raw packet queue empty` なら直ちに次の control-only `Update` を再実行する、と明記した方が安全です。

2. [Medium] `現在適用済み snapshot` と `in-flight request` の確定／解放タイミングが未規定で、duplicate 判定と打ち消し request の扱いを実装者ごとに解釈できてしまいます。今回の差分は `desired target snapshot`、`pending request`、`in-flight request`、`現在適用済み snapshot` を別管理すると定義し、重複判定もこれらに依存させていますが（`Tracker/Tracker.Core/Design/tracker-architecture-plan.md:320`, `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:555`, `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:556`）、`ProfileSwitched` 受信時にどの順序で `現在適用済み snapshot` を更新し、`in-flight request` を破棄し、残っている `pending request` を再評価するかが書かれていません。ここが曖昧だと、直前に適用済みになった snapshot をまだ未適用として見なして余計な再切替を起こすか、逆に打ち消し request を duplicate と誤判定して落とす恐れがあります。`ProfileSwitched(RequestVersion=X)` の処理完了をもって `currentApplied = inFlight(X)`, `inFlight = null` を同一直列化区間で原子的に確定させ、その後にだけ `pending` の duplicate 判定を行う、と明示してください。

3. [Medium] `ProfileSwitched` 通知時に observer から見える coordinator 外 state の整合タイミングが未定義です。文書は `EmittedEvents` 順で observer 通知するとしつつ（`Tracker/Tracker.Core/Design/tracker-architecture-plan.md:311`）、`ProfileSwitched` 受信時に publisher 配信先切替、active profile 表示更新、`TrackedSnapshotStore` clear を行うとしています（`Tracker/Tracker.Core/Design/tracker-architecture-plan.md:317`, `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:318`, `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:320`）。しかし、それらの state 更新を `ProfileSwitched` observer 通知の前に完了させるのか後に行うのかが書かれていません。同期 observer が `TrackedSnapshotStore` や active profile を読む設計なら、ここが未規定のままだと `ProfileSwitched` を受け取ったのに old profile / old frame が見える競合を招きます。`ProfileSwitched` event を外へ通知する前に coordinator 外 state の反映と store clear を済ませる、と順序を固定した方が実装の揺れを防げます。

## 前提
- レビュー対象はユーザー提示の差分のみで、`tool/exec` や追加のファイル読取は行っていません。
- 行番号は提示差分の適用後座標を前提に記載しています。

## 総評
- r9 の 2 指摘に対する今回の修正方針自体は妥当です。特に `TrackerProfileSwitchRequest` へ immutable snapshot を必須化した点と、raw packet 不在時の control-only drain を MUST に引き上げた点は、前回の主要リスクを正しく潰しています。
- ただし、今回の差分で coordinator 側の状態機械が一段複雑になったため、`pending` / `in-flight` / `current applied` の遷移完了条件と observer 可視順序をもう一段明文化しないと、実装差で old/new state 混在が再発する余地が残ります。

## リスク
- 本レビューは提示差分ベースの設計レビューであり、周辺章や関連設計書との整合は未確認です。
- `ProfileSwitched` / `GeometryReset` の event payload 定義までは提示されていないため、payload 上の識別子や timestamp 契約に起因する問題は今回の判定対象外です。
