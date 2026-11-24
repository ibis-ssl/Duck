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
- 修正を含む当時の PR HEAD `6bc0aa02480063ee407ff6cb8e25f3041c0bf92c` に関連する [.NET CI run 45391359357](https://github.com/ibis-ssl/Duck/actions/runs/45391359357) は成功しました。`Tracker.Tests` は1,578成功・4スキップ・0失敗です。Linux packet-flow と full-stack acceptance は skip です。Windows 固有動作の検証結果ではありません。
- Windows 固有の試験、Windows 通常起動、Docker を用いた受入、クラウドの通常起動は未確認です。別担当の実機結果と混同していません。

## 当時の残る作業

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

## 独立再レビュー前の通常レビューと検証の集約

[通常再レビューの合格記録](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7133221975) は 2026-11-24 13:42:36 UTC に投稿されました。対象の実装 HEAD は `692464cf095ee42d33ea1573f354e25458b45319` です。この記録は通常レビューと修正確認を集約します。独立再レビューの判定は未確定です。

### 通常再レビューの経過

| 投稿日時（UTC） | レビュー対象 HEAD | 結果と記録 |
| --- | --- | --- |
| 2026-11-24 12:25:05 | `31f4051cff36412ae45905d1d4915715b7ef5f18` | IFR-01 と IFR-04 は解消。IFR-02 の入力判定と IFR-03 の読み取り失敗時の診断が残りました。[通常再レビュー](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7131410084) |
| 2026-11-24 12:48:55 | `cdb7c2a730024da9eb9b6cc87a746ddb3d3cbd80` | IFR-03 は解消。IFR-02 の targets の置換・無効化と標準 import の検索先が残りました。[通常再レビュー](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7131955432) |
| 2026-11-24 13:11:12 | `5a6b470e6831bf1007bed64d2c3d4c4d617cdd92` | targets の置換・無効化と標準 import パスの検査を確認。IFR-02 の親条件と後続の空値代入が残りました。[通常再レビュー](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7132470539) |
| 2026-11-24 13:30:34 | `6e20e0569dd2f7068dcd2ca7337bce76c939a146` | 前回の対象。実装は `a8d458fb121e916ad6cf6c44426e1a58b96b8104`。親条件と後続の空値代入は確認済みで、Import 自身の条件と重複 import が残りました。[通常再レビュー](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7132931370) |
| 2026-11-24 13:42:36 | `692464cf095ee42d33ea1573f354e25458b45319` | 通常再レビュー合格。IFR-02 の残る必須指摘は解消し、IFR-01・03・04 の再指摘はありません。[合格記録](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7133221975) |

### 指摘ごとの修正確認

| 指摘 | 修正内容と確認した経路 | 通常レビューでの状態 |
| --- | --- | --- |
| D158-IFR-01 | 配置済み作業ツリーで変更検出を省く index のフラグを調べ、変更を確認できない場合は書き込み前に停止します。回帰試験は既存ファイルの内容が保たれ、他方も未配置であることを確認します。[実装と試験の確認](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7131410084) | 解消 |
| D158-IFR-02 | 固定 Proto 入力、実際に読む targets の選択、標準 import パスを照合します。最終修正で Import 自身と親の条件を扱い、設定を確定できない場合は両配置先への書き込み前に停止します。同じファイルの再評価を省き、途中の代入を保持します。既設 SDK の評価との比較と局所回帰試験を上節に記録しました。[最終修正差分](https://github.com/ibis-ssl/Duck/compare/6e20e0569dd2f7068dcd2ca7337bce76c939a146...692464cf095ee42d33ea1573f354e25458b45319)・[合格記録](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7133221975) | 解消 |
| D158-IFR-03 | 保存 index の Git 照会と必須 Proto の読み取りに失敗した場合も、対象、期待 SHA、失敗の状態、README の安全な準備手順を共通診断で示します。回帰試験は読めない入力と配置前停止を確認します。[修正確認](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7131955432) | 解消 |
| D158-IFR-04 | 対象の回帰試験、Proto 検査用の内部入口、リンク判定の説明を日本語 XML コメントで補いました。[コードと試験の確認](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7131410084) | 公開要旨の範囲で解消 |

### 同じ実装 HEAD の検証

- Linux の `pull_request` CI で、`Tracker.Tests` は **1,593件成功・4件スキップ・0件失敗** でした。Linux packet-flow ジョブはスキップです。[CI run 45404974278](https://github.com/ibis-ssl/Duck/actions/runs/45404974278)・[テストログ](https://github.com/ibis-ssl/Duck/actions/runs/45404974278/job/138483074839#step:7:1)
- クラウドの既設 SDK を使用した実装担当の局所試験は **14件成功** でした。実行内容と照合したファイルの Git blob は上節に記録しました。
- FA780 の Windows 担当は同じ実装 SHA で、今回追加した条件付き import と重複 import に関する **4ケースすべて成功** と報告しました。Docker、UDP、実機通信の受入は未実施です。[通常再レビューの検証欄](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7133221975)

親の判断は「今回新規スキル改修なし」です。繰り返し確認した IFR-02 の指摘と対応は上の通常レビュー履歴に残します。報告と追跡を記録した後は親が独立再レビューを扱います。

## D158-IFR-02 / D158-IFR-04 最終修正（2026-11-24）

### 対象

- 親の独立レビュー指摘: [PR コメント #7135362022](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7135362022)。指摘対象は `f4d9ef6d35f58c1a74ad0326c08595544fa685d7` です。
- 実装 HEAD: `5423d6d2eeb772f0f5b608bfb35d75d10299b616`（実装・焦点試験コミット）。本レポートを反映するコミットは後続するため、この報告の更新用 HEAD は PR の最終 HEAD として別途記録します。
- 対応対象は大小文字を区別しない MSBuild `Protobuf` 項目名と、欠落・内容不一致テストの日本語 XML 要約の2点です。IFR-01 / IFR-03、UDP、Windows、親 Issue #128 の実機通信は今回の対象外です。
- 実装作業前に GitHub PR #159 の HEAD、既存枝 `issue-158-startup-submodule-preflight`、fetch 済み remote 枝の HEAD がすべて `f4d9ef6d35f58c1a74ad0326c08595544fa685d7` で一致することを確認しました。`main` は統合していません。

### 変更

- `IsProtobufItem` に項目名の `OrdinalIgnoreCase` 判定を集約し、`VerifyReviewedProtoDeclarations` の宣言走査、固定入力を数える走査、`ProtoCompile` メタデータの親項目判定で共用します。大小文字の異なる `<protobuf Remove="..." />` を宣言検査で見つけ、準備処理の配置前に停止させます。
- `CaseInsensitiveProtobufRemoveStopsBeforeEitherPlacement` は固定入力を除外する大小文字違いの `Remove` を設け、両サブモジュールが未配置のまま停止することを確認します。`CaseInsensitiveProtobufDeclarationsAreEnumerated` は大小文字違いの固定入力宣言を列挙し、通常の準備入口で両方の配置・検査が完了することを確認します。
- `MissingMaterializedProtoInputIsReportedByReadOnlyCheck` の要約は、入力欠落の診断と既存 checkout / 他方の未配置状態の保護を説明します。隣接する `MaterializedProtoContentMustMatchPinnedBlobBeforeStatusCheck` は、ピン留め内容との不一致の診断とローカル変更 / 他方の未配置状態の保護を説明します。
- 変更ファイルは `scripts/start-duck-simulation.cs`、`Tracker/Tracker.Tests/LauncherProcessContractTests.cs`、本レポートです。独立最終レビュー予約パスは作成・編集していません。

### 検証

- 既設 .NET SDK 10.0.404 の小さな SDK-style プロジェクトで `Protobuf Include="Pinned.proto"` の後に `protobuf Remove="Pinned.proto"` を置き、`dotnet msbuild <fixture.csproj> -nologo -getItem:Protobuf` を実行しました。MSBuild の評価結果は `"Protobuf": []` であり、大小文字違いの Remove が固定項目を外すことを確認しました。fixture は `/tmp` に作成し、SDK/依存キャッシュを消去していません。
- 焦点試験は `Tracker.Tests` で4件成功・0件失敗・0件スキップです。対象は大小文字違いの Remove による配置前停止、大小文字違いの固定宣言列挙、Proto 入力の欠落診断、ピン留め内容との不一致診断です。実行コマンドは `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:BuildProjectReferences=false --filter 'FullyQualifiedName~CaseInsensitiveProtobufRemoveStopsBeforeEitherPlacement|FullyQualifiedName~CaseInsensitiveProtobufDeclarationsAreEnumerated|FullyQualifiedName~MissingMaterializedProtoInputIsReportedByReadOnlyCheck|FullyQualifiedName~MaterializedProtoContentMustMatchPinnedBlobBeforeStatusCheck'` です。既存の `Tracker.Tests` 依存出力を使用しました。
- `git diff --check` は成功しました。C# の両ファイルは UTF-8 BOM と LF を維持し、差分範囲外の一括変換はありません。
- 通常の `dotnet test --no-restore` は project reference のビルド中に未取得の `Microsoft.NETCore.App.Runtime.linux-x64` により `NETSDK1112` で停止しました。追加 restore や環境の永続設定変更は行わず、既存出力を使う上記の焦点試験を実行しました。テスト用の小さな fixture 修正後の最終結果は4件成功です。
- 新しい実装 HEAD に対応する CI、Windows 固有動作、UDP、Docker、シミュレーター、実機通信は未検証です。前回の CI 1,593成功・4スキップは親コメント記載の対象 HEAD に対する結果であり、この修正の CI 成功を意味しません。

### 状態

修正と局所検証を既存枝へコミットし、通常再レビューへ戻す状態です。Draft は維持し、PR コメント、PR の状態、マージは変更していません。

## 通常再レビュー合格と独立レビューへの引き継ぎ（2026-11-24）

### 対象と判定

- 対象リポジトリ: `ibis-ssl/Duck`、PR #159、ブランチ `issue-158-startup-submodule-preflight`。
- 通常再レビュー対象 HEAD: `e8f31b26e6d38234f599e7a2fe07ebc52a49f73f`。GitHub PR HEAD と同一であることを確認しました。
- 通常レビュワー証拠: [レビュー会話](https://chatgpt.com/c/6b04bff9-5150-83ec-a41a-0c07043e80a3)。通常再レビュー合格として、残件 D158-IFR-02 / 04 の処置を確認しました。
- D158-IFR-01 / 03 の既解消判定は維持します。D158-IFR-02 は共通 `IsProtobufItem` 判定、実準備入口での大小文字違い `Remove` による両配置前停止、および固定宣言の正常列挙を確認しました。D158-IFR-04 は欠落・内容不一致の2試験それぞれの日本語 XML 要約を確認しました。
- 前回の焦点試験は4件成功です。本引き継ぎでは指示により再実行していません。

### 同一 HEAD の CI

- [.NET CI run 45423165811](https://github.com/ibis-ssl/Duck/actions/runs/45423165811) は対象 HEAD `e8f31b26e6d38234f599e7a2fe07ebc52a49f73f` と一致し、run 全体は成功です。
- [`Tracker.Tests` のログ](https://github.com/ibis-ssl/Duck/actions/runs/45423165811/job/138547886129#step:7:1) は **1,595成功・4スキップ・0失敗** を報告します。
- `ASPIRE-NET-002 Linux packet flow` と `ASPIRE full-stack Linux acceptance` の2ジョブは skip です。CI 成功はこれらの通信・全体受入を実行した証拠ではありません。

### ローカル全等価ゲートと準備確認

- Duck には独立した「全等価ゲート」スクリプトや build target は見つかりません。README が通常のローカルテスト手順として示す入口は `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore` です。これは `Tracker.Tests` とその project reference をビルドし、標準の Tracker テスト群を実行します。CI workflow はこれに加えて CM4 native build/test、AutoRef game-log agent build、追加の acceptance 項目を扱います。full-stack acceptance の詳細は Docker や資源準備に依存し、今回の許可範囲外です。
- リポジトリに .NET 用の準備スクリプト、`packages.lock.json`、`RestorePackagesWithLockFile` 設定はありません。README の標準入口は restore をせず既存の restore assets を使います。したがって、許可された既存準備スクリプトまたは lockfile に固定された standard restore はありません。未固定の restore は新たな依存解決を生じ得るため実行していません。これはユーザー指示の境界による判断で、外部の拒否や承認エラーではありません。
- 通常ローカル入口を候補 HEAD `e8f31b26e6d38234f599e7a2fe07ebc52a49f73f` で一度実行しました。コマンド: `DOTNET_CLI_HOME=/workspace/.duck-env/dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 NUGET_PACKAGES=/workspace/.duck-dev/nuget /workspace/.duck-dev/dotnet/dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore`。結果は終了コード 1、`NETSDK1112: The runtime pack for Microsoft.NETCore.App.Runtime.linux-x64 was not downloaded. Try running a NuGet restore with the RuntimeIdentifier 'linux-x64'.` で、テスト実行前に停止しました。キャッシュの削除や依存変更、ロックなし restore、別ビルド経路による回避は行っていません。
- 同一 HEAD の正規 CI は通常 `Tracker.Tests` の実行範囲を置き換えられますが、スキップされた packet-flow / full-stack acceptance の範囲は補えません。以前の焦点4試験の成功は局所条件の証拠です。ローカル全等価ゲートの成功証拠ではありません。

### 引き継ぎ状態

通常再レビューは合格です。独立再レビューは親担当が扱います。この引き継ぎでは独立判定の文書を作成せず、`reports/issue-158-independent-final-review-20261124.md` にも触れていません。ブランチ変更は本レポートだけです。Draft を維持し、GitHub コメントとマージは行っていません。
