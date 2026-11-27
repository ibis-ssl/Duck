# Markdown ホワイトリスト更新・ローカル検証報告

## 対象と結果

- 作業日: 2026-09-15（日本時間）。
- リポジトリ: `ibis-ssl/Duck`。対象: PR #20、`docs/runtimehost-readme-appsettings`。
- 開始 HEAD: `bf18194ea1b877851ce9c2d573fb0e5df55cebae`。
- PR の基点: `main`、`f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`。
- 設定変更 HEAD: `98ddb777e97cb264366e48d54c40e7d5fc8b3985`。
- 設定変更: `tools/lint/markdown-whitelist.yaml` のみ、63 行追加。147 項目から 170 項目へ増加。
- 新規 23 項目、新規項目内の別表記 5 件、既存項目への別表記追加 5 件。新たに許可する表記は合計 33 件。
- ローカル実行経路: RDC、`/home/ibis/ssl/IbisDuck`。GitHub への登録と PR 更新は GitHub connector を使用。
- 個別検証は 90 件すべて期待どおり。全体の Markdown lint は未通過。
- この報告と検証 JSON は設定変更後の記録用コミットに保存する。記録用コミット自身の SHA と最終 HEAD の CI は PR コメントへ記録する。

## 利用者の方針と変更範囲

読みやすさを優先する。製品名・略語を除く技術用語は、クラス、メソッド、引数、設定などの名称に対応する概念だけ英語の許可候補とする。コードとの対応がない設計上の説明語は日本語へ寄せる。

今回の依頼は、会話で精査した候補のホワイトリスト反映と、ローカル lint 環境の整備・検証である。文書全体の翻訳、既存 147 項目の再設計、共有検査スクリプトの改修は含めない。英語を許可することは、その語を本文で常に英語にすることを意味しない。

`fixture`、`assertion`、`metadata` の単独登録や、不明瞭なカタカナへの一括置換は行っていない。`best-effort`、`field-first`、`control-only` も追加していない。`source`、`snapshot`、`process`、`noise` を単独で許可する拡張は行っていない。

## 追加した語と根拠

| 区分 | 追加した項目 | 扱い |
| --- | --- | --- |
| 略語・形式 | XML、SVG、DI、LINQ、CI、OS、NIC、URL、SSL | 文書内での意味を説明する。SSL は競技区分として定義する。 |
| 製品・技術名 | Java、xUnit.net、Protocol Buffers、gzip | xUnit、protobuf をそれぞれ同じ概念の別表記として登録する。 |
| 名称 | Duck、TIGERs Mannheim、README、Web UI | TIGERs を短縮表記として登録する。 |
| コードに対応する語 | Kalman filter、process noise、measurement noise、wall-clock、round-trip、tie-break | 意味と対応する識別子を説明に残す。ProcessNoise、MeasurementNoise も別表記として登録する。 |

| 既存項目 | 追加した別表記 |
| --- | --- |
| Tracker.DebugHost | DebugHost |
| Tracker.RuntimeHost | RuntimeHost |
| Tracker.CaptureReplay | CaptureReplay |
| カルマン | Kalman |
| event time | event-time |

既存項目の説明、既存の別表記、項目順を維持している。新規項目は末尾に追加した。変更前後の YAML を解析し、指定した追加以外の意味上の差分がないことを確認した。

### ソースコードとの対応

| 語 | 確認箇所 |
| --- | --- |
| Kalman filter / Kalman | `Tracker/Tracker.Core/Engine/TrackerEngine/Kalman.cs:142` の `KalmanAxisState`。 |
| process noise | `Tracker/Tracker.CaptureReplay/ReplaySettingsOptions.cs:122` の `ProcessNoise`、同ファイルの `KalmanProcessNoiseScale`。 |
| measurement noise | `Tracker/Tracker.CaptureReplay/ReplaySettingsOptions.cs:127` の `MeasurementNoise`、同ファイルの `MeasurementNoiseVarianceScale`。 |
| wall-clock | `Tracker/Tracker.DebugHost/Components/Pages/DiagnosticsPlaybackState.cs` の `GetRealtimePlayIndex` にある `startWallClock` と `currentWallClock`。 |
| round-trip | `Tracker/Tracker.Tests/TrackerComparisonSourceTddTests.cs:29` の `TrackerSnapshotSidecar_RoundTripPayload_RestoresRawTrackerPacketForReplayDecode`。 |
| tie-break | `Tracker/Tracker.Tests/Contracts/TrackerPacketGeneratorContractTests.cs:59` の `Generate_WhenSecondaryBallsTieOnVisibilityAndTimestamp_UsesInternalTrackIdAsFinalTieBreaker`。 |
| event-time | 既存 `event time` と同じ観測時刻の概念。`EventTimestampNs` などの名前を持つ時刻処理を確認した。 |

