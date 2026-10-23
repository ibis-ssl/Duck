# Sub-agent実行レポート

## タスク

Issue81 / PR84 / I81-05B1の通常レビュー。RuntimeHostの既存vision準備判定を実コンテナ資源へ接続する単位を確認する。

## sub-agentを使う理由

同じ通常レビュアーが固定仕様と実装・実行証拠を確認する。

## 対象範囲

基準9284640aaec4af59e44afe7687e4bf8a39494c48。追加対象は05B1の前提06A1（資源内制御プロトコル、実HostedService、診断identity置換と受信epoch）。外側DCP通知と実execは未実装。追加起動試験の事前Red欠落PD-I81-06A1-TDDを保持し、機能検証と工程遵守を分ける。先行試験、実AppHost登録、資源内の既存health、TCP端点と依存条件。未commitのProgram.csとAppHostApplicationModelTests.csを対象とし、固定試験69d87728、製品77734467と実行証拠を照合する。

## 対象外

実機受入、全外部資源移行の完了判定、独立最終レビュー、外部ソース・依存・環境の変更。

## Dispatch profile

- selection_source: continuity_reuse
- task_kind: review
- signals: work_class=judgment_heavy; uncertainty=medium; change_radius=cross_module; criticality=high; repetition=single; decomposability=independent_workstreams; decomposition_policy=forbidden; context_need=bounded_history
- decomposition_disposition: prohibited_by_review_lifecycle
- parallelism_mode: single_agent
- original_requested: {model:gpt-6.1-sol,reasoning_effort:high,fork_turns:none}
- executor: /root/issue81_normal_review
- applied: null
- application_status: reused_existing_agent_profile
- runtime_profile_observability: final_profile_hidden
- role_plan: original default role; effect unknown
- normal_cycle_state: reviewed b96d255世代はneeds_changes / NR005 P1・NR009 P2 open / NR006・NR007・NR008 P2 closed / required_findings=2。後続44d8dc7世代は未評価。PD-I81-06A1-TDD未充足。独立最終ではない。
- report_persistence_mode: normal_persistence
- verification_capability: AppHost build/modelは既存資材でlocal_execution_available。実起動・実機の成立とは分ける。

## 実行コマンド

- review_mode: initial normal_review / 05B1。reviewed_base_head: 9284640aaec4af59e44afe7687e4bf8a39494c48、branch task/issue81-aspire-desktop-bridge。対象は未commit Program.cs SHA7773446749b7d3b7bf1de2d222dc2eb4a25e880172d6fb59f5d740f4df00559eと固定試験file SHA69d8772874b8dde7306330a81cad0bf0e3dd66750b2be8b4db17bff080bfd107。
- 既読work-context-manager/review-enforcer/review-worker/sub-agent-task-manager/report-writer/report-output-managerを継続適用し、built-in code reviewを同じ一人で実施。requested gpt-6.1-sol/high/fork none、applied:null、continuity reuse、report_attestation_allowed:false。新agent/入れ子/orchestrator再入場なし。
- 正規FA780 PowerShell login:false / require_escalated → wsl.exe -d Ubuntu -- python3 - でgit HEAD/branch/status/diff、実source・直接依存・design・保存JSON/raw/manifestを読んだ。helper a782c5、b591c0、830ec7、048801、529e82、02a9e6、362c03、f3e653は全exit0。git grepで一致なしのexit1は検索結果として分離する。大きな依存出力の省略部分は必要なDI/diagnostics/health箇所を048801/529e82で個別読取した。
- 保存済み20entryのsize/hashと5段階のcommand/cwd/exit/raw、各source hashを照合した。新build/test/lint/CI・socket再現・実機起動は実行していない。自分の証拠は /tmp/issue81-05b1-normal-review-20261023/。

### NR006限定fix検証

- 同一通常cycleを継続。base HEAD9284640aaec4af59e44afe7687e4bf8a39494c48の未commit修正だけを確認した。requested Sol6.1/high・applied:null・normal_persistence・report_attestation_allowed:falseは不変。
- 読取helper8180e8、5addf5、df1f9bは全exit0。実差分・実占有fixture・保存Red/Green/class/buildのargv/cwd/exit/rawとsource hashを照合。新build/test/lint/CIや占有listenerを自分では実行していない。
- /tmp/issue81-05b1-nr006-fix-review-20261023/へ確認時のraw16fileのsize/hash対応表、最小差分/固定test監査、自己report保存guardを残す。

### 06A契約案の限定設計助言

- 実装/合格/独立finalではない。NR005/P1/open、NR006/P2/closed、needs_changes/残必須1を保持。同じSol6.1/high requested、applied:null、report_attestation_allowed:false。新agent/build/test/実DCP/実execなし。
- helper37ac6c、277c3a、9bc05f、962da0、1720efはexit0。固定source bundle13entryと7Git blobを照合。公開Version可視性の不足1点だけgh read-onlyで固定sourceを追加取得しexit0。外部sourceを依存へ追加しない。
- 保存5816adは実装reportが親next_unit追記でf794e3ba...から609e20f6...へ更新されたため、書込み前hash guardでexit1。b79542の読戻しexit0で通常reportと製品不変・未書込みを確認。最新親追記を保持して保存を再開する。新検査の再実行ではない。
- 証拠は /tmp/issue81-06a-contract-normal-review-20261023/。実装reportに残った旧version表現のみ、既訂正の公開API境界と一致するよう軽微整文し、実質結果/判定を変えない。

### 06A1 初回通常レビュー（修正前の対象固定）

- 同じ通常cycleとしてbuilt-in code reviewを実施。基準HEAD9284640aaec4af59e44afe7687e4bf8a39494c48、branch task/issue81-aspire-desktop-bridge、未commitの06A1実装を対象とする。元要求gpt-6.1-sol/high/fork none、applied:null、continuity reuse、normal_persistence、report_attestation_allowed:false。新agent/入れ子/orchestrator再入場なし。
- 実source/全差分/直接依存/現行2設計/5fixtureを読んだ。正規FA780 PowerShell login:false / require_escalated→WSL Pythonの読取・静的hash照合のみ。直近helper ab28f1/b45558/e29e64は各exit0。大きい表示の省略は必要なcommand/raw/対象箇所の個別読取で補った。新build/test/lint/CI/socket再現/実exec/DCPは実行していない。
- control-red inventory-v2の37entryを全size/hash照合し不一致0。green-v6/full-tests-v2/runtimehost-build-v1/apphost-build-v1のsource_sha256全件は現sourceと一致。文書bundleは保存されたcommand/入力/結果を確認し、不合格を成功へ変換しない。自己確認記録は /tmp/issue81-06a1-normal-review-20261023/。

### NR007–009 第1回限定fix検証

- 同一通常cycle、基準HEAD9284640aaec4af59e44afe7687e4bf8a39494c48上の未commit修正を確認。requested Sol6.1/high、applied:null、report_attestation_allowed:falseを保持。新agent/新build/test/lint/CI/実DCP/execなし。
- 読取3494eb/603b87/f6e87b/d36211/465ab0/bb8766はexit0。c964e1は既存metadataのJSON終端後にリテラル\\nがあるためjson.loadsがExtra dataでexit1となった読取失敗であり、新試験の失敗ではない。603b87で先頭JSONと残余を分離して実読した。大きな初回表示は対象boundary fixtureと該当rawの個別読取で補った。
- 79entryのinventory-v2全size/hash一致。key rawの記録hash/byte数も一致。旧重複v4の2実行は保存audit上でTERM後PID非残存を確認しているが、元exitは未回収でありRed/Green/成功へ採用しない。自身の読取processとは別履歴。

### NR007/009 第2回限定fix検証

- 同一通常cycle、基準HEAD9284640aaec4af59e44afe7687e4bf8a39494c48上の未commit liveness修正。requested gpt-6.1-sol/high/fork none、applied:null、report_attestation_allowed:falseを継続。新agent/独立final/新build/test/lint/CI/実DCP/execなし。
- helper0c29b0/7906f0/d68b98/263069の読取・hash照合は各exit0。大きい出力の省略はsource全365行/fixture全129行、必要なRed assertion・Green metadata・raw集計の限定読取で補完した。
- 両no-restore buildの保存結果まで回収。初回読取時のsourceと8組のGreen/build command内source hashes、raw size/hashが一致する。manifestの記載pathはrepo root相対として解決した。証拠と製品のguardを分け、後続別fixtureは本targetへ混ぜない。

- 保存46705eは書込前のsource hash guardでAssertionError/exit1となり未保存。確認8890e2/exit0で、他担当の後続製品がb96d255...から44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18fへ移動したと確認した。今回の結論は実読済みb96d255...世代に固定し、後続sourceを次targetとして分離する。現在作業木との一致を再主張しない。

### NR009 第3回・不変commit限定fix検証

- reviewed_commit: 2a6fd94455978021cd995104751f4f09b0a28dfd。commit内protocol SHA44d8dc7d15abce2a460b29f6f26e1b460882cbac01f9e267023606181e74c18fをgit showで読み、後続作業木と混同しない。同Sol6.1/high要求・applied:null・normal_persistence・report_attestation_allowed:false、新agent/入れ子なし。
- bafbe7/f36eda/60463c/d0dc79の読取はexit0。bc7b47はmanifestをheader付きと仮定したKeyErrorでexit1、cc71c7はrepo:参照をbundle相対と仮定しgit showがexit128（Python helper全体exit1）になった。いずれも読取だけで書込み/試験実行なし。実headerless sha/size/pathとrepo:参照を確認後、d0dc79で85件全照合を完了した。
- 新build/test/lint/CI/実container/DCP/execは発行していない。既存資材・commit内source/raw/metadataの照合だけを実施。自己証拠は /tmp/issue81-06a1-nr009-commit-review-20261023/。

### NR009 第4回・最小残actionのcommit固定検証

- reviewed_commit: 26c9e8655c0011a0e53628f7291ba86703db5895。Protocol SHA94bcc52414e0802cf049f80427145d663825f801a11d5f88e0ecf0eb2fd57e21。git showで不変commitの製品/新fixture/同commit設計・tracking差分/証拠を読んだ。既存Sol6.1/high要求、applied:null、normal_persistence、report_attestation_allowed:false、新agent/入れ子なし。
- 読取3577ca/8b955b/4cf933/f7848d/d870f0はexit0。2b5854はmanifest列順の仮定でAssertionError/exit1、acbeb2はGit未追跡のobj/project.assets.jsonをcommit内へ要求してgit exit128/Python exit1となった読取失敗。書込み/試験発行なし。観測したrelative_path/bytes/sha256列順へ合わせ58件全照合後、生成入力は既存diskのhash確認と区別した。
- 新test/build/lint/CI/実DCP/execは実行していない。通常report子5欄と /tmp/issue81-06a1-nr009-close-review-20261023/ の自己確認証拠のみ保存する。親prefixの更新拒否は親通知として保持し、代行変更しない。

### 06A2 初回通常レビュー（正常系優先）

- reviewed_commit: a2dd3584578d88499c3e52fc0d9d97db164cc5c5、baseline26c9e8655c0011a0e53628f7291ba86703db5895。最新本人指示「一旦正常系が動くように」「コスパ重視」を適用し、今回の純粋候補投影と既存安全条件を確認した。網羅異常系/汎用化/文書磨き込みを追加必須にしない。
- 既読review-enforcer/review-worker/work-context-manager/report-output-manager等を継続適用し、同じSol6.1/high要求・applied:null・normal_persistenceでbuilt-in review。新agent/入れ子/独立final/attestationなし。
- e5efb9/beba40/3b998e/a0c61cのgit show/証拠読取・静的照合は各exit0。3b998eのscaffold source照合先を最初は復元用implementation-source.csと取り違えたが、a0c61cで実false scaffoldのred-scaffold.csが記録ed3676...と一致し、復元用は3449ae...であると確認した。製品や証拠の不一致ではない。
- 新test/build/lint/CI/実DCP/execを実行せず、保存済み結果を照合した。自己保存証拠は /tmp/issue81-06a2-normal-review-20261023/。親prefixは保持し子5欄のみ追記する。

### 06A3 初回通常レビュー（固定commit）

- reviewed_commit: a5672531162da9d398b6348746933d0321a58dc7。基準a2dd3584578d88499c3e52fc0d9d97db164cc5c5。同じgpt-6.1-sol/high/fork none要求、applied:null、continuity reuse、normal_persistence、report_attestation_allowed:false。既読のwork-context-manager/review-enforcer/review-worker/sub-agent-task-manager/report-writer/report-output-managerに従う一人のbuilt-in code review。新agent/入れ子なし。
- 正規PowerShell login:false / require_escalated→WSL Pythonで固定commitの全製品・試験・project・設計・台帳差分と直接依存、保存command/raw/metadataを読取した。helper 3c018f/528552/ba3f22/6e3419/8d7eaf/0acc67/b106c2/180d32/4aec1c/bad2e6はexit0。producer検索の一致なしから通知順序を推測していない。大きい表示の省略は限定読取で補い、書込前の再読取出力の省略は既読内容の再表示として扱った。
- handshake-completion manifest-v1は58entry、SHA815add3c6eeceec3c837fa943fc67061cc087491339f61c635530f2b834eb886。focused文書inventory-v1は43entry、SHA7cbe79440bff3646ea0b3a2273ef7080e10ac3e94749bc68f009735a2e1d06b5。各entryのsize/hash、実command/exit、current source hashを照合した。新build/test/lint/CI/DCP/execは発行していない。
- 自己証拠は /tmp/issue81-06a3-normal-review-20261023/。実装reportの空diffに見えるbaseline→同baseline表現だけを、preflightの実際のbaseline→当時の未commit作業木と現在commitへの4source hash対応へ直接訂正した。実質結果・第三者判定・rawは変更しない。

### NR010 単独fix検証（固定commit）

- reviewed_commit: 7cd1c2512872062d80b8d83312f2a19065e81b31。修正前a5672531162da9d398b6348746933d0321a58dc7の初回needs_changes/NR010履歴を保持する。同じgpt-6.1-sol/high要求、applied:null、continuity reuse、normal_persistence、report_attestation_allowed:false。既読review契約を継続し、新agent/入れ子/独立finalなし。
- helper4329f9/e2c375/644843は各exit0。対象commitの製品1file差分、新fixture全体、保存Red/Green/related/build/restoreの実command配列・exit・raw集計、summaryの世代対応を読取した。新test/build/lint/CI/DCP/execは発行していない。
- reports/evidence/issue81-nr010-current-resource-key-20261023/manifest-v1.tsvは23entry、SHA5140cb83b1f14ff93deaff5c93ee1b1b35a8d20aaaa18ad05cc1d21f397605ea。全size/hash一致、不一致0。fixtureの証拠コピーとcommit内testは同byte。自己proofは /tmp/issue81-nr010-normal-fix-review-20261023/。

### 05B2 明示VisionSimulationOnly構成の通常レビュー

