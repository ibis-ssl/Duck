# Issue #54 通常レビュー

## 対象と識別

- Repository: `ibis-ssl/Duck`
- Issue / PR: #54 / PR #122
- Branch / base: `task/issue54-aspire-006g4` / `task/issue50-aspire-006g`
- Reviewed HEAD: `6487783` (`Handle referee force-start after ball placement`)
- Scope: 保存済みrun counter 42以後の診断出力と、STOP/NEXT FORCE_STARTで進行が停止するrun経路の修正。
- Non-goals: Crane/planner/Simulatorの挙動変更、過去の1.6m開始阻害を同一原因扱いすること、#54をPOST_GAME到達済みとみなすこと。

## Dispatch profile (parent-owned)

- task_kind: initial normal review
- work_class / uncertainty / change_radius / criticality: judgment-heavy / medium-high / cross-module / high
- decomposability: independent_workstreams (source and evidence/report surfaces)
- decomposition_policy / disposition: forbidden / prohibited by review lifecycle; single reviewer
- selection_source: explicit user override
- requested: `gpt-6.1-sol`, reasoning `high`, fork `none`
- role_plan: `agent_type` omitted; runtime default. No explicit role override requested.
- planned_runtime_profile: `gpt-6.1-sol` / `high`
- applied profile: pending; record only what parent can observe after dispatch.

## Findings

必須指摘あり。以下の4件はすべて初回通常レビューで検出した `P2 / medium`、未解消。順序は影響の大きいものからとした。保存済みrunでこれらの全条件が発生したとは主張しない。

### I54-NR-001 — FORCE_START の最新 next-command context を照合していない

- 分類・所在: blocking normal-path correctness / `Testing/Duck.Testing.RefereeDriver/RefereeDriver.cs:539`、関連 `:461`、`:506`。
- 発生条件: UDP が STOP / NextCommand ForceStart でも、同じ STOP・stage の API snapshot または送信直前の continuation read が nextCommand を別の再開指令へ変更した、あるいは nextCommand を欠落させた場合。FORCE_START の action entry が READY_MANUAL / UNKNOWN / issueなしなら通過する。
- 根拠: 新分岐は UDP.NextCommand だけを読む。`IsAlignedLifecycleObservation` は command/phase/stage、`IsCurrentContinuationContext` も command/phase/stageのみ照合し、再選択では StageTimeLeft だけを更新する。`GameControllerContinuationState` と observation.ApiSnapshot には NextCommand/NextCommandForTeam が既にあるが、この経路は参照しない。追加テスト `RefereeDriverFixtureTests.cs:255` も ApiSnapshot と continuation.NextCommand を欠落させたまま FORCE_START 送信を成功扱いしている。隣接する kickoff readiness と `SimulatorBallPlacementFixture.IsEligibleOfficialPlacement` は next-command/team を観測と直前readの両方で照合している。
- Report/acceptanceへの影響: READY_MANUAL は手動操作の実行可能性であり、このdriverが処理対象とする再開指令との一致の証明にはならない。上記の入力では FORCE_START 待ちと確認できない現在状態にも操作を送信するため、別の再開手順を進める可能性がある。保存済みrunで誤送信が起きたという指摘ではない。
- 最小是正: FORCE_START固有の条件として、現在の API snapshot と直前 continuation read の nextCommand=`FORCE_START`、team=`UNKNOWN`、UDP nextCommand=`ForceStart`、同じ停止中の通常stageを照合してから送信する。欠落・不一致は待機する。
- 必要テスト: 実際の ApiSnapshot を持つ残り17秒の正例、観測APIまたは直前readの nextCommand/team が欠落・不一致の各負例（送信0）、blocked/issueあり/READY_AUTOの負例、同じSTOPで再送しないこと。fresh FORCE_START後に通常終端操作へ進みPOST_GAMEとなる構成例と、古い/異なるeffectを受理しない例も追加する。現在の追加テストは最終結果がMaximumTimeReachedであり、effect確認を削除しても同じassertを満たすため、effectの成立を単独では証明しない。

