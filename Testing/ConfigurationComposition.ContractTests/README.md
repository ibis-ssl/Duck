# 設定合成の契約試験

この小さな .NET 10 のコマンド行試験プログラムは、[設計案](../../Configuration/Design/configuration-composition-design.md)の一部を解析済みデータで表し、試験専用の参照処理に対する入力と期待値を確認します。製品の合成器や設定形式・条件式の解析器ではありません。合格しても、実際の設定形式、条件式、製品実装が契約を満たす証拠にはなりません。

## 実行

依存パッケージを持たず、リポジトリ全体のビルド構成や CI にも登録していません。開発環境に .NET 10 SDK と対象の参照用ファイルが既にある場合、空の NuGet 保存先だけを指定して次のように復元・実行できます。

```powershell
$emptyFeed = Join-Path $env:TEMP 'duck-composition-empty-nuget-source'
New-Item -ItemType Directory -Force $emptyFeed | Out-Null
dotnet restore Testing/ConfigurationComposition.ContractTests/ConfigurationComposition.ContractTests.csproj --source $emptyFeed
dotnet run --no-restore --project Testing/ConfigurationComposition.ContractTests/ConfigurationComposition.ContractTests.csproj
```

SDK や参照用ファイルが不足している場合は取得せず、実行不可として記録します。

## 対応範囲と保留

| 設計契約 | この試験の範囲 |
| --- | --- |
| 複数・入れ子の取り込みと値の出典 | 解析済み文書のつながりに対する参照処理の採用元を確認 |
| 子ごとの型付き引数 | 別々の呼出しに渡した文字列・64個の二進桁を使う整数を独立して確認し、型違いを拒否 |
| 既定値 | 呼出しで明示した値が子の既定値より優先することを確認 |
| 条件あり・なし | 試験入力の真偽値で取り込みの採否を確認。条件式の構文・評価は保留 |
| 未知キー | 構造内の未知キーを拒否。Duck の設定キー検証は保留 |
| 後勝ちと出典・順序 | 同型値は記述順で後の値が採用され、採用元と適用順が残ることを確認。値の候補を製品 API で返す契約自体は未実装 |
| 型・種別の不一致 | 同じ項目の型違いと同名の種別違いを拒否。配列・写像など異なる値の形の試験は保留 |
| 循環 | 取り込みの循環を拒否 |
| 失敗時の起動抑止 | 解決成功後だけ試験用の起動処理に進み、種別の不一致時は呼ばないことを確認 |
| Aspire 境界 | この試験プロジェクトに Aspire の `<PackageReference>` / `<ProjectReference>` がないことだけ確認。製品ライブラリや RuntimeHost の境界は未証明 |

この10件版では、同型の異値を拒否せず、後の値と採用元・適用順が残ることを成功条件とします。拒否の試験対象は型・種別の不一致です。

この参照処理は設計契約を確認するためだけの模擬実装です。未知キー、条件、循環、引数、型違い、採用値、採用元、適用順、および起動抑止はこの処理で実行できます。実際の設定形式や条件式の読み込み、相対パスの安全性、すべての候補・位置情報の製品 API、Duck の型・未知設定キー・有効値検証、サービス起動前の製品統合、パッケージ境界は製品実装がないため実行できません。

課題 #150 は設計案の提示段階です。この試験は製品動作を証明せず、CI に失敗を持ち込みません。
