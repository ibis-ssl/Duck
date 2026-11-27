# Sub-agent実行レポート

## タスク

Issue81 / PR84 / I81-05A。通常用3配布定義と既存固定参照を使う資源構成モデルの先行試験と実装。

## sub-agentを使う理由

同じ実装担当を継続し、親は要件・依存・台帳・公開を管理する。

## 対象範囲

基準HEAD1c9ac3562ab81be2a78d4383cf873c08706dd0ca。主設計179行と245行、手元Issue完了条件を照合し、本人必須・設計選択・担当追加条件を区別する。通常用3定義と固定参照、UDP非公開、localhost限定動的TCPの構成モデルだけを対象とする。先行する固定試験の実行時失敗を確認後、同じ期待値で最小実装する。

## 対象外

今回の単位ではI81-08、G1外部宛先/G3診断統合、全資源移行の完了を主張しない。これらは後続順序であり既存Issue全体の承認を撤回しない。環境・依存・認証・外部repo・拒否済み操作等は、本人の既存承認と具体操作を照合して扱う。独立最終は後続工程、mergeの許可なし。デバッガー固定版調査は任意。

## Dispatch profile

- selection_source: explicit_user_override / continuity_reuse
- task_kind: implementation
- signals: work_class=judgment_heavy; uncertainty=medium; change_radius=cross_module; criticality=high; repetition=single; decomposability=sequential_dependencies; decomposition_policy=forbidden; context_need=bounded_history
- executor: /root/issue81_luna_implementation
- original_requested: {model:gpt-6-luna,reasoning_effort:high,fork_turns:none}
- planned_runtime_profile: original requested retained; final role effect unobserved
- applied: null
- application_status: reused_existing_agent_profile
- runtime_profile_observability: final_profile_hidden
- role_plan: original default role; effect unknown
- override: 自動候補Sol highより本人の実装Luna high指定を優先。新規起動なし。
- report_persistence_mode: normal_persistence
- verification_capability: .NET実行はremote_ci_only。既存ローカル検査は対象入力を事前照合して再利用。
- parent_role: ASPIRE全体統括。本人09:43の役割訂正を適用。現在Issue81を進め、#18/#79/#80/#82/#43/#50の既存依存を管理する。新規作業の無制限着手はしない。
- scope_correction: 上位親の便宜的な通常構成限定と08禁止を撤回。本人の09:42「親の作業範囲を意図して狭めていない？大丈夫？」を受け、既承認Issue全体を順に継続し、追加の工程承認待ちを作らない。05Aは開始単位で権限上限ではない。
- successful_local_candidate: AppHost全体build exit0/ASPIRE010警告1、固定3件を含むモデル試験23pass/0skipを受領。既存固定31入力・既存cacheだけを使用、gitlink/設定/依存宣言変更なし。API修正後のPort8080実失敗も保存し、固定期待値を保ったPort未固定への修正で成功。ビルド前push例外は不要。現在sourceの通常commit/pushと同じ通常担当のP1修正検証へ進む。
- verification_route_update: 既存固定31入力の復元と既存cacheのみの通常restoreにより、今回対象のlocal_execution_availableを確認した。AppHost全体build exit0、固定3モデル試験はcompile成功後2pass/1runtimefail（host Portが8080）。以後このlocal経路をinner loopに用い、旧remote_ci_onlyを固定前提にしない。SourceLink等のCLI差とCI同等性は別記する。
- existing_source_recovery: 既取得b9 failure artifactのsource.tar.gzにある2submoduleの31 .protoが既存Git object storeの固定commit blobと全件一致。新規取得や版変更なしで通常生成入力を用意できる経路を確認した。限定CI公開例外を必須とした前判断は撤回し、まず既存入力の通常生成/buildを継続する。
- local_generation_followup: 上位親の指示によりf71のCI成功とlocal不足の差を同じ担当が読取で切り分ける。既存承認済み通常生成/buildで解消できる場合は継続。新依存・設定・版変更なし。ビルド前push例外は未承認のままで、次公開停止を維持する。
- parent_publication_gap: b9d92eedのpush前に当該変更内容のビルド成功証拠は無かった。f71のビルド成功は先行試験版の証拠であり新実装へ移転できず、構造検査やXML名の確認も代用にならない。親がremote_ci_onlyを理由に公開を進めた判断は本人のビルド成功後push条件を満たさなかった。f71の計画Redとb9のcompile失敗を区別し、次pushは停止する。
- build_failure_continuation: b9d92eed / CI39938732680のCS1061で試験未実行。検証失敗から調査へ再分類（uncertainty=high、cross_module/high）。本人指定Luna highを維持しapplied:null。同じ担当が正しいAPIと既存ローカルビルド経路を調べる。次pushはビルド成功確認まで停止、単に未確定なG1/G3や任意デバッガー調査とは別の具体的な制約。
- red_acceptance: f71bb2688caf080382d170937ea932601e0b3c3f / CI39936732269 attempt1 build成功、TRX779=776pass/3runtimefail/0skip。先行3件の契約欠落を有効Redとして受領。実装へ継続、Greenと実機成立は未確認。
- preimplementation_state: fixed_test_sha256=0fbc0e30918c241d100b9b415b71a13f455153650e2f475ff824ef3eddf38b2e; technical_head=1c9ac3562ab81be2a78d4383cf873c08706dd0ca; commit=commit_pending; push=push_pending; ci_wait=not_started; runtime_red=unconfirmed. 遠隔のみ実行可能なため先行試験候補を通常pushしてコンパイル成立と実行時失敗を区別する。
- design_decision: 主設計の公開契約と通常用定義を実装する。版/digest要件の削除や別デバッグ方式採用はしない。追加の契約変更が必要なら先に親へ返す。

