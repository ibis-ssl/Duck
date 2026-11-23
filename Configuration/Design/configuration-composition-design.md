# Duck の設定合成設計案

状態: 本人確認用の設計案。関連課題は [#150](https://github.com/ibis-ssl/Duck/issues/150)。設計の提示だけでは課題を完了にしない。製品実装、依存の導入、実サービスでの検証、main への統合はこの変更に含めない。

## 目的と前提

ROS Launch のように、共通設定、設置場所別の設定、開発用の部分構成を再帰的に取り込み、一つの確定した設定に合成する。本番の Duck 単体と開発用 AppHost が同じ合成処理を使う。通信形式、送受信先の意味、資源の所有と終了処理は既存の契約を維持する。既存の設定ファイル形式との互換は要求しない。

基準は main の `d215ec0c3b9daef978bf889c15addbe97623da59`。追跡性能改善 [#147](https://github.com/ibis-ssl/Duck/issues/147) と独立して扱う。MCAP を含む診断ログの出力・保存方式は本機能の対象外とする。

既存コードでは [RuntimeHost の起動](../../Tracker/Tracker.RuntimeHost/Program.cs)が設定を読み、[サービス登録](../../Tracker/Tracker.RuntimeHost/RuntimeHostServiceCollectionExtensions.cs)で型へ結び付け、`ValidateOnStart` を設定している。[起動スクリプト](../../scripts/start-duck-simulation.cs)は RuntimeHost または開発用転送の publish と AppHost の build を行ってから AppHost を起動する。そのため、構成の選択と検証を AppHost 内に閉じ込めない。

## 決める責務境界

同じリポジトリ内に、独立したクラスライブラリ `Configuration/Configuration.Composition/Configuration.Composition.csproj` を一つ追加する案とする。名前空間は `Configuration.Composition`。Duck、Aspire、ホストの設定型への参照を持たず、将来このプロジェクトだけを分離できるようにする。対象の .NET 版は依存の実証後に決める。

| 担当 | 責務 |
| --- | --- |
| 独立ライブラリ | ファイル読込、構造解析、相対 include、子ごとの型付き引数、条件判定、参照循環と型・形の不整合の検出、確定結果・採用値・出典・適用順の返却。 |
| Duck 側の変換・検証 | 種別の解決、Duck 固有型への変換、未知の設定キー、有効値、引数間の関係、通信上の整合の検証。既存 `ValidateOnStart` も維持する。 |
| Duck 単体の起動 | 一つの Duck 設定を選び、検証完了後にホストとサービスを起動する。Aspire は参照しない。 |
| 開発用ランチャー | 起動計画を解決・検証してから、必要な配布物の publish と AppHost の build を行う。 |
| AppHost | 検証済みの計画を既存の資源登録に変換し、開発用の外部サービスの起動、準備確認、所有、停止を管理する。 |

依存の向きは次のとおりとする。図中の Duck 側の処理と起動計画は通常の C# の型であり、Aspire の型を含めない。

```text
RuntimeHost ──> Duck 側の変換・検証 ──> Configuration.Composition
起動スクリプト ──> 同じ変換・検証を呼ぶ確認コマンド
AppHost ──> Duck 側の起動計画 ──> 既存の Aspire 資源登録
```

追加する独立ライブラリは一つにとどめる。初期の Duck 側の変換・検証と起動計画の型は、既に AppHost から参照されている `Tracker.RuntimeHost` の `Configuration` 配下に置く案とする。開発資源の論理的な設定もここで検証するが、Aspire や Docker の実行 API は持ち込まない。将来分離する場合も、この Duck 側の型を汎用ライブラリへ混ぜない。

README は将来の `Configuration/Configuration.Composition/Configuration.Composition.csproj` と同じプロジェクトルートに `Configuration/Configuration.Composition/README.md` として配置する。本段階では配置方針と検査対象を決めるだけで、プロジェクトや実装は作らない。

## 利用者が編集する設定の例

以下は採用を提案する YAML の記述例であり、現在実行できる設定やコマンドではない。`parts` と `profiles` の共通ファイルは配布側が用意し、利用者は入口のファイルだけを編集できるようにする。YAML のパーサー自体は未採用であり、先に C# の責務と動作を決める。

### 本番で Duck 単体を使う最短例

利用者は `config/field.yaml` を編集する。共通部品の既定値でよければ、必要なのは次の取り込みだけである。

```yaml
version: 1
includes:
  - file: parts/duck-runtime.yaml
```

提案する起動方法は `Tracker.RuntimeHost --config config/field.yaml --instance duck`。この経路は外部サービスを起動しない。同じ入口に複数の設定がある場合は `--instance` で選ぶが、単体用の検証では外部サービスの宣言を許さない。

上記で配布する `config/parts/duck-runtime.yaml` の具体例を示す。名前、出力先、識別値だけを変更して同じ部品を再利用できる。

```yaml
version: 1
parameters:
  instance: { type: string, default: duck }
  outputPort: { type: int, default: 11010 }
  sourceName: { type: string, default: ibis }
  uuid: { type: string, default: ibis }
instances:
  - name: { parameter: instance }
    kind: duck.runtime
    settings:
      VisionReceiver:
        MulticastAddress: 224.5.23.2
        Port: 10020
      RuntimeHost:
        OperationLoopIntervalMilliseconds: 16
      Tracker:
        Enabled: true
        PublishUdp: true
        SourceName: { parameter: sourceName }
        Uuid: { parameter: uuid }
        ActiveProfileName: sim
        Profiles:
          sim:
            Publish:
              MulticastAddress: 224.5.23.2
              Port: { parameter: outputPort }
            Engine:
              ReorderWindowNs: 10000000
```

これは基準版の `sim` と同じ 10020 の受信、11010 の出力、10 ms の待ち幅を明示した例であり、すべての本番現場の既定値を定めるものではない。その他の Duck の既定値は既存の設定型から補い、その出典を Duck 側の確定結果へ付加する。現場で使う入出力先は部品または場所別の部分設定で指定する。

### AppHost を使う開発の最短例

利用者は `config/local.yaml` を編集する。

```yaml
version: 1
includes:
  - file: profiles/development-base.yaml
```

配布する `config/profiles/development-base.yaml` は次の二つの部分構成を取り込む。

```yaml
version: 1
includes:
  - file: ../parts/duck-runtime.yaml
  - file: ../parts/development-services.yaml
```

`config/parts/development-services.yaml` は開発資源の選択だけを表す。

```yaml
version: 1
instances:
  - name: simulation
    kind: duck.development.base
    settings:
      RuntimeInstance: duck
      RuntimePlacement: windows
      Lifetime: session
```

`duck.development.base` は既存の基本開発構成に対応する Duck 側の種別である。既存のシミュレーター、Game Controller、Crane、選択配置で必要な開発用転送を既存の登録処理へ渡す。Duck 本体は `RuntimeInstance` の参照先を使い、二重に登録しない。任意の実行ファイル、シェル、イメージを設定から直接実行する仕組みは追加しない。固定イメージと資源管理の規則も既存処理に従う。

提案する利用コマンドは `dotnet run --file scripts/start-duck-simulation.cs -- --config config/local.yaml`。既存のスクリプトを入口に保ち、内部で事前検証を行ってから AppHost を起動する。本番の単体起動はこのスクリプトを必要としない。

### 親が複数の子を取り込み、条件で部分構成を選ぶ例

`config/comparison.yaml` の例を示す。子の相対パスは、その include を記述したファイルのディレクトリから解決する。

```yaml
version: 1
parameters:
  cameraCount: { type: int, default: 4 }
  mode: { type: string, default: comparison }
includes:
  - file: profiles/development-base.yaml
  - file: sites/field-a.yaml
  - file: parts/comparison-services.yaml
    when: 'cameraCount >= 4 && mode == "comparison"'
    args:
      cameraCount: 8
```

`config/sites/field-a.yaml` は既存の名前に部分設定を足す例である。選択した include は記述順に深さ優先で適用するため、後続の比較用構成でも `duck` に同じ項目があれば、後の有効値が採用される。

```yaml
version: 1
instances:
  - name: duck
    kind: duck.runtime
    settings:
      VisionReceiver:
        InterfaceAddress: 192.0.2.10
```

このアドレスは記述用の例であり、利用時は実際の受信インターフェイスを指定する。次の `comparison-services.yaml` でも `duck` の同じ設定項目を指定し、後から適用された `192.0.2.11` が採用される例にする。実運用の値を推奨するものではない。`comparison-services.yaml` は引数を自分で宣言する。

```yaml
version: 1
parameters:
  cameraCount: { type: int }
instances:
  - name: comparison
    kind: duck.development.comparison
    settings:
      RuntimeInstance: duck
      CameraCount: { parameter: cameraCount }
  - name: duck
    kind: duck.runtime
    settings:
      VisionReceiver:
        InterfaceAddress: 192.0.2.11
```

比較資源の識別、受信、Duck の出力を購読する意味は[既存の比較設計](../../Tracker/Design/Testing/tracker-comparison-debug-design.md)を維持する。条件が偽なら比較資源は計画に含まれない。比較用の引数は Duck の追跡処理の新しい設定項目には変換しない。

### 同種サービスを別名で二つ起動する例

同じ部品を違う引数で二度取り込むことは循環ではない。明示した引数は呼び出しごとの既定値に優先し、次の入口は二つの Duck 設定を作る。

```yaml
version: 1
includes:
  - file: parts/duck-runtime.yaml
    args:
      instance: duck-main
      outputPort: 11010
      sourceName: ibis-main
      uuid: ibis-main
  - file: parts/duck-runtime.yaml
    args:
      instance: duck-candidate
      outputPort: 11011
      sourceName: ibis-candidate
      uuid: ibis-candidate
```

本番では同じ確定設定を指定し、`--instance duck-main` と `--instance duck-candidate` を付けた二つの単体プロセスを利用者の実行環境が起動する。AppHost の開発ではこの二つを同じ計画から二資源として登録し、基本開発構成が必要なときだけ前項の外部資源を加える。AppHost で使う資源名はこの名前から決定的に作り、変換後の名前の衝突も Duck 側で拒否する。

上の例では出力先とパケット内の識別値も分けた。別名だけでは通信競合は解消しない。複数の送信者が同じ受信先を共有できる既存の通信契約と、排他的な待受先は区別し、Duck 側で判定する。複数受信の成立を本設計書だけで実証したとは扱わない。

## 最小限の構造と合成規則

独自の条件式言語や汎用の起動フレームワークは作らない。構造は `version`、`parameters`、`includes`、`instances` に限定する。式は `when` の既存 CEL だけに使う。引数参照は補間文字列ではなく `{ parameter: name }` という単一値への参照であり、式や C# の実行ではない。

| 項目 | 採用を提案する規則 |
| --- | --- |
| 型付き引数 | 初版は `bool`、64 bit の `int`、有限の `double`、`string`。既定値は子の宣言に置き、呼出しの明示引数があればそちらを採用する。暗黙の型変換はしない。 |
| 子への受渡し | `args` は子の宣言済み引数だけを受け付け、型を厳密に照合する。既定値を持たない引数は呼出し時に必須。子ごとに独立して束縛し、親や兄弟から変数を暗黙に継承しない。 |
| 値位置の参照 | `instances.name` と `settings` 内の値で `{ parameter: name }` を解決する。この形は参照専用として予約する。キー名、include のパス、`kind` の動的生成、文字列埋め込みは認めない。 |
| 条件 | 親の宣言済み引数を CEL に渡す。型検査で結果が真偽値になることを要求し、真だけを取り込む。構文誤り、未知変数、型違い、評価エラー、上限超過はエラーにする。偽に読み替えない。 |
| 読込順 | 選択した include を記述順に深さ優先で展開し、その後にファイル自身の `instances` を適用する。順序が値の優先順位となり、後の有効値を採用する。 |
| 合成 | 同名・同種別の設定を項目単位で再帰的に統合する。同じ型・同じ形の異なる値はエラーにせず、後の指定値を採用する。同じ値も適用順・出典の履歴に残す。 |
| 配列 | 配列は全体置換とする。追記、削除、添字単位の合成は初版に含めない。配列とスカラーなど形が異なる指定はエラー。 |
| 型・種別 | 型の変更、写像・配列・単一値の形の変更、同名で異なる `kind` の指定はエラー。 |
| 未知キー | 文書構造、引数宣言、include、instance の未知キーはライブラリが拒否。`settings` 内の未知キーと必要項目は Duck 側が種別ごとの型で起動前に拒否する。辞書として宣言した領域だけは自由なキーを許す。 |
| 名前とキー | 構造キー、引数、名前は大文字小文字を区別する。Duck の設定キーは既存の正しい綴りに固定し、大文字小文字だけが異なる二重指定も変換前に拒否する。 |

構造解析では一つの YAML 文書だけを受理し、同じ写像内の重複キーを同値でも拒否する。YAML の別名、アンカー、合成キー、任意タグは初版では無効にする。文字列・真偽・数値・空値の対応規則を固定し、日付や `yes` などを暗黙の別型にしない。複雑な値は部品と明示引数で再利用する。

## 読込と循環、評価上限

入口は利用者が指定した絶対位置へ正規化する。相対 include は記述元を基準に解決し、呼出元の作業ディレクトリには依存させない。初版は手元のファイルだけを扱い、URL、glob、環境変数展開、動的なパス式は受け付けない。入口と配布部品を含む許可ディレクトリを呼出側で指定し、その外への参照を拒否する。

循環は、現在の取り込み経路上にあるファイルの実体の同一性で検出する。引数が異なっていても A → B → A は拒否し、循環全体と各 include の行を示す。同じ部品の兄弟取り込みは許容する。シンボリックリンクを解決できない場合は、安全に同一性を決められない参照として拒否する。Windows の大文字小文字や別名パスも考慮する。

偽の include は子を開かず、親側のパス形式と条件式だけを検査し、除外理由を出典へ残す。子の引数名・内容は選択時に検証する。未選択の構成は別の引数組合せの検証試験で覆う。

合成要求には上限を持たせる。初版の提案値は、深さ32、展開した include 256件、ファイル単体1 MiB、合計8 MiB、式4 KiB とする。構造の深さと値の総数にも上限を設ける。CEL には実装が提供する評価予算と取消を使い、上限超過は設定エラーにする。具体値と対応 API は依存の実証で確定する。

## C# の構造案

以下は責務を示す公開境界の案であり、未採用のライブラリ API に依存する実装コードではない。

```csharp
namespace Configuration.Composition;

public sealed record CompositionRequest(
    string EntryFile,
    IReadOnlyDictionary<string, ScalarValue> Arguments,
    CompositionLimits Limits,
    IReadOnlyList<string> AllowedRoots);

public interface IConfigurationComposer
{
    ValueTask<CompositionResult> ComposeAsync(
        CompositionRequest request, CancellationToken cancellationToken);
}

public sealed record CompositionResult(
    ResolvedConfiguration? Configuration,
    IReadOnlyList<CompositionDiagnostic> Diagnostics);

public sealed record ResolvedConfiguration(
    IReadOnlyDictionary<string, ResolvedInstance> Instances,
    IReadOnlyDictionary<string, ResolvedValueTrace> Values,
    IReadOnlyList<IncludeDecision> Includes,
    IReadOnlyList<SourceFileFingerprint> Sources);

public sealed record ResolvedValueTrace(
    ConfigValue AdoptedValue,
    ValueOrigin AdoptedFrom,
    IReadOnlyList<ValueCandidate> Candidates,
    int ApplicationOrder);
```

`ScalarValue`、`ConfigValue` は型を保持した値、`ResolvedInstance` は名前・種別・設定値を持つ不変のデータとする。各設定項目の `ResolvedValueTrace` は採用値、採用元、適用順、全候補とその出典を構造化して保持する。選択・除外された include と条件も同じ結果に残す。エラーがある場合、実行可能な `Configuration` は返さず、診断だけを返す。

内部は次の小さな処理に分ける。初版でプラグインの動的読込、独自の DI 機構、式解析器は作らない。YAML と CEL の外部型は内部の変換層に閉じ込める。

| 処理 | 入出力と役割 |
| --- | --- |
| 読込・解析 | 読み取ったバイト列から位置付きの文書モデルを作り、ファイルのハッシュ値を記録する。位置を失う前に重複キーと未知構造を検出する。 |
| 引数の束縛 | 宣言と呼出値を照合し、子ごとに不変の引数辞書を作る。 |
| 条件の評価 | 宣言済みの単一値だけを渡し、型検査済みの真偽値を得る。C# オブジェクト、任意関数、リフレクションを公開しない。 |
| 取り込みの展開 | 経路と上限を管理し、循環検出と子の解決を行う。同一バイト列の解析は再利用しても、別引数での解決結果は混同しない。 |
| 値の合成 | 型付き候補を適用順に処理し、後の同型・同形の値、採用値の履歴と出典を不変の結果にする。不正な型・形や種別は診断にする。 |

Duck 側は `ResolvedConfiguration` を `DuckConfigurationValidator` に渡す。既存の Options の既定値、型への変換、`RuntimeTrackerConfigurationResolver` と共通の有効値検証を通し、Aspire 型を含まない `ValidatedDuckPlan` を作る。初版の種別表は `duck.runtime`、`duck.development.base`、`duck.development.comparison` の明示登録とし、未登録の種別は拒否する。外部サービスの網羅的な定義方式は追加しない。

既存の `ValidateOnStart` をライブラリへ移さず、Duck 側で同じ検証関数を事前検証にも使う。`IOptions<T>.Value` を起動前に全件評価し、既存の遅延解決で初めて見つかる不正も計画の段階で検出する。検証のために UDP ソケットやサービスを作らない。

## 単体と AppHost の起動順

### 本番の単体

1. `--config`、`--instance`、入口引数だけを先に解析する。
2. 独立ライブラリで合成し、単体用として外部サービスの有無、全 Duck 設定の未知キー・型・有効値・識別の整合を検証する。
3. 指定した一つの設定を型へ変換する。確定済みの設定を使ってホストを作り、既存 `ValidateOnStart` も通す。
4. すべて成功してからサービスを開始する。設定エラー時は非0で終了し、サービスを一つも開始しない。

### 開発のランチャーと AppHost

1. ランチャーが共通の確認コマンドで開発用計画を解決し、全資源の設定と関係を検証する。AppHost の作成、Docker の操作、サービスの登録・起動はまだ行わない。
2. 検証済みの計画から必要な publish 対象、AppHost の build 条件、資源と設定の対応を決める。任意のコマンド文字列ではなく、既存の許可済み対象に対応させる。
3. 必要な publish と build を行う。失敗したら AppHost を起動しない。
4. 検証済みの設定と計画を、実行ごとに別の読み取り専用入力として AppHost に渡す。AppHost は形式版・ハッシュ値・対象名・設定を再検証してから資源を登録する。
5. 起動後の所有、準備確認、終了、後片付けは既存 AppHost の責務とする。

確認コマンドは既存 `Tracker.RuntimeHost` に副作用のない `config` サブコマンドを追加する案とする。これによりランチャーと AppHost が同じ Duck 側の型と検証を使える。コマンドが未配布の開発環境では、この確認用の RuntimeHost と独立ライブラリを先にコンパイルする準備は必要だが、選択資源の publish と AppHost の build・実行は計画検証後とする。AppHost をビルドしないと設定検証できない依存は作らない。

設定ファイルの読込後は同じバイト列を使って解析・合成する。publish/build の途中で元ファイルが変わった場合は、開始前のハッシュ値照合で失敗させ、再確認を求める。AppHost や子プロセスが入口を別に読み直して異なる条件分岐を選ぶことはない。実行ごとの識別子など動的な運用情報は別の型付き入力にし、利用者の設定値を暗黙に上書きしない。

既存の環境変数やコマンドラインの一般的な設定供給で、確定した Options を後から変えない。入口の引数は明示された `--arg` だけで渡し、Duck の確定設定は専用の設定供給経路から渡す。従来形式の移行は別作業とし、通信内容と運用上の契約を変える理由にはしない。

## 出典と確認コマンド案

各設定値の出典情報には採用値、採用元、適用順、全候補と各候補のファイル位置・include 経路・引数の出所を保持する。include の条件式と評価結果も構造化結果に含める。Duck の型の既定値を補った場合は、設定型と項目名を既定値の出典として付ける。構造解析だけで行・列を失わないことをパーサー選定条件にする。

確認コマンド案は次のとおり。いずれもサービスを開始しない。`check` は合成だけでなく Duck 側の検証も必須とし、成功0、設定・検証エラー2、読込などの実行エラー1を返す。

```powershell
# 本番単体の検証
Tracker.RuntimeHost config check --config config/field.yaml --target standalone

# 開発の選択条件を変えて全体検証
Tracker.RuntimeHost config check --config config/comparison.yaml --target development --arg cameraCount=4 --arg mode=comparison

# 確定値と出典を確認
Tracker.RuntimeHost config explain --config config/field.yaml --target standalone --path /instances/duck/settings/VisionReceiver/Port

# ランチャー内部で検証済み計画を生成する。設定の原文と出典も束ねる。
Tracker.RuntimeHost config plan --config config/local.yaml --target development --output artifacts/launch-<run-id>.json
```

`config explain` は起動せずに全項目の採用値、全候補、適用順、出典を確認できる。標準の表示は全項目を示すが、秘密値は伏せ、出典は残す。実際の行番号は解析結果から生成する。

`check`、`explain`、`plan` は同じ検証経路を使う。型・形の不整合や Duck の有効値エラーがあるときに、不完全な設定を成功出力しない。通常の起動ログは既存と同程度に保ち、全項目や途中の候補を自動列挙しない。採用値と出典は構造化結果に保持し、必要な場合だけ起動側が既存の構造化ログ経路へ渡す。汎用ライブラリは Console やログ出力先を呼ばず、独自トレーサーも設けない。診断表示とログでは秘密値を伏せる。

## 採用前に実証する依存

CEL は [公式仕様](https://github.com/cel-expr/cel-spec/blob/master/doc/langdef.md)を基準とする。`cameraCount >= 4 && mode == "comparison"` のような自然な条件式をそのまま扱う。C# スクリプト、Roslyn、任意アセンブリの実行は採用しない。以下は公式リポジトリの説明を読んだ候補であり、この作業で導入・実行・性能比較はしていない。

| 候補 | 読み取れた特徴と採用前の確認 |
| --- | --- |
| [Celly](https://github.com/bsidio/celly) | 管理された C# 実装で、変数宣言、型検査、辞書の入力、評価予算と取消、構文木の検査を提供すると説明されている。候補の第一案。対象の .NET 版、配布物の依存、公開 API、実際の予算の効き方と公式仕様との一致を実証する。 |
| [CelDotNet](https://github.com/hbjydev/celdotnet) | 型検査と .NET の式木への変換を提供する候補。C# スクリプトの実行とは異なるが、公開する型や関数の制限、評価予算、仕様どおりのエラー処理を実証する。式木を使う必要がないため、第一案の不足時に比較する。 |
| [YamlDotNet](https://github.com/aaubry/YamlDotNet) | 低水準の解析と文書モデルを提供する候補。行・列の保持、重複キー検出、YAML 1.2 相当の単一値の解釈、別名等の無効化、深さとサイズの制限を実証する。 |

実装に進む前の小さな検証で、候補の版とライセンス、推移的な依存を記録し、次を確認する。公式説明の完全性や性能の主張を、そのまま Duck の検証結果にはしない。

- 宣言した `int`、`string` に対する上記の式で、境界値3・4、二つの mode の真偽が一致する。
- 未知変数、文字列と数値の比較、真偽値以外の結果、桁あふれ、評価エラーを確実に拒否する。CEL の論理演算で扱われるエラーについても公式仕様と照合する。
- ホストのメソッド、ファイル、通信、時刻、環境変数へ到達できない。初版は比較・論理・算術と単一値に限定し、不要な拡張とマクロを無効化または構文木の検査で拒否する。独自の式構文は追加しない。
- 大きな式、深い式、大きな文書が予算内で終了し、取消と上限超過を区別して返す。
- YAML の位置情報、文字列の引用、同じファイルの二回利用、Windows の別名パスでも出典と循環検出が安定する。

満たせない候補は差し替え、必要な契約を満たす既存実装がなければ依存選定を未解決として本人へ戻す。独自 CEL の実装で埋め合わせない。

## 実装時の検証条件

本変更では製品コードやテストを追加しない。実装時は対象プロジェクトの手順に従い、次の動作を仕様から先に固定する。

| 対象 | 必須の確認 |
| --- | --- |
| 合成 | 再帰 include、作業ディレクトリに依存しない相対参照、必須・既定・未知・型違い引数、兄弟の引数分離、偽の include、同じ子の別引数利用。 |
| 値と出典 | 循環の全経路、記述順の後勝ち、同値を含む全候補と採用元、配列全体の置換、型・形の不一致、未知キー、大文字小文字の二重指定。 |
| 起動しない失敗 | 読込失敗、条件エラー、循環、衝突、Duck の型・未知キー・有効値・資源参照の不正で、ホストの開始・外部プロセス開始・Docker 操作・資源登録をすべて0回にする。偽の起動先を数えて確認する。 |
| ランチャー | 計画が不正なら対象資源の publish/build も0回。正常な計画でだけ必要な配布物を選ぶ。準備の build 失敗や設定変更後に AppHost を起動しない。 |
| 経路の一致 | 同じ部品と引数から単体と開発計画の Duck の有効値・型・出典が一致する。開発固有の配置情報だけを外側で付加する。 |
| 境界 | 独立ライブラリが Duck と Aspire を参照しないこと、RuntimeHost のプロジェクト・推移的な参照・配布ファイルに Aspire が含まれないことを確認する。単体の確認・起動試験は AppHost や Docker を使わず行う。 |
| 既存の意味 | 通信形式、出力先、パケット内の識別、所有、準備確認、終了処理の既存回帰試験を維持する。実サービスでの通信成立は後続の受入検証で扱う。 |

## 未決事項と後続作業

合成規則、別名での複数起動、条件付き include、ログ粒度、配列の全体置換は本人の指示に基づく設計案として記載した。製品実装や設定形式の最終採用は、この Draft PR では扱わない。独立したクラスライブラリと契約試験の境界を保ち、依存の技術項目を先に実証する。

依存の具体的な版、対象の .NET 版、上限の最終値、Windows のファイル実体の識別手段は技術検証待ちとする。NuGet 化の境界は、ライブラリ本体、公開 C# API、宣言・推移依存の範囲で定義する。将来の検証ではローカル consumer を作り、Duck と Aspire を参照せずに参照・ビルドできること、公開 API と依存グラフおよびパッケージ内容を確認する。現時点では実プロジェクトやパッケージを作らない。これらの未実証を採用済みとは扱わない。設計の確認後に、依存の小さな検証、独立ライブラリ、Duck 側の型への変換、単体の起動、ランチャーと AppHost の順に作業を分ける。#150 は設計の提示段階として継続する。
