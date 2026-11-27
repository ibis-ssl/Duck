# Issue81 I81-04B 配布資材の調査報告

## Dispatch profile（親所有）

- selection_source: continuity_reuse
- task_kind: investigation
- signals: {work_class: judgment_heavy, uncertainty: high, change_radius: cross_module, criticality: medium, repetition: single, decomposability: independent_workstreams, decomposition_policy: forbidden, context_need: bounded_history}
- decomposition_disposition: 既存担当で逐次実行。新規担当の追加なし。
- automatic_floor: Sol/high（高い不確実性と判断を伴う調査）。ユーザーの現行モデル固定指示を優先してLuna/highを継続し、無断で変更しない。
- original_requested: {model: gpt-6-luna, reasoning_effort: high, fork_turns: none}
- executor: /root/issue81_luna_implementation
- applied: null
- application_status: reused_existing_agent_profile
- profile_observability: final_profile_hidden
- role_plan: original default role reused; effect unknown
- scope: Issue81/PR84、I81-04B固定配布資材・デバッグ構成。既存worktree/branch、基準5f280e33da9f0e93d7396373c9573b0f21424582。04Aは通常レビュー条件付き合格、全体未完了。
- boundary: 読取調査と本報告の子5欄のみ。外部変更・取得・導入・コンテナ作成起動・配置切替・mergeなし。
- parent_disposition: 読取結果を回収。既存AppHostの実image選択への接続はI81-05の範囲とし、04Bの資材定義と混同しない。8platform参照と3GHCRの公開由来は候補証拠、まだ採用・実供給・起動の成立ではない。Debug供給は不足を保持。
- evidence_validation: 親がSHA256SUMS.txtの68データentryを照合。manifest自身を含む69ファイル。証拠束はworker保存の未追跡資料として保持、未commit・未公開。最初の親読取はファイル名と列形式の仮定違いで失敗し、実名と3列形式を確認して回復。
- prior_publication_blocker: 04A記録commit/pushと既存PR報告は初回実行前拒否。後続の具体的ユーザー原文を根拠とする各同一操作1回の再審査で許可され、28f227dと既存PR報告更新が成功。04Bの本調査資料はそのcommit対象外。

- continuation_20261023: I81-04B2の供給契約調査。基準b37555e27ba82f9b78b1e6a4ccb754531419786c。04B1は通常判定pass_with_held、必須0、同HEADのCI39887015100で標準776・3発行・PDB10・実112参照/完全ID成功。初回調査本文は当時の履歴として保持し、以下の子欄へ04B2追補を加える。
- continuation_assessment: task_kind=investigation; work_class=judgment_heavy; uncertainty=high; change_radius=cross_module; criticality=ordinary; repetition=single; decomposability=independent_workstreams; decomposition_policy=forbidden; decomposition_disposition=prohibited_by_caller_policy; context_need=bounded_history。外部参照群は独立でも既存1担当で逐次実行する。
- continuation_profile: automatic_floor=Sol/high、明示ユーザー固定Luna/highを優先。同じexecutorの原起動requested={model:gpt-6-luna,reasoning_effort:high,fork_turns:none}を再利用。planned_runtime_profile=unknown(default role effect未観測)、applied:null、最終設定非公開。新しい起動/モデル切替を主張しない。
- continuation_boundary: Duckと公開一次資料・既存成果物のメタデータ読取、既存調査報告の子5欄と新証拠のみ。製品/設計/配布定義の変更は調査の阻害解消と最小案を親が統合してから同じ実装工程へ。外部変更・依存導入・認証/環境/ネットワーク設定変更・イメージ本体やデバッガー取得・作成起動・配置切替なし。AppHost接続は05、デバッガー供給は04B3。
- publication_correction: 本報告と初回68entry証拠は1e8f624で公開済み。初回Dispatchの未公開記録は当時の履歴。現在は取得済み候補と04B1成果を区別して04B2へ継続する。

## 実行コマンド

