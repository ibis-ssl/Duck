# ASPIRE-005 Crane startup/readiness 通常レビュー

## レビュー識別

- Review mode: initial normal review（独立最終レビューではない）
- Reviewed implementation HEAD: `530b14dee10938bdce5cbab899c95c80768316f5`
- Base: `0119daa6b232375ec58a14473eec726d257b4f6d`
- Reviewed range: `0119daa6b232375ec58a14473eec726d257b4f6d..530b14dee10938bdce5cbab899c95c80768316f5`
- 対象commit:
  - `0ecd57aca47e8f8342600da090a65515ce5841ec` `fix(aspire): match game controller process`
  - `530b14dee10938bdce5cbab899c95c80768316f5` `fix(ci): wait for full Aspire stack readiness`
- Reviewer: `/root/aspire005_normal_review`
- 独立性: このreviewerは対象2 commitの実装者ではなく、このレビュー中にsource、workflow、testを編集していない。通常レビュー専任であり、independent-final-reviewの独立性を主張しない。
- レビュー開始時と完了前に `git rev-parse HEAD` が上記SHAと一致し、対象外の追跡差分がないことを確認した。

## Findings（severity順）

### N1 — medium — startup gateの合成契約がworkflow文字列testに留まる

- Origin: initial normal review。
- 場所: `scripts/tests/test_wait_aspire_stack_readiness.py:31-56`、production pathは `.github/workflows/dotnet-test.yml:443-553`。
- 内容: helper単体はall-ready same cycle、timeout、AppHost exitを実行testしているが、workflow側の回帰testはhelper名、acceptanceの `if`、owner/stack filter文字列の存在だけを検査する。HTTP 200が現在のAppHostのwrapperと結び付かないstale-listenerケース、helper成功後にsame-stack containerが0/複数/wrong stateとなるケース、そこでacceptanceが起動しないことを実行していない。文字列が残っていてもshellの条件やexit処理が変わればtestは通り得る。
- 影響: CRANE-START-002の原因だった「準備完了前にacceptance開始」を防ぐ主要契約に対し、production workflowの合成回帰が弱い。現在の実装は静的には正しいが、将来のshell編集で同じ欠陥を再導入してもfocused testが検出できない可能性がある。
- Evidence: `test_workflow_gates_motion_acceptance_on_the_full_stack_readiness_probe` は `assertIn` / `assertNotIn`のみ。`test_workflow_diagnostics_and_cleanup_are_limited_to_the_run_owned_stack` も文字列検査のみ。stale listenerを模したHTTP serverやfake Docker結果をworkflow gateと組み合わせるfixtureはない。
- Required action: readiness + same-stack owner verificationを実行可能なhelperへ寄せ、fake HTTP/fake Dockerで少なくとも (1) stale 200 + current-stack containerなし、(2) 4 ready + exact current-stack containers、(3) missing/duplicate/not-running resource を検証するか、同等のcomposition fixtureでstartup failure時にacceptance commandが実行されないことを証明する。

### Held H1 — exact-head hosted runtime evidenceなし（verdict blocking）

- 分類: validation hold。source findingではない。
- 対象: `530b14dee10938bdce5cbab899c95c80768316f5` 全体。
- 内容: 親から現在HEADをpush済みとの連絡は受けたが、このレビュー時点で同一SHAの終端済みGitHub Actions結果は未提示。ローカル環境にはDocker CLIがなく、4つの実image、host network、wrapper `/health/ready`、Crane実起動、UDP motion、Duck tracker出力、AppHost停止と所有container cleanupを実行できない。
- 影響: 静的・focused validationは通っているが、この変更の主目的であるLinux hosted stackの実挙動は未証明。`pass`にはできない。
- 必要な後続: 通常push後、同一SHAの `.NET tests` と `ASPIRE full-stack Linux acceptance` を終端まで確認し、下記「exact-head CIで確認する項目」を満たすこと。

### Held H2 — cleanup stepの成功はjob statusだけでは証明されない

- 分類: residual validation risk。現時点ではsource findingに昇格しない。
- 場所: `.github/workflows/dotnet-test.yml:584-715`。
- 内容: cleanup stepは `set +e` で動き、削除後のsame-stack一覧を `docker-containers-after.txt` に保存する。安全側のowner/stack/resource/name/network条件で削除対象を限定している一方、`docker rm`失敗や残存container自体をstep failureへ昇格しない。
- 影響: exact-head runがgreenでも、停止・cleanup受入はartifactの残存一覧とremove logを確認するまで成立しない。
- 必要な後続: current-head artifactで `docker-containers-after.txt` が空、各remove logが成功、AppHost停止後に対象stackのcontainerが残っていないことを確認する。残存があればfindingとして扱う。

## 要件適合性

### CRANE-START-001: Game Controller process契約

- `Testing/Duck.Testing.AppHost/Program.cs:55-69` は固定image `robocupssl/ssl-game-controller:3.20.3` を保持し、`ExpectedProcessName`だけを `java` から `app` に変更している。
- 既存の `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs:69-93` はimage、`ExpectedProcessName == "app"`、既存args、11003の唯一producerを同時に固定する。
- ports、Yellow team、Crane planner、Simulator/CM4経路、Game Controller argsは対象rangeで変更されていない。
- supplied red evidence: exact-head run `37201339890`（HEAD `0119daa...`）は Expected `app` / Actual `java` で1件失敗、375件成功。
- supplied green evidence: run `37202666936`（HEAD `0ecd57a...`）で.NET tests成功。今回ローカルでもmodel tests 6/6成功。

### CRANE-START-002: acceptance開始前のfull-stack readiness

- `scripts/wait_aspire_stack_readiness.py` は `simulator:39101`、`game-controller:39102`、`cm4-sim:39103`、`crane:39104` の全 `/health/ready` が同一poll cycleでHTTP 200になるまで待つ。AppHost PID終了とtimeoutを別のstartup failure reasonとして返す。
- workflowはAppHost起動時に `Testing__StackOwnership__StackId=gha-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT` を渡し、readiness成功後にowner/stack/resource labelで各containerが1つかつrunningであることを確認する。
- motion acceptance stepは `steps.aspire_full_stack_ready.outcome == 'success'` のときだけ開始する。旧Game Controller image/running-only gateは削除されている。
- Crane wrapperの既存依存 `WaitForStart(duck)`、`WaitFor(gameController)`、`WaitFor(cm4Simulator)` は変更されていない。したがってCrane `/health/ready` 成功は、同じAppHostでDuck projectが少なくともstartedであることを含む。
- readiness判定はwrapper自身の `/health/ready` を直接使用し、DCP表示上の `Service Ready` を証拠にしていない。
- supplied prior-run evidence: `37202666936`（HEAD `0ecd57a...`）では旧gateが全wrapper準備前にacceptanceを始め、`active_robot_motion_wait` で `UDP_MOTION_FAILURE`。DCP artifactではCrane RuntimeHost readinessが最初のacceptance stepより後だった。今回のgateはこの順序欠陥を直接解消する構造になっている。

### stale listenerの静的追跡

- `DockerContainerWrapper.RunAsync` は `DockerContainerWrapper.cs:66` でhealth listenerを開始し、その後 `:83` で `docker run`を実行する。固定portを古いlistenerが占有していれば、新wrapperはcontainer作成前に失敗する。
- 仮に古いlistenerが `/health/ready` へ200を返してhelperを満たしても、古いrunのcontainerは今回固有の `gha-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT` stack labelに一致しない。workflowの後段は4 resourceごとにcurrent-stack containerが厳密に1つ必要であり、0件なら `STARTUP_READINESS_FAILURE` で終了する。
- したがって通常の起動順序では、stale listener単独でacceptanceを開始させる経路は見つからない。ただし、この2段階契約を合成fixtureで固定していない点がN1である。

### failure分類

- readiness helper failureと、readiness後のowned-container整合失敗は `startup-status.txt` に `failure_category=STARTUP_READINESS_FAILURE` を記録し、acceptanceを開始しない。
- acceptance側の既存 `UDP_MOTION_FAILURE` は `AspireFullStackAcceptanceTests` のactive motion段階に残り、startup readiness failureと混同されない。
- referee、vision、Duck tracker outputも既存の個別failure名を維持する。

### ownership、diagnostics、cleanup

- startup diagnosticsはowner + stack + resourceで対象を絞り、container inspect/process/logをresource単位で保存する。
- cleanup fallbackはpreflight snapshot後に生成され、owner + stackに一致し、許可された4 resource、nonempty run label、期待name、host networkを満たすcontainer IDだけを削除候補にする。image cache削除、daemon設定変更、全container cleanupはない。
- wrapper本体のexact ownership cleanup契約と、Game Controllerが唯一のreferee producerである構成は変更されていない。
- H2のとおり、実際の停止完了と残存なしはcurrent-head artifact確認が必要。

