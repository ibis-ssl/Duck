# Tracker live third-party sample investigation

## 目的

- 現在動いている 3rd party tracker packet が、TrackerConnectionLib の sample app から見えるか確認する。
- `/diagnostics` / CaptureOn 側で 3rd party tracker が出てこない原因が、送信 endpoint、interface、receiver 設定、packet 内容、または保存/UI 側のどこにあるか切り分ける。

## 調査条件

- ユーザー申告: いま 3rd party tracker が動いている。
- 調査対象: TrackerConnectionLib sample app、現在の Tracker.Server 設定、実 UDP packet 受信状況。

## 実行内容

- `sub-agent-task-manager` / `codex-delegation-executor` の該当 skill を確認した。
- repo 内で `TrackerConnectionLibExample/TrackerConnectionLibExample.csproj` と `TrackerConnectionLibExample/Program.cs` を確認した。
- `TrackerConnectionLib/src/UdpTrackerReceiver.cs`、`Tracker/Tracker.Server/appsettings.json`、`Tracker/Tracker.Server/Program.cs`、`TrackerReceiveEndpointResolver.cs`、`TrackerConnectionLibReceiverHostedService.cs`、`TrackerConnectionLibSnapshotRecorder.cs`、`TrackerPacketSnapshotLogWriter.cs`、`VisionPacketCaptureSession.cs`、`Tracker.Server/README.md` を確認した。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" NUGET_HTTP_CACHE_PATH="$PWD/.codex-nuget-http-cache" timeout 60s dotnet build TrackerConnectionLibExample/TrackerConnectionLibExample.csproj -m:1 /nr:false` を実行し、build 成功を確認した。
- `timeout 10s bash -lc 'tail -f /dev/null | dotnet TrackerConnectionLibExample/bin/Debug/net10.0/TrackerConnectionLibExample.dll 11010'` を実行した。
- `timeout 10s bash -lc 'tail -f /dev/null | dotnet TrackerConnectionLibExample/bin/Debug/net10.0/TrackerConnectionLibExample.dll 10010'` を実行した。
- 追加観測として `timeout 10s bash -lc 'tail -f /dev/null | dotnet TrackerConnectionLibExample/bin/Debug/net10.0/TrackerConnectionLibExample.dll 11003'` を実行した。
- `ip -4 addr show`、`ip route show`、`ss -ulpn`、`cat /proc/net/igmp`、`ps -ef | rg 'dotnet|Tracker\.Server|TrackerConnectionLibExample|ssl|grsim|er-force'` を実行し、OS 側の multicast / socket / process 状態を確認した。

## 観測結果

- sample app は `Program.cs` で `args[0]` を UDP port として受け取り、未指定時は `11010` を使う。`UdpTrackerReceiver(port, deserializer)` の port-only constructor を使うため、sample app 自体には multicast address / interface 指定の CLI はない。
- `UdpTrackerReceiver` には `UdpTrackerReceiver(port, multicastAddress, deserializer, interfaceAddress)` があり、server 側はこの constructor を使って multicast group join する。sample app はこの経路を使っていない。
- `11010` sample 実行では live packet を受信できた。代表値は `uuid=farwvkgxsyjsnrpbqrvcdqtepjqsbbjl`、`source=ER-FORCE`、`remote=192.168.1.105:*`、`balls=1`、`robots=22`。追加情報どおり 22-23 robots が見えている。
- `10010` sample 実行では 10 秒内に packet 受信出力なし。
- `11003` sample 実行では `uuid=` / `source=` 空、`balls=0`、`robots=0` の packet が複数 remote から見えた。今回の 3rd party tracker 本体は 11010 側と見るのが妥当。
- `Tracker.Server/appsettings.json` の現在値は `Tracker:ActiveProfileName=sim`、`Tracker:Profiles:sim:Publish=224.5.23.2:11010`、`default=224.5.23.2:10010`、`Tracker:Receive:Enabled=false`、`Tracker:Receive:MulticastAddress=null`、`Port=null`、`InterfaceAddress=null`。ユーザー追加情報では実運用設定は `Receive.Enabled=true` に変更済み。
- `TrackerReceiveEndpointResolver.Resolve(...)` は `Receive.MulticastAddress ?? Publisher.MulticastAddress` と `Receive.Port ?? Publisher.Port` を使う。したがって `Receive.Enabled=true`、`MulticastAddress=null`、`Port=null`、`InterfaceAddress=null`、active profile `sim`、かつ publish runtime override なしなら、server receiver の監視先は `224.5.23.2:11010` になる。
- `Program.cs` は起動直後に `startupTrackerOptions = builder.Configuration.GetSection("Tracker").Get<TrackerOptions>()` を読み、`if (startupTrackerOptions.Receive.Enabled)` のときだけ `UdpTrackerReceiver` / `TrackerConnectionLibSnapshotRecorder` / hosted service を DI 登録する。このため `Receive.Enabled` の変更後は `Tracker.Server` restart が必須。起動済み process に後から appsettings を書き換えても receiver 登録は増えない。
- receiver endpoint も singleton 作成時に解決される。README / design どおり runtime profile switch 後に receiver socket は再構成されない。
- `appsettings.Development.json` は Logging のみで `Tracker` override はない。`launchSettings.json` は `ASPNETCORE_ENVIRONMENT=Development` だけを指定する。したがって repo 設定上は Development 環境で `Tracker:Receive` を上書きしていないが、標準 .NET configuration の env var / command-line override は別途あり得る。
- この調査時点の `ps -ef` では `dotnet` / `Tracker.Server` process は確認できなかった。実行中 process がこの workspace から見えていないため、live process が実際にどの設定を読んでいるかは capture metadata または起動ログで確認する必要がある。
- `/proc/net/igmp` には `224.5.23.2` 相当の group membership が複数 interface に見えていた。`ss -ulpn` でも `0.0.0.0:11010` の UDP socket が複数見えており、OS 側で 11010 traffic は存在する。
- `TrackerConnectionLibSnapshotRecorder` は `MultiTrackerManager.TrackerUpdated` から `TrackerPacketSnapshotLogWriter.CapturePacket(...)` へ渡すだけで、external source を除外しない。
- `TrackerPacketSnapshotLogWriter.Append(...)` は `VisionPacketCaptureSession.Enabled` が false のとき `Stop()` して return する。つまり receiver が packet を受けていても、CaptureOn 中でなければ `tracker-packet-snapshots.jsonl` は作られない。
- `VisionPacketCaptureSession` は metadata に `TrackerOptions`、`ResolvedTrackerOptions`、`TrackerSnapshotLog`、`TrackerSnapshotSources` を書く。実行中 server が読んだ設定確認は、最新 session の `*.metadata.json` で `TrackerOptions.Receive.Enabled`、`TrackerOptions.ActiveProfileName`、`ResolvedTrackerOptions.PublisherOptions.MulticastAddress/Port`、`TrackerSnapshotLog.IsCreated/RecordCount/SkippedRecordCount/ErrorCount`、`TrackerSnapshotSources[].SourceLabel/RemoteEndpoint` を見るのが最短。

