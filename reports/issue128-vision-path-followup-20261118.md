# Duck #128: Vision の受信経路追跡（2026-11-18）

## 対象と変更範囲

Issue #128 / PR #136 の既存構成に対する通信確認を行った。Docker Desktop、Windows Firewall、永続ネットワーク設定は変更していない。新規イメージやソースマウントも使っていない。診断用の一時 Docker コンテナと RuntimeHost プロセスは各試験後に停止・削除した。

リポジトリは `task/issue-128-cm4-compose-restacked`、開始時 HEAD は `61f5610`。開始時から存在した `Tracker/Tracker.Tests/RuntimeVisionReceiverForwardingTests.cs` の変更と `reports/issue128-udp03-acceptance-followup-20261117.md` は保持した。この作業では実装ファイルを変更していない。

## 確認した構成

- Simulator: `ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd`
- Crane: `ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d`
- Game Controller: `robocupssl/ssl-game-controller:3.20.3`（本作業の RuntimeHost 転送・Tracker 戻り計測では起動していない）
- Crane 固定イメージの `crane.launch.xml` は Vision ポートを 10020 に固定している。`vision_port:=10022` を渡しても有効にならず、launch の引数一覧にも出ない。したがって、当該イメージの起動引数だけでポート競合を回避できる根拠はない。
- 既存 AppHost の host-network 経路は RuntimeHost が Vision UDP 10020 を受信し、`127.0.0.1:10021` に転送する設定。今回、転送先は Windows 内の一時 UDP 観測ソケットにして確認した。

## 試験結果

### Linux 内の送信正例

Simulator を Docker の host network で通常引数（`-g 2020B --realism None --ibis-port 12346 --ibis-team-color yellow --ibis-feedback-addr 127.0.0.1`、`--localhost` なし）で起動した。Perl 標準 Socket API の一時観測器は Linux multicast `224.5.23.2:10020` で Vision を受信した。別の host-network 比較では 390 datagrams / 633,232 bytes を観測し、Game Controller のログに `Geometry changed` が出た。よって Simulator の Linux 側送信と少なくとも GC の Geometry 反応は確認できる。

同じ条件で Crane は multicast に参加していたが、Geometry を正常に処理した証拠は得られず、待機・fallback のログが続いた。Crane 固有の処理は未解決。

### Windows RuntimeHost への一時 unicast 中継

一時観測器が上記 multicast を受け、Docker Desktop gateway `192.168.65.254` 経由で Windows RuntimeHost に unicast 複製した。

- RuntimeHost の Vision listener を UDP 10021 にした確認では、受信診断が 1,004 件まで増えた（診断カウンターは protobuf の正常デコード数）。
- 既存 AppHost の受信・転送設定に合わせて listener を UDP 10020、forward を `127.0.0.1:10021` にした確認では、RuntimeHost の正常デコード数が 851 件、Windows 側の UDP 10021 観測ソケットが 864 datagrams / 1,403,008 bytes を受信した。
- 後者により、Linux multicast → Windows RuntimeHost decode → RuntimeHost の UDP 10021 forward までの実パケット経路を確認した。UDP 10021 の受信先は Game Controller ではなく計測用ソケットなので、GC への実配送・受理は未確認。

### Tracker の戻り方向

同じ `--realism None` 試験中、Linux 側で `224.5.23.2:11010` を観測したが Tracker datagram は 0 件だった。RuntimeHost には Vision datagram が届いている。今回のシミュレーションが Geometry 中心で、追跡対象となる Detection フレームを含んだかは確認していないため、この 0 件だけで Tracker multicast 経路の故障とは断定しない。Detection 入力を含む実条件で、Windows RuntimeHost から Linux/GC 側への Tracker 出力を再確認する必要がある。

## 検証・片付け

- 既存テスト: `dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~RuntimeVisionReceiverForwardingTests" --logger "console;verbosity=normal"` は 1/1 pass。NuGet 脆弱性情報取得エンドポイントへの接続警告はあったが、キャッシュ済み依存で対象テストは通過。
- 診断用 Perl relay は既存 Crane イメージ内の Perl 標準 `Socket` API のみ使用。最初に試した `IO::Socket::Multicast` はイメージに存在せず起動失敗したため、依存追加せず標準 API に切り替えた。
- Docker CLI の最終確認では今回の一時コンテナは残っていなかった。RuntimeHost の一時プロセスも停止し、UDP 10020/10021 の観測ポートを解放した。
- 既存 Game Controller/Simulator コンテナや設定には触れていない。Firewall の画面・設定には触れていない。

## 結論と残件

標準 Socket の一時中継で RuntimeHost の Vision decode と 10021 への UDP forward を通せることは実証できた。したがって、Windows ingress から RuntimeHost までを切り分ける診断手段としてこの中継形は有効。

ただし、Crane が同じ Vision を処理すること、Game Controller が RuntimeHost の 10021 転送を受理すること、Detection を含むデータで Tracker datagram が Windows から Linux/GC 側へ届くことは未実証。固定 Crane image にはポート上書きも既存 Vision relay も確認できなかった。これらが埋まるまで、新しい永続 relay/Compose 実装を確定せず、原因特定済みとも扱わない。次の有効な確認は、既存の承認範囲で Detection を伴う試合データを使い、実 Game Controller の受信状態と Tracker multicast を同時に記録すること。