### secretsとevidence

- startup AppHost tail、container inspect/top/logは `sanitize` を通す。host process一覧はargumentsを出さず `comm`だけを出す。
- cleanup時にAppHost log、acceptance stdout/stderr/result、TRX、resource inspect/logを無害化する。
- sanitizerはAspire dashboard login URLの `?t=` / `&t=` tokenを追加でredactし、focused testでsentinel非残存を確認する。
- full environment dump、認証file、token読取りを追加していない。

## Coverage matrix

| Criterion | Disposition | Evidence |
| --- | --- | --- |
| requirement / design conformance | `checked_no_finding` | 001 process契約、002 all-wrapper gate、Duck `WaitForStart`含意、failure分類をdiffと直接依存で照合。 |
| correctness / edge cases | `checked_no_finding` | all-ready same cycle、503/unreachable、AppHost exit、timeout、readiness後のsame-stack running確認を確認。実DockerはH1。 |
| scope discipline / unrelated changes | `checked_no_finding` | 6 files、GC process 1行とreadiness/workflow/secret-redactionの範囲。port/team/planner/referee producer変更なし。 |
| changed files / direct dependencies | `checked_no_finding` | workflow、Program、2 scripts、2 Python testsに加えDocker wrapper、resource extension/spec、model tests、full-stack acceptance testを確認。 |
| API / data / configuration compatibility | `checked_no_finding` | 新規外部APIなし。stack IDは既存 .NET config形式、固定health portsとAppHost modelを回帰testで照合。 |
| workflow behavior | `checked_no_finding` | acceptance `if`、startup failure exit、owner/stack filtering、always cleanup、artifact uploadを確認。H1/H2はruntime evidence hold。 |
| error handling / diagnostics | `checked_no_finding` | startup category、probe history、sanitized resource evidence、UDP motion failure分離を確認。 |
| security / secret handling | `checked_no_finding` | dashboard token regex、argument省略、artifact sanitization経路を確認。 |
| tests / local validation | `checked_finding` | Python 10/10、AppHost model 6/6、`git diff --check`成功。workflow wiringが文字列testのみであるためN1。 |
| current-HEAD CI / hosted acceptance | `held` | 親からpush済みとの連絡はあるが、`530b14d...` の終端済みmatching CI結果は未提示。Docker unavailable。 |
| documentation / reports / tracking accuracy | `not_applicable` | 対象rangeに設計・tracking変更なし。既存設計を直接依存として確認。 |
| regression / maintainability | `checked_finding` | readiness helperはstdlibのみでportsも集約されるが、workflow合成契約の回帰が文字列testに留まる（N1）。 |

## Validation assessment

このreviewerが `530b14dee10938bdce5cbab899c95c80768316f5` で実行したもの:

- `python -m unittest discover -s scripts/tests -p 'test_*.py' -v`: 10/10 pass。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~Tracker.Tests.AppHostApplicationModelTests' --logger 'console;verbosity=minimal'`: 6/6 pass。NuGet vulnerability feedへ接続できない `NU1900` warningと既存 `ASPIRE010` warningは出たが、test failureなし。
- `git diff --check 0119daa6b232375ec58a14473eec726d257b4f6d..530b14dee10938bdce5cbab899c95c80768316f5`: pass。
- ローカルshellに `bash` がなく、このreviewerはworkflow blockの `bash -n` を再実行できなかった。実装側から提供された証拠では10個のworkflow `run` blockが `bash -n` pass。

提供済みだが、このreviewerが再実行していないもの:

- local full .NET suite: 365/376 pass、11件は既知のWindows file-sharing failure。
- run `37201339890` at `0119daa...`: 375 pass / 1 fail（Expected `app`, Actual `java`）、full-stack skip。
- run `37202666936` at `0ecd57a...`: .NET pass、旧full-stack gateで `UDP_MOTION_FAILURE`。

未実施:

- Docker image起動、host-network UDP、実wrapper `/health/ready`、Crane motion、Duck output、停止cleanup。
- exact-head `530b14d...` のGitHub Actions。

## Finding completeness matrix

| Finding | Required action | Production path | Composition fixture | Focused evidence | Disposition |
| --- | --- | --- | --- | --- | --- |
| N1 (medium) | readinessとcurrent-stack owner検査を実行回帰化し、startup failure時にacceptance非起動を証明 | `.github/workflows/dotnet-test.yml:443-553`、`scripts/wait_aspire_stack_readiness.py` | stale listener / exact current stack / missing・duplicate・not-running resourceを含むfake HTTP + fake Docker相当 | 未実装。現状はhelper unitとworkflow文字列testのみ | incomplete |

H1とH2はcurrent-head hosted validationで判定するholdである。

## Verdict

**fail**。

productionの静的追跡ではCRANE-START-001のred→green契約と、CRANE-START-002のacceptance前all-wrapper `/health/ready` gateは要求に適合し、stale listener単独のbypassも見つからない。しかし主要な合成契約をworkflow文字列testでしか固定していないN1 (medium) があるためfail。加えて、同一SHA `530b14dee10938bdce5cbab899c95c80768316f5` のhosted full-stack CIとartifact確認が必要。

## exact-head CIで確認する項目

1. `.NET tests` とPython regressionが同一SHAでpassする。
2. `wrapper-readiness.json` が4 resourceを同一pollで200として記録し、`startup-status.txt` が `readiness_source=all wrapper /health/ready endpoints` を示す。
3. 4つのinspect evidenceがowner、同一stack、resource、run、期待image、`NetworkMode=host`を示す。
4. acceptance evidenceがGame ControllerのHALT→active、Yellow robot 100 mm以上のmotion、Duck `ibis` tracker outputを示す。失敗時はstartup failureとUDP motion failureが別分類になる。
5. Crane readinessがacceptance開始より前である。DCP `Service Ready`表示だけを根拠にしない。
6. cleanup artifactでsame-stack containerの残存がなく、remove failureがなく、他stack/container/image/daemon設定に作用していない。
7. artifactにAspire dashboard login token、Bearer、password/secret/key sentinelまたはfull environment dumpが残っていない。

## Remaining risks / unexplored areas

- hosted runner上の4 fixed imagesと実process名、ROS graph、cm4 listener、multicast経路はcurrent HEADで未確認。
- readiness成功直後からacceptance終了までのservice継続性は実runでのみ確認できる。
- cleanup stepは残存をjob failureにしないため、H2のartifact確認が必要。
- N1が閉じるまで、workflow shellの条件・exit semanticsに対する回帰防止は静的文字列検査に依存する。
- Windows local full-suite 11 failureは今回のrange外という提供情報に依存し、このreviewerは個別差分比較を再実施していない。

## Next action

N1のcomposition regressionを追加してfocused testを実行する。現在push済みの `530b14dee10938bdce5cbab899c95c80768316f5` についてはmatching CIを終端まで確認し、成功・失敗いずれでもfull-stack artifactを確認する。新HEADではN1のfix verificationとH1/H2のCI delta確認を同じ通常reviewerへ戻す。新PR、force push、merge、deploy、GitHub commentはこのレビューの対象外。

## Review lifecycle metadata

- `initial_independent_reviewed_head`: `null`
- independent-review continuity: `not_applicable`
- severity reclassification records: none
- reserved report paths: none（通常レビューreportとして直接保存）
- `report_attestation_allowed`: `false`（independent-final-reviewではない）

---

## Follow-up normal review — `bfaed12f48c1385d1a03d026bca864a3b599346f`

### レビュー識別

- Review mode: normal review fix verification。同じreviewer `/root/aspire005_normal_review` を継続使用。
- Reviewed implementation HEAD: `bfaed12f48c1385d1a03d026bca864a3b599346f`
- Original base: `0119daa6b232375ec58a14473eec726d257b4f6d`
- Follow-up delta: `530b14dee10938bdce5cbab899c95c80768316f5..bfaed12f48c1385d1a03d026bca864a3b599346f`
- Commit: `bfaed12f48c1385d1a03d026bca864a3b599346f` `test(ci): verify Aspire readiness ownership gate`
- レビュー開始時と完了前にHEAD一致を確認。既存の本report以外に未追跡・未commit差分なし。実装、test、workflowは編集していない。

### Findings（severity順）

#### N2 — medium — Craneの内側360秒timeoutを外側300秒gateが先に打ち切る

