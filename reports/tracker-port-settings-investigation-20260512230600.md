# Sub-agent実行レポート

## タスク

- 目的: 3rd party tracker 受信ポートと ibis tracker publish 設定の関係を確認し、同一設定化が必要か判断する。
- タスク種別: investigation / config review

## sub-agentを使う理由

- 理由: ユーザー指定により、実装・調査・レビューは gpt-5.5 high sub-agent を使う。tracker port 設定は CaptureOn 比較ログの normal path に関わるため、独立確認する。

## 対象範囲

- 対象:
  - `Tracker/Tracker.Server/appsettings*.json`
  - `Tracker/Tracker.Server/Program.cs`
  - tracker receive / publish 関連実装
  - README / design の該当説明

## 対象外

- 対象外:
  - unrelated refactor
  - commit / push / PR 操作

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/development-orchestrator/SKILL.md`
  - `sed -n '1,220p' reports/tracker-port-settings-investigation-20260512230600.md`
  - `rg -n "Tracker|Receive|Publish|Multicast|Address|Port|UdpTracker|TrackerConnection|TrackerProfiles|Profiles|Ibis|Third|3rd" Tracker/Tracker.Server Tracker/Tracker.Core/Design TrackerConnectionLib README.md`
  - `rg --files Tracker/Tracker.Server Tracker/Tracker.Core/Design TrackerConnectionLib | rg "(appsettings|README|Program\.cs|TrackerConnectionLibReceiverHostedService|UdpTrackerReceiver|Design|\.cs$|\.md$)"`
  - `git status --short`
  - `sed -n '1,240p' Tracker/Tracker.Server/appsettings.json`
  - `sed -n '1,240p' Tracker/Tracker.Server/appsettings.Development.json`
  - `sed -n '1,260p' Tracker/Tracker.Server/Program.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Tracking/TrackerConnectionLibReceiverHostedService.cs`
  - `sed -n '1,260p' TrackerConnectionLib/src/UdpTrackerReceiver.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Tracking/TrackerOptions.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Tracking/TrackerConfigurationResolver.cs`
  - `sed -n '1,220p' Tracker/Tracker.Server/Tracking/TrackerResolvedOptions.cs`
  - `sed -n '1,220p' Tracker/Tracker.Server/Tracking/TrackerPublisherOptions.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Tracking/UdpTrackerPacketPublisher.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Tracking/TrackerCoordinator/ProfileSwitch.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
  - `sed -n '1,240p' Tracker/Tracker.Server/Tracking/TrackerProfileRequestService.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Tracking/TrackerOptionsCloner.cs`
  - `rg -n "ApplyConfiguration|PublisherOptions|Resolve\(|ProfileName|ActiveProfile|Receive" Tracker/Tracker.Server/Tracking Tracker/Tracker.Server/Vision Tracker/Tracker.Tests`
  - `sed -n '1,340p' Tracker/Tracker.Tests/TrackerMulticastReceiverReviewFixTddTests.cs`
  - `sed -n '1,220p' Tracker/Tracker.Tests/TrackerConfigurationBindingTests.cs`
  - `sed -n '1,180p' Tracker/Tracker.Tests/TrackerRuntimeRegistrationTddTests.cs`
  - `sed -n '1,240p' Tracker/Tracker.Server/README.md`
  - `rg -n "Receive|receiver|Tracker:Receive|publish|Publish|10010|11010|224\.5\.23\.2|3rd|third|TrackerConnectionLib|snapshot" Tracker/Tracker.Server/README.md Tracker/Tracker.Core/Design/*.md README.md`
  - `sed -n '240,340p' Tracker/Tracker.Server/README.md`
  - `sed -n '540,585p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `sed -n '24,92p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `sed -n '120,150p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `rg -n "Tracker:Receive|Receive.*Multicast|Receive.*Port|ReceiveAddress|ReceivePort|TrackerReceiveOptions|interface address|live receiver" Tracker/Tracker.Server Tracker/Tracker.Tests Tracker/Tracker.Core/Design`
  - `sed -n '1,260p' Tracker/Tracker.Server/Tracking/TrackerConnectionLibSnapshotRecorder.cs`
  - `sed -n '1,280p' Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
  - `sed -n '1,260p' TrackerConnectionLib/src/MultiTrackerManager.cs`
  - `sed -n '1,260p' TrackerConnectionLib/src/TrackerState.cs`
  - `sed -n '1,260p' TrackerConnectionLib/src/TrackerWrapperPacketDeserializer.cs`
  - `rg -n "TrackerSnapshot|SnapshotLog|TrackerSnapshotSources|RecordCount|SkippedRecordCount|ErrorCount|SourceRole|SourceLabel|Sidecar|IsCreated|Metadata" Tracker/Tracker.Server Tracker/Tracker.Tests`
  - `sed -n '260,330p' Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
  - `sed -n '50,88p' Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs`
  - `sed -n '1,320p' Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`
  - `sed -n '1,120p' Tracker/Tracker.Tests/TrackerLiveExternalTrackerReceiverTddTests.cs`
  - `nl -ba Tracker/Tracker.Server/appsettings.json | sed -n '20,130p'`
  - `nl -ba Tracker/Tracker.Server/Program.cs | sed -n '35,75p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerOptions.cs | sed -n '70,92p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerConfigurationResolver.cs | sed -n '31,60p'`
  - `nl -ba Tracker/Tracker.Server/README.md | sed -n '258,272p'`
  - `nl -ba Tracker/Tracker.Server/README.md | sed -n '210,214p'`
  - `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '26,30p'`
  - `nl -ba Tracker/Tracker.Server/appsettings.json | sed -n '128,146p'`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/tracker-port-settings-investigation-20260512230600.md`
  - 確認: `Tracker/Tracker.Server/appsettings.json`
  - 確認: `Tracker/Tracker.Server/appsettings.Development.json`
  - 確認: `Tracker/Tracker.Server/Program.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerOptions.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerConfigurationResolver.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerResolvedOptions.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerPublisherOptions.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/UdpTrackerPacketPublisher.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerConnectionLibReceiverHostedService.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/ProfileSwitch.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerProfileRequestService.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerOptionsCloner.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerConnectionLibSnapshotRecorder.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`
  - 確認: `TrackerConnectionLib/src/UdpTrackerReceiver.cs`
  - 確認: `TrackerConnectionLib/src/MultiTrackerManager.cs`
  - 確認: `TrackerConnectionLib/src/TrackerState.cs`
  - 確認: `TrackerConnectionLib/src/TrackerWrapperPacketDeserializer.cs`
  - 確認: `Tracker/Tracker.Server/README.md`
  - 確認: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - 確認: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - 確認: `Tracker/Tracker.Core/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.Core/Design/phases-status.md`
  - 確認: `Tracker/Tracker.Tests/TrackerMulticastReceiverReviewFixTddTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerConfigurationBindingTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerRuntimeRegistrationTddTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerLiveExternalTrackerReceiverTddTests.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 現状、ibis tracker の publish endpoint は `Tracker:Profiles:<profile>:Publish:MulticastAddress` / `Port` で設定される。`appsettings.json` では `default=224.5.23.2:10010`、`sim=224.5.23.2:11010`、`fast=224.5.23.2:10011`。起動時 active profile は `sim`。
  - 現状、3rd party tracker packet receiver は `Tracker:Receive:Enabled=true` のときだけ起動する。`Program.cs` は `TrackerPublisherOptions` の `Port` と `MulticastAddress` を `UdpTrackerReceiver` に渡しており、これは起動時 active profile と `Tracker:RuntimeOverrides:Publish` を解決した publish endpoint である。
  - したがって起動時 default の receiver 監視先は ibis publish endpoint と同じになる。現在の `appsettings.json` では、`Tracker:Receive:Enabled=true` にすると `sim` の `224.5.23.2:11010` を監視する。`ActiveProfileName=default` なら `224.5.23.2:10010`、`fast` なら `224.5.23.2:10011`。
  - ただし `Tracker:Receive` は現状 `Enabled` と `InterfaceAddress` しか持たず、receiver 専用の address / port を明示指定する設定口はない。ユーザー追加条件の「別に指定できる口」は未実装。
  - runtime profile switch 時、publisher は `TrackerCoordinator/ProfileSwitch.cs` で新 profile の endpoint に再設定されるが、live receiver は `Program.cs` で起動時に作った singleton socket のまま再構成されない。README の「active profile の publish endpoint を監視」は起動時 active profile については一致するが、runtime switch 後まで追従する意味に読むと現実装とずれる。
  - README は `Tracker:Receive` が active profile publish endpoint を監視し、`InterfaceAddress` だけを明示設定できると説明している。この点は現実装と一致するが、追加条件の receiver 独自 address / port 口は説明にも実装にもない。
  - design は「receiver は設定済み multicast address / port を使って multicast group に参加する」としており、profile publish endpoint を使う現状とは大筋一致する。一方、receiver 独自 endpoint の optional override は design にまだ明記されていない。
  - ユーザーが「試しに使ったところ 3rd party tracker が記録されていなかった」と言っている観点では、現設定で receiver が見ている endpoint は `Tracker:Receive:Enabled=true` なら起動時 resolved publish endpoint である。現在の `appsettings.json` のままなら active profile `sim` の `224.5.23.2:11010`。3rd party tracker が `224.5.23.2:10010` や別 multicast group / port に publish している場合、receiver は見に行っていない。
  - `Tracker:RuntimeOverrides:Publish:MulticastAddress` / `Port` が指定されている場合、現実装では receiver も override 後の publish endpoint を見る。運用者が「profile の publish endpoint」を見ているつもりでも、runtime override により receiver endpoint がずれる可能性がある。
  - `Tracker:Receive:Enabled=false` のままでは `UdpTrackerReceiver` / hosted service が登録されず、`tracker-packet-snapshots.jsonl` は live 3rd party packet から増えない。CaptureOn 中に ibis own publish は `TrackerCoordinator/Dispatch.cs` から snapshot writer に直接保存され得るが、external は live receiver が起動していないと入らない。
  - CaptureOn でない場合、`TrackerPacketSnapshotLogWriter.Append` は session が disabled なら `Stop()` して return する。つまり receiver が packet を受けていても CaptureOff 中は sidecar に書かない。
  - multicast 受信では `Tracker:Receive:InterfaceAddress` が重要である。未指定時は利用可能 IPv4 interface を列挙して join するが、OS / NIC / multicast routing の都合で 3rd party tracker が流れている interface と合わない場合、record は増えない。複数 NIC 環境では実際に packet が来る local IPv4 を明示する必要がある。
  - `UdpTrackerReceiver` は bind address が `IPAddress.Any` で、`MulticastAddress` が multicast 範囲でない場合は group join せず unicast receive として動く。3rd party 側が multicast なのに receiver address を unicast にする、または逆に unicast 送信なのに multicast group 前提で疎通確認する、といった不一致でも記録されない可能性がある。
  - source 判定・保存条件で external だけを落とす実装は見当たらない。`MultiTrackerManager.GetSourceRole` は ibis `Uuid` / `SourceName` と両方一致なら `own`、両方空なら `unknown`、それ以外は `external` とする。`TrackerConnectionLibSnapshotRecorder` は `LastPacket` があれば role に関係なく writer へ渡し、`TrackerPacketSnapshotLogWriter` は own / external / unknown を metadata source summary に集計する。既存 test でも own / external / unknown 全保存を固定している。
  - external が落ちる例外的可能性は、protobuf decode できない payload、writer 例外、metadata 書き込み失敗、CaptureOff、receiver 未起動、endpoint / interface 不一致である。decode / writer 失敗は `TrackerSnapshotLog.SkippedRecordCount` / `ErrorCount` と logger warning に出る。
  - ユーザーが確認すべき実行時設定 / log / metadata:
    - 実行時 `Tracker:Receive:Enabled` が `true` か。
    - 実行時 active profile が何か。metadata の `TrackerOptions.ActiveProfileName` と `ResolvedTrackerOptions.EngineSettings.ProfileName`。
    - receiver が見る publish endpoint。metadata の `ResolvedTrackerOptions.PublisherOptions.MulticastAddress` / `Port`。現状 receiver endpoint はこれと同一。
    - `TrackerOptions.RuntimeOverrides.Publish.MulticastAddress` / `Port` が入っていないか。
    - `TrackerOptions.Receive.InterfaceAddress` が 3rd party tracker の multicast が届く local IPv4 と一致しているか。
    - session metadata の `TrackerSnapshotLog.IsCreated`、`RecordCount`、`SkippedRecordCount`、`ErrorCount`。
    - session metadata の `TrackerSnapshotSources` に `SourceRole=external` の source があるか、`SourceLabel` / `RemoteEndpoint` / `RecordCount` / `LastReceivedAt` が期待と合うか。
    - sidecar path `TrackerSnapshotSidecarPath` が存在し、`tracker-packet-snapshots.jsonl` の record に `sourceRole=external` があるか。
    - server log の `Started live tracker receiver.`、`Stopped live tracker receiver. HandlerErrorCount=...`、`Skipped invalid tracker packet snapshot payload...`、`Failed to write tracker packet snapshot...`。
  - 最小変更案:
    - `Tracker/Tracker.Server/Tracking/TrackerOptions.cs`: `TrackerReceiveOptions` に optional `string? MulticastAddress` と `int? Port` を追加する。未指定時は現行どおり publish endpoint を使うため後方互換。
    - `Tracker/Tracker.Server/Program.cs`: `UdpTrackerReceiver` 生成時に `receive.Port ?? publisherOptions.Port`、`receive.MulticastAddress ?? publisherOptions.MulticastAddress` を渡す。`InterfaceAddress` は現行どおり `Tracker:Receive:InterfaceAddress`。
    - `Tracker/Tracker.Server/appsettings.json`: 設定口を見せるなら `Receive` に `"MulticastAddress": null, "Port": null` を追加する。省略でも binding 互換は維持できる。
    - `Tracker/Tracker.Tests/TrackerMulticastReceiverReviewFixTddTests.cs` または `TrackerConfigurationBindingTests.cs`: default は publish endpoint、`Tracker:Receive:MulticastAddress` / `Port` 指定時は receiver endpoint が override される contract test を追加する。
    - `Tracker/Tracker.Server/README.md`: `Tracker:Receive` 表に `MulticastAddress` / `Port` を追加し、未指定時は active profile publish endpoint、指定時は receiver 独自 endpointを監視する、と更新する必要がある。
    - `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`: CaptureOn 比較ログの receiver endpoint 解決規則として、`Tracker:Receive` 明示値が優先、未指定時は active profile publish endpoint fallback、と追記するのが望ましい。
    - runtime profile switch 後も receiver を自動追従させる場合は hosted receiver の再構成が必要で、上記の設定口追加より大きい変更になる。最小変更では「live receiver endpoint は起動時解決」として README に明記するのが現実的。

