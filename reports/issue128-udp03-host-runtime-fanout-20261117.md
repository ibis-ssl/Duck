# Issue #128 / PR #136: host RuntimeHost Vision 分配の実装記録

## 作業範囲

- 対象: 既存 branch `task/issue-128-cm4-compose-restacked`、作業開始 HEAD `844a7fb6d1079080e625c690489296fd1e8333ab`
- 合意した経路: host-networking profile のみ Simulator `--localhost` → Windows RuntimeHost UDP 10020 → decode 成功 datagram の複製 → `127.0.0.1:10021/UDP` → Game Controller
- bridge/default profile の Vision `224.5.23.2:10020/UDP` multicast は変更しない。
- 既存の未コミット差分と既存調査報告を保持した。新しい Docker 起動、管理者操作、security/firewall 設定変更、外部送信、PR コメントは実施していない。

## 実装

- `RuntimeVisionReceiverOptions` に無効時既定の転送先を追加。両方の値が有効なときだけ、RuntimeHost の既存 receiver が正常 decode した payload bytes を loopback UDP へ best-effort で送信する。転送失敗は warning として記録し、受信処理は続ける。
- AppHost の host-networking profile だけ RuntimeHost に `127.0.0.1:10021` を設定し、Game Controller を同 profile に限り `224.5.23.2:10021` に切り替えて UDP endpoint を Compose model に追加した。
- Simulator の既存 `--localhost` は既存差分として保持。起動前の simulator Ibis port 重複検証に 10021 を追加した。
- bridge/default 構成には転送設定・10021 endpoint を加えない。

## 検証

実行:

```powershell
dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~WindowsHostRuntimeProfileUsesRequiredUdpEndpointsAndDoesNotRegisterDuckImage|FullyQualifiedName~ComposeHostNetworkingRejectsSimulatorInputOverlappingSharedUdpPorts|FullyQualifiedName~RuntimeVisionReceiverForwardingTests"
```

結果: 成功 14、失敗 0、skip 0。loopback テストは正常 decode 済み wrapper datagram の byte-for-byte 転送を確認し、AppHost model tests は host profile の 10021 と bridge profile の既存 10020 の差を確認した。Docker Engine や AppHost stack は起動していない。復元済み package advisory と既存 Aspire/build analyzer warnings は表示されたが失敗にはならなかった。

Markdown focused lint は `npm run lint:md:text` と `npm run lint:md:spell` を対象ファイルへ実行したが、worktree に `node_modules/textlint` と `yaml` がなく両コマンドが起動できなかったため unsupported。package install を避けるため `npm ci` は実行していない。whitelist lint は checker が Python を起動するため制約により skip。 `git diff --check` は成功。

## 残る未検証項目

- Docker Desktop の host networking が Windows RuntimeHost から Linux namespace 内の UDP 10021 listener へ転送する実 packet 動作。
- Game Controller が 10021 で同じ Vision を受信・処理すること、他の必要な全 consumer への分配、Tracker egress / CM4 feedback と restart 後の通信。
- この候補は I128-UDP-02 の multicast 観測境界を解決しない。既存記録の Linux eth0 ingress capture と Windows UDP socket 受信 0 の間は未解決であり、Pktmon positive control もない。direct multicast の再試験は行っていない。
- UDP boundary relay の削除、I128-UDP-02 の完了、Issue #128 全体受入は主張しない。I128-FINAL-03 は未着手のまま。

## 区切り時点の状態

- 実装・単体検証・設計/追跡更新・既存調査報告を commit `fd204c28aefa455ae674872117ca2538a602352c`（`Add host RuntimeHost Vision UDP fan-out`）に記録した。
- `origin` の `task/issue-128-cm4-compose-restacked` へ同 commit を push しようとしたが、自動承認レビューが「完全な宛先に対する信頼できるユーザー承認が確認できない」として拒否した。push は実行されず、commit はローカルのまま。
- 終了時に worktree は clean。追加の Docker 起動、設定変更、PR コメントは行っていない。

