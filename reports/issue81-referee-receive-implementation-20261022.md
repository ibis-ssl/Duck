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

### 取消testの同SHA runtime Red収集

HEAD `be8bd84519bb04ac1aba858ed46268b64c0e56d0`、PR84、開始時worktree clean。既存Skills/remote_ci_only継続のCI収集のみ。`run list --commit <同SHA> --event pull_request` exit0、`run watch 39789901203 --repo ibis-ssl/Duck --exit-status --interval 5` は終端failure・exit1。`run view --json`、test job119862857801の`--log`、artifacts一覧GET、artifact12429644430のzip GETは全exit0。正確な引数/exitをcommands-summary.json、各stdout/stderr/exitを証拠dirへ保存。CIの標準dotnet testコマンドはci-test-command.txt、実dotnet-test.exit-code=1。ローカルbuild/testは今回実行していない。

### 取消後準備解除の最小Green候補

親tdd-executorがexactbe8bd845 CI39789901203の取消後ready残留を有効Redに採用した後、同じ担当/既存Skillsで最小実装。2sourceの既存SDK宣言抽出exit0。`/usr/lib/dotnet/dotnet build Testing/Duck.Testing.Diagnostics/Duck.Testing.Diagnostics.csproj --no-restore -m:1 /nr:false -p:EnableSourceControlManagerQueries=false` はexit0・警告0・エラー0。同じ引数のDriver project buildはexit1、既存生成Referee不足のCS0246×2/CS0400×2。`git diff --check` exit0。実引数/出力/exit/source差分は `/tmp/issue81-referee-receive-cancel-green-candidate-20261022/` に保存。今回test実行/CI操作なし。

### 取消後準備解除のGreen CI収集

HEAD cc771ba36de6045148f720d398fdef6fe2b8805d、開始時worktree clean。既存Skills/remote_ci_only継続のCI収集のみ。`run list --commit <同SHA> --event pull_request`、`run watch 39791056335 --repo ibis-ssl/Duck --exit-status --interval 5`、`run view --json`、test job119866873891の`--log`、artifacts GETは全exit0。正確な引数/exit/raw logは証拠dircommands-summary.json等へ保存。test step successとworkflowの終了値伝播を確認、成功runのdotnet生exitcode artifactは未取得。

### 単一Runの先行テスト準備（取消Green後）

親継続指示で単一Runの1先行testだけを追加。expectations.jsonをsource保存前に固定。既存SDK宣言抽出の正確な引数はsyntax.command.json、exit0（semantic compileではない）。新build/test/CIは未実行でfunctional Red pending。`git diff --check` exit0、未追跡testの`git diff --no-index --check -- /dev/null Tracker/Tracker.Tests/RefereeReceiveSingleRunTests.cs` はexit1・stdout/stderr空（新fileとの差分、空白指摘なし）。

### 単一Run testの同SHA runtime Red収集

HEAD `6b09edaf30d905f0e322650ed9f18906bca0950a`、PR84、開始時worktree clean。既存Skills/remote_ci_only継続のCI収集のみ。`run list --commit <同SHA> --event pull_request` exit0、`run watch 39792254453 --repo ibis-ssl/Duck --exit-status --interval 5` は終端failure・exit1。view/artifacts GET/zip GETはexit0。終端前job log取得は`job ... is still in progress`でexit1、初回履歴をtest-log-before-terminal.*へ保持。watch終端回収後の同job log取得はexit0。全引数/exitはcommands-summary.json。CIの実dotnet testコマンドはci-test-command.txt、dotnet-test.exit-code=1。今回ローカルbuild/testなし。

### 単一Run排他の最小Green候補

親がexact6b09edaf CI39792254453の最大同時Receive1→2を有効Redに採用した後、同じ担当/既存Skillsで最小実装。既存SDK宣言抽出exit0。`/usr/lib/dotnet/dotnet build Testing/Duck.Testing.RefereeDriver/Duck.Testing.RefereeDriver.csproj --no-restore -m:1 /nr:false -p:EnableSourceControlManagerQueries=false` はexit1、既存生成Referee型不足のCS0246×2/CS0400×2。`git diff --check` exit0。実引数/出力/exit/source before-after/diffは `/tmp/issue81-referee-receive-single-run-green-candidate-20261022/` へ保存。今回test/CIなし。

