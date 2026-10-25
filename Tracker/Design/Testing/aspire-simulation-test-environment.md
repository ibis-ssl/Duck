# Aspire シミュレーション試験環境

> **現行設計案・移行未完了・未受入。** Windows の AppHost と利用者ごとの Docker Desktop を使う。UDP とマルチキャストの参加者は、すべて同じ DCP 管理のブリッジネットワーク内へ配置する。Windows 上には AppHost、DCP、利用者のブラウザーを置く。利用者管理の Ubuntu 上の別の Docker Engine は製品実行経路に含めない。Docker Desktop 内部の Linux 仮想環境 や WSL 2 は製品の基盤であり、その変更や削除は求めない。
>
> [2026-10-21 の再編前の設計記録](aspire-simulation-test-environment-history.md)は当時の内容を保持する。本書の方式への移行、Windows 上の DCP、ブリッジ内の通信、準備完了判定は未検証である。

## 目的と設計判断

Windows AppHost から Aspire 13.5.4 の標準 `AddContainer` を使い、利用者自身の Docker Desktop に試験資源を登録する。DCP が資源の作成、状態監視、記録、停止、削除を管理する。別の Docker コマンド管理プログラムとコンテナの所有権を二重に持たない。

DCP は Developer Control Plane の略称で、Aspire が内部で使う起動管理プログラムである。AppHost は資源資源定義を DCP に渡し、Docker Engine がコンテナを実行する。DCP はネットワークの転送機能ではない。[DCP 公式リポジトリ](https://github.com/microsoft/dcp)と[Aspire 13.5.4 のコンテナ作成処理](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/ContainerCreator.cs#L109-L204)を根拠とする。当該版が作る既定の Aspire ネットワークをこの実行の専用ブリッジとする。任意の別ネットワークを追加指定できるとは仮定しない。

課題 #80 の I80-NR-001 に対応し、Duck、DebugHost のサーバー、RefereeDriver の審判情報受信機能、準備確認の受信機能もコンテナ側へ置く。Windows のプロセスにブリッジ内のマルチキャストを直接受信させる経路は要求しない。これは配置の設計変更であり、UDP の公開、中継、ホストネットワークは使わない。Docker Desktop の公式資料は、ブリッジが 仮想環境 内にあり Windows ホストから直接到達できないことを示す（[既知制限](https://docs.docker.com/desktop/features/networking/networking-how-tos/#known-limitations)、[通信の構造](https://docs.docker.com/desktop/features/networking/)）。同じブリッジ内に配置しても、複数受信や外部イメージとの通信の実測は必要である。

現行 `Testing/Duck.Testing.AppHost` の実装はこの設計と異なる。Duck と DebugHost はホスト上の `ProjectResource`、外部資源は Docker コマンド起動管理を介した `ExecutableResource` で、後者は `--network host` を使う。旧 `AddContainer` は DCP の利用者定義ネットワークと `--network host` が競合し、Docker に `cannot attach both user-defined and non-user-defined network-modes` と拒否された。移行時は標準コンテナ資源だけに所有権を集め、同じ引数を戻さない。現行実装や過去の受入証拠を、新配置の実装済み・受入済みの根拠にしない。

## 対象資源

すべての UDP 送受信機能を同じブリッジ内に置く。各イメージの版とハッシュ値を固定し、起動時には完全なコンテナ ID、DCP 資源 ID、実行 ID、ネットワーク ID を記録する。Duck と DebugHost の Linux 向け配布イメージ、準備確認機能を含む各起動構成は後続実装の対象である。

| 資源 | 配置と役割 |
| --- | --- |
| `simulator` | コンテナ。SSL-Vision `224.5.23.2:10020` の送信者。別の SSL-Vision プロセスは起動しない。 |
| `duck` | `Tracker.RuntimeHost` のコンテナ。視覚検出を受信し、`sim` 設定で公式形式 `TrackerWrapperPacket` を `224.5.23.2:11010` へ送信する。 |
| `game-controller` | コンテナ。審判情報 `224.5.23.1:11003` の唯一の送信者。視覚検出と追跡情報も入力する。画面と制御 API は内部 TCP 8082。 |
| `crane`、`cm4-sim` | コンテナ。必要なロボット制御と応答を同じブリッジ内で送受信する。`cm4-sim` は構成に応じて選ぶ。 |
| `referee-driver` | 選択時に起動するコンテナ。新しい実行プロジェクトの単一受信処理で11003を観測し、自身の診断と内部API状態を照合する。自動制御は明示選択時だけ行う。Windows に UDP 受信部を残さない。 |
| `tracker-tigers` | 比較時の Sumatra 追跡機能コンテナ。10020と11003を受信し、11010へ公式形式の追跡情報を送る。TIGERs AI と内蔵 Game Controller は起動しない。 |
| `tracker-erforce` | 比較時の ER-Force AutoRef 追跡機能コンテナ。同じ入力から11010へ公式形式の追跡情報を送る。 |
| `debug-host` | 比較時の `Tracker.DebugHost` コンテナ。自身の追跡・UDP送信を無効にし、10020と Duck / TIGERs / ER-Force の11010を受信する。表示・記録・再生をサーバー内で行い、Windows ブラウザーへ画面を返す。 |
| 各資源の準備確認機能 | 対象コンテナ内で UDP を受信し、対象サービス自身の復号件数と新しさを確認する。ホスト上の旧起動管理プログラムの受信数は代用しない。 |

比較モードは任意選択であり、選択した全資源を同じ AppHost 実行で管理する。三つの追跡器の識別と比較契約は[比較デバッグ設計](tracker-comparison-debug-design.md)に従う。Crane は追跡器ではない。TIGERs AI 対 Crane の対戦経路は別課題 #43 / #50 とし、この比較モードでは起動しない。

Vision 入力から `Tracker.RuntimeHost` の準備完了までを単独で確認する場合は、`Testing:VisionSimulationOnly=true` を明示する。この選択では `simulator` と `duck` の2資源だけを Aspire 標準 `ContainerResource` として登録し、既定の base / comparison モデルは変更しない。既存の `RuntimeHostOnly` は Duck だけの1資源選択として維持する。simulator は `linux-image-lock.json` の `framework-simulator` 一覧ダイジェストを使い、Docker image の `Entrypoint=["tini","--"]` と `Cmd=["./bin/simulator-cli"]` に対応するよう、`tini` と `-- ./bin/simulator-cli -g 2020B --realism None --ibis-port 12346 --ibis-team-color yellow` を指定する。追加の `tini` を command に重ねない。UDP は公開せず、ホストネットワークも使わない。simulator が起動した後に Duck を開始し、Duck 自身の `/health/vision` が復号済み Vision 入力を確認する。これは最小 Vision 入力経路の検証であり、比較資源やその他の外部サービスを含む全体構成の受入を意味しない。固定イメージの実行、同一 DCP bridge 内の multicast 到達、strict health の成功は実測で確認する。

### Linux 向けアプリケーションの配布出力（I81-04A）

次の既存プロジェクトを、復元済み依存を使い、.NET 10 の共有実行環境で動かす配布物として発行する。各出力は .NET アセンブリから起動し、OS 固有の実行ファイルと実行基盤資材を含めない。実行時は後続の I81-04B で確定する .NET 10 実行環境を使う。コンテナイメージの基底、外部イメージ、デバッグ専用資材の版とダイジェストは I81-04B で別に確定する。

| プロジェクト | 固定する配布出力 |
| --- | --- |
| `Tracker/Tracker.RuntimeHost` | `Tracker.RuntimeHost.dll`、対応する `.deps.json` と `.runtimeconfig.json`、`appsettings.json`。 |
| `Tracker/Tracker.DebugHost` | `Tracker.DebugHost.dll`、対応する `.deps.json` と `.runtimeconfig.json`、`appsettings.json`、`wwwroot/app.css`、`wwwroot/favicon.png`。画面に使う静的ファイルを配布先へ含める。 |
| `Testing/Duck.Testing.RefereeDriver.Host` | `Duck.Testing.RefereeDriver.Host.dll`、対応する `.deps.json` と `.runtimeconfig.json`。設定は環境変数で渡すため、追加の設定ファイルを要求しない。 |

自動検査では既存プロジェクトを実際に発行し、生成先の各ファイルと `.runtimeconfig.json` の .NET 10 の共有実行基盤を使う設定を検査する。新しい配布一覧だけの自己検査で代用しない。ここではイメージの取得・作成・起動、外部成果物の取得、実行中の依存導入、デバッグ資材の追加を行わない。

### Linux 向け通常コンテナの配布定義（I81-04B2）

`Testing/Duck.Testing.AppHost/linux-image-lock.json` は、AppHost が既に参照する外部イメージ6件と通常用の .NET 基底2件について、完全な参照値、`digest`、`mediaType`、公開情報で確認した `platforms` を固定する。`otherDescriptors` の未識別項目は対象環境として数えない。`GHCR` の3件では公開手順から確認した `sourceCommit` も記録するが、元リポジトリの変更番号と配布イメージとの暗号学的な対応までは証明しない。固定参照は最上位一覧のダイジェストを使い、公開情報の対象環境一覧から新しい利用者要件を追加しない。

| 管理プログラム | 固定基底 | 定義ファイルと入力元 | 実行入口 |
| --- | --- | --- | --- |
| `Tracker.RuntimeHost` | 固定した `mcr.microsoft.com/dotnet/runtime` 参照 | `Tracker/Tracker.RuntimeHost/Dockerfile`。実際の発行先を入力元にする。 | `dotnet Tracker.RuntimeHost.dll` |
| `Tracker.DebugHost` | 固定した `mcr.microsoft.com/dotnet/aspnet` 参照 | `Tracker/Tracker.DebugHost/Dockerfile`。実際の発行先を入力元にする。 | `dotnet Tracker.DebugHost.dll` |
| `Duck.Testing.RefereeDriver.Host` | 固定した `mcr.microsoft.com/dotnet/aspnet` 参照 | `Testing/Duck.Testing.RefereeDriver.Host/Dockerfile`。実際の発行先を入力元にする。 | `dotnet Duck.Testing.RefereeDriver.Host.dll` |

3つの `Dockerfile` は `COPY . /app` でそれぞれの発行先を取り込み、実行入口のアセンブリと共有実行基盤を発行出力に合わせる。RuntimeHostは `Microsoft.NETCore.App`、画面プログラムと審判情報受信プログラムは `Microsoft.AspNetCore.App` を使う。DebugHost用の追加デバッグ資材と事前配布するデバッガーはI81-04B3、AppHostから固定参照を選択する接続はI81-05に属する。ここでの検査は固定表と同じCI実行で生成した発行出力を照合するが、コンテナイメージの取得・作成・起動や実行環境での提供成立は証明しない。

## 配置案

図の破線は未実装の管理・起動経路を表す。各資源内の UDP 準備確認機能もブリッジ内で動く。別所有者のコンテナはネットワークに加えない。

```mermaid
flowchart LR
 subgraph win[Windows]
  browser[利用者のブラウザー]
  app[AppHost]
  dcp[DCP]
  ports[127.0.0.1 / 動的 TCP 接続先]
  app -. 資源モデル .-> dcp
  browser -. HTTP / WebSocket .-> ports
 end
 subgraph desktop[Docker Desktop]
  engine[Docker Engine]
  subgraph bridge[当該実行の DCP 管理ブリッジ]
   sim[simulator]
   gc[Game Controller]
   duck[Duck / RuntimeHost]
   ref[RefereeDriver / 11003 受信を含む]
   crane[Crane]
   cm4[cm4-sim / 選択時]
   checks[各資源内の UDP 準備確認機能]
   subgraph optional[比較モード]
    tigers[TIGERs tracker]
    erforce[ER-Force tracker]
    debug[DebugHost サーバー]
   end
  end
  foreign[別所有者のコンテナ / 保護対象]
 end
 dcp -. 全選択コンテナの作成・監視・停止 .-> engine
 engine -. 選択資源を同じブリッジで実行 .-> bridge
 ports -. GC 内部 TCP 8082 .-> gc
 ports -. DebugHost 内部 TCP 8080 .-> debug
 ports -. RefereeDriver 内部 HTTP 39110 .-> ref
 ports -. 用途を限定した HTTP 準備確認 .-> checks
```

## 通信契約

UDP はブリッジ内で完結させる。コンテナ IP を固定せず、資源名から接続先を解決する。マルチキャストは マルチキャストの宛先 と ポート を維持し、各受信側はコンテナ内の IPv4 インターフェースで参加する。Windows の NIC を `InterfaceAddress` に渡さない。送信側のインターフェース、同時受信、再参加の挙動は実機受入で確認する。

| 用途 | 送受信と接続先 |
| --- | --- |
| 視覚検出 | simulator → Duck / DebugHost / TIGERs / ER-Force / Game Controller。 `224.5.23.2:10020/UDP`。選択資源が同じ新しい入力を受ける。 |
| 審判情報 | Game Controller のみ → 比較追跡器、Crane、RefereeDriver、および必要な準備確認機能。 `224.5.23.1:11003/UDP`。各受信機能の実装は確認対象とする。 |
| 追跡情報 | Duck / TIGERs / ER-Force → DebugHost と Game Controller 等の必要受信側。 `224.5.23.2:11010/UDP`。三者は公式形式と独立した送信元識別を保持する。 |
| シミュレーション／チーム制御 | `10300/UDP`、`10301/UDP`、`10302/UDP`。構成で使う送信者・受信者を外部契約確認で確定する。 |
| Crane 経路 | `12345/UDP`、`12346/UDP`。報告された `12350/UDP` の使用有無も確認する。 |
| ロボット応答 | simulator の `50100 + robot-id/UDP` と、`224.5.20.(100 + robot-id):50100 + robot-id/UDP` の使用経路を確認する。 |
| 内部 Game Controller 制御 | RefereeDriver 等のコンテナ内利用者 → `ws://game-controller:8082/api/control`。 |
| Windows の Game Controller 画面・制御 | `http://127.0.0.1:<GC割当ポート>/` と同じポートの `ws://127.0.0.1:<GC割当ポート>/api/control`。 |
| Windows の比較画面 | `http://127.0.0.1:<DebugHost割当ポート>/`。DebugHost 内部は HTTP 8080 を設計値とする。既存の HTTPS 転送・待受設定の対応は実装前に確認する。 |
| HTTP 準備確認 | 各資源の限定された検証口を127.0.0.1の動的 TCP ポートへ対応付ける。UDP をホストで受信しない。後述の確認契約が成立するまで公開しない。 |

### 明示的な Game Controller 視覚検証モード（I81-05B3）

`Testing:GameControllerVisionOnly=true` は、通常の全構成や二資源の `VisionSimulationOnly` を切り替えず、simulator、Duck、Game Controller、RefereeDriver の四資源だけを同じ Aspire/DCP 管理ブリッジへ登録する明示選択である。既存の固定イメージ参照と起動引数を使い、Game Controller を `224.5.23.1:11003/UDP` の唯一の送信元とする。UDP はホストへ公開せず、Game Controller の HTTP 8082 は Windows の `127.0.0.1` に動的 TCP ポートで対応付ける。RefereeDriver は観測用の既存設定で起動し、自動運転は有効にしない。AppHost モデル試験は資源種別、固定参照、引数、依存順と公開端点を検証する。Windows 画面操作、RefereeDriver の実観測、DCP 実行と所有停止は別の実行時確認であり、このモデル試験では証明しない。

```mermaid
flowchart LR
 subgraph win[Windows]
  browser[ブラウザー]
  gcPort[127.0.0.1 / GC 割当 TCP]
  debugPort[127.0.0.1 / DebugHost 割当 TCP]
 end
 subgraph bridge[Docker Desktop / 同じ DCP 管理ブリッジ]
  sim[simulator]
  gc[Game Controller / 唯一の 11003 送信者]
  duck[Duck]
  debug[DebugHost / 追跡無効]
  tigers[TIGERs tracker / AI 無効]
  erforce[ER-Force tracker]
  ref[RefereeDriver / 単一受信・自身診断]
  crane[Crane]
  cm4[cm4-sim]
  checks[各資源内の準備確認]
  sim -. 10020 / SSL-Vision .-> duck
  sim -. 10020 .-> debug
  sim -. 10020 .-> tigers
  sim -. 10020 .-> erforce
  sim -. 10020 .-> gc
  duck -. 11010 .-> debug
  tigers -. 11010 .-> debug
  erforce -. 11010 .-> debug
  duck -. 11010 .-> gc
  tigers -. 11010 .-> gc
  erforce -. 11010 .-> gc
  gc -. 11003 .-> tigers
  gc -. 11003 .-> erforce
  gc -. 11003 .-> ref
  gc -. 11003 .-> crane
  sim -. 10020 の受信証跡 .-> checks
  gc -. 11003 の受信証跡 .-> checks
  duck -. 11010 の受信証跡 .-> checks
  crane -. 制御 UDP / 契約確認対象 .-> cm4
  cm4 -. 制御 UDP / 契約確認対象 .-> sim
  sim -. 応答 UDP / 契約確認対象 .-> cm4
  cm4 -. 応答 UDP / 契約確認対象 .-> crane
  ref -. WebSocket /api/control / 観測・明示制御 .-> gc
 end
 browser -. HTTP / WebSocket .-> gcPort
 gcPort -. TCP 8082 .-> gc
 browser -. HTTP / 比較画面 .-> debugPort
 debugPort -. TCP 8080 .-> debug
```

破線は設計上の通信で、実測済みを示さない。準備確認は対応する資源内で行い、独立観測器の受信だけでサービス自身の受信を代用しない。10300、10301、10302等の未確定の制御契約を、図の既知経路へ推測で割り当てない。

### 外部接続先と再作成

Crane、cm4-sim、simulator を同じブリッジへ移しても、別コンテナの `127.0.0.1` は共有しない。既存資料は、Crane の数値 IP、cm4-sim の `inet_addr` と 127.0.0.1 固定応答、simulator の数値 `QHostAddress` を報告している。固定した外部イメージで次の確認を終えることを、設計段階の検証契約とする。確認が終わるまで当該経路と全体受入を阻害として扱う。

| 対象 | 必要な確認と変更判断 |
| --- | --- |
| Crane → cm4-sim／simulator | 送信先を資源名で設定できるか確認する。数値 IP だけを受ける場合は、接続時の名前解決と再接続を外部実装へ加える変更を特定する。 |
| cm4-sim → simulator と応答 | 他コンテナへの送信・応答先に127.0.0.1を使わず、simulator／Craneの資源名へ対応させる。送信先、応答先、待受 ポート、マルチキャスト用インターフェース の設定箇所を固定版で確認する。 |
| simulator → cm4-sim | 数値宛先しか渡せない場合は資源名を再解決する変更を特定する。起動時に一度だけ解決した IP の永続利用は採らない。 |
| 個別再作成 | DCP の資源名から現在の接続先を取得し直す。UDP は送信先と応答先を更新し、マルチキャスト は新しいコンテナのインターフェースで参加し直す。古い接続先は無効化し、通信準備を未完了へ戻す。 |

外部契約を変更できない場合は、固定 IP、暗黙の中継、同一コンテナ化へ自動変更せず、#80 の設計判断へ戻す。後続実装の開始前に、対応する版・変更先・受入条件を記録する。この文書変更では外部イメージを変更しない。

### Windows の TCP 待受

AppHost は Game Controller の8082、DebugHost の8080、必要な HTTP 準備確認口に名前付きの接続先を定義し、動的なホストポートを Dashboard に示す。Windows ブラウザーは Dashboard の `game-controller` と `debug-host` の URL を開く。コンテナ内では対象のネットワークインターフェースで HTTP を待ち受け、ホスト側は127.0.0.1だけとする。コンテナ内の待受と Windows の待受範囲を混同しない。

準備確認口は simulator=39101、Game Controller=39102、cm4-sim=39103、Crane=39104、TIGERs=39105、ER-Force=39106、Duck=39108、RefereeDriver=39110 の内部 TCP ポートを設計値とする。DebugHost は8080の `/health/vision` と `/health/comparison` を使う。各外部サービスに既存の検証口があるとは仮定せず、必要な受信・復号診断を当該コンテナ内へ実装する。検証口はデータ中継をせず、実行 ID、資源 ID、対象の完全なコンテナ ID と結び付けた件数・受信時刻・状態を返す。

Aspire 13.5.4／Windows DCP の 127.0.0.1 待受は**設計段階の確認事項**である。実装前に当該版の 接続先 定義から Docker の ホスト側の待受アドレス までを追跡し、127.0.0.1の指定方法、動的 ポート、HTTP／双方向API、Dashboard表示、再起動時の再割当てを確定する。標準 `AddContainer` で限定できることを確認できなければ、接続先を有効にせず設計を阻害とする。全インターフェース公開、Docker Desktop設定変更、別の起動管理へ切り替えない。

Docker が 127.0.0.1 指定の公開を説明していても、Aspire 13.5.4の設定で保証できた証拠にはしない（[Docker 公開ポート](https://docs.docker.com/engine/network/port-publishing/)、[Aspire ネットワーク概要](https://aspire.dev/fundamentals/networking-overview/)）。Game Controller の UI と `/api/control` が同じ ポート を使うことは[v3.20.3 README](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/README.md)を根拠とする。画面の URL 末尾と実到達性は固定イメージで確認する。

## RefereeDriver の実行と内部診断

### 実行プロジェクトと明示設定

`Testing/Duck.Testing.RefereeDriver.Host` を明示選択する実行プロジェクトとする。薄い `Program` は標準構成の生成と起動へ委譲し、標準の依存登録で単一の `RefereeReceiveService`、API観測、状態照合、内部HTTPを組み合わせる。HTTPの実行時依存 `Microsoft.AspNetCore.App` はこのプロジェクトへ分離し、既存の受信・制御ライブラリと利用側の実行条件を変えない。

次の構成は起動時に読み取る。設定の自動再読込みを保証しない。環境変数で渡す場合は、構成キーの区切り `:` を `__` に置き換える。

| 構成キー | 型と既定動作 | 有効時の契約 |
| --- | --- | --- |
| `RefereeDriver:Enabled` | 真偽値。明示した `true` だけ有効。未指定、`false`、解釈できない値は無効。 | 無効時はUDP受信、Game Controller接続、制御を開始せず、診断経路を登録しない。 |
| `Testing:ContainerMode` | 真偽値。明示した `true` だけコンテナ用待受を選ぶ。 | `Enabled=true` の場合、本設定が `true` なら `http://0.0.0.0:39110`、それ以外は `http://127.0.0.1:39110`。HTTPSへの転送は追加しない。 |
| `RefereeDriver:GameControllerUri` | 有効時に必須の絶対形式の接続先アドレス。暗黙の既定接続先はない。 | `ws` または `wss` の `/api/control` を指定する。通常の内部値は `ws://game-controller:8082/api/control`。観測と明示制御に同じ検査済み接続先を渡す。 |
| `RefereeDriver:InterfaceAddress` | 任意のIPv4アドレス。未指定なら既定の受信インターフェース。 | `224.5.23.1:11003` の受信で共有ポートを許可し、IPv4の全アドレスで待ち受け、指定したインターフェースで参加する。 |
| `RefereeDriver:AutoDriveToActive` | 真偽値。未指定、`false`、解釈できない値は観測専用。 | `Enabled` と本設定がともに明示した `true` の場合だけ、既存の制御処理を一度実行する。 |
| `Testing:Diagnostics:RunId` / `Testing:Diagnostics:ResourceId` / `Testing:IdentityControl:ExpectedRole` | RunIdは空白だけでない文字列。通常の固定ResourceId経路ではResourceIdも必須。起動時に資源キーが未確定の明示制御経路ではExpectedRoleを必須とし、ResourceIdは未設定にする。 | RunIdと役割を起動時に固定し、AppHostが照合したResourceIdだけを同一UDS handoffの初回commitで確定する。送信元アドレスやホスト名から補完しない。 |
| `Testing:Diagnostics:ContainerId` / `Testing:Diagnostics:Generation` | 完全なコンテナIDを半角16進表記64桁で指定し、起動世代は正の `Int64` で指定する。 | 未指定・不正値は帰属不明とする。正常復号件数は記録するが、準備完了にはしない。 |

無効時の診断経路は404となり、既存の待受設定を保持する。無効設定は、HTTPサーバー自体を起動しないことを意味しない。コンテナ内の39110待受と、後続のDCPによるWindows 127.0.0.1の動的ポート公開は別に確認する。

接続先に利用者情報や断片識別子を含めない。コンテナ用構成では127.0.0.1や::1等の自身を指す接続先、既知の自身の名前、資源IDと同じホスト名、診断用39110を指す宛先を起動前に拒否する。コンテナ用でない明示構成の127.0.0.1接続と、既存の制御機能の公開既定値は維持する。

### Desktop BridgeでのRefereeDriver帰属確定

四資源構成などで`Testing:IdentityControl:DesktopBridgeEnabled=true`を明示した場合、AppHostはRefereeDriverにもDuckと同じ実行IDと資源役割ラベルを付け、別々の資源通知監視を登録する。各監視は現在の資源キー・完全なコンテナID・所有ラベル・Docker Engine IDを照合し、対象コンテナだけに`dotnet Duck.Testing.RefereeDriver.Host.dll --identity-control`を実行する。RuntimeHost側と同じprepare/challenge/commitの契約を使い、RefereeDriver側の専用Unixソケット`/tmp/duck-referee-identity.sock`で受け付ける。ソケットはコンテナ外へ公開せず、既存のUDP通信・GC API接続先・Docker Desktop構成を変更しない。

受信開始時点ではコンテナIDと資源キーが未確定でもよいが、帰属は未成立とする。受信中に正しい識別情報がcommitされた時だけ、既存の単一受信処理の世代を切り替え、旧試行と保持中の指令を無効化する。切替後の新しい正常復号件数が増加するまで準備完了にしない。同じ識別情報の再送は冪等とし、RunId・資源キー不一致、世代後退、BootIdの不一致は拒否する。RefereeDriver未選択時またはブリッジ無効時は新しい監視・制御口を起動しない。この受渡しは所有コンテナを照合した実装経路であり、実機でのReady・GC API対応成立は別途検証する。

### 自身の受信診断と状態照合

`GET /health/referee` は自身の受信証跡とAPI状態の現在の照合を返す。一致して準備完了の場合だけ200とし、帰属不明、入力なし、復号失敗、未知の経過時間、期限切れ、状態不一致では503とする。

| 応答項目 | 内容 |
| --- | --- |
| `isReady` / `reason` | 現在の照合結果と理由。失敗後に保持値が解除されると `NoObservation` となり得る。最後の失敗履歴を保持する契約ではない。 |
| `diagnostic.identity` | `runId`、`resourceId`、`containerId`、`generation`。帰属不明時は `null`。構造検査された入力であり、実DCP所有の証明ではない。 |
| `diagnostic` の受信証跡 | `inputId`、`receiverEpoch`、`sourceKey`、`sourceName`、`sourceUuid`、`remoteEndpoint`、`successfullyDecodedCount`、`decodeFailureCount`、`lastReceivedAt`、`decodeAgeUpperBound`、`decodeStatus`、`isReady`、`reason`。単一の対応する受信状態を返す。 |
| `api` | 正規化した `state.command` と `state.phase`、`endpoint`、`endpointGeneration`、`requestSequence`、`readStartedTimestamp`、`readCompletedTimestamp`、`ageUpperBound`。有効な観測がなければ `null`。 |

協定世界時は最終受信日時の表示に使い、新しさは同じプロセスの単調時計で判定する。日時は時差を含む形式（例：`2026-10-22T21:00:00+00:00`）で返す。同じ受信世代・送信元で正常復号件数の増加を確認し、UDP受信から5秒以内、API取得開始から3秒以内の証跡を照合する。API取得処理にも3秒の期限を設ける。読取り開始前の古い入力や、別の受信件数で更新された入力を混ぜず、HTTPの再取得で経過時間を延命しない。

APIの `HALT`、`STOP`、`RUNNING` とUDPの指令を明示対応させる。APIにUDPの `command_counter` があるとは仮定しない。同じプロセス内の観測期間の有限な新しさを照合するもので、異なる通信規約の原子的な同期や実流量での可用性を保証しない。資源の起動世代、受信世代、接続先の設定世代を区別する。

観測処理は0.1秒間隔で順次照合し、UDPを別に受信しない。観測専用時は制御への書込みを行わない。明示した自動制御では、同じ受信処理を使う既存の制御処理が `HALT` から `NEXT_COMMAND`、必要に応じて `FORCE_START` または `NORMAL_START` へ進める。この選択と診断の200判定は独立した契約である。既存のJSON形式と明示的なチーム名設定を保持し、自動改名用の設定は追加しない。

制御の送信は初期出力全体の受信後に行い、正常・失敗・取消のいずれでも接続を破棄する。自動制御の正常完了後も観測を継続し、停止時は受信、API取得、保留制御と明示所有資源を回収する。制御の待機は既存の呼出取消に従い、新しい自前期限を追加しない。

### 実装と受入の証拠範囲

標準登録は既存のUDP生成処理へ渡し、API接続は要求ごとに新しい `ClientWebSocket` へ接続先の絶対アドレスを渡す。固定した数値IPを別に保持しない。CIの隔離した組成試験では通信口と時計を置換し、実際の復号、標準の依存登録、常駐処理、127.0.0.1の動的ポートのHTTP、停止、明示制御を確認する。既存のUDP生成処理は別の動的ポート試験で実ソケットへの設定を確認する。

薄い実行入口の単独起動、標準の通信口をすべて使う実行、実Game Controllerへの接続と名前の再解決、Docker Desktopのマルチキャスト、Windowsでの公開、DCP所有照合は別の未検証事項である。実行プロジェクトの実装を、コンテナ配布やAppHost／DCPへの資源配線の完了に置き換えない。名前空間を共有する候補の採用や適用も、この契約の具体化には含めない。

## AppHost から RuntimeHost への診断識別子受渡し（06A1）

AppHost は `ResourceNotificationService.WatchAsync` で資源通知を受け、登録した `IResource` の参照、イベントの不透明な `ResourceId`、役割を表す `Resource.Name`、完全な `container.id` を照合する。`TryGetCurrentState(ResourceId, out ...)` で取得した現在状態にも同じ資源キーと完全IDがあることを確認し、既知の古い通知から実行先を選ばない。この照合は公開された現在状態との一致を確認するもので、通知の全順序やDCPのDocker接続先との同一性を証明するものではない。

診断ブリッジは `Testing:IdentityControl:DesktopBridgeEnabled=true` の明示選択時だけ有効にする。その場合は `Testing:IdentityControl:DockerContext` と `Testing:IdentityControl:ExpectedEngineId` を明示し、同じcontextの `docker --context <context> info --format {{.ID}}` が期待IDと一致することを確認する。さらに `docker --context <context> inspect --format {{json .}} <完全ID>` で完全ID、実行中状態、当該AppHost実行のrun-idラベルと役割ラベルを照合し、すべて一致した完全IDだけに `docker --context <context> exec -i <完全ID> dotnet Tracker.RuntimeHost.dll --identity-control` を行う。確認に失敗した場合は `exec` しない。これは明示したDocker Desktop context内で通知対象と当該実行の所有コンテナを照合する境界であり、DCPが選択したdaemon endpointとの直接比較を行ったという主張ではない。HTTPの宛先、名前解決、ポート番号は対象選択に使わず、AppHostは利用者のDocker設定や接続設定を変更しない。既定構成はブリッジを有効化せず、RuntimeHostOnly選択もブリッジの有効化とは別である。

DCP/AppHostは資源の作成・起動・停止・削除を引き続き単独で管理する。`exec` は既に起動した対象コンテナ内でRuntimeHostの同梱アセンブリを起動し、診断識別子だけを渡す。別の起動管理者は作らず、Dockerイメージや外部資源、コンテナの起動・停止・削除は操作しない。

常駐RuntimeHostはコンテナ内の固定Unixソケット `/tmp/duck-runtime-identity.sock` で診断制御を受け付ける。`--identity-control` の短命プロセスは同じコンテナ内のこのソケットへ接続し、改行区切りJSONの `prepare` / `challenge` / `commit` を同じ接続で中継する。 `prepare` と `commit` にはRunId、ResourceId、役割名、完全ContainerId、Generationを含め、両者の識別情報を一致させる。常駐プロセスは起動ごとに新しい `BootId` を持ち、`challenge` で返す。AppHost側は前回受理したBootIdと比較し、同じContainerIdでもRuntimeHostプロセスが再起動していればGenerationを増やしてから、同じ接続へ `expectedBootId` と同じ識別情報一式を `commit` する。常駐側はBootIdと識別情報一式の一致を再確認してから受理する。起動時にResourceIdが未確定の経路では、ExpectedRoleだけを事前固定し、ResourceId未設定の間は診断を未帰属として503にする。役割名が一致する `prepare` は候補確認に限り、ResourceIdをbindしない。同じ接続の `commit` が成功した時だけResourceIdを確定する。ResourceIdを起動時に明示する既存経路は従来どおり厳格一致させる。

AppHostが新たに起動した場合は新RunIdで世代状態を開始し、別の所有者が管理する資源へ `exec` しない。RuntimeHost起動時点ではコンテナIDと資源キーがまだ確定していない場合があるため、その経路では役割名だけを期待値として保持し、受渡し完了まで診断識別情報を未帰属とする。新しい識別情報を受理した時点で診断状態と受信時刻を原子的に初期化し、次の正常な受信が観測されるまでは厳格な準備判定を503に保つ。完全IDの欠落・不正、資源ID・役割名・実行IDの不一致、世代後退、同じ世代で異なるコンテナID、challenge後のBootId変化は拒否する。同じ識別情報の再送は冪等とし、既存の新鮮な証跡を不必要に消さない。

このソケットはコンテナ内の制御面であり、ホストへ公開しない。認証情報や識別子をURL、Dashboard、通常ログへ出さない。Docker `exec`、DCPの実行環境、イメージ取得・起動を行わず、受入はDuckの実装にある試験で制御プロセスと同じコンテナソケットの契約まで確認する。実Docker Desktopでの `exec`、資源通知の実到着、コンテナ起動、UDP通信、ホストポート公開は別途未実証のまま記録する。RuntimeHostへの識別情報受渡しはUDPパケットの送信元コンテナを暗号学的に証明するものではない。

## DebugHost のデバッグと記録

Windows の開発環境から、DCP が起動した `debug-host` に接続する。デバッグ用イメージには同じ版のソースコードから作成した実行ファイル、対応する移植可能なデバッグ情報ファイル、`/vsdbg/vsdbg` を事前に含める。実行中のコンテナへの依存追加やデバッグ用 TCP ポート公開を前提にしない。

1. Dashboard／DCP の資源情報から、当該実行の `debug-host` の完全なコンテナ ID と イメージのハッシュ値 を取得する。期待する実行 ID・資源 ID・イメージと照合し、名前だけで対象を選ばない。
2. Windows の .NET デバッグ環境で、Docker Desktop の明示的な `desktop-linux` 接続先を指定した `docker.exe --context desktop-linux exec -i <完全ID> /vsdbg/vsdbg --interpreter=vscode` を接続用の標準入出力として使う。コンテナ内の `dotnet Tracker.DebugHost.dll` のプロセスIDを選ぶ。デバッグ環境の `pipeTransport`、`processId`、`sourceFileMap` に、この接続と `/src` → Windows の同じ版のソースコードの絶対パスを設定する。
3. 停止位置とソースコード／デバッグ情報ファイルの一致を確認する。停止中は受信の新しさが失われ得るため、通信受入の計測時間から除き、再開後に準備確認をやり直す。切断はデバッグ接続だけを終え、コンテナの作成・停止・削除は DCP に任せる。
4. 再作成後は新しい完全 ID とプロセスIDを取得し直す。旧 ID へ再接続しない。Windows の開発環境、事前配布したデバッガー、同じ版のソースコード対応の成立は後続の確認対象である。

記録は `%LOCALAPPDATA%\Duck\Aspire\<run-id>\captures` を実行ごとの Windows 保存先とし、DCP が `debug-host` の `/data/captures` へ読み書き可能で割り当てる。AppHost は所有する実行の保存先だけを作成し、記録先を Dashboard の資源説明とログへ示す。`VisionReceiver__PacketCapture__DirectoryPath=/data/captures` を渡す。現在の設定キーは存在するが、この割当てと読み書きの権限は未実装・未検証である。

利用者はブラウザーで CaptureOn を開始し、終了時に CaptureOff とファイルの書き出し完了を確認する。raw vision、診断、三つの追跡情報、送信元情報、時刻対応、記録の一覧を同じ記録保存先に残す。Windows の保存先から保存先全体を複製して取り出し、関連ファイルを分離しない。DCP の通常停止・後始末は Windows の記録を削除しない。記録の削除は利用者の別操作とする。

同じ起動中は `/diagnostics` で再生する。後日の再生専用起動では、保存した保存先を `/data/replay` へ読み取り専用で割り当て、`VisionReceiver__PacketCapture__DirectoryPath=/data/replay`、記録無効の設定で開く。記録と再生が相互に書き換えないこと、終了後の再読と 送信元識別子 の保持を後続実装・受入で確認する。

## 準備完了、依存、停止

コンテナの開始は通信準備完了ではない。simulatorを先に起動可能にし、その受信者を同時に動かす。Duck／DebugHostの受信を simulator の起動条件にしない。比較追跡器は simulator と Game Controller の準備を待つ。DebugHost の起動を三つの追跡結果受信の後にせず、起動後の比較準備確認で三者の別 識別子 と同じ有限窓内の新しい パケット を要求する。

RefereeDriver は内部 API に接続し、API の状態と11003の審判情報を照合する。各資源の準備確認はブリッジ内で採取した診断を HTTP で DCP／AppHostへ伝える。ホストの旧起動管理で動く `VisionServiceReadiness`、`TrackerPacketReadinessMonitor`、`GameControllerReadinessMonitor` の マルチキャスト受信を残さない。診断の期限切れ、復号失敗、対象IDの変更では準備未完了へ戻す。`WaitFor` は開始順を定め、障害後の自動再起動を保証しない。

```mermaid
sequenceDiagram
 participant User as 利用者 / Windows ブラウザー
 participant App as Windows AppHost
 participant DCP as Windows DCP
 participant Engine as Docker Desktop Engine
 participant Sim as simulator / ブリッジ
 participant Receivers as Duck・比較追跡器・DebugHost / ブリッジ
 participant GC as Game Controller / ブリッジ
 participant Ref as RefereeDriver / ブリッジ
 Note over User,Ref: 移行後の管理・通信経路。実機未受入。
 User-->>App: 選択資源を起動
 App-->>DCP: 全コンテナと接続先のモデル
 DCP-->>Engine: 同じ実行ネットワークで作成・監視
 Engine-->>Sim: 起動
 Engine-->>Receivers: 起動して受信を開始
 Engine-->>GC: 起動
 Engine-->>Ref: 起動
 Note over Sim,Receivers: 開始と通信準備を分け、受信待ちの循環依存を作らない
 Sim-->>Receivers: 10020 / 同じ視覚検出
 Receivers-->>GC: 11010 / 追跡情報
 Receivers-->>App: 各資源自身の限定 HTTP 受信・復号診断
 Ref-->>App: 自身の GET /health/referee / 内部39110
 App-->>User: Dashboard に GC と DebugHost の localhost URL
 User-->>GC: 127.0.0.1 動的 TCP 経由の UI / API 操作
 Ref-->>GC: 内部 WebSocket /api/control / 観測・明示制御
 GC-->>Ref: API 状態と内部 11003 審判情報
 Ref-->>App: 同じ状態遷移を確認した証跡
 User-->>Receivers: localhost TCP 経由の比較画面・CaptureOn / Off
 User-->>App: 停止・取消
 App-->>DCP: 所有資源だけ停止
 DCP-->>Engine: 所有 ID を照合して停止・削除
 Note over DCP,Engine: 別所有者と Windows 保存記録は保護する
 DCP-->>App: 停止結果と証跡
```

図の利用者からコンテナへの TCP 線は、前述の Windows 127.0.0.1 動的接続先を経由する。製品の UDP が Windows へ出る経路を表さない。正常停止は15秒以内とし、取消、部分失敗、個別再作成、一括停止で当該実行の資源だけを後始末する。固定する SDK は13.5.4であり、過去に観測した DCP 0.25.13を現在の Windows 実行版とみなさない。

## 優先受入条件

すべて将来の受入目標であり、実施済みの結果ではない。項目ごとに合格、失敗、未実施、阻害、未検証を記録する。Windows 上の AppHost と利用者の Docker Desktop で、対象の版と全選択資源を記録して実施する。

| ID | 必須条件 |
| --- | --- |
| `ASPIRE-NET-001` | 全選択資源、Duck、DebugHost、RefereeDriver、準備確認機能が同じ DCP ブリッジ内で動く。AppHost／DCPだけが所有・管理し、開始、取消、途中失敗、二重起動、個別再起動、一括停止でも別所有者を変えず、正常停止15秒以内を完全IDの前後比較で示す。 |
| `ASPIRE-NET-002` | 安定起動後5秒以内に Duck 自身の `VisionPacketsReceivedTotal` が新しい検出データ10件以上の復号を示す。Windows上の観測器や仮想環境内の独立観測器だけでは代用しない。 |
| `ASPIRE-NET-003` | Duck と DebugHost が同じ確認窓で同じ10020入力を受信する。受信件数、入力時刻、検出内容を記録する。 |
| `ASPIRE-NET-004` | TIGERs と ER-Force が同じ10020と Game Controller の11003を受信し、独立した追跡結果を生成する。Game Controller以外が11003を生成しない。 |
| `ASPIRE-NET-005` | DebugHost が TIGERs / ER-Force の11010を別 識別子 として受信し、同じ有限窓の新しさを確認する。 |
| `ASPIRE-NET-006` | Duck の11010も同じ DebugHost が同時受信し、三つの 比較対象の役割 が互いに別の 識別子 へ一意に対応する。Game Controller等の必要受信側にも届く。 |
| `ASPIRE-NET-007` | 観測専用のRefereeDriverを使い、Windows ブラウザーの Game Controller UI操作で `HALT` から `running` へ遷移し、API応答と内部11003受信で同じ遷移を確認する。Crane／cm4-sim／simulatorの必要UDP往復と、視覚検出データ上の100 mm以上の移動を示す。 |
| `ASPIRE-NET-008` | コンテナ内 `InterfaceAddress` 未指定と明示IPv4の両方で受信する。一受信者の開始・停止が他の件数を止めない。VPN、Tailscale、複数NIC等のホスト条件とコンテナ内の選択結果を分けて記録する。 |
| `ASPIRE-NET-009` | 単一AppHostの排他と制御ポート競合を確認する。共有マルチキャスト ポートの使用中判定だけで失敗にしない。個別再作成で新ID・接続先の再解決・応答先更新・マルチキャストの宛先再参加・準備復帰を示す。古いIPを使い続ける場合は失敗とする。 |
| `ASPIRE-HOST-001` | DashboardのGame Controller／DebugHost URLから画面と双方向接続が成立する。Game Controllerの操作、DebugHostの三者比較、127.0.0.1からの到達、127.0.0.1以外での待受なしを確認する。準備確認口を含む全ホストTCP接続先の待受範囲・割当・再割当てを記録する。 |
| `ASPIRE-DEBUG-001` | Windowsから当該所有IDのDebugHostへ標準入出力で接続し、同じ版のソースコード・デバッグ情報ファイルで停止／再開する。デバッグTCP公開なし、再作成後の対象更新、切断後のDCP管理を示す。 |
| `ASPIRE-CAPTURE-001` | CaptureOff後にWindows保存先へ全関連ファイルが残る。停止・再作成後も取り出して再生でき、三者の識別子と時刻対応を保持する。読み取り専用再生が元記録を変えない。 |

RefereeDriverの確認では、有効・無効、帰属欠落・短いID、古い入力、API不一致、停止、明示した自動制御と既定の観測専用を分ける。`GET /health/referee` の本文と200／503を同じ対象の受信世代・件数・単調経過時間に結び付ける。無効時404、内部39110待受、将来のWindows限定公開を個別に確認し、CIの組成試験をWindows実機受入へ転用しない。

HTTP `GET` の400応答、ソケットの待受だけ、Linux別Docker Engineでの成功、WSL内での継続検証は Windows Docker Desktop の合格証拠ではない。Linux／macOSの対応も同じ配置契約で個別に確認し、Windowsの成功や未実施を代用しない。選択した版の外部接続契約、127.0.0.1限定待受、必要経路のいずれかが未確定なら受入を保留する。

## 安全境界と関連課題

UDP公開、マルチキャスト中継、ホストネットワーク、固定コンテナIPを前提にしない。TCPの公開は127.0.0.1の画面・制御・用途を確定した準備確認だけとする。利用者管理Ubuntuの別Docker Engine、別所有者のコンテナ、既存の記録を変更しない。Docker Desktop、WSL、Docker接続先、`PATH`、認証、ネットワーク、ホスト設定の恒久変更は本設計の作業に含めない。

#80はこの配置・外部接続契約・127.0.0.1待受の設計とレビューを扱う。設計受理後の#81で標準コンテナ資源への移行を実装し、#82でWindows実機の受入を行う。この文書変更で#79〜#82を閉じない。旧Docker起動管理のレビュー、過去の環境一覧やパケット観測は当時の証拠に限り、新配置の成立を保証しない。旧履歴は変更せず、本書と比較設計を再レビューする。
