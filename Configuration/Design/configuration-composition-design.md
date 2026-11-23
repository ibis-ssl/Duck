# Duck の設定合成設計案

状態: 本人確認用の設計案。関連課題は [#150](https://github.com/ibis-ssl/Duck/issues/150)。設計の提示だけでは課題を完了にしない。製品実装、依存の導入、実サービスでの検証、`main` への統合はこの変更に含めない。

## 目的と前提

`ROS Launch` のように、共通設定、設置場所別の設定、開発用の部分構成を再帰的に取り込み、一つの確定した設定に合成する。本番の Duck 単体と開発用 AppHost が同じ合成処理を使う。通信形式、送受信先の意味、資源の所有と終了処理は既存の契約を維持する。既存の設定ファイル形式との互換は要求しない。

今回の設計対象基準は `main` の `7c8e860899f08acd4b02a0cdf1b44eba180a9f21`。初版を作成した時点の旧基準 `d215ec0c3b9daef978bf889c15addbe97623da59` は設計履歴の参照として残し、今回の対象基準には用いない。追跡性能改善 [#147](https://github.com/ibis-ssl/Duck/issues/147) と独立して扱う。診断ログの出力・保存方式は本機能の対象外とする。

既存実装では [RuntimeHost の起動](../../Tracker/Tracker.RuntimeHost/Program.cs)が設定を読み、[サービス登録](../../Tracker/Tracker.RuntimeHost/RuntimeHostServiceCollectionExtensions.cs)で型へ結び付け、`ValidateOnStart` を設定している。[起動手順](../../scripts/start-duck-simulation.cs)は RuntimeHost または開発用転送の配布物と AppHost を用意してから AppHost を起動する。そのため、構成の選択と検証を AppHost 内に閉じ込めない。

## 決める責務境界

同じリポジトリ内に、独立したクラスライブラリ `Configuration/Configuration.Composition/Configuration.Composition.csproj` を一つ追加する案とする。名前空間は `Configuration.Composition`。Duck、Aspire、ホストの設定型への参照を持たず、将来このプロジェクトだけを分離できるようにする。対象の .NET 版は依存の実証後に決める。

| 担当 | 責務 |
| --- | --- |
| 独立ライブラリ | ファイル読込、構造解析、相対設定の取り込み、子ごとの型付き引数、条件判定、参照循環と型・形の不整合の検出、確定結果・採用値・出典・適用順の返却。 |
| Duck 側の変換・検証 | 種別の解決、Duck 固有型への変換、未知の設定キー、有効値、引数間の関係、通信上の整合の検証。既存 `ValidateOnStart` も維持する。 |
| Duck 単体の起動 | 一つの Duck 設定を選び、検証完了後にホストとサービスを起動する。Aspire は参照しない。 |
| 開発用起動手順 | 起動計画を解決・検証してから、必要な配布物を作り、AppHost を構築する。 |
| AppHost | 検証済みの計画を既存の資源登録に変換し、開発用の外部サービスの起動、準備確認、所有、停止を管理する。 |

依存の向きは次のとおりとする。図中の Duck 側の処理と起動計画は通常の C# の型であり、Aspire の型を含めない。

```text
RuntimeHost ──> Duck 側の変換・検証 ──> Configuration.Composition
起動手順 ──> 同じ変換・検証を呼ぶ確認コマンド
AppHost ──> Duck 側の起動計画 ──> 既存の Aspire 資源登録
```

追加する独立ライブラリは一つにとどめる。初期の Duck 側の変換・検証と起動計画の型は、既に AppHost から参照されている `Tracker.RuntimeHost` の `Configuration` 配下に置く案とする。開発資源の論理的な設定もここで検証するが、Aspire や Docker の実行 API は持ち込まない。将来分離する場合も、この Duck 側の型を汎用ライブラリへ混ぜない。

README は将来の `Configuration/Configuration.Composition/Configuration.Composition.csproj` と同じプロジェクト最上位に `Configuration/Configuration.Composition/README.md` として配置する。本段階では配置方針と検査対象を決めるだけで、プロジェクトや実装は作らない。

## 利用者が編集する設定の例

以下は採用を提案する `YAML` の記述例であり、現在実行できる設定やコマンドではない。`parts` と `profiles` の共通ファイルは配布側が用意し、利用者は入口のファイルだけを編集できるようにする。読み取り器自体は未採用であり、先に C# の責務と動作を決める。

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

`duck.development.base` は既存の基本開発構成に対応する Duck 側の種別である。既存の模擬環境、Game Controller、Crane、選択配置で必要な開発用転送を既存の登録処理へ渡す。Duck 本体は `RuntimeInstance` の参照先を使い、二重に登録しない。任意の実行ファイル、シェル、イメージを設定から直接実行する仕組みは追加しない。固定イメージと資源管理の規則も既存処理に従う。

提案する利用コマンドは `dotnet run --file scripts/start-duck-simulation.cs -- --config config/local.yaml`。既存の起動手順を入口に保ち、内部で事前検証を行ってから AppHost を起動する。本番の単体起動はこの手順を必要としない。

### 親が複数の子を取り込み、条件で部分構成を選ぶ例

`config/comparison.yaml` の例を示す。子の相対位置は、その取り込みを記述したファイルのディレクトリから解決する。

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
      cameraCount: { parameter: cameraCount }
```

`config/sites/field-a.yaml` は既存の名前に部分設定を足す例である。選択した取り込みは記述順に深さ優先で適用するため、後続の比較用構成でも `duck` に同じ項目があれば、後の有効値が採用される。

```yaml
version: 1
instances:
  - name: duck
    kind: duck.runtime
    settings:
      VisionReceiver:
        InterfaceAddress: 192.0.2.10
```

このアドレスは記述用の例であり、利用時は実際にデータを受信する装置を指定する。次の `comparison-services.yaml` でも `duck` の同じ設定項目を指定し、後から適用された `192.0.2.11` が採用される例にする。実運用の値を推奨するものではない。`comparison-services.yaml` は引数を自分で宣言する。

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

この例では入口の `cameraCount` は整数 4 で、`CEL` の条件もこの値を評価する。`args` で子の `cameraCount` に `{ parameter: cameraCount }` を明記しているため、子にも整数 4 が渡る。暗黙の継承ではない。出典には入口の `cameraCount` 宣言と取り込みの引数束縛位置を記録する。比較資源の識別、受信、Duck の出力を購読する意味は[既存の比較設計](../../Tracker/Design/Testing/tracker-comparison-debug-design.md)を維持する。条件が偽なら比較資源は計画に含まれない。比較用の引数は Duck の追跡処理の新しい設定項目には変換しない。

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

独自の条件式言語や汎用の起動フレームワークは作らない。構造は `version`、`parameters`、`includes`、`instances` に限定する。式は `when` の既存 `CEL` だけに使う。引数参照は補間文字列ではなく `{ parameter: name }` という単一値への参照であり、式や C# の実行ではない。

| 項目 | 採用を提案する規則 |
| --- | --- |
| 型付き引数 | 初版は `bool`、64桁幅の `int`、有限の `double`、`string`。既定値は子の宣言に置き、呼出しの明示引数があればそちらを採用する。暗黙の型変換はしない。 |
| 子への受渡し | `includes[].args` の値に `{ parameter: name }` を使う場合、参照先はその取り込みを記述した直近の親が宣言した引数に限る。まず親の宣言と値を解決し、その後で子の宣言済み引数の有無と型を照合する。祖先や兄弟の引数を暗黙に探さない。既定値を持たない子の引数は呼出し時に必須。 |
| 値位置の参照 | `instances.name` と `settings` 内の値で `{ parameter: name }` を解決する。この形は参照専用として予約する。キー名、`includes` の位置、`kind` の動的生成、文字列埋め込みは認めない。 |
| 条件 | 親の宣言済み引数を `CEL` に渡す。型検査で結果が真偽値になることを要求し、真だけを取り込む。構文誤り、未知変数、型違い、評価エラー、上限超過はエラーにする。偽に読み替えない。 |
| 読込順 | 選択した取り込みを記述順に深さ優先で展開し、その後にファイル自身の `instances` を適用する。順序が値の優先順位となり、後の有効値を採用する。 |
| 合成 | 同名・同種別の設定を項目単位で再帰的に統合する。同じ型・同じ形の異なる値はエラーにせず、後の指定値を採用する。同じ値も適用順・出典の履歴に残す。 |
| 配列 | 配列は全体置換とする。追記、削除、添字単位の合成は初版に含めない。配列と単一値など形が異なる指定はエラー。 |
| 型・種別 | 型の変更、写像・配列・単一値の形の変更、同名で異なる `kind` の指定はエラー。 |
| 未知キー | 文書構造、引数宣言、`includes`、`instances` の未知キーはライブラリが拒否。`settings` 内の未知キーと必要項目は Duck 側が種別ごとの型で起動前に拒否する。辞書として宣言した領域だけは自由なキーを許す。 |
| 名前とキー | 構造キー、引数、名前は大文字小文字を区別する。Duck の設定キーは既存の正しい綴りに固定し、大文字小文字だけが異なる二重指定も変換前に拒否する。 |

構造解析では一つの `YAML` 文書だけを受理し、同じ写像内の重複キーを同値でも拒否する。別名参照、参照先の印、合成キー、独自の型指定は初版では無効にする。文字列・真偽・数値・空値の対応規則を固定し、日付や `yes` などを暗黙の別型にしない。複雑な値は部品と明示引数で再利用する。解析、条件式の構文検査、引数参照、型検査で機密項目を分類する前に失敗した場合、診断に含めるのはエラー種別と、取得できるときの行・列番号だけとする。ファイル名、キー、入力原文の抜粋、条件式に含まれる文字列値はエラー、例外、ログ、標準出力へ含めない。安全な行・列番号が取得できなければ、エラー種別だけを示す。

## 読込と循環、評価上限

入口は利用者が指定した絶対位置へ正規化する。相対取り込みは記述元を基準に解決し、呼出元の作業ディレクトリには依存させない。初版は手元のファイルだけを扱い、URL、`glob`、環境変数展開、動的な位置式は受け付けない。入口と配布部品を含む許可ディレクトリを呼出側で指定し、その外への参照を拒否する。

循環は、現在の取り込み経路上にあるファイルの実体の同一性で検出する。引数が異なっていても A → B → A は拒否し、循環全体と各取り込み位置を示す。同じ部品の兄弟取り込みは許容する。シンボリックリンクを解決できない場合は、安全に同一性を決められない参照として拒否する。Windows の大文字小文字や別名の位置も考慮する。

条件が偽の取り込みは子を開かず、親側の位置形式と条件式だけを検査し、除外理由を出典へ残す。子の引数名・内容は選択時に検証する。未選択の構成は別の引数組合せの検証試験で覆う。

合成要求には上限を持たせる。初版の提案値は、深さ32、展開した取り込み256件、ファイル単体1,048,576バイト、合計8,388,608バイト、式4,096バイトとする。構造の深さと値の総数にも上限を設ける。`CEL` には実装が提供する評価予算と取消を使い、上限超過は設定エラーにする。具体値と対応 API は依存の実証で確定する。

## C# の構造案

以下は責務を示す公開境界の案であり、未採用のライブラリ API に依存する実装例ではない。

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

`ScalarValue`、`ConfigValue` は型を保持した値、`ResolvedInstance` は名前・種別・設定値を持つ不変のデータとする。非機密項目の `ResolvedValueTrace` は採用値、採用元、適用順、全候補とその出典を構造化して保持する。選択・除外された設定の取り込みと条件も同じ結果に残す。エラーがある場合、実行可能な `Configuration` は返さず、診断だけを返す。Duck 側は自らの型で機密項目を分類し、表示や診断に使う結果から機密の値と候補値を除く。

内部は次の小さな処理に分ける。初版で外部機能の動的読込、独自の DI 機構、式解析器は作らない。`YAML` と `CEL` の外部型は内部の変換層に閉じ込める。

| 処理 | 入出力と役割 |
| --- | --- |
| 読込・解析 | 読み取ったバイト列から位置付きの文書型を作り、ファイルのハッシュ値を記録する。位置を失う前に重複キーと未知構造を検出する。 |
| 引数の束縛 | 宣言と呼出値を照合し、子ごとに不変の引数辞書を作る。 |
| 条件の評価 | 宣言済みの単一値だけを渡し、型検査済みの真偽値を得る。C# オブジェクト、任意関数、型情報からの動的な探索を公開しない。 |
| 取り込みの展開 | 経路と上限を管理し、循環検出と子の解決を行う。同一バイト列の解析は再利用しても、別引数での解決結果は混同しない。 |
| 値の合成 | 型付き候補を適用順に処理し、後の同型・同形の値、採用値の履歴と出典を不変の結果にする。不正な型・形や種別は診断にする。 |

Duck 側は `ResolvedConfiguration` を `DuckConfigurationValidator` に渡す。既存の `IOptions<T>` の既定値、型への変換、`RuntimeTrackerConfigurationResolver` と共通の有効値検証を通し、Aspire 型を含まない `ValidatedDuckPlan` を作る。初版の種別表は `duck.runtime`、`duck.development.base`、`duck.development.comparison` の明示登録とし、未登録の種別は拒否する。外部サービスの網羅的な定義方式は追加しない。

既存の `ValidateOnStart` をライブラリへ移さず、Duck 側で同じ検証関数を事前検証にも使う。`IOptions<T>.Value` を起動前に全件評価し、既存の遅延解決で初めて見つかる不正も計画の段階で検出する。検証のために UDP ソケットやサービスを作らない。

## 単体と AppHost の起動順

### 本番の単体

1. `--config`、`--instance`、入口引数だけを先に解析する。
2. 独立ライブラリで合成し、単体用として外部サービスの有無、全 Duck 設定の未知キー・型・有効値・識別の整合を検証する。
3. 指定した一つの設定を型へ変換する。確定済みの設定を使ってホストを作り、既存 `ValidateOnStart` も通す。
4. すべて成功してからサービスを開始する。設定エラー時は非0で終了し、サービスを一つも開始しない。

### 開発用起動手順と AppHost

1. 起動手順が共通の確認コマンドで開発用計画を解決し、全資源の設定と関係を検証する。AppHost の作成、Docker の操作、サービスの登録・起動はまだ行わない。
2. 検証済みの計画から必要な配布物の作成対象、AppHost の構築条件、資源と設定の対応を決める。任意のコマンド文字列ではなく、既存の許可済み対象に対応させる。
3. 必要な配布物作成と構築を行う。失敗したら AppHost を起動しない。
4. 検証済みの非機密設定を、一時的なプロセス間入力として AppHost に渡す案とする。設定原文や計画の JSON を保存しない。具体的な受け渡し方式は未実装で、既存の起動経路を確認して選ぶ。AppHost は対象名と非機密設定を再検証してから資源を登録する。
5. 起動後の所有、準備確認、終了、後片付けは既存 AppHost の責務とする。

確認コマンドは既存の `Tracker.RuntimeHost` に副作用のない `config` 呼出し形式を設ける案とする。これにより起動手順と AppHost が同じ Duck 側の型と検証を使える。開発環境で確認コマンドがまだ配布されていない場合は、確認用の RuntimeHost と独立ライブラリを先にコンパイルする必要がある。選択資源の配布物作成と AppHost の構築・実行は計画検証後とする。AppHost を構築しないと設定検証できない依存は作らない。

設定ファイルの読込後は同じバイト列を使って解析・合成する。配布物の作成や構築の途中で元ファイルが変わった場合は、開始前のハッシュ値照合で失敗させ、再確認を求める。AppHost や子プロセスが入口を別に読み直して異なる条件分岐を選ぶことはない。実行ごとの識別子など動的な運用情報は別の型付き入力にし、利用者の設定値を暗黙に上書きしない。

既存の環境変数やコマンドラインの一般的な設定供給で、確定した `IOptions<T>` を後から変えない。入口の引数は明示された `--arg` だけで渡す。現在、設定合成から起動までの秘密値経路は追加しない。秘密値が起動に必要と判明した場合は既存の別経路があるかを先に確認し、その読み手と保存期間を設計課題として決めるまで起動対象に含めない。従来形式の移行は別作業とし、通信内容と運用上の契約を変える理由にはしない。

## 出典と確認コマンド案

各非機密設定値の出典情報には採用値、採用元、適用順、全候補と各候補のファイル位置・取り込み経路・引数の出所を保持する。条件式と評価結果も内部の構造化結果に含めるが、確認コマンドの外部表示には使わない。Duck 側の型情報は機密項目を明示する。機密項目は合成に必要な間だけメモリ上で扱い、採用値と候補値を説明、ログ、エラー、保存計画に含めない。Duck の型の既定値を補った場合は、設定型と項目名を既定値の出典として付ける。構造解析だけで行・列を失わないことを読み取り器の選定条件にする。

確認コマンド案は次のとおり。いずれもサービスを開始しない。`check` は合成だけでなく Duck 側の検証も必須とし、成功0、設定・検証エラー2、読込などの実行エラー1を返す。

```powershell
# 本番単体の検証
Tracker.RuntimeHost config check --config config/field.yaml --target standalone

# 開発の選択条件を変えて全体検証
Tracker.RuntimeHost config check --config config/comparison.yaml --target development --arg cameraCount=4 --arg mode=comparison

# 確定値と出典を確認
Tracker.RuntimeHost config explain --config config/field.yaml --target standalone
```

開発用起動手順は内部 API で計画を解決・検証し、永続ファイルに保存しない。

`config explain` は起動せずに全非機密項目の採用値、候補、適用順、出典を確認できる。条件式、式に含まれる文字列値、条件へ束縛した値、評価結果は成功時も表示しない。出典のファイル名と位置は安全な表示情報として明示的に分類された場合だけ表示し、未分類のファイル名と位置は表示しない。Duck 側で機密指定した項目は値と候補を表示しない。秘密値を含むエラーも、値を出さずキーと位置だけを示す。通常の保存計画は作らず、設定原文も計画・診断に複製しない。

`check` と `explain` は同じ検証経路を使う。型・形・種別の不一致や Duck の有効値エラーがあるときに、不完全な設定を成功出力しない。通常の起動ログは既存と同程度に保ち、全項目や途中の候補を自動列挙しない。採用値と出典は構造化結果に保持し、必要な場合だけ起動側が既存の構造化ログ経路へ渡す。汎用ライブラリは `Console` やログ出力先を呼ばず、独自の記録処理も設けない。診断表示とログでは秘密値を伏せる。機密項目の分類前に失敗した場合はエラー種別と安全な行・列番号だけを示し、ファイル名、キー、設定原文、条件式の文字列値を診断に含めない。行・列番号を安全に取得できない場合はエラー種別だけを示す。

### 選択値を一度だけ残す既存の構造化ログ

通常起動のログ量は ROS 2 と同程度に保ち、全候補を自動列挙しない。既存の `Logging:LogLevel` で `Debug` を選んだ場合だけ、採用済みの非機密値と出典を設定項目ごとに一度記録する。単体起動では RuntimeHost が既存の `Tracker.RuntimeHost` 分類へ記録する。開発起動では AppHost が既存の `Duck.Testing.AppHost` 分類へ一度だけ記録し、子の RuntimeHost は同じ合成結果を記録しない。起動前の `config check` では記録しない。独自の記録処理は追加しない。

既存のログ分類だけで出力を絞る設定例を示す。通常のログは `Warning` を保ち、確認したいホストの分類だけ `Debug` にする。

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Tracker.RuntimeHost": "Debug",
      "Duck.Testing.AppHost": "Debug"
    }
  }
}
```

- RuntimeHost 単体では、合成と Duck 側の検証が成功した後、`Tracker.RuntimeHost/Program.cs` で `host.RunAsync()` に入る前に出す。事前確認用の `config check` では出さない。
- 開発起動では、AppHost が受け取った計画を検証した後、資源を登録・開始する前に AppHost の既存 `ILogger` へ一度だけ渡す。起動手順の事前確認と AppHost の両方から同じ項目を記録せず、子の RuntimeHost も重ねて記録しない。

出力は有効値と採用元だけを含み、中間候補・設定原文・機密項目の値や出典候補は含めない。出典のファイル名と位置は安全な表示情報として明示的に分類された場合だけ出力し、それ以外は省略する。機密項目は Duck 側の型情報で分類し、ログ・説明・候補一覧・エラーから値を除く。現在、起動に必要な秘密値の新しい経路は作らない。

`計画` は利用者向けの保存コマンドにせず、検証済みの非機密計画を実行中だけ保持する内部処理とする。原文や機密値を含む永続 JSON は作らない。将来秘密値が起動に必要なら、既存の別経路、その読み手、保存期間を確認して別途設計するまで未対応とする。


## 採用前に実証する依存

`CEL` は [公式仕様](https://github.com/cel-expr/cel-spec/blob/master/doc/langdef.md)を基準とする。`cameraCount >= 4 && mode == "comparison"` のような自然な条件式をそのまま扱う。C# の任意処理の実行、`Roslyn`、任意アセンブリの実行は採用しない。以下は公式リポジトリの説明を読んだ候補であり、この作業で導入・実行・性能比較はしていない。

| 候補 | 読み取れた特徴と採用前の確認 |
| --- | --- |
| [`Celly`](https://github.com/bsidio/celly) | 管理された C# 実装で、変数宣言、型検査、辞書の入力、評価予算と取消、構文木の検査を提供すると説明されている。候補の第一案。対象の .NET 版、配布物の依存、公開 API、実際の予算の効き方と公式仕様との一致を実証する。 |
| [`CelDotNet`](https://github.com/hbjydev/celdotnet) | 型検査と .NET の式木への変換を提供する候補。C# の任意処理の実行とは異なるが、公開する型や関数の制限、評価予算、仕様どおりのエラー処理を実証する。式木を使う必要がないため、第一案の不足時に比較する。 |
| [`YamlDotNet`](https://github.com/aaubry/YamlDotNet) | 低水準の解析と文書型を提供する候補。行・列の保持、重複キー検出、`YAML` 1.2 相当の単一値の解釈、別名等の無効化、深さとサイズの制限を実証する。 |

実装に進む前の小さな検証で、候補の版、利用条件、推移的な依存を記録し、次を確認する。公式説明の完全性や性能の主張を、そのまま Duck の検証結果にはしない。

- 宣言した `int`、`string` に対する上記の式で、境界値3・4、二つの `mode` の真偽が一致する。
- 未知変数、文字列と数値の比較、真偽値以外の結果、桁あふれ、評価エラーを確実に拒否する。`CEL` の論理演算で扱われるエラーについても公式仕様と照合する。
- ホストのメソッド、ファイル、通信、時刻、環境変数へ到達できない。初版は比較・論理・算術と単一値に限定し、不要な拡張機能を無効化または構文木の検査で拒否する。独自の式構文は追加しない。
- 大きな式、深い式、大きな文書が予算内で終了し、取消と上限超過を区別して返す。
- `YAML` の位置情報、文字列の引用、同じファイルの二回利用、Windows の別名位置でも出典と循環検出が安定する。

満たせない候補は差し替え、必要な契約を満たす既存実装がなければ依存選定を未解決として本人へ戻す。独自の `CEL` 実装で埋め合わせない。

## 実装時の検証条件

本変更では製品実装やテストを追加しない。実装時は対象プロジェクトの手順に従い、次の動作を仕様から先に固定する。

| 対象 | 必須の確認 |
| --- | --- |
| 合成 | 再帰取り込み、相対参照、必須・既定・未知・型違い引数、明示的な親引数参照、条件が偽の場合、同じ子の別引数利用。 |
| 値と出典 | 同じ型・形の異なる値は成功し、後の値・採用元・適用順を確認する。同値を含む全候補を記録する。配列は全体置換。型・形・種別の不一致、未知キー、大文字小文字の二重指定は拒否する。 |
| 起動しない失敗 | 読込失敗、条件エラー、循環、型・形・種別の不一致、Duck の未知キー・有効値・資源参照の不正で、ホストの開始・外部プロセス開始・Docker 操作・資源登録をすべて0回にする。偽の起動先を数えて確認する。 |
| 起動手順 | 計画が不正なら対象資源の配布物作成・構築も0回。正常な計画でだけ必要な配布物を選ぶ。準備の構築失敗や設定変更後に AppHost を起動しない。 |
| 経路の一致 | 同じ部品と引数から単体と開発計画の Duck の有効値・型・出典が一致する。開発固有の配置情報だけを外側で付加する。 |
| 境界 | 独立ライブラリが Duck と Aspire を参照しないこと、RuntimeHost のプロジェクト・推移的な参照・配布ファイルに Aspire が含まれないことを確認する。単体の確認・起動試験は AppHost や Docker を使わず行う。 |
| 既存の意味 | 通信形式、出力先、パケット内の識別、所有、準備確認、終了処理の既存回帰試験を維持する。実サービスでの通信成立は後続の受入検証で扱う。 |

## 未決事項と後続作業

合成規則、別名での複数起動、条件付き取り込み、ログ粒度、配列の全体置換は本人の指示に基づく設計案として記載した。製品実装や設定形式の最終採用は、この `Draft` PR では扱わない。独立したクラスライブラリと契約試験の境界を保ち、依存の技術項目を先に実証する。

依存の具体的な版、対象の .NET 版、上限の最終値、Windows のファイル実体の識別手段は技術検証待ちとする。NuGet 化の境界は、ライブラリ本体、公開 C# API、宣言・推移依存の範囲で定義する。将来の検証では利用側の試作を作り、Duck と Aspire を参照せずに参照・構築できること、公開 API と依存関係、パッケージ内容を確認する。現時点では実プロジェクトやパッケージを作らない。これらの未実証を採用済みとは扱わない。設計の確認後に、依存の小さな検証、独立ライブラリ、Duck 側の型への変換、単体の起動、起動手順と AppHost の順に作業を分ける。#150 は設計の提示段階として継続する。
