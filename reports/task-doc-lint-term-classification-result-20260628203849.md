# Markdown 用語分類案

## 入力と前提

- 入力 report:
  - `reports/doc-lint-handover-20260519113733.md`
  - `reports/doc-lint-synonym-candidates-20260517165117.tsv`
  - `reports/doc-lint-synonym-extraction-20260517165117.md`
  - `reports/doc-lint-proper-noun-extraction-20260517133249.md`
  - `reports/doc-lint-noun-extraction-20260517133815.md`
- 参照した lint 設定:
  - `tools/lint/markdown-whitelist.yaml`
  - `tools/lint/prh.yml`
  - `tools/lint/README.md`
  - `tools/lint/markdown-targets.json`
- `tools/lint/prh.yml` は現状 `rules: []` であり、表記統一規則は未登録。
- `tools/lint/markdown-whitelist.yaml` は `term` と `aliases` を許可語として扱い、追加・削除・変更には利用者の明示確認が必要。
- `reports/**` は `tools/lint/README.md`、`tools/lint/markdown-targets.json`、`.textlintignore`、`cspell.config.jsonc` で通常 Markdown lint 対象外として扱われる。
- ChikkarPy / SudachiPy は候補整理の材料であり、出力結果を自動で `aliases` や `prh` に変換しない。

## 分類方針

- `prh候補`: 本文一般語としては正式表記へ寄せたい揺れ。識別子、UI ラベル、設定キー、プロトコル名、ファイル名、コード片は対象外にする。
- `whitelist維持候補`: 日本語文書でも英字維持すべき略語、製品名・技術名、複合設計語。候補と理由を示すが、この段階では設定を編集しない。
- `要確認候補`: 多義語、UI ラベル混在、ChikkarPy 上は近いが設計上別概念の可能性がある語。利用者確認後に `prh`、whitelist、本文修正、無視のいずれかへ分ける。
- 単独英単語は broad allowance になりやすいため、略語・固有名詞・UI ラベルを除き、原則として複合語または日本語表記を優先する。
- `description` にも英字語・片仮名語が入るため、whitelist 候補は説明文まで lint 対象になる前提で作る。

## prh候補

### 本文一般語

次は本文一般語として片方に寄せる候補。`prh.yml` へ入れる場合も、まず対象文書でコード・ラベル・設定キーではない出現だけを確認する。

| expected 候補 | pattern 候補 | 根拠と注意 |
| --- | --- | --- |
| 設定 | `setting` | `設定` 174 件、`setting` 1 件。設定キー名や設定階層名は対象外。 |
| 保存 | `保管` | `保存` 135 件、`保管` 3 件。保存先や記録保存の本文語としては `保存` へ寄せる候補。 |
| 更新 | `update` | `更新` 88 件、`update` 12 件。API 名、イベント名、識別子は対象外。 |
| 状態 | `condition` | `状態` 97 件、`condition` 1 件。条件式や型名は対象外。 |
| 文書 | `ドキュメント` | `文書` 71 件、`ドキュメント` 1 件。Markdown 形式名や外部製品名ではなく本文一般語に限る。 |
| 入力 | `Input` | `入力` 64 件、`Input` 7 件。`Vision Input` など UI ラベル・固有表示名は対象外。 |
| 規則 | `ルール` | `規則` 39 件、`rule` 16 件、`ルール` 3 件。ただし `textlint rule` や lint 設定説明では英字を残す可能性がある。 |
| 要求 | `request` | `要求` 29 件、`request` 25 件、`要望` 1 件。HTTP request、API 名、通信文脈は要確認。 |
| ファイル | `file` | `file` 52 件、`ファイル` 33 件。ファイル名、パス、設定キーは対象外。 |
| ボタン | `button` | `button` 38 件、`ボタン` 5 件。UI ラベルそのものではなく UI 部品の一般説明に限る。 |
| 速度 | `speed` | `速度` 50 件、`speed` 10 件。`playback speed` のような設計複合語は whitelist 側の可能性がある。 |
| 手動 | `manual` | `手動` 12 件、`manual` 4 件。文書名や命令名は対象外。 |
| 閾値 | `threshold` | `threshold` 6 件、`閾値` 9 件。設定キー名・型名は対象外。 |
| 警告 | `warning` | `warning` 2 件、`警告` 2 件。ログ種別や UI 表示名なら要確認。 |

