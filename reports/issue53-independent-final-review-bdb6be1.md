# Issue #53 独立最終レビュー・限定 closure

## 最終判定と対象 identity

- verdict: **pass_with_held**。未解決の必須 finding は0件。明確化された Issue #53 / ASPIRE-MATCH-004 の受入は完了した。
- repository: ibis-ssl/Duck。Issue #53、PR #121、branch task/issue53-aspire-006g3、base ref task/issue50-aspire-006g。
- base commit: 9756f9dd35ce05cf18d6ad91eaf277ad917fcfe9。
- initial_independent_reviewed_head: bdb6be1e11ba8cd5224268b9e244c95ec4dae377。
- reviewed_implementation_head / technical_head: **132d74b5ea74a1387424f2844cb7f8c12a61c733**。
- closure_reviewed_heads: c0d4fb23df180013a14d8fef93c0a32e7bb63814、078a3225871dcf05ecdaff2cd759f28906db1d56、132d74b5ea74a1387424f2844cb7f8c12a61c733。
- 全範囲の独立レビューは bdb6be1 に対して1回だけ実施。その後は同一 reviewer が既存 finding、対象 identity、tracking/report、CI・受入証拠の差分だけを限定確認した。2回目の網羅レビューは行っていない。
- 132d74b5 の first parent は 078a322。差分は phases-status と通常修正確認報告だけ。実行コードは c0d4fb23 / 078a322 と同一である。
- 132d74b5 自体に一致する CI run は検索時点で0件。078a322 の CI / MATCH を132d74b5で実行したとは主張しない。コード同一性と文書差分の確認に基づき最終判定を対応付ける。

## reviewer と権限

- reviewer: /root/issue53_independent_final_bdb6be1。同じ担当が全 closure を継続。
- 実装、実装修正、通常レビューへの参加はない。通常 reviewer /root/issue53_normal_review_eafb5ef とは別の新規独立担当として開始した。
- requested profile: gpt-6.1-sol high。初回 dispatch は fresh / fork_turns=none、reviewer 1名、分解なし。親から実引数指定として通知された。
- applied profile: 観測不能。requested と applied を同一視しない。継続は同じ担当の再利用であり、新規 profile の実適用を主張しない。
- nested Codex、子agent起動、実装修正、テスト、build、Docker、インストール、push、PR編集・コメント、mergeは実行していない。
- 最終段階で親が明示的に許可した書込みは、この予約済み報告1ファイルと、その1件の行政的 attestation commit だけである。
- 適用スキル: work-context-manager、review-worker、report-writer、report-output-manager。リポジトリと確認した上位階層に AGENTS.md はなかった。

## 要求・範囲・非対象

ユーザーが明確化した #53 の条件は、AutoRef 自身の10020 / 11003復号済み入力件数、Sumatra と AutoRef の11010出力を現在の participant / resource に対応させた件数、および受理された設定と observer 証拠による Duck の11010非送信である。

raw packet capture、任意の不正payloadを含む全UDP datagramの不在測定、#54 の POST_GAME、得点発生は追加条件にしない。AutoRef の11010は固定版の出力経路として扱い、旧設計履歴にある tracker入力表現を理由に新たな受信器や受入条件を追加しない。

## Findings と最終 disposition

### I53-IF-BDB6-001 — medium — closed

