# Duck #136 通常テスト検証（2026-11-19）

## 対象と範囲

- 対象候補ツリー: `1e553048aca8c117ecc56116f18630f8990c8ca4`（ツリー識別値。コミット SHA ではない）。統合先の基準 main: `482c8aae273f4dc5d383ede1f0bea983e54b29ce`。
- 対象候補の通常再レビューでは、前回の必須指摘が解消し、追加の必須修正はなし。
- 通信経路の契約は、Windows 側・Docker 側の製品通信がともにマルチキャストで、両構成間の境界だけ UDP ユニキャストを使うこと。今回の単体テストでは実機通信を観測していない。

## 通常テスト

実行コマンド:

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~Tracker.Tests.SimulationLauncherTests|FullyQualifiedName~Tracker.Tests.LauncherProcessContractTests|FullyQualifiedName~Tracker.Tests.ComposeSimulationStackTests' --logger 'console;verbosity=minimal' -m:1 /nr:false
```

固定 proto の作業コピーが空だった初回は、生成 protobuf 型を解決できずビルド停止し、テストは0件実行だった。候補の gitlink と照合した既存ローカルソース（`ssl-game-controller` `9b0765fe7a07a55124f6ee957d4559348462d227`、`ssl-simulation-protocol` `1e719a8d410cc84847ec69104da44fed13738291`）から `proto` 入力を一時配置して同じ絞り込みを実行した結果、39 passed（7 + 16 + 16）、0 failed、0 skipped。Docker は起動していない。これは通常の単体テストであり、製品実機の受信や全体構成を確認した結果ではない。

## 文書検査と実機証跡の区別

固定 main からの今回の Markdown 変更行と新設 UDP 設計書全文は、Yomiyasu・許可語検査とも新規指摘0。既存文書の全文に残る指摘は main にもある既存指摘（#144）であり、今回の変更行には該当しない。`reports/**` は Markdown 検査対象外。通常レビューは今回候補で必須事項なしとしたが、独立最終レビューおよび公開先 HEAD の CI は未実施。

2026-11-18 の通信確認は先行版の実機実測として [既存記録](issue128-development-udp-fa780-20261118.md) に保存されている。2026-11-19 の実機受入は [別記録](issue136-fa780-live-acceptance-20261119.md) のとおり CM4 再作成前に中断し、製品側受信と再作成後の復旧は未確認。これらの実機記録を今回の単体テスト結果と混同しない。

## 未実施事項

実機 Docker 受入、独立最終レビュー、公開候補 HEAD に対する CI は未実施。
