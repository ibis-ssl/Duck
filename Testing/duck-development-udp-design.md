# Duck 開発環境の Windows／Docker UDP 転送設計

Duck #128 の Windows 開発環境では、Duck RuntimeHost を Windows で起動し、Simulator、Game Controller、Crane、CM4 simulator は既存の固定イメージで動かす。Duck と各製品のソースは本番と開発で共通にする。開発用 `base` の内外では、Vision と Tracker それぞれのマルチキャストグループ、ポート、データ形式を維持する。開発用の転送は AppHost と専用イメージに置く。

![開発環境の Windows と Docker の UDP 転送設計](duck-development-udp.dio.svg)

[編集用 draw.io ソース](duck-development-udp.drawio)。図は開発用 `base` プロファイルの Windows native 構成を示す。TIGERs は別プロファイルで扱う。

## 本番と共通の部分と開発環境の違い

本番と共通にするのは製品のソース、UDP マルチキャストを使う通信方式、Vision / Tracker のデータ形式である。開発環境では Docker の内側も外側も製品はマルチキャストで送受信し、その2区間を Windows と Docker の境界で UDP ユニキャストによってつなぐ。Windows のループバックと WSL 仮想 NIC、Docker の共有名前空間、境界の転送はこの開発環境で選んだ配置である。

図のアドレスとポートは開発用 `base` の値である。Duck の Tracker は launcher が選ぶ `sim` プロファイルで `11010/UDP` を使う。リポジトリの設定では `default` が `10010/UDP`、`fast` が `10011/UDP` である。本番で選ばれているプロファイルや実際のネットワーク配置は今回の確認範囲に含まれない。[RuntimeHost のプロファイル定義](../Tracker/Tracker.RuntimeHost/appsettings.json)。

| 方向 | Docker 外の Windows | Windows と Docker の境界 | Docker 内の Linux |
| --- | --- | --- | --- |
| Vision · Linux → Windows | Windows 転送 → Duck。ループバック上で `224.5.23.2:10020/UDP` にマルチキャスト | Linux 転送 → Windows 転送。`host.docker.internal:39109/UDP` へユニキャスト | Simulator → Linux 転送、Game Controller、Crane。共有名前空間で `224.5.23.2:10020/UDP` にマルチキャスト |
| Tracker · Windows → Linux | Duck → Windows 転送。WSL 仮想 NIC 上で `224.5.23.2:11010/UDP` にマルチキャスト | Windows 転送 → Linux 転送。Windows の `127.0.0.1` に公開された動的 UDP ポート宛てにユニキャストし、Docker がコンテナの `39111/UDP` に配送 | Linux 転送 → Game Controller、Crane。共有名前空間の `eth0` から `224.5.23.2:11010/UDP` にマルチキャスト |

## Windows と Docker Desktop の通信上の制限

### 公式資料で確認したこと