- origin: bdb6be1 に対する今回の独立最終レビュー。初回 verdict は fail。
- 初回場所: Testing/Duck.Testing.AppHost/Program.cs:703、Duck.Testing.AppHost.csproj:33。直接影響は Tracker/Tracker.Tests/AppHostApplicationModelTests.cs の MATCH 構成テストと CreateAppHostAsync。
- 問題: 新しいJARの存在検査がAutoRef起動時ではなくAppHostモデル構築中に実行され、Javaを起動しない既存MATCH構成テストまで FileNotFoundException になる。
- 根拠: READMEの前提は.NET SDK 10.0、全体コマンドは dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore。プロジェクトは生成済みJARの条件付きコピーだけで生成せず、JAR生成はCIの明示的事前scriptにある。既存CM4 helperは BeforeResourceStartedEvent に欠落検査を遅延しており、モデル読取りを阻害しない。
- 影響: JAR未生成checkoutで既存MATCHモデル検査を実行できない。旧ローカル全体テストの代表例外と一致するが、全15失敗をこの原因へ帰属しない。JDK未導入という能力の不足と、検査位置のコード回帰を区別した。
- 頻度: JARなしで該当モデルを構成すると決定的に発生する。ユーザーの「1000人で月1回程度以上」という目安を考慮したが、母集団・利用統計はないため月間発生数を推定していない。
- 必要対応: 通常buildへ適切に生成を接続するか、モデル構築を阻害しない実起動境界へ欠落検査を移す。実MATCH起動時の明瞭な欠落失敗を維持し、既存経路の検証を対象内容へ結び付ける。空のダミーJARは修正証拠にしない。
- 修正: c0d4fb23 で eager guard を除去し、AutoRef container の WithEnvironment callback から AutoRefGameLogAgent.RequireAvailable を呼ぶ。例外型、既存message、FileNameを維持。
- 回帰: MatchModelCanComposeWithoutAgentButMissingAgentFailsItsPreflight はMATCHモデルを構成し、対象AutoRefの EnvironmentCallbackAnnotation をRun用contextで実際に評価する。JAR不在分岐の例外と、一意な欠落pathでのhelper例外を確認する。
- 検証: 実装担当の日本語報告は、Windows / .NET 10、JAR不在、focused test 1 passed / 0 failed / 0 skipped、diff-check成功を記録。reviewerは再実行していない。後続078a322のLinux CI・実MATCH起動も成功。
- closure: dirty候補で内容確認後、c0d4fb23との4ファイルSHA-256一致とコミット差分一致を確認して正式に閉鎖。078a322 / 132d74b5 はコード差分なし。severity mediumは維持し、再分類なし。

### I53-NR-EAFB-001 — medium — closed（通常レビュー由来）

- origin: eafb5ef に対する通常レビュー。model name duck とallocated ID duck-wdmzzrcwの不一致で、正当なDuck-address sourceも派生self-source欄から除外される問題。
- 影響: 空結果とidentity不一致を区別できない検出能力の欠落。実際のDuck送信が観測されたというfindingではない。
- 必要対応: 同じ検証済み診断にallocated Duck ResourceIdを保持して使い、未取得と有効な空結果を区別し、異なるmodel name / allocated IDのpositive / rejectingケースを確認する。
- 修正: f667050 の source-bearing result が duckTarget.ResourceId を診断、container / network identityと同時に保持し、loopが同じresultをselectorへ渡す。欠落証跡はnull。
- fixture: duck / duck-wdmzzrcw、positive self-source count 8、JSON round-trip、欠落ID、誤run / identity、generation / network guardsを確認。提供されたprojectionテスト結果は16/16。
- 独立確認: 初回の全範囲レビューで各production returnとselector、fixtureを確認。新規必須問題なし。078a322の実成果物では正しいallocated ID duck-azgmtjttを使用している。
- severity mediumを維持。severity再分類・転記訂正なし。

## Finding completeness matrix

| finding / 必要対応 | production path | 実composition fixture・証拠 | 最終 disposition |
| --- | --- | --- | --- |
| I53-IF-BDB6-001: モデル構成の妨害を除く | Programのeager guard削除 | MATCH modelをCreateAsyncで構成後AutoRefを取得 | 完了 |
| 同: 実起動前の明瞭な失敗 | AutoRefのWithEnvironment callback → RequireAvailable | 実EnvironmentCallbackAnnotationの評価、例外型/message/FileName | 完了 |
| 同: 検証と対象identityを対応 | c0d4fb23に実装3ファイルと報告を固定 | focused 1/1の提供記録、4ファイルhash一致、後続CI / MATCH | 完了 |
| I53-NR-EAFB-001: 同じ診断のallocated IDを使用 | source-bearing result → loop → self-source selector | 異なるmodel / allocated ID、positive source、JSON復元 | 完了 |
| 同: 未取得と有効な空結果を区別 | 欠落result / sources / address / IDはnull | 欠落ID assertion、positive / rejecting checks、projection16/16提供記録 | 完了 |

## 1回の網羅レビューのcoverage

初回は9756f9dd..bdb6be1の全23変更ファイル、直接依存、Issue / 設計、通常レビュー2件、通常修正確認、実装follow-up、tracking、履歴を確認した。後続は上記findingと文書・CI証拠差分だけを確認した。

