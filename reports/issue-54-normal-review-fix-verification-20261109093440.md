# Issue #54 finding-limited修正確認：7fdf56c

## Findings / verdict

**`incomplete`（closure readinessの検証セルのみ残る）。** 4件の既知のproduction上の原因に対応する修正は揃っている。NR-002のpresence不足も今回解消された。新しい無関係な指摘や、今回の修正によるproduction回帰は検出していない。

元のrequired test/evidenceセルを照合すると、**NR-001とNR-004の一部が未充足**。`review-worker`の規定に従い、全セルが充足するまで正式なfinding closureは開始しない。NR-002/003はreadiness complete、NR-001/004はreadiness partial。4件のformal statusはclosure pendingであり、元のproductionバグが4件とも残っているという意味ではない。

元severityは全件 `P2 / medium` を維持し、severity再分類・新しいreview criteriaの追加はない。

## 対象と継続性

- reviewed_implementation_head: `7fdf56c6f533d25e543d98b988ab3adc08edb9f0`。
- 前回readiness確認HEAD: `443fd64b4271337ea104e2bfa6f1951a62b7da37`。
- 初回通常レビューHEAD: `6487783bad2df5ca493efc1bf40cbde895ed2e70`。
- review_mode: `finding-limited normal fix verification` のclosure readiness再確認。
- 今回差分: `443fd64..7fdf56c`。必要な既存fixは`6487783..443fd64`を継続参照。
- reviewer: `/root/issue54_6487783_normal_review`。同じ初回通常reviewer。実装・修正を担当していない。
- requested profile: `gpt-6.1-sol / high`を継続。実runtimeは観測できず、`applied: null`。新agent/forkなし。
- 本reportは、前回の `reports/issue-54-normal-review-fix-verification-20261109092513.md` に対する新HEADでの再確認結果。前回の歴史的verdictは改変しない。
- 初回の独立最終レビューHEAD / closure HEAD: not_applicable（通常レビュー）。

## Closure readiness matrix

focused evidenceは、親の今回依頼とcommitted実装報告にある同一候補内容の46 passedを使用した。reviewerはテストを再実行していない。下表の「complete」はproduction・actual composition fixture・focused evidenceが存在することを表し、全findingの正式closure完了とは区別する。

| finding / required action | production path | actual composition fixture | focused evidence / disposition |
| --- | --- | --- | --- |
| NR-001: UDP/API/直前readのFORCE_START/UNKNOWN一致 | `RefereeDriver.cs:466`、`:523`。API null/next-command/team不一致を拒否、既存manual gateは`:475`以後 | `ContinueMatchWaitsWhenForceStartDoesNotMatchLatestApiContext`、`ContinueMatchWaitsWhenPreSendForceStartReadDisagreesWithObservedApi` | 46 passed内。nextCommand null/別値・API team YELLOW・直前nextCommand競合のfixtureあり。以下の欠落/eligibility負例セルはpartial |
| NR-001: 同じSTOPで二重送信しない | pending effect処理とattempt keyを維持 | `ContinueMatchIssuesReadyManualForceStartForStoppedNextCommandEvenWhileClockHasTimeLeft`（`:255`）へ同counter STOPを追加し、送信1回をassert | complete |
| NR-001: fresh FORCE_STARTの確認後に終端へ進む | pendingAction→IsExpectedContinueActionEffect→END_GAME→POST_GAME | `ContinueMatchForceStartProgressesThroughEndGameToPostGame`（`:282`）：17秒STOP→ForceStart→0秒STOP→PostGame、actions=[ForceStart,EndGame] | complete。前回のMaximumTimeReached-only oracle不足を解消 |
| NR-001: 誤effectを受理しない | fresh evidenceと期待command/stageを確認 | `ContinueMatchFailsWhenForceStartProducesAnUnexpectedRefereeEffect`（`:312`）：NORMAL_STARTを受けるとInvalidOperationException | different-command負例complete。古いFORCE_START/異なるstageの専用負例は未充足 |
| NR-002: presence/finiteを投影しJSONを保存 | `SslLogMotionAcceptanceVerifier.cs:123`–126でHasX/HasYとfiniteを個別検査 | `RefereeDiagnosticCoordinatesAreNullWhenNonFiniteAndReportRemainsJsonSerializable`（`:119`）：NaN/+Infinity→-Infinity/有限Y、archive Read→default serializer→JsonDocument。`RefereePointWithMissingRequiredCoordinateIsNotReportedAsZero`（`:151`）：正常point→raw空point | complete。RecordStreamComplete=trueと既存motion判定falseもassert。初回の非有限timeline保持でもJSONが失敗する経路を解消 |
| NR-003: 区間専用last-ballと最終append | verifierの`lastBallTraceSampleAfterCounter42`をcounter42以後のtimestampだけで更新し、final appendへ使用 | `Counter42BallTraceDoesNotReusePreCounterBallWhenNoLaterBallExists`（`:184`付近）、`ReportsLastRefereePacketAndLastVisionBallForEndOfMatchDiagnosis`（`:68`） | complete。global last維持/後続trace空、区間内ball・10秒未満の最終sample保持を確認 |
| NR-004: 両Listの上限、切詰め明示、最新保持 | verifier `MaximumCounter42TimelineEntries/MaximumCounter42BallSamples=256`、`AddBounded`。通常追加とfinal appendが同じhelperを使用 | `Counter42DiagnosticsStayBoundedAndRetainLatestEvidence`（`:203`）、InlineData 256/257/300。両件数256、期待truncated値、最新score/ball timestampをassert | 基本上限・境界・最新保持complete。以下の満杯後final append/JSON compositionセルのみpartial |
| NR-004: 連続重複を増やさない | `SameRefereeState`はpacket受信時刻を比較対象にしない | `ReportsLastRefereePacketAndLastVisionBallForEndOfMatchDiagnosis`に同状態のcounter42 packetを追加。timeline counter列は[42,43] | complete |