- I81-04Bの公開メタデータ照会では `docker manifest inspect --verbose <既存タグ>` のみ実行。8参照すべてexit 0。pull/build/runなし。
- GHCR package versions APIは3参照とも403、gh exit 1。stderrは `You need at least read:packages scope to get a package's versions. (HTTP 403)`。認証変更・API再試行なし。
- vsdbg配布先へのHTTP HEADのみ。応答200。installer scriptの最終URL等を記録し、本体は取得せず。
- 04AのPR #84、head `28f227dbcd0e7b1643203231942ce91718dfe72c` に対する既存 `.NET tests` pull_request run `39875364344` を読み取り確認。run completed/success。standard job `120159553408` のログでテストと発行出力を照合した。rerun/dispatchなし。
- 保存証拠: `reports/evidence/issue81-04a-final-ci-28f227-20261023/`。9ファイル（8 data + SHA256SUMS）、合計9218 bytes、manifest SHA256 `3732a8e467b7085ad1ecbc70740ba76b9895687779ab5837ca58ee9e71af7c41`。run JSON、選択ログ行、空stderr、exit code、run identity、コマンド記録を収納。前回の69ファイル04B束は変更していない。


### I81-04B2 2026-10-23追補

- 追加した公開metadata照会はDocker Hubのtag/repository APIとMCR manifest metadataのみ。curlは認証なしの公開GET、MCRは docker manifest inspect。8 endpointすべてexit 0。各command/stdout/stderr/exit/hashを reports/evidence/issue81-04b2-supplement-20261023-v3/ に保存。image/layer取得・build・startなし。
- 最初のPowerShell経由here-documentは引用符構文エラーでHTTP要求前に失敗した。後続Python subprocessのコマンドだけが実行済みmetadataとして数えられている。


### I81-04B2 2026-10-23追補2

- 認証情報なしMCR Registry V2 HEADは runtime:10.0 と aspnet:10.0 の両方HTTP 200、Content-Type application/vnd.docker.distribution.manifest.list.v2+json、Docker-Content-Digest取得に成功。各応答ヘッダーと正確なURL/argv/exitはv4証拠束へ保存。
- GHCRの未認証HEADは3タグとも401と標準Bearer challengeを返した。その後、ユーザー認証なしの公開匿名pull tokenをメモリ内だけで使う通常metadataフローでtoken endpoint 200、manifest HEAD 200を得た。tokenとAuthorization headerは表示・保存していない。既存の認証設定/ユーザー権限/スコープ変更なし。各401 challenge、redacted成功記録、stdout/stderr/exitをv4へ保全。
- GHCR package versions REST APIの既存403は別経路として保持し、再試行していない。既存Docker CLIで公開workflow list digestを@sha256指定したmanifest inspect --verboseも3件すべてexit 0、JSON descriptor metadataのみを保存。image/layer取得・build・startなし。

## 対象ファイル

- 既存設計: `Tracker/Design/Testing/aspire-simulation-test-environment.md` L35-L45 は実発行した3管理プログラムの配布物を指定。L224-L231 は同じ版のDebugHost実行物・移植可能なデバッグ情報、事前配置する `/vsdbg/vsdbg`、`/src` からWindowsソースへの対応を指定。
- 既存発行検査: `scripts/tests/verify-linux-publish-outputs.sh`。Releaseで3プロジェクトを実際に発行し、DLL/deps/runtimeconfig、設定・静的資産、共有実行基盤を検査。DebugHostのPDBとそのソースパスは検査しない。
- 設定・CI: `Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj` は `net10.0` Web SDKで、明示的な `DebugType`/`DebugSymbols`/`PathMap` 設定なし。`.github/workflows/dotnet-test.yml` は標準テストの後に上記実発行検査を行う。I81-05のAppHost image選択は今回の変更・提案範囲外。
- 前回の公開メタデータ証拠: `reports/evidence/issue81-04b-readonly-20261022/`（69 files / 220209 bytes、manifest SHA256 `6a8db74566edd185c05c519e3dd7d0e789cf05e82aba428566e10c1f45ec1b31`）。原本不変。


### I81-04B2 2026-10-23追補

- 追加対象は既存仕様 Tracker/Design/Testing/aspire-simulation-test-environment.md L35-L45、L224-L231、現行image参照 Testing/Duck.Testing.AppHost/Program.cs、発行検査 scripts/tests/verify-linux-publish-outputs.sh、現行RuntimeHost/DebugHost/RefereeDriver.Hostプロジェクト。製品・設計・配布定義は編集していない。
- 基準調査69ファイル reports/evidence/issue81-04b-readonly-20261022/ は不変。新しいDocker Hub/MCR応答は世代別bundleに格納。


### I81-04B2 2026-10-23追補2