### 単一Run排他のGreen CI収集・再開候補

HEAD `5811bd88210c46ad89d49590d74e2d42c14af643`、開始時worktree clean。既存Skills/remote_ci_only継続。`run list --commit <同SHA> --event pull_request`、`run watch 39793648955 --repo ibis-ssl/Duck --exit-status --interval 5`、終端view/log/artifacts GETは全exit0。長い待機中のview1回でrun/test queued・step未開始を確認しview-waiting.*へ保存、重複watch/rerunなし。同watchから最終successを回収。全引数/exitはcommands-summary.json、標準CIコマンドはci-test-command.txt。成功runのdotnet生exit artifactは未取得。

### 再開・owner・freshness回帰の先行テスト準備

親採用の03A回帰単位として期待値をsource配置前にexpectations.jsonへ固定。既存/tmp再開候補をbyte同一でTracker.Testsへ配置し、owner/freshnessの2 Factを追加。既存SDK宣言抽出は新2fileでexit0、正確な引数はsyntax.command.json。新build/test/CIは未実行、次のexact-SHA PR CI待ち。`git diff --check` exit0、各未追跡testの`git diff --no-index --check -- /dev/null <file>` はexit1・stdout/stderr空（新fileとの差分、空白指摘なし）。

### 回帰CIの待機checkpoint・購読/tmp候補

CI39795794024 exactba2ba820のwatch1件を継続。14:34:32 UTCのviewはexit0、run/test queued・step未開始。watch exit未回収、重複watch/rerunなし。/tmp購読候補の既存SDK宣言抽出exit0、semantic compile/test未実行。

### 回帰job集計・購読test配置（run終端と分離）

旧run全体未終端で`gh run view --log`はexit1。公式job REST GET初回もgh端末escape出力保護でexit1/stdout0。親許可で`gh api --allow-escape-sequences --method GET repos/ibis-ssl/Duck/actions/jobs/119882661137/logs`を1回、stdout=PIPEからbinary fileへ捕捉してexit0。生制御文字を端末へ出さず除去済み集計だけ解析。恒久設定変更なし。初回0byte履歴と全引数/exitを保持。exec-server切断でmanifest/reportの1scriptは作成前失敗、read-only再接続で未保存を照合して保存再開、watch重複起動なし。

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

### 取消testの同SHA runtime Red収集

今回製品/test/期待値の変更なし、既存report子5欄のみ追記。証拠dir `/tmp/issue81-referee-receive-cancel-red-ci-20261022/` に生log/TRX/binlog/exit/source archiveと照合copyを保持。取消test SHA256 `76b9cb78854541fc613463c3d6499ef74c84f59b6596f84ffc1c3ea3af51366f`、製品source `7b72034fedb7d3a545d793827c240f76de91724a24ad2c49ac6913434053044d`。対象5fileと固定独立source7fileの全12fileをCI archive/HEAD/currentのbyte/hashへ照合。固定v1の11file/v2の9fileも元hash不変。ZIP SHA256 `702b436fbcd4442068b3f4b118c590dead834ae1193daed0ba64f88ed167c057` はAPI digest一致。

### 取消後準備解除の最小Green候補

製品変更は`Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs` SHA256 `d908ba59f7d0895f9cb6536cd2db803b6e5b214c13637a1cdc986fd50bee6920` と`Testing/Duck.Testing.Diagnostics/ServiceReceiveDiagnostics.cs` SHA256 `86aa44082a0945ddeac957e8fa8a1ee823223b7e6afc82c11ca132b7e8ab1223` の2file。取消test `76b9cb78854541fc613463c3d6499ef74c84f59b6596f84ffc1c3ea3af51366f`、既存positive/不正4case/csprojはHEAD byte不変。固定v1の11file/v2の9fileとrepo対応sourceも元hash不変。既存report子5欄へ追記し親prefix/Dispatch/履歴・親tasks/phases/evidence差分を保持。

### 取消後準備解除のGreen CI収集

今回製品/test変更なし。取消候補2source/取消test/既存positive/不正4case/csprojの全6fileがcandidate/HEAD/currentで一致。固定v1の11file/v2の9file・repo対応sourceも元hash不変、Redbe8bd845→当HEADのTracker.Tests差分は空。証拠dir `/tmp/issue81-referee-receive-cancel-green-ci-20261022/` のhead-source-manifest.jsonへ保持。成功runのsource archiveは未取得。

