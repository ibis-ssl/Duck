# 固有名詞候補抽出レポート

## 概要

SKILL 側リポジトリ PR37 で導入された SudachiPy ベースの語彙抽出を前提に、IbisDuck 側の設計書、進捗ファイル、README、lint 設定メモからホワイトリスト候補になる固有名詞、識別子、設計語を抽出した。

このレポートでは、ホワイトリストへそのまま追加する一覧ではなく、利用者レビュー用の候補として整理する。ホワイトリスト本体は編集していない。

## 実行条件

- リポジトリ: `/home/ibis/ssl/IbisDuck`
- 対象一覧取得: `npm run -s lint:md:targets`
- 語彙抽出: `.agents/skills/review-enforcer/scripts/extract-markdown-vocabulary-sudachi.py`
- 依存関係: `tools/lint/requirements.txt`
- 一時実行環境: `/tmp/ibisduck-sudachi-venv`

通常の `npm run -s lint:md:vocab -- --format json` は、長い Markdown ファイルで SudachiPy の 1 入力上限に当たり失敗した。そのため今回は、正式対象 20 ファイルを固定し、長いファイルを抽出時だけ分割して処理した。

## 対象ファイル

```text
AGENTS.md
feedback-points/feedback-points.md
README.md
tools/lint/README.md
Tracker/Design/Archive/Core/phases-status.md
Tracker/Design/Archive/Core/tasks-status.md
Tracker/Design/Archive/DebugHost/phases-status.md
Tracker/Design/Archive/DebugHost/tasks-status.md
Tracker/Design/Core/tracker-architecture-plan.md
Tracker/Design/Core/tracker-core-engine-detail-design.md
Tracker/Design/Core/tracker-history-000-038.md
Tracker/Design/Core/tracker-test-maintainability-detail-design.md
Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md
Tracker/Design/DebugHost/debug-host-maintainability-design.md
Tracker/Design/DebugHost/raw-vision-viewer-plan.md
Tracker/Design/phases-status.md
Tracker/Design/RuntimeHost/runtime-host-plan.md
Tracker/Design/tasks-status.md
Tracker/Tracker.CaptureReplay/README.md
Tracker/Tracker.DebugHost/README.md
```

## 集計

- 抽出語彙 JSON: 1,902 entry
- ホワイトリスト候補 TSV: 279 entry
- 既存ホワイトリストでカバー済み: 57 entry
- 未登録候補: 222 entry
- 候補種別: english 193、katakana 83、japanese 3

抽出結果の詳細は一時成果物として次に保存した。

- `/tmp/ibisduck-vocab.json`
- `/tmp/ibisduck-proper-noun-candidates.tsv`
- `/tmp/ibisduck-md-targets.txt`

## 既存ホワイトリストでカバー済みの代表例

```text
UI
PR
CaptureOn
ID
Tracker.DebugHost
SSL-Vision
AutoRef
CLI
JSONL
UDP
Tracker.RuntimeHost
API
DTO
SudachiPy
TDD
ER-FORCE
JSON
Blazor
FastForward
Tracker.CaptureReplay
HTTP
same-source
Codex
Docker
Markdown
NuGet
Serena
TrackerCoordinator
SSL_WrapperPacket
カルマン
```

## 追加レビュー優先候補

次は固有名詞、製品名、技術名、型名、ファイル名、UI 表示名として扱う可能性が高い。

| 候補 | 出現数 | 判断メモ |
| --- | ---: | --- |
| DebugHost | 32 | `Tracker.DebugHost` の短縮表記。短縮表記も許可するか判断が必要。 |
| RuntimeHost | 23 | `Tracker.RuntimeHost` の短縮表記。短縮表記も許可するか判断が必要。 |
| Kalman | 30 | カルマンフィルタ文脈の英字表記。`カルマン` は既存登録済み。 |
| Tigers | 19 | TIGERs / Tigers 系の外部トラッカー参照。正式表記を確認する。 |
| CaptureReplay | 4 | プロジェクト名または名前空間短縮表記。 |
| ASP.NET | 2 | 技術名。 |
| Java | 2 | 技術名。 |
| SDK | 2 | 技術略語。 |
| CI | 1 | 技術略語。 |
| DI | 1 | 技術略語。 |
| LINQ | 1 | 技術名。 |
| xUnit | 1 | テストフレームワーク名。 |
| OS | 1 | 技術略語。 |
| NIC | 4 | ネットワークインターフェース文脈の略語。 |
| SVG | 5 | 画像形式名。 |
| XML | 12 | 文書形式名。 |
| NET | 6 | `.NET` 由来の抽出揺れ。ホワイトリスト登録より抽出側の扱い確認が必要。 |
| appsettings.json | 1 | ファイル名。 |
| Home.razor | 1 | Razor コンポーネント名。 |
| VisionBallMarker.razor | 1 | Razor コンポーネント名。 |
| VisionDetailsPanel.razor | 1 | Razor コンポーネント名。 |
| VisionFieldCanvas.razor | 1 | Razor コンポーネント名。 |
| VisionFieldLines.razor | 1 | Razor コンポーネント名。 |
| VisionRobotMarker.razor | 1 | Razor コンポーネント名。 |
| rawPayloadRestored | 1 | 識別子。 |
| trackedFrame | 2 | 識別子。 |