## 実行コマンド

- `git diff --check` — `/home/donabe/Duck-issue81-aspire-desktop-bridge`、終了値0。
- .NET試験は未実行。標準 .NET 試験は `remote_ci_only` のため、親による先行試験公開後の正確なCIで実行時Redを確認する。
## 対象ファイル

- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs` のみを変更。試験追加と共通の配布期待値ヘルパーを含む。ファイルSHA-256 `0fbc0e30918c241d100b9b415b71a13f455153650e2f475ff824ef3eddf38b2e`。
- 製品登録、Dockerfile、発行profile、主設計、canonical trackingは未変更。
## 指摘事項

- 先行試験は既存AppHostの実際のアプリケーションモデルを作成し、RuntimeHost・DebugHost・明示選択時のRefereeDriver.Hostを `ContainerResource` として確認する。各資源の `DockerfileBuildAnnotation` が該当プロジェクトの `bin/Release/net10.0/publish` をcontextにし、実Dockerfileと固定FROM digest・ENTRYPOINTに結び付くことを検査する。
- UDP端点がないこと、DebugHostの内部TCP 8080と動的host port・loopback設定、RefereeDriverの内部 `ws://game-controller:8082/api/control` とdefaultでの非選択を固定する。注釈の構成検査であり、DCP/Windowsの実際のhost bindや内部疎通は主張しない。
- 既存04B2の3発行profileとDockerfileの固定値を期待値に使用。実publish実体との検証は公開後CIへ残す。
## 結果

- 05A先行試験3件を `AppHostApplicationModelTests.cs` に追加し、期待値を固定した。対象変更はこの試験ファイルだけ。`git diff --check` は終了値0、試験ソースSHA-256は `0fbc0e30918c241d100b9b415b71a13f455153650e2f475ff824ef3eddf38b2e`。
- 固定期待値: RuntimeHost Dockerfile context=`Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish`、FROM `mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3`、ENTRYPOINT `Tracker.RuntimeHost.dll`; DebugHostとRefereeDriver.Hostはcontextを各プロジェクトの同じprofile出力先とし、FROM `mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca`、各DLLをENTRYPOINTとする。DebugHost TCP target port=8080、host portは未固定、annotation target host=`127.0.0.1`、UDP端点なし。RefereeDriverは `RefereeDriver:Enabled=true` と `Testing:ContainerMode=true` を明示した時だけモデルへ追加し、内部Game Controller URIは `ws://game-controller:8082/api/control`。
- 親へ先行試験公開を依頼する状態。CIでruntime Redが得られるまでは製品登録変更を行わない。
## リスク