- Origin: follow-up normal reviewで新規発見。
- 場所: `Testing/Duck.Testing.AppHost/Program.cs:108-125`、`.github/workflows/dotnet-test.yml:513-519`、契約を固定するtestは `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs:61-67` と `scripts/tests/test_wait_aspire_stack_readiness.py:38-50`。
- 内容: Crane wrapperの `StartupTimeoutSeconds` は360秒へ延長されたが、AppHost開始直後から計測するworkflow readiness helperは300秒で終了する。Crane wrapperはSimulator、Duck start、Game Controller、cm4-sim readinessを待ってから起動するため、Crane自身が利用できる外側残時間は300秒未満になる。内側timeoutの300〜360秒部分は到達不能である。
- 影響: Craneがwrapper契約上は正常な時間内である300〜360秒、または依存起動時間を含めて外側300秒を超えてreadyになった場合、workflowは `STARTUP_READINESS_FAILURE` として先に失敗し、`always()` cleanupを開始する。今回追加した360秒緩和が実動作上無効になり、cold runnerでfalse startup failureになり得る。
- Evidence: model testはCrane `360`をassertし、workflow testは外側 `--timeout-seconds 300`をassertしており、現行test自身が逆転した契約を固定している。helperのdeadlineは `wait_aspire_stack_readiness.py:124` で呼出し開始時に設定される。
- Required action: 外側gateを依存chain + 最大wrapper startup timeout + probe/diagnostic marginより長くするか、Crane内側timeoutを外側より短く戻す。両値を別々に文字列固定するだけでなく、外側timeoutが最大内側timeoutを上回る関係を回帰testでassertする。

### N1 disposition — closed

- Source finding: N1 medium（`530b14dee10938bdce5cbab899c95c80768316f5` review）。severity変更なし。
- Required action coverage:
  - Production path: `verify_owned_stack` を `scripts/wait_aspire_stack_readiness.py:56-102` に追加し、workflowから `--stack-id "$stack_id"` を渡す。
  - Ordering: `wait_for_readiness` は4 health endpointが同じcycleですべて200になった分岐内 (`:147-160`) でだけ `verify_stack(stack_id)` を呼ぶ。200未達、AppHost exitではowner検査を呼ばない。
  - Exact composition: resourceごとにowner、今回のstack、resource、nonempty run label、`/duck-$stack_id-$resource` name、`State.Running is true`、host networkを要求し、container数は厳密に1つ。
  - Stale listener / missing stack: `test_stale_health_200_without_this_runs_owned_stack_blocks_readiness` は全health 200でもcurrent-stack container 0で `owned_stack_mismatch` を返す。
  - Success outcome: exact 4-container fixtureは `wait_for_readiness.status == ready` と `owned_stack_valid == true` をassertする。
  - Failure outcomes: duplicate、not-running、wrong-stackはそれぞれ `failed` / `owned_stack_mismatch`。missingはstale-listener fixtureで確認。
  - Workflow binding: helperがnon-readyならCLI `main` は1を返し、readiness stepがfailureになる。acceptance stepは `steps.aspire_full_stack_ready.outcome == 'success'` の場合だけ実行する既存条件を保持する。
- 結論: stale fixed-port listenerとcurrent AppHost stackの誤結合を防ぐproduction検査がhelperに統合され、N1のcomposition regression要件を満たした。N1はclosed。

### Cleanup assessment

- readiness helperがtimeoutまたは `owned_stack_mismatch` を返すと、workflowの `fail_startup` が `failure_category=STARTUP_READINESS_FAILURE` とdiagnosticsを保存してexit 1にする。
- cleanup stepは引き続き `if: ${{ always() }}` で実行される。AppHostへINT、TERM、KILLの順で停止を試みた後、owner + current stack + resource allowlist + run label + expected name + host networkを満たすcontainerだけをfallback削除する。
- `bfaed12` はcleanup選択条件や広さを変更しておらず、他stack、image cache、daemon設定への作用を追加していない。
- 初回reviewのH2は継続する。cleanupは `set +e` で、remove failureやsame-stack残存をjob failureに昇格しないため、exact-head artifactの `docker-containers-after.txt` とremove log確認が必要。
- N2のtimeout逆転により、Craneがまだ有効な360秒window内でも外側がcleanupを開始する点はcleanup自体の所有権問題ではなく、startup gateの早期打切り問題である。

### Follow-up coverage matrix

| Criterion | Disposition | Evidence |
| --- | --- | --- |
| N1 stale listener / exact stack | `checked_no_finding` | all-200後だけstack検査。stale 200、missing、exact stack testあり。 |
| owner identity | `checked_no_finding` | owner/stack/resource/run label/name/running/host networkをinspect JSONで検証。 |
| duplicate / wrong-state handling | `checked_no_finding` | duplicate、stopped、wrong-stack fixtureが `owned_stack_mismatch`。 |
| acceptance outcome | `checked_no_finding` | valid fixtureはready、invalid fixtureはfailed。workflowはreadiness step successだけをacceptance条件にする。 |
| timeout consistency | `checked_finding` | Crane inner 360秒 > outer gate 300秒（N2）。 |
| cleanup ownership / failure path | `checked_no_finding` | `always()`とcurrent-stack限定fallbackを保持。残存の自動failure化はH2。 |
| ports/team/planner/referee producer | `checked_no_finding` | timeout引数以外のCrane config、GC process/producer、ports、team、planner変更なし。 |
| secret handling | `checked_no_finding` | follow-up deltaにartifact/log出力拡大なし。既存sanitize経路を保持。 |
| current-HEAD hosted CI | `held` | このreviewerは `bfaed12...` の終端済みfull-stack artifactを受領・確認していない。 |

### Follow-up validation

`bfaed12f48c1385d1a03d026bca864a3b599346f` で実行:

- `python -m unittest discover -s scripts/tests -p 'test_*.py' -v`: 12/12 pass。HTTPError fixtureのcleanupに関するPython `ResourceWarning`が1件出たがtest failureなし。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~Tracker.Tests.AppHostApplicationModelTests' --logger 'console;verbosity=minimal'`: 6/6 pass。既知の `NU1900` / `ASPIRE010` warningのみ。
- `git diff --check 530b14dee10938bdce5cbab899c95c80768316f5..bfaed12f48c1385d1a03d026bca864a3b599346f`: pass。
- Docker CLIはローカル利用不可のため、実container compositionとcleanupは未実施。

### Finding completeness matrix

| Finding | Required action | Production path | Composition fixture | Focused evidence | Disposition |
| --- | --- | --- | --- | --- | --- |
| N1 (medium) | health成功後にcurrent-stack exact ownershipを検査し、valid/invalid outcomeを固定 | `wait_aspire_stack_readiness.py:56-160` + workflow `--stack-id` | stale/missing、exact、duplicate、stopped、wrong-stack fake Docker fixture | Python 12/12 pass | closed |
| N2 (medium) | outer gateを最大inner timeoutと依存時間より長くし、大小関係をtestで固定 | Program Crane timeout + workflow readiness timeout | timeout relationship test | 未実装。現行testは360と300を別々に固定 | incomplete |

### Follow-up verdict

**fail**。

N1 mediumはclosed。owner verificationはall-health-200後だけ実行され、stale listener、current stack、missing、duplicate、not-running、wrong-stackとready/failed outcomeがfocused testで固定された。新規N2 mediumとして、Crane wrapperの360秒timeoutを外側300秒gateが先に終了させる矛盾がある。N2修正と同一SHA hosted full-stack / cleanup artifact確認後に、同じ通常reviewerでbounded fix verificationを行う。

---

## Timeout semantics reconsideration — `bfaed12f48c1385d1a03d026bca864a3b599346f`

### Clarified contract

- Workflowの300秒は、stack全体がacceptanceを開始できる状態になるまでのauthoritative upper boundである。
- Crane wrapperの `StartupTimeoutSeconds=360` はworkflowが360秒待つという契約ではない。wrapper readinessが成立しないときにwrapper自身がcontainerをcleanupして終了するまでの内側deadlineである。
- 外側300秒が先に失敗した時点でもCrane containerを残し、`collect_startup_diagnostics` がinspect、process、bounded log tailを取得できるよう、内側deadlineを外側より長くしている。
- Crane wrapperは `WaitForStart(duck)`、Game Controller readiness、cm4-sim readinessの後に起動する。したがって内側360秒の時計は外側stack時計より遅く始まり、外側300秒時点の診断余裕は最低60秒、通常は依存起動時間分だけ長い。
- diagnostics後は `if: ${{ always() }}` cleanupがAppHostを停止し、wrapperのexact-owner cleanupとcurrent-stack限定fallback cleanupを実行する。通常経路でinner 360秒満了を待つ必要はない。

### N2 reclassification record

- Finding identity: N2。
- Source classification: medium required finding。
- New classification: withdrawn / `checked_no_finding`。
- Reason: 初回follow-upはinner 360秒を「workflowが許容すると約束するready時間」と解釈した。この解釈は誤りであり、outer 300秒はstack acceptanceの明示的上限、inner 360秒はouter failure時のdiagnostic evidenceを保持するための自己cleanup猶予である。`inner > outer` は矛盾ではなく意図した順序である。
- Approving/clarifying authority: task ownerから親agent経由で提示されたtimeout semantics（2026-10-04 follow-up）。reviewerがsourceで起動順序とcleanup flowを再確認した。
- Code change required: none。