- reviewed_commit: b14800065c93343ef7fef18bba92290f19ca958c、base: 7cd1c2512872062d80b8d83312f2a19065e81b31。同じ通常担当、requested gpt-6.1-sol/high、applied:null、reused_existing_agent_profile、normal_persistence、single_agent/decomposition forbidden、report_attestation_allowed:false。既読の正本review-enforcer/review-worker/work-context-manager等を継続適用し、新agent/独立finalなし。
- 正規PowerShell login:false / require_escalated→WSL Pythonで固定差分・直接依存・保存証拠を読取。helper4db4e7/a23ab2/88c4cd/c6cdf4/f8d846はexit0。2db84fはmanifestを旧形式と誤認してheaderをpathとして読んだためgit show exit128/読取helper exit1。書込や試験発行はなく、実header path/bytes/sha256を確認後88c4cdで全照合を完了した。
- vision bundleの21entryとlint bundleの35entryは全size/hash一致。各manifest SHAはf5c3582ec39512ad92579e7d6a488ac9a3d4a9405bac30e3bad6f92e3abcd0ea、7dd0cbb6cab560a6acf4df0fddeabf4b9e01d7e428bef5288b219054defa4303。新build/test/lint/CI/実DCP/exec/画像操作は行っていない。自己proofは /tmp/issue81-05b2-normal-review-20261023/。

### 05B2 実測証拠・記録差分の限定通常確認

- reviewed_record_commit: 03f98c986be388d23acd02417bf5b7bf6704b2fe。製品/試験はb14800065c93343ef7fef18bba92290f19ca958cと不変で、git diff --quietの製品/試験対象はexit0。32pathは実測/文書証拠・台帳・報告の差分。同じrequested gpt-6.1-sol/high、applied:null、reused_existing_agent_profile、normal_persistence、single_agent/decomposition forbidden、report_attestation_allowed:false。新agent/独立finalなし。
- 正規PowerShell login:false / require_escalated→WSL Pythonで限定読取。eb2894/17d23b/50bda7/981b28は各exit0。元NR005のrequired actionを再読し、既存の本番供給・正常受信・strict health基準で判定した。新実測/build/test/lint/CI/外部操作なし。
- 実測manifest SHAbea266f4062a1c0b5ceaf95b290cabecaecad38c403c9b2f6d6a89a4f2d5e64fはworkspace原本と同byte。8entry全size/hash一致、includeInGit=trueの6JSONはcommitと同byte。stdout2038bytes/token可能とstderr0bytesの2entryはworkspaceのみで、本文を表示・転載せずhashだけ照合。Gitに欠けた失敗ではなく明示収納区分。
- 台帳2行のclosure-current-tracking-v2 manifestは16entry、SHA955f60559d4e09052fd232735eb49fdd7f3e03a4da90db40fe64cb9be26f1b4a。全size/hash、12設定/補助script、source/inputとcommand/exit/raw対応が一致。自己proofは /tmp/issue81-05b2-measurement-normal-review-20261023/。

### 05B3 明示GC視覚構成の初回通常レビュー

- reviewed_commit: 54ef302f199e0dc24e000a6fb0101441af42795d、base: 03f98c986be388d23acd02417bf5b7bf6704b2fe。同じrequested gpt-6.1-sol/high、original applied:null、reused_existing_agent_profile、normal_persistence、single_agent/decomposition forbidden、report_attestation_allowed:false。既読正本review-enforcer/review-worker/work-context-manager等を継続する一人のbuilt-in review。新agent/入れ子/独立finalなし。
- helper8b9a02/7b5b04/288ef5/86036b/c84fa9は各exit0。固定commitの全製品・試験・設計差分と直接依存、台帳/報告、保存Red/Green/build/文書証拠を読取。大きい出力の省略は必要なprojection/Host/観測worker/最終command/rawの限定読取で補完した。新build/test/lint/CI/実GC/DCP/execは実行していない。
- reports/evidence/issue81-05b3-gc-standard-resource-20261023/manifest-v3.tsvは71entry、SHAa5a724891567e73538b7c071315d57ace46745aef9ced6253939b929d79f6613。全size/hash一致。自己proofは /tmp/issue81-05b3-normal-review-20261023/。

## 対象ファイル

- 全製品差分はTesting/Duck.Testing.AppHost/Program.csのduck登録と、Tracker/Tracker.Tests/AppHostApplicationModelTests.csの固定先行1test。Program全272行と追加test全29行、既存モデル入口/比較回帰/待機関係を読んだ。新しい関数説明/XMLコメントの追加はなく、英語識別子は翻訳対象にしていない。
- 直接依存: RuntimeHost Program、RuntimeVisionDiagnosticsHttpService、RuntimeHostServiceCollectionExtensions、RuntimeVisionReceiverServiceの受信/ProcessDatagram/RuntimeVisionReceiverDiagnostics、RuntimeVisionPacketBuffer、RuntimeTrackerConfigurationResolver、RuntimeHost/receiver options、RuntimeHost appsettings/Dockerfile、ServiceReceiveDiagnostics、ResourceDiagnosticIdentity、既存HostDiagnosticsCompositionTestsとExclusiveControlPortPreflightを確認。
- 現行主設計114/181–185の内部39108と動的localhost公開、identity付き自身の受信証跡、既存DI/strict healthの契約を照合。所有供給・DCP照合・外部診断の後続全体を今回の成功として扱わない。
- reports/issue81-bridge-integration-implementation-20261023.md受領SHA233d9a3941cc1419099e3d2d99f633035daebead02e22569466ff25341de195c、親tasks05B/05B1の未commit2行差分を確認。phases変更なし。台帳の最終文書補完は未完了であり旧結果を転用しない。
- reports/evidence/issue81-05b-runtimehost-health-tdd-20261023/manifest-v1.tsvは20entry、SHA092cb0e3ca20b6e7626f6b692b357ccba8f5a845ba01db1d4828e42fa72c1239。全size/hash一致、不一致0。

### NR006限定対象

- Program現SHA caaaab79b24daa9a6722ccc269f050e41b518109e31de856063c2e0232bd8501。初回reviewed77734467からの差分はcomparison時にRuntimeHost vision diagnosticsの固定TCP39108をpreflightへ足す4行の削除だけ。ExclusiveControlPortPreflight.csと他サービスの固定端点リストは変更なし。
- AppHostApplicationModelTests現SHA93e66834284a5bc27abd10df3cb9901c313aec1c0b15e4284452db04c8e21873。ComparisonDuckHealthEndpointDoesNotRequireItsContainerTargetPortOnTheHostを追加した以外の全byteが初回固定test69d8772874b8dde7306330a81cad0bf0e3dd66750b2be8b4db17bff080bfd107と一致することを文字列再構成で確認した。製品/test fileは自己編集していない。
- reports/evidence/issue81-05b1-reviewfindings-20261023/のnr006-red/green、apphost-model-class、apphost-build各4fileを実読取。確認時16fileでauthor manifestはまだ存在しないため、独自evidence-snapshot.jsonでこの集合を固定した（SHA3fa367fffe88695412be89f27a9d8b57f2c6014a204e8005a9773515d4dc6b6c）。未確定manifestを検証済みとは記載しない。
- 実装reportのNR005/006追補（読取時SHAec1ad951ec86699080768a171ebadddcb88cbbcd508a0473ad648e30bb2e120d）も確認。NR005の供給元/起動世代は未特定・未解消として区分される。

### 06A読取と根拠

- 実装report06A初版2fc6f19d062388dd6b53d8780c46c80efae9a6bf3e1d7287146d15cd4e2e7b64、Version訂正版f794e3ba49101e46563bf151545032c2e4910ddcee97dde3b044cd5bfc378a7b、親next_unit追記609e20f6264b1789eb1e65169f57fe31ab95c62dbbc2de2da2d522818ef5aa16を区分する。親は06A1でexec/controlmode/socket方向の設計・先行試験を継続するが、実execは未実施。
- 固定Aspire v13.5.4 commit9c1b401dd67746739044f68959cbf4d3d7af93a6。identity-source bundle inventory-v1.tsvは13entry、SHA9d7439b50381b3dec677a6ec1b9a850b79ea886097ab50e1db26dfa2808871ba。全size/hashとsource-manifestの7Git blob SHA一致。ContainerStatus.ContainerId→container.id→公開ResourceEventの実sourceを読んだ。
- 追加CustomResourceSnapshot.cs blob07dfd322d0994ef99feefd73c415e173a075d800、SHAf563c3f1265519828feff34dfa8d1a29fe8fefdf12bddfeaed0011e740c05908の25行はinternal long Version。外部C#からpublic Snapshot.Versionとして使えない。ResourceNotificationService761–805のversionsSeenも初期snapshotの重複除去で、継続全update/DCP engineの単調version保証ではない。
- 現製品Program caaaab79...、test93e66834...は未変更。実イベント取得/engine IDとprocessの結合は今回観測していない。

### 06A1 固定対象・証拠

- 全tracked差分10pathと新規2pathを確認した。新規RuntimeIdentityControlProtocol.cs全260行、RuntimeIdentityControlProtocolTests.cs全247行。ServiceReceiveDiagnostics、RuntimeVisionReceiverService、RuntimeHost Program/DI、既存AppHost Program/model tests、主/比較設計、tasks/phasesを確認。既存strict health、ResourceDiagnosticIdentity、receive attempt/epoch/readiness、通常入口と直接依存も照合した。
- protocol SHA8506a43d3fac16be8d2d7e543d9a848a9586c4e2c511980856a5821cf034e183、5tests SHAa9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06。ledger SHA84200328b26c45115fb44e1a53c16cfb101b7ce576d0334fe8b5bdfb8a94a3a3、receiver SHA58582bb95f4b071abd6459ef6c569577a84dca8cb636233d6a215e3ad6df2f2c、RuntimeHost Program SHA837a4bda88d4e27f6f6bd6498732e84f61edf1a40f29faa7dce3f8c783f66eb1、DI SHA cc7362346261ffa0e115cbf831e41624563f50b2ff29cd511b7e13bf5383535c。
- AppHost Program caaaab79b24daa9a6722ccc269f050e41b518109e31de856063c2e0232bd8501、model tests93e66834284a5bc27abd10df3cb9901c313aec1c0b15e4284452db04c8e21873を保持。固定duck ResourceId/ランダムRunIdの一時env案は撤回済み。outer event→execは未実装、HostedServiceは明示Enabled時だけ動き、実AppHostでは未有効化。
- 実装report受領SHA528af73e3a8e1820081b90bdbc3f6e89248ba5512cc29ed3c637ed48e3be8667。control-red inventory-v2 SHA7375252aed2235475d9bdd74d8b12fe14eaeae07f872c9d4e2222882219286d6。diff-lint inventory-v2 SHA92a0abf1db5ce5a6301db0de2c2c7de93700c930d0e3122a557a020c8f1c8605。
- 初期2test Redは1pass/1fail、拡張4test Redは2pass/2fail（exit1、実assertion）。red-matrixとgreen-v1の4test SHA dbf7f2b00acc039e651d30b6f0dfe6fe647874afdff2ca1aca5b1e64d19dfa29は記録上一致する。green-v2/v3はcompile/setup失敗で機能Redではない。Hosted第5test追加後green-v5は5pass。その後full-v1は同testで785pass/1fail（227行のrelay exit期待0/実1）、startup同期修正後green-v6=5pass、full-v2=786pass/0fail/0skip。RuntimeHost build0warning/0error、AppHost build既存ASPIRE010 warning1/error0、各exit0。

### NR007–009 第1回fix対象と実行証拠

- protocol現SHA5a983432fa7d860eb6421587fbfa76710d9170a2e27bb588b8fb97737f586b37（331行）、RuntimeHost Program現SHA4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117b。元5tests SHAa9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06は不変。
- 新RuntimeIdentityControlBoundaryTests.cs全318行、SHA58c304d7d96c7812a816bd8407c316f1ceb58f22539a1572718446f714ab8993。theory3caseと8Factの計11case。実UDS、occupied regular/socket、own cleanup、silent prepare/commit、oversized server/relay、GatedTextWriter取消の実assertionを確認した。
- reports/evidence/issue81-05b-boundary-tests-20261023/inventory-v2.tsvは79entry、SHA14f058ad6f6b37c61a21df94f4337ff720ab9180694557d637a1b17d2c3e5761、全一致。full-suite-unfiltered-v1の正式argvはdotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=falseでfilterなし、797pass/0fail/0skip/exit0。full-suite-v1の730passはFullyQualifiedName~Tracker.Testsによる部分結果で、旧67が除外される。boundary suite11pass/exit0、RuntimeHost build0warning/0error・AppHost build既存ASPIRE010 warning1/error0、両exit0をrawで確認。
- 固定58c304...でmalformed-prepare-v1、theory-commit-v3、theory-disconnect-v1は後続handoff期待0/実130、owner-socket-v1はpath保存assert、idleclient-red-post007/idlecommit-v2は有限時間内の後続成功assertでruntime Red/exit1。対応Greenは同test hashで各1pass/exit0。全11caseが個々に事前機能Redを持つとは記載しない。oversizeの旧4test版runtime失敗も保存されるが、現在11testとのbyte一致の証拠ではない。camelCase fixture不備、ValueTask compile失敗、製品中間CS1022、v4未回収実行は別履歴。
- metadataの多くはJSONオブジェクト直後にリテラル\\nがあり、strict JSONとして不正。先頭objectのargv/cwd/exit/fixture hashと別rawは読めるが、黙って正規JSONへ直して原本成功証拠としない。inventory-v1やverification-summaryの転送崩れも現物保持。companion整形は別担当の後続で、今回未確認。

### 第2回liveness固定対象

- 今回reviewed RuntimeIdentityControlProtocol.cs SHA b96d255bc7e52fd017bcc9676df994fc5c2331575b1d14cec281d27de7bf67cf。RuntimeHost Program SHA4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117bは前世代と同じ。新RuntimeIdentityControlLivenessTests.cs SHA2d136c3d9ad3320c3fcf61fba38e88da56ede44bd309c637b05643b152731745、129行、2Fact+theory2case。
- 元5tests SHAa9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06、境界11test SHA58c304d7d96c7812a816bd8407c316f1ceb58f22539a1572718446f714ab8993を保持。ledger/receiver/strict healthの前回確認をやり直さず、今回のCompletion・Dispose・HostedService監視・relay deadline差分と直接呼出しだけを確認。
- reports/evidence/issue81-05b-liveness-tests-20261023/manifest-v1.tsvは今回読取時47entry、SHA f2477881d2e5d11d47939af704eea045cbaa0a6ae5b6cecde5e6823fe6134421、全byte数/hash一致。実装report読取時の旧manifest hash bde40...は現在値とは別であり、現sourceの実照合に使わない。writer最終化中の第三者reportは編集しない。
- liveness-suite-green-v1は4pass、boundary-regression-v1は11pass、full-unfiltered-green-v1はfilterなしで801pass/0fail/0skip。全exit0。正式標準argvはdotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=false。RuntimeHost build0warning/0error、AppHost build既存ASPIRE010 warning1/error0、両exit0。source・stdout/stderr hash/bytesはcommand記録と一致した。
- Redの区分: fatal-loop-red-v1/v2はstate-machine反射fixture失敗、hosted-supervision-red-v1はTask派生型をAssert.IsType<Task>で拒否したfixture不備。hosted-supervision-red-v2はExecuteTaskの1秒以内完了assert失敗で有効な監視Red（API seam追加後、監視実装前）。relay-output-red-v2は外部取消前の3秒以内完了assertがwrite/flush両caseで失敗する有効runtime Red。fatal-cleanup-red-v1は旧Assert.Nullが元SocketExceptionを拒否して失敗し、最終fixtureはAssert.IsType<SocketException>へ変更されている。したがって最終4case全てが同一byteの固定期待値Red→Greenとは主張しない。直接cleanupは現sourceと最終Greenで確認する。

- 8組Green/build metadataとb96d255...の対応は0c29b0/263069の読取時に確認した。その後の44d8dc7...は新helper修正中の別世代で、本判定/801成功を移転しない。