- C#/.NET試験はremote CI待ちのため、コンパイル成立と実行時Redは未確認。API compile errorは機能Redとして扱わない。
- publish contextの存在と実際のファイル内容、DCPがWindowsで127.0.0.1へbindすること、旧wrapper経由のGame Controllerと内部URIの疎通は未検証。今回の試験はResource/Endpoint/Dockerfile構成を確認する。
- 既存AppHost登録は現時点で旧ProjectResource/ExecutableResourceであり、runtime RedはCIで確認する。通常資源の移行後は既存回帰試験の資源型期待値を更新しつつ、外部wrapperの従来のライフサイクル契約を保持する必要がある。

## 05A focused 文書検査と要件根拠の区分（2026-10-23）

- 要件根拠: Issue81の既存完了条件と主設計179–187/245–249を根拠として、今回の試験は実AppHostモデル上の通常3資源、UDP非公開、127.0.0.1限定の動的TCP、内部接続先を確認する。ContainerResource/Endpoint注釈の検査は設計上の構成契約であり、実DCPのWindows bindや実疎通は受入根拠に含めない。
- 設計選択: 既存のDockerfileBuildAnnotation、04B2で固定済みのimage参照、明示選択時だけのRefereeDriver構成をAppHostモデルへ照合する。全Issue完了や実機成立を今回の条件へ縮めず、05Aは順序上の作業単位として扱う。
- 担当側の追加条件: 05A先行の分割は親の工程整理であり、ユーザーの追加受入条件ではない。デバッガー固定版の調査/installer取得を05Aの停止条件にしない。実行前拒否されたinstaller GETは未実行の履歴として保つ。
- 1c9ac356との差分preflightは、実sourceからtasks 6追加行（36,37,38,39,53,58）とphases 8追加行（5–11,26）を抽出し、抽出fragment byte/hashとmirror入力の一致、baseline/current source hash、固定C#試験SHA `0fbc0e30918c241d100b9b415b71a13f455153650e2f475ff824ef3eddf38b2e`、lint設定8件のhashを実行直前にassertした。既存Windows Node v24.20.0/cache/mirrorと既存WSL venv Python 3.14.4を使用。1回の `--files` 引数へcanonical相対2pathを渡した。
- 追加行だけの結果: textlint exit 0; CSpell exit 1（tasks fragment: bind, Container, Draft, Issue）; Sudachi exit 1（bind/TCP/AddContainer/Draft/Issueとカタカナtoken）。PowerShell stream captureでは日本語tokenが文字化けしたが、同一fragment SHAとsourceのUTF-8 bytesは一致し、WSL内subprocessのstdout/stderr byte captureは「モデル」と出力した。
- 削除側も同じv4 mirror/config・canonical path・ツールで4 tasks行と1 phases行を比較検査した。textlint exit 0、CSpell exit 1、Sudachi exit 1。現在側で検出された英単語/CSpell token/katakana tokenはいずれも削除側で検出された語と重複し、変更に起因する新規語彙指摘は確認できなかった。終了値1を成功扱いにはせず、文書gateはheldのまま。
- Evidence bundle: `reports/evidence/issue81-05a-status-lint-20261023/final/`（inventory 31 entries; `inventory-v1.tsv` SHA-256は同ファイル）。実行raw、Windows側の元captureとWSL内Sudachi byte-capture、入力fragment、commands/exit比較、config/source/hash対応を保持。初回preflightの手入力hash誤りによる中断記録は親bundle直下に保存済みで、lintは起動していない。
- .NET試験はremote_ci_onlyで本担当環境から未実行。製品/C#試験/固定期待値は今回の文書確認で変更していない。

## 05A先行試験のexact PR CI（2026-10-23）

