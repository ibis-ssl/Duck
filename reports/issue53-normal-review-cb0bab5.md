# Issue #53 通常レビュー

## 判定と対象

- review mode: initial / normal review
- verdict: `pass_with_held`。今回の「既存の Duck tracker 受信診断を participant-status 成果物へ保存する」変更について、必須修正指摘はない。
- Issue #53 / `ASPIRE-MATCH-004` の実通信受入は **未完了**。本判定はライブ受入合格、Issue 完了、独立最終レビュー完了、マージ許可を意味しない。
- repository: `ibis-ssl/Duck`
- branch: `task/issue53-aspire-006g3`
- base: `9756f9dd35ce05cf18d6ad91eaf277ad917fcfe9`
- reviewed_implementation_head: `cb0bab5943d68e5d1d95a294096c792392a5b45a`
- current_head（レビュー開始時・報告書保存直前）: `cb0bab5943d68e5d1d95a294096c792392a5b45a`
- range: `9756f9dd35ce05cf18d6ad91eaf277ad917fcfe9..cb0bab5943d68e5d1d95a294096c792392a5b45a`、実装コミット1件。
- reviewer: 独立した通常レビュー担当 `/root/issue53_normal_review_cb0bab5`。本担当は実装・実装修正をしていない。実装担当自身の差分点検とは別のレビューである。独立最終レビュー担当ではない。
- 実行ユーザー: `whoami` の実測 `fa780\donabe`。`CodexSandboxOffline` は使用していない。
- persistence: `repository_file`。許可された保存先は本報告書のみ。実装、設計、試験、workflow、既存報告書は変更していない。
- reserved_report_paths: `reports/issue53-normal-review-cb0bab5.md`
- report_attestation_allowed: `false`。独立最終レビューの行政的追認コミット例外は今回の通常レビューには適用しない。
- initial_independent_reviewed_head / closure_reviewed_heads / report_attestation_head: 該当なし / 空 / null。

## 要求と範囲

Issue #53 の原文を `gh issue view 53 --repo ibis-ssl/Duck --json number,title,state,body,url` で読み取った。Issue は OPEN で、実パケットによる Sumatra 11010、AutoRef 10020 / 11003 / 11010、Duck 11010 非送信の packet count とログによる確認を要求する。OS、Docker バージョン、ネットワーク設定、受信数、送信元識別、標準出力・標準エラーを残し、失敗時も途中の証拠を保存する。未実施を成功とせず、完了にはその時点の HEAD に一致する検証と独立レビューが必要である。

今回の候補は、その受入に向けた保存経路の追加である。`/health/vision` が返す既存の `ServiceReceiveSnapshot` を、Duck の受信側 identity、source name/UUID/key、remote endpoint、受信件数、失敗件数、受信世代、時刻・鮮度とともに保存する。AutoRef 入力 counter、新たな UDP 観測器、Duck publisher の実効状態や送信 counter は追加していない。#52 統合後の基点を使用し、#54 の `POST_GAME` 条件は範囲外である。

基準設計と現行 history の `ASPIRE-MATCH-004` には AutoRef の tracker「入力」とする旧記述がある。今回の tasks-status は 10020 / 11003 入力、11010 出力を明記する。保存済み run の送信ログを根拠とした整理であり、今回新たに AutoRef イメージ内部の仕様を実証したものではない。この方向の不確実性は H53-NR-003 に保持する。

## 作業環境と検証状態

| 項目 | 観測・状態 |
| --- | --- |
| machine | `FA780` |
| OS | `Microsoft Windows NT 10.0.22631.0` |
| shell | PowerShell `7.4.22` |
| absolute working directory | `C:\Users\donabe\Documents\Codex\2026-11-07\task\Duck-issue53-aspire-006g3` |
| source state | 読取開始時と保存直前はいずれも clean、指定 branch / HEAD と一致 |
| ownership / writes | current task のレビュー。許可書込みは本報告書のみ |
| verification_capability | `local_execution_available`。PowerShell executor を使用でき、`Get-Command dotnet` は `C:\Program Files\dotnet\dotnet.exe` を返した。実装担当による focused test 成功報告もある |
| dependencies | git / gh は今回の読取コマンドが成功。dotnet は実行ファイルの存在を確認。SDK・全依存・Docker・Linux 環境の実行可用性は本レビューでは未確認 |
| technical_head | `cb0bab5943d68e5d1d95a294096c792392a5b45a` |
| commit | 実装は committed。本報告書の commit は本担当では実施しない |
| push | 本担当は実施せず。公開状態は未確認 |
| ci_wait | 本担当は開始せず。対象 HEAD の matching run は0件 |
| full_local_equivalence_gate | not_started。本レビューはそのゲートを実行していない |