## 未充足セルだけの一覧

### I54-NR-001 — 残るfixture

1. **ApiSnapshot自体がnullのケース。** 現在の `[InlineData(null,"UNKNOWN")]` は `ForceStartApiSnapshot(null,"UNKNOWN")` を生成するため、snapshotは存在しNextCommandだけがnullになる。更新報告の「API snapshot欠落」とは別である。
2. **欠落・不一致の残りのcontextケース。** API NextCommandForTeam欠落、直前readのNextCommand欠落、team欠落/不一致の送信0。これらを同じtheoryに追加できる。現在はAPI team YELLOWと直前NextCommand NORMAL_STARTがある。
3. **FORCE_START自身のmanual gate負例。** actionがBLOCKED、continuation issueあり、READY_AUTOの送信0。既存 `ContinueMatchUntilPostGameFailsClosedWhenManualActionIsBlocked` はResumeFromHalt経路を使う。既存共通条件はsource上維持されているが、初回に要求したFORCE_START経路のfixtureはまだない。
4. **古いFORCE_START/異なるstageのeffect拒否。** 新しいNORMAL_STARTの負例は追加済み。初回で求めたfreshness/stage条件の専用負例は未追加。正常な同stage・新timestampはcomposed正例で確認済み。

これらは元NR-001のtest/evidenceセルの不足であり、上記のproduction guardが壊れているという新指摘ではない。

### I54-NR-004 — 残るfixture

**256件に達した後、10秒sampling条件を満たさない別の最後のballをEOF final appendで保存するケース**がない。既存final append例は上限未満、256/257/300例はすべてball間隔11秒なので最後のballも既にsamplingされ、EOF追加分岐に入らない。

初回/前回で求めたcompositionとして、そのarchiveをRead→default serializer→JSON parseまで通し、ball件数256、最新ball保持、BallTraceAfterCounter42Truncated=true、JSON上のflag、およびRecordStreamComplete/Passedが診断切詰めで変わらないことをassertする。source上はfinal appendにもAddBoundedを使っているが、その実際の合成fixtureが未充足である。

## 確認済みの差分・証拠

- exact HEADとclean working treeを開始時に確認。`git diff 443fd64 7fdf56c`で新しいHasX/HasY guardsと2関連testファイルを確認した。
- 最終確認でもHEADは`7fdf56c`で同じ。他作業により2関連testファイルが未commit変更となっていたが、本reportは固定commit内容だけを対象とし、その後の変更を評価していない。
- committed実装報告の修正確認対応表、追加coverage節、初回reportに追記されたmatrix、前回reportの追加coverage節を読んだ。これらの追記をreviewerによる正式closureと取り違えない。
- parent attestation/committed report: focused **46 passed**。最新full suite **1,370 passed / 2 skipped / 8 failed**。8件は既存Windows/Linux verifier群。`git diff --check` passed。`%TEMP%/issue54-focused-tests.log`と`issue54-full-tests.log`の末尾も読み、概要件数と照合した。
- 新しいCI、Docker/full match、POST_GAME実通信の証拠は今回の委任に含まれず、未確認。親は本HEADをpushedと指定したが、reviewerはpush/remote CIを独自取得していない。
- source、workflow、testを編集していない。test/build/replay/CI dispatch/Docker/commit/push/comment/mergeは実行していない。本reportだけを書いた。

## Coverage・次の操作

| bounded criterion | disposition |
| --- | --- |
| 同じfinding ID/severity/reviewerの継続 | checked_no_finding |
| 四つのproduction修正と必要な直接依存 | checked_no_finding（原原因への修正あり） |
| completeness matrix・actual composition fixture | checked_finding（NR-001/004の上記セルのみ） |
| 追加修正による直接回帰 | checked_no_finding（本限定範囲） |
| exact-HEAD CI / Linux MATCH・実POST_GAME | unexplored |
| 新しい全体レビュー・無関係箇所 | not_applicable |

上記の限定fixtureとfocused evidenceを揃えれば、同じreviewerが4件のclosureを継続できる。別のfull reviewを始める必要はない。NR-002/003について新しい修正要求はない。

保存run `42532919016` のcounter42 BALL_PLACEMENT_YELLOW、counter43 STOP/NEXT FORCE_START、残り17秒、MaximumTimeReached/POST_GAME未達という事実は保持する。修正後の実MATCH成功やAutoRef/Craneの因果は推定しない。

- reserved_report_paths: 本report。
- report_attestation_allowed: `false`（独立最終レビューのattestationではない）。
- incompleteの根拠: 既読review-workerの「required cellがabsent/partialならclosureを開始しない」という明示的規定。新しいユーザー承認を求めるものではない。

### 通常レビュー最終の残セル

7fdf56cで残ったNR-001について、snapshot自体がnull、直前readの古いstage/API command、FORCE_START blocked / READY_AUTO / continuation issueありを送信なしとするnegative fixtureを追加。NR-004について、256件満杯のあと10秒sampling対象外の最終ballをEOF時に追加し、truncated JSON flagと最終ballを保持するfixtureを追加。focused suiteは53件pass、全体suiteは実行中。