- 既発行PR run `39936732269`、attempt 1、event `pull_request`、branch `task/issue81-aspire-desktop-bridge`、head `f71bb2688caf080382d170937ea932601e0b3c3f` を回収した。checkoutは `refs/pull/84/merge` / merge SHA `9282ba8cbbbc50df77ad9a57d20659a8bdcc3105`。同じworktree HEAD/branchが一致し、commit内とworktreeの試験SHAはともに `0fbc0e30918c241d100b9b415b71a13f455153650e2f475ff824ef3eddf38b2e`。
- `.NET SDK` setupと試験assemblyのbuildは成功。`dotnet test` exit 1、TRXは779件中776 pass/3 fail/0 skipped。3件は追加した05A構成試験の実行時assertion失敗で、compile/setup失敗ではない。RuntimeHost/DebugHostは `ContainerResource` を期待した箇所で実値が `ProjectResource`、明示選択時RefereeDriverは条件に合う資源なし（既存モデルにはExecutable/ProjectResourceが残る）。この結果は実AppHostモデルの未移行を示す実Redとして保持する。
- 後段のLinux publish出力検査およびLinux publish failure diagnosticsはskipで、発行出力の成否は測定していない。packet-flowとfull-stack Linux acceptanceもskip。標準既存試験776件は成功。
- failure artifact `dotnet-test-failure-39936732269-1` (id 12494381685, 27,060,611 bytes, API digest `4bb2888678e68fa0f4ca925e91bc1985b25152cb3cc9e8a76c43d66c94837cc4`) のTRX/stdout/stderr/exit/environmentを保存した。source archiveとbinlogはTemp原本に保持しrepoへ複製していない。証拠bundle `reports/evidence/issue81-05a-ci-f71bb268-20261023/`。`inventory-v1.tsv` SHA-256 `76d6987e3683b2afcd92b5452b08766700aaa71247b8d06bb916ab900fef5fb1`。
- 監視session 57490はexit 1でrun failureを回収済み。追加run/rerunなし。既存run queryの `gh run view --json artifacts` はCLI未対応fieldでexit 1（raw保存）；artifact一覧は既存REST readで確認してから正しいWindows Temp pathへ取得した。


## 05A 実装候補（2026-10-23、exact CI待ち）

- `Testing/Duck.Testing.AppHost/Program.cs` で通常の `duck` を既存 RuntimeHost Dockerfile と Release `net10.0/publish` 出力を build context にする `ContainerResource` へ登録した。比較モードの `debug-host` も既存 DebugHost Dockerfile と同じ profile の発行出力へ接続する。`referee-driver` は `RefereeDriver:Enabled=true` かつ `Testing:ContainerMode=true` を明示した時だけ、既存 RefereeDriver.Host Dockerfile／publish 出力を使って追加し、内部 URI `ws://game-controller:8082/api/control` と Game Controller の起動依存を設定する。外部 wrapper 資源の登録とライフサイクルは残した。
- 固定参照は既存 Dockerfile をそのまま使用する。RuntimeHost は `mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3`、DebugHost と RefereeDriver.Host は `mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca`。対象アセンブリの ENTRYPOINT、`COPY . /app`、各 publish path を構造検査で対応確認した。Docker build／image取得／起動はしていない。
- DebugHost は `Testing__ContainerMode=true` で既存アプリ設定がコンテナ内8080を待ち受けるようにし、named `http` endpointをtarget port 8080、host port未固定、target host `127.0.0.1`、non-proxiedにした。`/health/comparison` のhealth checkをこのendpoint名へ結び付けた。旧固定host port 39107/39109と対応preflightを除去した。旧設定はコンテナ時にアプリを8080だけへbindする `DebugHostHealthEndpoints.ConfigureInternalHttp` と整合しなかったためである。RuntimeHost比較診断の39108設定と旧wrapper資源の待機関係は残した。
- 既存回帰試験では、RuntimeHostのProjectResource型assert 2箇所とDebugHostの型assert 1箇所をContainerResourceへ変更した。comparison設定試験で旧39107/39109固定端点assertを、ContainerMode=trueと8080/loopback動的端点assertへ置き換えた。新規の固定3試験と `AssertLinuxDistribution` helperはf71基準とバイト単位で一致し、SHA-256は順に `859e995dfa2b8a2d2f8cfb16857a3039e8b7f19d4aaeb088c3bb0d6d69a2c48c`、`392f20836c266654b58bf76199581aaa0a3e79c753b16521a901a69ada10c0c7`、`1e6b02b2d8eec716234f9b10f1a3bb0f49ab3cd741741c1dd6c4c251d808475b`、helper `04a8c5ef0b803d77c93d8e5b3ea950082018b8baa3ea0851bdef31bdfef30f9e`。全体試験ファイルSHAは実装後 `a63641ea949a3222fa11cf3934b4b241a137702b917e43e226a2768814fd0d16`。
- 検査はfocused structural validator exit 0、`git diff --check` exit 0。evidence bundle `reports/evidence/issue81-05a-implementation-20261023/`、inventory `inventory-v1.tsv` SHA-256 `f7ef738144f687eba39e37197965b7b1309c087d6cbe737e7d4acf600e69b715`。Bundleはコマンド、stdout/stderr、exit、f71との差分test hash、Dockerfile参照値と現行AppHost/test SHAを記録する。
- Aspire.Hosting 13.5.4の既存XML API資料で `WithDockerfile(contextPath,dockerfilePath,stage)`、`WithHttpEndpointCallback(callback,name,isProxied)`、endpoint名指定可能な `WithHttpHealthCheck(path,statusCode,endpointName)` の存在を確認した。新依存はない。設計文書は既にcomparison設計31/38/132–149行および主設計179–187行でコンテナ、DebugHost 8080、動的localhost接続先を定義しているため、契約自体の変更は不要と判断した。ここで検査するのはAppHost構成注釈であり、Windows DCPの実bind、通信、Docker build／startは未検証。
- 標準 .NET build/test は `remote_ci_only` のため今回未実行。f71の有効Redから実装した変更のcompile、旧回帰、3固定新試験のGreen、publish出力連携はまだ証明していない。親による公開後にexact-head PR CIが必要。
- 検証script作成時にPython変数の取り違えで補助操作がexit 1となった。Program.cs側の置換は完了していたため、実内容を読み直してtests側の対応置換を完了後、focused validatorを再実行してexit 0を得た。製品コードや固定試験期待値の破損はなかった。report Dispatchおよび親所有trackingは変更していない。