### 識別子・UIラベル・設定キー対象外の注意

- `` `Tracked` ``、`Vision Input`、`Raw Aggregate`、`Raw Camera`、`Layer A/B`、`Fast Forward`、`Capture On`、`Capture Off` は UI ラベルまたは表示名として扱われているため、本文一般語の `prh` では置換しない。
- `Tracker:ActiveProfileName`、`Tracker:Profiles`、`ReorderWindowNs` は設定キーまたは設定名であり、`prh` の対象外。
- `source key`、`source label`、`source metadata`、`field geometry`、`render snapshot` などの複合設計語は、単独語の日本語化ではなく whitelist 側で意味を固定する。
- `file`、`Input`、`request`、`condition` などはコード・設定・UI ラベルと本文一般語が混在しやすいため、実登録前に対象ファイル単位の出現確認が必要。

## whitelist維持候補

### 略語

日本語文書でも英字維持が自然な略語。現状 whitelist にあるものは維持候補、未登録のものは exact candidate として利用者確認が必要。

| 候補 | 状態 | 理由 |
| --- | --- | --- |
| UI | 登録済み | 画面と操作部を指す略語。 |
| API | 登録済み | 外部から機能を呼び出す口を指す略語。 |
| CLI | 登録済み | 命令行から使う実行体を指す略語。 |
| JSON / JSONL | 登録済み | データ形式名。 |
| XML | 登録済み | `XML documentation comment` の構成語として登録済み。単独登録するかは要確認。 |
| SVG | 未登録候補 | 画像形式名。固有名詞抽出では 5 件。 |
| UDP / HTTP / HTTPS | 登録済み | 通信方式名。 |
| DTO / UUID / ID | 登録済み | 型分類または識別子形式。 |
| TDD / PR | 登録済み | 開発手法・変更提案の略語。 |
| SDK / CI / DI / OS / NIC / LINQ | 未登録候補 | 技術略語として英字維持が自然。出現数は少ないため登録要否は確認対象。 |
| CSV / PC | 未登録候補 | 引き継ぎ方針上は例外候補。ただし入力 report 上で強い出現根拠は未確認。 |

### 製品名・技術名

| 候補 | 状態 | 理由 |
| --- | --- | --- |
| .NET / .NET SDK / ASP.NET Core | 登録済み | 実行基盤・開発基盤・製品名。 |
| Blazor | 登録済み | .NET 画面構築技術名。 |
| Python / JavaScript | 登録済み | 処理系名。 |
| Java | 未登録候補 | 技術名として抽出済み。 |
| Markdown | 登録済み | 文書形式名。 |
| SudachiPy | 登録済み | 形態素解析器名。 |
| ChikkarPy | 未登録候補 | 同義語候補の材料として使うライブラリ名。 |
| NuGet / npm / Git / Docker / Codex / Serena | 登録済み | ツール・環境名。 |
| xUnit | 未登録候補 | テストフレームワーク名。 |
| SSL-Vision / ER-Force | 登録済み | 外部仕組みまたは外部追跡器名。 |
| Tigers | 未登録候補 | 外部トラッカー参照。正式表記の確認が必要。 |

### 複合設計語

現状の whitelist は単独 `source` や `field` ではなく、意味を固定した複合語を多く登録している。この方向を維持する。

