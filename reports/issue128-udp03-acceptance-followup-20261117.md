# Duck #128 / PR #136 追補: fan-out 受入確認

## PR の積み上げ確認

- `gh pr view` で確認: #130 は `main` を base、#134 は #130 の head `5e1eabd` を base、#135 は #134 の head `c93c9c1` を base。
- #136 の base `design/issue-128-cm4-shared-namespace-restacked` は #135 の head `04a6f69b3bd1c0ff665bb6bacb07f9af3127bc73` と一致。
- GitHub compare は `04a6f69` → #136 head `61f5610262249f99834468a0de85a9810d1522a7` が ahead 17 / behind 0。#136 は #135 に正しく積まれているため base は変更しなかった。
- #131 は #130 を base とする並列 PR。#133 の旧枝は `design/issue-128-cm4-shared-namespace` を base にしており、現在の積み上げ先ではない。

## Docker 実行

- 2026-11-17 21:47 UTC、専用 Compose project `duck-262a015618da3c4ff584` で host-runtime profile を起動。既存 image を使い `--pull never`。CM4 / Simulator / Game Controller / Crane の4サービスが起動した。
- 設定は Simulator `--localhost`、Windows RuntimeHost ingress `0.0.0.0:10020/UDP`、同 RuntimeHost の転送先 `127.0.0.1:10021/UDP`、Game Controller の Vision endpoint `224.5.23.2:10021`。
- `http://127.0.0.1:39108/health/vision` は HTTP response を返したが `VisionPacketsReceived=0`、`SuccessfullyDecodedCount=0`、`DecodeFailureCount=0`、`Reason=NoObservation`。
- Game Controller は `224.5.23.2:10021` の listen を記録したが、Vision geometry 更新は記録しなかった。Crane も Vision/Tracker 待ちを記録した。Compose 設定は配線意図を示すだけで、packet 配送の証明ではない。
- この実行では RuntimeHost への受信・decode、および Game Controller の Vision 受信を確認できなかった。原因は特定できず、以前の単一 consumer 受信結果から今回の4サービス経路を推定しない。
- 固有 project の `docker compose down --remove-orphans --timeout 15` を実施。project label に属するコンテナーは0、UDP 10020/10021 と TCP 39108/8082 の関連 listener も後片付け後に0。既存 container の削除や prune は行っていない。

## 現 head CI とローカル修正

- GitHub Actions run [44181594138](https://github.com/ibis-ssl/Duck/actions/runs/44181594138) は #136 head `61f5610` の merge commit `0c77bee` を実行。`.NET tests` は失敗し、Linux packet-flow と full-stack acceptance job は skip。
- 唯一のテスト失敗は `RuntimeVisionReceiverForwardingTests.SuccessfullyDecodedDatagramIsForwardedUnchanged` の5秒 UDP receive timeout。テストが datagram を一度だけ送っており、受信開始との競合時に再送しないことを確認。
- テストを受信完了まで100 ms間隔で同じ valid payload を再送する形に変更。全体 timeout は5秒のまま。
- ローカル `dotnet test ... --filter FullyQualifiedName~RuntimeVisionReceiverForwardingTests` は1/1 passed。変更後の10回連続実行もすべて1/1 passed。restore/build には既存 submodule を初期化した。NuGet vulnerability advisory endpoint への接続は環境制限でできず NU1900 warning が出たが、必要な cached packages で build/test は成功。
- この修正は CI の落ちた unit test に対するもの。Docker の不達原因や fan-out 完了を解決する変更ではない。push 後に新しい head の CI を確認する。

## 残件

- 4サービス構成で Simulator Vision が Windows RuntimeHost に届かない境界を特定する。
- RuntimeHost decode と Game Controller/Crane を含む全 consumer への実 packet 受信を確認する。
- Tracker multicast、CM4/Simulator/Crane feedback 往復、再作成時の通信は未確認。
- Linux eth0 から Windows UDP socket までの multicast boundary 調査、Pktmon positive control も未解決。

## 指示・手順の所在

- checkout と親階層から `AGENTS.md` / `development-orchestrator` を検索したが、この repository 内には見つからなかった。利用可能な skill 一覧にも development-orchestrator はなく、適用範囲を定める独立文書はこの環境から特定できなかった。
- 既存の #128 報告と `Tracker/Design/Testing/aspire-simulation-test-environment.md` の制約を参照し、この追補は未解決項目を成功扱いしない。
