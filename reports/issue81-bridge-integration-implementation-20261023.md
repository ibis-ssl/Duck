# Sub-agent実行レポート

## タスク

Issue81 / PR84 / I81-05B。全資源の標準コンテナ構成・外部宛先と資源内診断の統合を既存承認と具体契約で進める。

## sub-agentを使う理由

同じIssue実装担当を継続し、親はASPIRE全体の順序・依存・権限・通常レビューを管理する。

## 対象範囲

05A技術HEAD3a7e62e6e8b3fad156414f47be43963b835298b7の通常収束記録を土台とする。既存固定イメージと外部設定、同じDCPブリッジ、三つのマルチキャスト、GC唯一送信元、比較追跡器、診断と準備判定を契約ごとに照合し、実装可能な部分を先行試験から進める。G1/G3は未確認というだけで包括禁止や課題全体の停止条件にしない。

## 対象外

無関係な新Issue/PR、外部ソースの無承認変更、依存・認証・環境の無承認変更、拒否操作の迂回、未受入の成功主張、merge。新しい操作が必要な場合は本人の既存承認と照合し、その具体操作だけを保留する。デバッガー固定版調査は任意。

## Dispatch profile

- selection_source: explicit_user_override / continuity_reuse
- task_kind: implementation（前段の固定API調査を継承）
- signals: work_class=judgment_heavy; uncertainty=high; change_radius=cross_system; criticality=high; repetition=single; decomposability=sequential_dependencies; decomposition_policy=forbidden; context_need=bounded_history
- executor: /root/issue81_luna_implementation
- original_requested: {model:gpt-6-luna,reasoning_effort:high,fork_turns:none}
- applied: null
- application_status: reused_existing_agent_profile
- runtime_profile_observability: final_profile_hidden
- role_plan: original default role; effect unknown
- override: 自動選択候補Sol highより本人の実装・調査Luna high指定を優先。同一担当を継続し新規起動なし。
- previous_unit_record: 9284640aaec4af59e44afe7687e4bf8a39494c48 / CI39946025164 success。標準testとLinux発行が成功、packet/full-stackはskip。既存取得結果をreports/evidence/issue81-05a-ci-9284640-20261023へ保存。
- policy_deviation: PD-I81-06A1-TDD。HostedService接続の追加5件目は製品実装後の試験で、事前Red欠落。既存4件Redや後のstartup-race Redで代用しない。本人の例外承認はなく、TDD工程完了とはしない。機能検証と必要修正は継続する。
- report_persistence_mode: normal_persistence
- verification_capability: 固定入力・既存cacheの復旧によりAppHost build/modelはlocal_execution_available。実Docker/DCP/Windowsの操作と証拠は個別に照合する。
- dependency_reorder: 05B1のNR005実identity供給には06Aを先行。固定Aspire v13.5.4 / 9c1b401dd67746739044f68959cbf4d3d7af93a6 の公開通知container.id契約を使用し、作成後の受渡し・所有者/起動世代を設計と先行試験で固定する。実機成立の主張ではない。
- next_unit: I81-06A1。完全ID指定execから自社controlmode/資源内socketへ渡す経路を設計・先行試験から実装する。DCP lifecycle所有を保持、実exec/実機は未確認。BootId・起動世代・旧run拒否を測定し、候補のlease等は必要性を評価して扱う。
- authority: ASPIRE全体統括、現在Issue81。本人09:32固定版調査任意、09:33親要求を鵜呑みにしない、09:43全体統括を適用。最大2Issueと既存担当を維持。

## 実行コマンド

- 05A終了時の記録HEAD `9284640aaec4af59e44afe7687e4bf8a39494c48` をread-only確認。親回収済みPR CI `39946025164` はcompleted/success（standard test/Linux publish成功、packet/fullstackはskip）。CIを重複実行していない。
- 読取: `Program.cs`、`DockerContainerSpec.cs`、`DockerContainerWrapper.cs`、`DockerWrapperResourceExtensions.cs`、`GameControllerReadinessMonitor.cs`、`TrackerPacketReadinessMonitor.cs`、`VisionServiceReadiness.cs`、`AppHostApplicationModelTests.cs`、主設計2文書。WSL Python読み取りはexit 0。事前に試したWindows絶対パス`C:\home\...`は存在せずexit 1、読取なしで終了し、WSL絶対パス方式へ切替。
- `git status --short` exit 0: 親所有 `Tracker/Design/tasks-status.md` の変更、前の証拠bundle、予約reportのみ確認。製品/試験ファイルは変更していない。各読取時のGit HEADは `9284640aaec4af59e44afe7687e4bf8a39494c48`。

### 05B1 — RuntimeHost vision readiness接続

- 固定先行テスト: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~DuckPublishesDecodedVisionReadinessOnItsInternalTcpEndpoint -p:EnableSourceControlManagerQueries=false`。Red exit 1、修正後Green exit 0。
- AppHost回帰: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~Tracker.Tests.AppHostApplicationModelTests -p:EnableSourceControlManagerQueries=false` → 24 pass / 0 fail / 0 skip、exit 0。
- AppHost build: `dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore --verbosity minimal -p:EnableSourceControlManagerQueries=false` → 0 errors、既存ASPIRE010 warning 1、exit 0。
- `git diff --check` exit 0。初回 `EnableSourceControlManagerQueries` なしのtestはrelativeworktreesをSDKが読めず、test開始前に2 project errorでexit 1。これは準備失敗である。既存成功経路のinvocation-only propertyを加えた後のruntime Redとは別に保存。
- raw command/stdout/stderr/exit、入力source hashを `reports/evidence/issue81-05b-runtimehost-health-tdd-20261023/` に保存。`manifest-v1.tsv` は20 fileを列挙しSHA-256 `092cb0e3ca20b6e7626f6b692b357ccba8f5a845ba01db1d4828e42fa72c1239`。
### 06A1 実装・検証追補

- 固定UDS matrixのRedは `reports/evidence/issue81-06a1-control-red-20261023/` に世代別で保持。元2件Redはbuild成功後1 pass/1 fail、拡張4件matrix Redは2 pass/2 fail（いずれも機能assert、exit 1）。後段で追加したHostedService第5件は実装前Redなし。これを元Redで代用しない。
- 最終 focused test `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=false --filter FullyQualifiedName~RuntimeIdentityControlProtocolTests` は5/5 pass、exit 0。全体 `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=false` は786/786 pass、skip 0、exit 0。以前の全体785/786失敗（HostedService UDS listener開始競合）は `full-tests-v1` として残し、TCS同期後の `full-tests-v2` と区別した。
- `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj --no-restore -p:EnableSourceControlManagerQueries=false` と `dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore -p:EnableSourceControlManagerQueries=false` は各exit 0。AppHostには既存ASPIRE010 warning 1件。中間compile失敗v2/v3も元rawを保持し、機能Redに数えていない。個別command/stdout/stderr/exitはbundle `inventory-v1.tsv` から追跡可能。
- 実行後、検証processはすべて終端し、未回収exitなし。Docker/DCP/container/exec/UDPは実行していない。

## 対象ファイル

実読取した主な対象（SHA-256）:

- `Testing/Duck.Testing.AppHost/Program.cs` `cb6f77516d2a2d2161ec29dba6a94da7339fb21aa5c2480755c82c134b200003`
- `Testing/Duck.Testing.AppHost/DockerContainerSpec.cs` `9c94409e7ffc0f093df74093186c4f0d4ddfb7f5d585c6e905a31f32a130cc4d`
- `Testing/Duck.Testing.AppHost/DockerContainerWrapper.cs` `bff840fa61abbd8929db73fa5bc33e29d419ce10d0d039c9324e4ba8b6f077fd`
- `Testing/Duck.Testing.AppHost/DockerWrapperResourceExtensions.cs` `6eef2665623d4d2108523cda14afac0dc9d3d275bf102e9bc23241c1632026d0`
- `Testing/Duck.Testing.AppHost/GameControllerReadinessMonitor.cs` `f8d9e75676a5829d91b3bb2c90783d984c9042dded68cbcd793c6b631571ea64`
- `Testing/Duck.Testing.AppHost/TrackerPacketReadinessMonitor.cs` `2d8bb85d70de6051555fb067599bf4efa144ef245a19adfd2ba70f13c005f128`
- `Testing/Duck.Testing.AppHost/VisionServiceReadiness.cs` `ff6b9a02874144a627d66ae051e2545fc46f2525e30bc892cfe8bf099e1ffe27`
- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs` `a63641ea949a3222fa11cf3934b4b241a137702b917e43e226a2768814fd0d16`
- `Tracker/Design/Testing/aspire-simulation-test-environment.md` `15205d3b20221336368ea5b2f3375f88e6cc08b22e62371e1f4edb9e80d413d9`
- `Tracker/Design/Testing/tracker-comparison-debug-design.md` `c1bfa61d4a5267bf4ac429791dbf19ad5c71e247052a461105eafdd69036cc85`

05B1先行変更時: `Testing/Duck.Testing.AppHost/Program.cs` SHA-256 `7773446749b7d3b7bf1de2d222dc2eb4a25e880172d6fb59f5d740f4df00559e`。05B1先行テストのRed/Green時source hashは `69d8772874b8dde7306330a81cad0bf0e3dd66750b2be8b4db17bff080bfd107`。後続NR006 fixtureを同じtest fileへ追加したため、現在のtest file hashは `93e66834284a5bc27abd10df3cb9901c313aec1c0b15e4284452db04c8e21873`。設計文書、固定publish定義、外部ソースは変更していない。
### 06A1 実装差分と固定hash

- 追加: `Tracker/Tracker.RuntimeHost/RuntimeIdentityControlProtocol.cs`（SHA-256 `8506a43d3fac16be8d2d7e543d9a848a9586c4e2c511980856a5821cf034e183`）、`Tracker/Tracker.Tests/RuntimeIdentityControlProtocolTests.cs`（`a9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06`）。
- 変更: `Tracker/Tracker.RuntimeHost/Program.cs` (`837a4bda...`)、`RuntimeHostServiceCollectionExtensions.cs` (`cc736234...`)、`RuntimeVisionReceiverService.cs` (`58582bb9...`)、`Testing/Duck.Testing.Diagnostics/ServiceReceiveDiagnostics.cs` (`84200328...`)、`Tracker/Tracker.Tests/AppHostApplicationModelTests.cs` (`93e66834...`)。AppHostの現在hashは `Testing/Duck.Testing.AppHost/Program.cs` `caaaab79...`。全ての正確なhashは `reports/evidence/issue81-06a1-control-red-20261023/verification-summary.md` とinventoryを参照。
- 固定fixture matrix test fileのRed前SHA `dbf7f2b00acc039e651d30b6f0dfe6fe647874afdff2ca1aca5b1e64d19dfa29` を維持して実装した。既存AppHost model assertionsの差分はNR006の固定39108 preflightを競合fixtureで置換する05B1作業であり、今回UDS testの期待値変更ではない。

## 指摘事項

### 要件と現状

- 主設計 `aspire-simulation-test-environment.md` 3–15, 21–33, 101–114 は全UDP資源をAspire/DCP管理ブリッジへ置き、UDPをhostへpublishせず、資源名で接続する契約を置く。現コードの `Program.cs` 85–98, 113–128, 143–173, 212–243 はsimulator/Game Controller/CM4/Crane/Tigers/ER-Forceを `AddDockerWrapper` の `ExecutableResource` として登録。wrapperは `DockerContainerSpec.CreateRunArguments` で必ず `--network host` を付け、`IsExactOwner` も `NetworkMode == host` を所有条件にする。
- 05Aで `duck` は通常 `ContainerResource`、`debug-host`は比較モードだけの標準コンテナ。RefereeDriverは明示選択時だけ標準コンテナ。現在のGCは唯一のreferee publisherとして11003を維持し、vision/tracker/refereeのマルチキャストは既存引数のまま。
- wrapperは単なる起動器ではない。`DockerContainerWrapper` はコンテナID/label/image/networkを再検証し、ログ転送・停止・正確な所有者の削除を行う。`DockerWrapperResourceExtensions` はhealth listenerをAspire readinessへ接続。特にGame Controller readinessはbridge内11003のreferee受信状態と `ws://127.0.0.1:8082/api/control` のWebSocket/team-name状態を結合する。Simulatorはbridge内でvision datagramをdecodeするreadiness、Tigers/ER-Forceはtracker sourceとvision packet freshnessを同じwrapperで判定。

