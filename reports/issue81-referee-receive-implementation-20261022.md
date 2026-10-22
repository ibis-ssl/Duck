# Sub-agent実行レポート

## タスク

I81-03A：RefereeDriver自身の単一常駐11003受信と正常decode診断。最初に先行testとコンパイル可能な最小scaffoldだけを作り、実行時のfunctional Redを記録してから実装する。

## sub-agentを使う理由

既存実装担当が同じ契約・テストでRed→実装→Greenを継続し、親は台帳・CI・レビューを管理する。

## 対象範囲

Issue81/PR84、branch task/issue81-aspire-desktop-bridge、準備baseline234067d3cc3027a20f3794a7ce8fea75f26220ca。対象は03Aのみ。単一受信loop、parse前の単調stamp/epoch/明示owner、同counterも正常件数へ計上、change subscriberのみ重複抑制、未知/破損/欠落/取消/再起動のfail-closed。既存public APIと固定67件を保持。

## 対象外

Red観測前の対象behavior実装、03B API状態reader/照合・03C Program/HTTP・AppHost配線、外部repo変更、依存導入/バージョン変更/環境/auth/network設定変更、実Docker/外部service/実機起動、merge/Issue close。compile-errorはfunctional Redに数えない。

## Dispatch profile

Parent-owned. task_kind=tdd_test_authoring_then_implementation; work_class=judgment_heavy; uncertainty=medium; change_radius=cross_module; criticality=high; repetition=single; decomposability=sequential_dependencies; decomposition_policy=forbidden; context_need=bounded_history. existing_executor=/root/multicast_investigation_fresh; original actual requested=gpt-6.1-sol/high; applied=null; application_status=reused_existing_agent_profile; profile_observability=final_profile_hidden; original_fork=none; normal_persistence. TDD governing source=Tracker/Design/Testing/tracker-comparison-debug-design.md:260. formal verification_capability=remote_ci_only for real parser/host; local focused evidence kept separate. Parent owns commit/push/exact-HEAD CI/normal review; independent final remains Issue81 overall lifecycle.

## 実行コマンド

- 指定6Skillの実ファイルを再読し、既読手順を継続適用。work-context-managerでHEAD/所有差分/03A限定を確認、implementation-executor/workerはtest/scaffold準備のみ、tdd-executorは親の比較設計260行の適用判断に従いfunctional Red待ち、report-output-manager/writerで本報告を保存。
- PowerShell login=false / require_escalated → wsl.exe -d Ubuntu → Python/git/dotnet。cwd=/home/donabe/Duck-issue81-aspire-desktop-bridge、branch=task/issue81-aspire-desktop-bridge、baselineHEAD=234067d3cc3027a20f3794a7ce8fea75f26220ca。親のtasks/phases/scope report差分を保存。
- 初回保存scriptは新source/testの2file保存後、csproj参照の文字列一致assertでexit1。csproj未変更を確認し、実際の既存行から参照2行を追記して解消。2fileの部分成果は保持し、重複fileを作成していない。
- 既存SDK Roslyn syntax parserで新2fileを解析、exit0。csprojのXML parse成功。これは構文検査でありsemantic compile/test成功ではない。
- `/usr/lib/dotnet/dotnet build Testing/Duck.Testing.RefereeDriver/Duck.Testing.RefereeDriver.csproj --no-restore -m:1 /nr:false -p:EnableSourceControlManagerQueries=false` → dotnet exit1。既存RefereeDriver.csでCS0246×2、新scaffoldで同じ不足Referee型のCS0400。依存projectのSslProto/Diagnostics build出力はあるが、proto生成元がないためDriver compile未完了。
- 証拠dir `/tmp/issue81-referee-receive-red-preparation-20261022/` にsyntax/local-buildの実引数/出力/exit、expectations.json、source-manifest.json、変更前projectを保持。新testは未実行、TRXなし、functional Red未観測。build失敗をRedへ転記しない。

### 先行testの同SHA CI収集（実装前）

- 対象HEAD=8b9a1cf52a6fcf99b3196a81f638264337120143、PR84。開始時worktree clean。既存Skills/remote_ci_onlyを継続し収集だけ実施。
- 既存Windows ghからrun list --commit <同SHA> --event pull_request（exit0）でCI39784129851を同定し、run watch 39784129851 --exit-status --interval 5で終端failureを回収（watch exit1）。view --json headSha,event,status,conclusion,jobs,urlはexit0、head/event一致を確認。
- run view --job119842678541 --log（exit0）、actions/runs/39784129851/artifactsのGET（exit0）、actions/artifacts/12427094364/zipのGET（exit0）。artifact name=dotnet-test-failure-39784129851-1。正確な引数/exitは `/tmp/issue81-referee-receive-red-ci-20261022/commands-summary.json`、各stdout/stderr/exitは同dir。
- CI実行コマンドは既存workflowのdotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --logger trx --results-directory <results_dir> --diag <vstest-log> --blame -m:1 /nr:false -bl:<binlog>。実際のlogger/パス引数をci-test-command.txt、job全logをtest-log.stdout.log、artifact由来stdout/stderr/exitを同dirに保存。dotnet-test.exit-code=1。
- ZIPのSHA256はAPI digestと一致。TRXとsource.tar.gz内の対象3sourceを読取り、HEAD source-manifestとのhash一致を確認。archiveを製品/依存配置へ展開せず、照合copyは/tmp証拠dirだけへ保存。

### 最初のGreen候補（親のRed採用後）