| 必須観点 | 初回 → 最終 disposition | 根拠 |
| --- | --- | --- |
| 要求・設計適合 | held → checked_no_finding | 明確化されたMATCH-004と078a322の実測を照合 |
| 正しさ・境界条件 | checked_finding → checked_no_finding | JAR回帰を修正・閉鎖。allocated ID修正も確認 |
| scope・無関係変更 | checked_no_finding | Crane / planner / protocolへの差分なし。#54条件を追加しない |
| 全変更ファイル・直接依存 | checked_finding → checked_no_finding | producer、projection、consumer、artifact、callbackまで確認 |
| API・data・互換性 | checked_no_finding | optional JSON追加、既存DTOの追加property許容 |
| config・workflow | checked_finding → checked_no_finding | JAR生成・mount・収集、モデル / 起動境界の修正 |
| error handling・failure diagnostics | checked_no_finding | Java timeout、archive欠落 / summary失敗、upload前sanitization |
| security・secret・cleanup | checked_no_finding | owner / stack / resource / runと事前snapshotによる対象限定 |
| tests・validation | checked_finding → held | focused修正と078a322 CI成功。旧Windows全15失敗の詳細原因は未確定 |
| current-HEAD CI | unexplored → held | 132d74b5のrunは0。コード同一078a322のCI / MATCH成功を別identityで保持 |
| reports・tracking | held | 132d74b5の同期は既存証拠と整合。過去資料の旧状態は履歴として保持 |
| regression・maintainability | checked_finding → checked_no_finding | 固定upstreamに対するJava hookとJAR検査回帰修正を確認 |
| #54 / raw capture追加 / merge | not_applicable | 受入条件・本reviewer実行範囲外 |

初回変更ファイル: .github/workflows/dotnet-test.yml、AutoRefGameLogAgent.java、BaseSimulationResourceRegistration.cs、Duck.Testing.AppHost.csproj、Fixtures/match/autoreferee.xml、MatchParticipantStatusProjection.cs、Program.cs、AppHostApplicationModelTests.cs、MatchParticipantStatusProjectionTests.cs、scripts/build_autoref_game_log_agent.sh、tasks-status.md、phases-status.md、および次の11報告。

- issue53-autoref-native-gamelog-20261108.md
- issue53-autoref-source-audit-20261108.md
- issue53-fix-I53-NR-EAFB-001.md
- issue53-implementation-20261108.md
- issue53-implementation-followup-I53-NR-EAFB-001.md
- issue53-intake-note-20261108.md
- issue53-intake-verification-20261108.md
- issue53-match-42479742584.md
- issue53-normal-fix-verification-i53-nr-eafb-001-f667050.md
- issue53-normal-review-cb0bab5.md
- issue53-normal-review-eafb5ef.md

限定closureではAutoRefGameLogAgent.cs、issue53-implementation-I53-IF-BDB6-001.md、issue53-normal-fix-verification-I53-IF-BDB6-001-c0d4fb2.md、関連test差分、tracking差分を追加確認した。

直接依存: ServiceReceiveDiagnostics、ResourceDiagnosticIdentity、TigersTrackerOutputReadiness、RuntimeTrackerReceiverService、RuntimeVisionDiagnosticsHttpService、RuntimeVisionReceiverDiagnostics、UdpTrackerPacketPublisher、SslLogRecordReader、MatchControllerWorkerのDTO / readiness、verify_aspire_match_acceptance.py、sanitize_aspire_artifacts.py、Linux publish手順。固定upstream SHA 3617de906213971972aa89da6982623b6058caf8のGameLogRecorder / GameLogWriter / autoreferee.xmlもGitHub APIから読取確認した。

## #53 受入証拠の限定照合