### 最小単位の分解

- 登録モデルとreadinessは**試験上は分離して測れる**（AppHost生成モデルの資源型/annotation/port/dependencyを固定値で検査し、readiness monitor状態を単体で検査できる）。ただし実行構成を1資源ずつ標準コンテナへ切替えると、既存monitorはhost-network名前空間のUDPを受けられず、health/readiness/cleanup意味を落とす。よってテストを分離しても、機能公開はreadiness経路を同じ実装単位へ含める必要がある。
- 通信トポロジー上の最小まとまり候補はmulticast cohort（simulator、既に標準化済みDuck、唯一送信元Game Controller、比較時Tigers/ER-Force）。これらを同じDCP networkに揃え、UDP endpointを公開せず、Game Controllerの8082 TCP host端点を動的割当・127.0.0.1にし、resource-name内部アクセスを保つ。固定マルチキャスト設定を維持する。
- G1のCrane/CM4数値宛先（設計168–177、既存調査の `127.0.0.1`/数値IP変換）はこのcohortから分離できるが、Crane/CM4の制御トラフィック成立はそのまま未確認。G3の統合診断も別契約として残す。Duckのvision受信状態を外部trackerの正常decode/受信指標に代用しない。
- cohortを移すと既存のwrapperにあるUDP readiness観測元をどこで実行するかが未解決。既存 `AddContainer` のHTTP health checkだけでは `GameControllerReadinessMonitor` のreferee multicast検証も、Tigers/ER-Forceのsource-aware UDP freshnessも置換できない。third-party pinned imageの起動引数に任意のhealth agentを加える既存APIは見当たらず、外部source変更は現在スコープの前提でない。DCPのcontainer内からreadinessを提供する既存の機能/ endpointがある根拠も未確認。

### 次の試験候補と受入値

- 先行AppHostモデル試験は実際の `Program.cs` registration pathを呼び、`ContainerResource`、固定index digest/entrypoint/args、UDP `EndpointAnnotation`なし、Game ControllerのTCP target 8082 + host port未固定 + target host `127.0.0.1`、GC唯一producer、選択モード/資源依存順を検査する。wrapperを使わない未接続の自己完結manifest試験だけで完了にしない。
- 別のreadiness契約試験では現状のUDP parser/source/freshness/GC API・チーム設定の意味を保ち、どのnetwork namespace内でcheckするかを実在するresource health経路へ接続する。host-only monitorが残る状態をmigration Greenにしない。
- 既存metadata設計から分離可能な固定期待値は既に試験可能。外部の実通信成立、Windows DCPのnetwork recreation、G1数値宛先、全診断は実機/後続契約として未検証のまま記録する。

### 05B1 固定受入値と変更

- 主設計 `aspire-simulation-test-environment.md` 183行はDuck内部vision確認口をTCP target 39108に置く。実装済みRuntimeHost `RuntimeVisionDiagnosticsHttpService` はContainerModeで `http://+:39108/` にbindし、`/health/vision` を処理する。`GetHealth` はContainerModeでは正常復号済みDetectionの準備状態がない場合503、受信後200を返す。これはDuck/RuntimeHost自身の実受信証跡であり、Simulator自身が正常decodeした証明にはしない。
- 先行AppHost model testは実際の `Program.cs` 登録を使い `Testing__ContainerMode=true`、vision group `224.5.23.2`/UDP target 10020、health endpoint target 39108・host port未固定・127.0.0.1・非proxied、HealthCheckAnnotation、UDP endpointなし、および既存simulator `WaitUntilHealthy` を固定値検査。
- Redはproperty付きtest実行で1件の実行時assertion失敗: `vision-health` endpointがまだAppHost modelに無かった。Program編集前のtest source SHAとafter source SHAが同一。setup compile失敗は別の試行として保全。
- 最小修正はDuckの既存ContainerResourceへContainerMode設定、dynamic host TCP 39108 endpoint、`/health/vision` HTTP health checkを追加。既存 `WaitFor(simulator)` は保持したので依存循環を作らない。新helper・新service・依存は追加しない。
- module testsはHTTP responseの実DCP到達やpacket受信を起動検証していない。model annotationと既存endpoint実装の接続までを確認。UDPをhostへpublishしていない。
### 06A1 実測範囲と履歴

- RuntimeHost内UDSで同一接続のprepare/challenge/commit、BootId/run/resource/container/generation照合、同一tuple再送の冪等、新identity受理時の診断証跡リセットを実装しfixture化。受理後は新しい正常受信までstrict health 503、受信後200、旧epochのattempt拒否を検証。
- `ResourceId="duck"` の固定envとランダムRunId env注入は一時案を撤回。現在のAppHostにはResourceEvent購読・完全container ID抽出・実Docker execによるhandoffは実装されていない。ResourceIdは実際の `ResourceEvent.ResourceId` に由来する必要があり、役割名 `Resource.Name` や engine `container.id` と置き換えない。
- TDD欠落 `PD-I81-06A1-TDD` は未解消。HostedService起動接続の第5 test は後から追加され、事前Redを取得していない。全体testのstartup raceは別の後発回帰Redであり、これを第5 testのRedに読み替えない。

## 結果

今回の区切りでは、05B1のRuntimeHost vision readinessを実AppHost登録経路へ結ぶ先行テストと最小実装を完了した。実行時Redでvision-health endpoint欠落を確認後、ContainerMode設定・TCP target 39108 (dynamic host port、127.0.0.1、非proxied)・/health/vision health checkをDuck ContainerResourceへ登録し、既存simulator WaitForを保持した。固定testはRed以後byte不変。focused test 1/1、AppHostApplicationModelTests 24/24、AppHost build成功、git diff --check成功。raw commands/stdout/stderr/exitとmanifest-v1.tsvをreports/evidence/issue81-05b-runtimehost-health-tdd-20261023/へ保存し、manifest SHA-256は092cb0e3ca20b6e7626f6b692b357ccba8f5a845ba01db1d4828e42fa72c1239。初回SDK relativeworktrees失敗は準備失敗として分離保存し、成功経路ではEnableSourceControlManagerQueries=falseを指定した。これはmodel annotationおよび既存HTTP handlerとの接続までの検証で、Docker/DCP/Windows到達、multicast受信、全UDP資源cohort移行の実証ではない。次の意味ある実装単位はDCP bridge上のmulticast cohort登録とreadiness供給を同時に成立させる契約。G1数値宛先とG3統合診断は別境界で保持できる。

### 05B1 結果

固定テストRed後にAppHost登録を最小変更し、focused test 1/1 pass、`AppHostApplicationModelTests` 24/24 pass、AppHost build exit 0。固定テストはRed以後byte不変。エビデンスは上記bundleのgeneration 1 manifestに固定。現在実行中のprocess/sessionおよび未回収exitなし。Docker/DCP/Windowsでのendpoint到達、multicast受信、全UDP資源cohort移行は未実施・未確認。親の通常reviewへ回す候補であり、05B全体の完了ではない。
### 05B1 通常レビュー追補（NR005 / NR006）

- 通常レビュー `reports/issue81-bridge-readiness-normal-review-20261023.md` は needs_changes、NR005/P1とNR006/P2がopen。NR005はRuntimeHost strict healthの実構成へRunId/ResourceId/完全ContainerId/Generationが供給されず、正常Detection後もDI identityがnullのため503のままになる点。偽のIDやstrict条件緩和は禁止。主設計の `aspire-simulation-test-environment.md` 205行は完全64桁IDと正の世代を指定し、欠落/不正時は帰属不明・準備未完了、同243行は再作成後の新完全ID取得を要求する。I81-06の再作成ID/endpoint更新との順序・owner供給契約を照合中であり、NR005の修正は未完了。
- 05B1のNR006は、comparison時に旧preflightがdynamic host portと無関係なTCP 39108をbind probeしていた点。先行テスト `ComparisonDuckHealthEndpointDoesNotRequireItsContainerTargetPortOnTheHost` は39108を占有してcomparison AppHostを構成する。製品変更前のruntime Redはpreflight `Probe`のbindでexit 1。ProgramからDuck専用39108 preflight登録だけ削除後、同テスト1/1 pass、AppHostApplicationModelTests 25/25 pass、AppHost build exit 0となった。製品Program SHA `caaaab79b24daa9a6722ccc269f050e41b518109e31de856063c2e0232bd8501`、test file SHA `93e66834284a5bc27abd10df3cb9901c313aec1c0b15e4284452db04c8e21873`。既存の固定ホスト端点preflightは保持。
- Red/Green/class/buildのraw command/stdout/stderr/exitは `reports/evidence/issue81-05b1-reviewfindings-20261023/` に保存。v1の文書lint scratchはWSL symlinkをWindows Nodeが解決できずtextlint/CSpellがMODULE_NOT_FOUNDで失敗した準備履歴として保持し、合格証拠にしない。親の指示で追加lintは台帳修正後まで保留し、正しい既存Windows junction経路で最終2行をまとめて検査する。

### 06A1 文書検査・証拠

- bundle: `reports/evidence/issue81-06a1-control-red-20261023/`（37 entries、inventory SHA-256 `7375252aed2235475d9bdd74d8b12fe14eaeae07f872c9d4e2222882219286d6`）と `reports/evidence/issue81-06a1-diff-lint-20261023/`（43 entries、inventory SHA-256 `ad96e407763310fac50516a1180f161246722f4a7d51683a4bbf67a6df1e342d`）。各bundleの `verification-summary.md` は対象・未検証を記録。
- 追加22行のdiff入力（base `9284640aaec4af59e44afe7687e4bf8a39494c48`、主設計/比較設計/tasks/phases）でtextlint exit0。CSpellはexit1かつ表示上限100、Sudachiはexit1で全unknown token listingを保存、stderr要約表示上限200。成功扱いにはしていない。9 config/dictionary関連ファイルhashを `config-hashes.json` に保存。
- Junction準備の初回失敗を保持: WSL symlinkでMODULE_NOT_FOUND、その後正しいWindows Junctionへ修復。1回目Node preflightは誤ったcwdでMODULE_NOT_FOUNDとなり、失敗JSONを保存。正しいmirror cwdの再preflightでLinkType Junctionおよび `require.resolve(textlint/cspell)` が両方exit0。依存や辞書は変更せず、lint再実行なし。
- Yomiyasu strictは同じ既存venvでbaseline/current全体を比較。主設計aspire-simulation-test-environmentはbaseline SHA `15205d3b...` / current `3437732d...` ともscore100、clean、exit0。比較設計はbaseline `c1bfa61d...` / current `c5513c1a...` ともscore91、exit1。各々 `excess_list` warning 1件・`negative_parallelism` info 2件で分類・件数は同じ。`negative_parallelism` の2本文断片hashは一致し行番号が移動。`excess_list`本文は追加行により全行数/割合（52.0%→51.4%）が変化したためsnippet hashは異なるが同じrule/severityである。strictの失敗は既存baselineにも存在し、成功とはしない。raw JSON、commands、exit、input hashesはdiff-lint bundle `yomiyasu/` に保存。

## リスク

- readiness pathの代替が決まる前にwrapperを単に `AddContainer` へ置換すると、AppHostが実サービスをhealthyと誤判定するか、healthyにならない。wrapper cleanupの厳密所有者・終了動作も失われうる。
- multicast cohortのみ移す間はG1対象のCrane/CM4とUDP宛先が接続しない可能性がある。これは隠さず選択modeの対象と未成立範囲を明記し、既定実行へ無断適用しない。
- 次の試験は実AppHost経路に結びつける。DCP/Windows実通信成功、外部imageの起動、container-local readiness sourceの存在、AppHost host-port経由でのmulticast readinessはまだ実証されていない。

### 05B1 残る境界

- ContainerModeを有効にするとRuntimeHostの内部HTTP serviceがall-interface bindされる。AppHost側はhost portをdynamicにし、target host 127.0.0.1、proxyなしの診断用TCPとしてだけ登録する。実DCPのport assignment/reachabilityは未試験。
- `WaitFor(simulator)`を維持しているため、この単位はSimulatorをstandard resourceへ移していない。RuntimeHostの受信準備完了はsimulatorの自身のdecode/healthと同一ではない。
- 他external tracker/GCのreadiness提供、全multicast cohortの構成関数と実適用、G1 Crane/CM4数値宛先、G3統合診断は引き続き未完了。