`tie-break` の上記根拠は、同条件の球の並び順を内部 ID で決める処理である。別の表示元選択などに同じ説明を流用しない。説明文は人が意味を確認するための情報であり、現行検査器が使用文脈まで自動判定するわけではない。

## ローカル lint 環境

- Node.js: `v22.13.1`。npm: `11.4.2`。Python: `3.12.3`。
- Node.js 側は既存環境を確認。`npm ls --depth=0` は成功。cspell `9.8.0`、textlint `15.7.0`、textlint-rule-prh `6.1.0`、yaml `2.9.0`。
- Python 仮想環境に不足していた ChikkarPy を、リポジトリの指定どおり `0.1.1` で導入した。
- 導入コマンド: `.venv/bin/python -m pip install --no-build-isolation -r tools/lint/requirements.txt`。
- SudachiPy `0.6.11`、辞書 `20260428`、PyYAML `6.0.3`。`pip check` は成功。
- ChikkarPy を含む語彙抽出は `tools/lint/README.md` に対して成功し、同義語候補を持つ JSON 出力を確認した。

Python 側の全対象列挙に、仮想環境の依存物の文書が混入していた。検査対象の除外設定を広げず、環境本体を次の場所へ移した。元の `.venv` と `.codex-doc-lint-venv` はシンボリックリンクとして保持している。

```text
/home/ibis/.local/share/duck-doc-lint/duck-whitelist-20260915T084730/venv
/home/ibis/.local/share/duck-doc-lint/duck-whitelist-20260915T084730/codex-doc-lint-venv
```

移動後も `.venv/bin/python -m pip check` は成功。依存物の文書を含む Python 側の列挙は 103 件から 21 件へ減った。内訳は通常の Markdown 20 文書と、既存処理が対象にする `tools/lint/requirements.txt`。文書を削除したり、追跡対象の除外規則を追加したりはしていない。ローカルのシンボリックリンクだけを `.git/info/exclude` へ追加した。

通常の起動方法は従来どおりである。ただし、後述の未解消事項があるため全体 lint の終了コードは非ゼロになる。

```bash
cd /home/ibis/ssl/IbisDuck
. .venv/bin/activate
npm run lint:md
npm run lint:md:whitelist -- --list-unknown
```

## 検証結果

設定変更前に追加候補を入力すると許可一覧検査が終了コード 1 で拒否し、変更後は許可することを確認した。設定更新に対する前後比較であり、製品コードの TDD 実装を行ったという意味ではない。

| 検証 | 結果 |
| --- | --- |
| YAML 構文、既存項目の維持、追加差分 | 成功。既存項目の意図しない変更なし。 |
| 許可一覧検査: 追加 33 表記を 1 件ずつ入力 | 33 件すべて終了コード 0。 |
| 許可一覧検査: 未登録 12 表記を 1 件ずつ入力 | 12 件すべて終了コード 1。意図どおりの拒否。 |
| cspell: 追加 33 表記を 1 件ずつ入力 | 33 件すべて終了コード 0。各回の検査対象は 1 文書。 |
| cspell: 未登録 12 表記を 1 件ずつ入力 | 12 件すべて終了コード 1。各回の検査対象は 1 文書。 |
| 個別ケース合計 | 90 件中 90 件が期待どおり。 |
| `git diff --check` | 成功。 |
| 全体 `npm run lint:md` | 変更前後とも終了コード 123。textlint の後、cspell で失敗。許可一覧検査には進まない。 |
| 全体 cspell の表示件数 | 20 文書中 11 文書に指摘。変更前 1,029 件、変更後 1,026 件。 |
| 英語のみの補助集計 | 20 文書で、未登録語の出現は 7,985 件から 7,768 件、異なり語は 807 語から 783 語へ減少。 |
| 全体の許可一覧検査を単独実行 | 終了コード 1。SudachiPy の入力長上限で停止。 |
| 許可一覧の値・説明文検査 | 変更前後とも終了コード 1。未登録の日本語・カタカナを検出。未登録英語は 0。 |

英語のみの補助集計は、共有検査器の Markdown 除去・許可語マスク・英語検出処理を使った全件集計である。日本語検査を含む全体 lint 成功の代用にはしない。cspell の表示指摘数と、補助集計の全出現数は異なる指標である。

個別検証の入力一覧と集計は [検証 JSON](markdown-whitelist-validation-20260915084730.json) に保存した。未登録側は `UnregisteredEnglish`、`best-effort`、`field-first`、`control-only`、`source`、`snapshot`、`fixture`、`assertion`、`metadata`、`process`、`noise`、`Kalmanish`。

検証は以下へ 1 表記ずつ標準入力を渡して実行した。通常の文章を逆引用符で囲んで検査から逃がす方法は使っていない。

```bash
.venv/bin/python .agents/skills/review-enforcer/scripts/check-markdown-whitelist-sudachi.py --stdin tools/lint/README.md --list-unknown
node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js --no-progress stdin://tools/lint/whitelist-probe.md
```