## 判定

- 3rd party tracker packet は現在 `224.5.23.2:11010` 相当の sim endpoint に出ている。sample app で `source=ER-FORCE`、remote `192.168.1.105:*`、balls 1、robots 22-23 を受信できたため、送信元不在や port 10010 への送信ではない。
- ユーザー指定の `Tracker:Receive:Enabled=true`, `MulticastAddress=null`, `Port=null`, `InterfaceAddress=null` で active profile が `sim` なら、server receiver は `224.5.23.2:11010` fallback で起動する認識でよい。ただしこれは `Tracker.Server` をその設定で restart していること、`Tracker:RuntimeOverrides:Publish` や env var / command-line で publish endpoint が別値に上書きされていないことが前提。
- sample app と server receiver の差分はある。sample app は multicast address を渡さず port-only bind で受信している。一方 server receiver は multicast address を渡して group join する。今回 sample で受信できているため 11010 traffic 自体は見えているが、server 側で増えない場合は「server process が設定を読んでいない / restart されていない / CaptureOn ではない / writer session が開始していない / metadata 側で sidecar が未作成または空」の順に疑うべき。
- source 判定・writer 側で `ER-FORCE` を落とす根拠は見当たらない。`MultiTrackerManager` は self と一致しない uuid/source を `external` とし、writer は role に関係なく保存する。

## 次アクション

- `Tracker.Server` を設定変更後に restart する。起動方法が `dotnet run --project Tracker/Tracker.Server/Tracker.Server.csproj` なら、起動コマンドの env var / command-line に `Tracker__Receive__Enabled`、`Tracker__Receive__MulticastAddress`、`Tracker__Receive__Port`、`Tracker__ActiveProfileName`、`Tracker__RuntimeOverrides__Publish__Port` 相当の override がないか確認する。
- CaptureOn 後の最新 `packet-captures/<session>/*.metadata.json` を確認し、`TrackerOptions.Receive.Enabled=true`、`ActiveProfileName=sim`、`ResolvedTrackerOptions.PublisherOptions.Port=11010`、`TrackerSnapshotLog.IsCreated=true`、`RecordCount>0`、`TrackerSnapshotSources` に `SourceLabel=ER-FORCE` / `RemoteEndpoint=192.168.1.105:*` があるかを見る。
- metadata が `Receive.Enabled=false` なら、実行中 server は変更後設定を読めていない。restart / 起動 working directory / env var / command-line override を確認する。
- metadata が `Receive.Enabled=true` かつ `TrackerSnapshotLog.IsCreated=false` なら、receiver 未起動または CaptureOn 中に writer が開始していない可能性が高い。`TrackerConnectionLibReceiverHostedService` の起動ログ、`TrackerPacketSnapshotLogWriter` の `Writing tracker packet snapshots to ...` ログを Information で見えるようにして確認する。
- metadata が `IsCreated=true` かつ `RecordCount=0` なら、server receiver の endpoint / multicast interface を疑う。`InterfaceAddress=192.168.1.105` を明示して再起動し、`/proc/net/igmp` と `ss -ulpn` で `224.5.23.2:11010` の join / socket を確認する。
- metadata に record があり UI に出ないなら、保存ではなく diagnostics comparison reader / selected source filter / diagnostics log と metadata の対応付けを疑う。
