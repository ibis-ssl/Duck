# Duck #128: 最終統合確認（2026-11-18）

## 実行ユーザーと権限

- 標準実行では `whoami` が `fa780\\codexsandboxoffline`、Docker CLI の PATH 検索は失敗し、既知の CLI 絶対パスもアクセス拒否だった。
- 直近の指示に沿い、既知の成功手順を再確認した。`functions.exec` の `exec_command` に `sandbox_permissions: "require_escalated"` を付けた `whoami` は `fa780\\donabe` を返した。審査拒否はなく、同じ実行方法で Docker CLI の絶対パス `C:\\Users\\donabe\\AppData\\Local\\Programs\\DockerDesktop\\resources\\bin\\docker.exe` が利用できた。
- これを使い、直前に失敗した project `duck-25fa1af6c4e0bc600434` を確認。project のコンテナはなく、Compose manifest も存在しなかった。検証前に他コンテナの起動がないことも確認した。

## 最終実機確認

- 修正済み AppHost の host-networking + Windows RuntimeHost profile を起動。Run ID: `issue128-final-20261118-fa780donabe-3f5677d`、Compose project: `duck-0842dea77a992216b0e7`。既存固定イメージを使い、pull は行っていない。
- UDP Tracker ingress の動的公開先を `docker compose port --protocol udp duck-tracker-udp-ingress 39112` で確認し、`127.0.0.1:63392` が返った。UDP protocol 指定の修正後に endpoint 解決が成功した。
- 既存 Detection fixture 915 bytes を Simulator control UDP `127.0.0.1:10300` に送信。RuntimeHost `/health/vision` は `VisionPacketsReceived=11829`、`SuccessfullyDecodedCount=11829`、`DecodeFailureCount=0` を報告し、後の読取では件数が `17844` へ増えた。`IsReady=true`。この実行で Simulator → RuntimeHost ingress → Vision decode が進行した。
- Game Controller log に `Geometry changed` と `Switched tracker source to ibis (ibis)` を確認。これは検出・geometry 入力と GC の Tracker source 選択の証拠である。
- RuntimeHost の tracker diagnostics は `SuccessfullyDecodedCount=0`、`Reason=NoObservation` のまま。Crane log は `crane` が referee state に見当たらず、青チーム `Unknown`、黄チーム `ibis` と繰り返し報告した。Game Controller の UI control API に `BLUE` のチーム名 `crane` 更新を送ると `Process change 1` は記録されたが、Crane の直近ログは変わらなかった。
- よって Vision の実受信・復号は確認済み。GC の Tracker source 選択ログも得たが、RuntimeHost Tracker の受信や Crane の正常な Tracker/world-model 処理、Crane 向け Tracker の実配信成功は確認できていない。青チーム割当が不明な fixture 条件も残るため、Tracker 通信の完全成功とは扱わない。

## 検証と後片付け

- 直前の修正後に実行済みの対象 unit/model test: 11 passed / 0 failed。
- AppHost 広域 test の既知失敗 1 件は Docker CLI 不在の標準実行環境に限る。別の 2 件は CRLF/LF 前提と endpoint port allocation 時期のテスト誤りで、いずれも修正済み。以前の run 結果は 125 passed / 1 failed (Docker CLI 起動不能)。
- 実機確認の終了後、この Compose project に対してだけ `docker compose down --remove-orphans` を実行した。project label の残存コンテナは 0、manifest は削除済み。他の起動コンテナも 0 件だった。
- Firewall、Windows network/security settings は変更していない。永続 PATH 変更、Docker pull、既存外部コンテナへの操作もない。

## 残件

- RuntimeHost Tracker diagnostics が `NoObservation` である理由と Tracker 返送経路の受信点を引き続き切り分ける。
- fixture の referee state に Crane 用チームを正しく割り当てた条件で再検証し、Crane の正常な world-model/Tracker 処理を確認する。
