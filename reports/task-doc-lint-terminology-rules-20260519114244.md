# Markdown lint 運用ルールと設定メンテナ内部メモ

## 目的

IbisDuck の Markdown 資料作成では、作業者に細かい用語規則を覚えさせず、lint システムで表記と用語の品質を確認する方針を固定する。

この文書は、作業者へ知らせる Markdown lint 運用ルールと、lint 設定メンテナ向け内部メモを分けて記録する案である。作業者向けには最小限の運用ルールだけを示し、細かい語彙判断は `tools/lint/markdown-whitelist.yaml` と `tools/lint/prh.yml` に寄せる。

現時点では `markdown-whitelist.yaml` と `prh.yml` を変更しない。

## 入力

- 引き継ぎ資料: `reports/doc-lint-handover-20260519113733.md`
- 同義語候補: `reports/doc-lint-synonym-candidates-20260517165117.tsv`
- 固有名詞候補: `reports/doc-lint-proper-noun-extraction-20260517133249.md`
- 名詞一覧: `reports/doc-lint-noun-extraction-20260517133815.md`
- 既存 whitelist: `tools/lint/markdown-whitelist.yaml`
- 既存 prh: `tools/lint/prh.yml`

## 前提

- `markdown-whitelist-maintenance` skill は今回確認時点で存在しない。
- `review-enforcer` は Markdown whitelist 作業について、利用者が明示レビューした語だけを反映することを要求している。
- ChikkarPy の `synonyms` は候補整理の材料であり、`aliases` や `prh` へ自動変換しない。
- `reports/` は通常の Markdown lint 対象外であるため、この提案書自体は lint 門の対象外。

## 作業者向けルール

作業者へ知らせるルールはこの節だけにする。作業者が読む範囲は、この節の末尾で終了する。

1. Markdown 資料を作成または編集したら、リポジトリの Markdown lint システムを実行する。
2. lint の指摘に従って資料を直す。
3. 指摘が不適切に見える場合は、バッククォート化や言い換えで回避せず、lint 設定側の見直しとして報告する。

作業者に、単独英単語、複合語、alias、prh、whitelist、個別語句の細かい判断規則を覚えさせない。資料作成時の詳細な用語判断は lint システムの責務とする。

作業者向けの説明はここまでとする。以降は作業者へ直接提示しない。

## lint 設定メンテナ向け内部メモ

以降は lint 設定を作る側と保守する側の内部メモであり、作業者へ直接説明するための資料ではない。具体候補一覧や `Tracked` などの個別判断は、lint 設定メンテナが利用者レビューへ出す前の材料として扱い、作業者の判断規則にしない。

## lint システム側の責務

lint システムは次を担う。

1. 資料内の未登録語、説明不足の英単語、片仮名語、表記揺れを検出する。
2. 許可する語は `markdown-whitelist.yaml` に意味付きで登録する。
3. 直したい表記揺れは `prh.yml` で正式表記へ寄せる。
4. 語彙候補は少数の概念単位で利用者レビューへ出す。
5. 作業者が lint 回避のためだけにバッククォートや表記変更を使わなくて済む状態を保つ。

## lint 設定設計の原則

以降の規則は、lint 設定メンテナが守る内部方針である。

## 検査設定への分類先

用語候補は次の 3 種類へ分ける。

| 分類 | 目的 | 反映先 |
| --- | --- | --- |
| `prh候補` | 本文では正式表記へ直したい揺れを検出する | `tools/lint/prh.yml` |
| `whitelist維持候補` | 今後も使ってよい固有名詞、略語、複合設計語を説明付きで許可する | `tools/lint/markdown-whitelist.yaml` |
| `要確認候補` | 文脈差が大きい、多義的、または UI ラベルと本文語が混ざるため、人手判断が必要 | まだ反映しない |

## 検査設定の共通ルール

1. 単独英単語は原則として whitelist に登録しない。
2. 日本語本文では、単独英単語より日本語またはカタカナ表記を優先する。
3. `UI`、`CSV`、`PC` のように日本語文書でも通常英字で書く略語は例外として英字を維持できる。
4. 製品名、型名、名前空間、ファイル名、コマンド名、設定キー、UI ラベルそのものは、本文語とは別扱いにする。
5. 技術的な意味が複合語で固定される語は、単語単体ではなく複合語で whitelist 登録する。
6. `aliases` は、今後も同じ概念として許可する別表記だけに使う。
7. 正式表記へ直したい揺れは `aliases` に入れず、`prh.yml` へ入れる。
8. 意味が違う語を同じ `aliases` に混ぜない。近そうでも文脈が違う場合は別 term に分ける。
9. バッククォートは lint 回避に使わない。コード、識別子、パス、コマンド、設定キー、UI ラベルを示す場合だけ使う。
10. whitelist 変更は、少数の概念単位で利用者レビューを受けてから行う。

## `prh候補` へ寄せる条件

次を満たすものは `prh候補` とする。

- 本文の一般語として使われており、英字や片仮名を残す必要が薄い。
- 正式表記へ寄せても識別子、UI ラベル、設定キー、プロトコル名を壊さない。
- 同じ概念の表記揺れで、複数表記を今後も許可する理由がない。

初期候補:

