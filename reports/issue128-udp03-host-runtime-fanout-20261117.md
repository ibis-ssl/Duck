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