ローカル CodexSkill の `work-context-manager`、`review-worker`、`report-writer` を読んで適用した。repository と task から上位3階層までの `AGENTS.md` は `Test-Path` で存在しなかった。レビューは読取・報告書保存に限り、Python、Docker、workflow、ビルド、テストを実行していない。

## 確認した変更と依存関係

変更ファイル6件を対象に、全差分、実装・試験ファイル、追加報告書、および設計・追跡文書の変更部分と関連する要求を確認した。

| 変更ファイル | 確認内容 |
| --- | --- |
| `Testing/Duck.Testing.AppHost/MatchParticipantStatusProjection.cs` | snapshot の追加フィールド、loop の保存、HTTP応答からの伝播、not-ready、失敗・取消、既存の inspect / generation 検証、atomic write |
| `Tracker/Tracker.Tests/MatchParticipantStatusProjectionTests.cs` | 追加の round-trip / not-ready 伝播テスト、および既存の readiness / generation / recorder / atomic write テスト |
| `Tracker/Design/tasks-status.md` | G3 の通信方向の明記、隣接 G2 / G4 との境界 |
| `Tracker/Design/phases-status.md` | #52 統合、#53 未完了、旧 run の HEAD、証拠不足、#54 との境界 |
| `reports/issue53-implementation-20261108.md` | 保存対象・非対象、実行コマンドと結果の自己報告、統合実行なし・Linux 受入なしの明示 |
| `reports/issue53-intake-verification-20261108.md` | 過去 run の証拠と限界、AutoRef 方向不一致、counter / sender attribution / 非送信の不足 |

直接依存・関連経路として、以下を確認した。

- `Testing/Duck.Testing.Diagnostics/ServiceReceiveDiagnostics.cs`、`ResourceDiagnosticIdentity.cs`: immutable snapshot の各型、identity の制約、source別 counter、receiver epoch、未観測時の値、decode failure の意味。
- `Tracker/Tracker.RuntimeHost/RuntimeVisionDiagnosticsHttpService.cs`、`RuntimeVisionReceiverService.cs` の診断クラス、`RuntimeTrackerReceiverService.cs`: 実際の HTTP property、HTTP 503 時も返る診断、正常復号・tracked frame の記録、source key の構成。
- `Testing/Duck.Testing.AppHost/TigersTrackerOutputReadiness.cs`: source name `TIGERs`、期待送信元 IP、Duck receiver identity / generation / epoch、鮮度、unique source の選択。
- `Testing/Duck.Testing.AppHost/Program.cs` の match 設定と保存先、AppHost と Tracker.Tests の csproj: participant の順序、bind mount、既存参照、InternalsVisibleTo、PublishUdp=false。
- `Testing/Duck.Testing.RefereeDriver.Host/MatchControllerWorker.cs` の participant 読取・準備判定: 既存DTOは追加 property を消費せず、通常の System.Text.Json 読取で未知propertyを許容する。
- `scripts/verify_aspire_match_acceptance.py`: `_preserve_partial_evidence` による participant-status ファイル全体のコピー、既存受入判定の対象。
- `.github/workflows/dotnet-test.yml` の match 収集、後始末、sanitization、upload 経路、および `scripts/sanitize_aspire_artifacts.py` / `capture_aspire_dcp_logs.py` の秘匿処理。コードを読んだだけで Python / workflow は実行していない。
- `Tracker/Design/Testing/aspire-simulation-test-environment.md` の受信証拠契約と history の G3、MATCH-004、失敗時保存の要求。

## 技術評価