## 05A CS1061 API investigation and local build (2026-10-23)

- Exact PR run `39938732680`, attempt 1, head `b9d92eedf96bb00a07d18b61db4f8497829f1dce`, branch `task/issue81-aspire-desktop-bridge`, checkout `refs/pull/84/merge` / merge SHA `99ab997f865864f25c5cab04b14d5b52b0332956`, completed failure. Setup/restore completed, then AppHost compilation failed at `Program.cs(254,10)` with CS1061: `IResourceBuilder<ContainerResource>` has no accessible `WithHttpEndpointCallback`. `dotnet test` exited 1 before test execution; no TRX was produced. Standard tests, three publish-output checks and publish diagnostics were not measured. Packet-flow and full-stack acceptance were skipped. This is a compile/API failure, not a runtime Red. Artifact `12495550402` was downloaded once and its text/raw metadata retained in `reports/evidence/issue81-05a-ci-b9d92eed-20261023/` (20-entry `inventory-v1.tsv`, SHA-256 `65ac7d8ec50e2a8bbade55a4d66da98d11055c87599191e13ab960ae247cda83`). No run/rerun was started.
- The initial API claim came from the cached `Aspire.Hosting.xml` member name. Inspection of the resolved 13.5.4 `Aspire.Hosting.dll` shows that `ContainerResource` implements `IResourceWithEndpoints`; `WithHttpEndpoint<T>` is actually present and constrained to `T : IResourceWithEndpoints`. `WithHttpEndpointCallback` and `WithEndpointCallback` are not public methods in that referenced DLL, despite their XML entries. `EndpointAnnotation.TargetHost` has a public setter, and `IResourceBuilder<T>.Resource` / `IResource.Annotations` are public. Therefore the XML member name was not evidence that the callback was available to this compiled assembly; the observed CS1061 is the actionable evidence. This does not characterize the callback as project-only.
- Minimal uncommitted correction in `Testing/Duck.Testing.AppHost/Program.cs`: retain `.WithHttpEndpoint(targetPort: 8080, name: "http", isProxied: false)`, then get the `EndpointAnnotation` named `http` from `debugHost.Resource.Annotations` and set `TargetHost = "127.0.0.1"`. No test expectation was changed. Current AppHost source SHA-256 is `f438efe6766a72fcba8f550a5c8b13f6dd4c33b04cc738531c85bc869e59ac57`; fixed test source remains `a63641ea949a3222fa11cf3934b4b241a137702b917e43e226a2768814fd0d16`.
- A temporary C# API-usage probe using the existing `project.assets.json` compile references and installed .NET 10 reference pack compiled successfully (205 references; exit 0; only CS1701 compatibility warning). It exercised `WithHttpEndpoint` and the annotation setter. This verifies API syntax only, not AppHost integration or DCP behavior.
- Local full build attempts used only `dotnet build ... --no-restore`. Without a transient property, SDK 10.0.113's Git metadata task stopped before compilation because this worktree has unsupported Git extension `relativeworktrees` (exit 1). With `-p:EnableSourceControlManagerQueries=false` for this command only, build advanced but failed with 30 unresolved generated protobuf type errors: the referenced `SslProto/src/**/proto` directories contain no `.proto` inputs and no generated C# files exist in the current tree. A further AppHost-only attempt with `-p:BuildProjectReferences=false` stopped with CS0006 because `Testing/Duck.Testing.RefereeDriver/obj/Debug/net10.0/ref/Duck.Testing.RefereeDriver.dll` and `TrackerConnectionLib/obj/Debug/net10.0/ref/TrackerConnectionLib.dll` are absent. No restore, install, Git/config edit or environment change occurred. Thus a whole-project local build is unavailable from the current generated assets; CI remains necessary for compile and tests.
- API reflection, C# probe, and all three build command/stdout/stderr/exit records are preserved in `reports/evidence/issue81-05a-api-investigation-b9d92eed-20261023/`; `inventory-v1.tsv` SHA-256 `a73f92bffc7e78f4534d775dcd25fbaf9471d57690241312ae245162a3145d0e`. The probe source, actual resolved DLL/XML/project-assets hashes, and test/source identity are recorded in `metadata.json`. The earlier CI artifact bundle and all prior 05A/04B evidence generations remain unchanged.
- Remaining validation: parent must publish the correction before a new exact-head PR CI can establish that the AppHost compiles and the three 05A tests pass. No CI was retried here; no publish/image build or runtime was performed.