- 最終世代は reports/evidence/issue81-04b-supplement-20261023-v4/。v3の原raw/manifestをv4内にも明示名で保持し、v4でMCR HEAD、GHCRの各応答段階、既存CLIの固定digest descriptorを追加した。token値やAuthorization headerは含めていない。
- v4のinventory.tsvは59データentry、98,580 bytes、SHA-256 c34e1828d94a15d6f49aec002cad7ea8eab8c9da94abb82c05504fd95e831bbd。元69-file束は変更していない。

## 指摘事項

- exact CI run `39875364344`: `.NET tests` job success。standard suiteは **776 passed / 0 failed / 0 skipped**。同じjobの `Verify Linux application publish outputs` は成功し、RuntimeHost・DebugHost・RefereeDriver.Hostの3つすべてで「Linux publish output contract passed」。テストとpublish検査を別結果として記録する。
- `ASPIRE-NET-002 Linux packet flow` と `ASPIRE full-stack Linux acceptance` は両方skipped（pull_request event）。失敗診断収集/アップロードstepもsuccess jobの条件によりskipped。artifactを取得・生成したとは扱わない。
- I81-04Bの次の自然な最小単位は、**DebugHostの実発行物にportable PDBとソースパス対応を定義し検査する契約**。これならI81-05のAppHost image選択、外部vsdbg、image buildを待たずに進められ、設計のデバッグ対応条件へ直接つながる。
  - 仕様→固定期待値: 実際の `dotnet publish Tracker/Tracker.DebugHost` 出力に `Tracker.DebugHost.dll` と隣接 `Tracker.DebugHost.pdb` を含める。PDBのCodeView識別子と対象DLLの識別子が一致し、portable PDB documentに `/src/Tracker/Tracker.DebugHost/Program.cs` を含む（および他の同一repo source pathを `/src/` 以下に収める）。Windows側のsourceFileMapで `/src` を同じrevisionのcheckoutへ対応させる。
  - 先行test/scaffold候補: 既存 `scripts/tests/verify-linux-publish-outputs.sh` が出したDebugHostの実publish directoryを対象にする。manifestや新しいファイル一覧の自己検査にせず、実行物/PDBの対応とsource document pathを固定値検査する。追加NuGet packageなしで使えるSDK/.NET metadata APIの有無は実装前に確認し、別依存導入をしない。
  - 予想されるRed: 現行projectには `/src` へのPathMap/source mapping設定が見当たらず、runner上のcheckout絶対パスがPDBへ入る可能性が高い。ただし今回のCIはPDB内部を保存・検査していないため、Redの実測とは主張しない。先行testで既存実publishの値を採取し、固定期待値との差を確認してから実装する。
- プラットフォーム契約: 設計はLinux向け配布を指定するがCPU architecture/RIDは指定していない。現profilesはRIDなしのframework-dependent publish、検査scriptは明示的にRIDを付けない。`ubuntu-latest` runnerだけでは利用者向けコンテナplatform選択の根拠にならない。したがって前回記録のlinux/amd64 manifest digestsは照会時のamd64 child descriptorにすぎず、正式な採用platformではない。image base/Dockerfileでplatform別digestを固定する単位は、対象platformが設計決定されるまで保留する。
- 外部取得なしで完了できる上記PDB/source mapping契約とは別に、Debug専用実イメージへ含めるvsdbg本体のversion/digest、取得元固定、再配布条件は未供給のまま。installer URLへのHEAD応答は本体や許諾の根拠ではない。現状、追跡Dockerfile/.pdb/vsdbgはない。通常ビルドのbin/obj PDBは配布済み/debug image資材とは扱わない。


### I81-04B2 2026-10-23追補

- Docker Hubのtag metadataが返したタグindex digestとplatform childは次の通り。以前のdocker manifest inspectで選ばれたamd64 childは、tag APIのamd64 childと全3件一致。
  - tigersmannheim/sumatra:2025 — index/list sha256:9ad3f65410f1c33b5b00400390dd3fb85c35ba6d76500fa68b4d51b4c3cd90c1。linux/amd64 sha256:0c05451f26aeb21bffde4c3d6858b6f441bd6bfaf8286f0ecd799241cb1fbc6c、linux/arm64 sha256:e07efb360effb09dc716f37943f46f0ae9eb3569f0c5f9bf4c6be88599c0b5d0。
  - roboticserlangen/autoref:2025.1.0 — OCI index sha256:de4f06a9bcfad524b3bae2015f92bb3fc3a209295eb668a34d5a0b329616bf68。linux/amd64 sha256:04067106660f5ff31d1ebe4db9b19b1078a2124db3adabae79f6c9d23fa99317、linux/arm64 sha256:3453c128d3242e5a6eabfae53ed5c9a6b9c5a384a8aa9e69e2e4e5d932420373。
  - robocupssl/ssl-game-controller:3.20.3 — OCI index sha256:d62d772de063c2951f600a0b7898803f97822f4f46a412a731e42f7d82a99dc7。linux/amd64 sha256:bd0d621373098bbf6f27243a2c1b9a219d6402a96dd30b9825ded5258c106721、linux/arm64 sha256:1a94486e2c5d6ac9ce5c443eeafe35bcd4af674598b41604ee6ab558d748ad72。