### 第3回固定sourceとv3証拠

- protocol全401行、新RuntimeIdentityControlNonCooperativeLivenessTests.cs全136行（SHAc8251b75203047b8555d0c649a3d0b4b148e9778e5fc3c4a2535a83671a7c543）を実読。実RuntimeHost Program4bc18b...の短命relay分岐と入出力・token・終了コード、既存server/HostedService/cleanupへの新helper利用を直接依存として確認した。
- 新fixtureは同期WriteLine/Flushの2case、tokenを無視する非同期Flush/Readの2case。RelayReturnsWhenInputReadIgnoresCancellationTokenは非協力な非同期readであり、同期read prefixを個別実測したtestではない。4caseとも外部取消なしでrelayが戻ったかを先に記録し、その後fixture自身がReleaseする構成。
- noncooperative-io-red-v1はb96d255...で4fail/0pass/exit1。各assertは期限内relay完了の失敗で、build/setup失敗ではない。同test SHA c8251b...の44d8... Greenは4pass/exit0。期待値緩和やskip化なし。
- manifest-v3.tsvはheaderなし85件、SHA960df89bad8b4126916e780908ff326d3e11442962fc18235c4b17c4547988d2。bundle相対とrepo:参照をcommit内へ解決し、全size/hash一致。8組の44d8... metadata/source/raw hash/byte数も一致。
- 現sourceのprotocol5/5、boundary11/11、liveness4/4、noncooperative4/4、無filter805/805・0fail・0skipは各exit0。標準argvはdotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=false。RuntimeHost build0warning/error0、AppHost build既存ASPIRE010 warning1/error0、両exit0。805結果を旧801世代へも新しいPR CI結果へも移さない。
- 元5/11/4testのSHAはa9ef455.../58c304.../2d136c...を維持。protocol-suite-v2のmetadata当初対象リストに元5test hashがなかった限界は、後続記録/manifest内repo:sourceと区分する。個別805caseのTRXを確認した主張ではない。

### 第4回の固定対象・source/証拠対応

- Protocol差分はRunWithDeadlineAsyncのoperationTask保持とfinallyのObserveFault呼出し、fault-only continuation追加（391–415行）。既存待機期限/所有UDSのusing/Completion監視/ownsSocketPath条件は変えていない。Program SHA4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117bも不変。
- 新RuntimeIdentityControlOperationLifetimeTests.cs全162行、SHA30a22fe1c8031f36ea8aec66951555999e0efa7f74d9986fc71dae95f1f9005d。late faultのUnobservedTaskException/GC/WeakReference、timeout後UDS後続接続、実RuntimeHost project outputを起動するchild-process fixtureを全読した。新説明/XMLコメントは日本語。
- manifest-nr009-close-v2.tsvは58件、SHA0330b12b28618b5e8cb74f54725bed6860ffd827f986743c84f4b303d0e6ecb9。全size/hash一致。red-v2は同fixture SHA、旧Protocol44d8...、2pass/1fail/skip0/exit1でlate fault観測assertだけ失敗。green-v1はProtocol94bcc...、同fixture3pass/skip0/exit0。初期test-bin copy起動のexit134/Microsoft.Extensions.Hosting解決失敗はsetup failureとして分離した。
- full-suite-v1はfilterなし808pass/0fail/0skip/exit0。RuntimeHost build0warning/0error、AppHost build既存ASPIRE010 warning1/error0、両exit0。標準argvはdotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=false --verbosity minimal。
- 単独child-process-project-output-v2は1pass/exit0で、fixtureはstdinを閉じずexpected child exit2を35秒上限で検査し、raw duration30秒。旧単独記録のDLL hash f3a3...は後続buildのDLLとは異なるため、現DLLとの一致や94bcc...の単独binary証拠に転用しない。94bcc...に帰属するGreen3/full808が同じchild fixtureを含むことと、後続build成功を別根拠にする。runtimeconfig/depsと既存assetsの現disk hashは保存値一致、DLLの世代差は保持した。
- 主/比較設計の語彙置換16追加行は、完全ID/RunId/ResourceId/Generation/BootId、DCP単一owner、host HTTPで選ばない、未帰属/503、外側未実証の意味を維持する。tasks/phasesの2変更行は現94bcc.../808と通常確認後続を記録し、NR005/TDD/実機未完了を保持する。
- tracking-lint-v2はその2変更行内の追加断片（130/73文字）を2入力にした三検査で、各exit0・timed_out false。preflightのbaseline/current sourcehash、入力hash、9config hashをcommit/保存入力と照合し一致、両断片の現source内連続一致も確認。全文/行全体すべての合格へ拡張しない。全文CSpell/Sudachi未合格と初回語彙指摘rawを保持する。

### 06A2 固定対象・直接依存・証拠

- RegisteredContainerEventProjection.cs全107行、SHA3449ae230e73f6dc0315ea4658c9eee22d9e3994613dc85edbcff13e158e1f3dとRegisteredContainerEventProjectionTests.cs全104行、SHA33493d7830dcf88bc375fc1162cb59848c245cccb24e2f52192c159ee9ba8f94を実読。6Fact+theory4caseの計10case。唯一の製品追加はこの純粋class/recordで、Program/DI/外部実行へまだ接続しない。
- registeredResource参照、登録key、Resource.Nameと期待role、observation/currentStateのRunning、literal container.idの一意なstring/full64hex、両ID一致を確認。識別子を補完せず、候補recordにResourceId/RoleName/ContainerIdを分けて返す。mutable共有state/IO/async副作用なし。新関数説明/XMLは日本語。
- 直接使用するAspire13.5.4のpublic ResourceEvent/CustomResourceSnapshot/ResourcePropertySnapshot/ResourceStateSnapshot.Text/KnownResourceStatesを保存reflection出力で照合。既読固定source9c1b401...のContainerStatus.ContainerId→container.idとの対応を維持し、非公開Snapshot.Version/KnownPropertiesを製品から使わない。
- inventory-v2.tsvは42件、SHA05935bc39adfb75158f570e38665770e3fff6c98380d0088a77e2e87df5015ef、全size/hash一致。保存された最終製品/testのcopyはcommit sourceと同byte。Green/regression/build/API確認のcommand/source/raw hashも一致、各exit0。
- focused10pass、既存AppHostApplicationModelTests25pass（旧test fileはbaselineと同byte）、AppHost build error0/既存ASPIRE010 warning1をrawで確認。今回全標準suite/PR CIの成功は主張しない。
- tasks I81-06A2追加1行と実装reportのscope/TDD/未実装記述を照合。tracking入力は追加行と完全byte一致（756bytes、SHA59d5e6f05bac4244bd4630e93e78e51da0b2dd71097c2484f6a74e3df1913de7）、source hashと9config一致、textlint/CSpell/Sudachi各exit0・raw空。全文gate合格へ拡大しない。
- reconstructed sensitivity runはrootのred-scaffold.cs（SHAed3676...）と同33493d...のtestで8pass/2fail/exit1。red-scaffold/implementation-source.csは復元用の最終3449ae...であり、false scaffoldとして扱わない。

### 06A3 固定対象と直接依存

- 固定commitの13製品/試験/文書path：AppHostのContainerIdentityControlExecSession.cs、ContainerIdentityExecHandoff.cs、Program.cs、Duck.Testing.AppHost.csproj、RuntimeHostのRuntimeIdentityControlProtocol.csとcsproj、ContainerIdentityControlExecSessionTests.cs、RegisteredContainerExecHandoffTests.cs、RuntimeIdentityBootstrapTests.cs、主設計・比較設計・tasks-status・phases-status。各source hashと差分一覧を自己proofへ保存した。
- 直接依存として既存RegisteredContainerEventProjection、ResourceDiagnosticIdentity/ServiceReceiveDiagnosticsのidentity/epoch境界、RuntimeHost DI/CLI relay、既存Dockerfile/WORKDIRと公開Aspire v13.5.4のResourceNotificationServiceを確認。ResourceIdを役割名と同一視しない。TryGetCurrentStateはexact key優先、役割名fallback時にも実際のmatched keyを返す。WatchAsyncは初期snapshotも返す。
- AppHost登録→実HostedService→WatchAsync→current state→投影→明示contextのEngine ID→exact fullID/Running/run-role labels→structured exec→実prepare/challenge/commit clientというコード経路を確認した。RuntimeHostOnly/DesktopBridgeEnabledは明示選択、defaultはbridge未有効化。RuntimeHostの初回未bindはprepareのみでbindせず、commitでkeyを確定し、事前指定keyのstrict動作は保持する。
- 後続未追跡ContainerIdentityCurrentResourceKeyTests.csは今回の固定commitに含めず、修正前判定へ混ぜない。

### NR010 限定fix対象

- Testing/Duck.Testing.AppHost/ContainerIdentityExecHandoff.cs SHA3efc24a224ace87f88065060ec36b467c0f695d41e324feed0bafed8678169a9。最初のresourceId固定を除去し、各observation.ResourceIdからcurrentを取得して同keyでprojectionする298–307行を確認。generation/lastContainerId/lastBootIdは従来どおり有効projection後に進み、handoff受理後に保持する。role/登録参照/fullID両snapshot一致、engine/owner検査を迂回しない。
- Tracker/Tracker.Tests/ContainerIdentityCurrentResourceKeyTests.cs SHAbe11812e9dd0f8645bbde95415f38af7235940ecc5efd15e176d7c4b3a2915d8。実ResourceNotificationServiceと実ContainerIdentityHandoffServiceへ同じ登録IResourceのStarting/ID無しkey A→Running/fullID有りkey Bを公開し、1回だけBのResourceIdでrelayされたことを確認する。Docker runnerだけfakeであり、helper投影だけの試験ではない。
- a567からの製品/試験差分は上記2path。既存06A3全範囲を再評価せず、raw/summary/製品patchの対応だけを確認した。今回新たな関数説明/XMLコメントの追加・変更はない。

### 05B2 対象と直接依存

- Program.csの追加61行、AppHostApplicationModelTests.csの追加51行、主設計の1段落、tasks/phases現在値、実装report追補、前回通常report保存分、2証拠束を確認した。製品SHA63075593c8ab5619d0bd88ed213d9c130b6d8c1c960d58b07df89920593f4d69、試験SHA933d3fcd60e136a7dd2352f7e52439452b175e6ace3ad39ba03fdb77093c8068、主設計SHAd259bcbd3d5722a97da2ff66f2715c7f67b3d2c38c68fc71a9be4f491d80ed1fは成功記録before/afterと一致する。
- 明示VisionSimulationOnly分岐はRuntimeHostOnly分岐の後、旧base/comparison分岐の前にあり、登録後returnする。2資源はsimulatorとduckのみ。既定/単独選択と従来base/comparisonの本文は変更されない。StackOwnershipLease、AddPublishedContainerの既存Release publish context、既存identity bridgeを再利用する。
- simulator固定indexはlinux-image-lock.jsonのframework-simulatorと一致し、tag overrideは不一致を拒否する。WithEntrypoint(tini)と引数 -- ./bin/simulator-cli -g 2020B --realism None --ibis-port 12346 --ibis-team-color yellowを確認。既存Aspire.Hosting13.5.4のXMLはWithEntrypointをコンテナentrypoint設定、ContainerResource.Entrypointをその公開propertyとして記載する。専用annotationの不存在はentrypoint指定不能を意味しない。
- duckはsim profile/ContainerMode/identity control RunId・roleを明示し、VisionReceiverの224.5.23.2:10020を既存options/DIへ渡す。WaitForStart(simulator)で起動順を作り、循環依存なし。TCP target39108/host動的/127.0.0.1/nonproxy、GET /health/visionの既存strict判定を保持。UDP endpoint/host network指定を追加しない。RuntimeHostのreceiver/診断/HTTPは変更されず、新受信を伴わない503をreadyへ読み替えない。
- 試験は実AppHostモデルを構築して2資源・固定image/digest・args callback・環境・UDP非公開・health/待機依存を確認する。新規関数説明/XMLコメントは追加されず、追加inline説明は日本語。

### 05B2 実測・記録の確認範囲

- reports/evidence/issue81-05b2-dcp-vision-run-b148-20261023/のmanifestと6JSON、closure-current-tracking-v2の16entry/初期preflight失敗記録、tasks/phases各変更1本文行、実装report追補、前回通常report保存分を確認した。製品の再網羅や新しい条件を追加しない。
- 実測run68fd7e39e18e4968b18450730eca8651、明示Desktop Engine5bc5ab8a-7340-47ba-8b7c-9f645375a54c。実HTTPのidentity4値をDuck inspect投影と突合し、RunId=所有run label、ResourceId=duck-zhjxnngc（DCP name label）、ContainerId=3b158cd0ca4d1394b66e390c9cd0e6722897d7faae035055d1c5f03e036b6216（完全64桁）、Generation=1が一致する。役割duckと資源キーを同一視しない。
- 固定simulatorとduckの同じAspire user bridge/network ID、実entrypoint/args、duckの127.0.0.1:16537→39108/tcp、UDP host mappingなしを保存inspect投影から確認。HTTP200/IsReady=true/Reason Ready、VisionPacketsReceived10170を保存health投影から確認した。
- 実装reportの「両fullIDへのinspectがno such object」という1表現だけを、保存JSONの実際の根拠である「run label query空（duckを含む）＋simulator exactID inspectでno such object」へ直接正確化した。両資源不在という結果・判定・rawは変更しない。

### 05B3 固定対象・直接依存

- Program.cs追加75行（SHAcf6888ceba02dd2776b8ccc189c243054a1ba0ab03c24e73d57fc02d68776fcc）、AppHostApplicationModelTests.cs追加56行（SHA48cf90d3cfe87cb2623e3bbca4becee7d49a1cf8eeb4a675e6414a64ad9e1e9a）、主設計追加4行、tasks/phasesの05B3計画同期、実装report追補・旧report軽微修正、通常report前回保存分、証拠束を確認した。
- 直接依存はlinux-image-lock.json、既存05B2 simulator実entrypoint/command観測、RegisteredContainerEventProjectionのrole/key/fullID条件、EnableDesktopIdentityBridgeとHandoffService、RefereeDriverApplication/Host/観測worker、既存publish定義。既定base/comparison/RuntimeHostOnly/VisionSimulationOnlyの既存分岐本文は不変。
- GC固定index/既存3multicast引数、UI target8082/host動的/127.0.0.1/nonproxy、UDP endpoint/host network無しを確認。Driverは内部ws://game-controller:8082/api/controlへ接続し、Enabled/ContainerModeのみ明示する。AutoDriveToActiveをtrueとした場合だけ制御workerを登録する既存Application60–72行を保持し、この分岐は設定を渡さないため既定observe-only。
- 構成試験は4資源型・GC固定image/args・公開端点・Driver publish/環境・待機辺を検査するが、新分岐simulatorのrepository/binary名、duck環境/owner roleを検査しない。AutoDrive不存在のassert対象はAutoDriveというキーで、実制御キーAutoDriveToActiveはsourceで別途確認した。新しい関数/XML説明の追加・変更はない。

## 指摘事項

### I81-NR-005 / P1 / open — 新たに有効化したstrict healthへ必須identityが供給されない

