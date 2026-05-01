# Sub-agent実行レポート

## タスク

- 目的: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` の設計レビューを実施し、実装前に問題点や抜け漏れを洗い出す
- タスク種別: 設計書レビュー

## sub-agentを使う理由

- 理由: 独立した視点で設計妥当性を確認し、実装前レビューを分離して記録するため

## 対象範囲

- 対象: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` を中心とした `TRACKER-000` の設計内容

## 対象外

- 対象外: 実装変更、進捗管理ファイルの更新、設計方針の確定以外の派生タスク

## 実行コマンド

- `sed -n '1,220p' /home/ibis/ssl/IbisDuck/AGENTS.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/development-orchestrator/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `nl -ba /home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tasks-status.md`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/phases-status.md`
- `rg -n "class VisionReceiverService|class VisionPacketStore|TrackerWrapperPacket|TrackedFrame|ssl_vision_wrapper_tracked|ssl_vision_detection_tracked|TrackerConnectionLib|TrackedSnapshotStore|TrackerCoordinator" /home/ibis/ssl/IbisDuck -g '!reports/**' -g '!Tracker/Tracker.Core/Design/Ref/**'`
- `rg --files /home/ibis/ssl/IbisDuck/SslProto /home/ibis/ssl/IbisDuck/TrackerConnectionLib /home/ibis/ssl/IbisDuck/Tracker/Tracker.Server`
- `nl -ba /home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_detection_tracked.proto | sed -n '1,220p'`
- `nl -ba /home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_wrapper_tracked.proto | sed -n '1,120p'`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionReceiverService.cs | sed -n '1,240p'`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionPacketStore.cs | sed -n '1,220p'`

## 対象ファイル

- 確認:
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `/home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tasks-status.md`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/phases-status.md`
  - `/home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_detection_tracked.proto`
  - `/home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_wrapper_tracked.proto`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionReceiverService.cs`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionPacketStore.cs`
- 変更:
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-20260501120622.md`

## 指摘事項

- `High` `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md:205-207,267-277,492-506,599-613`
  - `ITrackerEngine` は packet 1 件ごとに状態を進める契約なのに、アルゴリズム方針では「camera ごとの raw 観測を時系列順に処理」「同時刻近傍のみを統合」としている。どの時点まで packet をバッファして時刻順に並べ替えるのか、どの窓幅で camera 横断統合を閉じるのかが未定義。
  - 既存受信は UDP 到着順に `StoreDatagram` しているだけで (`/home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionReceiverService.cs:57-58`)、このままでは arrival order 依存になり、最重要要件の「決定性」と velocity / primary ball / contact 判定が揺れる。

- `High` `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md:146-147,183-186,273-277,346-351`
  - `TrackerFrame` の時刻が「処理時刻」としか書かれておらず、official `TrackedFrame.timestamp` / `KickedBall.start_timestamp` に何を入れるかが決まっていない。proto 側は unix timestamp の data time を要求している (`/home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_detection_tracked.proto:36-37,72-74`)。
  - 複数 camera の `TCapture` がずれる状況で、capture time・sent time・receive time・processing time のどれを正とするかが未定義だと、テスト期待値も consumer 解釈も固定できない。

- `Medium` `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md:218-223,492-506,599-605`
  - 保持状態の説明では engine が持つのは「camera ごとの robot track 群 / ball track 群」だけだが、camera 統合の節では「統合後の観測を filter へ 1 回流す」となっている。world 側の track/filter を別に持つのか、filter は camera 側だけなのか、あるいは二段 filter なのかが食い違っている。
  - ここが曖昧なままだと、実装者ごとに状態機械が変わり、latency・平滑化・kick/contact 推定の入力が揺れる。

- `Medium` `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md:417-421,438-450`
  - `Uuid` を profile に含めたまま UI から live 切替可能にすると、同一 process 実行中に source identity が切り替わり得る。official wrapper proto は `uuid` を「while running で一定」としている (`/home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_wrapper_tracked.proto:8-10`)。
  - さらに profile 切替時に既存 track state を維持するのか reset するのかも設計されていないため、consumer 側では別 source に見え、engine 側では旧 parameter 状態を引きずる二重の不整合が起きる。

- `Medium` `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md:179-188,237-239,635-645,655-658`
  - `kicked_ball` をいつ生成し、いつ消すかの寿命規則がない。設計書は kick 検出と開始情報は書いているが、「still moving の間だけ出す」という official proto 側の前提 (`/home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_detection_tracked.proto:83-85`) に対応する clear 条件がない。
  - このままだと古い kick event を packet に残し続ける実装と、早めに消す実装に割れ、AutoRef 側のイベント解釈が不安定になる。

- `Medium` `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md:18-19,281-286,590-596`
  - ball 配列は primary を先頭に置く規則だけがあり、secondary ball の安定順が定義されていない。一方で品質優先順位では決定性を最優先にしている。
  - secondary の順序が merge 順や内部 collection 順に依存すると、同一入力でも packet の並びが揺れ、snapshot test と downstream diff が不安定になる。

## 結果

- 6 件の指摘あり。
- 実装着手前に、少なくとも「時系列バッファ/統合窓」「timestamp の基準」「merge 後 state の持ち方」「profile 切替時の UUID/state 方針」「kicked_ball の寿命」「secondary ball の安定順」を設計書で固定したほうがよい。

## リスク

- geometry 大変更時の reset 方針は `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md:295-300` で保留のままであり、simulation/hardware 切替時の stale track 混入リスクが残っている。
- 本レビューは設計書・関連参照・既存受信コードの整合確認までで、実装や executable test では未検証である。
