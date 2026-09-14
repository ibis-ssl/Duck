# 同義語候補抽出レポート

## 概要

SKILL リポジトリ更新後の `review-enforcer` 語彙抽出スクリプトを使い、設計書に散らばる同義語候補を収集した。更新後スクリプトは SudachiPy の形態素情報に加え、ChikkarPy の同義語候補を `synonyms` として出力する。

このレポートは候補収集であり、`tools/lint/markdown-whitelist.yaml` の `aliases` や `tools/lint/prh.yml` へ自動反映するものではない。同義語候補は、正式表記、許可する別表記、修正すべき揺れ表記を利用者が確認するための材料として扱う。

## 成果物

- 同義語候補 TSV: `reports/doc-lint-synonym-candidates-20260517165117.tsv`
- 語彙 JSON: `/tmp/ibisduck-vocab-synonyms.json`
- 対象ファイル一覧: `/tmp/ibisduck-md-targets.txt`

TSV の列は次の通り。

```text
groupKey	totalCount	observedTerms	suggestedSynonyms	sources
```

`observedTerms` は実際に対象文書内で観測された表記である。`suggestedSynonyms` は ChikkarPy が返した補助候補であり、文書内に出ていない語も含む。

## 抽出条件

- 対象: `npm run -s lint:md:targets` が返す 20 ファイル
- 抽出元スクリプト: `/home/ibis/AI/CodexSkill/skills/review-enforcer/scripts/extract-markdown-vocabulary-sudachi.py`
- 同義語補助: ChikkarPy
- 長文対策: SudachiPy の 1 入力上限を避けるため、抽出時だけ長い Markdown を分割
- レポート対象: ChikkarPy の `synonym:*` グループのうち、実際に文書内で 2 表記以上が観測されたもの

## 集計

- ChikkarPy 同義語グループ: 297
- 文書内で 2 表記以上が観測されたグループ: 137
- 英字、日本語、カタカナなど複数種別を含むグループ: 103
- 日本語だけで揺れているグループ: 34
- カタカナ語を含むグループ: 50
- 既存 whitelist で alias を持つ entry: 50

## 優先確認したい候補

次は文書内で複数表記が実際に使われており、表記統一または alias 判断の優先度が高い。

| 候補グループ | 観測表記 | 判断メモ |
| --- | --- | --- |
| 記録 / log / ログ | `記録`, `log`, `ログ` | `log` と `ログ` を許可するか、本文は `記録` に寄せるか判断が必要。 |
| source / ソースコード | `source`, `ソースコード` | `source` は設計語として多用される。日本語説明では `表示元` や `ソースコード` と混同しない確認が必要。 |
| field / 領域 | `field`, `領域` | 競技場の `field` と画面領域の `領域` が混ざる可能性があるため、自動 alias 化は危険。 |
| 設定 / setting | `設定`, `setting` | 基本は `設定` へ寄せる候補。設定キー名は別扱い。 |
| test / 試験 / 検査 / テスト | `test`, `試験`, `検査`, `テスト` | 文脈差が大きい。テスト工程、検査、試験名を分けて判断する。 |
| 保存 / 保管 | `保存`, `保管` | 本文では `保存` へ寄せる候補。 |
| robot / ロボット | `robot`, `ロボット` | SSL-Vision やプロトコル文脈の英字識別子と本文表記を分ける。 |
| alignment / 整列 | `alignment`, `整列` | 設計語として `alignment` が固定されている可能性がある。alias 候補。 |
| 選択 / choice | `選択`, `choice` | UI 表示や列名以外は `選択` へ寄せる候補。 |
| 出力 / output / 成果物 | `出力`, `output`, `成果物` | `output` と成果物は意味がずれる場合がある。 |
| 実行 / 実施 | `実行`, `実施` | 手順文では統一候補。 |
| 更新 / update | `更新`, `update` | 一般本文では `更新` へ寄せる候補。 |
| 状態 / condition | `状態`, `condition` | `condition` は通常語としては `状態` へ寄せる候補。 |
| file / ファイル | `file`, `ファイル` | ファイル名・パスは除外し、本文語としては `ファイル` へ寄せる候補。 |
| metadata / メタデータ | `metadata`, `メタデータ` | 設計語としてどちらを正とするか判断が必要。 |
| official / 公式 | `official`, `公式` | `official tracker packet` など複合語は英字のまま残す可能性がある。 |
| 文書 / ドキュメント | `文書`, `ドキュメント` | 本文では `文書` へ寄せる候補。 |
| 入力 / Input | `入力`, `Input` | UI 表示名や固有ラベル以外は `入力` へ寄せる候補。 |
| sample / 標本 / サンプル | `sample`, `標本`, `サンプル` | `diagnostics sample tick` のような設計語は英字のまま残す可能性がある。 |
| rule / 規則 / ルール | `rule`, `規則`, `ルール` | textlint rule などの固有文脈と本文語を分ける。 |
| request / 要求 / 要望 | `request`, `要求`, `要望` | API/通信文脈と要件文脈で意味が分かれる。 |