- location: Testing/Duck.Testing.AppHost/Program.cs:103–105（新しいContainerMode/HTTP health登録）。
- production path: RuntimeHost Program8–10でContainerModeを見て実HTTP serviceを起動する。RuntimeHostServiceCollectionExtensions67–71はTesting:DiagnosticsのRunId/ResourceId/ContainerId/GenerationをParseIdentityへ渡すが、実duck登録・RuntimeHost appsettings・Dockerfileのどこにもこの4値の供給がない。AppHostのstackIdもduckへ渡されず、DCPの一般的な環境値がこの独自キーへ自動変換される実装もない。
- ServiceReceiveDiagnostics20–27ではgeneration未指定でIdentity=null、150–158ではreadiness stateを作らない。正常Detectionを繰り返してcount/freshnessが成立してもGetSnapshots140–145はIsReady=false/Unattributedとなる。RuntimeVisionDiagnosticsHttpService28–32はContainerMode=true時これを503へ変換する。したがって実duckの新しいAspire healthは通常生成構成で準備完了へ到達できない。decode失敗を安全側へ倒すこと自体は正しく、問題は成立可能な本番供給を結ばずhealth接続完了としている点。
- 既存composition evidence: HostDiagnosticsCompositionTests25–43の200経路はConfig300–308で完全identity4値を手作業供給する。MissingOwnerAttributionIsStrictOnlyInExplicitContainerMode230–241は正常成功を記録しても503を期待する。新モデルtest404–430は注釈の存在だけを見てこの違いを検出しない。
- scope: 今回新たに有効化するduck自身の準備経路にあるcapability gap。外部trackerのG3や全UDP移行・実DCP受入を前倒し要求しているのではない。既存schemaに従う完全ID・正の世代等を信頼できる資源所有側から供給する経路を明確にする必要がある。
- required action: 採用する正しいidentity供給/起動世代更新を実duck構成へ接続し、その実構成からDI→実parser→GetHealthまでを通すfixtureを追加する。供給成立かつ複数の新鮮な正常Detectionで200、欠落・不正/古い帰属で503を固定する。偽の64桁値・名前からの推測・strict判定緩和を修正にしない。供給機能が未実装ならhealth機能の完成/ready接続を主張せず、必要な実装境界を未完了として扱う。

### I81-NR-006 / P2 / open — 動的host port化後も旧固定39108のpreflightが比較起動を拒否する

- location: Testing/Duck.Testing.AppHost/Program.cs:104/109、関連する同file56–60。
- 新登録はvision-healthの内部TargetPortだけ39108にしhost Port=nullへ変更した。一方comparison分岐の旧preflightは依然ホストTCP39108を必要端点へ加える。ExclusiveControlPortPreflight84–99はIPAddress.Any:39108へexclusive bindし、占有時77–79で例外を送出するため、別プロセスが39108を使うだけで資源登録前に比較AppHost生成が失敗する。
- impact: 実際には動的host portへ割り当て可能な構成を拒否し、旧固定host port依存が残る。主設計114/181/183と今回の動的host契約に不整合。port39108が空いていたclass24成功では検出できない。
- required action: 本当に固定ホスト端点を使う他サービスのpreflightは保ち、duckの旧39108固定probeを現在の動的割当契約へ合わせる。ホスト39108を別のlistenerが占有していても実comparison AppHostモデルが生成され、vision-health.Port=null/TargetPort=39108/loopback/non-proxiedが保たれる競合fixtureを先行固定して修正する。reviewerは新しいlistener試験を実行していない。

### held / 非阻害 / 証拠不足

- 元05AのI81-NR-004/P1/closed、他過去closed findingは変更しない。新規必須は上記2件。
- 新healthのroute名/target39108、ContainerMode、UDP非公開、WaitFor(simulator)保持はsourceに接続されている。simulatorはduckを待たず、duck→simulator、crane→duck started等の関係に新しい循環は見つからない。
- RuntimeHost単独入口/既定options、wrapper入口、外部資源型・既存制御契約は今回未変更。AppHostからはbaseでもContainerModeが新たにtrueとなるためstrict healthへの移行をNR005で評価した。
- 構成annotationだけの新testはHTTP handler/owner注入や固定port競合まで測らず、この不足をclass24passで埋めない。実HTTP応答・実UDP/DCP・Windows bindは未実施。

### I81-NR-006 / P2 / closed — 固定39108 preflightのみを削除し実占有fixtureで確認

- 初回P2のseverity/originは保持し、限定fix検証でclosedとする。現在のProgramはcomparisonでも39108を固定host必須リストへ加えず、duckのvision-health.Port=null/TargetPort39108の登録を保つ。他サービスのpreflight保護は残る。
- 新fixtureはTcpListener(IPAddress.Any,39108).Start()で実際に占有してから、実Programを通すCreateAppHostAsync("--Testing:Mode=comparison")を呼ぶ。fixture内でTargetPort39108/Port nullを検査し、usingによりlistenerを解放する。模型の別登録やpreflightを迂回したhelperではない。
- 先行Redはfixture自身のStartではなく、実Program60→EnsureAvailable79→Probe96のbindでAddressAlreadyInUseを発生し1fail/exit1。4行削除後の同fixtureは1pass/0skip/exit0。同じtest SHAとargvを保持し、固定期待値緩和/skipなし。
- loopback/non-proxiedは変更されていない共通登録sourceと、同class内の既存モデル回帰/元05B1固定testで確認する。実DCPのポート割当を起動測定した証拠ではない。

### I81-NR-005 / P1 / openを保持

- 今回の4行削除はidentity供給を追加していない。現在Program99–101付近でContainerModeとstrict healthを有効化しつつ、必要なRunId/ResourceId/完全ContainerId/Generationが未供給という初回根拠は変わらない。
- class25成功はモデル生成の回帰であり、実duckのDI/parser/GetHealthで正常受信後200へ遷移する検証ではない。P1のseverityや必要対応を引き下げず、全体合格にしない。

### 06A助言：整合する部分と未成立の契約

- RunIdをAppHost process毎にし、再利用可能なStackIdと分離する案は妥当。ResourceEvent.Resourceを登録済みIResourceへ結び、ResourceIdは通知unique key、Nameは許可役割selectorとする。container.idは正確なproperty名/string型/一意な64桁hexだけを採る。同snapshotのexpected URLは発見用候補でありreceiver processの証明ではない。
- Version公開利用の撤回は正しい。owner独自arrival seqは局所workの直列化/失効用に限り、DCP新旧の証明へ使わない。退役したContainerIdの遅延再通知を新しい現行IDとして復活させず、再確定不能ならfail closedとする。
- Generationを別ContainerId時だけ増やす案では、同engine ID内のreceiver process再起動を区別できない。receiver起動毎BootIdとbindingを結び、ContainerId変更に加えBootId変更や既知の起動無効化後の再bindingを新世代へ進める契約が必要。単なる同一通知/URL更新では増加させない。
- host HTTP+nonceだけではnonce応答processとengine IDを束縛できず、再作成後のURL再利用競合が残る。公開eventの再読込だけで原子的bindingになったと扱わない。
- 現ServiceReceiveDiagnostics.IdentityとRuntimeVisionReceiverDiagnosticsのepoch参照は起動時固定。IConfigurationだけを書換えても既存singleton/進行中ReceiveAttemptは更新されない。identity/epoch/診断resetを同じ同期境界で行い、旧attemptを新identityへ記録できないAPIが必要。
- 旧外部senderのpacketを新receiverが復号した事実は、receiver自身のidentityで記録できる。sender engine所有を証明する主張とは別であり、外部packet帰属は06B等へ残す。これをNR005 receiver bindingの新たな全体前提へ混ぜない。

### 最小の受渡し候補（親の採用方向、未実装）

1. ownerは固定版public eventから候補full IDを採り、同じ明示engine/contextのそのIDだけへ固定control commandを実行する。名前/短ID/host URLへfallbackしない。既存自社RuntimeHost実行体の専用modeを候補とし、追加外部binaryや通常起動の変更を前提にしない。DCPのcreate/stop/remove所有は維持する。
2. control clientはそのcontainer内のreceiverが持つprivate UDSへ接続し、起動毎BootId/challengeを同じ接続で取得する。ownerのRunId/ResourceId/full ContainerId/Generationをexpected BootId付きでcommitし、receiverがcurrent session/世代を検査する。engineのfullID対象選択と資源内socketが宛先の根拠になり、host URL再利用をhandoff選択から外す。
3. 応答も同じtuple/BootIdを返し、ownerは自分のbindingが途中で変わった遅い応答を採用しない。旧Run/別resource/古い世代/同世代別ID/別BootIdは拒否する。同一tupleの重複は冪等でも、診断の新しさを延長しない。
4. binding前、不正、既知の失効/置換は503。新identityへ旧証跡を引継がず、受理後の新しい正常Detectionが既存進捗/新鮮度を満たしてから200。新Runが残存receiverを暗黙に引継ぐことは認めず、明示handover/新bootのどちらを使うか未定なら拒否を保つ。

### 06A1 初回正式findings（修正前、重大度順）

#### I81-NR-007 / P2 / open — 不正な1接続が常駐accept loopを終了させる

- location: Tracker/Tracker.RuntimeHost/RuntimeIdentityControlProtocol.cs:80、92、102、132。production pathは実HostedService→server.StartAsync→AcceptLoopAsync→HandleClientAsync。JSON parse例外やpeer切断時のIO例外を接続単位で処理せず、唯一のacceptTaskへ伝播させる。
- 同file207は別の無限Delayだけを待ち、acceptTaskのfaultを監視しない。したがって一度の壊れたprepare/commitで受信loopが終わってもHostedServiceは稼働扱いのまま、以後の正しいidentity受渡しを受理できない。Dispose149–154でもfaultが再送出され、socket cleanupへ到達しない経路がある。拒否して次の正しいhandoffを受ける契約と失敗cleanupを満たさない。
- required action: 不正JSON/接続切断等の予測可能な接続失敗をその接続だけの拒否・解放として扱い、次のacceptを継続する。listener自体等の致命的faultはHostedServiceへ確実に伝え、稼働を偽装しない。例外時も所有socketの後始末を保証する。
- focused fixture: 実UDSへmalformed prepare、malformed commit、challenge途中切断を各々送り、identity/evidence不変、その後の正常prepare/commit成功を確認する。shutdownでfault/未回収task/socket残留がないことも固定する。現在5testsはこれを測っていない。reviewerによる新実行はなし。

#### I81-NR-008 / P2 / open — 起動拒否した他者のpathをDisposeで削除する

- location: Tracker/Tracker.RuntimeHost/RuntimeIdentityControlProtocol.cs:63、145–154、201–203。
- StartAsyncは既存pathを見つけるとbind前にIOExceptionを送出するが、HostedServiceのawait usingはそのserverをDisposeする。Disposeはbind成功/作成所有を問わずFile.Exists(socketPath)ならFile.Deleteするため、競合で保護すべき既存ファイルや他listenerのsocket pathを削除する。自分の失敗cleanupが他者の資源を破壊する。
- required action: 成功して所有したsocketだけをcleanupする状態を保持し、起動失敗時は既存pathを変更しない。cleanup失敗を含め元の起動失敗を不明にしない。名前だけの一致を所有証明にしない。
- focused fixture: 既存regular fileと実listenerの占有pathへStartを試み、拒否後Disposeしても元のbyte/path/listener到達性が保持されること。自分が正常bindしたpathは停止後削除されること。現在5testsには占有/起動失敗cleanupがない。

#### I81-NR-009 / P2 / open — 1接続の無期限・無上限読み込みで全handoffを塞げる

- location: Tracker/Tracker.RuntimeHost/RuntimeIdentityControlProtocol.cs:80、88–101、239–249、および Tracker/Tracker.RuntimeHost/Program.cs:7（CancellationToken.None）。
- serverは接続を直列処理し、prepare/commitのReadLineAsyncはhost停止tokenだけで待つ。1024はStreamReaderのbufferサイズで、1行の長さ上限ではない。接続したclientが改行またはcommitを送らなければ以後の正しい接続も処理されず、長い1行は制限なく保持される。実CLIもNoneのままstdin/socket待機し、短命control modeが無期限に残り得る。任意の全資源lease/heartbeat要求ではなく、現在追加した接続の進行・メモリ境界の欠落である。
- required action: server/clientそれぞれのフレーム長と接続/各段階の有限待機を明示し、timeout/取消/超過時に接続とpending IOを解放する。write/flushにも取消・待機境界を適用し、次の正常handoffを妨げない。具体的な時間値・上限値は用途に基づく実装選択であり本reviewで新global gateとして固定しない。
- focused fixture: silent prepare、prepare後silent commit、上限超過行、取消、相手が読まない応答を有界に終端し、その後正常接続が成功すること。relayのstdin/challenge/result待機も有界に終了しexit/cleanupを確認する。既存StringReader事前入力の正常fixtureだけでは裏付かない。

### 06A1 機能確認済み・工程未達・heldの分離

- identity置換はepochGate→ledger gateの順で同期し、旧ReceiveAttemptを新epochへ通さない。ledger内identity/証跡resetは同lock、同tuple再送はepoch/countをresetしない。現sourceに逆lock順は見つからない。run/resource不一致、低世代/同世代別ID、BootId不一致、prepare/commit tuple不一致の拒否を確認。新identity後は既存strict healthが503となる。
- 実UDSを通す5fixturesとRelay.RunAsyncは実装を使う。一方RecordValidDetection helperは実parserではなくledger.RecordSuccessを直接呼ぶ。Hosted第5testは手作業生成したHostedServiceであり、実GenericHost/DI/Program CLIプロセス全体、実受信→handoff→新decode→HTTPの組成を測らない。外側DCP/exec未実装と区分する。5/786成功で007–009の未測定境界を合格にしない。
- PD-I81-06A1-TDD: HostedService第5testは製品追加後で、事前機能Redの証拠なし・例外承認なし。後発startup raceのfull-v1失敗はその回帰修正のRedであり、初回実装前Redへ読み替えない。先行4matrixのRedや今回review依頼もwaiverではない。機能findingとは別の工程未達として保持。
- I81-NR-005/P1/openはouter実ID供給と実構成未接続のため維持。I81-NR-006/P2/closedと過去closed findingsは維持。新007–009は06A1部分単位自体の必要修正であり、後続全体を前倒しした指摘ではない。
- 文書追加行のtextlintはexit0。CSpell/Sudachiはexit1、表示上限を含む未合格として保持。Yomiyasu主設計はbaseline/currentとも100/exit0、比較設計は91/exit1で既存rule分類・件数を保持する。全文gate/図本人確認は別held。新しい関数/XML説明は日本語で、識別子/API名/コード例を翻訳対象にしない。

### NR007–009 第1回限定fixの処分

#### I81-NR-007 / P2 / open（部分修正）

- protocol116–123が接続ごとのJsonException/IOException/SocketException/ObjectDisposedExceptionを隔離したため、不正prepare/commitと切断後に次の正常handoffが成功する。元findingの主要な接続回復は、固定fixture Red→Greenで確認した。
- 残action: protocol276は依然無限Delayを待つだけでacceptTaskを観測しない。AcceptAsyncの非取消faultや接続処理で捕捉されない致命的faultが起きると、受付終了がHostedServiceへ伝わらない。またDispose214–224はacceptTaskの非取消例外で所有path削除/CTS解放へ到達しない。これは初回required actionの未完了部分である。
- 必要変更/fixture: serverの受付終了/faultをHostedServiceへ伝える契約と、finallyによる所有cleanupを実装する。listenerまたは受付taskの致命的失敗を決定的に作るfixtureで、host側へ失敗伝播し、正常稼働を偽装せず、所有socketとtaskが終端することを確認する。新しい全体gateではない。