## 既存記録から整理した次の Docker 実機確認

この実装 commit では Docker image を使っていない。実施した検証は model tests と同一 PC の loopback UDP test のみ。以下は過去の記録にある資材・手順・観測点から整理した未実施の次工程であり、新しい実行や設定変更は含まない。

### 対象と既存 image

- 対象 profile: `Testing:RuntimeHostOnWindows=true`, `Testing:Mode=base`, `Testing:Compose:HostNetworkingEnabled=true`, `Testing:Crane:Planner=visibility_graph`。AppHost と RuntimeHost は Windows native、Docker 側は `cm4-sim` owner と `simulator` / `game-controller` / `crane` の3 follower。
- Compose image refs: CM4 `ghcr.io/ibis-ssl/orion-cm4-sim:d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a`; Simulator `ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd`; Game Controller `robocupssl/ssl-game-controller:3.20.3`; Crane `ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d`。既存記録の bounded two-image probe は Simulator ID `sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79` と同 GC image を使い、`--pull=never` を指定した。次の AppHost run では既存 host-profile 方針のとおり pull を行わず、ローカル image の存在を事前に読むだけで確認する。
- 実装経路の期待値: Simulator に `--localhost`; Windows RuntimeHost ingress `0.0.0.0:10020/UDP`; RuntimeHost の転送先 `127.0.0.1:10021/UDP`; host-network profile の GC command `-visionAddress 224.5.23.2:10021`。GC の既存 config は Tracker `224.5.23.2:11010`、referee `224.5.23.1:11003`、UI `127.0.0.1:8082`。従来 port 10020 は bridge/default profile のまま。

### 実 packet の受入に必要な観測

1. unique な Compose project name を使って host profile を一度起動し、保存した generated Compose config と image IDs から profile flags、固定 image refs、`--localhost`、owner/follower namespace、GC の 10021 command/UDP endpoint を確認する。
2. 同じ時間窓で Windows RuntimeHost の `http://127.0.0.1:39108/health/vision` が HTTP 200 となり、`VisionPacketsReceived` / `VisionPacketsReceivedTotal` と freshness が進むことを記録する。10020 の raw UDP arrival と 10021 の転送結果も観測する。
3. Game Controller が 10021 の stream を実際に処理する証拠を同じ窓から保存する。過去に観測した GC geometry-change log は一度の geometry 受信を示すにとどまり、連続 detection datagram の受信件数を示さない。現状記録にある既存 GC packet counter は確認できていないため、この情報だけでは fan-out acceptance を閉じない。
4. これは Vision の ingress/fan-out の最小実機確認に限る。Tracker `11010` の Windows→Docker 全 consumer への egress、Crane/CM4 feedback loop、再作成後の到達性は別の acceptance evidence が必要。Linux eth0 ingress と Windows socket delivery の multicast 境界は、この unicast candidate の成功だけでは解決にならない。Pktmon positive control も未取得。

### 後片付けの具体化

- AppHost を通常停止した後、必ず今回の unique project name と `com.docker.compose.project` / ownership labels を照合する。過去の `duck-73ceebecc36756054c80` は `docker compose --project-name duck-73ceebecc36756054c80 down --timeout 10` で当該4サービスのみ削除し、container/network/process 残留なしを確認した例。次回は新しい project name を用い、例の ID を再利用しない。
- AppHost 終了だけでは cleanup 完了と扱わない。AppHost/DCP の終了後に正確な project/service labels を読み、必要な場合だけその project に対する同等の `compose down --timeout 10` を行い、同じ labels で container 0 件、project network 0 件、RuntimeHost process 終了、UDP 10020/10021 解放を確認する。別 project の container に対する prune/remove は行わない。
- 先行 run `duck-01a6ff33a97431f5d47a`, `duck-67a672264ffa9204085c`, `duck-2e8c3ad5095fc9a499a1` は過去記録上すでに cleanup 済み。ここに挙げた future acceptance は未実施。