## UI 表示名として判断する候補

次は英単語単体に見えるため、単語登録よりも UI 表示名または複合語として扱う方が安全。

| 候補 | 出現数 | 推奨判断 |
| --- | ---: | --- |
| Play | 42 | UI 表示名として許可する場合のみ登録。 |
| Forward | 26 | 単体登録は避け、`Fast Forward` として扱う方がよい。 |
| Stop | 16 | UI 表示名として許可する場合のみ登録。 |
| Off | 8 | UI 表示名として許可する場合のみ登録。 |
| Compare | 2 | UI 表示名または機能名として扱うか確認。 |

## 設計語として判断する候補

次は固有名詞ではないが、設計書中で意味を持つ複合語である。ホワイトリスト方針では単語単体より複合語を優先するため、説明付き登録候補として扱える。

```text
camera-local
best-effort
capture-time
latest-before
session-relative
wall-clock
control-only
multi-camera
read-side
tie-break
event-time
top-level
view-state
time-sync
type-owned
uncertainty-weighted
field-first
geometry-only
nearest-after
not-created
observer-event
profile-aware
project-local
round-trip
sub-agent
```

## SudachiPy の固有名詞判定

SudachiPy の品詞として `固有名詞` になったものは次の 4 件だった。

| 候補 | 出現数 | 判定 |
| --- | ---: | --- |
| 実時 | 4 | `実時間` などからの誤判定に見える。登録非推奨。 |
| 本開発 | 1 | 通常語の誤判定に見える。登録非推奨。 |
| 見方 | 1 | 通常語の誤判定に見える。登録非推奨。 |
| カルマン | 1 | 固有名詞または技術用語として妥当。既存ホワイトリストでカバー済み。 |

## カタカナ一般語

次は出現数が多いが、固有名詞ではなく一般語である。ホワイトリストへ追加する場合は、単語単体ではなく意味を限定できる複合語を優先する。

```text
コメント
ログ
ファイル
セット
テスト
コンポーネント
ユーザー
タスク
レビュー
データ
モデル
スキップ
フェーズ
リスク
アルゴリズム
ボタン
コスト
スクロール
ベース
ラベル
```

## 注意点

`extract-markdown-vocabulary-sudachi.py` と `check-markdown-whitelist-sudachi.py` は、長い Markdown をそのまま SudachiPy に渡すため、現状では 49KB 超の入力で失敗する。今回の対象では次のファイルが該当した。

```text
Tracker/Design/Core/tracker-architecture-plan.md
Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md
```

また、一時 Python 仮想環境をリポジトリ配下に作ると、lint target の全探索に仮想環境内のライセンス文書が混入し得る。実運用ではリポジトリ外に仮想環境を置くか、`tools/lint/markdown-targets.json` の ignore 対象へ追加する必要がある。

## 次の判断ポイント

1. `DebugHost` / `RuntimeHost` の短縮表記を、正式な `Tracker.DebugHost` / `Tracker.RuntimeHost` の alias として許可するか。
2. `Kalman` を `カルマン` の alias として許可するか、英字表記を別 entry にするか。
3. `Tigers` / `ER-FORCE` / `SSL-Vision` など外部プロジェクト名の正式表記を揃えるか。
4. UI 表示名の `Play` / `Fast Forward` / `Stop` / `Off` を許可語として扱うか。
5. `camera-local` などの設計複合語を、説明付きで段階的に追加するか。
6. SudachiPy スクリプトの長文分割対応を SKILL 側または IbisDuck 側の tooling として直すか。