### 単一Runの先行テスト準備（取消Green後）

新規`Tracker/Tracker.Tests/RefereeReceiveSingleRunTests.cs`、1 Factのみ。SHA256 `1234b94a517ddb1d460f3643571721b1e2eba8072197db380cd313d26c55ac71`。製品/scaffold追加なし。既存6case/固定67と製品2source/csprojはbyte/hash不変。証拠dir `/tmp/issue81-referee-receive-single-run-red-preparation-20261022/` にexpectations.json / source-manifest.json / remaining-03A.json / syntax/checkの生log/exit / evidence-files-manifest.jsonを保存。

### 単一Run testの同SHA runtime Red収集

製品/test/期待値の変更なし、既存report子5欄のみ追記。証拠dir `/tmp/issue81-referee-receive-single-run-red-ci-20261022/` に生log/TRX/binlog/exit/source archiveを保持。新test SHA256 `1234b94a517ddb1d460f3643571721b1e2eba8072197db380cd313d26c55ac71`。対象7fileと固定独立source7fileの全14fileをCI archive/HEAD/currentへbyte/hash照合。固定v1の11file/v2の9fileも元hash不変。ZIP SHA256 `4e567615116007bd592f1a93d3cf850611681517d57a73860d12870e046cfe3d` はAPI digest一致。

### 単一Run排他の最小Green候補

製品変更は`Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs`のみ、SHA256 `cbe85435fe44f74513b025ac6aa8bd34285029521b615425808769c0f9a9cbf2`。単一Run test `1234b94a517ddb1d460f3643571721b1e2eba8072197db380cd313d26c55ac71`、既存6case/Diagnostics source/csprojはHEAD byte不変。固定v1の11file/v2の9file・repo対応sourceも元hash不変。既存report子5欄のみ追記、親prefix/Dispatch/履歴と親tracking/evidence差分を保持。

### 単一Run排他のGreen CI収集・再開候補

今回active branchの製品/test変更なし、既存report子5欄のみ追記。候補/current/HEADの対象7file、固定v1の11file/v2の9file・repo対応sourceのbyte/hash一致、Red6b09edaf→当HEADのTracker.Tests差分なしを確認。証拠dir `/tmp/issue81-referee-receive-single-run-green-ci-20261022/`。待機中の親追加許可で`/tmp/issue81-referee-receive-restart-test-candidate-20261022/RefereeReceiveRestartTests.cs`を別dirへ準備。active repoには未配置、SHA256 `ee99c1913bd0f39ee4487a787791f4e647769394062ac7b9e5c709a443327879`、構文抽出exit0、semantic compile/test未実行。

### 再開・owner・freshness回帰の先行テスト準備

新規`Tracker/Tracker.Tests/RefereeReceiveRestartTests.cs`（1 Fact、SHA256 `ee99c1913bd0f39ee4487a787791f4e647769394062ac7b9e5c709a443327879`）と`Tracker/Tracker.Tests/RefereeReceiveReadinessBoundaryTests.cs`（2 Fact、SHA256 `4450730ef2f6cabbbf488cb75db9c30749013891c6e4f1ad18b22b09a763e924`）。製品/scaffold変更なし。既存7case/固定67/製品2source/csprojはHEAD byte/hash不変。証拠dir `/tmp/issue81-referee-receive-regression-preparation-20261022/` に実source copy、期待値、manifest、syntax/check生log/exit、残件一覧を保存。

### 回帰CIの待機checkpoint・購読/tmp候補

active branchの製品/test変更なし。購読候補は /tmp/issue81-referee-receive-subscriber-expectations-20261022/RefereeReceiveSubscriberTests.cs のみ、SHA256 00f7f4457cb57dcf41a6d703d31feaf452f873559a07d8f702b7b70b17b6278b。candidate-manifest/evidence-files-manifestとsource前の期待値を同dirへ保存。

### 回帰job集計・購読test配置（run終端と分離）

