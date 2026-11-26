# 条件式評価の共通契約試験（課題150）

対象は [設計PR 152の指定版](https://github.com/ibis-ssl/Duck/blob/b21c4286b7e16b6cfc0d525d657b720c786437b9/Configuration/Design/configuration-composition-design.md) と [契約試験PR 153の指定版](https://github.com/ibis-ssl/Duck/tree/dce0b005fff84f9236780320d8c1ff4748323eb4/Testing/ConfigurationComposition.ContractTests) です。

本人の暫定方針に従い、比較・論理・型付き単一値を対象に、構文解析・型検査・既存比較演算を Celly 1.2.0 へ委ねます。評価時の取消と処理量は呼出側で管理します。これは試験専用の候補であり、製品合成器ではありません。依存候補の正式採用、評価上限の最終決定、CEL 全体の仕様への適合は別の判定です。

## 共通契約と候補の差し替え

このプロジェクトは外部パッケージを持ちません。`ConditionContract.cs` の入力、期待結果、上限、取消通知は .NET の型と試験独自の列挙型だけで表します。`ConditionContractCases.Run(IConditionEvaluator)` が全21項目を実行します。

隣の `ConfigurationComposition.ConditionContracts.Celly` は候補の試験専用実行プロジェクトです。依存固有の型、構文木、標準関数、例外はアダプター内部で扱います。利用者の条件式全体を候補の評価器に渡すことはありません。通常の式エラーが論理演算で吸収されるかは、型検査済みの固定式を使って候補の評価器で確認します。

後から CelDotNet と比較する場合は、別の試験実行プロジェクトからこの共通プロジェクトを参照し、その候補用の `IConditionEvaluator` 実装を作ります。次の1行で同じ入力・期待値を実行します。

```csharp
return ConditionContractCases.Run(new YourConditionEvaluator());
```

次の制約は候補固有の型に置き換えず、共通試験で比較します。

- 構文・型検査、宣言と型に合う単一値のみ、許可外の構文の拒否。
- 真偽結果と通常式エラー・取消・上限超過の区別。取消と上限超過は論理演算で吸収されない。
- 処理量の暫定単位は、有効な利用者の式における単一値、変数参照、演算子の実行開始各1件。短絡した部分は数えない。候補内部の式木や生成された命令の数ではない。
- `StepEntered` は実行開始後に決定的に取消を通知するための試験用通知。`FaultedVariable` は妥当な束縛を読み出す時に候補の通常式エラー値へ変える試験専用の故障注入。未宣言変数や不正な束縛の検査とは別に扱う。製品の公開APIに含めない。
- 文字列は [CEL の公式仕様](https://github.com/cel-expr/cel-spec/blob/master/doc/langdef.md#ordering) に従い、Unicode の符号位置順で比較する。大文字と小文字の区別、正規化しない等値比較、不正な符号位置の拒否も確認する。

この差分では比較候補の本体調査、依存導入、実行は行っていません。共通期待値の緩和や試験の省略で比較結果を合わせないでください。

## 実行

.NET 10 SDK が既にある環境で、次のように既存のパッケージ保存先とは独立した一時ディレクトリを使います。依存は公式 NuGet の Celly 1.2.0 だけで、`packages.lock.json` に固定します。リポジトリ全体の依存宣言、全体のビルド構成、CIには登録していません。

シェルで実行する例（リポジトリの最上位から、使用可能な SDK の `dotnet` を指定してください）。

```bash
(
  trial_dir="$(mktemp -d)" || exit 1
  trap 'rm -rf "$trial_dir"' EXIT
  export DOTNET_CLI_HOME="$trial_dir/cli"
  export NUGET_PACKAGES="$trial_dir/packages"
  export NUGET_HTTP_CACHE_PATH="$trial_dir/http-cache"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1
  export DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true
  project=Testing/ConfigurationComposition.ConditionContracts.Celly/ConfigurationComposition.ConditionContracts.Celly.csproj
  dotnet restore "$project" --locked-mode --source https://api.nuget.org/v3/index.json --packages "$NUGET_PACKAGES" &&
    dotnet run --project "$project" --no-restore --disable-build-servers -p:UseSharedCompilation=false
)
```

この例では試験終了後に一時ディレクトリを片付けます。復元元は公式 NuGet のみです。SDK自体が使えなければ実行不可として扱います。

## 試験項目

| 試験 | 確認事項 |
| --- | --- |
| CC01-CC04 | 実際の式による4組合せの真偽、条件が偽なら試験用の子読込処理を呼ばない、真偽・有限の倍精度実数・整数境界 |
| CC05-CC09 | 構文・範囲外の整数、未宣言変数、型と非真偽結果、未評価分岐を含む不許可の式、不正な束縛 |
| CC10 | 文字列の符号位置順、等値、大文字と小文字の区別 |
| CC11-CC14 | 式4096バイト、構文木64要素、深さ16、文字列256バイトの上限と境界 |
| CC15-CC16 | 実行単位0・6・7の境界、短絡、予算超過の論理吸収を禁止 |
| CC17-CC18 | 評価前・途中の取消、取消後の次の実行単位停止、論理吸収を禁止 |
| CC19-CC20 | 通常式エラーの伝播、論理演算による両方向の吸収 |
| CC21 | 取消・予算状態を次の評価へ持ち越さない |

前回調査の16項目（4組合せ、真偽・倍精度実数、構文木全体・厳密な予算、符号付き値、整数桁あふれ、予算6対7、`||` と `&&` の予算吸収、短絡、事前/途中取消、許可外構文、束縛検査、深さ・構文木要素数・式長、通常式エラーの吸収、文字列の符号位置順）を、上記の実装非依存の期待値に再構成しました。追加は実際の式と読込処理の採否判定（CC02）、通常式エラーの単独伝播（CC19）、状態の独立性（CC21）、上限ちょうどと超過時の対照、両方向の取消・エラー吸収です。

## 既存10件との差

| PR 153 の契約試験 | 今回の対応 |
| --- | --- |
| 複数・入れ子の取り込みと出典 | 既存試験の対象。追加しない。 |
| 子ごとの型付き引数／明示値が既定値に優先 | 既存は子への束縛。今回のCC04、CC07、CC09は条件評価時の型、値、範囲。 |
| 条件あり・なし | 既存は解析済みの真偽値。CC01、CC02は実際の式の評価結果を使う。子を開く代わりの呼出回数の確認であり、実ファイル読込は未証明。 |
| 構造の未知キー | 既存試験の対象。CC06、CC08の未宣言変数・式構文とは別。 |
| 循環／同型値の後勝ち・採用元・順序 | 既存試験の対象。追加しない。 |
| 型の不一致 | 既存は合成時の型・種別。今回のCC07、CC09は式と束縛の型だけ。 |
| 失敗時に起動しない | 既存は試験用の起動処理。今回は条件失敗を通常の偽と区別し、試験用の読込処理へ進まないことだけ確認。 |
| Aspire参照なし | 既存は参照処理試験の確認。新規共通プロジェクトは外部パッケージなし、候補実行部は Celly のみ。製品の全依存は未証明。 |

元の10件と結果は変更せず再実行していません。

## 保留事項

試験段階の上限値と処理量の単位は仮のものです。文字列比較の費用を含めた最終上限、合成要求全体での複数条件への予算配分、入力の宣言数・総文字列量の制限は別途決定が必要です。協調取消を各実行単位の前後で確認していますが、単一の比較演算や解析・型検査の途中で即座に中断する保証と、実時間の上限は今回の証拠に含めません。

算術一般、YAML、ファイルの実体識別・相対参照、全候補・出典・機密分類、行と列を含む製品診断、実ファイルの読込抑止、サービス起動との接続、製品の公開API・配布物・推移依存、他の.NET版、並行実行、CelDotNet は未確認です。課題全体の完了とは扱いません。