## 05A local input preparation and model verification correction (2026-10-23)

- Correction to the earlier local-build note above: the protobuf compile failure was caused by the two gitlink worktrees being uninitialized in this checkout, not by a missing or unreproducible source revision. The prior conclusion that a whole-project local build was unavailable is superseded by this section. No `git submodule init/update`, fetch, clone, or change to `.gitmodules`, gitlinks, Git configuration, or shared module refs was performed.
- The already downloaded CI `source.tar.gz` at `/mnt/c/Users/donabe/AppData/Local/Temp/issue81-05a-ci-b9d92eed-20261023-artifact/dotnet-test-failure-39938732680-1/source.tar.gz` (SHA-256 `4f18d80e457dfbcda11ab7350b9b21f519bb4fc89af9872099fe0f8453c829c6`) contained exactly 20 regular `.proto` files for gitlink `9b0765fe7a07a55124f6ee957d4559348462d227` (`ssl-game-controller`) and 11 for gitlink `1e719a8d410cc84847ec69104da44fed13738291` (`ssl-simulation-protocol`). Each file was compared byte-for-byte to the pinned commit blob before placement. The 31 absent files were placed at their ordinary `SslProto/src/external/.../proto` paths; no existing file was overwritten. Every resolved output path was verified below this worktree's `SslProto` directory. `git submodule status` still reports the same uninitialized fixed gitlinks. `.gitmodules` SHA remains `3d493f4172bf5473b49d0bd1ec1fe564d6a0a821248aa05dfaea3e172444c3a9`.
- Using those inputs and installed SDK 10.0.113, `dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore --verbosity minimal -p:EnableSourceControlManagerQueries=false` completed with exit 0, one existing ASPIRE010 warning and zero errors. The SCM metadata property was an invocation-only CLI property; no config/project file was changed. Generated `SslProto`, RuntimeHost, DebugHost, RefereeDriver, and AppHost outputs are from the current checkout plus the two pinned proto input sets.
- The first focused `dotnet test ... --no-restore` attempt stopped before test execution with NETSDK1004 because `Testing/Duck.Testing.RefereeDriver.Host/obj/project.assets.json` was absent (setup failure, not a test Red). Its project has no direct package references; the already restored Tracker.Tests assets listed 101 package versions, and all 101 corresponding `.nupkg` files were present in the existing `/home/donabe/.nuget/packages` cache. Without changing package declarations or settings, a local-source-only restore of RefereeDriver.Host using `--source /home/donabe/.nuget/packages -p:NuGetAudit=false -p:EnableSourceControlManagerQueries=false` completed with exit 0. No remote feed was configured for that command and no package download/install was needed.
- With assets available, the fixed three 05A tests compiled and ran: 2 passed and 1 failed because `ComparisonModeUsesPinnedDebugHostPublishOutputAndLoopbackDynamicTcp` observed `EndpointAnnotation.Port == 8080` where the fixed contract asserts null (dynamic host port). The assertion was not changed. The minimal product correction sets the existing `http` annotation's `Port = null`, retaining `TargetPort = 8080` from `WithHttpEndpoint` and `TargetHost = "127.0.0.1"`. Existing Aspire 13.5.4/G2 static evidence at `reports/issue81-bridge-contract-investigation-20261022.md` lines 84–88 says `TargetPort` maps to the container port, a non-proxy endpoint with its public port omitted is assigned an available host port, and DCP carries the result into Docker mapping. This is source/spec analysis only; no DCP, Docker engine, image, or container was started, so actual Windows host binding remains unverified.
- After the correction, `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --verbosity minimal --filter FullyQualifiedName~Tracker.Tests.AppHostApplicationModelTests -p:EnableSourceControlManagerQueries=false` passed all 23 tests (0 failed, 0 skipped, exit 0). A final explicit AppHost no-restore build also succeeded (1 ASPIRE010 warning, 0 errors, exit 0). The fixed test file is byte-identical to the prior SHA `a63641ea949a3222fa11cf3934b4b241a137702b917e43e226a2768814fd0d16`; final `Program.cs` SHA is recorded in `postflight-final.json`. These local results do not replace the exact-head PR CI, full 776-test suite, Linux publish checks, or Windows/DCP runtime validation.
- Command/stdout/stderr/exit JSON, source-file path/hash list, local package-cache hashes, and final identity snapshot are in `reports/evidence/issue81-05a-localbuild-materialized-b9d92eed-20261023/`. The 31-source manifest SHA-256 is `8db88cdd2730eb8cd9a32df38a0bbf6b8c22852c1a18cd10b969e3db937403ae`; the generation inventory is listed below after finalization.