### I54-NR-002 — Referee の非有限座標で診断JSON全体を出力できなくなる

- 分類・所在: blocking report/serialization regression / `Testing/Duck.Testing.AppHost/SslLogMotionAcceptanceVerifier.cs:116`（117行も同じ）、消費側 `Testing/Duck.Testing.AppHost/Program.cs:54`。
- 発生条件: 復号できるReferee packetの designated_position に NaN / +Infinity / -Infinity が入り、LastRefereePacketまたはcounter42以後のtimelineに保持された場合。protobuf floatの復号とraw counter検査はこれらを拒否しない。
- 根拠: 新snapshotは座標を無検査でfloat?へ格納する。既存Vision座標経路（135、139–140行）はfinite検査済み。Programのdefault `JsonSerializer.Serialize` は名前付きの非有限浮動小数を許可せず、serializationはread用try/catchの外にある。counter42以後は後続の正常packetが来ても、不正値を含む古いtimeline entryが残る。
- Report/acceptanceへの影響: 診断追加前には無視されていた座標で、完全archiveやmotion判定の内容にかかわらず `motion-acceptance.json` の書込みまで到達できなくなる。workflow `dotnet-test.yml:1621` からのverifier呼出しは失敗し、失敗原因と部分証拠をJSONに残せない。これは実保存runにNaNがあったという意味ではない。
- 最小是正: presenceとfiniteを確認して不正座標をnull等の明示的な利用不能値へ投影し、JSONを常に保存できるようにする。必要なら不正理由を診断として残す。新診断値の異常で既存motion acceptanceを変えない。
- 必要テスト: NaN/±Infinityと座標欠落を持つRefereeを含むarchiveを読み、Programと同じdefault optionsでreportをserialize・parseできること。timelineに不正packet後の正常packetがある例も含め、既存Passed/RecordStreamCompleteの結果が診断だけで変わらないことを確認する。

### I54-NR-003 — counter42より前のballが「以後」の最終traceへ混入する

- 分類・所在: blocking diagnostic correctness / `Testing/Duck.Testing.AppHost/SslLogMotionAcceptanceVerifier.cs:243`–246。
- 発生条件: counter42前にballを含むVisionがあり、counter42以後にはballを含むVisionが一件もない場合。空ball frameのみ、またはVision停止でも発生する。
- 根拠: `lastVisionFrameWithBall` はcounter42前から更新される（145行）。終了時のappendはcounter42の有無とglobal last値だけを見ており、sampleがcounter42以後かを確認しない。そのため空のBallTraceAfterCounter42へ古いsampleが必ず追加される。
- Report/acceptanceへの影響: 消失・記録停止の診断で、counter42以後にball観測が存在したような出力になる。motion Passed自体は変わらないが、この変更の目的である停止後の証拠の正確性を損なう。global LastVisionFrameWithBallに古い最終観測を残すこととは分ける必要がある。
- 最小是正: counter42以後専用のlast-ball値を持ち、その区間のsampleだけをfinal appendする。必要ならrecord時刻境界も確認する。区間内のballがなければtraceは空のままにする。
- 必要テスト: counter41のball → counter42 → 空Vision/EOF、counter42以後にballがある通常例、10秒未満の最後のballを最終sampleとして保持する例。global last-ballと区間traceを別々にassertする。

### I54-NR-004 — 診断timelineとtraceに保存量の上限がない

