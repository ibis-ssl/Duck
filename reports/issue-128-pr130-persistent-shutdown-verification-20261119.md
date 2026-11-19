# 変更番号130 常駐起動の終了確認

2026年11月19日 UTC。対象は [通常レビュー指摘 NR130-001](https://github.com/ibis-ssl/Duck/pull/130#issuecomment-6994631850) の常駐起動中の Ctrl+C。作業開始時のコミットは `b0299013f46da63a8399ca6beaea512cf45010fa`。

## 原因と修正

FA780 の `fa780\donabe` で試験用の `duck130-trace-20261119-a` を起動した。AppHost は Ctrl+C を受信し、`ApplicationStopping`、停止処理、`ApplicationStopped` を経て約 74 ms で `Run()` から戻った。AppHost が終了した後も、常駐コンテナの CM4 補助処理を実行する `docker exec` とランチャーが残った。約 42 秒の待機期限後に試験プロセスを強制終了し、明示停止で試験用コンテナを回収した。この試行は通常終了の確認には含めない。

親・子・孫の小さな対照で、子の終了値が 0 でも孫が出力ハンドルを継承すると、両方の出力を読み終えるまで 4,245 ms かかった。子の標準出力・標準エラーの継承属性を解除した場合、子の終了と両出力の回収は 32 ms で完了し、両方の記録が残った。[Windows のハンドル継承の仕様](https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-sethandleinformation)

ランチャーからログ出力先を渡された Windows AppHost は、Docker CLI を起動する前に両方の出力ハンドルを自身に限定する。ランチャーは従来どおり両出力を末尾まで読み、AppHost の終了値を返す。信号受信と停止処理の一時計測は実装差分から除いた。

## 修正後の検証

- FA780 の通常ユーザー、`desktop-linux`、既存の固定イメージで試験用 `duck130-trace-20261119-b` を起動した。7 コンテナの稼働と CM4 の初期設定適用を確認して Ctrl+C を送信した。約 2 秒で外側の `dotnet` は終了値 0 を返し、AppHost の終了値 0 と両出力の回収完了を同じ試験のログで確認した。
- Ctrl+C 後も 7 コンテナは同一 ID のまま稼働した。続く `--stop-persistent --stack-id duck130-trace-20261119-b` は終了値 0 で、所有する試験コンテナが消えた。今回作成したイメージと空のネットワークを回収した後、コンテナ・イメージ・ネットワークの一覧は試験前と一致した。両試験の `docker exec` の残存は 0 件だった。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~LauncherProcessContractTests --no-restore` は 12 件すべて成功した。既存の依存関係・ビルド警告が出た。
- 詳細な時刻と生ログは FA780 の作業記録 `H-pr130-evidence/nr130-001-apphost-trace` に保存した。修正後の Session モードの実機再試験と CI は未実施。Session の先行実測は [先行の検証記録](issue-128-pr130-fix-verification-20261118.md)を参照。
追跡表 2 件の文章検査は成功した。今回の報告は規定の文書検査対象外のため、リンク先と文字コードを個別に確認した。