| 候補 | 状態 | 理由 |
| --- | --- | --- |
| source key / source label / source metadata / source snapshot | 登録済み | `source` 単独ではなく表示元・判別情報などに意味を限定している。 |
| field geometry / raw geometry / render snapshot geometry | 登録済み | 競技場形状情報として意味を限定している。 |
| packet snapshot / tracker packet snapshot / snapshot log | 登録済み | 記録・比較用に固定した通信単位を表す。 |
| diagnostics sample tick / diagnostics sample sidecar | 登録済み | 診断再生で保存単位を固定する設計語。 |
| official tracker packet / official tracker proto / tracker proto | 登録済み | 公式追跡出力の通信形式・通信単位。 |
| render snapshot / tracker snapshot / tracker source snapshot | 登録済み | 比較や旧診断表示の時点記録。 |
| latest-before snapshot / latest-before fallback | 登録済み | 後続記録を代用しない設計契約。 |
| camera-local track / camera-local ball track / camera-local robot track | 登録済み | 撮影元単位の追跡状態。 |
| playback speed / playback controls | 未登録候補 | `speed`、`Play`、`Stop` を単独登録しないための複合候補。実使用確認が必要。 |

## 要確認候補

| 候補 | 判断が必要な理由 |
| --- | --- |
| `記録` / `log` / `ログ` | 診断ログ、保存記録、出力ログで意味が分かれる。`diagnostics log` は whitelist、本文一般語は `記録` または `ログ` へ寄せる可能性がある。 |
| `source` / `ソースコード` | `source` は表示元・入力元・ソースコードの複数文脈にまたがる。`ソースコード` は既存 whitelist 登録済み。 |
| `field` / `領域` | 競技場の `field` と画面領域の `領域` は設計上別概念になり得る。 |
| `test` / `試験` / `検査` / `テスト` | テスト工程、検査、試験名、`textlint` 文脈が混在する。 |
| `robot` / `ロボット` | プロトコル・識別子・本文一般語が混ざる可能性がある。 |
| `alignment` / `整列` / `アライメント` | 対応表としての `alignment` と一般的な整列は別概念になり得る。 |
| `selection` / `選定` / `選択` / `choice` | UI 選択、候補選定、列名が混在する。 |
| `output` / `出力` / `成果物` | 出力と成果物は近いが同義とは限らない。 |
| `metadata` / `メタデータ` | 設計語として英字を正にするか、本文ではカタカナへ寄せるか判断が必要。 |
| `official` / `公式` | `official tracker packet` など複合語では英字維持、本文一般語では `公式` の可能性がある。 |
| `sample` / `標本` / `サンプル` | `diagnostics sample tick` は設計語、一般語の `サンプル` は別扱い。 |
| `runtime` / `実行時` | `Tracker.RuntimeHost` や `runtime profile` と一般語の実行時が混在する。 |
| `summary` / `概要` / `要約` | `summary` タグや UI/設定語が混在し得る。 |
| `index` / `索引` | `ReplayTimelineIndex` など識別子と一般語が混在する。 |
| `mode` / `モード` | `AutoRef mode`、UI モード、設定値が混在する。 |
| `interface` / `インターフェース` | API 境界、C# interface、UI 文脈の区別が必要。 |
| `page` / `ページ` | UI 画面、Markdown ページ、一般語が混在する。 |
| `Play` / `Stop` / `Off` / `Compare` | UI ラベルとしてのみ許可するか、`Play ボタン` などの複合語へ寄せるか確認が必要。 |

## `Tracked` の扱い

- 現状 `tools/lint/markdown-whitelist.yaml` には `term: Tracked` が単独登録されている。
- この状態では単独の `Tracked` 表記が本文でも通るため、利用者方針である「`Tracked` 単体を避け、`Tracked 画面` のように文脈を補う」を強制できない。
- 方針案:
  - 本文説明では単独の `Tracked` 表記を避ける。
  - UI ラベルそのものを示す場合だけ `` `Tracked` `` として残す。
  - 本文で画面や表示を説明する場合は `Tracked 画面`、`Tracked 表示`、必要なら `Tracked 状態` へ寄せる。
  - `term: Tracked` は削除するか、UI ラベル限定の扱いに限定する。ただし限定しても単独語許可自体は残るため、検出目的では削除または複合語化が有力。