1. 追加フィールドは optional 引数で、既存の snapshot 構築と `Resources` による準備判定を維持する。Web JSON 設定で追加される `duckTrackerReceiveSources` / `duckTrackerDiagnosticsObservedAtUtc` は既存読取側の必須フィールドを変えない。既存の診断 record を再利用し、新しい NuGet 依存や製品通信経路を追加していない。
2. `CollectNativeTrackerOutputCoreAsync` は現在の participant / Duck の full ID・run・network を前後で確認した後に応答を読む。診断の取得後は、source 選択失敗、初回 baseline、件数増分の準備成立の3経路すべてで一覧と取得時刻を返す。HTTP 503 を単独で準備成立とせず、既存の source / identity / count / freshness 条件を維持する。
3. `PreserveTrackerDiagnostics` は取得済みの一覧があれば ready=false でも更新する。一覧が null の失敗・未取得時は最後の一覧と取得時刻を維持する。古い診断が snapshot 全体の新しい `ObservedAtUtc` と並ぶ場合があるが、専用取得時刻と receiver identity / generation が残り、準備判定はこのキャッシュを参照しない。利用側は保存された `IsReady` / age を現在時刻の状態として読み替えてはならない。
4. 出力先は AppHost result directory の `participant-status.json` であり、match-controller の `/match-results` mount、acceptance script のファイル全体コピー、workflow の artifact upload までコード上でつながる。既存の atomic replacement と cancellation の挙動は変更されない。実測 artifact で新フィールドを確認したわけではない。
5. 新テストは ready=false の result から同じ保存 helper を通し、source別件数・UUID・endpoint・Duck receiver identity / epoch・取得時刻が JSON round-trip で保持されることを検査する。構成された HTTP/Docker/BackgroundService loop を実行するテストではない。fixture の AutoRef/Sumatra/Duck 名やゼロ件行は合成値であり、実際の source 名・実測 counter・非送信の証拠ではない。
6. 保存は「最新の取得に成功した診断一覧」1点である。受信計測窓、準備判定に使用した2時点の counter、publisher の full container ID / network との対応表を追加していない。`ServiceReceiveSnapshot.Identity` は Duck **受信側** の identity で、送信元の full ID ではない。受信件数は Duck で正常復号・記録された tracked frame の累積値であり、AutoRef 自身の10020 / 11003入力件数や、あらゆる UDP datagram 数ではない。
7. `ibisduck-tracker` の不在は Duck の11010送信0を証明しない。実際の source key は source name / UUID / endpoint を連結した値であり、一覧の有無、未復号、未観測、観測窓不足を区別する必要がある。既存報告書の live acceptance 未確認という限定を維持する限り、今回の保存追加はこれを合格と誤判定しない。

## Coverage matrix

| 必須観点 | disposition | 根拠・制約 |
| --- | --- | --- |
| 要求・設計適合（今回の保存変更） | `checked_no_finding` | 既存診断の保存を追加。#53全体の受入完了は主張しない |
| 正常系・境界・正当性 | `checked_no_finding` | ready / source選択失敗 / baseline の伝播、既存準備条件の維持 |
| 範囲と無関係変更 | `checked_no_finding` | 6変更ファイル。packet path、Crane、planner、port、protocol、workflowは変更なし |
| 変更ファイルと直接依存 | `checked_no_finding` | 上記の producer → projection → file → reader / artifact 経路を確認 |
| API・データ・互換性 | `checked_no_finding` | optional追加フィールド、Web JSON、既存DTO・scriptの追加property許容 |
| 構成・workflowの影響 | `checked_no_finding` | 既存mountと全体コピー・uploadを利用。実測経路は H53-NR-001 |
| エラー・取消・失敗証拠 | `checked_no_finding` | not-readyでも取得済診断を伝播、null時は前回時刻を保持、既存atomic write維持 |
| セキュリティ・秘密情報 | `checked_no_finding` | 新たな資格情報読取なし。既存診断値のみ保存、既存artifact秘匿経路あり。実測内容は未確認 |
| テスト内容の妥当性 | `checked_no_finding` | round-trip / not-ready helperテストは保存変更に対応。composition未実行を明示 |
| 対象HEADのローカル検証証拠 | `held` | 16/16・diff-checkは実装担当報告。生stdout/stderr未保存、再実行なし（H53-NR-004） |
| 対象HEADのCI | `held` | GitHub APIの head_sha 一致検索は0件（H53-NR-004） |
| 報告・追跡文書の正確性 | `checked_no_finding` | #53未完了、旧run SHA、#54分離を保持。方向の未確定根拠は H53-NR-003 |
| 回帰・保守性 | `checked_no_finding` | 既存record再利用、保存helperのみ追加。既存準備判定へ保存値を流用しない |
| ASPIRE-MATCH-004 実通信・非送信の受入 | `held` | 今回のbounded code review範囲を超える未完了受入（H53-NR-001〜003） |
| 独立最終レビュー・merge | `not_applicable` | 今回は通常レビューのみ |

