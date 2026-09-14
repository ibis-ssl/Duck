# Markdown 用語整理 引き継ぎメモ

## 目的

IbisDuck の Markdown 文書検査で使う `tools/lint/markdown-whitelist.yaml` と `tools/lint/prh.yml` を、意味が分かる単位で整備する。

最終的な狙いは次の通り。

- 既存設計書、README、進捗ファイルに散らばる固有名詞、名詞、同義語、表記揺れを抽出する。
- whitelist は「許可する語」と「その意味」が分かる形にする。
- prh は「直したい揺れ表記」を正式表記へ寄せる用途に分ける。
- 英単語単体をむやみに許可せず、複合語や文脈を持つ語を優先する。
- 利用者が明示レビューした語だけを whitelist / prh へ反映する。

## 対象リポジトリ

- IbisDuck: `/home/ibis/ssl/IbisDuck`
- SKILL リポジトリ: `/home/ibis/AI/CodexSkill`

現在確認した状態:

- IbisDuck branch: `docs/runtimehost-readme-appsettings...origin/docs/runtimehost-readme-appsettings`
- IbisDuck 未追跡ファイル:
  - `reports/doc-lint-proper-noun-extraction-20260517133249.md`
  - `reports/doc-lint-noun-extraction-20260517133815.md`
  - `reports/doc-lint-noun-list-20260517133815.tsv`
  - `reports/doc-lint-synonym-extraction-20260517165117.md`
  - `reports/doc-lint-synonym-candidates-20260517165117.tsv`
  - `reports/doc-lint-handover-20260519113733.md`
- SKILL リポジトリ branch: `main...origin/main`
- SKILL 最新確認コミット: `7771ef3 Add ChikkarPy vocabulary grouping (#39)`
- SKILL 作業ツリー: clean

## 関連する作業手順

今回の引き継ぎ作成では `handover-memo-writer` を使った。

引き継ぎ先では、少なくとも次を使うこと。

- `development-orchestrator`: AGENTS.md の入口として最初に確認する。
- `report-output-manager`: レポート作成時に使う。
- `review-enforcer`: whitelist / prh / Markdown lint の扱いを確認する。
- `design-doc-maintainer`: 仕組みや方針を設計書へ反映する場合に使う。

メモリ上では `skills/markdown-whitelist-maintenance/SKILL.md` が関連 skill とされていたが、今回の確認時点では `/home/ibis/AI/CodexSkill/skills/markdown-whitelist-maintenance/SKILL.md` は存在しなかった。次チャットではまず SKILL リポジトリの現物を再確認すること。

## 既存制約と利用者方針

### whitelist / prh の分離

- whitelist は文章中で使ってよい語を定義する。
- prh は修正すべき表記揺れを正式表記へ寄せる。
- 同じ概念として今後も許可する別表記だけを `aliases` に入れる。
- 正表記へ直したい揺れは `aliases` ではなく `tools/lint/prh.yml` に入れる。
- ChikkarPy の `synonyms` は候補グループ化の手がかりであり、whitelist aliases や prh へ自動反映してはいけない。

### 用語登録方針

- 複合語登録を優先する。
- 単独英単語は原則として broad allowance になるため避ける。
- 単独英単語は、必要ならカタカナ表現を優先する。
- ただし `UI`, `CSV`, `PC` のように、日本語文書でも通常英語表記される略語は例外。
- `render snapshot` のような確立済み複合 technical phrase は、そのまま複合語で扱う。無理に混ぜた日本語へしない。
- alias に意味が違う語を混ぜない。意味が違うなら別 term に分ける。
- whitelist 修正は利用者の明示レビューが必須。

### `Tracked` の扱い

利用者の直近方針:

> Tracked 単体で書くのではなく、Tracked 画面のように書くようにしませんか？Tracked 単体で書くと何やこれとなります

確認済み事実:

- 現在の `tools/lint/markdown-whitelist.yaml:43` には `term: Tracked` が単独登録されている。
- そのため現状では `Tracked` 単体の prose が通る。
- 仕組みとしては `Tracked 画面` や `Tracked 表示` のような文脈付き phrase を whitelist term にできる。
- ただし、`Tracked 画面` を追加するだけでは不十分で、単独 `Tracked` を削除または説明上 narrow しない限り、bare `Tracked` は通り続ける。
- バッククォート内の `` `Tracked` `` はコード / UI ラベル扱いで通常 prose lint の対象外。UI ラベルそのものの列挙は残せるが、本文の説明は `Tracked 画面` などに寄せる。