親採用で新規`Tracker/Tracker.Tests/RefereeReceiveSubscriberTests.cs`を既存/tmp候補とbyte同一で配置、SHA256 `00f7f4457cb57dcf41a6d703d31feaf452f873559a07d8f702b7b70b17b6278b`。active path構文抽出exit0/diffcheck0、新file no-index checkはexit1・出力空（差分あり/空白指摘なし）。製品/scaffold変更なし、旧CI対象9source/固定manifest不変。`/tmp/issue81-referee-receive-subscriber-red-preparation-20261022/source-manifest.json` SHA256 `ed2800c8f24040d7b7744ac541e81883fdab1541dad15d1bed056ed54932aa5d`。親scope report末尾の訂正/追加静的確認は読取り保持。

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

### 取消testの同SHA runtime Red収集

唯一のfailureは`Tracker.Tests.RefereeReceiveCancellationTests.CancellationAfterSuccessfulReceivesInvalidatesReadinessWithoutClockExpiry`。TRX messageは`Assert.False() Failure / Expected: False / Actual: True`、最初の位置はRefereeReceiveCancellationTests.cs:42（`Assert.False(stopped.IsReady)`）、duration=00:00:00.0104426。初期count2/readyと取消後pump終端の検査を通過し、固定時計のままreadyが残る実行時assertionを確認した。compile failure/型解決/timeoutではない。42行より後の件数維持/受信回数assertは未到達であり、それらの成功を主張しない。

### 取消後準備解除の最小Green候補

既存Diagnosticsには終了APIがなく、取消をdecode失敗件数へ混ぜず、世代を余分に増やさずに準備解除するため`EndEpoch(ReceiveEpoch)`を追加。lock内で現在epochの参照一致を確認し、active epochを終了、各sourceのstamp/status/readinessを無効化する。正常件数/失敗件数/最終受信UTCは保持する。既存public signatureは変更せず追加APIだけ、private epochをnullableへ変更しBeginEpochは生成した同じepochを返す。Runの`finally`から当該epochを終了し、取消/想定外例外は捕捉して成功へ変換しない。epoch照合は古い終端による他世代への副作用を防ぐ必要境界であり、再開/並行Run全体を完成したものではない。関数/XML説明は日本語。

### 取消後準備解除のGreen CI収集

直接証拠は標準621件の全成功。artifacts API total_count=0、個別TRX/成功test名は未取得。取消1件・既存5case・固定67のGreenは同一test byte/標準target/Red時621件構成と今回621集計成功の対応から確認し、今回個別Passed行を読戻したとはしない。成功時artifact非保存という既存workflow条件の限界を保持する。

### 単一Runの先行テスト準備（取消Green後）

scope report89行・台帳I81-03Aの単一常駐受信/Receive奪い合い禁止に対応。実parserと隔離receiverで第一Runの正常2packet/count2/ready/第三受信待機を確認後、同じserviceの第二Runを呼ぶ。同期拒否の例外型やTask参照同一性を固定しない。両tokenを取消し、返された全Taskの終端を有界に待って例外も回収する。第一Runがfaultしていないことと、receiver最大同時受信数が1であること（44行）をassertする。現実装で最大2が候補だが未実行なので断定しない。timeout/compile-errorを単一pump契約のRedにしない。

### 単一Run testの同SHA runtime Red収集

唯一のfailureは`Tracker.Tests.RefereeReceiveSingleRunTests.OverlappingRunsNeverCreateConcurrentReceives`。TRXは`Assert.Equal() Failure: Values differ / Expected: 1 / Actual: 2`、最初の位置RefereeReceiveSingleRunTests.cs:44（MaxActiveReceives）、duration=00:00:00.1682063。初期正常受信/ready、両Task終端、第一Run非faultの検査後に、最大同時Receiveが2本となる実runtime assertionを確認。型解決/compile failure/timeoutではない。第二Runの例外型やTask同一性は固定していない。

### 単一Run排他の最小Green候補

取消済みtokenを先に確認し、`Interlocked.CompareExchange`で同じserviceの実行所有をBeginEpoch前に取得する。既に所有中なら第二Runを拒否し、既存epoch/receiverへ触れない。取得後は外側finallyで`Interlocked.Exchange`により解放するため、BeginEpochの失敗や内側EndEpochの例外でも所有解放を通る。元の受信loop/不正入力処理/EndEpochの本体は字下げ以外不変で照合済み。例外型をtest期待値へ追加せず、広いcatch/取消の成功変換も追加しない。関数説明は日本語。再起動/購読/transportを先回り実装していない。