- 親tdd-executorがCI39784129851の同SHA/実行時件数assert/source照合を有効Redとして採用した後、同じ担当/既存Skillsで最初の最小実装だけを行った。baselineHEAD=8b9a1cf52a6fcf99b3196a81f638264337120143。
- 新sourceのRoslyn構文parseはexit0。`/usr/lib/dotnet/dotnet build Testing/Duck.Testing.RefereeDriver/Duck.Testing.RefereeDriver.csproj --no-restore -m:1 /nr:false -p:EnableSourceControlManagerQueries=false` はexit1。既存RefereeDriver.csのCS0246×2、新sourceも不足global Referee型のCS0400×2。ローカルGreenに数えない。
- 実引数/出力/exit/source before-after/diffを `/tmp/issue81-referee-receive-first-green-candidate-20261022/` へ保存。testは未実行、CI操作/待機は今回行っていない。

### 最初のGreen候補の同SHA CI収集

- 親がpushしたHEAD a38422004406edba7931e023fb17b6847d61d9c2、開始時worktree clean。同SHA/event=pull_requestをrun listで確認（exit0）。CI39785567950のrun watch --exit-status --interval5はexit0、completed/successで終端。
- run view --json、test job119847724417の--log、actions/runs/39785567950/artifactsのGETは全exit0。sourceのHEAD照合、固定source hash、Red→Green間のTracker.Tests差分なしをreadbackした。
- 全引数/exitは `/tmp/issue81-referee-receive-first-green-ci-20261022/commands-summary.json`、各stdout/stderr/raw job log、metadata、source/固定manifest検査を同dirへ保存。今回はdotnet testをローカル実行せず、CI結果収集だけを行った。

### 次の03Aテスト準備（最初のGreen後）

次の03A先行テスト準備（不正入力）: 既存SDK `dotnet /tmp/issue81-independent-spec-tests-prep-20261022/Declarations.dll` へ evidence dirのsyntax-input.json / syntax-result.jsonを渡しexit0。完全な引数はsyntax-command.jsonへ保存。これは構文抽出でありsemantic compileではない。`git diff --check` exit0。未追跡testの `git diff --no-index --check -- /dev/null Tracker/Tracker.Tests/RefereeReceiveFailureTests.cs` はexit1・stdout/stderr空（/dev/nullとの差分あり、空白指摘なし）。保存scriptはこの1を0と仮定したassertで一度exit1となり、検査結果を正確に区別して保存を完了した。新test build/test/CIは未実行。

### 次の4ケースの同SHA CI収集（behavior実装前）

- HEAD `8dfa1e03a12f2a3eb6e23bdf8dfe44423a06dbef`、PR84、開始時worktree clean。既存6Skillとremote_ci_onlyを継続し、CI収集だけ実施。Windows ghの `run list --commit <同SHA> --event pull_request` exit0でCI39787367706を同定。`run watch 39787367706 --repo ibis-ssl/Duck --exit-status --interval 5` は終端failure、exit1。
- `run view --json headSha,event,status,conclusion,jobs,url`、test job119854012067の`--log`、`api repos/ibis-ssl/Duck/actions/runs/39787367706/artifacts`、artifact12429468437のzip GETは各exit0。正確な全引数/exitは証拠dirのcommands-summary.json、各stdout/stderr/exitも保存。
- CIの標準`dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj` は実Referee型を含めcompile後に実行され、dotnet-test.exit-code=1。logger/TRX/diag/blame/binlog/結果dirの実コマンドをci-test-command.txtへ保存。compile-errorをRedにしたものではない。

### 4ケースの最小Green候補（親のRed採用後）

親がexact8dfa1e03 CI39787367706の4runtime failureを有効Redに採用した後、同じ担当/既存Skillsで最小実装。`dotnet /tmp/issue81-independent-spec-tests-prep-20261022/Declarations.dll <syntax-input.json> <syntax-result.json>` の構文抽出exit0。`/usr/lib/dotnet/dotnet build Testing/Duck.Testing.RefereeDriver/Duck.Testing.RefereeDriver.csproj --no-restore -m:1 /nr:false -p:EnableSourceControlManagerQueries=false` はexit1、既存生成Referee型不足のCS0246×2/CS0400×2。`git diff --check` exit0。実引数/出力/exitは `/tmp/issue81-referee-receive-failure-green-candidate-20261022/` に保存。今回test/CIは実行していない。

### 不正入力4ケースのGreen CI収集

- 対象HEAD `9791c234009091940e9dd0c95f905c063e7385e6`、開始時worktree clean。既存6Skill/remote_ci_onlyを継続し、CI収集だけ実施。`run list --commit <同SHA> --event pull_request` exit0。`run watch 39788803859 --repo ibis-ssl/Duck --exit-status --interval 5` は終端success、exit0。
- `run view --json headSha,event,status,conclusion,jobs,url`、test job119858998456の`--log`、`api repos/ibis-ssl/Duck/actions/runs/39788803859/artifacts` は全exit0。正確な全引数/exitは証拠dirのcommands-summary.json、raw stdout/stderr/exitも保存。
- CIの標準dotnet testの実コマンドをci-test-command.txtへ保存。test step successとworkflowの終了値伝播は確認したが、dotnet生exitcode artifactは成功runでは取得できていない。watch exit0と同一証拠として混同しない。

### 取消の先行テスト準備（4ケースGreen後）

親の継続指示で取消1件の先行testだけを追加。expectations.jsonをsource作成前に保存。既存SDK宣言抽出の正確な引数はsyntax.command.json、exit0（semantic compileではない）。新build/test/CIは未実行、functional Red pending。`git diff --check` exit0。未追跡testの`git diff --no-index --check -- /dev/null Tracker/Tracker.Tests/RefereeReceiveCancellationTests.cs` はexit1・stdout/stderr空（新fileとの差分、空白指摘なし）。