## ここまでの時系列

1. 利用者が「SKILL 側リポジトリ PR37 で SudachiPy を導入したので、現状の設計書などから固有名詞一覧を取得したい」と依頼した。
2. `development-orchestrator` と Serena を確認し、IbisDuck の対象 Markdown と SKILL 側の `review-enforcer` スクリプトを確認した。
3. 初回の `npm run -s lint:md:vocab -- --format json` は SudachiPy 依存不足で失敗した。
4. `/tmp/ibisduck-sudachi-venv` を作り、`tools/lint/requirements.txt` から `sudachipy`, `sudachidict_core`, `PyYAML` を入れた。
5. SudachiPy 実行は、長い Markdown ファイルで `Input is too long, it can't be more than 49149 bytes` に当たった。
6. 正式対象 20 ファイルを `npm run -s lint:md:targets` で固定し、長いファイルだけ抽出時に分割する読み取り専用ラッパーで語彙を抽出した。
7. 固有名詞候補レポートを作成した。
8. 利用者が「名詞一覧も取得できるか」「固有名詞以外も収集しよう」と依頼した。
9. SudachiPy が `名詞` と判定した日本語 / カタカナ語を全件 TSV 化し、名詞一覧レポートを作成した。
10. 利用者が「設計書に散らばる同義語を収集したい」と依頼した。
11. その途中で利用者が SKILL リポジトリを更新した。
12. 更新後の SKILL は `7771ef3 Add ChikkarPy vocabulary grouping (#39)` で、ChikkarPy による synonym grouping が入っていた。
13. ChikkarPy 導入は通常の `pip install -r tools/lint/requirements.txt` だと Python 3.12 の build isolation / `pkg_resources` 不足で失敗した。
14. `setuptools<81`, `wheel==0.47.0`, `vcs-versioning==1.1.1` を先に入れ、`--no-build-isolation` で `chikkarpy==0.1.1` を入れて成功した。
15. ChikkarPy 付き語彙 JSON を `/tmp/ibisduck-vocab-synonyms.json` に作り、実際に文書内で 2 表記以上出ている同義語候補 137 グループを TSV とレポートにした。
16. 利用者が「英単語単体は基本カタカナ表現を優先、UI / CSV / PC のような略語は除外」という方針を出した。
17. その方針に沿って、英単語単体は原則 prh でカタカナへ寄せ、略語・製品名・型名・ファイル名・UI ラベル・複合設計語を例外にする提案を返した。
18. 利用者が「設計語として単独登録するべきものはどれだけあるか」と確認した。
19. 回答として、単独登録すべき設計語は 0 から多くても 5 語程度で、ほとんどは複合語で扱うべきと整理した。
20. 利用者が `Tracked` 単体ではなく `Tracked 画面` のように書く方針を提案した。
21. 現状 whitelist は `Tracked` 単体登録のため、その方針をまだ enforce できていないことを確認した。
22. 今回、利用者が `$handover-memo-writer` を指定し、この引き継ぎメモを作成した。

## 作成済み成果物

### 固有名詞候補

- `reports/doc-lint-proper-noun-extraction-20260517133249.md`
- 目的: 設計書、進捗、README、lint メモから固有名詞、識別子、設計語候補を抽出。
- 主な内容:
  - 既存 whitelist でカバー済みの語。
  - `DebugHost`, `RuntimeHost`, `Kalman`, `Tigers`, `CaptureReplay`, `ASP.NET`, `Java`, `SDK`, `CI`, `DI`, `LINQ`, `xUnit`, `OS`, `NIC`, `SVG`, `XML` など。
  - `Play`, `Forward`, `Stop`, `Off`, `Compare` は UI 表示名として扱うか要確認。
  - `camera-local`, `best-effort`, `capture-time`, `latest-before`, `session-relative`, `wall-clock`, `control-only`, `multi-camera`, `read-side`, `tie-break`, `event-time`, `top-level`, `view-state`, `time-sync`, `type-owned`, `uncertainty-weighted`, `field-first`, `geometry-only`, `nearest-after`, `not-created`, `observer-event`, `profile-aware`, `project-local`, `round-trip`, `sub-agent` は複合設計語候補。

### 名詞一覧

- `reports/doc-lint-noun-list-20260517133815.tsv`
- `reports/doc-lint-noun-extraction-20260517133815.md`
- 目的: 固有名詞以外も含め、SudachiPy が名詞と判定した日本語 / カタカナ語を全件収集。
- 集計:
  - 名詞 entry 数: 1,020
  - japanese: 937
  - katakana: 83