### Safe relationship assessment

この設計で必要な関係は値の一致ではなく、次である。

`stack readiness outer bound < Crane self-timeout`

さらにdiagnosticsを確実に採るには、概念上は次のmarginを持たせる。

`Crane self-timeout >= outer bound + diagnostic collection budget`

現設定は `360 >= 300 + 60` で最低60秒のmarginを持ち、依存待ち時間が追加marginになる。workflow全体の45分timeoutも、300秒gate、startup diagnostics、AppHost graceful stop、fallback cleanup、artifact uploadを包含する。diagnostic shell command個々のhard timeoutは定義されていないため、daemon hangまでを360秒で保証する契約ではないが、このPRの通常Docker応答経路にrequired findingは認定しない。

### Current findings and verdict

- N1 medium: closed at `bfaed12f48c1385d1a03d026bca864a3b599346f`。
- N2 medium: withdrawn as a false positive after contract clarification。severityの黙示変更ではなく、上記reclassification recordを正本とする。
- H2: cleanup stepがsame-stack残存をjob failureへ昇格しないため、artifact確認が必要というvalidation holdは継続。
- Current-head hosted full-stack result/artifactはこのreviewerが未確認。

Current verdict: **incomplete**。required source findingはない。`bfaed12f48c1385d1a03d026bca864a3b599346f` のhosted full-stack、startup diagnostics、motion/Duck output、cleanup残存なし、secret redactionを同一SHA artifactで確認すればnormal reviewをpassへ進められる。

---

## Pre-edit design review — bounded Crane ROS graph probe

### Review input

- Design-only review。implementation edit、test edit、pushは行っていない。
- Source baseline: `bfaed12f48c1385d1a03d026bca864a3b599346f`。
- Supplied exact-head hosted evidence: outer readiness timeout時もCrane containerはowned/running、`crane_session_coordinator_node` processが存在し、同時に `docker top` 上で `bash -lc 'source ... && ros2 node list'` と `_local_setup_util.py` childが生存していた。probe stdoutは得られていない。
- Root-cause strength: unbounded `docker exec` probeがblockingしている疑いは強い。artifactはnode-name predicate不一致を証明していないため、`CraneHasCoordinator`条件を変更する根拠にはしない。

### Design verdict

**pass_with_held**。提案はsoundかつscope-safeである。

- 変更対象をCrane readinessの1回のcontainer commandへ限定する。
- required service evidenceは引き続きROS graphのcoordinator nodeであり、process existenceへ弱めない。
- timeout、command nonzero、coordinator不在はいずれもnot-readyとして扱い、wrapper startup deadlineまで再試行する。
- owner inspection、expected process、他service readiness、dependency graph、ports/team/planner、GC唯一producer、cleanupは変更しない。
- Held: pinned Crane image内で選択した `timeout` optionが実際に利用可能であることと、新commandがhosted containerで10秒以内に終了することは、このローカルDockerなしreviewでは未実証。既存image以外の導入やpackage installは行わず、focused fake testと次のhosted artifactで確認する。

### Recommended exact command

Docker CLIへshell文字列として連結せず、次のargument arrayを渡す。

```csharp
[
    "exec", containerId,
    "timeout", "--signal=TERM", "--kill-after=2s", "10s",
    "bash", "-lc",
    "source /root/ibis_ws/install/setup.bash && exec ros2 node list",
]
```

実行形は次と同義である。

```text
docker exec <container-id> timeout --signal=TERM --kill-after=2s 10s \
  bash -lc 'source /root/ibis_ws/install/setup.bash && exec ros2 node list'
```

理由:

- `timeout` はcontainer内に置き、Docker CLI接続ではなく実際のsetup + ROS commandを10秒で制限する。
- `--signal=TERM` で通常終了を試し、`--kill-after=2s` でTERMを無視するcommand/descendantを有限時間後にKILLする。
- `exec ros2 node list` によりsetup完了後のbashをROS CLIへ置換し、不要な中間shellを残さない。
- GNU `timeout` の既定process-group制御を使う。`--foreground` はchildをtimeout対象外にし得るため付けない。
- host側は既存 `DockerCliProcess.CreateStartInfo` のdirect argvを使い、追加のhost shell、string interpolation、secret-bearing environment dumpを導入しない。

### Result semantics

- Exit 0かつ `CraneHasCoordinator(stdout) == true`: ready。
- Exit 0かつcoordinator不在: not-ready、再試行。
- Exit 124（10秒timeout）: not-ready、再試行。
- Exit 137またはその他nonzero: not-ready、再試行。
- AppHost/wrapper cancellation: 既存cancellationを優先して抜け、通常のexact-owner cleanupへ進む。
- command failureをwrapper fatal exceptionへ変換しない。container ownership/process healthの失敗は既存どおり別経路でfatalにする。

### Utility and descendant-process risks

1. **Utility availability:** `timeout`不在、またはlong option非対応ならexit 127/usage errorとなり、常にnot-readyになる。実装前提としてpinned imageで `timeout --version` または同等のread-only確認が必要。GNU coreutilsが確認できない場合は、imageが対応する短option `timeout -k 2s 10s ...` を使い、そのexact formをtest/artifactへ固定する。新package導入で補わない。
2. **Descendant kill:** setup中の `_local_setup_util.py` やROS CLI childまで制限する必要がある。GNU `timeout`のdefault process group + kill-afterを使い、`--foreground`を避ける。単なる `timeout 10s`だけではTERM非応答childが残る可能性がある。
3. **Docker CLI cancellation boundary:** host側 `process.Kill(entireProcessTree: true)` はhostのDocker CLI treeを止めるが、container内exec childの即時終了を単独では保証しない。container内timeoutが最大12秒でchild groupを止め、その後のAppHost cleanupがcontainer stop/removeを行う二重boundとする。
4. **Polling cadence:** readiness loopの1秒delayに加え、hung probeは1回最大約12秒となる。これはouter 300秒内で有限回retryでき、unboundedな単一probeで全gateを停止する現状を解消する。
5. **Evidence:** stdoutが得られたexit 0の場合だけnode predicateを評価する。timeout/nonzero時の空stdoutを「node不存在の証明」と表現しない。

### Required focused tests

fake Docker lifecycle fixtureで最低限次を固定する。

1. Crane profileが上記exact argvを使うこと。既存の曖昧な `exec container-id bash -lc` substring assertionを置き換える。
2. fake `docker exec`がexit 124を返すcaseで、wrapperが即fatalにならず複数回probeし、startup deadline後にnonzero終了してexact owned-container cleanupを行うこと。
3. fake `docker exec`がexit 0 + `/other_node`を返すcaseで、process existenceだけではreadyにならず、probeを再試行して最終的にnot-ready/cleanupとなること。
4. exit 0 + `/crane_session_coordinator`でreadyになる既存predicate contractを保持すること。
5. traceに `--foreground` がなく、`--kill-after`と10秒boundが含まれること。

Fake Docker scriptは現在 `exec`をdefault exit 9に落としており、nonzeroがnot-readyになることは間接的に通るが、124 retry、exit 0 coordinator absent、exact timeout argvを別々に表現できるfixture拡張が必要である。

### Findings

Required design findingなし。

Held verification items:

- H3: pinned Crane imageの `timeout` implementation/optionsはこのreviewer未確認。実装時に既存imageでread-only確認し、選んだargvをtestとhosted evidenceで固定する。
- H4: bounded probe後のhosted runで、`docker top`に古いprobe process/`_local_setup_util.py`がpollをまたいで蓄積しないことを確認する。

### Next action

上記commandとresult semanticsをTDDで実装し、focused lifecycle testを先にred、次にgreenにする。その後same normal reviewerへ実装diffを戻し、exact command、retry、node predicate維持、child cleanup、current-head hosted artifactを確認する。

---

## Implementation follow-up review — `5894182bf8f70cadea18efe4ce347399ddcaaf75`

### Review identity and scope

- Review mode: follow-up normal review by the same reviewer; this is not an independent final review.
- Exact reviewed HEAD: `5894182bf8f70cadea18efe4ce347399ddcaaf75`.
- Prior reviewed HEAD: `bfaed12f48c1385d1a03d026bca864a3b599346f`.
- Reviewed delta: `bfaed12f48c1385d1a03d026bca864a3b599346f..5894182bf8f70cadea18efe4ce347399ddcaaf75`.
- Commit: `5894182 fix(aspire): bound Crane readiness probe`.
- Changed files: `DockerContainerWrapper.cs`, `DockerServiceReadiness.cs`, and `DockerContainerWrapperLifecycleTests.cs` (29 insertions, 3 deletions).
- No implementation files were edited by this reviewer. No push or GitHub comment was made.