## Findings

必須修正 finding は **0件**。severity の再分類・転記訂正はない。finding completeness matrix は対象findingがないため空であり、finding closure review は行っていない。

## 保留と未完了の受入

以下は今回の保存実装に対する必須修正 finding ではない。所有者は親担当 `/root` と Issue #53 の受入担当であり、Issue 全体の完了前に解消・判断が必要である。

| identity | disposition / severity | 未完了事項・必要な証拠 |
| --- | --- | --- |
| H53-NR-001 | `held` / finding severityなし | 対象HEADを実行したLinux同一runの新しいparticipant-status。Sumatra / AutoRefの11010出力について、2時点の同一世代counterと増分、UUID / endpoint、送信元full ID / run / network対応、有限な観測窓、OS / Docker / image / network、stdout / stderr、失敗証拠。今回の最新一覧1点だけで増分や完全なsender帰属を証明しない |
| H53-NR-002 | `held` / finding severityなし | AutoRef自身の10020 / 11003受信・処理件数は未証明。DuckやSSL recorderのcounterをAutoRefのcounterに付け替えない。Duck11010非送信について、実効publisher状態・送信数0・適切な観測窓の証拠は未取得。source不在だけを全datagram非送信としない |
| H53-NR-003 | `held` / finding severityなし | AutoRef11010は旧設計で入力と記載され、保存済みruntime logではtracker出力。公開source SHAと固定実行image digestの対応は未証明。送信ログを超えるimage内部仕様や11010受信器の存在を断定しない。方向の要求を受入契約として明確にし、必要なimage/source対応または直接実測を残す |
| H53-NR-004 | `held` / finding severityなし | focused testとdiff-checkの生stdout/stderrは別ファイルに保存されていない。現在HEADのCI / live Linux runはない。親担当は次の検証結果を実際のsource・環境・HEADに結び付け、成功・失敗出力を保存する。報告された16/16をCI成功やlive受入へ読み替えない |

未探索の必須コード領域はない。実環境のHTTP/Docker composition、最終artifactの実測内容、AutoRef image内部counterの可用性、固定image/source対応、Duck実効送信数は上記保留として未検証である。

## 検証証拠の評価

- 実装報告にある最終 focused command は `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -p:IsTestProject=true --no-restore --filter FullyQualifiedName~MatchParticipantStatusProjectionTests --logger "console;verbosity=normal"`。exit 0 / 16件成功と報告されている。実装修正後の結果として親担当から提供されたが、本レビューで再実行・生出力の確認はしていない。
- `git diff --check` は exit 0 / 出力なしと報告されている。今回レビューでは再実行していない。
- 実装報告は submodule不足に起因したcompile失敗、配列初期化の修正、既存NU1902 / ASPIRE010 / analyzer等の警告、後続成功を区別して記録する。最初の出力なしexit 0を試験成功件数に算入しない。生ログがないため詳細原因を独立には確定していない。
- `gh api repos/ibis-ssl/Duck/actions/runs?head_sha=cb0bab5943d68e5d1d95a294096c792392a5b45a&per_page=10` は exit 0、`total_count=0` / runs空。matching current-HEAD CI成功の証拠はない。
- 過去 Linux run `42445437138` の実行HEADは `a330c4ee7c8a457cdb7487ccca79ef31ccdb0ea9` であり、今回の対象HEADではない。intake報告の有用な過去証拠としてのみ扱い、今回の変更が実環境で保存された証明には使用しない。本担当は過去artifactの再取得・再実行をしていない。
- 読取時の失敗として、存在しない `Testing/Duck.Testing.MatchController` パスの検索と、PowerShellからrgに渡したglobパスがエラーになった。その後、実際の `Testing/Duck.Testing.RefereeDriver.Host/MatchControllerWorker.cs` とディレクトリに対する `-g` 検索で必要な依存を確認した。検証成功へ算入していない。

## 次の扱い

今回の保存候補に対する通常レビューは `pass_with_held` である。親担当は保留を維持して受入の観測契約と証拠不足を処理し、変更後の固定HEADに対する必要な検証と、別担当による独立最終レビューへ進める。実装が変更された場合、この技術判定を新HEADへ自動継承しない。commit / push / PR作成・コメント / Issue close / merge は本担当では実施していない。
