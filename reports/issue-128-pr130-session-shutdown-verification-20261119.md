# 変更番号130 セッション起動の終了確認

2026年11月19日 UTC に、FA780 の `fa780\donabe` と Docker の `desktop-linux` で、修正候補 `5cfac5c66c33d29208e2e625b6b22463cc30c75c` を使用した。[通常レビューの残件](https://github.com/ibis-ssl/Duck/pull/130#issuecomment-6995780329) のうち、NR130-001 のセッション起動を確認した。

## 実行条件

稼働中のコンテナと競合プロセスは 0 件で、排他ロックは利用可能だった。Simulator、Crane、CM4、Game Controller の固定イメージ4種がローカルにあることを、イメージ ID とともに記録した。既存作業コピーから `dotnet run --file scripts/start-duck-simulation.cs` を1回実行した。起動の上限を150秒、Ctrl+C 後の終了待ちを20秒、終了後の資源消失確認を15秒とした。

## 同一試行の結果

- 試験 ID は `local-20261119T003024Z-5be4bb92ed30417c86ced5dad552794d`。所有ラベルで7コンテナの稼働を確認した。接続先には、試験前に存在しなかった `aspire-session-network-jybpzxax-Duck.Testing` が1件あった。
- 00:30:53 UTC に試験専用のコンソールへ Ctrl+C を送った。送信開始の記録から1,964 msで外側の `dotnet` が終了値 `0` を返した。AppHost のログとランチャーの出力に `AppHost exited with code 0; stdout and stderr drained.` があり、AppHost の終了値 `0` と標準出力・標準エラーの終端を確認した。
- 00:31:01 UTC に、同じ7コンテナの ID と試験ネットワークがすべて消えたことを確認した。AppHost とランチャーは強制終了なしで終了し、コンテナとネットワークは Aspire が回収した。
- 作成イベントを所有ラベルで照合し、今回のビルドで作ったイメージ3件だけを、残存コンテナから参照されていないことを確認して削除した。コンテナ、イメージ、ネットワーク、ボリュームの一覧は試験前と一致した。

詳細は FA780 の `H-pr130-evidence/nr130-001-apphost-trace/session-5cfac5c-20261119` に保存した。常駐モードの結果は[先行の検証記録](issue-128-pr130-persistent-shutdown-verification-20261119.md)を参照。既定 ID の常駐試験と README は別工程。CI は未実施。