### Severity-ranked findings

#### N3 — low — lifecycle tests do not directly prove timeout/absent-node retry

- Location: `Tracker/Tracker.Tests/DockerContainerWrapperLifecycleTests.cs:135-182`.
- The new pure predicate test correctly fixes the exact timeout argv and proves that exit 124 is false, exit 0 without the coordinator is false, and exit 0 with the coordinator is true.
- The existing Linux lifecycle theory invokes the new command and proves a generic nonzero service probe prevents readiness and leads to exact-owner cleanup. Its fake Docker client returns exit 9 for `exec`, uses a one-second startup deadline, and does not assert the number of probe attempts.
- Consequently, the suite does not directly exercise the approved runtime cases of repeated exit 124 or repeated exit 0 with `/other_node`, nor assert at least two `docker exec` attempts before the startup deadline. Production control flow does retry these false results, but that connection is not locked by a focused lifecycle regression.
- Recommended follow-up: add fake-Docker response modes for exit 124 and exit 0 plus `/other_node`; use a deadline long enough for at least two quick fake probes; assert multiple exact probe traces, nonready exit, and `rm --force container-id`. This is a test coverage finding; no production defect was identified in this delta.

No medium or high source findings were identified.

### Implementation and design conformance

- `CreateCraneReadinessProbeArguments` constructs the approved direct argument vector exactly:
  `docker exec <id> timeout --signal=TERM --kill-after=2s 10s bash -lc 'source /root/ibis_ws/install/setup.bash && exec ros2 node list'`.
- `--foreground` is absent. GNU `timeout` can therefore supervise the command process group; `--kill-after=2s` provides a bounded escalation after TERM. `exec ros2 node list` removes the otherwise persistent shell after setup completes.
- `CraneProbeSucceeded` requires both exit code 0 and the unchanged `CraneHasCoordinator(stdout)` predicate. Exit 124, exit 137, utility errors, other nonzero exits, and exit 0 without the exact coordinator all remain false. Process existence was not substituted for ROS graph evidence.
- `IsServiceReadyAsync` returns that Boolean rather than throwing for probe nonzero. During startup, `WaitForServiceReadinessAsync` treats false as not ready, waits one second, and loops until the wrapper startup deadline. Ownership and expected-process checks remain in every iteration.
- Cancellation still propagates through `RunDockerAsync`; the host Docker CLI process tree is killed, then existing exact-owner container cleanup runs under its independent bounded cleanup token. The in-container timeout adds a maximum 10-second TERM point plus a 2-second KILL grace for a stuck probe.
- After readiness has once been established, the existing wrapper health loop treats a subsequent false probe as unhealthy and fails the wrapper. This behavior predates the change and is consistent with the wrapper's existing steady-state health contract; the requested startup retry behavior is present.
- No ports, team color, planner, Game Controller producer path, owner labels, stack labels, or cleanup selection changed in this delta.

### Test and validation assessment

- `git diff --check bfaed12..5894182`: pass.
- Focused local command:
  `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~DockerContainerWrapperLifecycleTests" --logger "console;verbosity=minimal"`: 15/15 reported pass.
- This host is Windows. Linux lifecycle methods return immediately under `OperatingSystem.IsWindows()`, so the local result compiles the production and test code and executes the platform-neutral predicate test, but it does not execute fake-Docker lifecycle behavior. Known `NU1900` network warnings and `ASPIRE010` were nonfatal.
- Exact-head hosted run `37205944801` is bound to `5894182bf8f70cadea18efe4ce347399ddcaaf75`. Its `.NET test` job passed. At the review cutoff, `ASPIRE full-stack Linux acceptance` remained in progress in `Wait for all full-stack resources to report ready`; therefore the pinned-image timeout utility and live descendant cleanup are still held runtime checks.

### Held runtime checks

- H3 remains held until the pinned Crane image demonstrates support for `timeout --signal=TERM --kill-after=2s 10s` in the exact hosted run.
- H4 remains held until hosted evidence shows the bounded probe completes/retries and no old `bash`, `ros2 node list`, or `_local_setup_util.py` descendants accumulate across poll iterations.
- Full acceptance still needs to show all wrapper readiness, exact owned-stack verification, motion/Duck evidence, failure classification, and final same-stack cleanup on this exact SHA.

### Coverage matrix

| Criterion | Disposition | Evidence |
| --- | --- | --- |
| exact process-group timeout argv | `checked_no_finding` | Exact argument-array assertion and production helper match the approved command; no `--foreground`. |
| retry semantics | `checked_finding` | Production startup loop retries every false result; focused lifecycle tests do not prove repeated 124 or coordinator-absent attempts (N3). |
| coordinator graph predicate | `checked_no_finding` | Readiness remains `exitCode == 0 && CraneHasCoordinator(stdout)` with exact node-name matching. |
| cancellation and cleanup | `checked_no_finding` | Existing cancellation and exact-owner cleanup paths are unchanged; local Linux lifecycle execution is unavailable. |
| scope discipline | `checked_no_finding` | Three focused files; no producer, port, team, planner, ownership, or workflow edits. |
| tests | `checked_finding` | Pure contract cases are present and compile/pass; explicit retry lifecycle cases are missing (N3). |
| exact-head hosted runtime | `held` | Run `37205944801` exact SHA: .NET pass, full-stack pending at review cutoff. |

### Follow-up verdict

**incomplete**.

The production implementation matches the approved bounded-probe design, preserves ROS graph coordinator evidence, and sends timeout/nonzero results through the startup not-ready retry path. No blocking implementation finding was found. N3 is a low-severity regression-test gap. A final pass remains withheld until exact-head hosted full-stack evidence resolves H3/H4 and confirms acceptance and cleanup; the relevant hosted job was still running at the review cutoff.

---

## Test-only follow-up review — `05d86269f1ee807b6fbe518c5391bbf43c165b15`

### Review identity and delta

- Exact reviewed HEAD: `05d86269f1ee807b6fbe518c5391bbf43c165b15`.
- Prior reviewed HEAD: `5894182bf8f70cadea18efe4ce347399ddcaaf75`.
- Reviewed delta: `5894182bf8f70cadea18efe4ce347399ddcaaf75..05d86269f1ee807b6fbe518c5391bbf43c165b15`.
- Commit: `05d8626 test(aspire): cover Crane readiness retries`.
- Scope: one test file, 42 insertions and 1 deletion; production implementation is unchanged.
- No implementation or test files were edited by this reviewer. No push or GitHub comment was made.

### Severity-ranked finding

#### N4 — medium — new Linux lifecycle test cannot become ready because its expected-process fixture contradicts its `docker top` output

- Locations: `DockerContainerWrapperLifecycleTests.cs:193-203` and `:466-468`.
- The new test constructs `FakeDockerClient(... processName: "ros2", craneProbeTimeoutResponses: 2)`. This makes fake `docker top` output `ros2`.
- `fake.Options()` still hardcodes `ExpectedProcessName` to `simulator-cli`. Therefore every startup iteration calculates `processReady == false`, including the third and later probes where fake `docker exec` returns exit 0 and `/crane_session_coordinator`.
- The wrapper can never set `/health/ready` to 200, so `WaitForReadyAsync` reaches its five-second test deadline.
- Exact-head hosted evidence confirms this path: run `37206322409`, job `111448145014`, failed only `CraneReadinessRetriesTimeoutsUntilCoordinatorAppearsThenCleansUp` at `WaitForReadyAsync`; totals were 377 passed and 1 failed. The `ASPIRE full-stack Linux acceptance` job was skipped because the .NET prerequisite failed.
- Required fix: set `ExpectedProcessName = "ros2"` in the new test options, or make the fake options use the same configured process name. Then rerun the Linux suite and require the new lifecycle test to pass before treating N3 as closed.

### Intended coverage assessment

- The intended composition is otherwise sound: two fast fake `docker exec` exit-124 responses, a third exit-0 response with the exact coordinator, `/health/ready` 200, cancellation, wrapper exit 0, exactly three bounded-probe traces, and exact-owner removal.
- The existing platform-neutral assertions still establish that exit 124 is not ready, exit 0 with `/other_node` is not ready, and exit 0 with the exact coordinator is ready.
- Once the process fixture is corrected and the Linux test passes, these assertions together cover both the predicate branches and the wrapper retry path. A separate lifecycle case for `/other_node` is not required because it produces the same false result consumed by the now-composed retry loop.
- The exact-three assertion is made immediately after readiness and cancellation, before the two-second steady-state health interval should elapse. This is acceptable for the focused test, though the Linux rerun remains the decisive evidence.

### Validation

- `git diff --check 5894182..05d8626`: pass.
- Windows focused suite: 16/16 reported pass. The new lifecycle method returns immediately on Windows, so this does not validate its shell fixture.
- Hosted Linux exact-head suite: fail, 377 passed / 1 failed, as described in N4.
- Full-stack runtime validation: skipped on this exact HEAD; H3 and H4 remain held.