## 05A exact PR CI and focused tracking lint (2026-10-23)

- Exact PR run `39942827459`, attempt 1, branch `task/issue81-aspire-desktop-bridge`, head `3a7e62e6e8b3fad156414f47be43963b835298b7`, completed success. Checkout was `refs/pull/84/merge`, full merge SHA `a8b051f4ab743f92fb1f05e2758a719bd2e4fe46`; the CI log shows the merge commit and the PR head. The existing watch session 46182 terminated with exit 0. No rerun or new run was started.
- Standard .NET suite: 779 passed, 0 failed, 0 skipped. Linux publish-output check: success for all 3 managed applications; its Python contract suite passed 16 tests. DebugHost portable PDB contract passed 10 tests, matching the complete identity and finding 112 source document paths under `/src/`. `ASPIRE-NET-002 Linux packet flow` and `ASPIRE full-stack Linux acceptance` were skipped. No image build/start or runtime DCP validation occurred.
- Raw run JSON and log are in `reports/evidence/issue81-05a-ci-3a7e62e-20261023/`: `run.json.raw` SHA-256 `d960a37b763da85577e859a29807c8bfe944adf33a9a86444c496c67a7a0f703`; `run.log.raw` SHA-256 `4f713545979d31ab5fd473cfa72887202a23ca4a90a49f991af33fc6926b90fc` (64,946 bytes). The immutable first inventory is `inventory-v1.tsv` SHA-256 `84864c73fd85a21128a73540a8229dc4caa0aa01ba1e269dc68f6f7562a9cb13`; the completed full-SHA/job summary generation is `ci-summary-v2.json` (SHA-256 `ec424ba13f8808ee053470335a92062d67c2c8ce541baba2a609b6a04b3f2eb6`); its `inventory-v2.tsv` SHA-256 is `6cc41c638295617ef0358ddeaa5d5441b5dd8fe195649a3574945cb59d967df2`.
- The only tasks-status change relative to `b9d92eedf96bb00a07d18b61db4f8497829f1dce` was the I81-05A status row at current line 38. Baseline row SHA-256 `13caa0ca610febbc1ecf61181354ca1c298d944ff462d1c77f09133a89d6f893`; current row SHA-256 `6dc2f852830eae610fc97c6a6c51914c33ffd50fec469f939258d9b85e810a73`. The current row records preparation of fixed inputs from existing materials, successful local build and 23 model tests, and ongoing normal correction verification and remote validation; the exact-head CI success is recorded separately above.
- Focused lint used the existing Node v24.20.0 cache/mirror/config and existing Sudachi Python venv; 9 configuration/source-support files matched manifest SHA-256 `9f2e9f4fcc3b5d02316e99e2d813c27e5b88e218707ab2db49f64d3deef09e85`. Baseline/current extracted row bytes were asserted against the two source revisions before each run. textlint exited 0 on both; CSpell exited 0 on both; Sudachi exited 1 on both with byte-identical diagnostics (`TCP`, `モデル`), and no new Sudachi finding. No dictionary or whitelist changed; document gate remains held because the baseline findings remain. Bundle: `reports/evidence/issue81-05a-3a-status-lint-20261023/`, `comparison-summary.json` SHA-256 `56a4eebee820213811dcaa70984e6e355869148328ae6e553049daf21ce51a8e`, `inventory-v1.tsv` SHA-256 `18617b00c3509043fbc09f3d0019da99e2a1bcbf76240851a52672c9b3c2e035`. The first scratch-mirror preparation error and first repo-inference watch error are preserved in that comparison summary; neither executed lint or inspected a run, and both were corrected without rerunning CI.
- This CI verifies model tests and publish/PDB contracts for this exact head. It does not establish packet-flow/full-stack acceptance or actual image/container behavior.