#### I81-NR-008 / P2 / closed

- protocol100–101でBind成功後だけownsSocketPathを立て、219–223は所有flagがある場合だけpathを削除する。起動前に占有を拒否したserverのDisposeは他者pathを削除しない。
- 実socket ownerのpath保持/後続handoff成功とregular fileのbyte保持、自分の正常socketはDisposeで削除される3fixtureを確認。socket競合の先行Red→固定test Greenがあり、元findingの必要変更を満たす。後から別processがpathを差し替える別脅威まで検証済みとは主張しない。

#### I81-NR-009 / P2 / open（部分修正）

- framing35–59は64Ki文字上限と取消付きread deadline、server67/135/156は各stage1秒、relay303/311/315/318はread30秒を追加した。silent prepare/commit後の回復、oversized拒否、外部tokenによるGatedTextWriter待機取消を確認した。
- RuntimeHost ProgramはCtrl+C tokenをrelayへ渡す。途中共有した「現在もNone」は旧入口参照だったため訂正する。ただしCtrl+Cは有限deadlineではない。
- 残action: server207–208のWriteAsyncはtokenも期限もなく、relay306のConnect、310/313/314/317/320/321のwrite/output/flushは外部token依存で内部期限がない。実CLIは自動期限付きtokenを作らないため、Ctrl+Cのない詰まった出力では依然無期限となる。GatedTextWriter fixtureは300ms外部取消後にReleaseし、実取消なしの有限終端やpending write解放を測らない。
- 必要変更/fixture: 各接続/出力段階に有限deadlineを適用し、tokenを無視するpending IOも資源解放または明確な終端契約で残さない。外部取消なしのblocked output/flush、必要なconnect/socket write、取消された入力のcleanupを有界に検査する。具体時間値は実装選択でありglobal lease/heartbeatは要求しない。

### 限定証拠・工程履歴

- NR005/P1/open、NR006/P2/closedを維持。NR008だけを閉じ、NR007/009の元P2 severityは変更しない。PD-I81-06A1-TDDは第5testの事前Red欠落として未充足。後続boundary Redや797成功はwaiverではない。
- 軽微転記として実装report追補内のBEL化したa9ef...、CR/tab/form-feed/vertical-tabで壊れた既知path/ファイル名/endpointだけを原sourceと記録名に合わせて直接整文する。実質結果/判定は変えず、raw/evidence原本は変更しない。

### NR007/009 第2回限定fix処分

#### I81-NR-007 / P2 / closed

- protocol94のCompletionは実acceptTaskを公開し、HostedService293は無限Delayからserver.Completion.WaitAsync(stoppingToken)へ変更。致命的受付faultがBackgroundServiceのExecuteTaskへ伝わるため、hostから観測できる。
- Dispose216–238はaccept faultを保持し、finallyで自所有socket pathを削除してCTSを解放し、cleanup後にExceptionDispatchInfoで元例外を再送出する。既存ownsSocketPath条件を維持するためNR008の他者path保護を落としていない。
- 実UDS listenerを閉じるfixtureで、server acceptTask fault・元SocketException伝播・所有path削除を確認。HostedService fixtureは既定nullのserverFactory seamで同じserverを供給し、実StartAsync→accept loop→ExecuteTaskを通してStop前のfault観測を固定する。GenericHostの起動全体や実DCPの検証ではないが、今回の未監視taskと例外cleanupの必要対応に直接対応する。監視の有効Redと最終Greenを照合しclosed。direct cleanupの旧例外期待変更は証拠履歴に残す。

#### I81-NR-009 / P2 / open（追加修正後も限定残action）

- relay309/326–344はconnect/write/output/flushに既定30秒のoperation timeoutを追加し、通常callerの外部取消がなくても期限tokenを渡す方向へ改善した。今回4caseのうちrelay2caseは150ms timeoutを注入し、外部取消前に非0・非130で戻り、次の正常handoffも成功する。
- 残location: protocol355–363。RunWithDeadlineAsyncはoperation(deadline.Token)が返したTaskをそのままawaitし、期限tokenを無視するTaskを待機側で打切らない。output.FlushAsync(token)がnever Taskを返す実装なら、CancelAfter後も359で待ち続ける。今回fixture127はnever.Task.WaitAsync(cancellationToken)を自ら返す協力的flushなので、この残欠を測っていない。TextWriter引数にその協力性の契約は明示されていない。
- protocol209–210のserver WriteAsyncも依然token/期限なし。Framing45のreadとrelayのoperation呼出しは、API呼出し自体が同期blockするprefixをdeadlineで中断できる構造ではない。現在fixtureは即Taskを返すStringReader/custom writerであり、実Programが渡すConsole.In/Outや非協力的writerの有界性・基底IO解放までは裏付けない。これは当初のNR009「外部取消なしの有限終端/pending IO解放」の残りで、新global gateではない。
- required action: tokenを無視するFlush/read taskでもrelay待機が内部期限で戻る契約を実装し、timeout後に接続/保留IOをどう終端・観測するか明示する。server応答writeにも必要な期限/停止契約を適用する。実CLIの標準入出力について、同期呼出しprefixも含めて有界になる根拠か採用する有限終端機構を示す。単に未観測のworker taskを増やして成功としない。
- focused fixture: FlushAsync(token)がtokenを無視してnever Taskを返す場合、同期prefixをgateする場合、必要なserver応答write/入力取消を、外部取消なしで有界終端してcleanupを確認する。現在の4/11/801成功はこれらの代替ではない。新試験はreviewer自身では発行していない。

### 第2回の証拠・工程境界

- NR008/P2/closed、NR006/P2/closedを維持。NR005/P1/openは実owner→receiver供給未実装のまま。PD-I81-06A1-TDDの第5test事前Red欠落、旧PD-I81-02-TDD、全文文書gate/図/実機は未解消。
- liveness bundleのcompanionは元boundary metadataのリテラルbackslash+nだけを別記録へ解釈し直し、原本を残す方式。元rawを編集した成功証拠とはしない。旧v4 process終了と元exit未回収も後続成功で埋めない。

### NR009 第3回処分 — 待機期限と基底IOの扱いを分ける

#### I81-NR-009 / P2 / open（残actionを限定）

- 修正済み: protocol393–394は同期prefixをTask.Runへ移し、そのoperationTaskを期限token付きWaitAsyncで待つ。token非協力のTaskでもcallerの待機が有限になる。server137/161のread、215–219の応答write、relay332–365のread/connect/write/output/flushへ同helperを適用した。前回の「server write期限なし」「非協力flushでhelper自体が永久待機」は現commitでは解消した。
- 残location: Tracker/Tracker.RuntimeHost/RuntimeIdentityControlProtocol.cs:387–399。WaitAsyncがtimeoutした後、operationTaskは保持/追跡/継続観測されない。外部IOが後から失敗する場合、そのtask faultをこの実装は観測しない。relayがusingで所有UDSを閉じた後、遅延したread/writeがObjectDisposedException/IOException等へ至る経路もある。新fixtureは32/60/77行で自分がReleaseし、基底taskを解放しているため、製品による後始末や遅延fault観測を証明しない。
- 適用境界: helperはinternalであり、一般公開された任意IO停止APIではない。取消を無視する任意のTextReader/Writerを強制停止する保証は要求しない。RuntimeHost Programのrelayは専用短命processとして戻り値をEnvironment.ExitCodeへ設定して通常hostを作らず終了する構造なので、caller所有Console IOの最終収束をprocess lifetimeへ委ねる案は成立候補。ただし実Console IOを詰まらせたCLI processの終了は今回fixtureで測っておらず、成功と扱わない。
- 最小必要対応: timeout後のoperationTaskを追跡して遅延faultを確実に観測し、終了/取消に伴う想定内IO失敗を明示する。自分が所有するUDS/socket等は中断/解放の責任を持ち、caller所有stdioは停止不能な場合の短命process境界を明記する。helperの保証を「有限の待機」と「基底処理は協力取消または所有process終了まで残り得る」に正確に分ける。新たな全資源leaseや任意オブジェクトの強制killは不要。
- 必要fixture/証拠: timeout後に遅延faultするoperationが未観測にならないこと、所有接続が解放されることを固定する。Consoleの収束をprocess終了へ委ねる場合は、その実運用境界に限るchild-process/stdioの有界終了fixtureを示す。Releaseでfakeを手動収束させただけの4caseから実CLI cleanup成立を外挿しない。reviewerは追加実行していない。
- 元要件との関係: 初回NR009の有限IOと第2回required action「timeout後の保留IOの終端・観測を明示、単に未観測worker taskを増やさない」に対応する残りである。待機期限そのものの修正は評価し、新しい一般IO強制停止契約へ拡大しない。

### 他finding/工程の扱い

- NR006/007/008はclosedを維持。新helper利用でもNR007のCompletion監視/finally cleanupとNR008のownsSocketPath条件は保持される。再openを要する差分は見つからない。
- NR005/P1/openは外側の実DCP identity供給未実装のため維持。PD-I81-06A1-TDD、旧TDD履歴、文書lint未合格、図/実機/独立finalは別held。新固定4caseの有効Red→Greenで過去の事前Red欠落を補填しない。

### I81-NR-009 / P2 / closed — 有限待機・遅延fault観測・所有境界を確認

- protocol401–415のfinallyは、未成功operationTaskへOnlyOnFaulted/ExecuteSynchronously/TaskScheduler.Defaultのcontinuationを付け、completed.Exceptionを読む。callerがtimeoutで戻った後のfaultを未観測のまま捨てない。成功/cancelはfaultとして偽装せず、強制停止保証も加えていない。
- 固定late-fault fixtureは旧44d8...でmarker付きUnobservedTaskExceptionを検出して失敗し、94bcc...で不検出かつWeakReference target解放を確認して成功。単なる試験都合のfake Releaseではなく、今回必要だった遅延fault観測に直接対応する。
- UDS fixtureは取消非協力のNeverCompletingTextWriterを解放せずにrelay exit2を確認し、同じserverへの次のprepare/commitがexit0で成功する。所有UDSはRunAsyncのusingで閉じる。caller所有TextWriterそのものを強制停止した主張ではない。
- 実CLIはProgramの専用分岐でConsole.In/Outを使い、relayの終了コードを設定して通常hostを起動せず戻る。実project outputのchild fixtureはstdinを開いたまま待ち、約30秒の既定期限でprocess exit2を検査する。この実運用の短命process境界を、内部helperの有限待機と組み合わせて受け入れる。全ての任意TextReader/Writerを停止する新保証は要求しない。
- 前回required actionの最小範囲である期限後fault観測、所有接続の解放、caller stdio/process lifetimeの具体化とfixtureが揃ったためclosed。実stdout詰まりのchild-process実測、全OS/実DCP execの検証まで拡大していないことは未検証境界として残す。新規必須findingなし。

### 第4回の既存finding・工程

- NR006/007/008closedを維持。今回差分はこれらの所有保護/監視/cleanupを緩めない。NR005/P1/openは外側の実identity供給が未実装であり、NR009closedで代替しない。
- PD-I81-06A1-TDD（初期Hosted第5testの事前Red欠落）は未充足、waiverなし。今回同3fixtureの有効Red→Greenはこの最小修正に帰属し、過去の欠落を埋めない。旧TDD/全文文書gate/図本人確認/実機/独立finalも保持。
- 親prefixの旧b96/44d8未評価という記載はそのまま保存する。今回の最新reviewed_commitと閉鎖判断は子欄の本履歴に明記し、親欄の判定を代行編集しない。

### 06A2 初回所見

#### 必須製品finding: なし

- 正常系では登録済みresource/key/roleが一致し、実行中の両snapshotに同じ一意な完全IDがあると、3識別子を混同せず候補を生成する。Sourceと10caseはこの純粋処理の受入に対応している。既存AppHost登録/既定モード/前単位の制御プロトコルに製品変更はなく、必要な正常系を妨げる欠陥は見つからない。
- 欠落/短ID/nonhex/非string/重複、非Running、別resource/key、両観測ID不一致の拒否はsourceと既存fixtureで確認。拒否時target=null。追加の対称異常系全組合せ、汎用化、名前の磨き込みは今回の必須へ加えない。

#### 工程未充足: 06A2の実装前Red欠落

- ResourceType必須項目漏れはcompile/setup失敗。資源Nameを登録keyと同一にして期待roleと違わせた初期2失敗は誤fixtureであり、製品不備のRedに数えない。その訂正時点ですでに製品が存在した。
- 訂正後10caseでfalse scaffoldへ戻した8pass/2failは感度確認として有効だが、事後の再構成であり事前機能Redの証拠ではない。初期raw未保存も実装reportに明記されている。TDD準拠/例外承認/過去の欠落解消とは認定しない。
- この履歴は機能判定と分けて保持する。新しい失敗再現や履歴の作り直しを要求せず、後続の正常系接続を止める追加条件へしない。

#### 安全境界と任意改善

- currentStateはcallerが渡した公開snapshotであり、projection自身は「現在性」や通知の時系列順序を獲得しない。両者が同じ旧IDなら候補は作り得る。候補投影の契約どおりであり、owner/engine authorityの証明ではない。現unitの未実装scopeを機能欠陥へ混ぜない。
- 実ResourceEvent購読、registered keyの実取得、DCP/CLI同engine照合、generation/BootId更新、exec→UDS接続は後続。NR005/P1/openは維持し、正しい形の候補を作れたことだけで閉じない。ここで新たな任意のheartbeat/網羅異常系/全外部診断を要求しない。
- 任意改善は後続callerが接続された段階の代表的な追加caseに留める。現在の10caseに対して追加試験や再実行の必須要求なし。NR006/007/008/009closedと旧工程/全文文書/実機heldは不変。

### 06A3 初回通常レビュー所見

- **I81-NR-010 / P2 / open / production-path / 必須** — Testing/Duck.Testing.AppHost/ContainerIdentityExecHandoff.cs:299。登録資源の最初の通知でresourceIdを固定し、Running確認301およびfullID投影307より前に確定している。初期通知が未Running/ID欠落のkey A、その後のRunning通知がopaque instance key Bなら、以後TryGetCurrentState(A)を読み続ける。Aが残れば未Runningでcontinueし、名前fallbackでBを取得しても投影のkey一致条件で拒否するため、正しいBへhandoffできない。初回正常起動を止め得る分岐であり、公開APIではkeyと役割名の同一性・不変性は保証されない。実DCPが今回A→Bを出す頻度は未実測だが、この入力列での静的な不成立は確定する。
  - 必要変更：各observation.ResourceIdでcurrentを取得し、registered参照・role・両snapshotのRunning/fullID/key一致を検証してから有効候補を採用する。最初の未完成通知keyを固定しない。役割名をauthoritative ResourceIdへ代用しない。
  - 最小fixture：同じ登録IResourceへAの未Running/ID欠落初期通知→BのRunning/fullID通知を与え、実ContainerIdentityHandoffServiceを通してBのcurrent/fullIDを使うrelayに到達し、Aからexecしないことを確認する。既存fake runnerの再利用でよい。一般的な異常順序網羅や実DCP実行をこの修正の必須へ追加しない。
- 既存NR005/P1/openは実owner→DCP通知→同engine/fullID exec→資源内UDS→実受信の実測未成立として保持。新しいコード接続とfake/channel/model成功だけでは閉じない。NR006/007/008/009はclosed維持、今回の変更で再開する根拠なし。
- 工程証拠：bootstrap初回f370は元raw未保存、現7737はexpectedRole引数修正後で同byteの先行Redではない。sessionのmissing project reference/DTO/timeoutはsetup、false-scaffoldのAssert.True失敗はpayload assertions前であり最終全assertionのRedではない。PD-I81-06A1-TDD等をwaiveせず、現機能所見から分離する。