## 対象ファイル

| 変更path | 準備内容 |
| --- | --- |
| Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs | 内部service/datagram transport境界だけ。InputId=referee、RunAsyncはTask.CompletedTask、command待機はNotImplemented、Disposeは完了値。実loop/parse/count/epoch処理なし。 |
| Tracker/Tracker.Tests/RefereeReceiveDiagnosticsTests.cs | positive1件。fake raw transport＋実Referee.Parser＋手動時計、正常同counter2packet。socket/外部service/hostを起動しないfixture。 |
| Testing/Duck.Testing.RefereeDriver/Duck.Testing.RefereeDriver.csproj | 既存Diagnostics ProjectReferenceとTracker.Tests InternalsVisibleToだけ追加。既存TFM/SDK/SslProto参照/public APIは維持。NuGet変更なし。 |
| reports/issue81-referee-receive-implementation-20261022.md | 親prefix/Dispatchを保持し子5欄へ準備記録。 |

- 既存RefereeDriver.cs / RefereeDriverNetworkAdapters.cs / 旧test / 固定v1v2の67test / 共通Diagnostics本体は未変更。API reader、Program、HTTP、AppHost、Dockerfile/bridge配線、外部repoへ未着手。
- 新規説明/XMLコメントは日本語。内部seamはpayload/観測endpoint/同じ受信時点のReceiveAttemptを渡し、復号前の時計を単一pumpで使うための宣言。実transport/socketの実装はまだない。

### CI収集で追加した証拠

- repository writeは本reportの子5欄への追記だけ。service/test/csprojの3sourceはCI対象HEADから未変更、Run no-op / Wait NotImplementedのまま。
- /tmp/issue81-referee-receive-red-ci-20261022/: CI JSON、job log、failure-artifact.zip、dotnet-test.trx/stdout/stderr/exit、artifact一覧、source-from-ciの3照合copy、ci-evidence.json、source-archive-check.json、コマンドと終了値。
- CI archiveと一致したsource SHA256: service 30cbef39a78f5befd3b466e5d6bc5123063d2e5ec07bec339b4fb3275687d019、test ffcf0eb460ad36fc0b12eef9d4e50305b1fd1b2760e237dd814cbaf9ebea53e1、csproj ee6ba0e4d17bc54da0db0e76399f43a365651e030050a63ac5720ddad7d1122c。

### 最初のGreen候補の差分

- 製品変更はTesting/Duck.Testing.RefereeDriver/RefereeReceiveService.csの1fileだけ。差分量はgit diff --numstatで `26	3	Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs`。
- receiver/diagnostics/decodeを保持し、1つのRun内で1つのepochを開始して逐次Receive→parse→成功記録を行う。transportへ渡すcallbackで既存BeginReceiveをparse前に呼ぶ。sourceKey/RemoteEndpointは観測endpointを使い、owner IDをproducer UUIDへ流用しない。
- 同command counterのpacketを除外しない。既存WaitForChangeAsyncはNotImplemented、DisposeAsyncは前scaffoldのまま。public API/型/既存test/csproj/共通Diagnosticsを変更していない。新規説明コメントは日本語。

### Green CI収集の対象・保存

- source/test/csprojはa384220のgit showとローカルbyteが一致。source SHA256=3dff843cced87b189566feeb9712430f1db63372eae1b7d6652c65c9d3ac2be2、test=ffcf0eb460ad36fc0b12eef9d4e50305b1fd1b2760e237dd814cbaf9ebea53e1、csproj=ee6ba0e4d17bc54da0db0e76399f43a365651e030050a63ac5720ddad7d1122c。
- 固定v1/v2 manifestと各sourceのhash不変を再照合。Tracker.TestsはRedの8b9a1cfからGreen候補a384220まで差分なし。既存67件や03A先行testを編集していない。
- 今回repository書込みは本report子5欄だけ。CI metadata/raw log、head-source-manifest.json、tests-source-delta.txt、ci-evidence.jsonは/tmp証拠dirへ保存。

### 次の03Aテスト準備（最初のGreen後）

次の03A候補は新規 `Tracker/Tracker.Tests/RefereeReceiveFailureTests.cs` のみ。SHA256 `e765209ae1d0ab9c72c75baa64d2d2e5216a28e0caaae8dca84adc247b7fe116`。behavior/scaffold追加なし。製品source・既存positive・csprojはHEAD a384220とのbyte一致、固定v1の11file/v2の9fileとrepo内対応sourceも不変。親tasks/phases差分は保持。証拠dir: `/tmp/issue81-referee-receive-failure-red-preparation-20261022/`、source-manifest.json / expectations.json / 構文抽出log / check-commands.json / evidence-files-manifest.jsonを保存。

### 4ケースのCI source・成果物照合

- 新test SHA256 `e765209ae1d0ab9c72c75baa64d2d2e5216a28e0caaae8dca84adc247b7fe116`、製品source `3dff843cced87b189566feeb9712430f1db63372eae1b7d6652c65c9d3ac2be2`、既存positive `ffcf0eb460ad36fc0b12eef9d4e50305b1fd1b2760e237dd814cbaf9ebea53e1`、csproj `ee6ba0e4d17bc54da0db0e76399f43a365651e030050a63ac5720ddad7d1122c`。
- artifact内source.tar.gzから対象4fileと固定独立source7fileを読み、全11fileをHEAD/currentのbyte/hashへ照合。source-archive-check.jsonと/tmp内ci-sourceへ保持。既存固定manifest v1の11file/v2の9fileも元hash不変。製品・test・期待値への書込みなし。
- 証拠dir `/tmp/issue81-referee-receive-failure-red-ci-20261022/`。ZIP digest `89acf6673523b083a19618d16ceb099dfb993343d9b7bd0d4ec622c676bcfb84` はAPI digest一致。成果物はdiagnostics/binlog/stdout/stderr/exit/TRX/source archiveを含む。