### 単一Run排他のGreen CI収集・再開候補

直接証拠は標準622件の全成功。artifacts API total_count=0、runner内TRX生成logはあるが個別TRX/成功test名は未取得。単一Run/既存6case/固定67のGreenは同一test byte/標準target/以前の622件構成と今回622集計成功の対応から確認し、個別Passed行の直接readbackとは区別する。成功時artifactを保存しない既存workflow条件の限界を保持。

### 再開・owner・freshness回帰の先行テスト準備

scope report47/78/89行と既存I81-01/02の共通診断契約に対応。再開testは旧/tmp候補とbyte同一で、旧callbackのReceiveAttemptを実decoderへ渡す。owner欠落testは正常復号2件・既知age・producer endpointがあってもIdentity=null/IsReady=falseを要求し、endpointを資源所有者へ代用しない。freshness testはt0の正常2件でready、t2の反復pollで件数/最終受信UTC/ageを維持、期限ちょうどはready、1tick超でnot-ready、その後の反復pollでも延命しない。すべて隔離receiver/実parser/手動時計を使い、finallyでRunを取消・終端回収する。

### 回帰CIの待機checkpoint・購読/tmp候補

現在CI pendingで625総数は見込みのみ、実測/Green/Redなし。購読候補はIRefereeCommandSource経由の1subscriberに限定。同counterは診断件数を増やし購読pending、新counterで該当snapshot、最大active Receive1と受信回数をassertする。多subscriber仕様・例外型/Task同一性を追加しない。

### 回帰job集計・購読test配置（run終端と分離）

旧CI39795794024 exactba2ba820の完了済みtest jobの公式ログで625/625成功・0失敗・0skipを直接確認。生42342bytes SHA256 `ba9cf64fd853a637a26a028b3a6a73b270cd2462955df586affa2025d18dcf11`。run全体は14:47:46 UTC viewでin_progress、個別TRXはartifacts0で未取得。回帰3件/旧7case/固定67の対応は同一source/target/625集計から確認し、個別TRX Passed行の直接readbackとはしない。新購読testは旧runに含めない。

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

### 取消testの同SHA runtime Red収集

`https://github.com/ibis-ssl/Duck/actions/runs/39789901203`、exactHEADbe8bd84519bb04ac1aba858ed46268b64c0e56d0 / pull_request / completed / failure。TRXは621実行・620成功・1失敗・0skip、error/timeout/abortedも0。新取消testだけが失敗。残620件の名前集合は前回620件と完全一致し全Passed（existing620-results.json）、既存positive/不正4caseと固定独立67件も個別TRXで全Passed（fixed67-results.json）。packet/fullstack jobsはSkipped。TRX SHA256 `4e5e96b367262480bcc0f2f08968e9ad2808023a1b923e6f2856f52d57dc8c71`、ci-evidence.json SHA256 `7efd518b1a098c1fbef5b9988aba58bcf836fc79adacbaf335b7d7c219b3703b`。有効Redの親採用判断へ証拠を返す段階で、本behaviorは変更しない。

### 取消後準備解除の最小Green候補

取消後not-readyの最小Green候補を保存。source-results-manifest.json SHA256 `e69978f44d776b6ba3ff18a7ef4d968db835687dafca6b11c0bbce6140d9b9e3` に採用Red/source before-after/不変test/検査結果を保持。BCL診断libraryのbuildは成功したが、実Driver semantic compileと同一取消testのGreenはローカルproto不足で未確認。実行test0件。親の非main commit/push後exact-SHA PR CIで、同じ取消testと既存620/固定67の結果を回収する。build制約をGreenへ数えず、元のruntime Redと旧成功履歴は保持。

### 取消後準備解除のGreen CI収集

`https://github.com/ibis-ssl/Duck/actions/runs/39791056335`、exactcc771ba36de6045148f720d398fdef6fe2b8805d / pull_request / completed / success。test job119866873891ログで621/621成功・0失敗・0skip。packet/fullstack jobsはSkipped。RedはCI39789901203 exactbe8bd845の取消後IsReady true（line42）。同じtest byteで終端準備解除の最小Greenを標準集計で確認。ci-evidence.json SHA256 `c831020c297a577e67e6767f02f653e7ac7487121cbf9c99fcd0697498dfe88d`。

