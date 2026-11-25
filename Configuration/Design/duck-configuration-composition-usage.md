# Duck で設定合成を利用する設計案

状態: ライブラリと Duck 側の接続は未実装の提案です。設定合成の共通契約は[設定合成ライブラリの設計案](configuration-composition-design.md)を参照してください。この文書は Duck 固有の構成、起動経路、運用上の表示例をまとめます。

## 既存構成との接続

既存実装では [RuntimeHost の起動](../../Tracker/Tracker.RuntimeHost/Program.cs)が設定を読み、[サービス登録](../../Tracker/Tracker.RuntimeHost/RuntimeHostServiceCollectionExtensions.cs)で型へ結び付け、`ValidateOnStart` を設定している。[起動手順](../../scripts/start-duck-simulation.cs)は RuntimeHost または開発用転送の配布物と AppHost を用意してから AppHost を起動する。そのため、構成の選択と検証を AppHost 内に閉じ込めない。

## Duck 側の責務境界と依存の向き

同じリポジトリ内に、独立したクラスライブラリ `Configuration/Configuration.Composition/Configuration.Composition.csproj` を一つ追加する案とする。名前空間は `Configuration.Composition`。Duck、Aspire、ホストの設定型への参照を持たず、将来このプロジェクトだけを分離できるようにする。対象の .NET 版は依存の実証後に決める。

| 担当 | 責務 |
| --- | --- |
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

Duck 側の変換・検証と起動計画の型は、既に AppHost から参照されている `Tracker.RuntimeHost` の `Configuration` 配下に置く案とする。開発資源の論理的な設定もここで検証するが、Aspire や Docker の実行 API は持ち込まない。将来分離する場合も、Duck 固有の型を汎用ライブラリへ混ぜない。

## Duck／Aspire の設定例

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

このアドレスは記述用の例であり、利用時は Duck の受信側でマルチキャストに参加するインターフェースに割り当てられた IPv4 アドレスを指定する。次の `comparison-services.yaml` でも `duck` の同じ設定項目を指定し、後から適用された `192.0.2.11` が採用される例にする。実運用の値を推奨するものではない。`comparison-services.yaml` は引数を自分で宣言する。

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

## Duck 側の変換と検証

Duck 側は `ResolvedConfiguration` を `DuckConfigurationValidator` に渡す。既存の `IOptions<T>` の既定値、型への変換、`RuntimeTrackerConfigurationResolver` と共通の有効値検証を通し、Aspire 型を含まない `ValidatedDuckPlan` を作る。初版の種別表は `duck.runtime`、`duck.development.base`、`duck.development.comparison` の明示登録とし、未登録の種別は拒否する。外部サービスの網羅的な定義方式は追加しない。

既存の `ValidateOnStart` をライブラリへ移さず、Duck 側で同じ検証関数を事前検証にも使う。`IOptions<T>.Value` を起動前に全件評価し、既存の遅延解決で初めて見つかる不正も計画の段階で検出する。検証のために UDP ソケットやサービスを作らない。

## Duck 単体と AppHost の起動順

### 本番の単体

1. `--config`、`--instance`、入口引数だけを先に解析する。
2. 独立ライブラリで合成し、単体用として外部サービスの有無、全 Duck 設定の未知キー・型・有効値・識別の整合を検証する。
3. 指定した一つの設定を型へ変換する。確定済みの設定を使ってホストを作り、既存 `ValidateOnStart` も通す。
4. すべて成功してからサービスを開始する。設定エラー時は非0で終了し、サービスを一つも開始しない。

### 開発用起動手順と AppHost

1. 起動手順が共通の確認コマンドで開発用計画を解決し、全資源の設定と関係を検証する。AppHost の作成、Docker の操作、サービスの登録・起動はまだ行わない。
2. 検証済みの計画から必要な配布物の作成対象、AppHost の構築条件、資源と設定の対応を決める。既存の許可済み対象に照合し、任意のコマンド文字列は実行しない。
3. 必要な配布物作成と構築を行う。失敗したら AppHost を起動しない。
4. 検証済みの非機密設定を、一時的なプロセス間入力として AppHost に渡す案とする。設定原文や計画の JSON を保存しない。具体的な受け渡し方式は未実装で、既存の起動経路を確認して選ぶ。AppHost は対象名と非機密設定を再検証してから資源を登録する。
5. 起動後の所有、準備確認、終了、後片付けは既存 AppHost の責務とする。

