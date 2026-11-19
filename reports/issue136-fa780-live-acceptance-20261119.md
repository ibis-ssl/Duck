# Duck #136 FA780 実機確認の中断記録

2026-11-19 UTC に候補 `d04be004683775209a319bacccb968677cfe7578` を使って一度起動した。これは受入完了の報告ではない。Docker の再起動や二度目の実機試験は行っていない。

## 起動前

通常起動コマンド `dotnet run --file scripts/start-duck-simulation.cs` を使い、Docker context `desktop-linux` を確認した。Simulator `a52b6bd`、CM4 `d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a`、Game Controller `3.20.3`、Crane `scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d`、転送用 .NET Runtime の固定 digest `sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3` が候補の参照と一致し、ローカルに存在することを起動前に照合した。

起動前の Docker 資源スナップショットは106コンテナ、3ネットワーク、314イメージ参照だった。SHA-256 は `6b3dc640a8e4572faea62969e870f385bb3fb23dd232b12ae1e5d233074da7cc`。

## 起動、中断、観測

実行 ID は `local-20261119T041315Z-79f3747105894a70a6aa896cccab3d68`、Compose project は `duck-e2cc9f143be505f10019`。Simulator、CM4、Game Controller、Crane、開発用 UDP 転送の5コンテナが起動した。Windows 側ログは Vision UDP `39109` と Tracker インターフェース `172.24.96.1` を示し、Vision と Tracker の初回データグラム転送を記録した。数値のパケット件数や Vision 復号数は採取していない。

Game Controller の製品ログは Vision / Tracker の待受と Tracker source の切替を記録したが、受信件数や個別データグラムの受領を示していない。Crane の製品ログは Vision / Tracker の未受信と Tracker 受信タイムアウトを記録し、world model は未発行だった。製品側で受信できたとは確認できない。CM4 は `172.18.0.2`、転送 UDP のホスト側ポートは `127.0.0.1:50063` だった。CM4 の `AtTarget` 出力は記録したが、これだけで Crane の feedback 受信を確認したとは扱わない。

中断時に確認できた権限情報は、実行中端末のタイトルに「管理者」と表示されていたことだけである。その時点のプロセスについて、同一プロセス内の `whoami /groups` と WindowsPrincipal による判定は採取していない。後に指定レビューを読み取った別プロセスでは`fa780\donabe`、`High Mandatory Level`、WindowsPrincipal判定`True`を記録したが、この値は当該実行プロセスの権限証拠ではない。通常権限の条件を満たすか判断できないため、CM4 再作成前に Ctrl+C で停止した。

## 終了、回収、比較

AppHost のログは終了値 `0` を記録し、ランチャーは Session mode の終了を出力した。外側のコマンドセッションは終了値 `1` だった。AppHost、dotnet、親 PowerShell の各プロセス終了値を個別には採取できていない。停止後、今回のコンテナ、ネットワーク、転送用イメージ、一時 Compose manifest は残っていなかった。起動後に再作成した CM4 はなく、前後の IP / port 比較もしていない。

停止後の既存 Docker 資源スナップショットは開始前と同じ106コンテナ、3ネットワーク、314イメージ参照で、同一の SHA-256 `6b3dc640a8e4572faea62969e870f385bb3fb23dd232b12ae1e5d233074da7cc` だった。今回生成したリポジトリ内の実行ログは一時領域へコピー後、実行 ID の artifact directory だけを削除した。ソース変更はない。

11月18日の成功記録では起動後約29秒に健康確認と Vision / Tracker 件数を採取している。今回の AppHost は約3分半動作し、その間に Crane の Tracker 受信タイムアウトが記録されたが、同じ健康確認と件数は採取していない。待機時間の違いだけで受信未確認の原因を説明できず、原因は未確定である。