### 4ケースの最小Green候補（親のRed採用後）

製品変更は `Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs` のみ（16 / 2 / Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs）。SHA256 `7b72034fedb7d3a545d793827c240f76de91724a24ad2c49ac6913434053044d`。新4case test `e765209ae1d0ab9c72c75baa64d2d2e5216a28e0caaae8dca84adc247b7fe116`、既存positive `ffcf0eb460ad36fc0b12eef9d4e50305b1fd1b2760e237dd814cbaf9ebea53e1`、csprojはHEAD byte不変。固定v1の11file/v2の9fileとrepo対応sourceも再照合し不変。報告は既存子5欄だけへ追記、親prefix/Dispatch/既存履歴を保持。

### 不正入力4ケースのGreen CI収集

- 今回製品/test編集なし、報告は既存子5欄のみ追記。source `7b72034fedb7d3a545d793827c240f76de91724a24ad2c49ac6913434053044d`、4case test `e765209ae1d0ab9c72c75baa64d2d2e5216a28e0caaae8dca84adc247b7fe116`、positive `ffcf0eb460ad36fc0b12eef9d4e50305b1fd1b2760e237dd814cbaf9ebea53e1`、csprojは候補/current/HEADで一致。Red8dfa1e03→当HEADのTracker.Tests差分は空。
- 固定v1の11file/v2の9fileとrepo対応sourceをhash再照合し不変。証拠dir `/tmp/issue81-referee-receive-failure-green-ci-20261022/` のhead-source-manifest.jsonに保持。成功artifactなしのため、今回CI実行環境のsource archiveは取得していない。

### 取消の先行テスト準備（4ケースGreen後）

新規`Tracker/Tracker.Tests/RefereeReceiveCancellationTests.cs`、1 Factのみ。SHA256 `76b9cb78854541fc613463c3d6499ef74c84f59b6596f84ffc1c3ea3af51366f`。製品behavior/scaffold追加なし、既存positive/不正4case/csprojと固定v1の11file/v2の9file・repo対応sourceはbyte/hash不変。証拠dir `/tmp/issue81-referee-receive-cancel-red-preparation-20261022/` にexpectations.json / source-manifest.json / remaining-03A.json / 構文抽出log / check-commands.json / evidence-files-manifest.jsonを保存。親tasks/phasesの差分は保持。

## 指摘事項

### TDD根拠と今回の期待値固定

- 比較設計tracker-comparison-debug-design.md:260と、親が採用したtasks-status I81-03Aに従う。主設計27/158/183、比較設計40/42/256が、Driver自身の復号・ID/世代・有限な新しさと外部観測の区別の根拠。過去I81-02のRed不足はそのまま保持し、今回の再承認待ちとはしない。
- 最初のpositive test名: `RepeatedCommandCounterCountsEachDecodedPacketAndPreservesReceiveClock`。command counter=7の正常HALT2packetを同じendpointから供給。1回目受信mono0→parse2秒、2回目受信mono3秒→parse2秒として、件数2/失敗0/Succeeded、LastReceivedAt=初期UTC+3秒、age=2秒、明示owner、観測endpoint、SourceUuid=null、IsReady=trueをassert。診断取得時刻を受信時刻へ代用する実装は通らない。
- third Receiveの待機開始を先の2packet処理完了のfixture境界にする。no-op scaffoldが先に完了する場合も待ち続けずsnapshotを調べるため、最初の件数assertはExpected2/Actual0の実行時失敗を示す設計。timeoutだけを意図したRedにしていない。取消はfinallyで回収し、キャンセル例外を後始末として受ける。
- 今回作成した先行testは1件だけ。happy/failure/boundary/regressionの残る対象をexpectations.jsonへ固定し、小さい機能ごとにRed→最小実装→Greenを積む。重複change購読、未知/破損/欠落、ID未供給、期限境界/再poll非延命、旧callback/再起動/取消、受信の奪い合い防止は03Aの通常review前に追加検証が必要。今回すべて実装済み/検証済みとはしない。

### scaffoldの上限

- constructorのnull guardと既存型の宣言だけで、受信処理・parser呼出し・正常/失敗件数更新・BeginEpoch・change通知を実装していない。既存ServiceReceiveDiagnosticsはtest側から注入するが、scaffoldは値を更新しない。
- 03B/CのGC状態/HTTP/設定/実行入口へ進まない。既存public constructor/default/signatureや固定67件の期待値を変更していない。

### 準備時の見込みと今回の実測

- 準備時のExpected2/Actual0は未実行の見込みだった。今回のTRX実測は `Assert.Equal() Failure: Values differ / Expected: 2 / Actual: 0`、RefereeReceiveDiagnosticsTests.cs:38。Assert.Single(empty)失敗ではなく、その次の正常件数assertに到達した実行時失敗をそのまま記録する。
- 対象名: Tracker.Tests.RefereeReceiveDiagnosticsTests.RepeatedCommandCounterCountsEachDecodedPacketAndPreservesReceiveClock。duration=00:00:00.0127991。timeout/型解決/compile failureではない。
- CIでは実Referee型を含むproject/test assemblyがcompileされtest実行へ到達。no-op Runが受信/decoder delegateを呼ばないため、今回のRedは件数進捗の欠落を示す。後続の受信時刻/age/identity/ready assertは未到達で、それらの動作検証成功とはしない。
- 準備段階のローカルCS0246/CS0400は過去の能力不足記録として保持し、今回のCI実行時Redと混同しない。