- MCR runtime:10.0 と aspnet:10.0 はDocker manifest listとしてlinux/amd64、linux/arm/v7、linux/arm64を列挙する。CLI出力はroot indexのDocker-Content-Digestを含まないため、root digestは未確認。JSON再整形内容からroot hashを作っていない。
- source要件の区別: Issue/設計が求めるのは外部成果物のregistry/reference、版・digest、対象環境、出典。SLSA等の全外部image commit-to-image暗号学的provenanceを必須にする文言はない。現時点の最低限の出典はregistry/repository/tagと対応するindex digest/platform表。より強いprovenanceは得られた分を記録するが、Docker Hubのsource commitが不明なだけで現要件を阻害しない。
- 既存のGHCR workflow記録はframework commit a52b6bdecd97a277544dd6f275afbf4483781e12 → list digest f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79、crane commit 4063cd31cd5b11b1cc919003907f5f4c527b252d → db6ab99009f98003c2f078e5d0820004a8e726c7a293965e7022ecfdb1bb2a49、Orion commit d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a → aef193194cce7c1c3729ac344065945b3d92c0a7ccdca0d5036cc472a8bfa61a を示す。一方、現在のtagとlist digestの同一性はGHCR package versions API 403のため未確認で、再試行していない。既存registry観測では各GHCR資材のplatform childはlinux/amd64のみ。
- Docker Hub repo APIのsource欄は3件ともnull。AutoRef/SumatraのタグAPIにもsource_urlはない。Game ControllerのDocker Hub説明はGitHub upstreamへのリンクを持つが、image tagとsource commitの対応証明ではない。
- platform候補の比較: verified top-level index/list digestをpinすればそのindex内のplatform選択を保つ。child digest pinはamd64等の特定platformを明示して利用対象を狭める。Windows host上のユーザー別Docker Desktop Linux containersまでは設計にあるがCPU/RIDは指定されない。DockerHub3件と.NET baseのmetadataがamd64/arm64等を含むことは受入要件を決めない。GHCR3件はindex pinでも現状観測のarm64対応を作り出さない。


### I81-04B2 2026-10-23追補2

- MCR Registry V2 HEADで得たroot index digest:
  - mcr.microsoft.com/dotnet/runtime:10.0 — sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3。子はlinux/amd64 sha256:1bac183f0f5e1b7dd58cf1083fa79e6310d47afe3ca5c9130c910ebba7720929、linux/arm/v7 sha256:0e7e2b03b5f3c3d0546c7d6141f1b75f682b0bac02d11a508a438991ee322b2d、linux/arm64 sha256:9aaf1ab716a4e15a7c8cf773bcf7638ae1b5114bccee5611ce371e1a043d2033。
  - mcr.microsoft.com/dotnet/aspnet:10.0 — sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca。子はlinux/amd64 sha256:f879b8a9cb3f6694b8b0c9fa530eb0c39d8920db09c05a7edbd6c4c6c104ae1b、linux/arm/v7 sha256:e8be2c890668fb4b1d56484b10694c54e060fe362363d916f9847f374158a546、linux/arm64 sha256:3a05e37c371f358d2b5cb9687d9992171b34b8c9146c283bef865bdbd2c53c55。