確認コマンドは既存の `Tracker.RuntimeHost` に副作用のない `config` 呼出し形式を設ける案とする。これにより起動手順と AppHost が同じ Duck 側の型と検証を使える。開発環境で確認コマンドがまだ配布されていない場合は、確認用の RuntimeHost と独立ライブラリを先にコンパイルする必要がある。選択資源の配布物作成と AppHost の構築・実行は計画検証後とする。AppHost を構築しないと設定検証できない依存は作らない。

設定ファイルの読込後は同じバイト列を使って解析・合成する。配布物の作成や構築の途中で元ファイルが変わった場合は、開始前のハッシュ値照合で失敗させ、再確認を求める。AppHost や子プロセスが入口を別に読み直して異なる条件分岐を選ぶことはない。実行ごとの識別子など動的な運用情報は別の型付き入力にし、利用者の設定値を暗黙に上書きしない。

既存の環境変数やコマンドラインの一般的な設定供給で、確定した `IOptions<T>` を後から変えない。入口の引数は明示された `--arg` だけで渡す。現在、設定合成から起動までの秘密値経路は追加しない。秘密値が起動に必要と判明した場合は既存の別経路があるかを先に確認し、その読み手と保存期間を設計課題として決めるまで起動対象に含めない。従来形式の移行は別作業とし、通信内容と運用上の契約を変える理由にはしない。

## 出典、確認コマンド、表示境界

機密分類や表示制御など呼出側の責務は、Duck 側の設定型と確認コマンドに対応付ける。機密項目は処理に必要な間だけメモリ上で扱い、採用値と候補値を説明、ログ、エラー、保存計画に含めない。Duck の型の既定値を補った場合は、設定型と項目名を既定値の出典として付ける。表示対象の出典は安全な情報として分類されていることを確認する。共通の出典・診断契約は設計書を参照する。

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

## Duck／Aspire の統合確認

本変更では製品実装やテストを追加しない。実装時は対象プロジェクトの手順に従い、次の動作を仕様から先に固定する。

| 対象 | 必須の確認 |
| --- | --- |
| 型への変換 | 合成済みの結果を Duck 固有型へ変換し、未知キー、有効値、引数間の関係、既存設定型との整合を検証する。 |
| 起動しない失敗 | 読込失敗、条件エラー、循環、型・形・種別の不一致、Duck の未知キー・有効値・資源参照の不正で、ホストの開始・外部プロセス開始・Docker 操作・資源登録をすべて0回にする。偽の起動先を数えて確認する。 |
| 起動手順 | 計画が不正なら対象資源の配布物作成・構築も0回。正常な計画でだけ必要な配布物を選ぶ。準備の構築失敗や設定変更後に AppHost を起動しない。 |
| 経路の一致 | 同じ部品と引数から単体と開発計画の Duck の有効値・型・出典が一致する。開発固有の配置情報だけを外側で付加する。 |
| 境界 | 独立ライブラリが Duck と Aspire を参照しないこと、RuntimeHost のプロジェクト・推移的な参照・配布ファイルに Aspire が含まれないことを確認する。単体の確認・起動試験は AppHost や Docker を使わず行う。 |
| 既存の意味 | 通信形式、出力先、パケット内の識別、所有、準備確認、終了処理の既存回帰試験を維持する。実サービスでの通信成立は後続の受入検証で扱う。 |

## 未実装の範囲

この文書の利用例と起動案は未実装であり、動作可能な設定形式やコマンドではない。Duck 固有の型変換、RuntimeHost と AppHost の間の計画受け渡し、運用値の扱いは後続の設計・実装で確定する。ログ出力の担当は前節のとおり確定しており、後続ではこの分担に沿った出力経路を実装する。ライブラリの依存候補と共通契約は[設定合成ライブラリの設計案](configuration-composition-design.md)に集約する。