- 分類・所在: blocking bounded-diagnostic requirement / `Testing/Duck.Testing.AppHost/SslLogMotionAcceptanceVerifier.cs:125`、`:149`、`:243`。
- 発生条件: counter42以後に状態が頻繁に変わるarchive、長時間archive、または各Vision timestampが10秒以上進むarchive。SameRefereeStateは座標の微小差でも別状態とするため、同じcounterで座標が揺れるpacketでもtimelineが毎回増える。
- 根拠: 両Listに件数・byte・期間の上限や切詰め通知がない。Referee側は連続重複排除のみ、ball側は入力timestampによる10秒間隔のみ。CLIは保存済み任意archiveを処理するため、現在のMATCH wall-clock上限だけではこのreaderの保存量は制限されない。`phases-status.md:378` はこの変更をbounded diagnosticと記載している。
- Report/acceptanceへの影響: 新診断のメモリとJSON出力量が入力全体に比例して増える。通常の保存runは2 timeline / 80 ball samplesで問題が見えていないが、実装上の上限は証明できない。既存のarchive全体読込みやmotion windowsの設計変更はこの指摘の対象外。
- 最小是正: 新しい両診断コレクションに明示的な保存上限を設け、最終状態/最終ballの保持とtruncated/dropped件数を定義する。診断の切詰めでmotion acceptanceを変えない。
- 必要テスト: 上限超過の状態遷移、座標変化、長い/大きく進むtimestampを入力し、保存件数・出力量が上限内、最終証拠が保持され、切詰めが明示されること。境界件数、重複、最終appendも含める。

## Coverage and review method

- review_mode: `initial normal review`。reviewed_implementation_head: `6487783bad2df5ca493efc1bf40cbde895ed2e70`。直接の比較元: `770626396f2fb2c0b4ff8bb09f9dc72c7a1949ab`。対象range: `7706263..6487783`。親ブランチ名は識別欄の通りで、今回の指摘はこの1commitの変更・直接依存を対象とする。
- reviewer: `/root/issue54_6487783_normal_review`。実装を担当していない初回通常reviewer。別agentの起動、実装修正、Docker、commit/push、外部comment、mergeは行っていない。runtimeの実際のmodel/reasoningは観測できず、parent-owned Dispatch profileを変更していない。
- 読んだskill: `C:/Users/donabe/Documents/Codex/CodexSkill/skills/review-worker/SKILL.md`、`.../sub-agent-task-manager/SKILL.md`。repository rootとworkspace祖先の確認範囲にAGENTS.mdは存在しなかった。
- 変更6ファイルを全件確認: `Testing/Duck.Testing.AppHost/SslLogMotionAcceptanceVerifier.cs`、`Testing/Duck.Testing.RefereeDriver/RefereeDriver.cs`、`Tracker/Tracker.Tests/RefereeDriverFixtureTests.cs`、`Tracker/Tracker.Tests/SslLogRecordReaderTests.cs`、`Tracker/Design/phases-status.md`、`reports/issue54-implementation-20261108.md`。
- 直接依存・構成: `SslLogRecordReader.cs`、AppHost `Program.cs` のCLI/JSON、`GameControllerStateClient.cs` のcontinuation正規化と有限message読取、`GameControllerStateMapping.cs`、`GameControllerStateObserver.cs`、`RefereeApiMatcher.cs`、`RefereeDriverNetworkAdapters.cs` の既存FORCE_START wire mapping、`RefereeReceiveService.cs`、host `MatchControllerWorker.cs` のObserve callback/POST_GAME/未確認配置の最終gate、`SimulatorBallPlacementFixture.cs` のofficial-placement/FORCE_START確認分岐と関連既存fixture testsを確認した。
- workflow・証拠: `.github/workflows/dotnet-test.yml` のarchive verifier呼出しとfull-match gate、`scripts/verify_aspire_match_acceptance.py` の証拠読取境界、`MatchLinuxBindPermissionsTests.cs` のWindows dynamic-skip条件、`reports/issue54-intake-20261108.md`を確認した。変更のないworkflow全体を改めて受入実行したものではない。
- FORCE_STARTの既存保護を確認: command/phase/stage整合、READY_MANUAL・UNKNOWN・issuesなし、attempt keyによる同一counter/stage再送抑止、有限match budget、fresh timestamp（欠落時counter）と同一stageのUDP FORCE_STARTによるeffect確認。HALTは先にResumeFromHaltへ進み、実際のAPI/UDP mapping上、API STOPとUDP HALTは整合せずFORCE_STARTには入らない。これらの保護は存在するがNR-001のnext-command照合を代替しない。
- serialization互換性: 新record引数は末尾optional追加で既存constructor使用は維持、既存JSONキーとmotion acceptanceロジックは維持、CLIは新しいキーを追加出力する。非有限値による例外はNR-002。新テストはJSON消費側まで通さないためその回帰を検出しない。