## 05A final three-row tracking lint (2026-10-23)

- 3a baselineから現在の実差分を抽出し、tasks-status.mdの行37–38とphases-status.mdの行26、計3本文行だけを対象にした。baselineは `git show 3a7e62e6e8b3fad156414f47be43963b835298b7:<path>`、currentは作業treeのbytesを読み、各抽出fragmentをmirrorへ書いた直後と各lint起動直前に完全一致をassertした。両ファイルのbaseline/current source SHA、3行fragment SHAと内容は `reports/evidence/issue81-05-final3rows-lint-20261023/preflight-v2.json` に保存した。
- 既存Windows Node v24.20.0/cache junction、同じscratch mirror、既存Sudachi venvを使用。既存9設定ファイルはrepositoryとmirrorの両方でmanifest SHA-256 `9f2e9f4fcc3b5d02316e99e2d813c27e5b88e218707ab2db49f64d3deef09e85` と一致。新しい依存・辞書・whitelist変更なし。baseline/currentそれぞれ一回ずつ、1つの `--files` 指定で両方のcanonical相対pathを渡した。
- 結果はtextlint 0/0。CSpellは1/1で両方 `tasks-status.md:1:37 Unknown word (bind)`、元行37に対応。Sudachiは1/1で両方のraw stderrがbyte一致し、`bind`（tasks行37）、`TCP` と `モデル`（tasks行38）を検出。phases行26に指摘なし。新規指摘は確認されず、終了値1のbaseline指摘は残るためこの限定検査は成功扱いにせず文書gateはheld。
- 最初のharnessはbaseline fixtureを書いた後にcurrent fixtureも先にmirrorへ書いてから検査を開始し、baseline byte assertionでexit 1となった。lint commandは一度も開始していない。順序誤りを `first-preflight-failure.json` に記録し、baselineを検査してからcurrentを書いて検査する順序で上記を取得した。
- Bundle `reports/evidence/issue81-05-final3rows-lint-20261023/`: final comparison `comparison-summary-v3.json` SHA-256 `efc04e6c60a70c121a72f21de4ee32db564ff48ca7cc05187c94e8915a90d87f`、`inventory-v3.tsv` SHA-256 `a3d0661a4433f983cac4b416b4d0635eb6b3612be8e7493a3123f2b652687f4b`。既存のv2 manifestとrawを保持し、v3 manifestを追加した。