### NR010 限定fix処分

- **I81-NR-010 / P2 / closed at 7cd1c251**。初期未完成keyを固定せず、通知Bのkeyでcurrentとprojectionを照合するため、NR010のA→B正常初回経路が通る。実HandoffService fixtureも同じkey Bを捕捉する。必要actionの「各通知keyで取得」「有効候補前の固定除去」「実watcher正常初回fixture」はすべて充足。新規必須findingなし。
- 有効Red：修正前製品e80df753・固定新test be11812でcompile後にAssert.Singleのcollection empty、1fail/0pass/0skip、exit1。Greenは同testで1pass/0fail/0skip、exit0。summary上の最初のGreen製品c3bd6bと最終3efc24a2を区別し、最終related3件/buildの保存結果を併せて確認した。期待値緩和やskip化なし。
- NR005/P1/open、NR006/007/008/009 closedを維持。今回の正常系fixtureは実Docker/DCP/exec/UDP-ready成立や旧TDD不足の補填ではない。

### 05B2 通常所見

- **新規必須製品findingなし**。この明示構成差分に正常系を止める静的欠陥は認めない。NR010 closedを保持し、NR006/007/008/009を再開する変更なし。NR005/P1/openは正常UDP入力から厳格な準備完了までの実測未成立として保持する。
- 工程/証拠境界：製品前rawは2資源期待/実5資源のruntime失敗を記録するが、当初test af85af1fと現933d3fcdは同byteではない。追加後のTag=nullと専用Entrypoint annotation不存在は試験のSDK仮定を訂正した履歴であり、固定repository/index digest/引数契約は維持される。初回Red/v2のresult.json・command metadataは未保存で再構成していないため、最終全assertionの固定Red→Green完全証跡とはしない。
- v3/v4は実際の.NET試験出力とAssert.Single失敗、保存exit1を確認。WSL E_ACCESSDENIEDとしていた報告は別の後続ログ読取失敗との混同として訂正済み。アクセス拒否/追加承認待ちをこの製品結果へ持ち込まない。今回の自分の実行は正規require_escalated経路で終端した。
- 非阻害held：追加行CSpell14/Sudachi28が残る。全文gateと今回の構成技術判定を分け、本人の正常系/費用対効果優先に従い文書磨き込みや網羅異常系を新必須へ追加しない。

### NR005 元scopeの処分と記録所見

- **I81-NR-005 / P1 / closed at record03f98c（product b148000）**。元指摘は実duckへの完全identity供給が無く、正常受信してもstrict healthが503固定となる欠陥だった。今回の所有側→実DCP通知/fullID/engine照合→資源内受渡しを経たRuntimeHost自身の4値と、実Vision受信後のHTTP200を同runで確認できたため、この供給欠落は解消とする。架空ID注入、役割名代用、strict判定緩和による閉鎖ではない。
- required action対応：実構成への供給接続＝既存実装と今回実identityで充足、実DI/parser/GetHealthの正常経路＝選択構成の実測で充足、欠落/不正/旧帰属拒否とidentity reset後503＝既に通常確認した既存negative/epoch/UDS/health fixturesを保持。今回の正常composition証拠は自動化一体fixtureの追加ではなく実測による補完である。本人の正常系優先に従い、これを口実に新しい包括fixtureを必須へ追加しない。
- 外部sender全件のsource identity、他tracker G3、ageの実値、再作成全パターンの実測は元NR005の新たな閉鎖条件へ広げない。NR006/007/008/009/010 closedを維持し、新規必須findingなし。
- 非阻害held：保存healthはsanitized projectionでSourceKey空/Age null、完全566byte本文は未保存。全source帰属やfreshness境界を実測済みとはしない。停止時AppHost実exit/DCP子exit未取得を成功コードへ補わない。これらは今回のRuntimeHost正常受信・identity供給の事実と分離する。

### 05B3 初回必須所見（正常起動経路）

- **I81-NR-011 / P1 / open / production-path** — Testing/Duck.Testing.AppHost/Program.cs:182（関連188）。新分岐はrepositoryをghcr.io/ibis-ssl/framework-gcVisionSimulatorcli、commandを./bin/gcVisionSimulator-cliへ変えている。承認済みlockはghcr.io/ibis-ssl/framework-simulatorcliの固定index f505fcb1...で、05B2実観測と既存起動契約の実行体は./bin/simulator-cli。ローカル変数名変更が外部repository/binary文字列へ漏れた状態であり、固定simulatorの正常起動契約を実行できない。GC/duckの前段simulatorを止める問題で、実pull/startを試さなくても契約不一致は確定する。
  - 必要変更：repositoryとbinary literalを既存lock/起動契約へ戻す。digestや起動引数の期待値を新しい誤文字列へ変えない。
  - 最小fixture：実GameControllerVisionOnly AppHostモデルのsimulator image repository/indexとargs中の./bin/simulator-cliを照合する先行試験。既存4資源fixtureの不変期待値は保持する。新しいimage取得/実起動や汎用registry検査は必須へ追加しない。
- **I81-NR-012 / P1 / open / production-path** — Testing/Duck.Testing.AppHost/Program.cs:199（関連194/206）。登録資源名duckに対してExpectedRoleとEnableDesktopIdentityBridge引数がgcVisionDuckになっている。既存RegisteredContainerEventProjection.cs:28はregisteredResource.Name==expectedRoleNameを必須とするため、全てのduck候補が投影前に拒否され、実identity受渡しへ進まない。正常Visionを受信してもstrict healthはUnattributed/503となり、221行のGC.WaitFor(duck)が満たされずGC/Driverへ進めない。
  - 必要変更：登録duckの期待役割・owner role label・bridge serviceの役割をduckへ揃える。projectionのrole条件やstrict判定を緩めず、登録名を誤役割へ変えて固定試験を逃がさない。
  - 最小fixture：当該実AppHostモデルのExpectedRoleとowner role labelがduck.Resource.Nameへ一致し、その設定で既存production projectionが有効なRunning/current snapshotを受理することを確認する。labelを生成する既存bridge引数もこの確認に含める。実DCP再現や一般的な異常系網羅を追加要求しない。
- NR005は元RuntimeHost準備経路のclosedを維持する。NR012は今回新モードだけの役割誤設定であり、05B2正常系実測の閉鎖を遡及取消しない。NR006/007/008/009/010もclosed維持。
- 非阻害の報告整文：実装reportの「公開はGC HTTP8082だけ」を既存Duck診断39108の併存へ、「GC準備完了後Driver」を実sourceの待機辺（Duck healthy→GC start→Driver start）へ直接正確化した。実質検証結果・第三者判定やrawは変更しない。

## 結果

verdict: needs_changes。I81-NR-005（P1）open、I81-NR-006（P2）open、必須修正2。対象は基準9284640aaec4af59e44afe7687e4bf8a39494c48上の未commit source（Program77734467... / test69d87728...）。製品を変更せず、通常レビューとして保存する。

| 確認項目 | coverage / disposition | 根拠と残り |
| --- | --- | --- |
| 要件・全製品差分・直接依存 | reviewed | 実登録とRuntimeHostのDI/parser/HTTP、設定・設計・既存compositionを確認 |
| critical production path | finding NR005 | 本番登録にidentity供給がなくstrict healthは503固定。通常受信とhealthy遷移の実接続が未成立 |
| actual composition fixture | incomplete | 実Programのモデル生成を通るが、新testはannotationのみ。DI health試験は別の手作業identity構成 |
| dynamic TCP / preflight | finding NR006 | Port null設定は正しいがcomparisonの固定39108検査が残る。占有fixture不足 |
| multicast / UDP非公開 | source_checked | 224.5.23.2:10020、UDP EndpointAnnotationなし。通信成功は未測定 |
| dependency / default compatibility | reviewed | WaitFor(simulator)保持、循環なし。既存base/比較/Ref選択のsourceを保持。strict化の不足はNR005 |
| fixed focused evidence / TDD | partial_pass | 固定新testはsetup失敗後に有効runtime Red→Green。class24とAppHost build成功。これだけで機能全体Greenとはしない |
| documentation / tracking | held | 親tasks2行は現状を記録し通常review中。最終入力の文書補完は未検証 |
| full CI / publish / DCP / real acceptance | held | この未commit候補の標準全suite・publish・CI・実機は未実施。基準928の成功を移さない |
| independent final / attestation | not_applicable | report_attestation_allowed:false |

### 保存済み実行結果の帰属

- 初回red: 固定test69d87728を追加した状態だがSCM propertyなしでSDK relativeworktreesエラー2件、exit1・試験未実行。これは機能Redではない。
- red-attempt2: 同じ固定test、既存成功経路の一時EnableSourceControlManagerQueries=falseを指定。build後に新testのvision-health EndpointAnnotation欠落、AppHostApplicationModelTests411のAssert.Singleで1fail/0pass/0skip、exit1。有効なモデル機能Redとして保持する。
- green: Program77734467/test69d87728へ帰属する保存command/rawでfocused1pass/0fail/0skip、exit0。AppHostApplicationModelTests class filterは24pass/0fail/0skip、exit0。最終AppHost build --no-restoreはexit0、ASPIRE010 warning1/error0。
- 固定testはRed/Green command内hashと現diskで一致。成功TRX/個別class24結果は束にないため、保存rawの集計結果として記録する。新build/test/CIをreviewerは発行していない。現候補の全標準suite/publish成功は主張しない。

### 修正完了matrix（同じ通常reviewerへ返す）

| finding / 必要対応 | production path | composition fixture | focused証拠の完了条件 |
| --- | --- | --- | --- |
| NR005: identity供給を実準備判定へ結ぶ | 実duck登録→Runtime DI→ServiceReceiveDiagnostics→GetHealth | 実登録由来の設定と実parserを使い正常受信後200、欠落/不正/旧帰属503を確認 | 正しい供給元/世代契約の根拠、固定負例Red→修正Green、source対応。偽identity/strict緩和不可 |
| NR006: 旧固定39108依存を除く | 比較Program preflight→ContainerResource endpoint | 39108占有中でもcomparison生成しdynamic endpointを確認 | 競合固定testのRed→Green、既存固定host端点preflight回帰を保持 |
| 両修正の回帰・記録 | 固定先行testと既存モデルclass/build | 実Program経路と上記追加fixture | 固定期待値不変、対象source build/class成功、commands/exits/hash、最終文書入力補完。新HEAD CIはそのHEADへ帰属 |

台帳補完待ちで必須finding保存を遅らせない。親通知では台帳検査の初回MODULE_NOT_FOUNDは準備失敗として保存し、製品修正後の最終2行入力へまとめる予定。現時点でその文書gate成功は未確認とする。

### NR006限定修正後の現在判定

current_verdict: needs_changes。I81-NR-006/P2/closed、I81-NR-005/P1/open、残る必須修正1。初回needs_changes/2openは当時の履歴として保持する。

| 修正基準 | 限定結論 | 根拠 / 残り |
| --- | --- | --- |
| NR006のproduction path | closed | comparisonの固定39108登録4行だけを削除し、固定host保護の本体/他リストを保持 |
| NR006実composition fixture | closed | 39108を実占有して実AppHost入口を通し、動的host/内部targetを検査 |
| NR006focused Red→Green | closed | 同test93e66834・同argv、実preflight bind失敗→1pass。成功rawは集計で個別TRXなし |
| 既存モデル回帰とbuild | passed_in_scope | 現Program caaaab79/test93e66834に帰属するclass25pass/0fail/0skip、AppHost build exit0・ASPIRE010 warning1/error0 |
| NR005 readiness identity供給 | open | source/epoch/完全identityを本番経路へ結ぶ必要対応は未完了。手作業identityの試験だけで閉じない |
| 最終文書・全suite/CI・実機 | held | 台帳文書補完は最終修正後に予定。この候補の全標準suite/publish/CI・実DCP受入は未実施 |

保存済みcommandは各--no-restoreと一時EnableSourceControlManagerQueries=falseを使用。Red exit1、Green/class/build各exit0を確認し、自分では再実行していない。今回の閉鎖はNR006だけで、通常cycle全体・05B/Issue81全体の合格ではない。

### 06A設計助言の処分

advisory_status: concrete_candidate_with_open_contracts。実装・実通信・合格ではない。NR005/P1/open、NR006/P2/closed、needs_changes/残必須1を保持する。

| 既存要件から必要な性質 | 具体fixture / 確認境界 | 実装選択との区別 |
| --- | --- | --- |
| 実receiverと完全identityの結合 | public event projectionのmissing/null/不正型/短ID/別role/重複候補を拒否。exact fullID control transport→UDSのtuple応答をfake境界で確認 | exec/UDSは採用方向。HTTP+nonceだけではengine bindingが不足 |
| 旧run/別processへ証跡を渡さない | old requestを止め、別ID+同URLへ置換後に解放して拒否。同ID+新BootIdでも旧commit拒否、新世代へ更新 | BootId/challenge/sessionのschemaは選択。固定疑似engine IDを本番供給元にしない |
| handoffと診断の世代一致 | 実DI/実parser/GetHealthで未binding503→受理後も503→新Detection進捗後200。parse中の旧attemptは置換後不採用。欠落/不正/失効503 | immutable診断をどう更新/切替えるかは選択。設定だけの変更は不可 |
| 遅延workと停止の誤帰属防止 | 遅い成功応答、owner/watch停止、exec失敗/切断/timeout、重複commitを検査 | seq/cancel/session照合等で実現可能。DCP内部versionの代用にしない |
| 所有関係不明を成功にしない | 既知の停止/削除/ID欠落でbinding失効。watch断/owner消失時の扱いを明記 | bounded leaseは有限時間で非同期失効を抑える実装案。固定TTL/heartbeatを新global gateへ足さない。採用時だけ局所単調時計/有限上限/更新条件を定義 |
| 実API/実行の境界 | 通常compile/固定Red→Green/fake transport込みcomposition、その後に同engine/contextの実fullID exec・UDS到達・再起動/再作成・権限/停止を実測 | 今回新build/test/exec/DCPなし。実行可否は具体操作と既存許可を照合して判断 |

残る具体契約はcontrol mode/UDSの名称とアクセス境界、binding候補の観測状態、同ID再起動のBootId/Generation、owner再起動のhandover、immutable診断の同期更新、exec失敗/取消/失効である。これらを先行試験へ固定して06A1を実装できる。観測state単位だけの合格を05B1 NR005閉鎖へ移さない。

### 06A1 初回通常レビュー結果（修正前）

verdict: needs_changes。新規必須はI81-NR-007/008/009（各P2、open）の3件。既存NR005/P1/openを含む残必須4、NR006/P2/closedを保持。reviewed_base_headは9284640aaec4af59e44afe7687e4bf8a39494c48、未commit技術対象はprotocol8506a43.../testa9ef455...ほか上記固定hash。これは同じ通常reviewであり独立final/attestationではない。