| 必須coverage | disposition | 根拠/残件 |
| --- | --- | --- |
| 要求・設計整合 | checked_finding | NR-001、003、004。保存runの診断目的とbounded記載を照合 |
| 正確性・境界条件 | checked_finding | NR-001〜004 |
| 変更範囲・無関係変更 | checked_no_finding | 6ファイル。Crane/planner/simulator/protocol変更なし |
| 全変更ファイル・直接依存 | checked_finding | 上記のsource/tests/workflow/reportを確認 |
| API/data/config/compatibility | checked_finding | wire mapping既存、optional JSON追加、NR-001/002 |
| error handling・診断 | checked_finding | NR-002〜004。既存partial/CRC/counter/malformed保護を保持 |
| security・secret | checked_no_finding | 新しい外部endpoint/credential/log secret入力なし。既存sanitizer変更なし |
| tests・検証の十分性 | checked_finding | 正例追加はあるが4件の境界とcomposed FORCE_START終端のoracleが不足 |
| exact-HEAD CI | unexplored | parent evidenceは未実行。過去HEADのCIは本HEADの成功証拠にしない |
| report/tracking/docs精度 | checked_finding | boundedの主張はNR-004。POST_GAME未達・因果未確定は正しく保持 |
| regression・保守性 | checked_finding | NR-001〜004。counter42固定の汎用性はheld |
| Linux MATCH実通信/POST_GAME | unexplored | 読取専用reviewの範囲外。保存run replayで代替不可 |

実行したのはgit status/rev-parse/log/diff/show、rgによる探索、Get-Contentによるsource/log読取、既存JSONのConvertFrom-Jsonによる要約。新テスト、replay、build、Docker、CI dispatchは実行していない。想定反例はsource control flowに基づくもので、再現テスト実行済みとは記載しない。

## Validation evidence

- Focused local tests: `SslLogRecordReaderTests` and `RefereeDriverFixtureTests`, 34 passed.
- Full local `Tracker.Tests`: 1,358 passed, 2 skipped, 8 Windows-host dynamic-skip failures in `MatchLinuxBindPermissionsTests`; no changed-test failure.
- Existing run replay: 42532919016 archive complete, 272,953 records, motion acceptance passed. This does not validate the changed HEAD in a new match.
- `git diff --check`: passed.
- Exact-HEAD CI and post-fix Linux MATCH: not yet run.

## Verdict

`fail / required changes`。必須指摘 I54-NR-001〜004 が残る。severityの変更・再分類はない。本判定は上記のimmutable implementation HEADだけに適用する。レビュー開始時・最終確認時のHEADは一致した。

親が記載したValidation evidenceは保持した。reviewerが独自に読んだ保存run `42532919016` のresource logでは、counter42のBALL_PLACEMENT_YELLOW/NormalSecondHalf/NextCommand ForceStart/17秒とball-only send（23:08:16.855 UTC）、counter43のSTOP/同next-command/17秒（23:08:22.729 UTC）、最後のMATCH_MAXIMUM_TIME_REACHEDを確認した。`match/match-result.json` はfailed / maximum_time / MaximumTimeReached / finalCommandCounter=43。保存されたcounter42-trace JSONは272,953 records、RecordStreamComplete=true、Passed=true、Referee遷移2件・ball samples80件、最終STOP/NORMAL_SECOND_HALF/FORCE_STARTを含む。Vision生座標はmmで、(-2493.8843,875.8011)mmを実装報告では(-2.494,0.876)mへ換算している。旧runのmotion合格と、現HEADの実試合完了は別の証拠である。

