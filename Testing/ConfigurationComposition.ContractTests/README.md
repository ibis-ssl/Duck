# 設定合成の契約試験

この小さな .NET 10 コンソール試験は、[設計案](../../Configuration/Design/configuration-composition-design.md)の一部を解析済みデータで表し、試験専用の参照処理に対する入力と期待値を確認します。製品の合成器や YAML/CEL パーサーではありません。合格しても、実際の YAML、CEL、製品コードが契約を満たす証拠にはなりません。

## 実行

依存パッケージを持たず、リポジトリのソリューションや CI にも登録していません。ローカル .NET 10 SDK とターゲット参照パックが既にある場合、空のローカル NuGet ソースだけで次のように復元・実行できます。

```powershell
$emptyFeed = Join-Path $env:TEMP 'duck-composition-empty-nuget-source'
New-Item -ItemType Directory -Force $emptyFeed | Out-Null
dotnet restore Testing/ConfigurationComposition.ContractTests/ConfigurationComposition.ContractTests.csproj --source $emptyFeed
dotnet run --no-restore --project Testing/ConfigurationComposition.ContractTests/ConfigurationComposition.ContractTests.csproj
```

SDK や参照パックが不足している場合は取得せず、実行不可として記録します。

## 対応範囲と保留

| 設計契約 | この試験の範囲 |
| --- | --- |
| 複数・入れ子の include と値の出典 | 解析済みの文書グラフと値の出典を確認 |
| 子ごとの型付き引数 | 別々の include 呼び出しに渡した文字列・64 bit 整数を独立して確認し、整数引数へ文字列を渡すケースを拒否 |
| 条件あり・なし | 試験入力の真偽値で include の採否を確認。CEL の構文・評価は保留 |
| 未知キー | 構造レベルの未知キーを拒否する参照処理を確認。Duck `settings` の既知キー検証は保留 |
| 循環・衝突 | include 循環と異なる設定値を持つ同名インスタンスを拒否する参照処理を確認 |
| 失敗時の起動抑止 | 解決成功後だけ試験用の起動コールバックに進み、失敗時は呼ばない制御を確認 |
| Aspire 境界 | この試験プロジェクトに Aspire の PackageReference/ProjectReference がないことだけ確認。製品ライブラリや RuntimeHost の境界は未証明 |

この参照モデルは同名インスタンスの完全に同じ設定の重複を許容し、異なるキーを持つ部分設定の合成、全出典の保持、祖先からの上書きは扱いません。必須・既定引数の網羅、未使用だが未指定の必須引数、親から子への引数参照も試験対象外です。これは試験範囲の制約であり、製品仕様を決めるものではありません。

条件評価、YAML の読み込み、相対パスの安全性、上限、Duck の型・未知設定キー検証、明示上書き、配列の全体置換、出典の行番号、実サービスを起動しない製品統合は製品 adapter ができてからの契約です。Issue #150 は設計案の提示段階であり、本文の仕様も本人未承認です。