### 単一Runの先行テスト準備（取消Green後）

単一Runの先行test候補保存済み。syntax exit0、実行0件、semantic compile未確認、CI Red待ち。expectations SHA256 `10a3b74b6eaecc58227452a4a69342fc314fa8edeb61038017dfcfa1eeb6896d`、source-manifest SHA256 `67652d6526e5e14710647c49b47b40fe1a6d4c04fee789eed0d3ebd5cb9290b0`。親の非main commit/push後exact-SHA PR CIで実assertionと全体回帰を確認し、有効Red採用後だけ最小behaviorを実装する。621集計Greenは前節へ保存済みで、この新1件へ転記しない。

### 単一Run testの同SHA runtime Red収集

`https://github.com/ibis-ssl/Duck/actions/runs/39792254453`、exact6b09edaf30d905f0e322650ed9f18906bca0950a / pull_request / completed / failure。TRXは622実行・621成功・1失敗・0skip、error/timeout/abortedも0。新単一Run testだけ失敗。残621件は前回621件と名前集合完全一致・全Passed（existing621-results.json）、既存6case/固定67も個別TRXで全Passed。packet/fullstack jobsはSkipped。TRX SHA256 `f8c8c7a68c298e5a8842d2b7b9683e68928a77633a98030c2cdf1ffba08fd7e0`、ci-evidence.json SHA256 `8734ad7f50f699b2e2a039f74358545256a5fff60bb490a95be6afa73eb06a83`。有効Red採用判断のため親へ返し、behaviorを実装していない。

### 単一Run排他の最小Green候補

単一Runの最小Green候補を保存。source-results-manifest.json SHA256 `3b8d7ba9c899a560cf0528f2fe70d738ca2e2f238398ecf7ef3e70262a9a55cd` に採用Red/変更source/不変test/hash/検査結果を保持。構文抽出成功だがDriver semantic compileは既存proto不足で未確認、test実行0件、Green未観測。親の非main commit/push後exact-SHA PR CIで同じ単一Run test・既存621/固定67を確認する。初回実runtime Redを保持し、ローカルcompile-errorをGreenへ転記しない。

### 単一Run排他のGreen CI収集・再開候補

`https://github.com/ibis-ssl/Duck/actions/runs/39793648955`、exact5811bd88210c46ad89d49590d74e2d42c14af643 / pull_request / completed / success。test job119875539485で622/622成功・0失敗・0skip、packet/fullstack jobsはSkipped。RedはCI39792254453 exact6b09edafのMaxActiveReceives Expected1/Actual2 line44。同じtest byteで最小排他のGreenを標準集計で確認。ci-evidence.json SHA256 `5449dfdfc2d72736b98ad9c55da9ad7417fdd58c4f0743b507ddedb043ae8ea7`、証拠一覧manifest SHA256 `f3abcbc39ffee479a079e5a105c31aeee86f0a07d8c835fb706831600118c066`。
次候補は、同じserviceを取消/終端後に再開→無受信ならcount2/not-ready→旧captureReceive callback由来のReceiveAttemptを実decoder経路へ渡してもcount2/not-ready→新epoch正常1packetはbaseline(count3/not-ready)、次正常packetで増分(count4/ready)を確認する1件。時計固定、旧callbackは共通RecordSuccessをtestから直接呼ばずtransport seamへ渡す。候補は現在のepoch/終了処理で通り得る回帰検証として分類し、未知の不足や人工Redを捏造しない。candidate-manifest.json SHA256 `40901ae4238b7a269a5a886bc15bc789a2c5ae1ace15531f76c88f6e4a466e9c`、期待値はsource作成前にexpectations.jsonへ固定。

### 再開・owner・freshness回帰の先行テスト準備

新3 Factの回帰候補を保存・構文抽出exit0。実行0件、semantic compile未確認、Red/Green未観測。既存挙動で通る場合は回帰Greenとして受け取り、不要な人工Red/製品修正を加えない。不足が実runtime failureになった場合だけ、親のCI証拠採用後に修正する。source-manifest.json SHA256 `c7763e6e06a897f1352a1017771b3475e0b7ff07671b6e7fa790f6f7e723a7fe`、expectations.json SHA256 `dd91cc0e8b351464e9e9adc0b6ab41e277b0bd5eda94c0e32bb985dbda174691`。前節622集計Greenを新3件へ転記しない。

