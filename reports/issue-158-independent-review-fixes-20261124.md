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
- 実装 HEAD `0faf5cb6852fcf551c58743ad465d0d0e560c452` の [.NET CI run 45390715628](https://github.com/ibis-ssl/Duck/actions/runs/45390715628) は、1,575 成功・4 スキップ・3 失敗でした。失敗した3件は共通診断へ移行した範囲に対し、旧文言を期待していた既存 assertion です。新規の assume-unchanged と条件付き Protobuf の回帰試験は失敗一覧にありません。これら3つの assertion を共通診断の実文言に合わせた修正を実装 HEAD `d475b70e0cdcd380deee534c068826e82749b8c4` に追加しました。
- 修正を含む現在の PR HEAD `6bc0aa02480063ee407ff6cb8e25f3041c0bf92c` に関連する [.NET CI run 45391359357](https://github.com/ibis-ssl/Duck/actions/runs/45391359357) は成功しました。`Tracker.Tests` は1,578成功・4スキップ・0失敗です。Linux packet-flow と full-stack acceptance は skip です。Windows 固有動作の検証結果ではありません。
- Windows 固有の試験、Windows 通常起動、Docker を用いた受入、クラウドの通常起動は未確認です。別担当の実機結果と混同していません。

## 残る作業

PR は Draft のままです。最終 HEAD の CI 終了と、通常レビューおよび独立レビューでこの修正範囲を確認する必要があります。統合は、独立レビュー合格後に親が判断します。

## IFR-02 通常再レビュー追補（2026-11-24）

### 対象と判断

- 通常再レビュー指摘: [コメント #7132470539](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7132470539)。指摘対象は `5a6b470e6831bf1007bed64d2c3d4c4d617cdd92` です。
- 対応した実装 HEAD: `a8d458fb121e916ad6cf6c44426e1a58b96b8104`。変更は `scripts/start-duck-simulation.cs` と `Tracker/Tracker.Tests/LauncherProcessContractTests.cs` のみです。
- **親 ImportGroup の条件:** 条件付き Import / ImportGroup 内の参照先を現在の設定値の複製で静的に読み、Proto 関連の設定値が変わる場合は書き込み前に停止します。無関係な設定だけを含む条件付き import は許可します。繰り返し import は文書の評価順を保ち、再帰 import は不確定として停止します。
- **後続の空値:** 追跡する設定は環境、ロード済み props、プロジェクト内の順に評価し、無条件の後続代入を採用します。`DirectoryBuildTargetsPath` の後続空値は最寄りの既定 `Directory.Build.targets` 探索へ戻り、`ImportDirectoryBuildTargets` の後続空値は MSBuild の既定 `true` と同じ扱いになります。MSBuild の [targets 実装](https://github.com/dotnet/msbuild/blob/main/src/Tasks/Microsoft.Common.targets) は空値を `true` に設定し、空の targets path に最寄りのファイルパスを設定します。[公式ドキュメント](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory?view=visualstudio) も両プロパティの既定と制御方法を説明しています。
- **境界:** MSBuild 全体の模倣や新しい評価依存は導入していません。静的に解決できない import、条件付き設定が追跡値を変える場合、または固定範囲外の Proto 宣言は配置前に停止します。`Protobuf_StandardImportsPath` の明示変更を止める既存契約も維持します。

### 回帰確認と証拠

- 追加した fixture: `ConditionalImportGroupProtoSettingStopsBeforeEitherPlacement` と `ConditionalImportGroupWithUnrelatedSettingsDoesNotBlockPreparation` で、条件付きの関連設定は両配置前に停止し、無関係設定だけなら準備が通ることを確認します。`EmptyDirectoryBuildTargetsPathResetsEarlierOverride` と `EmptyImportDirectoryBuildTargetsResetsEarlierDisable` で後続空値後の既定探索を確認します。
- 隣接動作: `DisabledDirectoryBuildTargetsAreNotInspected` と `DirectoryBuildTargetsPathOverrideIsCheckedBeforeEitherPlacement` を同じ `Tracker.Tests` 実行に含め、既定 targets を明示的に無効にする場合と固定置換先を指定する場合を確認しました。
- 対象コード SHA `a8d458fb121e916ad6cf6c44426e1a58b96b8104` の [.NET CI run 45402414960](https://github.com/ibis-ssl/Duck/actions/runs/45402414960) は成功。`Tracker.Tests`: 1,589 成功・4 スキップ・0 失敗。Linux packet-flow ジョブはスキップです。
- ローカルでの焦点試験は未実行です。既存 `/workspace/.duck-dev/dotnet` の .NET SDK 10.0.404 / MSBuild 18.9.15 を使い、標準 `dotnet test --no-restore` と直接 MSBuild `VSTest` の双方を試しましたが、NuGet SDK resolver が read-only の `/home/agent/.dotnet/corefx/Runtime/SharedMemory` に `NuGet-Migrations` mutex を作成できず停止しました。HOME、依存、永続設定を変えて回避していません。
- 先行 SHA に対する Windows 4件成功の証拠は、この実装 SHA の Windows 検証として扱いません。現 SHA の Windows 固有試験、Docker、シミュレーター、実機通信、独立レビューは未確認です。

## IFR-02 の条件付き import と重複 import

[通常再レビュー #7132931370](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7132931370) の対象は公開 HEAD `6e20e0569dd2f7068dcd2ca7337bce76c939a146` です。この追補は前節の import 判定を更新します。

- **Import 自身の条件:** Import 自身と親の条件を同じ規則で扱います。先に読み込んだと確定したファイルは再指定を無視できます。初回読み込みが未確定で、参照先が追跡対象の値を設定する場合は、両サブモジュールの配置前に停止します。その時点で同じ値でも、途中の代入と後続 import の組み合わせで最終値が変わり得るためです。追跡対象の宣言を含まない条件付き import は通します。
- **重複 import:** 選択に使う設定を読む際、読み込みが確定したファイルを正規化したパスで記録し、`Directory.Build.props` からプロジェクトまで共有します。後続の重複 import は設定を再代入しないため、途中の空値による既定動作への復帰が保たれます。
- **判定の範囲:** 既定の SDK 構成で、単一の固定パスに解決できる明示 import と、既存の Proto 入力契約を静的に確認します。式、ワイルドカード、リスト、エスケープ、明示 SDK import など、参照先をこの範囲で決定できない指定では停止します。パッケージと SDK の実際の入力解決は通常の復元とビルドが担当します。

### 検証

- Linux の既設 .NET SDK 10.0.404 で、自己所有の最小プロジェクトを `MSBuild.dll -getProperty` に渡し、評価だけを行いました。条件付き初回 import の事例では Debug で既定 targets を読み込み、Release では読み込みませんでした。先に `Directory.Build.props` から読み込んだ事例では、後続の `./selector.props` を MSB4011 で無視し、途中の空値代入から既定 targets を読み込みました。
- `Tracker.Tests` の対象 14 件がローカルで成功しました。Import 自身の条件の停止、親条件の停止、無関係な条件での配置成功、条件付きと無条件の重複、途中の空値、明示の無効化、固定置換先、標準 import と Proto 宣言の保護を確認しました。最初のテストフィルターは 0 件に一致したため、ビルド済みの同じバイナリに完全修飾名のフィルターを指定し、14 件を実行しました。
- 検証した実装の Git blob は `16c8901486009875ecbe791e2ad9a4a11385dfe2`、回帰テストの Git blob は `15d5aa209dab956799074d6bab8a1cfc3e137942` です。[PR #159 の CI](https://github.com/ibis-ssl/Duck/pull/159/checks) はこの追補を含む公開コミットに対して確認します。前節の Windows 証拠は先行版に対応し、今回の Windows 固有試験は未実施です。