- カタカナ名詞の代表:
  - `コメント`, `ログ`, `ファイル`, `セット`, `テスト`, `コンポーネント`, `ユーザー`, `タスク`, `レビュー`, `データ`, `モデル`, `スキップ`, `フェーズ`, `リスク`, `アルゴリズム`, `ボタン`, `コスト`, `スクロール`, `ベース`, `メタ`, `ラベル`

### 同義語候補

- `reports/doc-lint-synonym-candidates-20260517165117.tsv`
- `reports/doc-lint-synonym-extraction-20260517165117.md`
- 目的: ChikkarPy の synonym grouping を使い、設計書内で複数表記が実際に出ている候補を整理。
- 集計:
  - ChikkarPy 同義語グループ: 297
  - 文書内で 2 表記以上が観測されたグループ: 137
  - 複数種別を含むグループ: 103
  - 日本語だけで揺れているグループ: 34
  - カタカナ語を含むグループ: 50
  - 既存 whitelist で alias を持つ entry: 50
- 優先確認候補:
  - `記録 / log / ログ`
  - `source / ソースコード`
  - `field / 領域`
  - `設定 / setting`
  - `test / 試験 / 検査 / テスト`
  - `保存 / 保管`
  - `robot / ロボット`
  - `alignment / 整列`
  - `選択 / choice`
  - `出力 / output / 成果物`
  - `更新 / update`
  - `状態 / condition`
  - `file / ファイル`
  - `metadata / メタデータ`
  - `official / 公式`
  - `文書 / ドキュメント`
  - `入力 / Input`
  - `sample / 標本 / サンプル`
  - `rule / 規則 / ルール`
  - `request / 要求 / 要望`

## 決定済み事項

### 単独英単語の扱い

決定:

- 単独英単語は原則 whitelist に入れない。
- 必要ならカタカナ表現を優先する。
- ただし、日本語文書でも通常英語表記する略語は英語維持。

英語維持候補:

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

製品名・固有名詞・技術名として英語維持候補:

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

### 複合語優先

決定:

- `tracker`, `snapshot`, `source`, `field`, `packet`, `diagnostics`, `sidecar`, `profile`, `frame`, `tick`, `replay`, `alignment`, `geometry`, `render`, `state`, `timeline`, `metadata`, `official`, `sample` などは単独登録しない。
- `tracker snapshot`, `source key`, `field geometry`, `diagnostics sample tick`, `render snapshot` のような複合語で意味を固定する。

### `Tracked`

決定:

- `Tracked` 単体は reader-hostile なので避ける。
- 本文では `Tracked 画面`, `Tracked 表示`, `Tracked 状態` のように対象を補う。
- UI ラベルそのものを示す場合だけ `` `Tracked` `` のようなコード / ラベル表現として扱える。

未実装:

- `tools/lint/markdown-whitelist.yaml` から単独 `Tracked` はまだ削除または変更されていない。
- `Tracked 画面` / `Tracked 表示` の whitelist entry はまだ追加されていない。
- 既存文書の `Tracked` prose もまだ修正していない。

## 未解決項目

1. `Tracked` 方針を whitelist / prh / 文書修正のどこで enforce するか。
2. `Tracked` 単体 term を削除するか、UI ラベル限定 description として残すか。
3. `Tracked 画面`, `Tracked 表示`, `Tracked 状態` のどれを whitelist term とするか。
4. `Play`, `Stop`, `Off`, `Compare` も `Play ボタン` のように文脈付き phrase へ寄せるか。
5. 単独英単語からカタカナへ寄せる prh 候補をどこまで作るか。
6. `source`, `field`, `log`, `sample` のような多義語をどう細分化するか。
7. ChikkarPy の長文分割対応を SKILL 側スクリプトへ入れるか。現状は抽出時の一時ラッパーで回避しただけ。
8. ChikkarPy 導入時の pip workaround を IbisDuck の setup docs にさらに明記する必要があるか。
9. 未追跡レポート群を commit するか、整理するか。

## 環境・失敗情報

### SudachiPy 入力長上限

通常実行:

```bash
npm run -s lint:md:vocab -- --format json
```

長い Markdown で次の失敗が出た。

```text
SudachiError: Input is too long, it can't be more than 49149 bytes
```

該当した代表ファイル:

