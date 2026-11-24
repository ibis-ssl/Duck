# Issue #158 独立レビュー指摘対応レポート

## 対象

- リポジトリ: `ibis-ssl/Duck`
- ブランチ: `issue-158-startup-submodule-preflight`
- PR: [#159](https://github.com/ibis-ssl/Duck/pull/159)（Draft）
- base: `main` / `f2e8d3e6f3de2edca55013cf0158abdac73193cc`
- 指摘を受けた実装 HEAD: `0585b095505a124313bdb6ec43df976293541437`
- 対応した実装 HEAD: `d475b70e0cdcd380deee534c068826e82749b8c4`
- 設計: [起動前 Protocol submodule 準備設計](issue-158-startup-submodule-preflight-design-20261123.md)

この報告は、親から提示された独立レビュー指摘 D158-IFR-01〜04 の実装・検証記録です。独立レビューの再実施、PR 本文やコメントの変更、Draft 解除、マージは行っていません。

## 変更

- **D158-IFR-01:** 配置済み checkout を変更前に再検査する際、`git ls-files -v -z` で index の `assume-unchanged` と `skip-worktree` flag を検出し、隠れた変更がある場合は診断して停止します。内容を全ファイルと比較する冗長な走査は追加していません。小さな Git fixture で、通常の `git status` が空でも既存 README の変更を保持し、もう一方の未初期化 submodule を配置しない契約を追加しました。
- **D158-IFR-02:** 固定 Proto 入力を照合する前に、`SslProto.csproj` の root を含む条件、明示的な import / target、Protobuf item の Update / Remove を拒否します。プロジェクト階層の `Directory.Build.props` / `Directory.Build.targets` は入力へ影響する可能性があるため不確定として停止します。条件付き Protobuf item の fixture で両 submodule が未配置のまま停止する回帰試験を追加しました。動的 MSBuild 評価機構は追加していません。
- **D158-IFR-03:** 固定 object store、object 閉包、保存 index、必要 Proto 入力・import の不足・不一致を共通診断にまとめ、対象パス、期待 SHA、実際に確認した状態、README の既存手順に沿った次の安全な手段を示します。
- **D158-IFR-04:** 追加回帰試験、Proto 検査用内部入口、テスト用 link probe に日本語 XML summary を追加しました。UTF-8 BOM と LF を維持しています。

変更した実装・試験ファイルは `scripts/start-duck-simulation.cs` と `Tracker/Tracker.Tests/LauncherProcessContractTests.cs` です。

## 検証

- `git diff --check`: 成功。各コミット前に確認しました。
- 新規 regression fixture: assume-unchanged による隠れた変更の保護、および条件付き MSBuild item による書き込み前停止を追加。CI の完了をもって実行結果を記録します。
- ローカル `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~LauncherProcessContractTests`: 完了できませんでした。最初の実行は `.NET` が `/home/agent/.dotnet/10.0.404.toolpath.sentinel` を作成しようとして read-only filesystem で失敗しました。プロセス単位の `DOTNET_CLI_HOME=/tmp/duck158-cli-home` と初回経験スキップを指定した再試行も、NuGet migration mutex を `/home/agent/.dotnet/corefx/Runtime/SharedMemory` に作成しようとして `EROFS` で失敗しました。永続設定を変更する回避は行っていません。このローカル失敗は最終 HEAD でのテスト実行ではありません。
- 実装 HEAD `0faf5cb6852fcf551c58743ad465d0d0e560c452` の [.NET CI run 45390715628](https://github.com/ibis-ssl/Duck/actions/runs/45390715628) は、1,575 成功・4 スキップ・3 失敗でした。失敗した3件は共通診断の文言へ変更済みの範囲に対し、旧エラーメッセージを完全一致で期待していた既存 assertion です。新規の assume-unchanged と条件付き Protobuf の回帰試験は失敗一覧にありません。assertion を共通診断に合わせた修正を実装 HEAD `d475b70e0cdcd380deee534c068826e82749b8c4` に追加しました。この修正 HEAD の CI は確認時点でまだ開始・完了を確認できていません。
- Windows 固有の試験、Windows 通常起動、Docker を用いた受入、クラウドの通常起動は未確認です。別担当の実機結果と混同していません。

## 残る作業

PR は Draft のままです。最終 HEAD の CI 終了と、通常レビューおよび独立レビューでこの修正範囲を確認する必要があります。統合は、独立レビュー合格後に親が判断します。
