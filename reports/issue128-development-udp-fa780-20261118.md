# Duck #128 開発用 UDP 転送の FA780 実機確認

2026-11-18 UTC に FA780 の通常ユーザー `fa780\donabe` と Docker `desktop-linux`（Engine 29.9.1）で確認した。起動コマンドは `dotnet run --file scripts/start-duck-simulation.cs`。AppHost は Windows 上で Duck RuntimeHost を起動し、CM4、Simulator、Game Controller、Crane、開発用転送の5コンテナを起動した。

## イメージ

Simulator `a52b6bd`、CM4 `d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a`、Game Controller `3.20.3`、Crane `scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d` は、AppHost の固定タグがローカルにあることを照合して使った。転送用は同じくローカルにある固定 .NET runtime digest `0cf09aaccf6a…` をベースに、各試験の Compose プロジェクトがビルドした。初回に固定イメージが無い環境での取得は検証していない。

## 起動と受信

セッション `local-20261118T102906Z-94180279171c4f4d8acb2ddadf1b43e1`、Compose プロジェクト `duck-a0ea5e91a9e0e3732903`。

| 観測点 | 実測 |
| --- | --- |
| Windows RuntimeHost | 10:29:35 UTC の `/health/vision` は HTTP 200。Vision 643件をデコードし、失敗0件。受信元は `127.0.0.1:52542`。Tracker 315件の送信成功、失敗0件。 |
| Game Controller | Geometry 更新と、Tracker ソースを `ibis (ibis)` に切り替えたログ。 |
| Crane | `/world_model` が `tracker_detected: true`、`available_tracker: true`。 |
| CM4 feedback | Crane の `/robot_feedback` で robot 0 の valid 5,954件、invalid 0件。 |

Windows 側の転送は `127.0.0.1:39109/UDP` と Tracker の WSL 仮想アダプター `172.24.96.1` を使用した。Linux 側の転送は共有名前空間の `eth0`（`172.18.0.2`）で Vision に参加した。転送側の送信ログと、上表の製品側の受信を照合した。

## CM4 再作成後の復旧

この試験の CM4 owner だけを再作成した。AppHost が4つの follower を再作成し、全5サービスが復旧した。Tracker の公開 UDP ポートは `127.0.0.1:64076` から `127.0.0.1:60463` に変わった。再作成直前→復旧後で Vision デコードが 5,076→5,194件、Tracker 送信成功が 2,497→2,555件に増え、エラーはいずれも0件だった。再作成後の Crane でも Tracker 検出と利用可能を確認し、CM4 feedback は robot 0 の valid 823件、invalid 0件だった。

## 終了と片付け

初回は PowerShell のパイプを通してログを収集していた。そこで `Ctrl+C` を送ると AppHost の終了前に収集セッションが閉じ、5コンテナ、ネットワーク、転送用イメージとマニフェストが残った。所有ラベルを照合し、そのプロジェクトだけを `down --rmi local` して残存0を確認した。

launcher を修正し、コンソールから `Ctrl+C` を受けても AppHost が片付けを終えるまで待機するようにした。別セッション `local-20261118T103438Z-c779c8f5a5014375958bb78a2dcefd33`、プロジェクト `duck-036be9605af60f1eb1b7` を README と同じくパイプなしで起動した。Windows RuntimeHost は ready、Vision 1,098件、Tracker 540件送信を記録した。`Ctrl+C` 後に launcher は AppHost の正常終了を記録し、手動の削除なしでコンテナ0、ネットワーク0、転送用イメージ0、一時マニフェストなしを確認した。外側の試験用端末の終了コードは1だった。

## 残る確認

模擬 Compose テストでは、起動失敗時に今回のプロジェクトと転送イメージを片付けることを確認した。マニフェストの書き込みが中断された場合は、一時ファイルを削除し、既存の persistent マニフェストを保護する。

初回の固定イメージ取得、起動中断時の片付け、別 PC の構成は未検証。Vision の診断上の送信元帰属は `Unattributed` だった。外部送信元と TIGERs の動作はこの試験の対象外。