- `Tracked 画面` / `Tracked 表示` は whitelist term にできるが、単独 `Tracked` を残したままでは `Tracked` だけの表記を止められない。

## 次に利用者へ確認すべき exact entry 候補

次は設定編集前の exact candidate であり、まだ `markdown-whitelist.yaml` / `prh.yml` へ入れない。

### `markdown-whitelist.yaml` 候補

```yaml
  - term: Tracked 画面
    description: 診断画面で追跡済み状態の表示に切り替えた画面または表示文脈。
```

```yaml
  - term: Tracked 表示
    description: 診断画面で追跡済み状態を表示している状態。UI ラベルそのものの `Tracked` とは分けて扱う。
```

```yaml
  - term: ChikkarPy
    description: 語彙候補を同義語グループにまとめるために使う解析補助ライブラリ名。
```

```yaml
  - term: SVG
    description: 図や画面部品の説明で使う画像形式名。
```

```yaml
  - term: xUnit
    description: .NET のテストで使うテストフレームワーク名。
```

```yaml
  - term: SDK
    description: 開発に使う道具一式を指す略語。
```

```yaml
  - term: CI
    description: 変更の検証を自動実行する仕組みを指す略語。
```

```yaml
  - term: NIC
    description: ネットワークインターフェースを指す略語。
```

```yaml
  - term: playback speed
    description: 診断再生で時系列を進める速度設定。
```

```yaml
  - term: playback controls
    description: 診断再生の Play、Fast Forward、Stop などの操作部。
```

### `markdown-whitelist.yaml` 変更・削除候補

```yaml
  - term: Tracked
    description: 画面で追跡済み状態を選ぶ表示名。未加工入力とは分けて扱う。
```

- 候補: 削除、または UI ラベル限定として残すかを利用者確認する。
- 推奨: 本文 lint で単独の `Tracked` 表記を避けたいなら削除し、本文側は `Tracked 画面` / `Tracked 表示` へ寄せる。

### `prh.yml` 候補

```yaml
  - expected: 設定
    pattern:
      - setting
```

```yaml
  - expected: 保存
    pattern:
      - 保管
```

```yaml
  - expected: 更新
    pattern:
      - update
```

```yaml
  - expected: 状態
    pattern:
      - condition
```

```yaml
  - expected: 文書
    pattern:
      - ドキュメント
```

```yaml
  - expected: ファイル
    pattern:
      - file
```

- これらは候補であり、実登録前に識別子、UI ラベル、設定キー、ファイル名、コード片を誤検出しないことを対象文書で確認する必要がある。

## ChikkarPy / SudachiPy の扱い

- ChikkarPy は `synonyms` に近い語を出すが、設計文脈の同義性は保証しない。
- SudachiPy は品詞、読み、正規形、頻度の材料を出すが、`実時`、`本開発`、`見方` のような誤判定もある。
- そのため、ChikkarPy / SudachiPy の出力は分類、頻度確認、出現元確認の材料に限る。
- 自動で `aliases` へ入れない。
- 自動で `prh.yml` へ入れない。
- 最終的な whitelist / prh 編集は、exact entry を利用者が確認した後に別工程で実施する。

## 残リスクと次アクション

- `reports/**` は通常 Markdown lint 対象外なので、この report 自体は lint gate の直接対象ではない。
- Serena は利用者指示にあるが、この実行環境では callable tool が見つからず、通常のファイル確認で代替した。
- TSV の全 137 グループを個別に確定したわけではなく、既存 report の優先候補と lint 設定に基づく分類案である。
- `prh` 候補は単純登録すると識別子や UI ラベルを誤検出する可能性がある。
- `Tracked` は単独 term をどう扱うかで検出力が変わるため、次工程で最初に利用者確認が必要。
- 次アクション:
  - 利用者が `Tracked` の exact policy を選ぶ。
  - 利用者が whitelist exact entry と `prh.yml` exact rule の採否を確認する。
  - 承認後に `markdown-whitelist.yaml` / `prh.yml` を編集し、対象 Markdown lint を再実行する。
