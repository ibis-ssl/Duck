# TRACKER-000 Design Review r13

対象: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`

## Findings

1. High - `ProfileSwitched` と同じ `TrackerUpdateResult` に入った最初の `CommittedFrame` / official packet の処理順が規定されていません。`ProfileSwitched` 受領時に publisher 配信先や active profile 表示を切り替える規則は追加されていますが、`CommittedFrames` の反映と official packet 生成をその後に行うことが明文化されていないため、実装次第では「新 profile で生成された最初の frame を旧 publisher 配信先へ送る」「旧 active profile 表示のまま frame を描画する」経路が残ります。`ProfileSwitched` と `WorldFrameCommitted` の順序だけでなく、同一 result 内では `ProfileSwitched` に伴う local state 更新完了後に、その result の `CommittedFrames` / packet 配信を開始することまで契約に含める必要があります。該当箇所: `tracker-architecture-plan.md:267`, `tracker-architecture-plan.md:309`

2. Medium - `ProfileSwitched` と `GeometryReset` が同じ `TrackerUpdateResult` に共存しうるのに、両者の相対順序が未定義です。今回の変更で `ITrackerEngine` は request を `Update` 先頭で消費し、その後の geometry / detection 処理を新 profile で行う契約になりました。このため、profile 切替を伴う packet が同時に geometry major change を起こした場合、`GeometryReset` は新 profile 側の処理結果として発生しえます。しかし設計書では `ProfileSwitched` は `WorldFrameCommitted` より前としか定義されておらず、`GeometryReset` との順序は決まっていません。ここが未定義のままだと、observer が旧 active profile の文脈で `GeometryReset` を受け取る実装を許してしまいます。`ProfileSwitched` を常に先行させるのか、両 event の同時発火を禁止するのかを固定した方が安全です。該当箇所: `tracker-architecture-plan.md:267`, `tracker-architecture-plan.md:309`, `tracker-architecture-plan.md:592`

## Conclusion

r13 は前回指摘の方向性には沿っていますが、同一 `TrackerUpdateResult` 内の event / frame 処理順についてまだ実装解釈の揺れが残っています。上記 2 点を詰めないと、profile 切替直後の最初の出力先と observer 受信順が実装依存になります。