### 今回実装した最小動作と未実装

- 有効Redが示した正常件数0に対して、同一受信pumpで実decoderを呼び、正常復号ごとにRecordSuccessへそのReceiveAttemptを渡す動作だけを追加した。既定decoderは既存Referee.Parserを使い、先行testの注入decoder/手動時計も同じ経路で呼ぶ。
- epoch開始は共通診断がReceiveAttemptを発行するために必要な初期化。再起動/取消の状態無効化、同時Runの排他、遅いcallbackへの独自対策を完成したものではない。
- 不明command/意味欠落の検査、decode失敗の記録と継続、subscriberの変更待機/重複抑制、停止/再起動の契約は未Redの後続機能として残す。外部へ公開するProgram/HTTPや実UDP transportはまだなく、03A全体readyは主張しない。

### 集計Greenと個別結果の区別

- test job logは `Passed! - Failed:0, Passed:616, Skipped:0, Total:616`。同一test sourceとRed時の616件構成を保った標準全体の集計Greenを確認した。
- 成功runのartifacts APIはtotal_count=0。既存workflowは失敗時だけdiagnostics/TRXをuploadするため、runner内にResults File: .../dotnet-test.trxというlogはあるが、その個別TRXは取得できていない。個別test名も成功logに出ていない。
- 最初の03A testと固定67件はsource不変・同じ標準target・616全成功という対応から集計に含まれるGreenとして扱う。新test名のPassed行や固定67件の個別TRX行を今回直接読戻したとは記録しない。
- dotnet stepはsuccessで既存workflowがtest終了値をそのままexitするため成功終了と整合。生dotnet-test.exit-code artifactは未取得であり、toolのwatch exit0とは証拠の種類を分ける。

### 次の03Aテスト準備（最初のGreen後）

次の先行期待値は採用scope report89行・tasks-status28行の03Aに対応。4ケースを独立fixtureで実行するTheory: malformed / unknown-command / missing-command / missing-counter。正常2packet後count2/error0/ready → 不正packet後pump継続/count2/error1/Failed/not-ready → 次の同counter正常packet後pump継続/count3/error1/Succeeded/ready。実Referee.Parserと隔離raw受信seamを用いる。受信call3/4/5のTask signalとrun終了を観測し、pump停止は明示assertで検出する（任意sleepなし）。取消は後始末であり取消契約の検証へ数えない。現実装のpump停止/意味不正packet計上は失敗候補だが、CI未実行なので実測assert位置/値は未確定。

### 4ケースの実行時failure（予定ではなくTRX実測）

| 入力 | 実測failure | 最初の位置 |
| --- | --- | --- |
| malformed | `Assert.False(run.IsCompleted)`、メッセージ「破損や意味の欠落があっても受信pumpを停止しない。」 | RefereeReceiveFailureTests.cs:39 |
| unknown-command | `Assert.Equal` 正常件数 Expected2 / Actual3 | 同file:41 |
| missing-command | `Assert.Equal` 正常件数 Expected2 / Actual3 | 同file:41 |
| missing-counter | `Assert.Equal` 正常件数 Expected2 / Actual3 | 同file:41 |

- 各caseは初期正常2packetのassertを通過してから失敗。破損入力はpump停止、意味不正3入力は正常件数への混入を示す実行時assertionである。型解決/未実行/timeoutの失敗ではない。
- この失敗で後続のerror count/status/not-ready/正常入力による復帰assertは未到達。今回Redをそれらの成功や03A全体完成へ置換しない。

### 4ケースの最小Green候補（親のRed採用後）

decodeだけを`Google.Protobuf.InvalidProtocolBufferException`で捕捉し、同じReceiveAttemptでRecordFailureを記録して同じpumpの次受信へ進む。復号できても`HasCommand`/`HasCommandCounter`が欠落、または`Enum.IsDefined(packet.Command)`が偽なら同じ失敗経路へ進める。正常packetは既存の受信stamp/endpointによるRecordSuccessのまま。共通診断の失敗無効化と次count増分による復帰を利用する。取消/OOM/想定外例外を広いcatchで正常扱いにせず伝播させる。変更関数XML説明は日本語。subscriber/restart/transport等の未Red契約を先回り実装していない。

### 不正入力4ケースのGreen CI収集

- logの直接証拠は標準620件の全成功。artifacts APIはtotal_count=0、成功testの個別名行も0。runner内TRX生成のResults File行はあるが、アップロードされた個別TRXは取得していない。これはworkflowの失敗時限定保存による限界であり、権限拒否ではない。
- 新4ケース/既存positive/固定67のGreenは、Red時に確認した620件構成・同じtest byte・同じ標準target・今回620全成功の対応から扱う。今回それぞれのPassed行を個別TRXで直接読戻したとはしない。固定test期待値/skip変更なし。

### 取消の先行テスト準備（4ケースGreen後）