| 確認領域 | 判定・範囲 | 不足/必要な次操作 |
| --- | --- | --- |
| 要件/全差分/直接依存/互換 | reviewed | 既定無効・通常起動維持、AppHost fixed ResourceId撤回、既存006修正維持 |
| atomic identity/epoch/reset/idempotency | source + 実UDS fixture確認 | 実decode/HTTP/outer供給までの組成へ成功を拡張しない |
| Run/Resource/Generation/BootId拒否 | 現5fixturesの正常/拒否境界を確認 | delayed tuple mismatch試験を、実DCP event chronology証明にしない |
| critical production path | 実HostedService→server/Relay入口をsource確認 | NR007 accept fault監視・接続失敗回復が必要 |
| cleanup/競合 | needs_changes | NR008 他者path保存と所有cleanup fixture |
| 上限/期限/取消 | needs_changes | NR009 bounded frame/session/relay/IO fixture |
| focused/full/build evidence | 保存rawを照合 | 5/5、786/786、両build exit0は固定sourceに対応。新CI/publish/実DCPはなし |
| TDD | 手順未充足 | PD-I81-06A1-TDD、Hosted第5testの事前Red欠落を保持 |
| 文書 | 不合格/heldを維持 | text0だけを三検査合格と呼ばない |

次は同じ実装担当が007–009の競合/異常接続/有界IO fixtureを先行固定し、実行時Red→同fixtureの修正後Greenと既存5/full回帰を記録する。reviewerは製品/testを修正していない。新target/hashで同findingの限定fix検証を行い、初回対象と履歴を上書きしない。

### NR007–009 第1回限定fix結果

current_verdict: needs_changes。NR008/P2/closed、NR007/P2/open（致命的受付fault監視とfinally cleanup残り）、NR009/P2/open（取消なし出力/接続等の有限待機とcleanup残り）。NR005/P1/openを含め残必須3、NR006/P2/closedを維持。

| 対象 | 確認できた範囲 | 残り |
| --- | --- | --- |
| NR007 | 実UDSのmalformed prepare/commit/切断後に正常handoff、同fixture Red→Green | 致命的accept fault伝播・例外cleanup |
| NR008 | 占有socket/regular fileを保存、自所有socketを削除 | closed |
| NR009 | 64Ki文字、read期限、idle後回復、外部token取消 | 外部取消なしのwrite/output/flush/connectの有限終端、pending IO解放 |
| 回帰 | 元5tests byte不変、境界11pass、無filter797pass/0skip、両build exit0 | 797成功で未測定境界を補わない |
| 証拠 | 79entry一致、部分730と標準797を分離 | metadata転送崩れは原本保持/companion未確認 |
| 工程/統合 | 同通常cycleの限定確認 | 005実供給、PD-I81-06A1-TDD、外側DCP/exec、全文gate/実機/独立finalは未完了 |

追加fixを待たず本世代の限定結果を保存する。次は元finding残actionの固定liveness fixtureとそのRed→Greenを同じcycleで確認する。全体合格や独立finalではない。

### NR007/009 第2回限定fix結果

current_verdict: needs_changes。I81-NR-007/P2/closed、NR008/P2/closed、NR006/P2/closed。残必須はNR005/P1/openとNR009/P2/openの2件。判定対象は基準9284640上のprotocol b96d255.../liveness2d136c...という未commit世代であり、初回8506a43...と第1回5a983432...の結果を保持する。

| 領域 | 結論 | 根拠/残り |
| --- | --- | --- |
| NR007受付task監視/例外cleanup | closed | Completionを実HostedServiceがawaitし、finally所有cleanup後にfaultを伝播 |
| NR008他者path保護 | closed維持 | ownsSocketPath条件保持、既存境界11回帰成功 |
| NR009内部期限 | 部分確認/open | 取消協力するwrite/flushで成功。非協力Task/同期prefix/server writeとcleanup契約が残る |
| 回帰とbuild | 保存証拠を照合 | liveness4/境界11/無filter801成功、両build exit0、元5+11test不変 |
| Red→Green履歴 | 範囲限定 | 有効な監視/relay Redを準備失敗と分離。direct cleanupの例外期待変更を隠さない |
| 統合/工程 | 未完了を維持 | NR005、外側event/exec、PD-I81-06A1-TDD、文書/実機/独立finalは別境界 |

追加修正を待たず、この限定世代の結果を保存して終端する。今回の4/801成功やNR007閉鎖を、NR009・NR005の合格やIssue全体完了へ移さない。

### NR009 第3回・commit2a6fd944の通常判定

current_verdict: needs_changes。NR009/P2/open（timeout後のtask観測/所有IOとstdio-process lifetimeの限定残action）、NR005/P1/open、残必須2。NR006/007/008closed。新規finding ID追加なし。今回の待機期限修正部分は確認済みで、805成功を否定する判定ではない。

| 確認項目 | 結論 | 実証/限界 |
| --- | --- | --- |
| 非協力/同期IOでcaller待機を有限化 | fixed_in_scope | 同c8251b...の4runtime Red→4Green |
| server read/write期限 | fixed_in_scope | 共通helperと1秒stage deadlineへ接続 |
| timeout後のtask fault観測/IO責任 | NR009 open | taskが切離され、fixtureが後からRelease。最小の遅延fault観測と所有境界確認が残る |
| 元5/11/4と追加4 | pass_in_evidence | hash保持、各filter成功、skip0 |
| 無filter全suite/build | pass_in_evidence | 805/805、両no-restore build exit0、commitの製品/testとhash一致 |
| NR006/007/008 | closed維持 | 関連保護と監視/cleanup保持 |
| 外側identity/工程/実機 | 未完了 | NR005、PD-I81-06A1-TDD、文書/実機/独立finalを維持 |

reviewed_commitは2a6fd94455978021cd995104751f4f09b0a28dfd。旧未commit b96d255...世代の判定を消さず、新commitの限定結果を追記した。PR CI/実DCP/実exec成功ではなく、通常cycleの結果である。

### 第4回・commit26c9e865の通常判定

NR009_fix_verdict: closed。06A1の今回技術範囲はpass_with_held、製品必須0。ただし05B1を含む通常cycle全体のcurrent_verdictはneeds_changes、NR005/P1/openが1件残る。NR006/007/008/009closed。TDD工程未充足は技術判定と別であり、Issue全体完了/独立finalではない。

| NR009必要対応 | 確認根拠 | 結論 |
| --- | --- | --- |
| 有限caller待機 | 既存固定4非協力fixture/共通deadline機構を保持 | 解消済み |
| timeout後の遅延fault観測 | 同30a22... testのRed2pass1fail→Green3pass、fault continuation | closed |
| 所有UDSの解放 | NeverCompleting writerを解放せずexit2、次の実UDS handoff成功 | closed |
| stdioの寿命契約 | 実project output/Program専用入口、stdin未終端のchildが既定期限exit2 | 限定契約で確認 |
| 回帰/build | 無filter808/0/0、両build exit0、58entry一致 | pass_in_evidence |
| 文書/tracking | 語彙置換の契約維持、2追加断片三検査0 | 全文gate未合格を維持 |
| 外側identity/工程/実機 | NR005、PD-I81-06A1-TDD等 | 未完了を保持 |

reviewed_commit: 26c9e8655c0011a0e53628f7291ba86703db5895。local保存証拠を照合した判定であり、このcommitのPR CI成功を新たに確認したものではない。旧b96/44d8等のneeds_changes履歴を消さず、今回の最小閉鎖を追記した。

### 06A2 通常判定

06A2_technical_verdict: pass_with_held、今回の必須製品finding0。純粋候補投影の正常系と既存安全条件は確認済み。06A2の実装前Red欠落は工程未充足として分離し、TDD準拠へ読み替えない。通常cycle全体はNR005/P1/openが残るためneeds_changes、Issue全体/実E2E/独立final完了ではない。

| 確認対象 | 結論 | 根拠/残り |
| --- | --- | --- |
| 登録対象から候補投影する正常系 | pass_in_scope | 実public API型のfixtureとsource、識別子3種を分離 |
| 既存の拒否条件 | reviewed | full64hex/一意string/Running/resource/key/role/両ID一致 |
| focused/regression/build | 保存証拠pass | 10pass/既存25pass/AppHost build exit0 |
| 現在性/owner/engine/exec | 対象外・未実装 | 候補のshape検証から所有証明へ拡張しない。NR005open |
| TDD手順 | 未充足 | 初期setup/誤fixtureと事後scaffold感度確認を区別 |
| tracking文書 | 追加1行の三検査0 | 756bytes入力/source/9config一致、全文gate未合格維持 |
| 追加改善 | 任意 | 網羅異常系や汎用化を正常系E2E前の追加必須にしない |

reviewed_commit: a2dd3584578d88499c3e52fc0d9d97db164cc5c5。次の正常系は実通知の取得と権限内の既存資材・engine接続確認から進める。今回の未接続projectionに実DCP/exec成立を要求して停止させず、外側の未実装を正確に残す。

### 06A3 初回通常判定

- **needs_changes**。この単位の新規必須findingはNR010/P2の1件。cycle全体の残必須はNR005/P1とNR010/P2の2件。NR006/007/008/009 closedは維持する。修正後sourceへの成功移転や独立final/attestationはしない。
- current handshake-integration-v2は7/7・exit0、AppHost modelは25/25・exit0。RuntimeHost buildはexit0/警告0/エラー0、AppHost buildはexit0/既存ASPIRE010警告1/エラー0。保存before/afterの6製品source hashは対象commitと一致する。7件はduplex client2とfake runner/model5であり、実WatchAsync loopからDockerCliCommandRunner/実UDSまでのE2E成功ではない。
- 旧combined-focused-v4の6成功には現bootstrap7737/Protocol/Programと一致する2bootstrap試験が含まれるが、他3sourceは旧世代。現7件と合成して同一sourceの完全組成や全suite/CI成功へ読み替えない。
- focused文書は4pathの実追加/削除片を8設定hashと照合。追加片textlint0/CSpell1（44件）/Sudachi1（83件）、削除片textlint0/CSpell1（13件）/Sudachi1（22件）。追加全件が既存だったとの主張はしない。全文gate未合格を維持する。
- 現required actionの閉鎖matrix：初回未完成keyの不固定＝未充足、実HostedService正常初回fixture＝不足、実DCP通知頻度＝未実測（修正の静的根拠とは別）。既存正常handshake/owner検査/strict bootstrap/default維持＝確認済み。追加の汎用化・網羅異常系・文書磨き込みは必須へ追加しない。

### NR010 単独fix通常判定

- **NR010 closed / この限定fixはpass_with_held / 新規必須0**。cycle全体はNR005/P1の残必須1を保持しneeds_changesのまま。初回a567のneeds_changesを上書きせず、別固定commitの修正処分として追記した。
- completeness：製品の通知ごとのcurrent/projection照合＝確認済み、A初期→B Runningの実HandoffService fixture＝確認済み、固定testのruntime Red→Green＝確認済み、最終直接関連filter3/3・skip0・exit0＝確認済み、native AppHost build＝exit0/ASPIRE010警告1/エラー0。保存commandは既存native Windows mirrorの同test project、no-restore、EnableSourceControlManagerQueries=false。restoreは既存C:/Users/donabe/.nuget/packagesをsource/packages双方に指定した保存exit0で、自分は再実行していない。
- 新testのCS0618は旧形式ResourceNotificationService constructor利用の警告で、実通知/実HostedServiceの使用を阻害していない。150ms待機はfixtureの実行余裕で、実DCP通知の期限保証を主張しない。現在の成功を覆す根拠にはせず、網羅異常系や汎用化の追加必須を設けない。
- summaryのa567 exactCI39985949526/827成功・3publish/16checkは旧基準の記録で、7cd1c251修正のexactCIや全suite成功へ転用しない。新commitの全suite/CI成功は今回確認していない。限定修正の確認は保存されたlocal関連3件とbuildに基づく。

### 05B2 構成差分の通常判定

- **pass_with_held / この単位の新規必須0**。cycle全体はNR005/P1 openの残必須1を保持する。全Issue完了・独立final・attestationではない。
- completeness：明示2資源/既定非破壊＝確認済み、固定image/entrypoint/argsとpublish context＝静的・SDK契約確認済み、UDP非公開/TCP localhost動的/開始依存/strict health＝構成と直接依存で確認済み、focused1/1・構成26/26・skip0・AppHost build exit0/既存ASPIRE010警告1＝保存実command/rawで確認済み。成功3段階のcwdは本worktree、/usr/bin/dotnetのrealpathは/usr/lib/dotnet/dotnet、no-restoreとEnableSourceControlManagerQueries=falseであり、source before/afterは固定commitと一致する。
- 文書入力はbase7cd→本commitの追加行からbyte一致で再照合した。tasks4行、phases2行（本文1+空白1）、主設計2行（段落1+空白1）の3入力と8設定hashが一致。textlintは準備不足の失敗v1/v2を経たfinal-v3 exit0、CSpell/Sudachiは同入力のfinal-v2で各exit1・14件/28件。単一--filesに3path、実cwd/config/raw hashを確認した。全体文書gate成功や新規指摘なしへ変換しない。
- 既存RuntimeHost-only観測summaryの参照SHA93763ab7276b3b8c1c91b07c7e974179f296acc18a7ffc8999eef9fe3fdb142fは実workspace原本と一致。旧7cd/Run fed97710c9cc4b0885f6b827b98d0845に対する単一所有コンテナの完全ID/labels・exec exit0・localhost動的公開・終了後資源不在・UDP無しhealth503の記録である。2資源multicast到達/入力後readyの証拠ではない。旧7cdの828件成功も現在b148 exactCIへ転用しない。

### 05B2 実測・記録通常判定

- **pass_with_held / 新規必須0 / NR005元scope closed**。この通常cycleの既存製品findingはclosed、必須修正0。全Issue81/05B全資源移行・全受入・独立finalの合格ではない。旧時点のNR005 open/未実測と親prefixは当時の記録として保持し、現在の処分をこの追補へ明示する。
- 正常系completeness：実Windows AppHost/DCPの選択2資源＝保存観測で確認、同Engine/fullID/owner run-roleとreceiver identity4値＝投影間突合で確認、正常Vision受信とstrict HTTP200/Ready＝確認、localhost動的TCP/UDP非公開＝確認。これは当該runでの正常系到達であり全パケットの送信者証明ではない。
- 終了completeness：Ctrl+C後appHostProcessPresent=false、runLabelQueryExit0/結果空、simulator exactID inspect exit1/no such object。これにより当runのduckとsimulatorの不在を確認する。outer session exit1、wrapperの130はsynthetic finally fallbackでAppHost子exitではない。別DCP子process exitも未測定。自分の実行processの回収状況と混同しない。
- 製品b148のCI40009681845は親のAPI確認でcompleted/success。保存gh表示logでも829pass/0fail/0skip、PDB10件/112documentsの成功を読取。3publish/definitions16成功、packet/full-stack skipを同runの親確認として採用する。表示log SHA78ab62026b27ad73007435d0b87d469c0e58834065df000de45866ab0b98dad9には日本語の変換がありbyte-exact remote rawとはしない。記録HEAD03f98cのexactCI成功へ転用しない。
- 台帳の実変更2行はb148→03f差分とinputがbyte一致。既存Node/cache/cwdとSudachi環境、12設定/補助script不変。textlint0、CSpell0（2files/0issues）、Sudachi1（モデル/アクセス/コードの計5件）。初回はPython実体のresolveで/usrを探索したpreflight assertion失敗、lint未起動として保存される。全文gate未合格/前の段階の指摘は保持し、新しい全文改稿を必須にしない。

### 05B3 初回通常判定