Docker Desktop のホストネットワーキングは 4.34 以降で設定から有効にでき、Linux コンテナとホストの間で双方向の TCP / UDP 通信に対応する。Docker Desktop での処理は第4層で行われ、コンテナからホストのネットワークインターフェースへ直接アクセスして、その IP アドレスにバインドすることはできない。公式資料はマルチキャストグループ宛ての UDP が Windows と Docker の境界を越えて配送されるかを明記していない。[Docker 公式資料「ホストネットワークドライバー」](https://docs.docker.com/engine/network/drivers/host/)。

### FA780 で確認したこと

- 転送を入れない試験では、同じ Linux 名前空間の Game Controller がシミュレーターからビジョンデータを受信した。Windows 上の RuntimeHost はビジョンポートにバインドしてグループへ参加したが、受信は0件だった。[直接経路の試験記録](../reports/issue128-relay-free-direct-path-20261118.md)。
- Docker Desktop 側のホストネットワーキングを有効にした FA780（Docker Engine 29.9.1）では、`--network host` の UDP ユニキャスト試験で、受信側の起動を確認してから Windows → Docker の `127.0.0.1:49280` と Docker → Windows の `127.0.0.1:49281` で各1件を受信した。同じ PC でのマルチキャスト試験の記録は両方向とも受信0件だった。ただし、このマルチキャスト試験では送信時点に受信側が起動していたことを時刻付きで確認できていない。
- 今回の開発用転送を使った構成では、Windows 側でビジョンデータのデコード、Docker 内で Game Controller と Crane のトラッカーデータ受信を確認した。CM4 の再作成で公開ポートが変わった後も、ビジョンとトラッカーの受信が復旧した。[開発用転送の実機記録](../reports/issue128-development-udp-fa780-20261118.md)。

### 未確認のこと

マルチキャストの直接試験でパケットがどこで欠落したかは特定できていない。別の Docker Desktop バージョン、ネットワークモード、インターフェースや設定での到達可否も未確認である。上の受信0件は試験した条件での観測であり、境界越しのマルチキャストが不可能と判断する根拠にはならない。

### 今回の設計で境界だけをユニキャストにする理由

FA780 で受信を確認できたユニキャストを境界に使い、両側では製品が使うマルチキャストを維持する。転送サービスは UDP データグラムの内容をそのまま運び、受け側でその区間のグループと製品ポートに再送する。これにより、Duck と各製品のソースを開発用に分岐させず、複数の製品が同じグループを受信できる。Windows と Docker の境界に置く転送とインターフェースの割り当ては開発環境固有で、本番環境の配置や使用プロファイルはこの構成に含めない。

## 開発者の操作

| 場面 | 操作 |
| --- | --- |
| 初回 | .NET 10 SDK と Linux コンテナを実行できる Docker Desktop を準備し、取得したリポジトリで `git submodule update --init --recursive` を実行する。初回の復元・イメージ取得にはネットワーク接続が必要。 |
| 起動 | Docker Desktop 起動後にリポジトリのルートで launcher を実行する。launcher は発行とビルドをまとめる。試合設定が必要なときは Dashboard から Game Controller を開く。 |
| 終了 | 起動した端末で `Ctrl+C` を押す。AppHost が Windows プロセスと対象の Compose プロジェクトを停止する。 |

引数なしの launcher は Windows native RuntimeHost と開発用 UDP 転送を起動する。`--host-runtime` も同じ構成を選ぶ。開発者が入力するポート番号や、別端末での転送プログラムの起動はない。既定の Windows native 構成には、Docker Desktop 側の稼働中の WSL 仮想アダプターが1つ必要になる。

launcher から起動した場合、Compose の初回起動はローカルにない固定イメージだけを取得する。ローカルで準備済みのイメージは再利用する。AppHost を直接起動する既存の手順と、実行中のコンテナ再作成は従来の pull 方針を使う。

```powershell
dotnet run --file scripts/start-duck-simulation.cs
```

Linux 内での受信を確認済みの構成は `--helper-free-compose` で選ぶ。`--persistent` も既存の Linux 内構成を選び、同じ stack ID で再起動と停止を管理する。AppHost を直接起動する場合の構成選択は従来どおりとする。

## 転送の契約

- Vision は Simulator から Linux 内の `224.5.23.2:10020/UDP` に送る。Game Controller と Crane は直接受け取る。Linux 転送は datagram の複製を `host.docker.internal` の開発用 UDP ポートに送り、Windows 転送が Windows のループバック上の同じグループと製品ポートへ再送する。Duck は既存の `VisionReceiver:InterfaceAddress` を使ってループバックでグループに参加する。
- Tracker は Duck が `224.5.23.2:11010/UDP` に送る。AppHost は既存の `Tracker:Profiles:sim:Publish:InterfaceName` で Windows の WSL 仮想アダプターを指定する。Windows 転送は同じアダプターでグループに参加し、Linux 転送のループバック公開ポートに UDP ユニキャストで送る。Linux 転送は CM4 の共有名前空間内の `eth0` で Tracker multicast を送る。
- 各転送は一方向につき受信口と出力先を一つ持ち、datagram の内容と境界を保つ。Linux 転送は Vision のみを multicast から取り込み、Windows 転送は Tracker のみを取り込む。CM4 feedback は既存の共有名前空間内で扱う。
- Windows の Vision 転送受信は `127.0.0.1:39109/UDP` に限定する。Linux の Tracker 転送受信 `39111/UDP` は Windows のループバック上で動的に公開する。AppHost は WSL 仮想アダプターを一意に選べない場合やポートを確保できない場合に理由を表示して終了する。

## 実装範囲と受け入れ条件

製品側は通常の受信・送信を使う。旧試作で使ったシミュレーターのポート切替とテスト用ロボットの検出計数を製品側から取り除き、シミュレーターは既定のマルチキャスト先へ送る。

開発用フォルダーに UDP 転送プログラムと Dockerfile を置く。Windows では AppHost 内のサービスが転送コードを使い、Linux では Compose 内の専用コンテナが同じ転送コードを使う。launcher は Windows native 起動に必要な成果物を発行し、Compose は発行済み成果物から転送イメージを作る。AppHost は Compose が公開した Linux 側 UDP ポートを Windows 側の転送先に設定する。Linux 転送も共有名前空間の follower として再作成し、再作成後の公開ポートを AppHost が反映する。

## FA780 で確認したこと

引数なしで起動し、Windows RuntimeHost で Vision を受信した。Game Controller は Vision の Geometry 更新と Tracker ソース切り替えを記録し、Crane は Tracker と CM4 feedback を受信した。CM4 の再作成で公開ポートが変わった後も通信が復旧した。launcher の終了処理を修正し、通常の `Ctrl+C` で試験の Compose リソースと転送用イメージが片付くことを確認した。これらは FA780 の開発構成での観測結果である。初回の固定イメージ取得、別 PC、起動途中の中断と本番での通信は未検証。

- [Windows native 構成の FA780 実機記録](../reports/issue128-development-udp-fa780-20261118.md)
- [既存の実機検証](../reports/issue128-standard-dcp-helper-free-20261118.md)
- [Windows／Docker 間の既存測定](../reports/issue128-relay-free-direct-path-20261118.md)