- GHCR標準匿名registry metadata HEADでcurrent tagsのContent-Typeは3件ともOCI image index、root digestはframework sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79、crane sha256:db6ab99009f98003c2f078e5d0820004a8e726c7a293965e7022ecfdb1bb2a49、Orion sha256:aef193194cce7c1c3729ac344065945b3d92c0a7ccdca0d5036cc472a8bfa61a。各々既存公開build workflowのpublished-list digestとbyte-for-byteの値一致。固定@sha256参照の既存docker manifest inspect --verboseでもmetadata読取に成功し、各index descriptorはlinux/amd64 child（既存一覧参照）とunknown/unknown attestation childを列挙した。
- したがって8つ全参照のroot index digestを照合済み。Docker Hubの3つは前追補のtag API値、MCR2つは今回のV2 HEAD、GHCR3つはcurrent tag V2 HEADと既存published-listの一致。GHCR package versions REST 403はこの標準registry metadata経路の成功を妨げなかった。401は標準challenge、匿名flowは成功。資格情報/設定変更なし。
- 前追補の「MCR root未確認」「GHCR current tagとの一致未確認」は調査時点の記録として残し、本追補で解消を訂正する。Docker Hubのsource commit-to-imageリンク不足は別件で、現タスクのsource要件を越える追加保証であるため阻害条件にしない。

## 結果

- 04A exact PR CIはsuccess: 776件の標準suiteと3管理プログラムの実Linux publish/file checkが成功。packet/full-stack acceptance jobはskippedで、実行成功ではない。
- 04Bの推奨次単位はプラットフォームや外部供給に依存しないDebugHost portable PDB/source path contract。変更候補は既存publish validator、DebugHostの発行設定、同じ実発行物を読む小さなmetadata test helper。親のcanonical分割前にコード・設計は変更していない。
- 前回のamd64 digestは採用決定ではなく読取結果として保持。外部3 Docker Hub refsのsource provenance、vsdbgの固定資材・配布条件は未解決。
- 今回、CI rerun、コード/設計/配置/設定変更、image取得/build/start、外部repo改変、依存導入はなし。報告の子5欄とCI証拠束のみ追記。


### I81-04B2 2026-10-23追補

- 外部取得を待たずに始められる最小候補は、I81-04Aの実publish出力をCOPYする通常用3つのDockerfileと固定runtime/aspnet base参照の定義。候補パスは各管理プログラムの既存プロジェクト直下のDockerfile。DebugHostへvsdbgを含める工程は04B3、AppHost image選択の変更は05に残す。
- 先行検査の固定期待値は、各Dockerfile FROMが固定registry/repositoryとindex digestを指すこと、COPYの入力が既存CIで実際に生成した対応04A publishディレクトリであること。validatorは実publish出力とDockerfileのCOPY入力を結び付け、単独manifestの自己検査にしない。外部scenario imageのロックを加える場合は既存AppHost宣言の正確なregistry/repository/tagとの対応も検査するが、resource wiring変更は行わない。
- digest pin方針はindex/list digestを第一候補とする。これで現在のtagが選ぶplatformの意味を維持し、amd64 childの選択を新しい製品要件にしない。ただし現行GHCR3画像はmetadata上amd64 childしかないため、全stackのarm64対応を保証するものではない。
- 最小固定値を完全に確定する前に不足しているのはMCR runtime/aspnetのroot index digestと、GHCR current tagと公開workflow list digestの照合。これは公開metadataだけの不足で、Docker image/layer取得やログイン設定変更を必要とする機能条件ではない。DockerHub3タグはroot index digestとchild platform対応まで確定した。


### I81-04B2 2026-10-23追補2

- 固定供給値はDocker Hub3、MCR2、GHCR3すべてindex digestまで確定した。推奨する検査/実装は、通常用3管理プログラムのDockerfileを既存プロジェクト配下へ置き、RuntimeHostとRefereeDriver.Hostをruntime indexへ、DebugHostをaspnet indexへ固定し、04Aの実publish出力をCOPYする形。既存AppHostの外部image参照は8値の供給表と照合できるが、AppHost resource/image binding変更は05へ残す。DebugHostのvsdbg層と供給は04B3。
- 固定先行testの期待値: (1)各DockerfileのFROMにregistry/repository@index sha256が使われる、(2)COPY源は同CI実行の04A publishが生成した実ファイル/ディレクトリ、(3)外部資材表はAppHostに現存するreferenceを対応するroot digestへ解決する、(4)各root indexのmedia typeとplatform child一覧は保存済みmetadataと一致。publish validatorを実出力に結び、自己参照だけのmanifest試験にしない。新規CI/image buildは今回提案・実行していない。
- index digest pinを選べばCPUごとのtag解決動作を保ち、Linux/amd64に新たに固定しない。GHCR資材の現index descriptorはamd64とunknown/unknownのみであるため、実際の対応platform表示はそのまま記録しarm64を主張しない。