### N3 disposition

- Previous classification: low test-coverage finding at `5894182`.
- Current disposition: remains open because the added Linux composition test fails before readiness. Its design would close N3 after the process-name fixture is corrected and a Linux run passes.

### Verdict

**fail**.

The production bounded-probe implementation remains free of an identified defect, but the exact-head required .NET check is red due to N4 and the intended retry composition evidence has not passed. Full-stack acceptance is unavailable because it was skipped after the test failure.

---

## Fixture-fix follow-up review — `2094d035dabd11c58ec2175af8cc3a0548d86cad`

### Review identity and delta

- Exact reviewed HEAD: `2094d035dabd11c58ec2175af8cc3a0548d86cad`.
- Prior reviewed HEAD: `05d86269f1ee807b6fbe518c5391bbf43c165b15`.
- Reviewed delta: `05d86269f1ee807b6fbe518c5391bbf43c165b15..2094d035dabd11c58ec2175af8cc3a0548d86cad`.
- Commit: `2094d03 test(crane): align readiness fixture process`.
- Scope: one test-only insertion; production behavior is unchanged.

### Findings and dispositions

No new source finding was identified.

#### N4 disposition — closed

- The new lifecycle test now overrides `ExpectedProcessName = "ros2"`, matching its fake `docker top` output.
- Exact-head hosted run `37206501213` completed the Linux `.NET test` job successfully. This directly exercises the previously failing Linux-only fixture and closes N4.

#### N3 disposition — closed

- The passing Linux composition test now demonstrates the whole startup retry path: two exit-124 probe outcomes remain not ready, the third exact-coordinator result sets `/health/ready` to 200, cancellation returns wrapper exit 0, exactly three bounded probe invocations were recorded before cancellation, and the exact owned container was removed.
- The platform-neutral predicate test separately proves exit 124 is false, exit 0 with `/other_node` is false, and exit 0 with `/crane_session_coordinator` is true.
- These tests are adequate together. Every timeout or coordinator-absent result reaches the same Boolean false branch in `WaitForServiceReadinessAsync`; the Linux lifecycle test proves that branch is retried rather than treated as fatal. A second lifecycle test whose only difference is the false-result origin would not add a distinct control-flow guarantee.

### Validation

- `git diff --check 05d8626..2094d03`: pass.
- Local Windows focused lifecycle suite: 16/16 reported pass; Linux-only bodies remain no-ops locally.
- Exact-head hosted run `37206501213`: `.NET test` job succeeded, including the Linux-only lifecycle composition. Full-stack was still running at this review cutoff.

### Prior full-stack evidence assessment

Run `37205944801` at production HEAD `5894182bf8f70cadea18efe4ce347399ddcaaf75` completed as `STARTUP_READINESS_FAILURE` after the outer 300-second gate:

- Simulator, Game Controller, and cm4-sim wrappers reached 200; Crane remained 503 through poll 151.
- The exact owned Crane container was running with host networking and the intended pinned image.
- `docker top` at failure showed the ROS launch process, `crane_session_coordinator_node`, and the other Crane nodes running. It did not show a probe process in that single snapshot.
- Crane logs showed the coordinator and sender had started, including the sender target `127.0.0.1:12345`.
- The artifact contains no readiness-probe exit code, stdout, stderr, elapsed time, or attempt count. Searches found no `timeout` usage error, wrapper timeout message, or captured `ros2 node list` result.
- Cleanup completed with zero containers remaining and without image removal or daemon changes.

This evidence establishes that the bounded command prevented an indefinitely retained probe and preserved cleanup, but it does not establish why each bounded probe returned not ready. H3 remains unresolved: the artifact cannot distinguish repeated exit 124 from utility/setup failure or exit 0 with unexpected graph output. H4 is only partially supported: the final `docker top` snapshot shows no accumulated probe descendants, but one snapshot is not a full process-lifetime proof.

### Next debugging/design decision

Do not weaken `CraneHasCoordinator`, accept process existence as readiness, or extend the timeout based on this artifact alone. The next functional change is not yet evidence-backed.

If exact-head run `37206501213` repeats Crane 503, the next change should be a diagnostic-only design reviewed by the parent before editing:

1. Keep the 10-second plus 2-second kill bound and exact coordinator predicate.
2. Record a bounded, rate-limited probe outcome containing attempt number, elapsed time, exit code, coordinator-match Boolean, and truncated sanitized stdout/stderr. Do not emit environment variables or setup file contents.
3. Add a constant marker after successful setup and before `exec ros2 node list`; record whether that marker was observed. This separates a blocked `source .../setup.bash` from a blocked ROS graph query without treating the marker as readiness.
4. Preserve cancellation and exact-owner cleanup, and add tests proving diagnostics do not change ready/not-ready semantics or expose unrestricted output.
5. Use the next hosted artifact to choose the functional remedy: investigate setup if the marker is absent, ROS CLI/daemon behavior if the marker is present with exit 124, or node-name/domain discovery if exit 0 lacks the coordinator.

This is a concrete design change because it modifies the probe output contract and artifact logging. It should receive parent design review under the task instruction before implementation. If the current exact-head run reaches readiness, this diagnostic change is unnecessary and the review can proceed to motion and cleanup acceptance evidence.

### Current verdict

**incomplete**.

N3 and N4 are closed, and there are no open source findings in `2094d03`. Exact-head `.NET` validation is green. Final pass remains held on the in-progress exact-head full-stack run and its artifacts. A repeated Crane readiness timeout requires the diagnostic design review above before further functional changes.

---

## Normal design review — Crane probe diagnostics proposal

### Review identity and scope

- Design reviewed: `reports/aspire-005-crane-probe-diagnostics-design-20261004.md`.
- Source baseline checked: `2094d035dabd11c58ec2175af8cc3a0548d86cad`.
- Review mode: normal design review, not implementation review and not independent final review.
- Direct contracts checked: `DockerContainerWrapper`, `DockerServiceReadiness`, the DCP capture sanitizer, full-stack workflow artifact paths, and lifecycle/sanitizer tests.
- Current hosted evidence confirmed: run `37206501213` passed `.NET test` and failed the outer 300-second full-stack readiness gate with Crane still 503; motion acceptance was skipped and cleanup/evidence steps completed.

### Severity-ranked blocking findings

#### D1 — medium — sanitizer integration and the pre-persistence data boundary are not implementable as written

- The proposal requires stdout/stderr excerpts to pass through the existing sanitizer before storage/emission and says to reuse that sanitizer.
- The existing sanitizer is Python in `scripts/capture_aspire_dcp_logs.py`. The probe and its result handling run inside the C# AppHost wrapper. There is no shared callable library between these runtimes, and invoking repository Python from every wrapper probe would add a runtime/path dependency and subprocess behavior not described by the design.
- Relying only on the workflow sanitizer would mean raw free-form probe output is first written into AppHost or DCP logs. That conflicts with the proposal's requirement that bounded structured records contain already-sanitized excerpts and with the test that no raw uncapped copy is retained in stored diagnostics.
- “Representative secret sentinel” is also underspecified. The current sanitizer redacts structured secret fields, bearer values, private-key blocks, and dashboard query tokens; it cannot promise removal of every arbitrary opaque string.

Required design resolution:

1. Choose one in-process boundary. Recommended: add a small C# sanitizer with behavior kept in parity with the existing Python rules through shared input/expected-output fixtures. Do not spawn Python from the wrapper.
2. Sanitize the complete captured string before excerpt truncation so truncation cannot split a secret pattern and evade redaction; persist only the sanitized capped excerpt and flags. Clarify that the current raw `ProcessResult` strings may exist transiently in memory but are never logged or stored in diagnostic state.
3. Serialize the structured event as one JSON line so embedded newlines and quotes cannot create extra records or ambiguous fields.
4. Define sentinel tests using each supported secret shape rather than an unspecified plain token. If the design intends protection from arbitrary unknown secrets, omit free-form excerpts and use allowlisted classifications only.

#### D2 — medium — per-attempt logging, suppression, and final-state retention conflict and lack an outer-timeout path

- The proposal simultaneously says to emit one record for every completed probe, emit no more than once every two seconds, and avoid repeated unchanged payloads. A fast nonzero probe can complete every roughly one second because the wrapper startup loop delays one second, so all three statements cannot hold.
- The wrapper's inner startup deadline is 360 seconds while the authoritative workflow gate fails at 300 seconds. The outer workflow captures diagnostics before stopping AppHost. Therefore a summary emitted only when the wrapper reaches its own timeout will not exist in the failure artifact.
- “Keep the latest outcome available” does not define where that state lives or how `collect_startup_diagnostics` retrieves it. An in-memory latest record alone is inaccessible to the workflow.
- The proposed duration test says multiple durations are “monotonic”; individual attempt durations may validly decrease. The clock must be monotonic, while each measured duration must only be nonnegative.