## 結果

- 結果:
  - 現状の default 挙動は「3rd party tracker receiver が ibis tracker publish endpoint と同じ address / port を監視する」になっている。ただし receiver は既定無効なので、実際に監視するには `Tracker:Receive:Enabled=true` が必要。
  - 現状は `Tracker:Receive` 側に独自 address / port を明示指定する設定口がないため、追加条件は満たしていない。
  - 3rd party tracker が記録されなかった直接原因として最も疑わしいのは、`Tracker:Receive:Enabled=false`、3rd party tracker の publish endpoint と起動時 resolved publish endpoint の不一致、または `Tracker:Receive:InterfaceAddress` 未指定 / 誤指定による multicast interface 不一致である。
  - 実装上は external source を保存条件で除外していないため、endpoint / receive 起動 / CaptureOn / decode / writer が正常なら external は `SourceRole=external` として記録されるはず。
  - 同一化そのもののための変更は不要。別 endpoint 指定を可能にするための最小変更は `TrackerReceiveOptions` と `Program.cs` の小変更、README / design / test の追随。

## リスク

- 未解決のリスクまたは後続対応:
  - 調査のみのため、コード・設定・README・design は未変更。
  - live receiver は起動時 endpoint 固定で、runtime profile switch 後の publish endpoint 追従はしない。運用上 CaptureOn 比較を profile switch 後に行う場合は、receiver 再起動または再構成設計が必要。
  - `Tracker:RuntimeOverrides:Publish` がある場合、現状 receiver も同じ resolved publish endpoint を使う。receiver 独自 endpoint 導入後もこの fallback 順序を明文化しないと、override と receive override の優先順位が曖昧になる。
  - `Tracker:Receive:Port` を nullable int とする場合、`0` や範囲外 port の validation は既存 publisher と同程度に `UdpTrackerReceiver` / socket bind 例外へ委ねるか、明示 validation を追加するかを実装時に決める必要がある。
  - 現行 metadata は receiver endpoint を独立 field として保存していない。現状は `ResolvedTrackerOptions.PublisherOptions` から推定できるが、将来 `Tracker:Receive` 独自 endpoint を入れる場合は、capture metadata に resolved receive endpoint を明示保存しないと、事後調査で「どこを監視していたか」が曖昧になる。