実行はworkflow_dispatch [42488259296](https://github.com/ibis-ssl/Duck/actions/runs/42488259296)、headSha **078a3225871dcf05ecdaff2cd759f28906db1d56**、run ID gha-42488259296-1。test、Linux publish、ASPIRE-NET-002、full-stack jobはsuccess。別のpull_request CI [42487979593](https://github.com/ibis-ssl/Duck/actions/runs/42487979593) も同HEADでsuccess。

成果物を C:\Users\donabe\AppData\Local\Temp\issue53-run-42488259296 から直接読んだ。Linux hostはUbuntu24.04.5 LTS、Docker28.0.4。AutoRef実行digestは96dd8417c1fcf4025ee0a20c8210954068440991989587b68ecdea7d27db6b3d。trackingのlatest tag照合は親の採取記録であり、このreviewerがregistryを再照会したとは主張しない。

| 条件 | 判定 | 成果物の直接証拠 |
| --- | --- | --- |
| AutoRef自身の10020入力 | pass | native input-summary VisionDetectionRecords=25,899。inspect引数visionAddress=224.5.23.2:10020 |
| AutoRef自身の11003入力 | pass | native input-summary RefereeRecords=16,207。inspect引数refereeAddress=224.5.23.1:11003 |
| Sumatraの11010出力 | pass | 現在のtigers-blue full container IDとnetworkに対応する172.18.0.6:42499、UUID 3fd4f1df-96b1-4240-909f-0553fab02252、matched snapshot32,274、後刻の全source snapshot32,287、decode failure0。startup logも11010 publishを記録 |
| AutoRefの11010出力 | pass | 現在のautoref-tigers full IDとnetworkに対応する172.18.0.7:54745、UUID d9f5439a-f850-4f2e-a8e2-614a3b3d80bd、32,288 decoded、failure0。startup logも11010 publishを記録 |
| Duckの11010非送信 | pass | MATCH PublishUdp=false。検証済みDuck address172.18.0.8、allocated ID duck-azgmtjtt、run / generation1 / epoch2を保持。全sourceは上記2件で、修正済みself-source listは空。受理された設定＋observer証拠の範囲で充足 |

Sumatraのmatched count32,274と全source count32,287は異なる観測時刻であり、同一snapshotの値と混同しない。送信元IPとcurrent participantの対応、共通network 441ffbed90551b3638da80e42d5e687951599ec1dd11a96b682d5e55eb119654、Duck receiverのfull IDをinspect / participant JSONで照合した。

AutoRef archiveは5,499,224 bytes、ReachedDecompressedEof=true、ReadFailure=null、VisionTrackerRecords=0。captureは1 log、stop_exit_code=0、container_running=false、candidate_count=1。archive SHA-256を読取りで確認した: 01C8C742CC30887BA0F8A515376D8840BF3B6D81B5CB09145038F9B58E26AB6E。reviewer自身による再decode / CRC検証は行っていない。VisionTrackerRecords=0をAutoRef出力0と解釈しない。

match-resultはMaximumTimeReached / fullMatchPassed=false。match-acceptance.jsonのpassed scopeはMATCH-001/002であり、workflow successだけを#53合格理由にしていない。#53は上表の個別証拠から判定した。11003外部captureは172.18.0.2:34384の12,061 datagramsだが、uniqueOnWireSenderAttributionVerified=falseであり、送信process帰属の根拠にしていない。POST_GAMEは#54であり#53へ追加しない。

## 環境・commands・検証履歴

- reviewer環境: FA780、fa780\donabe、Windows10.0.22631.0、PowerShell7.4.22、作業ディレクトリ C:\Users\donabe\Documents\Codex\2026-11-07\task\Duck-issue53-aspire-006g3。
- verification_capability: local_execution_available。git / gh読取り成功、dotnet.exe利用可能。Windowsのjava / javac / jarはGet-Commandで見つからず、WSLもJavaなしという親の提供記録がある。この分類をLinux全体受入の実行能力と同一視しない。
- 初回bdb6be1のREADME全体コマンドは親が1回実行し15 failed / 1,346 passed / 2 skipped。生stdout / stderr、TRX、exit-code fileは未保存。可視代表例はLinux verifierのdynamic-skip前提とJAR欠落例外。全15原因は不明のまま保持し、successへ変更しない。
- f667050のprojection focused16/16、c0d4fb23候補のJAR回帰focused1/1、diff-check成功は実装報告・親の提供証拠。reviewerによる再実行ではない。
- 旧CI42483036726はeafb5ef、旧MATCH42481939519はa881af3。APIでidentityを確認し、現HEADの結果へ付け替えない。旧AutoRef count16,427 / 26,346や旧self-source欠陥も履歴として区別した。
- 078a322のCI2件とMATCHはAPIのheadShaで確認。132d74b5のhead_sha一致APIはtotal_count=0。c0d4fb23..132d74b5はtracking / reports以外のdiffなし。
- read commands: git status / rev-parse / branch / remote / log / diff / show、rg、Get-Content、Get-FileHash、gh issue / pr / run view、head_sha付きActions API、固定upstream SHAへのcontents API。
- 初回のスキルパス読取りはtask親directoryで失敗し、正しいworktreeへ移って読み直した。通常報告名の1回の誤指定も実在するc0d4fb2付きpathで読み直した。読取エラーを検証成功へ算入していない。

dirty候補とc0d4fb23の一致確認に使ったSHA-256は次のとおり。同じ4ファイルは固定コミットとworktreeのdiff=0でも確認した。

| file | SHA-256 |
| --- | --- |
| Program.cs | 00E77E7B80224AD77306DED0C769BC10423F54F8AFF6181BDB1182E44C3F0A15 |
| AutoRefGameLogAgent.cs | 6E128373384AE6546386991E9D48E45F6C784F582A8A125DA97A07744E3F6ABC |
| AppHostApplicationModelTests.cs | 93901FB998AB419979ED2BBDFF59CC24A58E9DD5B029DB3FD70F959C8E023988 |
| issue53-implementation-I53-IF-BDB6-001.md | CE0A7FC0BBDBD45175EDC35846C4A8C22238777E8D4085170C0EDD09770E122F |

## Held・未探索・残るリスク

- H-I53-001（親 / 検証担当所有）: 132d74b5そのもののCIは0件。078a322を実行したCI / Linux受入と、コード同一132d74b5の文書限定確認を分けて保持する。必須受入証拠ギャップはない。
- H-I53-002（親 / 履歴管理所有）: 旧Windows全15失敗の個別原因・生ログは未確定。後続Linux全体CI成功で履歴を上書きしない。
- H-I53-003（証拠消費側所有）: 診断取得失敗時には古いsnapshotを保持する。専用取得時刻とsource内identityを読み、最新snapshot全体の時刻だけで現在の非送信と解釈しない。
- H-I53-004（親 / 文書所有）: 過去intake、設計履歴、remote本文には旧方向・旧状態が残り、一部remote本文の文字化けも観測した。ユーザーの明確化と現行tasks / phases、実測を優先した。132d74b5の最新同期は過去の通常修正確認と実測に整合する。
- 未探索: reviewer自身によるtest / build / Docker、全旧失敗の内訳、archive再decode、fresh artifact再取得、image binaryからsourceへの再現build、任意raw egressの測定、runtime recovery実験。これらを成功とは記載せず、raw captureを追加受入条件にもしない。
- 実際のDuck誤送信は観測していない。誤送信があったとも、その月間頻度とも主張しない。

## 永続化と行政的 attestation

- persistence mode: report_attestation_commit。report生成前はdeferred_attestationとして証拠だけを保持し、passing verdict後に初めて本ファイルを作成した。
- reserved path: reports/issue53-independent-final-review-bdb6be1.md。
- reservation owner: review-enforcer。identity: issue53-independent-final-20261108-bdb6be1。予約は初回freeze前のmetadata_onlyであり、passing verdict前にplaceholder等を作成していない。
- technical verdictはreviewed implementation HEAD **132d74b5ea74a1387424f2844cb7f8c12a61c733**に適用する。本報告のcommitは技術実装ではなく、1件の行政的 attestationである。
- administrative_parent: 132d74b5ea74a1387424f2844cb7f8c12a61c733。報告生成時のcommit stateはcommit_pending。報告のSHAは作成後に親へ外部で返し、自己参照SHAを本文へ追記しない。
- 許容条件: 132d74b5をfirst parentとする後続commitがちょうど1件で、差分はこの予約pathだけ。実装、Skill、design、workflow、config、tracking、handoff、他報告を変更しない。後続commitが存在しないことを検証する。
- 本文保存後に上記diff / parent / commit件数 / clean状態を確認する。どれかが崩れればterminal completionは成立せず、通常修正確認と同じ独立reviewerのfinding / CI差分限定closureが必要。
- push、CI wait、PR操作、mergeはこのreviewerの実行対象外。本報告のpassing verdictはmerge権限を付与しない。

## 最終状態

I53-NR-EAFB-001とI53-IF-BDB6-001はともにmediumのままclosed。新たな必須findingなし。issue53_acceptance_complete=true、verdict=pass_with_held、report_attestation_allowed=true。技術対象と実行runのidentityを維持したまま、予約pathだけの行政的attestationを許容する。