### 回帰CIの待機checkpoint・購読/tmp候補

対象9source/固定manifestは準備/HEAD/current一致。CIはqueuedのままで結果未確定。ci-pending.jsonへ時刻/状態を保存。購読sourceは/tmp候補のみでactive repo未配置、構文抽出0、実行0件。

### 回帰job集計・購読test配置（run終端と分離）

旧job集計は625成功、旧run全体の終端/元watch exitはまだpending（ci-evidence.json SHA256 `d8cafda44ca7c93d0e03e43077b1eeb27a4b364ee420853018f11af9cdba5a73`）。新購読test配置は完了、実compile/test0件・Red未観測。1subscriberだけをIRefereeCommandSource経由で検証し、同counterで診断count3/購読pending、新counter8でSTOP返却/count4、最大Receive1を期待する。多subscriber/例外型/Task参照同一性の未合意仕様は追加しない。次commit/pushのexact-SHA CIへ渡すtest-only候補であり、製品behavior未実装。

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

### 取消testの同SHA runtime Red収集

取消の無効化/再開epoch/同時Run排他等を完成したとはしない。既存test期待値緩和/skip化なし、03B/C・外部/依存/設定/環境/auth/network変更・host/Docker/実機起動・commit/push/GitHub変更/再実行なし。全file一覧`/tmp/issue81-referee-receive-cancel-red-ci-20261022/evidence-files-manifest.json`（55file、SHA256 `6078e873cbb529e612e8238fb7ce29f6828c7c8126001ff723c6c43aa156d518`）に生log/TRX/zip/source archiveを含む。親の保存時はgitignore対象のraw logも省略しない。全list/watch/view/log/download/read/save process終端、exit回収済み、実行中process/session・未回収exit・保留approvalなし。

### 取消後準備解除の最小Green候補

未Redのsubscriber/再起動手順/並行Run排他/実transportは今回実装していない。追加EndEpochの旧世代・反復終了等を含む広い生命周期検証も今回実行したとは主張しない。03A残件、03B/C、外部G1/G3、実機受入は未完了。期待値緩和/skip変更、外部/依存/設定/環境/auth/network変更、host/Docker/実機起動、commit/push/GitHub変更なし。全証拠一覧evidence-files-manifest.json SHA256 `55489db007859284f8a06bccb27aca9f3d287e34b227b5ce600d0749fefe1070`（24file）に生logを含む。全build/構文抽出/read/save process終端、exit回収済み、実行中process/session・未回収exit・保留approvalなし。

### 取消後準備解除のGreen CI収集

個別TRX未取得を保持。03A全体完了や実機受入は主張しない。次の最小案は重なるRun呼出しでもactive Receiveが2本にならないことの先行test。二重Run拒否/同じTask再利用のどちらかへ例外型/戻り値を過剰固定せず、両taskを取消して終端回収し、隔離receiverの最大同時受信数を検証する。全file一覧evidence-files-manifest.json SHA256 `755bb1c3d099cd56bea5ced52f5424af6ae06bf89c391bd70af22648e9e273d7`（25file）。今回CI収集の全process終端・exit回収済み。commit/push/GitHub変更・次behavior・外部/環境/依存変更なし。

### 単一Runの先行テスト準備（取消Green後）

単一Run testは準備段階で未完了。再開/旧epoch/遅延callback、subscriber重複抑制と診断分離、owner欠落/source帰属/freshness、実11003 UDP transport/options/lifecycleはremaining-03A.jsonの残件。03B/C・DCP配置・実機受入へ広げない。既存期待値緩和/skip化なし、製品behavior・外部/依存/設定/環境/auth/network変更、host/Docker/実機起動、commit/push/GitHub変更なし。全証拠一覧evidence-files-manifest.json SHA256 `0101c3e560594097af771b3634a2984785b12fbcb5dc3627e2052f1f37abf5c2`（17file）。全process終端、未回収exit/保留approval/実行中processなし。

### 単一Run testの同SHA runtime Red収集