最小まとまりはRun所有の終了時無効化1件に限定。完全owner、実Referee.Parser、同counter正常2packetでcount2/readyを先に確認し、第三受信待機のsignal後に取消してpump終端を待つ。時計は常に同じ値なので期限切れと区別し、終端後`IsReady=false`（42行）・count2維持・受信呼出し3を期待する。取消完了が例外か正常returnかは追加の仕様にしない。現在の旧ready残留はruntime Red候補であり、未実行の失敗を実測扱いにしない。同時Run/restart/遅延callbackを一度にfixtureへ詰め込まず、後続caseへ残す。

## 結果

- test/scaffold準備完了、functional Redはpending。現在のローカルcompile-errorは既存proto実体不足の証拠であり、Red/Greenの成功履歴ではない。標準real parser/hostのformal routeはremote_ci_only。
- source hashes: service `30cbef39a78f5befd3b466e5d6bc5123063d2e5ec07bec339b4fb3275687d019`、test `ffcf0eb460ad36fc0b12eef9d4e50305b1fd1b2760e237dd814cbaf9ebea53e1`、csproj `ee6ba0e4d17bc54da0db0e76399f43a365651e030050a63ac5720ddad7d1122c`。全てsource-manifest.jsonへbaselineHEADと一緒に保存。expectations.json SHA256 `4323ef347fb4a2ce075f6a5e0df8dbd04accad400efbc58803e0dff7bf9d8525`、source-manifest.json SHA256 `e044d8c58e1a834c06cad5fdf1d9c9c614c8c5cc1e60752e0f8dbd7771e14566`。
- 固定v1の11file/v2の9fileを元manifest hashで再照合し全て不変。新testは独立67件への書換えではなく03A先行回帰として追加する。
- 親への次手順: 非mainのtest/scaffold commit/push→同SHA pull_request CIでcompileと実parser test実行→上記新testの実行時assertion失敗・TRX/test名・source/hash/終了値を回収。本担当はその結果が返るまで本behavior実装を行わない。CIで別のcompile問題が出た場合もRedに数えずcompile scaffold/testだけを修正する。
- functional Redが得られた後だけ同じtestを保って最小実装し、同一source Greenへ進む。今回のtaskでcommit/push/CI照会・待機は実施しない。

### functional Redの実測結果（親確認待ち）

- `https://github.com/ibis-ssl/Duck/actions/runs/39784129851`。headSha=8b9a1cf52a6fcf99b3196a81f638264337120143 / event=pull_request / status=completed / conclusion=failure。test job119842678541、dotnet exit1。
- TRX=616実行/615成功/1失敗/0skip。唯一の失敗は上記新positiveの件数assert。残り615件は全Passed、固定独立namespace67件も全Passed。packet/fullstack jobはSkippedで実機証拠にしない。
- TRX: `/tmp/issue81-referee-receive-red-ci-20261022/dotnet-test.trx`、SHA256 `3f22663e4e9fd6caac65b1e61104c89b15f5b3ec364c116dacccc1095772499e`。
- artifact ZIP SHA256 `7f54e0ee66b7da2b2624d4b9ff9fca77eb106eab01a5b4cb33673a0e0aefab64`（API artifact digest一致）。CI/source/失敗対応表ci-evidence.json SHA256 `aa9ad6f8dd3bde565388126d555e2bcb1356c68ef792c4dde9939c29886113a5`。source-archive-check.jsonはCIに使われた3fileとHEADのbyte/hash一致を保持。
- 今回収集した実行時failureは03Aの最初のfunctional Redとして親へ返す。behavior実装は開始せず、親がこのRedを確認して同担当へ戻すまでno-op scaffoldを保つ。未充足の残03A機能や過去Red不足を解消済みとはしない。

- CI収集file全一覧: /tmp/issue81-referee-receive-red-ci-20261022/evidence-files-manifest.json、SHA256 372611b59c05d61943cec623b20369a55a1e88eeeecf6ff7250daea44ec8188c。全39fileの相対path/bytes/hashと生log/TRX一覧を保持。親のrepository保存時はgitignore対象を確認し必要なforce-addを行い、生log/TRXを省略しない。今回stage/commit/pushは行っていない。

### 最初のGreen候補の保存結果

- source SHA256 `3dff843cced87b189566feeb9712430f1db63372eae1b7d6652c65c9d3ac2be2`。同じtest SHA256 `ffcf0eb460ad36fc0b12eef9d4e50305b1fd1b2760e237dd814cbaf9ebea53e1`はCI Red時からbyte不変。期待値緩和/skip/既存615件の変更なし。
- source-results-manifest.json SHA256 `82844d8f2af325c430497846a8cc6f1586c7034fac9339a363fcfd62def8797f`。baseline/採用Red/差分量/実行結果/source hashを保持。Greenは未観測、親の非main commit/push後exact-SHA PR CIへ渡す候補である。
- Red実測の実行時assertとTRXは前節のまま保持する。今のローカルcompile-errorをRed/Greenへ置換しない。最初の同一testがGreenになった後に、03Aの次の小さい機能の先行Redを得る。

### 最初のRed→GreenのCI結果

- `https://github.com/ibis-ssl/Duck/actions/runs/39785567950`。headSha=a38422004406edba7931e023fb17b6847d61d9c2 / pull_request / completed / success。test job119847724417 success、標準616/616成功・0失敗・0skip。packet/fullstack jobsはSkipped。
- RedはCI39784129851 exact8b9a1cfの新test件数assert Expected2/Actual0、Greenは同じtest byteを持つexacta384220の標準集計成功。最初の正常packet/count/時刻の最小実装ステップの結果として親へ返し、個別TRX未取得の限界を上記に保持する。
- CI/source/結果対応表 `/tmp/issue81-referee-receive-first-green-ci-20261022/ci-evidence.json`、SHA256 `0dcd74a1f0c7858a1c2951fa210df690375a4d5b45dc47d90197bba8b0623564`。
- subscriber、未知/欠落/破損、取消/再起動/旧callback等の残03A機能へは今回は着手していない。03A全体完了・通常review合格・実機受入とはしない。