初回の cspell 試行ではリポジトリ外の入力ファイルが選択されず、検査対象 0 文書・終了コード 1 だった。この試行は成功に数えず、リポジトリ内の仮想パスを持つ標準入力へ切り替えた。複数表記を一括入力すると改行をまたいだ複合語として解釈され得るため、最終の 90 ケースは各表記を独立して検査した。

## 未通過の理由と残る作業

### 本文の未登録英語

許可追加だけで全文を通す方針は取っていない。コードとの対応がない設計語や普通の説明語が本文に残る。これらは文脈を確認した日本語への書き換えが必要であり、今回のホワイトリスト更新では本文を変更していない。

### 説明文の日本語・カタカナ

許可一覧を入力として値と説明文を検査した際の未登録語種は、変更前 197 種から変更後 236 種となった。追加説明文により、この入力範囲で新たに検出された語は 39 種である。したがって、この失敗をすべて既存問題とは扱わない。

新たな検出語は「一式、並び順、予測、利用方法、動作、区分、区別、参照元、圧縮、基本的、変換、小型機、必要、接続、時計、条件、案内、検索、検証、概要、画像、統合、継続的、自動、装置、規則、言語、計算機、説明、資源、通信網、部門、集合、コメント、サッカー、チーム、テスト、ブラウザー、ロボット」。ブラウザーの正規形はブラウザ。これらを検査通過のためだけに一括登録していない。日本語の許可範囲は別途確認が必要である。

### 長文の処理上限

共有の `check-markdown-whitelist-sudachi.py` は文章全体を一度に形態素解析するため、49,149 バイトを超える入力で停止する。今回の処理済み入力は、`tracker-architecture-plan.md` が 68,793 バイト、`debug-host-cli-ui-detail-design.md` が 60,053 バイトだった。共有スクリプトや長文文書は変更せず、この制限を残した。

### 検査範囲と表記の限界

現行の許可一覧検査は単語・複合語の表記を許可する仕組みであり、同じ英語が別概念に使われていないかまでは判定しない。説明文に記載した意味との一致は本文レビューで確認する。大文字小文字の統一も、この更新だけで強制されるわけではない。

## CI と診断記録

設定変更 HEAD `98ddb777e97cb264366e48d54c40e7d5fc8b3985` に対し、`pull_request` 起動の `.NET tests`、run `34911155655` の `head_sha` 一致と成功を確認した。これは .NET の CI 結果であり、Markdown lint の成功を意味しない。

開始時のローカル追跡ファイルに `.github/workflows/*` はなく、上記 run が示す `.github/workflows/dotnet-test.yml` は PR 基点からの取得でも見つからなかった。実行の存在は確認したが、定義の取得元はこの作業では確定できていない。上記成功 run の artifact 一覧は 0 件だった。RevMem 実装向けの診断 workflow 追加方針を、この Duck の設定更新へ拡大適用していない。

ローカルではコマンド結果、標準出力、標準エラー、前後の許可一覧、個別 90 ケースの結果、英語の全出現一覧、環境移動記録を以下へ保存した。

```text
/home/ibis/.local/share/duck-doc-lint/duck-whitelist-20260915T084730/evidence
```

作業用の元記録は `/tmp/duck-whitelist-20260915T084730` にも残る。記録用コミット後の CI は、その新しい HEAD に一致する run を改めて確認し、PR コメントに記録する。上記の設定変更 HEAD の run を、別 HEAD の CI の代用にはしない。

## 運用手順・保存・引き継ぎ

`AGENTS.md` と `development-orchestrator` を入口に、`work-context-manager`、`chat-implementation-worker`、`implementation-worker`、`markdown-word-checker`、`report-writer`、`chat-handoff-manager` の役割分担を確認した。共有手順リポジトリは `106ea5dcf12c4805756351fb9381df220b94f044` で GitHub の main と一致し、未変更である。Serena はローカル実行コマンド・設定済み接続に見つからず、利用可能なプラグイン検索でも見つからなかったため使用していない。

GitHub connector へ登録したホワイトリストの blob SHA は `8e4d12653a2ec2bcfa20bc6739080fc363fb263d`。RDC で検証したファイルの `git hash-object` と一致することを確認してからコミットした。ローカルにも、GitHub が返したコミット情報から SHA が一致する Git オブジェクトを取り込み、同一内容へ同期した。

この報告は実装・検証報告であり、独立レビュー合格を宣言するものではない。製品コードと設計書、`prh.yml`、対象除外設定、共有スクリプト、依存関係の指定は変更していない。`reports/**` は既存の通常 lint 対象外であり、この報告の保存を全体 lint 通過とは扱わない。

引き継ぎ先は、この報告と検証 JSON、PR の現在 HEAD を確認する。全体 lint の未登録語整理と共有検査器の長文対応が残るため、`DOC-LINT-003` 全体を完了にはしていない。次の作業範囲や追加許可は利用者の指示を受けて決める。merge は実施していない。