Required design resolution:

1. Separate observation from emission: every completed attempt updates a per-wrapper state object containing exact attempt count and latest bounded outcome.
2. Define a deterministic emission rule, for example first outcome, classification change, success transition, and an unchanged heartbeat no more often than every two seconds. State which terminal records are exempt from that rate cap.
3. Ensure the outer 300-second artifact can retrieve a recent/latest record before AppHost shutdown. A periodic JSON-line record in an artifact-captured DCP/AppHost log is acceptable only if the design names that path and tests or hosted acceptance prove the record is captured. A dedicated sanitized diagnostic endpoint/file is another option but would require its own ownership, path, and cleanup contract.
4. Define a final summary at wrapper timeout/cancellation for wrapper-local evidence, while acknowledging it does not replace the pre-300-second observable path.
5. Make rate-limit tests deterministic by passing timestamps/elapsed values into a pure state/formatting component or using an injected time source. Assert nonnegative per-attempt duration and correct attempt counts; do not assert that duration values increase across attempts.

#### D3 — medium — marker parsing is not isolated from the readiness predicate, leaving predicate and truncation behavior ambiguous

- The marker makes setup stdout and ROS graph stdout share one stream. The existing `CraneHasCoordinator` scans every line in the complete stdout.
- The design says marker absence remains not-ready, but its tests do not include exit 0 plus a coordinator line with no marker. An implementation could retain the old `exitCode == 0 && CraneHasCoordinator(fullOutput)` expression and accidentally ignore the marker requirement.
- If setup output contains the coordinator text before the marker, scanning the whole output could report ready without graph evidence after setup. If an implementer runs the predicate on the 512-character excerpt, a real coordinator after the cap could incorrectly remain not-ready.
- Marker detection by substring would also allow an error message containing the marker text to be misclassified as completed setup.

Required design resolution:

1. Detect the marker only as an exact normalized stdout line.
2. Require exit 0, one exact marker, and the exact coordinator entry in the full stdout segment after the marker. Keep all readiness classification on full transient output before sanitizing/capping diagnostic excerpts.
3. Add tests for: exit 0 plus coordinator without marker is not ready; coordinator text before marker but absent after marker is not ready; coordinator after the 512-character excerpt boundary is still ready; substring-only marker text is not accepted.
4. Keep the marker as diagnostic/phase framing and never accept it alone as readiness.

### Nonblocking design assessment

- The command keeps `timeout --signal=TERM --kill-after=2s 10s` outside `bash`, omits `--foreground`, and uses `exec ros2 node list`. This preserves the intended process-group timeout and descendant escalation.
- A marker printed after successful `source` and before `exec` can distinguish setup completion from a graph-query timeout because partial stdout is available when GNU timeout returns 124.
- Exit 124 and other nonzero results remain retryable not-ready outcomes. Ownership/process failures remain separate fatal paths, and outer/inner deadline semantics remain unchanged.
- Container ID, environment, arbitrary command lines, ports, team/color, image pin, producer behavior, dependency graph, and cleanup scope need not be added to the diagnostic event.
- A 512-character per-stream persisted excerpt is reasonable once character-versus-byte semantics are fixed. Use one explicit unit, cap after sanitization, and calculate truncation against sanitized text.

### Revised deterministic test and acceptance minimum

In addition to the proposal's tests, the approved design should require:

- Shared sanitizer parity fixtures covering secret fields, bearer tokens, private-key blocks, and dashboard tokens in both stdout and stderr.
- Exact one-line JSON serialization with escaped newline/control characters and no container ID, environment, or command-line fields.
- Exact marker line and post-marker graph parsing cases described in D3, including a coordinator beyond the diagnostic cap.
- State-machine tests for first/change/heartbeat/final emission using supplied time values, while every attempt still increments the internal count.
- A Linux lifecycle test that produces two timeout results, then a coordinator success, and verifies diagnostic state does not alter ready/retry/cleanup behavior.
- A failure lifecycle test that leaves a recent sanitized outcome visible through the same path the outer workflow captures before the 360-second wrapper deadline.
- Hosted artifact acceptance that locates the event in the named sanitized artifact path, validates cap/truncation fields and attempt count, scans for all sentinels, confirms readiness/motion failure categories remain separate, and confirms zero same-stack containers after cleanup.

### Design verdict

**fail** pending D1-D3 resolution.

The diagnostic goal and bounded marker approach are sound, and no timeout/readiness weakening is required. Implementation should not begin from the current text because the sanitizer boundary, emission/retention path, and marker-to-predicate contract permit materially different implementations and do not yet guarantee that the outer 300-second failure artifact receives safe, semantically correct evidence.

### Revised-design re-review

The updated proposal closes most of the original ambiguity:

- D3 is closed. Marker matching is exact-line, coordinator evaluation uses the full post-marker graph segment before excerpt truncation, and the expanded tests cover pre-marker-only coordinator text and a coordinator beyond the excerpt cap.
- D2 is substantially closed. Per-attempt accounting is separated from progress emission, the state-change/two-second rule is deterministic enough to implement with a supplied monotonic time source, and periodic progress gives the outer workflow pre-shutdown evidence.

One blocking issue remains.

#### D1 revised disposition — remains open

The revised document places sanitization only in the workflow but defines the in-process diagnostic excerpt as already capped at 512 characters. It then says the wrapper may write the JSON line to a redirected file while never printing raw excerpts. Under the clarified workflow boundary, that line necessarily contains capped but unsanitized raw text until the workflow rewrite, so the wording is contradictory.

More materially, truncating before sanitization can split a supported secret pattern. An incomplete private-key block containing `BEGIN ... PRIVATE KEY` without its truncated `END` marker is not matched by the current Python `PRIVATE_KEY_BLOCK` expression. Similar boundary cases can undermine pattern-based redaction. A sentinel test wholly inside the first 512 characters does not prove the boundary safe.

The current cleanup workflow also runs under `set +e`. Its in-place sanitizer command is not checked before artifact upload. If that rewrite fails, the original AppHost log remains present and can still be uploaded. The design's statement that the file “must pass through” the sanitizer needs an enforced failure/exclusion contract.

Required final clarification:

1. State that capped unsanitized excerpts may exist only in a redirected staging log and are never emitted to the Actions console.
2. Either sanitize the complete field before capping in process, or extend the workflow sanitizer/structured-record handling so cap-boundary fragments of every supported secret form are redacted. Add sentinels crossing the 512-character boundary.
3. Gate artifact upload on successful sanitization of every file that can contain probe excerpts. On sanitizer failure, remove or exclude the affected file and record a fixed safe `diagnostic_sanitization_failed` status; never upload the unsanitized fallback.
4. Reword D2's final-summary claim: the outer 300-second failure artifact relies on the latest progress record emitted at most two seconds earlier. The unconditional final summary applies only when the wrapper reaches its 360-second deadline or is given an explicit pre-capture flush path.

Revised-design verdict: **fail** pending the remaining D1 data-boundary correction. D2's wording correction is required for evidence accuracy but does not require a different runtime mechanism; D3 is approved.

### Complete-file final re-review

The current complete design closes D1 and keeps D3 closed.

#### D1 final disposition — closed

- The wrapper now sanitizes complete stdout/stderr strings in process before the 512-character cap.
- C# and Python implementations share one checked-in input/expected-output fixture, avoiding divergent undocumented rules.
- Only sanitized capped JSONL is emitted; raw values remain transient in `ProcessResult` and are not retained in diagnostic state.
- Workflow sanitization remains defense in depth, and sanitizer failure must exclude the affected file from upload with a fixed safe status.
- These requirements resolve the cap-before-sanitize secret-fragment issue and the error-tolerant upload issue from the prior review.

#### D3 final disposition — closed

Exact-line marker framing, full post-marker coordinator evaluation before excerpt cap, and the required negative/beyond-cap tests remain sufficient.

#### D2 final disposition — remains open on the named cancellation-summary path

The revised emission state/rate semantics are sound, and latest progress emitted within two seconds is sufficient to classify the outer failure. The named artifact path and shutdown ordering are not consistent with the current runtime:

- `artifacts/aspire-full-stack/apphost.stdout-stderr.log` captures the parent AppHost process. The wrapper is a DCP-managed child executable; its stderr is not automatically the parent AppHost stderr. The prior artifact confirms the named AppHost file contains host logs, while DCP resource logs are captured separately.
- On failure, workflow startup diagnostics capture DCP logs before leaving the readiness step.
- The always-cleanup step captures DCP logs again at lines 597-598 before it sends INT to AppHost at lines 600-623.
- A wrapper cancellation summary is emitted only during that later shutdown. It therefore misses both DCP captures and has no defined route into the named parent AppHost log before that log is sanitized and uploaded.

