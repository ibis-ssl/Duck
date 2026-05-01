# TRACKER-000 Design Review r12

## Findings

### High

1. `pending request` の再 drain 条件が、packet 起点の `ProfileSwitched` 後に再び詰まる解釈をまだ許しています。  
   対象: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` の `TrackerCoordinator` 処理規則（`ProfileSwitched` または control-only `Update` の結果処理後に pending request がまだ残り、かつ raw packet 処理中でなければ...`）  
   問題: 今回の意図は「`ProfileSwitched` 後に pending が残っていたら idle に落ちる前に control-only `Update` を再実行する」ことですが、`raw packet 処理中でなければ` の評価時点が未定義です。packet を伴う `Update` の結果処理中にこの判定を行う実装だと条件が偽のままになり、そのまま event loop を抜けると r11 の「`pending=B` が残ったまま idle に入る」不具合が再発します。  
   必要な明確化: packet 起点の `Update` 完了後に pending を再評価する具体的なタイミングを固定し、「result 処理を終えて直列化区間を抜ける前に、pending が残っていれば即座に次の control-only `Update` を起動する」のように書き切る必要があります。

### Medium

2. `ProfileSwitched` 後に原子的に切り替える state に、`TrackedSnapshotStore` の `現在の設定セット名` が含まれていません。  
   対象: `TrackedSnapshotStore` の保持項目（`現在の設定セット名`）と、`ProfileSwitched` 受領時の更新順（`現在適用済み snapshot` / publisher / active profile 表示を先に更新し、その後に store の最新 frame と受信時刻を clear）  
   問題: observer への通知を local state 更新と store clear の後ろへ送ること自体は良いですが、store 側に残る `現在の設定セット名` をどの時点で新 profile 名へ更新するかが未規定です。`active profile 表示` が store 外 state を指す実装だと、`OnProfileSwitched` 直後に「UI 表示は新 profile、store は旧 profile 名」という分裂状態を観測できます。  
   必要な明確化: `TrackedSnapshotStore` の profile 名も同じ原子的切替に含めるのか、あるいは store には profile 名を持たせず単一の source of truth に寄せるのかを明記する必要があります。

3. draft override と explicit apply の境界がまだ曖昧で、実装者によっては override 編集のたびに reconfigure を投げる解釈が残ります。  
   対象: `v1 では UI の微調整はまず draft override として coordinator 側に保持し、engine へは明示 apply 時の snapshot だけを渡す` と、`override 単独更新も v1 では同じ reconfigure request 経路で扱い、profile 名と draft override snapshot を組にして pending request を置き換える`  
   問題: 前者は「明示 apply まで engine に送らない」契約ですが、後者の `override 単独更新` が draft 編集イベントそのものを指すのか、explicit apply 操作を指すのかが文面だけでは確定しません。この曖昧さが残ると、UI のスライダ変更ごとに `pending request` を作って engine state を繰り返し clear する実装が仕様準拠だと読めてしまいます。  
   必要な明確化: `override 単独更新` を「explicit apply 操作」に限定するか、draft 編集イベントとは別語に分けて定義してください。

## Open Questions / Assumptions

- レビューは提示された差分と補足説明だけを根拠に実施し、既存の設計書本文や実装は参照していません。
- `active profile 表示` と `TrackedSnapshotStore.現在の設定セット名` が同一 state であるなら Finding 2 は解消しますが、その同一性は今回の差分だけでは判定できません。

## Updated Files

- `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r12-20260501172011.md`