- `Tracker/Design/Core/tracker-architecture-plan.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`

今回の抽出では対象 20 ファイルを固定し、長いファイルを一時ラッパー内で分割して回避した。

### ChikkarPy install

通常の pip install は Python 3.12 の build isolation 内で `pkg_resources` が見つからず失敗した。

回避実績:

```bash
/tmp/ibisduck-sudachi-venv/bin/python -m pip install 'setuptools<81' wheel==0.47.0 vcs-versioning==1.1.1
/tmp/ibisduck-sudachi-venv/bin/python -m pip install --no-build-isolation chikkarpy==0.1.1 PyYAML==6.0.3
```

メモリ上の推奨は `PIP_NO_BUILD_ISOLATION=1 python3 -m pip install -r tools/lint/requirements.txt`。

## 次チャットで最初に確認すること

1. `AGENTS.md` を読み、`development-orchestrator` を入口にする。
2. SKILL リポジトリの現状を確認する。
3. `markdown-whitelist-maintenance` skill が存在するか確認する。今回の確認時点では見つからなかった。
4. IbisDuck の branch と未追跡レポート群を確認する。
5. `tools/lint/markdown-whitelist.yaml` の `term: Tracked` を確認する。
6. `reports/doc-lint-synonym-candidates-20260517165117.tsv` を起点に、`prh候補`, `whitelist維持候補`, `要確認候補` の 3 分類を作る。
7. まず `Tracked` の具体方針を利用者に提案する。勝手に whitelist / prh を編集しない。

## 次チャット用プロンプト

次の文を新しいチャットに貼れば再開できる。

```text
/home/ibis/ssl/IbisDuck で Markdown 用語整理を続けてください。

最初に AGENTS.md に従い development-orchestrator を確認し、関連 skill があるかを疑ってください。特に review-enforcer、report-output-manager、design-doc-maintainer、もし存在すれば markdown-whitelist-maintenance を確認してください。

現在の目的は tools/lint/markdown-whitelist.yaml と tools/lint/prh.yml の整備です。whitelist は許可する語と意味、prh は直したい表記揺れに分けます。ChikkarPy の synonym は候補グループ化の手がかりであり、自動で aliases や prh に変換しないでください。whitelist 修正は私の明示レビュー必須です。

ここまでに次の未追跡レポートがあります。
- reports/doc-lint-proper-noun-extraction-20260517133249.md
- reports/doc-lint-noun-extraction-20260517133815.md
- reports/doc-lint-noun-list-20260517133815.tsv
- reports/doc-lint-synonym-extraction-20260517165117.md
- reports/doc-lint-synonym-candidates-20260517165117.tsv
- reports/doc-lint-handover-20260519113733.md

ユーザー方針:
- 単独英単語は基本的にカタカナ表現を優先します。
- ただし UI, CSV, PC のように略語で日本人も通常英語表記するものは例外です。
- 複合語登録を優先してください。
- render snapshot のような確立済み複合 technical phrase は無理に日本語化せず、そのまま複合語として扱ってください。
- alias に意味が違うものを混ぜないでください。意味が違うなら別 term に分けてください。
- Tracked 単体ではなく、Tracked 画面のように文脈を補う方針です。Tracked 単体は何を指すか分かりにくいので避けたいです。

現状確認済み:
- tools/lint/markdown-whitelist.yaml:43 に term: Tracked が単独登録されています。
- そのため現状では Tracked 単体の prose が通ります。
- 仕組みとしては Tracked 画面 / Tracked 表示 のような phrase を whitelist term にできます。
- ただし単独 Tracked を削除または narrow しないと、compound を追加しても bare Tracked は通り続けます。
- backtick 内の `Tracked` はコード / UI ラベル扱いで prose lint 対象外です。

まずやってほしいこと:
1. reports/doc-lint-synonym-candidates-20260517165117.tsv と既存 whitelist を使い、prh候補 / whitelist維持候補 / 要確認候補 の 3 分類案をレポートとして出してください。
2. 特に Tracked の扱いを最初の提案に含めてください。
3. まだ tools/lint/markdown-whitelist.yaml や tools/lint/prh.yml は編集しないでください。提案を先に出してください。
```

## 検証状態

- この引き継ぎメモ作成では、Markdown lint は実行していない。
- `reports/` は `tools/lint/markdown-targets.json` で通常の Markdown lint 対象外。
- ホワイトリスト本体と prh は未変更。
- SKILL リポジトリは clean を確認済み。