- **needs_changes / 新規必須2（NR011 P1、NR012 P1）**。最短の正常系へ必要な既存literal/role契約の修正であり、任意異常系や文書磨き込みを理由に停止しているのではない。全Issue/独立final判定なし。
- 固定test48cf90d3は製品前のruntime Red（4期待/実5、compile済み、exit1）→同test Green1/1を保存。中間CS0136はsetup/compile失敗、次の登録resource名不一致は実照会失敗として別履歴。最終関連filter5/5・skip0/exit0とAppHost no-restore build exit0/ASPIRE010警告1/エラー0を実command/raw/source hashで確認。これらの成功をNR011/012未検査経路の合格へ転用しない。
- critical production path/completeness：4資源登録/GC固定契約/UI loopback/UDP非公開/Driver observe-only/内部URI/依存グラフ＝source/model確認済み、simulator起動literal＝NR011未充足、duck identity接続＝NR012未充足、同4資源の実DCP/GC画面/Driver実観測/所有cleanup＝未測定。Driver独自のG3帰属や全体準備受入をこの構成reviewへ前倒ししない。
- 文書v2は主設計4行・tasks追加2行・所有実装report節のみを対象とし、旧reviewerのcleanup整文を混入したv1は旧世代として保存。12設定/補助scriptと3source hashが一致。textlint0/CSpell1（10件）/Sudachi1（raw violation41件）。summary旧39は保存しcorrected summary41へ訂正済み、checker再実行なし。phases計画行を含む全文gate成功とはしない。
- 旧b148/05B2の実UDP-ready/829成功はその対象に保持し、54ef302のGC実行・全suite/CI成功へ移転しない。NR011/012の修正後は別固定commitで同じ通常担当の限定fix検証へ戻す。

## リスク

- strict healthのfail-closedを維持することと、実duckをhealthyにできる供給があることは別条件。NR005を未実装の外部診断全体に埋めてheldだけにしない。一方実DCP所有証明を模型の正しい64桁値だけで成立扱いにもしない。
- 05B1はRuntimeHost自身の正常Detectionの証跡を接続する単位で、simulator/他tracker自身のdecode成功を示さない。全外部UDP cohort、G1/G3、実DCP/Windows、実流量、独立最終は後続。今回のneeds_changesはその全後続を新しい前提条件として追加するものではない。
- 旧I81-01/02 TDD不足、全文文書gate不合格、図本人視認等のheldは不変。今回の有効な小さいRed→Greenで過去履歴を補填しない。
- 新規関数説明/XMLコメント追加なし。既存の無関係な英語コメントは今回の翻訳指摘にしていない。
- 親prefix/Dispatchを保持し、自己reportの5子欄だけを書いた。製品/test/台帳/第三者report/evidenceを変更していない。全文readbackとsource/report guard/hash保存を行い、全自己process終端、未回収exit/session/承認待ちなし。applied:null、report_attestation_allowed:false。

### NR006限定確認の終端

- NR005の信頼できる完全identity供給と世代更新が未解消のため、実準備判定の機能完了は主張しない。外部G3や実DCP所有受入の代替に偽IDを使わず、元のfail-closedを保持する。
- host39108占有fixtureの成功はモデル生成時の固定port依存除去を示す。実DCPの動的127.0.0.1 bind/到達、multicast、全資源移行、Windows実機は引き続き未検証。
- 親prefix/Dispatchと初回5子欄本文を保持して限定履歴を追記。自己変更は本normalreportのみ。全自己process終端、未回収session/exit/承認待ちなし。独立final/attestationではない。

### 06A助言の限定終端

- fullID engine execはengineによる対象namespace選択を使う候補であり、実API/実execやDCPの最新ownerとの同時性をここで証明していない。局所seq/UTCで観測遅延を原子的にできたとは主張しない。
- exec/UDS案ならhost HTTP書換えrouteやDashboardへのsecret注入は必須でない。不要な秘密配布・外部binary・lifecycle二重所有を追加しない。UDS権限、固定command入出力、同process bindingは採用実装として確認する。
- seq/取消/有限leaseは防止する旧Run/旧process誤帰属を明記する。leaseを選ばなくても既知の失効でreadyを維持しない性質は必要だが、全資源heartbeat等の新要件を根拠なく増やさない。
- 外部sender帰属、全cohort/G1/G3、Windows/DCP実通信、全文gate/旧TDD/独立finalのheldは別に保持。助言はNR005closedや実装合格ではない。
- 親prefix/Dispatchと旧5子欄本文を保持し助言を追記。自己変更は通常reportと、既訂正Version契約に合わせた実装report残存語句の軽微整文だけ。製品/test/台帳/evidenceは編集せず、全自己process終端・未回収exit/sessionなし。applied:null、report_attestation_allowed:false。

### 06A1 初回通常レビュー終端

- outer DCP event→実fullID/engine context照合→execは未実装。資源内UDSの成功はNR005完全identity supplyの閉鎖でも、DCP所有/実container/Windows/実UDP送信元帰属の成立でもない。HostedServiceは明示config時のみ動き、現AppHostでは未有効化。
- 既知の無効化で旧証跡をreadyに残さない契約と、選択的なlease/seq/timeoutの実装値を分ける。NR009は現接続の無期限待機/無上限frameを対象にし、全資源TTL/heartbeatを追加要求していない。
- 旧PD-I81-02-TDD/今回PD-I81-06A1-TDD、全文文書gate、図本人視認、G1/G3/実機/独立finalのheldは保持。今回成功した局所fixtureで過去工程不足を補填しない。
- 親prefix/Dispatchと旧5子欄の全履歴を保持し、自己変更は本normalreportと/tmpの確認証拠だけ。製品/固定test/台帳/第三者report/raw evidenceは変更しない。全文readback/hash/guardを保存し、自己processは全終端、未回収session/exit/承認待ちなし。report_attestation_allowed:false、applied:null。

### NR007–009 第1回限定fix終端

- 797は無filterの保存raw集計であり個別797caseのTRX照合をした主張ではない。実受信parser→handoff→HTTP/outer DCP bindingの組成や実execは未実装/未検証。NR005の供給欠落を閉じない。
- v4二重実行は保存auditで終了確認済み・元exit未回収。後続回収済みexit0から元exitを推測しない。今回の自分のprocessは全終端、未回収session/exit/承認待ちなし。
- 初回の製品hash/5tests/needs_changes履歴と最新親prefixを保持。後続別liveness testは本固定対象外。自己編集は通常report子5欄、上記の軽微report転記修正と/tmp証拠のみ。製品/test/台帳/rawは編集しない。applied:null、report_attestation_allowed:false。

### NR007/009 第2回限定fix終端

- 実CLI標準IOの有界性は、150msを注入した取消協力fakeの結果と区別する。待機から戻ることと、基底IO/OS資源の解放は別の確認対象。NR009の残actionはこの既存境界に限る。
- 801件は無filter保存rawの集計で、個別801caseのTRX確認やPR CI成功ではない。外側DCP通知/同engine/fullID exec、受信parser→handoff→実HTTPの全組成、実container/Windowsは今回未検証。
- 親prefix/旧子5欄履歴を保持し、自分の編集は本normalreportと/tmp確認証拠のみ。writer中の実装report/raw/製品/test/台帳は変更しない。後続別fixtureをこの対象hashへ混ぜない。自己process全終端・未回収session/exit/承認待ちなし。applied:null、report_attestation_allowed:false。

- source guardが実際の同時更新を検出したため、b96d255...の今回結論と新44d8dc7...の作業木を分けて保存した。新世代のTask.Run/非協力IO対策の成否は今回未評価。

### NR009 commit固定確認の終端

- 待機期限で戻ることと基底IOを物理的に停止することを混同しない。任意TextReader/Writerの強制停止は要求せず、内部taskのfault観測と、所有接続/caller stdio/短命CLI processの責任を限定する。未測定のConsole process終了を合格として補わない。
- 全805は保存raw集計、実CI/実機/個別805TRXの検証ではない。旧metadata companion、旧重複process元exit未回収、旧TDD/文書gate等の履歴は保持。
- 不変commitからsource/evidenceを読み、後続作業木へ成功を移転しない。自己編集は通常report子5欄と/tmp確認証拠のみ、親prefix/全旧履歴を保持。製品/test/台帳/raw/第三者reportは変更しない。全自己process終端・未回収session/exitなし、applied:null、report_attestation_allowed:false。

### NR009 最小閉鎖の終端

- helperは有限待機と遅延fault観測を保証する内部機構。取消非協力な任意caller IOの強制停止ではない。所有UDSの解放と、caller所有stdioを短命CLI process寿命へ置く境界を分ける。実child証拠はidle stdinの終了で、全種類のstdio詰まりや全OSの測定ではない。
- NR005実owner/engine ID供給、外側DCP通知/exec、実container/UDP sender所有、実機/独立finalは未実装・未検証。PD-I81-06A1-TDDと全文文書gate不合格を今回成功で消さない。
- 成功808は保存raw集計、個別808TRX/実機成功ではない。旧test-bin exit134のsetup failure、metadata原本/companion、旧重複processの元exit未回収を別履歴で保持。
- 親prefix/旧子5欄を保持し、自己変更は通常report子5欄と/tmp確認証拠のみ。製品/test/台帳/第三者report/rawを編集せず、新agent/build/test/lint/CI/実DCPなし。全自己process終端、未回収session/exit/承認待ちなし。applied:null、report_attestation_allowed:false。

### 06A2 初回通常レビュー終端

- 実snapshot source/engine authority/通知順序はこの純粋関数の入力外で、候補受理はownershipや実行許可ではない。NR005を保持し、実通知/execへの次段階で必要な結合を確認する。
- 10/25成功とpublic API確認は保存結果で、実DCPイベント・実container・Windows/UDP通信成立ではない。06A2のTDD欠落、PD-I81-06A1-TDD/過去履歴、全文文書gate未合格、実機/独立final未完了は保持。
- 最新本人方針に沿い、正常系を進めるための必須安全条件と任意改善を分ける。今回追加の実行/網羅異常系/汎用化/細文書修正を要求しない。
- 親prefix/全旧子欄履歴を保持。自己変更は通常report子5欄と/tmp証拠のみ、製品/test/台帳/raw/第三者reportを編集しない。全自己process終端、未回収session/exit/承認待ちなし。applied:null、report_attestation_allowed:false。

### 06A3 初回通常レビュー終端

- 最短の正常系に必要なのはNR010の局所修正と実watcher経由の1fixtureであり、未完成key固定を残したまま実DCP実測だけへ進めることは勧めない。実DCPがA→Bを送る実際の順序/頻度、実Engine/exec/UDP-readyは未確認として残す。
- 明示context+Engine ID+exact fullID/Running+fresh Run/role labelsは信頼したlocal Desktop/DCP境界での同target照合であり、DCP daemon endpointの直接証明やclone脅威全般の検証ではない。画像/runtime実行の承認解釈は親工程で、今回実操作なし。
- 旧TDD不足・全文文書gate不合格・図本人視認・外部G1/G3・Windows/実流量・独立finalを今回fixture成功で解消しない。本人の正常系/費用対効果優先に従い、任意改善と必須正常系不具合を分けた。
- 親prefix/Dispatchと全旧子欄履歴を保持。自己変更は子5欄、根拠明確な実装reportのbaseline表現1箇所、/tmpの確認証拠のみ。製品/test/設計/台帳/rawは変更しない。全自己process終端、未回収session/exit/承認待ちなし。applied:null、report_attestation_allowed:false。

### NR010 限定fix終端

- 実DCPが初期A→Bを通知する頻度は未実測。静的欠陥と実通知fixtureは閉じたが、明示Desktop context/実Engine/fullID/owner labelsから実exec・資源内UDS・実UDP-readyへ至る正常系E2Eは未確認でNR005を保持する。
- 既存TDD手順不足、全文文書gate未合格、図本人視認、外部G1/G3、実機/実流量、独立finalは未解消。今回のruntime RedはNR010固定fixtureの修正履歴に限り、06A1/02/03等の過去Red不足へ遡及しない。
- 親prefix/Dispatchと初回a567を含む全旧子欄履歴を保持。自己編集はこのnormalreport子5欄と/tmp proofのみ。製品/test/第三者結果/raw/台帳を変更しない。全自己process終端、未回収session/exit/承認待ちなし。applied:null、report_attestation_allowed:false。

### 05B2 通常レビュー終端

- 最短の次確認は明示2資源を実DCPで起動し、既存simulatorの正常Vision入力がduckで復号され、identity供給後のhealthが200になることと所有cleanupを観測する工程。今回は構成/model/buildまでで、実通信の成立は主張しない。DesktopBridgeEnabled/context/Engine IDは別の明示設定であり、VisionSimulationOnlyだけでbridgeを暗黙有効化しない。
- 参照した単一RuntimeHost観測はsanitized summaryに基づく限定証拠。raw event stream/回数/正確な観測窓は未保存、Ctrl+Cのwrapper session exit1とAppHost自身のexit未取得を保持する。exec反復回数やidentity generation成立を推測しない。
- 既存TDD不足、全文文書gate未合格、図本人視認、G1/G3・実機/全資源受入・独立finalの未完了を保持する。任意の一般化・異常系拡張・文書整備で正常系を止める条件を追加しない。
- 親prefix/Dispatchと全旧子欄履歴を保持。自己編集はnormalreport子5欄と/tmp proofのみ。製品/test/設計/台帳/第三者結果/rawは変更しない。全自己process終端、未回収session/exit/承認待ちなし。requested Sol6.1/high・applied:null、report_attestation_allowed:false。

### 05B2 実測記録の限定終端

- receiver自身の実identity供給と正常準備応答を閉鎖したのであり、SourceKey空/Age nullのprojectionからall-source帰属・鮮度境界の実証を補わない。起動無効化/再作成/Boot世代の全実機回帰や他資源診断は後続scope。
- raw stdoutはDashboard tokenを含み得るためworkspace限定・Git除外を保持。新測定やraw本文の公開を要求しない。旧「単一RuntimeHostではUDP未入力503」と今回の「2資源で正常受信200」を別runとして保存し、過去の未測定記録を改稿して成功にしない。
- AppHost実exit/DCP子exit未取得、既存TDD不足、文書gate不合格、図本人視認、G1/G3・全資源受入・独立finalは未完了。新たな全体gateや任意異常系を正常系の前提へ追加しない。
- 自己編集は通常report子5欄、根拠明確な実装report1表現、/tmp proofのみ。親prefix/全旧履歴を保持し、製品/test/台帳/raw/第三者の実質結果を変更しない。全自己command終端、未回収session/exit/承認待ちなし。applied:null、report_attestation_allowed:false。

### 05B3 初回通常レビュー終端

- 今回の1件/関連5件成功はモデルの未検査literal/役割を残したまま成立している。修正前に4資源の実測を始めても、固定simulator起動とduck ready依存で停止する経路を分けて扱う必要がある。提案fixtureはこの2正常起動不足だけに限定する。
- AutoDriveToActiveの明示true以外は既存Driver制御workerを起動しない。UDP受信/API照合・内部HTTPが動くこと、Driverのstrict health200、GC画面操作の成功は別であり今回は未実測。他資源G3/全体受入を合格にしない。
- 05B2のage/sourceKey projection限界、AppHost/DCP実exit未取得、既存TDD不足、全文文書gate/図本人視認/全体独立finalのheldは保持する。
- 親prefix/Dispatchと全旧子欄履歴を保持。自己編集はnormalreport子5欄、根拠明確な実装report2表現、/tmp proofのみ。製品/test/設計/台帳/実質結果/rawは変更しない。全自己process終端、未回収session/exit/承認待ちなし。applied:null、report_attestation_allowed:false。