| 揺れ候補 | 正式表記候補 | 判断 |
| --- | --- | --- |
| `setting` | `設定` | 本文一般語なら `設定` へ寄せる。設定キー内は対象外。 |
| `update` | `更新` | 本文一般語なら `更新` へ寄せる。コマンド名や API 名は対象外。 |
| `condition` | `状態` | 本文一般語なら `状態` へ寄せる。試験条件など別意味は要確認。 |
| `document`, `ドキュメント` | `文書` | リポジトリ文書の説明では `文書` へ寄せる。製品名や一般名称は対象外。 |
| `rule`, `ルール` | `規則` | lint 規則の説明なら `規則` へ寄せる。`textlint rule` は対象外。 |
| `request` | `要求` | API や通信文脈では残る可能性があるため、まず本文一般語だけ対象。 |
| `file` | `ファイル` | 本文一般語なら `ファイル` へ寄せる。パスやファイル名は対象外。 |
| `button` | `ボタン` | UI 部品の本文説明なら `ボタン` へ寄せる。UI ラベルは対象外。 |

## `whitelist維持候補` へ寄せる条件

次を満たすものは `whitelist維持候補` とする。

- 日本語本文でも英字表記が自然な略語である。
- 製品名、技術名、型名、名前空間、プロトコル名である。
- 複合設計語として意味が固定され、単独語より安全に説明できる。
- UI ラベルそのものを明示する必要がある。

英字維持候補:

```text
UI
API
CLI
CSV
PC
ID
UUID
DTO
JSON
JSONL
XML
SVG
UDP
HTTP
HTTPS
SDK
CI
DI
OS
NIC
LINQ
TDD
PR
```

製品名・技術名候補:

```text
.NET
ASP.NET Core
Blazor
Java
Python
JavaScript
Markdown
SudachiPy
ChikkarPy
NuGet
npm
Git
Docker
Codex
Serena
SSL-Vision
ER-Force
```

複合設計語候補:

```text
render snapshot
tracker snapshot
field geometry
source key
source label
source role
source identity
source metadata
diagnostics sample tick
official tracker packet
official tracker proto
camera-local track
event time
replay timeline
timeline scrubber
playback tick
```

## `要確認候補` へ置く条件

次を満たすものは `要確認候補` とする。

- ChikkarPy 上は近いが、設計上は別概念の可能性がある。
- UI ラベル、識別子、本文一般語が混在している。
- 単語単体で許可すると範囲が広すぎる。
- 既存文書内の使われ方を修正するか、複合語として許可するか判断が必要。

優先確認候補:

| 候補 | 理由 |
| --- | --- |
| `log` / `ログ` / `記録` | 診断ログ、保存記録、出力ログで意味が分かれる。 |
| `source` / `ソースコード` | 表示元、入力元、ソースコードが混ざる。単独 `source` は避ける。 |
| `field` / `領域` | 競技場の field と画面領域が混ざる。 |
| `test` / `試験` / `検査` / `テスト` | 試験、文書検査、テストコードで使い分けが必要。 |
| `sample` / `標本` / `サンプル` | 診断標本、サンプルデータ、一般語が混ざる。 |
| `metadata` / `メタデータ` | 技術語として英字維持かカタカナ化か判断が必要。 |
| `alignment` / `整列` / `アライメント` | 設計語として残す範囲を複合語で決める必要がある。 |
| `official` / `公式` | `official tracker packet` は複合語維持、本文一般語は `公式` 候補。 |
| `Play` / `Stop` / `Off` / `Compare` | UI ラベルそのものか、本文の説明語かで扱いが違う。 |

## `Tracked` の専用ルール

この節は lint 設定メンテナ向けの検討メモであり、作業者へ `Tracked` の個別判断を求めるための規則ではない。作業者には lint を実行し、その指摘に従う運用だけを求める。

現状:

- `tools/lint/markdown-whitelist.yaml` には `term: Tracked` が単独登録されている。
- そのため本文中の `Tracked` 単体が通る。
- 引き継ぎ方針では、`Tracked` 単体は何を指すか分かりにくいため避ける。

提案:

1. 本文の説明では `Tracked` 単体を使わない。
2. 画面や表示を指す場合は `Tracked 画面` または `Tracked 表示` と書く。
3. UI ラベルそのものを列挙する場合は `` `Tracked` `` として扱う。
4. whitelist では単独 `Tracked` を削除するか、UI ラベル限定の説明に狭める。
5. 代わりに `Tracked 画面` と `Tracked 表示` を whitelist 候補にする。

初期の反映案:

| 操作 | 候補 | 理由 |
| --- | --- | --- |
| 削除または狭義化 | `Tracked` | 本文語として広く許可しない。 |
| 追加候補 | `Tracked 画面` | 画面名として読める。 |
| 追加候補 | `Tracked 表示` | 表示状態または表示内容として読める。 |
| 要確認 | `Tracked 状態` | 追跡済み状態そのものを指す場合に必要か確認する。 |

## 初回分類の進め方

1. `Tracked` を最初の小さなレビュー単位にする。
2. 次に UI ラベル系を `Play ボタン`、`Stop ボタン`、`Fast Forward` などへ分ける。
3. 次に `source` / `field` / `sample` / `log` の多義語を、単独語ではなく複合語へ分解する。
4. 次に一般英単語の prh 候補を小さな組で提案する。
5. 最後に略語・製品名・技術名の維持候補を確認する。

## 反映前チェック

whitelist または prh を編集する前に、各候補で次を確認する。

- その語は本文語か、コード / 設定 / UI ラベルか。
- 単独語登録で意味が広がりすぎないか。
- `aliases` として今後も許可する別表記か、直したい表記揺れか。
- 既存文書を直す方が自然ではないか。
- 利用者がその exact entry を明示レビューしたか。

## 次の具体作業

利用者確認後、次の順で実施する。

1. `Tracked` 系だけを対象に、既存文書内の出現箇所と whitelist 変更案を作る。
2. 利用者レビューを受ける。
3. 承認済みの範囲だけ `markdown-whitelist.yaml` / `prh.yml` / 文書本文へ反映する。
4. focused Markdown lint を実行する。
5. 変更後に dedicated review を通す。