### 次の03Aテスト準備（最初のGreen後）

次の4ケースは実行前にexpectations.jsonへ固定済み。実行件数0、semantic compile未確認、functional Red pending。親のtest-only commit/push後exact-SHA PR CIで実assertion/TRXを回収し、有効Red採用後だけ同じtest byteで最小実装へ戻る。前節CI39785567950の616集計Greenと、今回の未実行4ケースは分離する。構文抽出成功・compile error・未実行をfunctional Redへ数えない。

### 次の03A functional Red収集結果（親採用待ち）

- `https://github.com/ibis-ssl/Duck/actions/runs/39787367706`、exactHEAD8dfa1e03a12f2a3eb6e23bdf8dfe44423a06dbef / pull_request / completed / failure。標準TRXは620実行・616成功・4失敗・0skip、error/timeout/abortedも0。
- 新4ケースだけが上表の実行時failure。既存positiveはTRXでPassed、他616件すべてPassed、固定独立67件も個別TRX行を照合し全Passed（fixed67-results.json）。packet/fullstack jobsはSkipped、実機証拠ではない。
- TRX `/tmp/issue81-referee-receive-failure-red-ci-20261022/dotnet-test.trx` SHA256 `f10ccf4263d2e1c3c24029deacab7a082aca915d2b7d5aed4c0a278f4e613964`。CI/source/各assertion対応表ci-evidence.json SHA256 `1afcbe5a31a5a5bf970faaff4d177fcce85a6f4b6238aff44db117425edbb799`。
- 親へ有効Red採用判断の証拠を返す段階であり、本behaviorは変更していない。同じtest byteを保持して、親の採用後だけ最小実装へ戻る。初回positiveのRed→Green履歴は保持。

### 4ケースの最小Green候補（親のRed採用後）

最小Green候補を保存。`source-results-manifest.json` SHA256 `79b84592223ccfa1e23568505560592e4e0ffb3462fd591684c5961d5e9f6360`、採用Red/source before-after/不変test/差分/実行結果を保持。候補のsemantic compileと実parser testはローカルproto不足で未確認、実行件数0、Green未観測。次は親の非main commit/push後exact-SHA PR CIで、固定4case/positive/固定67を保持して結果を回収する。今回build exit1をRed/Greenへ数えず、前節の実runtime Redを保持。

### 不正入力4ケースのGreen CI収集

- `https://github.com/ibis-ssl/Duck/actions/runs/39788803859`、exactHEAD9791c234009091940e9dd0c95f905c063e7385e6 / pull_request / completed / success。test job119858998456のlogは620/620成功・0失敗・0skip。packet jobはSkipped、実機/packet flow受入の成功ではない。
- RedはCI39787367706 exact8dfa1e03の4runtime failure（破損pump停止line39、他3caseの件数Expected2/Actual3 line41）。同じtest byteを保持した最小不正入力処理のGreenを今回の標準集計で確認した。
- `ci-evidence.json` SHA256 `b737328a3bbcda7e8fc782e41e79115a32a6c7192fa53e451e1787d6914d0f81`。次単位案は正常受信でreadyになった後の取消→pump終端後not-ready。手動時計を固定し、期限切れと取消の無効化を区別してassertする。先行caseを最小にし、必要なら開始前取消で受信/復号0という独立境界caseを続ける。期待/停止条件案はnext-unit-proposal.jsonへ保存。今回はtest/behaviorを作らず、親へ案だけ返す。

### 取消の先行テスト準備（4ケースGreen後）

取消先行testを保存・構文抽出exit0、実行件数0、semantic compile未確認。expectations SHA256 `da6ed1a5d235e4bb3385d5a2787759ebd244fd19608231a04bc8350d842acd46`、source-manifest SHA256 `e22ebbd36194f034bd143145c3de5618bf8af0b3e95859151d54084b0bc6655d`。親が非main commit/push→exact-SHA PR CIでcompileと実assertionを回収して有効Redを採用した後だけ、同一testで最小実装へ戻る。今回正常取消動作の実装を先行していない。前節620集計Greenの証拠は保持し、新しい未実行1件へ転記しない。

## リスク

- 新2fileは構文parseのみ成功し、実Referee型を含むsemantic compileはローカルで未完了。CIでのcompile/test実行までコンパイル成功やfunctional Redを断定しない。ローカル不足をsubmodule取得/依存追加/設定修復で埋めない。
- 03A全契約は未実装、今回のtest1件だけで重複購読/旧callback/再起動/取消等の完了を宣言しない。GC唯一11003 producer、手動画面操作、将来同一DCP bridge、外部G1/G3の保留条件を維持。
- 今回新behaviorのparse/UDP/HTTP/host/外部接続は実行していない。固定独立testの期待値緩和/skip化なし、自己レビュー合格判定なし。
- 親tracking/他reportの差分保持。新NuGet/版変更/外部依存取得・環境/auth/network設定・実Docker/外部service/実機起動・commit/push/GitHub操作/merge/closeなし。
- ローカルbuildを含む全process終端、exitcode回収済み。実行中process・保留approval・未回収結果なし。

### CI収集の終端

