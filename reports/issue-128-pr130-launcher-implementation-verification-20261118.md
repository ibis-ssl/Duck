# 課題128・変更番号130の実装と検証

## 対象

- 作業番号は `I128-LAUNCH-01`。[Duck の変更番号130](https://github.com/ibis-ssl/Duck/pull/130)、ブランチ `task/issue-128-launcher-dotnet`。
- 検証した製品版は `5e1eabd17686980c37a34c3745f7dbc169e48086`、比較元は `main` の `985ce8e7eb9357bd08c2b76ebb33a85fd304f59f`。製品差分は `61a31c5` と `5e1eabd` の2コミット。
- 根拠は課題128の後続指示、[起動手順](../Testing/README.md) と [Aspire 環境設計](../Tracker/Design/Testing/aspire-simulation-test-environment.md)。

## 実装内容

- `scripts/start-duck-simulation.ps1` を `scripts/start-duck-simulation.cs` に置き換え、二つの README の起動・停止例を更新した。
- `visibility_graph` の標準構成で、RuntimeHost、DebugHost、Referee Driver の発行、CM4 補助実行ファイルの準備、AppHost のビルドと起動を既存の順序で行う。既定は Session。Persistent の停止処理は所有者、スタック、実行のラベルが一致する資源だけを対象とする。
- Windows ホストへの RuntimeHost 配置、CM4 補助実行ファイルの撤去、通信方式変更は後続の範囲。今回のレビュー前更新は追跡と報告だけ。

## FA780 での検証

2026年11月18日 UTC、FA780 の通常ユーザー `fa780\donabe`、PowerShell、.NET SDK `10.0.404`、Docker Desktop `desktop-linux`（Engine `29.9.1`、Linux AMD64）。作業木は `C:\Users\donabe\AppData\Local\Temp\duck128-xml-stack\H-pr130-review`。試験時の製品ソースは公開版と同一。

- `dotnet run --file scripts/start-duck-simulation.cs -- --help` は終了値0。ファイルベースアプリをコンパイルし、使用法を表示した。
- 起動の初回申請は必要な .NET と CM4 のイメージが見つからないとの自動審査結果で、実行前に拒否された。Dockerfile と AppHost が使う実際の参照を `docker image inspect --platform linux/amd64` で個別確認し、ローカルにあることを確かめて同じ起動を1回実行した。試験時間帯の Docker イメージ取得イベントは0件だった。
- WSL に C コンパイラーが見つからなかったため、同一ソースツリーの CI 成果物から `cm4-transport-control` を配置した。マニフェストと、サイズ 819744 バイト、SHA-256 `cf3a97220151b7ea1487095db55a3de9f4a12d400c34f09946faaffdf13e6a19` を照合した。WSL で補助実行ファイルを自動生成する分岐は未検証。
- `dotnet run --file scripts/start-duck-simulation.cs` を引数なしで1回起動した。実行識別子は `local-20261118T224540Z-506918b7c24044299eb47a7f893dc9f3`。AppHost の起動メッセージと CM4 初期宛先の適用を確認し、同じ所有者・スタック・実行ラベルの7コンテナ（Simulator、CM4、Game Controller、Crane、Duck、Referee Driver、DebugHost）が稼働した。
- 固定外部イメージは CM4 `d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a`、Simulator `a52b6bd`、Game Controller `3.20.3`、Crane `scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d`。稼働中のコンテナから確認した。
- `Ctrl+C` 後、該当コンテナ7件と試験ネットワーク1件が消失した。外側の試験端末の終了値は1で、ランチャー自身の正常終了メッセージは確認できなかった。今回だけに生成した Duck、Referee Driver、DebugHost のイメージ3件は、試験前の一覧と所有記録を照合して追加で削除した。最終的なコンテナ、ネットワーク、イメージ一覧は試験前と同一で、既存コンテナ106件の状態を維持した。
- Persistent の保持・再起動・削除、全資源の準備完了、実パケット到達、試合受入は未検証。試験の証跡は作業機の `H-pr130-evidence` と、当該実行識別子の `artifacts/duck-simulation/` に保存した。

## 文書検査と CI

- `npm run lint:md:setup` は終了値0。README 2本と追跡文書2本に対し、`npm run lint:md:text`、`lint:md:spell`、`lint:md:whitelist` をそれぞれ `-- --files` 付きで実行した。テキスト検査は成功。語彙検査2件は終了値1。cspell は公開版・追跡更新後とも197件、許可語検査は両方とも1637件を報告した。
- 元の PR が変更した README 行にも未登録語の指摘がある。ルートの README は cspell 21件、許可語検査36件、Testing の README はそれぞれ41件、75件。今回の更新には既存文書指摘の修正や辞書更新を含めていない。規定により `reports/` は文書検査の対象外。
- [対象版の Linux CI](https://github.com/ibis-ssl/Duck/actions/runs/43678425814) は `pull_request`、1,445件成功、0件失敗、4件スキップ。checkout した `f80083c7a639efbb864c7e7b710a9e5a6b1d20c3` は製品版と同一ツリー。Linux 発行出力の検証は成功し、Linux のパケット経路と全体受入はスキップされた。Windows ランチャーは上記の FA780 実測に基づく。追跡と報告を含むレビュー対象コミットはローカルで準備するため、対応する公開 CI はない。

通常レビューは未実施。文書検査の指摘と、Session 終了時の外側の端末終了値1を確認事項として引き継ぐ。