## リスク

- portable PDBとソースpathの整合は現行CIのpublish successからは証明されない。先行検査で実publish出力を読み、CodeView/PDB識別子とdocument pathを見てRedを確認する必要がある。
- `/src` のPDB document pathとWindows側sourceFileMapは対応する同一revisionを必要とする。SourceLink/PathMapの正確な実装方法は未検証で、依存追加なしの実装可能性を調査してから選定する。
- コンテナ基底/外部manifestを固定する前に、製品のCPU architecture/platformを設計で決める必要がある。CI runnerのplatformから利用者契約を推測しない。
- GHCR API 403、Docker Hub由来の未確定、vsdbg実体/再配布条件の不足は継続。digest確認を実供給・起動成功と表現しない。

### I81-04B2 2026-10-23追補

- MCR queryはdocker manifest inspect成功でplatform childを列挙したが、root index digestを出さなかった。正確なMCR index digestが必要なら、次に必要な読み取りはregistry manifest応答のDocker-Content-Digestを取得するmetadata queryのみ。image pull/build/startやbase indexのハッシュ推測はしない。
- CPU supportの受入は設計上未決定。index pinはplatform選択挙動を残すが、GHCR資材の現在のamd64限定を解消しない。child pinを選ぶなら対象CPUを明示した仕様決定が先に必要。
- Docker Hubの公開出典metadataはtag/index digestとplatformを特定するが、3つともsource fieldがnull。Game ControllerはUpstream GitHubを説明文で示すのみ。現要求にprovenanceが明示必須とは読めないため、commit-to-image保証を新しいblocking gateとして扱わない。
- 今回製品/設計/配布定義/AppHost/依存/認証環境の変更、pull、build、start、GHCR再照会、CI実行はなし。8 metadata subprocessはすべて終端し、実行中processや未回収exitはない。


### I81-04B2 2026-10-23追補2

- 実index metadataとdigestを確認したが、image/layerをpullしておらず、Dockerfile buildや起動も行っていない。したがって固定値は供給元の公開参照を指すことの証拠であり、ローカルに実供給済み/起動可能という証拠ではない。
- CPU/RIDの正式な受入範囲は未指定。index固定はplatform選択を維持するが、GHCR3資材はamd64 childのみのため、arm64で全stackを動かすことを保証しない。arm64全stackが受入条件なら、既存資材のmultiarch供給または代替が別途必要。
- DockerHubのsource commitをimage digestに暗号学的に結び付ける情報は未確認。一方、要求文はregistry/source referenceとdigestの確定までで、全外部資材のcommit-to-image証明を明示していない。より強いprovenanceは追加保証として分離する。
- 全registry subprocessは終端。最終metadataの応答はMCR2件200、GHCR未認証HEAD3件401 challenge→匿名token endpoint3件200→manifest HEAD3件200、Docker CLI固定index descriptor3件exit0。GHCR package versions REST APIの403履歴は保持、再試行なし。処理中process/未回収exitなし。
### I81-04B2 test-stage統合訂正（2026-10-23）

- 履歴上のv4 evidence path typo reports/evidence/issue81-04b-supplement-20261023-v4/ は reports/evidence/issue81-04b2-supplement-20261023-v4/ が正しい。旧記述は履歴として残し、この訂正を正本とする。
- 旧推奨文の「RuntimeHostとRefereeDriver.Hostをruntime indexへ、DebugHostをaspnet indexへ」は誤り。既存 Testing/Duck.Testing.RefereeDriver.Host/Duck.Testing.RefereeDriver.Host.csproj に FrameworkReference Microsoft.AspNetCore.App があり、scripts/tests/verify-linux-publish-outputs.sh も同frameworkを要求する。従ってRuntimeHostのみruntime index、DebugHostとRefereeDriver.Hostはaspnet index。04B2のDockerfile候補もこの基底対応を固定検査する。
- canonical scope訂正: 04B2は通常用3 Dockerfile、8 index参照表、同一CI実publishとの対応検査。debug専用定義/vsdbg供給は04B3、AppHost bindingは05。終了条件は定義・実publish対応・通常reviewであり、image build/start成功ではない。
- Test-stageは先行検査のみを追加し、製品定義はまだない。15 unit testsとPython/Bash syntax checksはpassしたが、実publish Redは未実行。exact PR CIで実発行出力を作った上で評価する。