- 本behavior/03B/03Cへ進んでいない。テスト/期待値/skip、既存615件、固定67件、製品source/設定/依存/環境/auth/networkへ変更なし。新しいcompile scaffold問題は今回CIでは出ていない。
- sourcehash、失敗assertion、TRX、終了値を回収済み。各gh/read/download/watch processは全終端、保留approval/未回収結果/実行中processなし。commit/push/GitHub変更/再実行/外部service・実機起動なし。

### 最初のGreen候補の終端

- ローカルproto不足の制約がありsemantic compile/testは今回未完了。実parser型を持つCIで同一testのGreen確認が必要。構文parse成功をGreenへ昇格しない。
- 未Red機能の先回り実装なし。03B/C、外部/依存/設定、環境/auth/network、host/実機、commit/push/GitHub操作へ進んでいない。親の追跡/report差分は保持。
- 全process終端、buildのexit1と構文parseのexit0を回収済み。実行中process・未回収結果・保留approvalなし。

### Green収集の限界・終端

- 現runの個別TRX/成功test名行は未取得。集計616成功と固定source/以前のdiscoveryの対応を、個別TRXによる直接照合と区別した。成功artifactがないことはpermission denialではなくworkflowの保存条件による。
- 本taskで追加test/behavior、既存615件、固定67件、環境/外部/依存/設定を変更せず、CI再実行・commit/push/GitHub変更なし。全process終端、exitcode回収済み、保留approval/実行中process/未回収結果なし。

### 次の03Aテスト準備（最初のGreen後）

今回未対応の03A契約: owner欠落/短ID、freshness期限/再poll非延命、取消無効化、旧callback/再起動epoch、同時Run排他、subscriber重複抑制と診断件数の分離/受信奪い合い防止、実transport lifecycle。既存positiveの同counter件数/受信時計検証をsubscriber完成の証拠へ流用しない。03B/C、製品behavior、外部/依存/設定/環境/auth/network、実host/Docker/実機、commit/push/GitHub変更なし。新4ケースはCI待ち。全読取/構文抽出/保存/diffcheck process終端、未回収exit・保留approval・実行中processなし。

### 4ケースCI収集の終端

- 不正入力後の失敗診断/復帰を完成したとはしない。03Aのowner/freshness/epoch/cancel/single-run/subscriber等の残項目、03B/C、外部G1/G3、実機受入は保留のまま。test期待値緩和/skip化、behavior追加、自己レビュー合格判定なし。
- 全file一覧は`/tmp/issue81-referee-receive-failure-red-ci-20261022/evidence-files-manifest.json`（全57file、SHA256 `a884a9f9d7e8760b7a9b7a6af2019f815db8f7a50143aab1783a9708d2051c4f`）。生log/TRX/zip/source archiveを省略せず、親のrepository保存時はgitignore対象も必要に応じforce-addする。本担当のstage/commit/push/GitHub変更/再実行はなし。
- 全list/watch/view/log/artifact download/read/save process終端、exitcode回収済み。実行中process/session、未回収exit、保留approvalなし。製品/test/設定/依存/環境/auth/network/外部repoに変更なし、host/Docker/実機起動なし。

### 4ケースの最小Green候補（親のRed採用後）

4case候補以外の03A契約、03B/C、外部G1/G3、実機受入は未完了。個別testの期待値緩和/skip変更、依存/設定/環境/auth/network/外部repo変更、host/Docker/実機起動なし。commit/push/GitHub操作なし。全証拠一覧`evidence-files-manifest.json` SHA256 `c20fbdb8e57a3873adb508f61c74216936c25f99ecfd971b974ad29d87d70e5b`（17file）に生logを含む。全構文抽出/build/read/save process終端、exit回収済み、実行中process/session・保留approval・未回収結果なし。

### 不正入力4ケースのGreen CI収集

- 成功runの個別TRX未取得を保持。03Aの取消/epoch/restart/owner/freshness/single-run/subscriber/実transport等は未完了、03B/Cや全体受入へ広げない。新しいRedを未実行のまま断定せず、次behaviorは親が先行testの実runtime Redを採用した後に進める。
- 全file一覧 `/tmp/issue81-referee-receive-failure-green-ci-20261022/evidence-files-manifest.json`（26file、SHA256 `faff7c6e0836ac25f921d9da957f63d173281ef39ae8e09264895a716ac35bc0`）。生logを含め保存し、親のrepository保存時はgitignore対象の保存漏れを防ぐ。
- 全list/watch/view/log/read/save process終端、exit回収済み、実行中process/session・未回収exit・保留approvalなし。製品/test/依存/設定/環境/auth/network/外部repo変更、host/Docker/実機起動、commit/push/GitHub変更/再実行なし。

### 取消の先行テスト準備（4ケースGreen後）

03A完了前の残件をremaining-03A.jsonに仕様根拠付きで列挙。取消終端、単一Run排他、再開/旧epoch/遅いcallback拒否、subscriber重複抑制と毎packet診断の分離、owner欠落not-ready/freshness非延命、実11003 UDP transport/options/lifecycleが必要。fake受信seamだけを実UDP完了にしない。03CのProgram/DI/HTTPや後続DCP配置・Issue82実機受入とは分ける。既存testの期待値緩和/skip化なし、03B/C・外部/依存/設定・環境/auth/network変更・host/Docker/実機起動・commit/push/GitHub変更なし。全process終端、未回収exit/保留approvalなし。

## 親による公開準備記録

先行テストと最小scaffoldの証拠を reports/evidence/issue81-referee-receive-red-preparation-20261022/ へ保存。functional Redは未観測で、次の同一SHA CIで実行結果を確認する。現段階で本動作は実装していない。