単一Run排他、再開/旧epoch/遅延callback、subscriber/実transport等を完成したものではない。既存期待値緩和/skip化、03B/C・外部/依存/設定/環境/auth/network変更、host/Docker/実機起動、commit/push/GitHub変更/再実行なし。全証拠一覧`evidence-files-manifest.json` SHA256 `7d0bb100e2b959f5d9e543d766bd9bbb7c15f139c72f6624d7bc93b9369ec50e`（65file）に生log/TRX/zip/source archiveを含む。親の保存時はgitignore対象のraw logも省略しない。全watch/view/log/download/read/save process終端、exit回収済み、実行中process/session・未回収exit・保留approvalなし。

### 単一Run排他の最小Green候補

同時Runの最小排他以外の再開/旧epoch/遅延callback/購読/実transport検証は未完了。開始前取消/BeginEpoch失敗/EndEpoch失敗の所有解放はコード境界として扱い、今回それぞれのruntime test成功を主張しない。03B/C・外部G1/G3・実機受入も保留。既存期待値緩和/skip化、外部/依存/設定/環境/auth/network変更、host/Docker/実機起動、commit/push/GitHub変更なし。全証拠一覧evidence-files-manifest.json SHA256 `e912ef9250130e0264d2e427fdee1ab21b53ac700f38716eeaa53e83ea09e66f`（17file）に生logを含む。全build/構文抽出/read/save process終端、exit回収済み、実行中process/session・未回収exit・保留approvalなし。

### 単一Run排他のGreen CI収集・再開候補

次候補は/tmpのみ・構文抽出のみで、実semantic compile/実parser実行/RedまたはGreenは未観測。親が採用/配置して同SHA CIで実測するまで成功を断定しない。既存再開/epoch実装が期待どおりなら回帰成功として扱い、不要なbehaviorを追加しない。subscriber/owner/freshness実統合/実UDP transport、03B/C、DCP配置・実機受入は未完了。外部/依存/設定/環境/auth/network変更、host/Docker/実機起動、commit/push/GitHub変更/再実行なし。生logを各dirのevidence-files-manifest.jsonへ列挙。全watch/view/log/構文抽出/read/save process終端、exit回収済み、実行中process/session・未回収exit・保留approvalなし。

### 再開・owner・freshness回帰の先行テスト準備

03A残件はremaining-03A.jsonへ記録。購読/同counter変更待機の重複抑制、実11003 UDP transport/options/lifecycle、現在no-opのDispose/停止所有・受信資源解放の契約と回帰、必要な開始失敗/終端境界検証を残す。今回3件もCI未実行で完了扱いにしない。03B/C・DCP配置・実機受入は後続。既存期待値緩和/skip化、製品behavior・外部/依存/設定/環境/auth/network変更、host/Docker/実機起動、commit/push/GitHub変更なし。全証拠一覧evidence-files-manifest.json SHA256 `9f22e889f8f74078efed0e09dcccd3933429897e447530da7e4c66b8618205c1`（23file）。全構文抽出/read/save/check process終端、exit回収済み、実行中process/session・未回収exit・保留approvalなし。

### 回帰CIの待機checkpoint・購読/tmp候補

稼働は既存watch session17234だけ、watch exit未回収。他の候補生成/構文抽出/read/save processは終端。CI結果を取得するまで回帰3件の成功や失敗を断定しない。製品behavior/外部/依存/環境変更なし。

### 回帰job集計・購読test配置（run終端と分離）

稼働は既存watch17234のみ、exit未回収。他のGET/構文抽出/read/save/check processは終端。旧run全体完了をjob集計成功へ読み替えない。親scope末尾の外部前提訂正を保持し、外部ソース変更ゼロnamespace共有は第一評価候補だが未採用/未適用。既存args/loopback維持余地、anchor再作成責務、DCP Networks+RunArgs共存等は上位親の静的判断材料で、このunitでは適用/検証しない。外部改変必須とせず現経路不足/代替調査中を分ける。購読behavior・実UDP・Dispose等は未完了。既存期待値緩和/skip化、外部/依存/環境変更・commit/pushなし。

## 親による公開準備記録

先行テストと最小scaffoldの証拠を reports/evidence/issue81-referee-receive-red-preparation-20261022/ へ保存。functional Redは未観測で、次の同一SHA CIで実行結果を確認する。現段階で本動作は実装していない。