保存artifactの所在: `%TEMP%/issue54-run-42532919016/aspire-full-stack-42532919016-1/`、追加解析は `%TEMP%/issue54-42532919016-counter42-trace.json` と `issue54-42532919016-motion-diagnostic.json`。reviewerはこれらを再生成していない。

### Finding completeness matrix（初回・全件open）

| finding | required action / production path | composition fixture | focused validation evidence | disposition |
| --- | --- | --- | --- | --- |
| I54-NR-001 | FORCE_START用API/UDP/直前stateのnext-command/team一致 / RefereeDriverFixture | 実ApiSnapshotを持つdriver → callback → FORCE_START effect → normal termination。正負例は上記 | 新経路の既存正例のみ。必要な不一致・effect判定・終端例なし | incomplete / open |
| I54-NR-002 | 新Referee座標のpresence/finite投影 / archive verifier → Program JSON | synthetic archive → Read → default serializer → JSON parse | serializeを含む境界例なし | incomplete / open |
| I54-NR-003 | counter42以後のlast-ballのみfinal append / archive verifier | pre42 ball → counter42 → ballless/EOF、および正常最終sample | 区間内ballがある正例のみ | incomplete / open |
| I54-NR-004 | 両新診断Listの上限・最終証拠・切詰め情報 / archive verifier → JSON | cap超過archive → bounded report → serialize | 上限・超過の例なし | incomplete / open |

次は各必須是正と必要な検証をcommit単位で確定し、同じ通常reviewerへfinding-limited fix verificationを返す。exact-HEAD CIとLinux MATCH受入は別途必要。initial_independent_reviewed_head / closure_reviewed_head: not_applicable（通常レビュー）。reserved_report_paths: 本reportのみ。report_attestation_allowed: `false`（独立最終レビューのattestationではない）。commit/push/CIはそれぞれ未実行・未確認の状態を保持する。

## Held and unexplored

- A Linux MATCH run on the reviewed HEAD is still needed to verify FORCE_START continuation and POST_GAME behavior.
- AutoRef's reason for the STOP / FORCE_START transition and Crane's `ball_sensor_active` causal role are not established by the saved artifacts.

- confirmation-required capability gap G1: 現HEADのLinux MATCHで、実際にFORCE_STARTがREADY_MANUAL/issueなしとなること、送信後のfresh FORCE_START、placement確認解消、最終POST_GAMEと全記録gateの成立は未確認。reviewerにはDocker/新run実行が禁止されている。親は既存の許可範囲と実行環境を解決して検証する必要があるが、このreportはユーザーへの再承認が常に必要とは判断しない。保存runでそのmanual actionが最終停止時に提供されていたことも、読んだartifactだけでは証明していない。
- held H1（非blocking・owner: parent/Issue #54）: Windows-hostの既存8 dynamic-skip failuresは変更に起因する証拠がなく、別修正をこのcommitへ要求しない。Linux exact-HEAD CIの実結果で評価する。
- held H2（非blocking・owner: parent/Issue #54）: 診断開始のcounter42は今回の保存run固有で、別runでは同じ事象が別counterになる。今回の限定診断として保持可能。汎用化する場合は開始条件の仕様を別途定義する。
- held H3（非blocking・owner: parent）: 実装報告末尾の未commit/未push/未reviewという記載は当時の作業時点を表すが、今回のreview後に最新状態を追記する。これをCIやPOST_GAME成功へ読み替えない。
- unexplored: 新しい実通信、AutoRef内部の原因、Crane内部制御の因果、現在のremote Issue/PR本文・CI状態の独自再取得。今回の委任はローカルの保存Issue記録・artifactとexact commitのcode reviewであり、これらを確認済みにしていない。