## 既存 whitelist aliases から見える同義語

既に許可一覧で alias として管理されている代表例は次の通り。

```text
Tracker.DebugHost => tracker-debug-host / tracker debug host / Tracker DebugHost
Tracker.RuntimeHost => Tracker RuntimeHost / tracker-runtime-host / tracker runtime host
SSL-Vision => SSL Vision
ASP.NET Core => ASP NET Core / ASPNETCORE
.NET SDK => NET SDK
Tracker.CaptureReplay => Tracker CaptureReplay
SSL_WrapperPacket => SSL WrapperPacket
3rd party tracker => third-party tracker / third party tracker / 3rd-party tracker
ER-Force => ER-FORCE / ER Force
Raw Aggregate => raw aggregate / raw-aggregate
Raw Camera => raw camera / raw-camera
Fast Forward => FastForward / fast forward
raw vision viewer => raw-vision-viewer
raw vision => raw-vision
same render tick => same-render-tick
render snapshot => render-snapshot / render snapshots
tracker snapshot => tracker-snapshot
```

これらは「許可する別表記」として既に扱われている。今後の判断では、同じ概念として残す表記は `aliases`、正表記へ直したい表記は `prh` に分ける。

## 同義にしないと明記されている語

設計書内には、似ているが同義にしないと明記された語がある。これらは ChikkarPy などの候補に出ても alias 化しない。

| 語 | 扱い |
| --- | --- |
| `diagnostics sample tick` / `tracker committed frame cadence` | `diagnostics sample tick` の cadence は `tracker committed frame cadence` と同義にしない。 |
| `diagnostics sample tick` / `tracker committed frame` | `diagnostics sample tick` は `tracker committed frame` と同義にしない。 |

## prh 向けに見える候補

次は同じ概念の許可表記というより、本文では片方に寄せたい可能性がある。

| 正表記候補 | 揺れ候補 |
| --- | --- |
| 設定 | setting |
| 保存 | 保管 |
| 更新 | update |
| 状態 | condition |
| 文書 | ドキュメント |
| 入力 | Input |
| 規則 | ルール |
| 要求 | request |

ただし、識別子、UI ラベル、設定キー、プロトコル名に含まれる英字表記は prh で直してはいけない。

## whitelist aliases 向けに見える候補

次は設計語や技術語として別表記を許可する候補である。

| term 候補 | alias 候補 |
| --- | --- |
| Kalman | カルマン |
| metadata | メタデータ |
| alignment | アライメント / 整列 |
| sample | 標本 / サンプル |
| robot | ロボット |
| official | 公式 |

ただし、単独語で許可すると範囲が広くなるものは、`diagnostics sample tick` や `official tracker packet` のような複合語で扱う方が安全である。

## 注意点

- ChikkarPy の同義語候補は広く、文脈上の意味違いを自動では判別しない。
- `field` / `領域` のように、同義語辞書上は近くても設計文脈では別概念になり得る語がある。
- `source` は `表示元`、`入力元`、`ソースコード` など複数文脈にまたがるため、単純な alias 化は避ける。
- `log` / `ログ` / `記録` は近いが、診断ログ、保存記録、出力ログで使い分けがあり得る。
- whitelist へ追加する場合は利用者の明示レビューが必要である。

## 次の作業候補

1. TSV の 137 グループを、`aliases` 候補、`prh` 候補、無視候補に分類する。
2. `source`、`field`、`log`、`sample` のような多義語を先に人手で除外または細分化する。
3. prh に入れる語は、識別子や UI ラベルを誤置換しない条件を先に設計する。
4. whitelist に入れる語は、単独語より複合語を優先する。