Required resolution, choose one:

1. **Progress-record route:** name `dcp-startup-failure/dcp-log-tails.txt` as the outer-gate evidence path, rely on the latest progress record emitted no more than two seconds earlier, and remove the claim that the cancellation summary appears in the outer artifact. Keep final summaries for inner deadline and wrapper-local lifecycle tests.
2. **Dedicated-file route:** pass an owner/run-scoped diagnostics file path under `artifacts/aspire-full-stack` through wrapper launch options and append sanitized JSONL directly. Sanitize it again after AppHost shutdown, exclude it on sanitizer failure, and upload it. Define path uniqueness, file creation mode, append synchronization, and cleanup. This is broader than the progress route.
3. A third valid option is a post-shutdown DCP capture, but only if evidence proves DCP log files survive AppHost shutdown long enough for capture; current ordering does not establish that.

Final design verdict: **fail**, solely on D2's cancellation-summary path/order. D1 and D3 are approved. The smallest correction is option 1 because the outer artifact needs classification rather than an exact cancellation-time attempt total.

### D2 option-A timing/path review

The amended path and cleanup ordering are now correct:

- Outer readiness failure calls `collect_startup_diagnostics` before the always-cleanup step stops AppHost.
- `capture_aspire_dcp_logs.py` produces `artifacts/aspire-full-stack/dcp-startup-failure/dcp-log-tails.txt` while wrapper/DCP logs still exist.
- The design no longer claims a cancellation summary is visible in that outer artifact.
- Final summaries are limited to the inner wrapper deadline or non-canceling wrapper exit, consistent with current ordering.

One timing assertion remains inaccurate. Progress is emitted only after a completed attempt. A single bounded attempt can occupy ten seconds before TERM plus two seconds before KILL, with Docker CLI completion overhead; the loop can also spend its one-second delay before the next attempt. If the outer 300-second deadline occurs during an attempt, the latest completed-attempt progress record can be materially older than two seconds. The two-second rule limits emission after completed fast attempts; it cannot provide a two-second wall-clock freshness guarantee while a probe is running.

Required final wording/test correction:

- Replace “progress record no older than two seconds before timeout” with “latest completed-attempt progress record captured before timeout.”
- If an age bound is required, derive it from the maximum probe duration, loop delay, and bounded Docker completion allowance; otherwise make no strict age claim.
- Keep the deterministic test on emission eligibility: unchanged completed outcomes produce a record whenever at least two monotonic seconds have elapsed since the previous record.

D2 disposition: **open only on the two-second freshness claim**. The selected DCP artifact path and pre-shutdown capture ordering are approved. After this wording/assertion correction, D2 closes and the full diagnostics design passes.

### D2 final confirmation

The final amended timing/path contract is sound:

- Progress is generated only from completed attempts and emitted on classification change or when at least two monotonic seconds have elapsed since the previous progress event.
- The design explicitly makes no wall-clock freshness promise while a bounded probe remains in flight.
- The outer 300-second artifact uses the latest completed-attempt record already present in the pre-shutdown sanitized DCP capture and does not fabricate an exit code for an incomplete attempt.
- Cancellation-summary visibility is no longer claimed. The unconditional summary is limited to the inner 360-second deadline or non-canceling wrapper exit.
- Tests now distinguish completed-attempt accounting from an in-flight probe spanning the outer deadline.

D2 disposition: **closed**.

Final diagnostics design verdict: **pass**. D1, D2, and D3 are closed. Implementation may proceed subject to the documented focused tests, normal implementation review, exact-head hosted artifact inspection, and preserved readiness/ownership/cleanup contracts.

---

## Normal implementation review - Crane probe diagnostics worktree delta

### Review identity

- Review mode: normal implementation review, not independent final review.
- Baseline: `2094d035dabd11c58ec2175af8cc3a0548d86cad`.
- Reviewed implementation identity: unstaged/untracked worktree delta over the baseline; `HEAD` still equals the baseline, so there is no implementation commit SHA yet.
- Reviewed areas: C# probe/diagnostic path, marker and post-marker graph evaluation, sanitizer parity fixture, lifecycle tests, DCP capture, workflow sanitization and upload gate, and cleanup ordering.

### Severity-ranked findings

#### N5 - medium, blocking - diagnostic JSONL has no implemented or proven route to the named DCP artifact

`DockerContainerWrapper.RunAsync` supplies `Console.Error.WriteLine` as the production diagnostic writer (`DockerContainerWrapper.cs:35`) and emits progress/final records only through that writer (`:143`, `:414`). The outer-timeout contract, however, names `artifacts/aspire-full-stack/dcp-startup-failure/dcp-log-tails.txt`. That file is produced by `capture_aspire_dcp_logs.py`, which scans `/tmp/aspire-dcp*` for eligible `.log` files. Nothing in this delta redirects the DCP-managed wrapper child's stderr to one of those files or copies resource console output into the capture roots.

The prior hosted artifact for run `37205944801` is evidence against assuming that route. Its DCP manifest contains only DCP `resource-executable-*.log` and `resource-service-*.log` files. The executable logs contain reconciliation/start metadata, while the wrapper's existing forwarded container stdout/stderr is absent. The parent `apphost.stdout-stderr.log` likewise contains AppHost host logs but no wrapper/container console output. Therefore a progress record can be generated correctly and still be absent from every uploaded artifact at the 300-second gate.

The new tests do not close this gap. The lifecycle tests replace the production writer with `diagnostics.Add`, and the Python artifact test manually creates a diagnostic-shaped `dcp-log-tails.txt`; neither composes the production stderr transport with `capture_aspire_dcp_logs.py`.

Required action: return this transport choice to parent design review. Add an artifact-visible transport for already-sanitized JSONL, or prove and capture the actual Aspire resource-console location. Add a composition test that emits through the production-equivalent wrapper path, performs the pre-shutdown capture, and finds the progress record in the named artifact with its sentinel redacted. Do not claim outer-timeout classification until hosted evidence confirms this path.

#### N6 - low, nonblocking - C# secret-field regex is not exact Python parity outside the shared fixtures

`CraneProbeDiagnostics.SecretField` applies `RegexOptions.Singleline`; Python `SECRET_FIELD` uses `(?ix)` without DOTALL. This changes the `\\.` quoted-value branch for a backslash followed by a newline. The five shared fixtures do not exercise that semantic difference, so passing them proves the listed cases but not exact rule parity. The C# behavior is more aggressive for that edge case, so this does not create a known diagnostic leak, but it contradicts the stated parity contract and makes future drift harder to detect.

Recommended action: remove `Singleline` from the C# secret-field regex (the private-key-block regex already has its own Singleline option), or explicitly define the intentional difference and add a shared multiline/escaped-value fixture.

### Checked with no additional finding

- Probe argv preserves `timeout --signal=TERM --kill-after=2s 10s` around `bash -lc`, and `exec ros2 node list` preserves process-group timeout intent.
- Readiness remains exit code zero plus exact coordinator line in the full output segment after an exact setup-marker line. Pre-marker coordinator text, substring markers, and coordinator text beyond the diagnostic excerpt cap are covered.
- Complete stdout/stderr strings are sanitized before the 512-character excerpts are retained. Structured records use `System.Text.Json`, so embedded controls cannot create extra JSONL records.
- Every completed attempt updates count/latest state. Progress emits on classification change or after the two-second completed-attempt interval. Cancellation suppresses the final summary; inner timeout and non-canceling exit emit it before scoped cleanup.
- The cleanup step runs the whole artifact-tree sanitizer and sets `sanitization_success`; upload is conditioned on that output being exactly `true`. A sanitizer exception makes the script return nonzero and the upload condition false.
- Existing ports, team/color, Game Controller producer, owner/stack labels, container naming/network checks, and scoped cleanup were not changed by this diagnostic delta.

### Validation

- `python -m unittest discover -s scripts/tests -p 'test_*.py' -v`: 16/16 passed.
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~DockerContainerWrapperLifecycleTests`: 19/19 passed on Windows. The Linux-only lifecycle bodies return early on this host, so subprocess, real stderr transport, and owned-container cleanup behavior remain unexecuted locally.
- `git diff --check 2094d035dabd11c58ec2175af8cc3a0548d86cad`: passed; only checkout line-ending warnings were printed.
- Docker/full-stack hosted validation was not available for this uncommitted worktree delta.

### Verdict

**fail** for the current implementation delta because N5 prevents the requested diagnostic from being reliably present in the named outer-timeout artifact. Sanitizer fixture coverage, marker semantics, retry/readiness behavior, final-summary cancellation semantics, and fail-closed upload gating otherwise match the approved design. N6 is a small parity cleanup and is not the reason for failure.