### 05B1 通常レビュー修正の終端追補（NR006 / NR005）

- NR006の固定競合試験は旧Programの39108 bind probeで実行時Red（exit 1）。probe登録のみを削除後、同試験1/1、`AppHostApplicationModelTests` 25/25、AppHost `--no-restore` buildがすべてexit 0。通常レビュアーはNR006をclosedと判定し、NR005/P1のみopenとして残した。固定host portのpreflightは維持した。Red/Green/class/buildのcommand/stdout/stderr/exitとhashは `reports/evidence/issue81-05b1-reviewfindings-20261023/` の `inventory-v1.tsv` に収録。inventoryは16件、合計17809 bytes、SHA-256 `6da0809c4c59a130c7fed184295f116cea5b2fb5bbff4ba122507557fb9e65db`。NR006修正後のProgram SHA-256 `caaaab79b24daa9a6722ccc269f050e41b518109e31de856063c2e0232bd8501`、AppHost model test file SHA-256 `93e66834284a5bc27abd10df3cb9901c313aec1c0b15e4284452db04c8e21873`。旧Redは保持し上書きしていない。
- NR005のAspire API/供給経路は、既存Aspire.Hosting 13.5.4のDLL公開型reflectionと、同じ固定版の公開ソースを読んで切り分けた。タグ `v13.5.4` はcommit `9c1b401dd67746739044f68959cbf4d3d7af93a6`。`BeforeResourceStartedEvent.ResourceInstanceId` はresource instance ID（ResourceEvent.ResourceIdと同値）であり、Docker engine IDとは定義されない。一方、固定版ソース `Dcp/Model/Container.cs` はDCP `ContainerStatus.ContainerId` を実Container IDとして保持し、`Dcp/ResourceSnapshotBuilder.cs` はその値を `KnownProperties.Container.Id` (`container.id`) の `ResourcePropertySnapshot` に入れる。`Dcp/DcpResourceWatcher.cs` はContainer変更時にsnapshot builderを呼び、その結果をresource changed通知へ渡す。公開 `ResourceNotificationService.WatchAsync` の `ResourceEvent.Snapshot.Properties` を読むことで、作成後の実IDを得る既存経路はある。固定ソースの確認範囲は [Container.cs](https://github.com/microsoft/aspire/blob/9c1b401dd67746739044f68959cbf4d3d7af93a6/src/Aspire.Hosting/Dcp/Model/Container.cs)、[ResourceSnapshotBuilder.cs](https://github.com/microsoft/aspire/blob/9c1b401dd67746739044f68959cbf4d3d7af93a6/src/Aspire.Hosting/Dcp/ResourceSnapshotBuilder.cs)、[DcpResourceWatcher.cs](https://github.com/microsoft/aspire/blob/9c1b401dd67746739044f68959cbf4d3d7af93a6/src/Aspire.Hosting/Dcp/DcpResourceWatcher.cs)。該当blobはそれぞれ `5a41bee536b0ee98881cc263cd659c5e901aa5ce`、`949134a43d637b375455a699363fc740384ce2cd`、`e718c574f83d127abca12a6197b4c57509c4d172`。
- 制約も残る。`ContainerStatus` と `DcpInstancesAnnotation` は同package source内でinternal。`container.id` のKnownProperties定数もinternalで、専用の公開 `ContainerId` APIはreflectionで見つからない。公開snapshot側は汎用 `ResourcePropertySnapshot(Name, Value, ...)` を提供するため、13.5.4固定の `Name == "container.id"` と完全ID形式を先行試験で検査すれば実値を読み出せるが、将来版に対する公開契約保証とはしない。`ResourceEvent.Snapshot.Properties` はDCPが実IDを確定した後に更新される値であり、起動前envへの注入元にはならない。今回DCPは起動していないため、実イベント到着、ID値の取得、再作成時のevent順序は未検証。
- 最小の依存順案: I81-06のowner観測を先に（または同時に）行い、ResourceNotification eventの `container.id` とresource instance/event identity、再作成ごとのGeneration、同snapshotのendpointを同一世代として保持する。RuntimeHost起動時に未供給のstrict identityは503のまま維持し、ownerがpost-startに完全4値を送る一時的な認証済みinternal handoffを用意する。受信側がRunId/ResourceId/ContainerId/Generationの妥当性と世代遷移を検査し、次の正常Detectionからreadinessを成立させる。handshake route/authentication/retry/endpoint選択の詳細は未実装・未試験で、実DCPなしに成功としない。既存ResourceInstanceId/DCP resource IDを起動前値に見立てたり、固定/疑似IDを注入したりしない。
- reflection用の初回scratch実行はローカル依存解決不足でexit 1となり、既存framework/NuGet cacheを使うresolverで再実行してexit 0。製品、test、依存、設定は変更していない。sourceコピーはTempの `/mnt/c/Users/donabe/AppData/Local/Temp/issue81-aspire-13.5.4-source-read-20261023/` に限る。



### 06A identity owner の契約整理（NR005の順序依存）

本節は仕様・固定版ソースからの実装案であり、製品変更、DCP実行、実通信の成功を示さない。NR005のstrict healthを緩めず、06Aで実ID観測を先行し、05B1へ起動後に渡す。06Aは固定版通知の読み取りとowner stateを対象にし、受信プロセスとDCP container IDの原子的な対応を証明できる仕組みが決まるまではhandoffの完成・NR005 closureとしない。

#### フィールドの意味を固定

- `RunId` はAppHostプロセス起動ごとに生成する一意値とする。既存 `Testing:StackOwnership:StackId` は設定で上書き可能な所有ラベルなので流用しない。新AppHost起動ではRunIdを新しくし、owner状態・generationを空から開始する。
- プロトコルの `ResourceId` は `ResourceNotificationService.WatchAsync` が返す `ResourceEvent.ResourceId` の完全値（unique resource key）とする。Aspire v13.5.4のsourceはこれをresource state mapのkeyとしてsnapshotと一緒に発行する。`Resource.Name` は別フィールドで、`duck` / `debug-host` 等の許可された役割を選ぶために照合する。name fallbackやreplica曖昧検索を使わず、想定外nameまたは重複候補は拒否する。`ResourceId`をNameやDocker engine `ContainerId`と同一視しない。
- `ContainerId` は同じ `ResourceEvent.Snapshot` の `Properties` から、固定v13.5.4で確認した正確なキー `container.id` と値を取る。64桁hex以外、欠落、別propertyへのfallbackは拒否する。
- `Generation` は各 `(RunId, ResourceId)` のownerが持つ正のInt64。初回の有効なcontainer IDは1、同じIDの再通知は冪等、観測された別IDでのみ増やす。新RunIdでは1から開始する。同一generationでIDが異なるhandoff、generationが現行以下の遅延handoffは拒否する。

#### 観測・endpointの同一性と競合

`WatchAsync` は初期snapshotとresource updateを流すが、固定版 `CustomResourceSnapshot.Version` は `internal long` であり、利用側のpublic APIでは読めない。`WatchAsync` 内部の `versionsSeen` は購読開始時の初期snapshotと並行到着した同一snapshotを除くための一時表で、全updateに対する公開version順序を保証しない。したがってowner独自のarrival sequenceは自プロセス内の処理を直列化する補助に限り、DCP更新の新旧やengine時系列の証明に使わない。処理対象は登録済みIResourceの `ResourceEvent.ResourceId` と `Resource.Name` の組で固定する。状態変化/ID/active endpointの欠落はその時点で観測した状態を無効化できるが、非公開のVersionを仮定したstale-event拒否はできない。

`container.id` とactiveな必要endpoint URLは同じ `ResourceEvent.Snapshot` から採る。endpointはexpected endpoint nameで一つに絞り、inactive/欠落/重複を拒否する。endpoint URLだけでcontainerを選ばない。動的host portが同じ値へ再利用されても、別container IDは新generationであり、旧generationのhandoffや観測は受け入れない。endpointだけの変化は同じIDの同一世代内でowner endpointを更新する。

handoff送信前にownerは自分が観測したID/endpointを現行stateで再検査し、同一resourceへの更新とhandoffを直列化する。再検査により、自分が後から処理したsnapshotを越えて旧eventを送ることは防げる。しかし「最後のsnapshot再読込後、HTTP送信まで」にDCPがcontainerを再作成し、動的endpointを再利用した競合はこの再検査だけでは排除できない。固定Aspire sourceはsnapshot値の発行を示すが、HTTP受信プロセスとcontainer.idの原子的bindingは提供していない。この区間で旧payloadが新processへ届く可能性は未解決として残す。完全なinstance challenge等を候補にする場合も、実際にengine IDと応答processを結び付ける根拠が必要で、endpoint応答だけをID proofとしない。

AppHost owner再起動時は新RunIdを使い、旧watchを取消し、旧token/eventを新ownerに通さない。RuntimeHostが新RunIdの認証情報を持たない状態では503を保つ。旧RuntimeHostが残存するなら新ownerで有効とみなさず、DCPにより新しい設定を持つresourceへ再同期できるまでfail closedとする。初期案の短期capabilityはURL query、Dashboardの平文、通常ログへ出さず、HTTP header等の専用経路で扱う。秘密の注入・Dashboard redactionが固定Aspire APIで成立することは未確認であり、確認できるまで平文環境値へ実装しない。

新identityを受け入れたRuntimeHostは旧診断状態を消去し、同じ新RunId/ResourceId/ContainerId/Generationの後に観測された新しい正常Detectionが来るまでは `/health/vision` を503に保つ。identity欠落、不正、token不一致、別resource、古いgenerationは拒否し、既存strict healthの契約を維持する。これはRuntimeHostが受けたUDP packet自体の送信元engine IDを証明するものではない。既存Detection形式にengine IDが含まれないため、旧containerが再作成後も同じ有効streamを送れる場合、そのpacketを新identityへ帰属できるかは別の未解決境界であり、freshnessだけでpacket ownershipを主張しない。

#### 固定試験の受入値案

1. 固定13.5.4 public API projection: `ResourceEvent.ResourceId`、`Resource.Name`、`Snapshot.Properties` の `container.id`、expected active URLを分離して抽出する。`Snapshot.Version` へpublicアクセスするコード/試験は作らない。resource key/role mismatch、missing/null/short/nonhex ID、inactive/ambiguous URLはhandoffなし。ResourceInstanceId、name、endpointからContainerIdを補完しない。
2. owner state: first complete delivered event → generation 1; duplicate identical payload → idempotent; same ContainerId with changed active endpoint updates endpoint but not container generation; changed full ID rotates generation and revokes prior observation. The owner may serialize its own event handling, but with no public snapshot version it cannot classify a delayed older event as stale. Therefore this fixture verifies observed transitions only and records out-of-order DCP updates as an unmet ordering contract; it must not assert that a local counter rejects them.
3. RuntimeHost receiver: exact token/current RunId/ResourceId/full ContainerId/positive generation only; duplicate tuple idempotent; mismatch, malformed, replay, older generation and same-generation/different-ID rejected. Accepted replacement clears prior receive evidence; health remains 503 until a post-replacement valid Detection. Owner restart with new RunId rejects prior token/event and remains 503 until new owner handoff.
4. Race fixture: hold an old handoff, deliver a replacement event with a different ID but the same endpoint, then release the old request; old payload rejection requires an authoritative event-order/current-process binding. A local sequence or public `WatchAsync` behavior alone is insufficient. Until that binding is supplied and tested, this is an explicit unresolved requirement, not a passing property of event handling.
5. Separate limit test: a packet from a still-live old sender after new identity handoff must not be described as belonging to the new container. If existing packet/source evidence cannot distinguish it, keep the readiness/ownership acceptance unresolved rather than weakening strict health or fabricating an ID.

設計根拠は主設計 `aspire-simulation-test-environment.md` 204–205行の4値とfail-closed、211–224行の照合/新しさ、175行および243行の再作成後の新接続先/新完全ID、比較設計 `tracker-comparison-debug-design.md` 31–42行のDCP単一ownerとcontainer ID/image/run/resource/networkの結合である。Aspire固定ソース `9c1b401dd67746739044f68959cbf4d3d7af93a6` では `Container.cs` 505–520行がContainerStatus.ContainerId、`ResourceSnapshotBuilder.cs` 21–60行が `container.id` property、270–337行が同snapshotのresource URL、`DcpResourceWatcher.cs` 890–924行が更新snapshot通知、`ResourceNotificationService.cs` 761–805行がWatchAsyncの初期snapshot重複除去（継続更新の順序保証ではない）、1440–1457行がResourceEvent.ResourceId/Snapshotを定義する。`CustomResourceSnapshot.cs` の公開surfaceではSnapshot.PropertiesとUrlsを利用し、Versionはinternal。固定ソースの保存先は `reports/evidence/issue81-05b1-identity-source-v13.5.4-20261023/`、`inventory-v1.tsv` SHA-256 `9d7439b50381b3dec677a6ec1b9a850b79ea886097ab50e1db26dfa2808871ba`。これは公開ソースと既存DLL public surfaceのread-only確認であり、DCP通知実行やcontainer/processの実結合確認ではない。

この区切りでは仕様・固定版source・受入案のみ記録し、主/比較設計やproduct/testは変更していない。06Aは実ID観測とowner state、05B1は認証handoffとstrict health受入を担当する案。owner独自arrival sequenceが防ぐ局所的な重複処理と、DCP event orderingおよびsnapshot-read後のprocess/container bindingが未証明な範囲を通常reviewへ分けて渡す。


### 06A 設計契約訂正履歴（通常レビュー指摘）

前版（SHA-256 `2fc6f19d062388dd6b53d8780c46c80efae9a6bf3e1d7287146d15cd4e2e7b64`）には「snapshot versionがresourceごとに増加し」「ownerも最後に処理したversionを記録し、低い/同じversionの異なる内容を拒否する」と記したが、public API検査が不足した実質的な設計誤りだったため、この節と試験期待値を訂正した。固定commit `9c1b401dd67746739044f68959cbf4d3d7af93a6` の `CustomResourceSnapshot.cs`（blob `07dfd322d0994ef99feefd73c415e173a075d800`）では `Version` は `internal long Version { get; init; }`。利用側へ公開されず、reflection/private accessを前提にしない。公開source: https://github.com/microsoft/aspire/blob/9c1b401dd67746739044f68959cbf4d3d7af93a6/src/Aspire.Hosting/ApplicationModel/CustomResourceSnapshot.cs

同commit `ResourceNotificationService.cs` の `WatchAsync` では、`versionsSeen` は購読後に初期snapshotを列挙し、その間にchannelへ届いた同じ/古いinitial eventとの重複を一度除くため使う。比較対象を外した後にkeyを削除するため、継続する全updateのpublic単調sequenceではない。これはDCPやcontainer engineの時系列契約でもない。従って先の「older versionを拒否する」テスト期待値は取り下げ、compile-time/API確認にはpublic `ResourceEvent.ResourceId`, `Resource`, `Snapshot.Properties` 等だけを使う。owner arrival counterは内部処理の直列化に限定し、source freshness、DCP ordering、現containerの証明には用いない。

訂正後も確認済みの事実は維持する: 固定版DCPは `ContainerStatus.ContainerId` を生成snapshotの `container.id` propertyへ載せ、ResourceNotificationServiceはそのsnapshotを通知する。これは作成後のID候補を読む経路であって、各通知の新旧をpublicに比較するAPIではない。snapshot再読込からhandoffまでの再作成/endpoint再利用競合、receiver processとengine IDのbinding、旧containerからのUDP packet識別はいずれも未解決のまま。新testの失敗やcompile errorを機能Redと見なさず、安全なcurrent-owner証明がない間はNR005をclosedにしない。


### 06A1 実装契約とfixture対応（同通常レビューの助言反映）

前節のHTTP capability／nonce handoff案は採用しない。通常レビューで合意した次の単位へ置き換える。AppHostはDCPの完全な `container.id` を照合した後、そのIDのcontainer内で既存 `Tracker.RuntimeHost.dll` を短命control modeとして実行し、同じcontainerだけに存在する固定UDSへ接続させる。host HTTP endpointは宛先選択に使わない。DCPのcreate/start/stop/remove所有は維持する。control modeは実containerへ実行しない。DCPに用いるengine contextとDocker CLI contextの一致は未確定で、既存のauthorityから一致を確認できないときはexecを発行しない。当時の確認は旧Program Files pathとPATH上の実体に限られていた。後続の読取専用確認ではper-user Docker CLI実体とdesktop-linux context設定が見つかったため、旧所見は全ホストでCLIなしという意味ではない（本報告末尾の訂正参照）。

#### 仕様から変更対象と試験へ

| 仕様／境界 | 実装対象 | 先行fixtureと受入値 |
| --- | --- | --- |
| RuntimeHostは通常の `dotnet Tracker.RuntimeHost.dll` 起動を維持。image実ENTRYPOINTはRuntimeHost DLL。 | `Tracker/Tracker.RuntimeHost/Program.cs`、`RuntimeHostCommandLine.cs`、新 `RuntimeIdentityControlCommand.cs` | 通常起動引数はhost生成へ進み、`--identity-control` は短命relayだけを起動してhost lifecycleを重複起動しない。未知/重複/不正control引数は拒否。 |
| DCPが唯一のcontainer lifecycle owner。ResourceIdとResource.Name/roleを分離し、正確な完全 `container.id` のみ対象選択へ使う。engine contextが一致しなければfail closed。 | 新 `Testing/Duck.Testing.AppHost/RuntimeIdentityResourceBinding.cs` と `RuntimeIdentityExecProcess.cs`（実AppHostへの接続時にResourceNotificationService eventを購読） | eventのResourceId・IResource参照・許可role・unique `container.id` を照合。欠落/不正/別resource/role/engine context不一致はprocess runner未呼出し。host URL/port/name fallbackなし。command fixtureは `exec -i <fullId>` と実Dockerfile ENTRYPOINTの `dotnet Tracker.RuntimeHost.dll --identity-control` を確認する。 |
| 同一container内のUDSで、prepare/challenge/commitを同じ接続・同じidentity tupleで行う。 | 新 `Tracker/Tracker.RuntimeHost/RuntimeIdentityControlServer.cs`、`RuntimeIdentityControlSession.cs` | 実Unix domain socketを一時pathでbindしprepare→BootId challenge→expectedBootId commitを一つのconnectionで成功。BootId交換、tuple改変、欠落、切断、旧Run/Resource/世代は拒否。通常app protocolとは独立し、HTTP/TCP host endpointなし。 |
| 1 process起動ごと新BootId。同IDでもBootId変更は新Generation。受理したidentityとreceive epoch/evidenceは同じ同期境界で更新し、新しい正常受信まで503。 | `Testing/Duck.Testing.Diagnostics/ServiceReceiveDiagnostics.cs`、`RuntimeHostServiceCollectionExtensions.cs`、新server | 同ContainerId/newBootがGeneration増加後のみ受理、same tuple retryは冪等。new identity受理で旧freshnessを無効化。既存attemptを新identityへ記録しない。次の有効Detectionまでstrict healthは503、その後契約どおりready。 |
| 欠落/不一致は未帰属・未準備、旧世代/旧bootを拒否。sender packet起源は別契約。 | `RuntimeVisionDiagnosticsHttpService.cs`は既存strict判定を保持（条件緩和なし） | real diagnostics DI→control handoff→GetHealth: absent/invalid/stale 503、accepted identityだけでは503、fresh decoded input後200。外部sender identity/packet sourceはassertしない。 |

今回はまず `Tracker.Tests/RuntimeIdentityControlProtocolTests.cs` と `RuntimeIdentityAppHostBindingTests.cs` を追加し、既存SDKでcompileを成立させた上で、実UDSのRedとresource-to-command fail-closed境界を測る。test helper/mocksはAspire通知のpublic surfaceと実socketを通し、ソケット/JSON契約をmockだけで代用しない。Redが機能assertではなくcompile/setup失敗なら修正まで進め、失敗をRedと数えない。新規依存や既存固定試験のskip/緩和はない。先行固定SHAはreportの別項目で記録してから最小実装する。

既知の未解決境界: DCP固定版の `Snapshot.Version` はinternal、`WatchAsync`に全更新の公開単調version保証はない。ownerローカルseqでengine chronologyを主張しない。完全IDを宛先にすることでhost port再利用競合を避けるが、実Docker engine contextとの一致は別途正規authorityで照合を要する。旧global pathにはDocker CLI実体がなかったが、後続読取でper-user実体とcontext設定を確認した。CLI実行、DCPとのengine一致、exec、実container、UDP、実host port、起動後の実ResourceEventは未実証。テスト結果はこれらの成立へ一般化しない。bounded lease/TTLは固定要件とせず、現要件で必要な再送・取消意味だけをfixture化する。外部senderのpacket identityは今回対象外。

設計同期後の主設計SHA-256: 3437732d3f830cc907ca27cc388f88460772b67c6a69ff38649cb0f0130d554b。比較設計SHA-256: c5513c1a23f40ba77884e1627164ca18b3c32fd2289bcf9a108feb73d4aa09cf。06A通常review report `reports/issue81-bridge-readiness-normal-review-20261023.md` はneeds_changes、NR005/P1 open・NR006/P2 closedの状態を保持する。
### 06A1 残るリスク

- AppHostのイベント→完全engine ID→同一DCP engine contextの確認→exec→常駐RuntimeHost UDS handoffは未実装/未検証。per-user Docker CLI実体は存在し、contextはdesktop-linuxに設定されているが、CLIは実行していない。実Docker Desktop/DCP操作は行っていない。資源内UDS fixture成功は外側owner供給の成立を意味しない。
- 外部container IDの真正な帰属、同snapshot後の再作成競合、旧containerから届くUDP packetの起源は未証明。Strict healthを弱めず未達として保持する。
- Docs gateはtextlintのみpass。CSpell/Sudachiの指摘と全体gate未合格は継続。図レンダーは本人の最終確認なしで合格にしない。

### 06A1 NR007–NR009 境界修正の実測追補（2026-10-23）

- 追加境界試験 Tracker/Tracker.Tests/RuntimeIdentityControlBoundaryTests.cs は SHA-256 58c304d7d96c7812a816bd8407c316f1ceb58f22539a1572718446f714ab8993。実UDSを使う11ケースを含み、既存プロトコル試験 RuntimeIdentityControlProtocolTests.cs は既記録SHA a9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06 を維持。
- NR007: prepare/commit不正JSONおよびchallenge後disconnectで、AcceptLoopが後続接続を処理できないRedを個別filterで取得し、境界処理追加後に各filter exit 0。NR008: 他所有者のsocket pathを消すRedを取得後、ソケット所有フラグで自分がBind成功したpathだけを削除し、socketと通常ファイル保護/自socket cleanupの試験がGreen。NR009: idle prepare/commitで後続接続を塞ぐRedを取得し、1秒のstage deadline、64 Ki文字frame上限、relayの30秒read deadlineとcancel伝播後にGreen。64 Kiは文字数上限でありbyte上限とは主張しない。
- 先行Redの証拠は reports/evidence/issue81-05b-boundary-tests-20261023/ の malformed-prepare-v1、theory-commit-v3、theory-disconnect-v1、owner-socket-v1、idleclient-red-post007、idlecommit-v2。oversized-server の直接Redは実行履歴にはあるが当時のrunner rawがなく、このbundleではRed rawを保持していない。各初期結果を網羅的な個別Red証拠とは扱わない。fixtureの不備・compile error・重複process履歴も同bundle内に残し、Redと混同しない。
- 実装では RuntimeIdentityControlProtocol.cs に接続ごとのJSON/IO/Socket/ObjectDisposed例外隔離、Bind成功時のみsocket path所有、bounded framing/deadlineを追加。Program.cs のrelayはCtrl+C CancellationTokenを伝搬。現SHAはそれぞれ 5a983432fa7d860eb6421587fbfa76710d9170a2e27bb588b8fb97737f586b37 と 4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117b。新boundary 11/11、無filterの dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=false は797/797、skip 0、exit 0。RuntimeHostとTesting AppHostの同じno-restore buildは各exit 0。以前の --filter FullyQualifiedName~Tracker.Tests 結果730/730はsubset結果として保持し、全suiteとは呼ばない。無filter結果は同bundleの full-suite-unfiltered-v1.*。AppHost buildに既存ASPIRE010 warning 1件。
- 対象bundle reports/evidence/issue81-05b-boundary-tests-20261023/ はcommand/stdout/stderr/recordを世代別保存。verification-summary.md と inventory-v1.tsv はこの追補と全rawに対応。旧重複test processは終了を確認したが、元tool sessionへexitを回収できず、process終了とexit未回収を区別して保持する。現在この作業で起動したtest/build processはすべて終端。
- Docker CLI所見訂正: 旧global path C:\Program Files\Docker\Docker\resources\bin\docker.exe は不在、PATH上にもなく、per-user C:\Users\donabe\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe は存在する。読取専用 docker context show/inspect はexit 0でcontext desktop-linux、endpoint npipe:////./pipe/dockerDesktopLinuxEngine、DOCKER_HOST/DOCKER_CONTEXT overrideなしを確認した（記録: C:\Users\donabe\Documents\Codex\2026-10-22\task-17\docker-cli-existing-context-readonly.json）。これはCLI/context設定の証拠であり、DCPが同じengineを選ぶことやexec/コンテナ/UDP成立の証拠ではない。Docker CLI実行による接続・DCP操作は未実施。


### 05B1 NR007/NR009 liveness修正の実測追補（2026-10-23）

通常reviewの最新結果 `reports/issue81-bridge-readiness-normal-review-20261023.md` SHA-256 `02cc826646b0d380134adba4ec820f89b02565410d0d54b7f2bb5ff07efb3b28` は、NR008をclosed、NR007（受付taskの監視/finally cleanup）とNR009（外部取消なしの書込/出力/flushの有限終端）をopenとしている。NR005/P1も別境界でopenのまま。

製品変更前に新しい別fixture `Tracker/Tracker.Tests/RuntimeIdentityControlLivenessTests.cs` を作り、固定期待値から実行時Redを測定した。最終試験SHA-256は `2d136c3d9ad3320c3fcf61fba38e88da56ede44bd309c637b05643b152731745`。既存 `RuntimeIdentityControlBoundaryTests.cs` はSHA `58c304d7d96c7812a816bd8407c316f1ceb58f22539a1572718446f714ab8993`、`Program.cs` は `4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117b` のまま。

- NR007 direct cleanup Red `fatal-cleanup-red-v1`: test build成立後exit 1。listenerを閉じてAcceptLoopをfaultさせた際、`DisposeAsync` は元のSocketExceptionを伝播した。変更前sourceはaccept taskをawaitしてからsocket pathを消す順序だったため、fault時にcleanup行へ到達しない。試験は例外伝播のRedを記録し、cleanup結果は同process内で収集したが失敗assertの先行でraw出力には個別値を出していない。Green `server-cleanup-green-v1` は元例外の伝播を許容しつつ、fault後も自所有socket pathが削除されることを1/1で確認。
- NR007 HostedService監視Red `hosted-supervision-red-v2`: test/API scaffold後、監視挙動を実装する前にexit 1。AcceptLoopをfaultさせてもHostedServiceの `ExecuteTask` が1秒完了せず、shutdownを要求するまで待機した。初回のfixture不備（async state-machine反射およびruntime Task実型を厳密比較）はRedに数えずrawを分離。Green `hosted-supervision-green-v1` は1/1でfatal completionがhosted taskへ伝播し、shutdown後もsocket path cleanupを確認。
- NR009 Red `relay-output-red-v2`: cancellation fallbackを尊重するfake writerへ修正した後、外部取消を行わない3秒観測でblocked `WriteLineAsync` と `FlushAsync` の2ケースがexit 1。最初のv1はfakeがcleanup時のCancellationTokenを無視してtimeoutしたsetup/harness不備として保持し、機能Redに使わない。Green `relay-output-green-v1` は2/2。connect、各relay/output書込、output flushへ有限operation deadlineを適用し、テストは短いdeadlineを注入して終端と後続relayの回復を確認する。呼出し側CancellationTokenは従来どおり伝播する。

最小製品変更は `RuntimeIdentityControlProtocol.cs` に限定。accept taskの `Completion` を公開し、HostedServiceは無期限Delayでなく完了taskをawaitする。DisposeAsyncはaccept taskのfaultを保持しつつfinallyで自所有socketを削除し、必要時に元例外を再送出する。relayはconnect/write/output/flushに共通の有限deadlineを持たせた。TextWriter実装自身が取消不能の内部処理を持つ場合、その基底処理の停止までは保証せずrelay側の待機を有界化する。

最終結果は新liveness suite 4/4、既存boundary suite 11/11、無filter全体 `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=false` 801/801、skip 0、exit 0。RuntimeHost no-restore build exit 0（warning/error 0）、Testing AppHost no-restore build exit 0（既存ASPIRE010 warning 1、error 0）。各command/stdout/stderr/exitとsource hashesは `reports/evidence/issue81-05b-liveness-tests-20261023/` に世代別で保持し、`manifest-v1.tsv` SHA-256 `f2477881d2e5d11d47939af704eea045cbaa0a6ae5b6cecde5e6823fe6134421`、要約 `verification-summary.md` SHA-256 `5b6e1b2dc41c4bdf13216be9ea2d8f41a1643295979f866a4b5f558d1c622438`。以前の797/797は変更前sourceの履歴として別世代のまま。

既存の読取専用Docker情報は `docker-cli-existing-context-readonly.json` と `docker-cli-existing-engine-readonly.json` にある。既存 `desktop-linux` contextのendpointは `npipe:////./pipe/dockerDesktopLinuxEngine`。親が保存したread-only `docker info` の出力はEngineID `5bc5ab8a-7340-47ba-8b7c-9f645375a54c`、OS `Docker Desktop`、ServerVersion `29.9.1`。これはdaemon metadataだけであり、DCPが同engineを選ぶ証拠ではない。GUI版へ換算せず、container create/pull/start/exec/stop/removeは一切行っていない。

この区切りの全test/build processは終端しexitを回収済み。既存process auditに記録した古い重複testの元tool exit未回収は監査gapとして残す。前に記録した `PD-I81-06A1-TDD` は履歴として維持し、今回の新liveness fixtureを過去5件目の代替Redとして遡及しない。未実施: PR/CI、実Docker/DCP/exec、NR005の実engine identity受渡し。通常reviewerへこのsource/hashの限定fix確認を依頼する段階。


### NR009 非協力I/O deadline fixture追補（2026-10-23）

新規fixture `Tracker/Tracker.Tests/RuntimeIdentityControlNonCooperativeLivenessTests.cs`（SHA-256 `c8251b75203047b8555d0c649a3d0b4b148e9778e5fc3c4a2535a83671a7c543`）は、同期blockするRead/Write/FlushとCancellationTokenを無視する非同期Read/Flushを個別に測る。Red `noncooperative-io-red-v1` は修正前Protocol.cs SHA `b96d255bc7e52fd017bcc9676df994fc5c2331575b1d14cec281d27de7bf67cf`、4/4失敗、exit 1。操作はいずれも注入deadline後も完了せず、fixtureが待機workerを解放した。Green `noncooperative-io-green-v1` はProtocol.cs SHA `44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18f`、4/4、exit 0。新helperは操作本体と同期prefixを別taskとして実行し、deadline完了を待つ呼出し側を有限化する。これはローカルfilter結果であり、このsourceの全suite/buildやCI成功とはしない。

直接cleanup Redの説明を限定する。`fatal-cleanup-red-v1` のrawで独立に測れたのは `DisposeAsync` が元のSocketExceptionを伝播することまでで、失敗assertの前にsocket pathの観測値は出力されていない。旧ソースはfaultしたaccept taskをawaitしてからcleanupする順序だったためcleanupへ到達しないリスクはコードから分かるが、Red rawだけをcleanup不実行の独立測定とは扱わない。Green `server-cleanup-green-v1` は元例外の伝播を許容し、自所有pathのcleanupを確認した。期待値訂正前後を同一の完全Red→Greenとは記述せず、別期待値fixtureの履歴を保持する。

この補追の証拠世代は `reports/evidence/issue81-05b-liveness-tests-20261023/verification-summary-v2.md` および `manifest-v2.tsv`。既存manifest-v1、全raw、metadata companionは変更していない。前の `full-unfiltered-green-v1` 801/801および両buildは、新しい4case fixture/最新helperより前のsource世代であるため、本sourceの全suite/build検証として流用しない。


### 44d8dc7 現行sourceの限定検証追補（2026-10-23）

この追補は、正式通常reviewが対象とした旧source `b96d255bc7e52fd017bcc9676df994fc5c2331575b1d14cec281d27de7bf67cf` と、今回ローカル検証したsource `44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18f` を区別する。最新sourceの非協力I/O Red/Greenは各々 `noncooperative-io-red-v1` / `noncooperative-io-green-v1` にある。現在版で追加確認した限定試験はprotocol 5/5、boundary 11/11、liveness 4/4、noncooperative liveness 4/4、全てskip 0・exit 0。無filter full suiteは805/805・skip 0・exit 0。RuntimeHost no-restore buildは0 warning/0 error・exit 0、Testing AppHost no-restore buildは既存ASPIRE010 warning 1・error 0・exit 0。全て `-p:EnableSourceControlManagerQueries=false` を付け、既存restore/cacheのみで実行。各command/stdout/stderr/source fingerprintは証拠generation v3に保存する。

ここでの805/805と両buildはローカルの現在dirty sourceの証拠で、Git commit、PR CI、実Docker/DCP/exec/コンテナ起動の証拠ではない。正式review結果は旧source対象とし、このcurrent-source検証結果で旧review報告を上書きしない。外部のDocker daemon/DCP engine一致とNR005の完全ID供給は未確認。既存process auditの時刻表記は原本がtimezoneを示さないためUTCへ換算せず、過去の未回収exit gapは維持する。

本追補時点で、source SHAはProtocol `44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18f`、Program `4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117b`、protocol tests `a9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06`、boundary tests `58c304d7d96c7812a816bd8407c316f1ceb58f22539a1572718446f714ab8993`、liveness tests `2d136c3d9ad3320c3fcf61fba38e88da56ede44bd309c637b05643b152731745`、noncooperative tests `c8251b75203047b8555d0c649a3d0b4b148e9778e5fc3c4a2535a83671a7c543`。Red/Greenの差、旧process audit gap、metadata companionは別ファイルを保持し、旧世代を改稿していない。


### 2a6fd94 設計語彙差分の限定検査（2026-10-23）

対象はbaseline `9284640aaec4af59e44afe7687e4bf8a39494c48` とcurrent `2a6fd94455978021cd995104751f4f09b0a28dfd` の設計追記だけ。実diffは主設計12行、比較設計4行。既存Node/cache/Junction/Pythonと同じ9設定hashを使い、入力byteとmodule解決を実行前に確認した。通常文の `owner`、`role`、`container` 等を日本語へ直し、規約上必要な正式API名・protocol field/step名・Unix socket技術名は保持した。全文の文書gate合格とはしない。

- 主設計のbaseline/current SHA-256: `15205d3b20221336368ea5b2f3375f88e6cc08b22e62371e1f4edb9e80d413d9` / `d50a7a8b2b4fa6ff7c18fe49d69c7fac22912ae1b11411b28cfa8b9da7fb716c`。比較設計: `c1bfa61d4a5267bf4ac429791dbf19ad5c71e247052a461105eafdd69036cc85` / `b983590bb17c4cdedcd7144a3bfb202cef73aa66a4a5dc7bedc1d3b45937993f`。
- CSpellは既存wrapperで全文を各1ファイルずつ実行。主設計 baseline 1件/current 7件で追加6語 `Unix`, `Resource`, `Container`, `Generation`, `Boot`, `challenge`。比較設計 baseline 43/current 45で追加5語 `Unix`, `Resource`, `Container`, `Generation`, `Boot`、削除3語 `alignment`, `cursor`, `timeline`。追加は正式API/識別フィールド/プロトコル段階・ソケット技術用語で、成功扱いせず原文rawを保持。
- Sudachiは全文同時入力で既存baseline違反が上限表示を越えるため、同じ設定のまま実diff追加行だけを別入力にして全件出力を取得した。残った新指摘は `RunId`, `ResourceId`, `ContainerId`, `Generation`, `BootId`, `challenge`, `Unix` と `Generation`/`challenge` のカタカナ判定。正式field/protocol/transport名として必要な用語であり、辞書・whitelistは変更していない。一般説明のエンジン/ドメイン/リセット/データグラム/コード表現は日本語へ修正。
- 全文CSpellと追加行Sudachiは各exit 1。これは未登録語を含む正確な結果で、全体gateの合格ではない。textlint/Yomiyasuは今回対象外。既存baselineの不合格と追加行の指摘を区別した。
- 生の旧比較結果/失敗、段階ごとの修正後raw、command/config/source hashを `reports/evidence/issue81-2a-design-vocabulary-20261023/` に世代別保存。最終確認metadataは `closure-final-v5.json`。`manifest-v1.tsv` はこのbundleの全filesを列挙し、manifest自身を除外する。


### NR009 遅延fault観測・短命制御processの限定修正（2026-10-23）

通常reviewのNR009第3回残actionに対し、別fixture `Tracker/Tracker.Tests/RuntimeIdentityControlOperationLifetimeTests.cs` を追加した。製品変更前の有効な Red は `red-v2` で、Protocol SHA `44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18f`、試験SHA `30a22fe1c8031f36ea8aec66951555999e0efa7f74d9986fc71dae95f1f9005d`。3件中2件pass/1件fail、exit 1: 期限切れ後にtaskが投げた例外が `UnobservedTaskException` として観測され、固定assertが失敗した。実UDSの所有ソケット回復試験はpass。修正後 `green-v1` は Protocol SHA `94bcc52414e0802cf049f80427145d663825f801a11d5f88e0ecf0eb2fd57e21`、同試験SHAのまま3/3 pass、skip 0、exit 0。

最小修正は `RuntimeIdentityControlProtocol.cs` の `RunWithDeadlineAsync` で、成功完了でないtaskへ `OnlyOnFaulted` continuation を登録し、期限後に遅れて完了したfaultも観測する。期限後の任意IOを強制停止する保証は追加していない。短命CLI processの上限確認は、既存ビルド済み `Tracker/Tracker.RuntimeHost/bin/Debug/net10.0/Tracker.RuntimeHost.dll` と同位置の `.runtimeconfig.json` / `.deps.json` を使う。標準入力を閉じず30秒待った後に既定期限で終了し、期待終了コード2を確認する専用process fixtureは1/1 pass。先の test-bin コピーを使った試行はMicrosoft.Extensions.Hosting解決失敗で子process exit 134だったためsetup失敗として分離し、product Redに数えない。実プロジェクト出力へ切替えた試行では解消した。

source/hashの訂正: 直前の記録で「exact source 44d8」とした記述は修正前sourceとの混同で誤り。Red-v2が44d8、Green/full/buildは修正後 `94bcc52414e0802cf049f80427145d663825f801a11d5f88e0ecf0eb2fd57e21` を実測した。Green/full/build command recordsには実行直前source hashを保存し、Protocol SHAは相互一致する。Program.cs `4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117b`、試験 `30a22fe1c8031f36ea8aec66951555999e0efa7f74d9986fc71dae95f1f9005d`。RuntimeHost生成DLL SHA `8e7d3cde7bf0d46e2984989bbb64f8642232542f1e36c8af031d1bcfbf715232`。

修正後の無filter標準suiteは808/808、skip 0、exit 0。RuntimeHost no-restore build exit 0、warning/error 0。Testing AppHost no-restore build exit 0、error 0、既存ASPIRE010 warning 1。全て `-p:EnableSourceControlManagerQueries=false`、既存restore/cacheで実行。Red/Green/子process診断/full suite/両buildのcommand、raw stdout/stderr、exitとsource hashesは `reports/evidence/issue81-05b-nr009-lifetime-20261023/` に世代別保存した。`manifest-nr009-close-v1.tsv` が同bundle内全証拠ファイルを列挙する（manifest自身は自己hash列挙から除外）。すべての本試験/build processは終端しexitを回収済み。実Docker/DCP/execは未実施。Git commit/push、CI、通常review後のclose判定は親工程。

この報告以前の `44d8` 時点の805/805/両build記録は、その当時のsource検証として残す。現在版の808と混ぜず、通常review reportの旧target判定を変更しない。設計語彙差分の限定検査は前段の記録どおりCSpell/Sudachi指摘が残り、全文gate成功とはしない。`engineID` の誤称は追補前に「コンテナID」へ修正済み。


### 94bcc524 tracking差分の限定文書検査（2026-10-23）

baseline `2a6fd94455978021cd995104751f4f09b0a28dfd` と現行差分から、tasks-status.md line 43 と phases-status.md line 26 の変更2本文行だけを抽出し、追加文がbaselineに無いこと、入力byte/source hashを検査前にassertした。既存Windows Node/cache Junction（node_modulesがissue80既存cacheを指す）と既存Sudachi Python環境を再利用。9設定/whitelist hashはbaseline検査bundleと完全一致、Nodeのtextlint/cspell/yaml module解決exit 0。初回入力のSudachiはtasks文「3アプリ」の `アプリ` を未登録語としてexit 1で指摘。親が所有行を「3つの管理プログラム」へ直した後、最終2入力hash `8da1adf253aa80a7e393d583f17bf9f8e9850513bb4620de4ceae3ffee481060` (tasks) と `ee897c1889ebf6c9905a4c177a3edfb52733a1076ba3a624299c747b5f2cd3ba` (phases) を使って再検査し、textlint/CSpell/Sudachiすべてexit 0。whitelist/辞書/依存は変更していない。全文gateの再実行ではない。

この実装追補と限定文書raw/入力/preflightは `reports/evidence/issue81-05b-nr009-lifetime-20261023/` に統合。初回Sudachi指摘は上書きせず `tracking-lint-v1`、修正後全0は `tracking-lint-v2`、全raw/metadataは `manifest-nr009-close-v2.tsv` に世代別で保持する。


### 06A2 public resource-event projection (2026-10-23)

- Added `Testing/Duck.Testing.AppHost/RegisteredContainerEventProjection.cs` and `Tracker/Tracker.Tests/RegisteredContainerEventProjectionTests.cs`. Final SHA-256: projector `3449ae230e73f6dc0315ea4658c9eee22d9e3994613dc85edbcff13e158e1f3d`; tests `33493d7830dcf88bc375fc1162cb59848c245cccb24e2f52192c159ee9ba8f94`.
- The pure projection keeps the three identifiers separate: the registered/event key, role from `IResource.Name`, and runtime `container.id`. It accepts only matching registered resource references and keys, matching role, Running observation and current snapshot, and exactly one full 64-hex `container.id` property with the same value in both snapshots. Missing, malformed, duplicate, non-string, non-running, other-resource/key, stale-ID, and replacement cases are fixed in 10 focused test cases.
- Fixed Aspire v13.5.4 source is commit `9c1b401dd67746739044f68959cbf4d3d7af93a6`. Existing source says snapshot `container.id` is copied from `ContainerStatus.ContainerId`; `KnownProperties` is not public, so production uses the literal public property key. Runtime reflection against the already-built test assembly confirmed `ResourceEvent`, `CustomResourceSnapshot`, `ResourcePropertySnapshot`, `IResource`, and `ContainerResource` are public; `ResourceStateSnapshot.Text` is public; `Snapshot.Version` is not public. No event-order guarantee or engine/owner authority is inferred.
- TDD chronology is explicit: the first fixture compile failed because required `ResourceType` was omitted; that was setup, not Red. A later pair of failures came from naming `ContainerResource` as the resource key while expecting a distinct role; fixed source shows `Resource.Name` is the role selector, so that was an invalid fixture and not Red. The implementation had already been written before the corrected role fixture. A subsequent compileable false-scaffold sensitivity run on the corrected test produced 8/10 pass and 2 positive-assert failures (exit 1), but it was reconstructed after implementation and is not claimed as a pre-implementation Red. The corrected 10/10 Green and successful build are valid; no expectation was relaxed. This leaves an honest test-first evidence gap rather than a waived exception.
- Validation: corrected focused test `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=false --filter FullyQualifiedName~RegisteredContainerEventProjectionTests --logger console;verbosity=normal` passed 10/10, exit 0. `dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore -p:EnableSourceControlManagerQueries=false` succeeded, exit 0 (one existing ASPIRE010 warning). Existing `AppHostApplicationModelTests` passed 25/25, exit 0. All use existing restore assets; no package or environment change.
- Raw command/stdout/stderr, source hashes, reconstructed scaffold, corrected source snapshots, and API reflection are in `reports/evidence/issue81-06a2-event-projection-20261023/`. `inventory-v1.tsv` enumerates 20 files; inventory SHA-256 `94b36b623d050cc72ba2ff042c77a81a209d49d70fc00de335d3bdc31ddded38`. Earlier 06A2 setup/invalid-fixture observations are described but their raw outputs were not saved.
- This unit only projects a candidate from public resource snapshots. It does not subscribe to AppHost events, establish chronological freshness, prove DCP/engine ownership, execute a command, or validate a real container. Those outer connections remain unverified; same-ID process restart is still governed by the separate BootId contract.



### 06A2 tracking行の限定検査とDCP接続前read-only確認（2026-10-23）

親所有 Tracker/Design/tasks-status.md の追加1本文行だけをbaseline 26c9e8655c0011a0e53628f7291ba86703db5895 と現行差分から抽出し、1行/756 byteであること、現sourceとlint入力のbyte一致、baselineにI81-06A2行が無いことを検査前にassertした。入力SHA-256は 59d5e6f05bac4244bd4630e93e78e51da0b2dd71097c2484f6a74e3df1913de7。既存Windows Node/cache Junctionと既存Sudachi Python、9個の設定hashは直前の成功経路と一致し、Node module解決はexit 0。限定textlint/CSpell/Sudachiはいずれもexit 0、stdout/stderr 0 byte。これはこの1行の検査であり、全文文書gateの結果ではない。command、raw、入力片、preflightはこのbundleの tracking-lint-v1/ と tracking-preflight-v1.json に保存した。

DCP接続へ向けた読み取りでは、Windowsの既存Docker CLIは desktop-linux context、endpoint npipe:////./pipe/dockerDesktopLinuxEngine を返し、既存daemonのread-only info はEngine ID 5bc5ab8a-7340-47ba-8b7c-9f645375a54c、Docker Desktop、ServerVersion 29.9.1 を返す。別に、AppHostから docker を実行する現在のWSL環境では /usr/bin/docker が解決され、WSL CLIのdefault context endpointは unix:///var/run/docker.sock、DOCKER_HOST/DOCKER_CONTEXT overrideは無い。したがってWindows CLIのcontext/Engine IDをDCPが選ぶengineと同一視できない。AppHost/DCPの実選択は未確認。Windows desktop-linuxのread-only image listでは固定外部image 4件を確認したが、Duck RuntimeHostの公開済みruntime imageは確認できなかった。RuntimeHostのRelease publish DLL/runtimeconfigとAppHost Release publish DLLもworktreeに存在せず、RuntimeHost Dockerfileとpublish設定、AppHost Debug DLL、image lockは存在する。

Standalone RuntimeHostだけではAppHostの ResourceEvent は発生しないため、候補eventの実測にはAppHost/DCP lifecycleが必要。現状のAppHost normal経路はRuntimeHostに加えて複数のwrapper資源を登録する。よって次の正常系に必要な差分は、まずDCPが実際に使うDocker executable/context/endpointの確認と、既存Publish→Duck runtime image→AppHost lifecycleの実行可能な資材経路の確認である。今回はdaemon設定変更、image pull/build、container create/start/exec、AppHost起動を行っていない。既存CLI表示はDCP選択・runtime image存在・実通信成立の証拠ではない。

Read-only evidence: dcp-readonly/ (Windows desktop-linux image list command/raw, WSL context command/raw, local asset stat/hash); existing parent observations remain in workspace docker-cli-existing-context-readonly.json and docker-cli-existing-engine-readonly.json. 06A2 source/test/build validation is unchanged from the preceding section.


### 06A3 local prepare/challenge/commit connection (2026-10-23)

The explicit Desktop Bridge path now initiates the same control protocol that the container relay awaits. `ContainerIdentityControlExecSession` writes and flushes `prepare` to the child stdin, reads its `challenge` and `BootId` from stdout, then writes and flushes `commit` with the same RunId/ResourceId/ContainerId/generation and `ExpectedBootId`, and reads the accepted result. Docker calls remain structured arguments against the explicitly configured context and full container ID; current engine ID, Running state, run-id label, and role label are checked before exec. DCP remains the lifecycle owner. Generation begins at 1, advances after an observed non-running transition or changed full ID, and a BootId change returns before commit, closes that child, and retries with the next generation. A repeated same tuple and BootId remains idempotent in RuntimeHost.

The initial pre-product attempts are kept separate: the first focused run failed to compile because the AppHost lacked a project reference to the existing `Duck.Testing.Diagnostics` project; that reference was added without a package/dependency change. The next compileable false-scaffold run failed its runtime `Assert.True` (expected accepted, actual false) before any implementation and is saved as the pre-product unimplemented-session Red. That run did not reach the payload assertions, so it is not evidence that the final handshake assertions themselves failed against the old runner. An intermediate concurrent fake-server harness timed out waiting for the challenge; subsequent deterministic sequential duplex test setup replaced it. The final test drives the production client through both TextReader/TextWriter directions and checks the complete message order and unchanged tuple. This chronology does not claim a pre-product Red for the exact final payload-level assertions.

The final focused run passed 7/7 (`ContainerIdentityControlExecSessionTests` and `RegisteredContainerExecHandoffTests`), exit 0. It includes prepare/commit ordering, BootId-change no-commit, generation+1 retry, explicit engine/full-ID/owner checks, and opt-in AppHost registration. `AppHostApplicationModelTests` passed 25/25, exit 0. RuntimeHost no-restore build passed with 0 warnings/errors; AppHost no-restore build passed with 0 errors and the existing ASPIRE010 warning. `git diff --check` exit 0. Commands, raw stdout/stderr, exit records, and before/after source hashes are in `reports/evidence/issue81-06a3-handshake-completion-20261023/` and referenced generations; `manifest-v1.tsv` has 58 entries (excluding itself).

This validates the in-process duplex protocol client, wrapper call path compilation, AppHost model registration, and local project builds only. No Docker exec, DCP-selected engine, actual container, UDP receive/readiness, or container restart was exercised. The local Docker/Desktop ownership boundary and real DCP pairing remain unmeasured, so NR005 stays open; actual E2E connection remains pending the image/runtime execution path.

### 06A3 handshake/raw chronology and focused document checks (2026-10-23)

The evidence history is kept by generation. The initial 06A2 bootstrap tool outputs were not captured as byte-exact raw files; the earlier summary is derived, not raw. `RuntimeIdentityBootstrapTests` was later corrected to pass the expected role argument; its corrected source hash differs from the earlier test hash, so the earlier result is not attributed to the corrected test. For 06A3, the first run was a compile/setup failure caused by the missing existing Diagnostics project reference. After adding that project reference, the compileable false-scaffold run produced a runtime Red (`Assert.True`, expected true/actual false) before payload assertions. It is not a Red for the exact final handshake assertions. Concurrent fake-responder timeouts and DTO/compile failures remain harness/setup outcomes. The final production-client duplex run is `handshake-integration-v2` (7/7); the direct AppHost model run is 25/25; RuntimeHost and AppHost builds exit 0.

Focused lint used the existing v4 mirror at `/mnt/c/Users/donabe/AppData/Local/Temp/issue81-04b2-status-fragments-mirror-v4`, its existing `node_modules` junction to `/mnt/c/Users/donabe/AppData/Local/Temp/issue80-mdlint-20261022-a1f4c7/node_modules`, the existing Node runtime, and the existing Sudachi Python. Preflight confirmed the junction and resolved `textlint`; the eight config hashes and source hashes are recorded in `preflight.json`. Inputs were extracted from baseline `a2dd3584578d88499c3e52fc0d9d97db164cc5c5` to the then-uncommitted working tree (the four source hashes match commit `a5672531162da9d398b6348746933d0321a58dc7`) at the four canonical Markdown paths: tasks 3 added/2 deleted lines, phases 1/1, simulation design 6/4, comparison design 1/1. Additions and deletion baseline fragments were checked separately with one explicit four-path `--files` set. Current-fragment exits were textlint 0, CSpell 1 (44 findings), Sudachi 1 (83 findings); deletion-fragment exits were textlint 0, CSpell 1 (13), Sudachi 1 (22). Raw stdout/stderr and command/input hashes are in `reports/evidence/issue81-06a3-final-diff-lint-20261023/`. The findings remain failures; deleted-fragment counts are comparison data, not proof that all current terms were previously present. This was a diff-fragment check, not a full-document gate.

The focused evidence inventory is `reports/evidence/issue81-06a3-final-diff-lint-20261023/inventory-v1.tsv` (43 entries; SHA-256 `7cbe79440bff3646ea0b3a2273ef7080e10ac3e94749bc68f009735a2e1d06b5`; all entries reverified: True). The separate handshake bundle remains `reports/evidence/issue81-06a3-handshake-completion-20261023/manifest-v1.tsv` (58 entries; SHA-256 `815add3c6eeceec3c837fa943fc67061cc087491339f61c635530f2b834eb886`). Final implementation/test source hashes are recorded in `final-summary.json` in the focused bundle. Actual Docker/DCP/exec, container UDP/readiness, and full end-to-end success remain unmeasured.


### 06A3 NR-010 current resource key fix (2026-10-23)

- Exact baseline `a5672531162da9d398b6348746933d0321a58dc7` CI run `39985949526` completed successfully: 827/827 standard tests passed; Linux publish validation passed 16/16 checks for all three managed programs. Packet-flow and full-stack jobs were skipped. The log is in the task workspace at `issue81-a567-39985949526-run.log` (SHA-256 `87693D8D7308EDD285E537CBDBF3FDADCF5BED8D0491C578F46DF93CF31B7D34`). This is the pre-fix baseline, not validation of the current uncommitted fix.
- The same baseline source was materialized in the isolated native Windows Temp mirror using the existing NuGet package cache only. AppHost restore, AppHost build, and RuntimeHost `LinuxContainer` profile publish exited 0; the publish output included `Tracker.RuntimeHost.dll`, `.runtimeconfig.json`, `.deps.json`, `SslProto.dll`, and `appsettings.json`. After the NR-010 source change, the AppHost no-restore build also exited 0 (existing ASPIRE010 warning, zero errors). No Docker image operation, DCP operation, or container start occurred. Commands and raw output are recorded under the evidence bundle below; the mirror and generated output remain under `C:\Users\donabe\AppData\Local\Temp\issue81-native-win-a567253-20261023-v3`.
- NR-010 runtime Red used fixed test source SHA-256 `be11812e9dd0f8645bbde95415f38af7235940ecc5efd15e176d7c4b3a2915d8` against the exact a567 product source SHA-256 `e80df7533348fb96f7c7ce2a6db56bb4683a5d6f26ac54260f4cf59ba28be747`. It published a Starting observation under resource key A, then a Running observation under key B with a valid full container ID. The test compiled and ran but failed because no handoff was recorded (1 failed, exit 1). This is a runtime Red for the stale-key branch; it does not establish how frequently DCP emits this event sequence.
- The fix removes the initial-event `resourceId` pin. Each observation's `ResourceId` is now used for current-state lookup and projection; handoff proceeds only after the event/current snapshot projects as a valid Running target. It does not infer ordering or claim actual DCP event behavior. The final product source SHA-256 is `3efc24a224ace87f88065060ec36b467c0f695d41e324feed0bafed8678169a9`.
- Against that final source, the `FullyQualifiedName~ContainerIdentity` filter passed 3/3 tests (including the new NR-010 fixture), 0 skipped, exit 0; the native AppHost no-restore build exited 0. The dedicated focused test also passed once on the immediately preceding minimal source variant `c3bd6b059a46e9d05a27755e05b74a80aad92e7c31052b5f5a87e5107561f329`; that intermediate variant only retained an unnecessary local assignment, which was then removed. The final 3/3 run is the verification for the final source.
- Evidence bundle: `reports/evidence/issue81-nr010-current-resource-key-20261023/`, `manifest-v1.tsv` SHA-256 `5140cb83b1f14ff93deaff5c93ee1b1b35a8d20aaaa18ad05cc1d21f397605ea` (23 entries, excluding the manifest itself). It contains the fixed test source, source patch, local restore/test/build command JSON, stdout/stderr, and explicit exit files. The final fix has not yet received exact-head PR CI or normal review. No new CI run was started; actual DCP behavior remains unmeasured.


### 05B2 明示 Vision simulator の標準コンテナ構成（2026-10-23）

- 明示設定 `Testing:VisionSimulationOnly=true` のときだけ、`simulator` と `duck` の2つを標準 `ContainerResource` として登録する分岐を追加した。`RuntimeHostOnly` の1資源経路と既定 base/comparison 経路は維持する。simulator は固定参照 `ghcr.io/ibis-ssl/framework-simulatorcli` と index SHA-256 `f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79` を使う。image lockのtag `a52b6bd` と不一致のCLI値は拒否する。UDP endpointをホストへ公開せず、host networkも使わない。Duckは既存 strict `/health/vision`、target 39108、dynamic host port、127.0.0.1 bindを持ち、simulatorの開始後に起動する。
- 初回Redは製品分岐追加前の固定test SHA-256 `af85af1faa0040a38c47a6469a0affea1e0ed54d556cdf9b9c6d3eb4e7468243`。実行時に2資源を期待して5資源が観測され exit 1。製品 `Program.cs` の事前SHAは `b5e0faef05aebc91452168e3693e2b5dd9ec2a17c3f1c672de4c85e75dd173a9`。これは有効な製品前Red。
- 製品追加後のv2失敗はtest側が `WithImageSHA256` 後もtagが残ると期待したためで、実モデルでは `ContainerImageAnnotation.Image="ghcr.io/ibis-ssl/framework-simulatorcli"`, `Registry=null`, `SHA256=<fixed index digest>`, `Tag=null`。固定repository/digestは維持し、tag nullを検査するよう訂正した。
- v3/v4は存在しない `ContainerEntrypointAnnotation` をreflectionで探す試験側の仮定でexit 1となった。保存ログの実モデル一覧は `ContainerImageAnnotation` と `CommandLineArgsCallbackAnnotation` の2つで、実際に失敗したのは該当annotationに対する `Assert.Single`。WSL拒否という前報告は誤りであり、v3/v4のraw stdout/stderrにはWSLエラーがない。WSL `E_ACCESSDENIED` は後続の別ログ読取試行で発生したものだった。試験を実SDKモデルのcallback surfaceへ合わせ、製品コードの `WithEntrypoint("tini")` と `WithArgs("--", "./bin/simulator-cli", ...)` は保持した。callback引数列は固定profile値と一致する。Effective Docker entrypoint/runtime起動は実DCPでの次段階観測に残る。
- 訂正後のfocused testは1/1 pass、直接 `AppHostApplicationModelTests` は26/26 pass、AppHost `--no-restore` buildはexit 0。いずれもLinux `/usr/bin/dotnet`（realpath `/usr/lib/dotnet/dotnet`）で実行。対象source SHA-256: Program `63075593c8ab5619d0bd88ed213d9c130b6d8c1c960d58b07df89920593f4d69`; test `933d3fcd60e136a7dd2352f7e52439452b175e6ace3ad39ba03fdb77093c8068`; design `d259bcbd3d5722a97da2ff66f2715c7f67b3d2c38c68fc71a9be4f491d80ed1f`.
- Raw command, stdout/stderr, exit and hashes are in `reports/evidence/issue81-05b2-vision-simulation-20261023/`, `manifest-v1.tsv` SHA-256 `f5c3582ec39512ad92579e7d6a488ac9a3d4a9405bac30e3bad6f92e3abcd0ea` (21 entries). The initial Red/v2 raw files are preserved; no command metadata was reconstructed where it had not been saved. No container/image build, DCP run, multicast input, or strict-health success was measured in this code unit. The next normal-path measurement is the existing simulator plus Duck in the same Windows DCP bridge with owned cleanup.


### 05B2 changed-fragment lint and prior RuntimeHost-only observation (2026-10-23)

The changed-fragment check used the three extracted current-addition inputs at the recorded HEAD: tasks (4 added lines), phases (1 content line plus a blank line), and the simulation design (1 paragraph plus a blank line). The extracted input hashes and all eight existing lint configuration hashes are recorded in `reports/evidence/issue81-05b2-diff-lint-20261023/final-result-summary.json`. The preexisting Sudachi interpreter loaded `sudachipy` and `sudachidict_core`; package-version metadata was unavailable, so no version is asserted. The existing dictionary file was read-only hashed (`system.dic`, SHA-256 `6c1d5adc8a2389875713056e7b39bbcd0073d6122ffd509866e1d3a196f8608e`, 217,374,303 bytes).

The first two attempts exposed incomplete mirror preparation, not document findings: the first lacked `list-markdown-targets.js`; the next lacked `tools/lint/scripts/textlint-rules`. Those raw failures remain under `final-v1` and `final-v2`. After copying the existing helper scripts and two existing textlint rules into the isolated Temp mirror with matching hashes, textlint passed (exit 0). CSpell checked all three input fragments and exited 1 with 14 findings; Sudachi exited 1 with 28 findings. These are findings on the extracted added lines, including `localhost`, `Draft`, `Issue`, `Vision`, `base`, `comparison`, `image`, `command`, `bridge`, `multicast`, `strict`, `health`, `モデル`, `アクセス`, `レシピ`, and `コード`; no whitelist or dictionary was changed. This is not a full-document gate. Yomiyasu was not run for this one paragraph/three-row fragment check.

Current measurement also includes the already-existing, sanitized RuntimeHost-only observation in workspace `issue81-dcp-runtimehost-run-v2-summary.json` (SHA-256 `93763AB7276B3B8C1C91B07C7E974179F296ACC18A7FFC8999EEF9FE3FDB142F`), referenced rather than duplicated in the evidence bundle. Run `fed97710c9cc4b0885f6b827b98d0845` observed the owned container full ID `f3ea729b2c0d22acdae2e6772510a67a6d155ad2218bc2fa53103c88b732e95d`, localhost port mapping `39108 -> 127.0.0.1:32003`, and same-container identity-control exec exit 0. No UDP input was sent, so `/health/vision` returned the expected 503; UDP-to-ready was not demonstrated. DCP-owned cleanup completed without manual container stop/remove. The raw event stream and exact event count/observation interval were not saved, and the AppHost wrapper exited 1 on Ctrl+C without a separately captured AppHost exit code.

Evidence inventory for this check is `reports/evidence/issue81-05b2-diff-lint-20261023/manifest-v1.tsv`; individual failed preparations and corrected runs are retained by generation.


### 05B2 Windows AppHost/DCP vision normal-path observation (2026-10-23)

- Source was exact commit `b14800065c93343ef7fef18bba92290f19ca958c`, synchronized into the existing native Windows mirror. AppHost no-restore Debug build and RuntimeHost Release `LinuxContainer` publish both exited 0. AppHost DLL SHA-256: `2d14ea71affd2f791dd1af7c837093b452813f496652c9435b6ee9417b071b48`; published RuntimeHost DLL: `560e9ff908648124deb10fb53673e4755c7eef5ebf6c8f22ef238f6bdd8379a7`; runtimeconfig: `c230a317a54dd960bcbeb5f347f52e18dc665a26f7efda2159fced9a5ac7e097`. Dockerfile SHA-256 `70846d6a485e77470931e3bce193378b6b577140e9ba0b5233a168b557bf7661` retains the pinned `mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3` base.
- One foreground native Windows AppHost run used `Testing:VisionSimulationOnly=true`, desktop identity bridge enabled, explicit Docker CLI path and `desktop-linux` context, expected EngineID `5bc5ab8a-7340-47ba-8b7c-9f645375a54c`, and fresh RunId `68fd7e39e18e4968b18450730eca8651`. No persistent PATH/context/auth/security setting changed. The AppHost/DCP run registered only the selected `duck` and fixed simulator resources. Both containers joined network `aspire-session-network-edknahwb-Duck.Testing` (network ID `01d6bb0e42532792aff633cc0a7f96a6c9949e1229053d2bc8c072b0769f8f0a`).
- RuntimeHost container full ID was `3b158cd0ca4d1394b66e390c9cd0e6722897d7faae035055d1c5f03e036b6216`; its `com.ibis-ssl.duck.run-id` and `com.ibis-ssl.duck.role` labels matched the RunId and `duck` role. Aspire resource name/ResourceId was `duck-zhjxnngc`; this matches the DCP name label and is distinct from the full ContainerId. DCP produced image ref `duck:55faad2d635092002bd34212ba5aa801e232ab2d`, image ID `sha256:2f611370b8a805834674a0a3a2da1eaa8717c5bbaf4c4e527e14ae6a38e73868`, from the existing publish/Dockerfile context.
- Simulator full ID was `f435efb2b63a10d01e2fdd0b7b2e4c02023a927f0116b06cc64caf9601231a32`; its image was the already-present fixed digest `ghcr.io/ibis-ssl/framework-simulatorcli@sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79`. Inspection showed entrypoint `tini`, command `-- ./bin/simulator-cli -g 2020B --realism None --ibis-port 12346 --ibis-team-color yellow`, the same Aspire network as Duck, and no host UDP port mapping. Duck's only observed diagnostic mapping was `127.0.0.1:16537 -> 39108/tcp`.
- `GET http://127.0.0.1:16537/health/vision` returned HTTP 200. A sanitized response projection recorded `VisionPacketsReceived=10170`, `IsReady=true`, diagnostics reason `Ready`, and identity `(RunId=68fd7e39e18e4968b18450730eca8651, ResourceId=duck-zhjxnngc, ContainerId=3b158cd0ca4d1394b66e390c9cd0e6722897d7faae035055d1c5f03e036b6216, Generation=1)`. ContainerMode returns 200 only when its snapshot is ready; this observed successful RuntimeHost vision reception on the selected simulator path. The complete 566-byte HTTP response was not saved: the sanitized projection showed `SourceKey` empty and `Age` null, so no stronger source-attribution or freshness claim is made, and simulator-internal decode health was not measured.
- The same foreground session received Ctrl+C. Afterwards there was no AppHost process, the RunId-filtered container query was empty, and inspecting the simulator by its exact full ID returned `no such object`. Together with the empty RunId-filtered query for the labeled Duck, these observations confirm both selected containers were absent after DCP shutdown. The PowerShell session exited 1 on interruption; the child AppHost exit code was not captured. `apphost.exit.txt` contains synthetic fallback `130`, not a measured child exit. The separate DCP subprocess exit was not measured. No manual container stop/remove was issued.
- The evidence bundle is `reports/evidence/issue81-05b2-dcp-vision-run-b148-20261023/`. Six sanitized JSON files and the original `manifest.json` were copied byte-for-byte from the task workspace; copied manifest SHA-256 is `bea266f4062a1c0b5ceaf95b290cabecaecad38c403c9b2f6d6a89a4f2d5e64f`. The bundle contains no AppHost stdout/stderr or synthetic exit file. The 2,038-byte stdout remains only in the task workspace because it may include a Dashboard token (SHA-256 `d2285bb142bde21c0d30f9b14993a301ce6eb6e00abc8db2ebf4bfacf03100f7`); stderr was empty. The original `shutdown-verification.json` preparation error is also excluded; the corrected shutdown record is included.

### I81-05B3 明示的な Game Controller 視覚資源選択（2026-10-23）

`Testing:GameControllerVisionOnly=true` の明示設定で、固定 simulator、Duck RuntimeHost、Game Controller、RefereeDriver の4つを標準 `ContainerResource` として登録する。既定構成と、既存の2資源 `VisionSimulationOnly` は変更しない。simulator の固定ダイジェストと起動引数を維持し、Game Controller は固定 `robocupssl/ssl-game-controller` 参照、既存の視覚・追跡・審判情報の宛先、`:8082` のHTTP待受を使う。ホスト側では既存の Duck 診断HTTP 39108に加え、Game Controller のHTTP 8082を `127.0.0.1` の動的TCPポートへ対応付ける。UDP端点とホストネットワークは設定しない。RefereeDriver は内部URI `ws://game-controller:8082/api/control` で観測用に起動し、自動運転設定を渡さない。依存順は simulator の起動、Duck の準備完了、Game Controller の起動、RefereeDriver の起動である。

固定試験 `ExplicitGameControllerVisionModeUsesPinnedStandardResourcesAndLoopbackUiOnly` のSHA-256は `48cf90d3cfe87cb2623e3bbca4becee7d49a1cf8eeb4a675e6414a64ad9e1e9a`。製品ブランチ追加前はコンパイル後に実行され、資源数の期待値4に対し実値5で失敗した。これは構成モデルの有効なRedである。製品追加後の最初の実行はローカル変数名の衝突（CS0136）によるコンパイル失敗で、機能結果には含めない。次の実行はコンパイル後、登録名 `gcVisionSimulator` / `gcVisionDuck` と試験が照会する標準名 `simulator` / `duck` の不一致で失敗した。登録名を標準名へ直し、試験の期待値は変更していない。最終の固定試験は1/1成功し、この試験と直接関係する既存AppHostモデル試験4件を合わせて5/5成功、skipなし。AppHostの `--no-restore` build はエラー0で成功し、既存のASPIRE010警告が1件出た。

最終ソースSHA-256: `Testing/Duck.Testing.AppHost/Program.cs` は `cf6888ceba02dd2776b8ccc189c243054a1ba0ab03c24e73d57fc02d68776fcc`、`Tracker/Tracker.Tests/AppHostApplicationModelTests.cs` は `48cf90d3cfe87cb2623e3bbca4becee7d49a1cf8eeb4a675e6414a64ad9e1e9a`。コマンドと生出力は `reports/evidence/issue81-05b3-gc-standard-resource-20261023/` に世代別保存した。`test-stage-red-v1/` は機能Red、`test-stage-green-v1/` はコンパイル失敗、`test-stage-green-v2/` は登録名の照会失敗、`test-stage-green-v3/` はfocused成功、`final-validation-v1/` は関連試験5件とbuildの結果である。ここで確認したのはAppHostモデルとbuildまでであり、DCPでの4資源起動、Windows画面操作、RefereeDriverの観測、実UDP受信、DCP所有の後始末は未検証である。


### 05B3 NR-011/012 の固定参照・役割修正（2026-10-23）

54ef302f199e0dc24e000a6fb0101441af42795d の既存4資源モデルテストに対し、simulatorの固定repository/digest/起動引数を確認するテストと、登録名 `duck` に対するExpectedRole/所有role/projection受理を確認するテストを追加した。後者は実AppHostの `duck` Resourceを使い、opaque event key・Running snapshot・64桁container IDの既存projection正例を通すモデル契約である。DCPからの実通知を証明するfixtureではない。

製品変更前、54efのProgram.cs SHA-256 `cf6888ceba02dd2776b8ccc189c243054a1ba0ab03c24e73d57fc02d68776fcc` と固定テストSHA-256 `bf616f532d219872d6183f81257b01383a01edecf48dbac07713a5009c2d2e03` で新2 testを実行した。コンパイルは成立し、2/2が実行時assertionで失敗した（exit 1）。simulator testは期待 `ghcr.io/ibis-ssl/framework-simulatorcli` に対し実値 `ghcr.io/ibis-ssl/framework-gcVisionSimulatorcli`、role testは期待 `duck` に対し実値 `gcVisionDuck` を観測した。後続のowner label/projection正例はrole mismatchで最初のassertが失敗したため未到達である。この実Redはレビュー対象54efの不一致を捉えたもので、54efの以前のCI成功はこの不一致の解消証拠ではない。

最小修正は Program.cs の4つの文字列値のみ: simulator repositoryを `framework-simulatorcli`、実行体を `./bin/simulator-cli`、ExpectedRoleとbridge roleをresource名と同じ `duck` に戻した。変数名、固定digest、他resource、既定構成は変更していない。test sourceはRedからGreenまで同一SHA-256 `bf616f532d219872d6183f81257b01383a01edecf48dbac07713a5009c2d2e03`。修正後の4関連model testは4/4、skipなし、exit 0。AppHost `--no-restore -p:EnableSourceControlManagerQueries=false` buildはexit 0、stderr 0 byte。`git diff --check` もexit 0。修正後Program.cs SHA-256は `7bb54742f1504c02d0f4060adff577d4e28cf5e18f506403d2981faff474167c`。

原command/stdout/stderr/exitとhashは `reports/evidence/issue81-05b3-nr011-nr012-fix-20261023/` に保存。`inventory-v1.tsv` は12 files、SHA-256 `581cc44463c2b5220394b4df0b5ed2c2b5e6a89f408e9aa9d717d0ffae394f92`。今回の新report段落に対するtextlint/CSpell/Sudachi検査は未実行。既存の文書語彙gate不合格は継続しており、ここで成功扱いしない。4資源の実DCP起動、GC画面操作、RefereeDriver観測、Vision UDP受信/healthはまだ実測していない。次のDCP測定はこの修正の通常review/固定後に行う。
