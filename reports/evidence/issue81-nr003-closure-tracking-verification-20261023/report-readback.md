# Sub-agent実行レポート

## タスク

Issue81 / PR84 の I81-04B1。実発行したDebugHostの実行体・対応デバッグ情報・ソース参照を固定試験で検査する単位の通常レビュー。独立最終レビューではない。

## sub-agentを使う理由

実装担当と異なる既存の通常レビュアーによる要件・差分・証拠の確認を継続する。

## 対象範囲

基準28f227dbcd0e7b1643203231942ce91718dfe72cからの先行試験1e8f624、最小修正cd561ed37e7f578d9289af6a392f2cb7333ec983、承認済み主設計、台帳と実装報告・検証証拠。修正cd561edのCI39880689547は成功。記録同期f92b5df2d5ebd4a9269e97e141ca53bb451d10ccがレビュー対象で、同HEADの遠隔検査は回収中。最終判定時にその結果を区別して照合する。

## 対象外

04B2/3の未採用供給条件、05以降の配線、実機受入、独立最終予約・凍結・証明。実装変更、第三者レビュー変更、外部取得や設定変更は行わない。

## Dispatch profile

- selection_source: continuity_reuse
- task_kind: review
- proposed_profile: gpt-6.1-sol / high
- original_requested: {model: gpt-6.1-sol, reasoning_effort: high, fork_turns: none}
- executor: /root/issue81_normal_review
- application_status: reused_existing_agent_profile
- applied: null
- runtime_profile_observability: final_profile_hidden
- role_plan: existing original default role; final effect unknown
- decomposability: independent_workstreams
- decomposition_policy: forbidden
- decomposition_disposition: prohibited_by_review_lifecycle
- parallelism_mode: single_agent
- reviewer_continuity: 同じIssue81の通常レビュアーを維持する。新規起動なし。
- selection_source_evidence: ユーザーのレビューSol/high指定を維持。再利用APIによるモデル変更を主張しない。
- report_persistence_mode: normal_persistence
- verification_capability: remote_ci_only

## 実行コマンド

- normal_review、担当=/root/issue81_normal_review。work-context-manager / review-enforcer / review-worker / report-writer / report-output-managerの既読実ファイルとhashを確認し、sub-agent-task-managerも実読した。後者のdispatchは親の責任であり、既存Sol/high通常レビュアーを再選択・新spawnせず、built-in code reviewを1人で継続した。original_requested=gpt-6.1-sol/high・fork none、applied:null、application_status=reused_existing_agent_profile、report_attestation_allowed:false。
- FA780のPowerShell login:false / require_escalated → wsl.exe -d Ubuntu -- Python stdinを使用。repo=/home/donabe/Duck-issue81-aspire-desktop-bridge、branch=task/issue81-aspire-desktop-bridge、reviewed HEAD=f92b5df2d5ebd4a9269e97e141ca53bb451d10cc。root AGENTSなし、既存Skill参照を継続。本report親prefix/Dispatchを最初と保存前に読み、子5欄だけを更新した。
- git rev-parse/status/diff/show/ls-files、source/reportの行番号付き読取、JSON/manifest/SHA256照合を実施。製品/検査器/test修正、新test/build/lint/実機/コンテナ/外部状態変更、GitHub操作、依存・設定・認証・network変更、commit/pushなし。標準試験は既存ローカルproto不足を補修せずremote_ci_only、保存されたexactCIで確認した。
- 6証拠groupを直接照合：readonly68、04A公開CI8、prepublish76、Red＋fix55、cd561Green32、exactf92 v2の10data entry。原本・保存hash、固定3source、段階別tracking sourceと入力・config hashを確認。新exactCIの親untracked収納も読取対象として区別した。
- portable PDB実装の仕様照合に必要なdotnet公式PortablePdb-Metadata/PE-COFF addendumを読取。新しい外部状態変更やサービス操作ではない。20byte照合不足はコードと公式仕様から静的に確認し、再現testを新たに実行したとの主張はしない。
- 読取wrapper2件は文書snippet hashを末尾LF付きfile hashと仮定したAssertionErrorでexit1。2件目は空行paddingとも仮定していた。実内容を読んで、保存hashがLFなし選択行text、実mirrorはその内容＋LF1byteであることを確認し、後続照合はexit0。lint失敗や製品操作ではない。他の今回review commandはexit0・同期終端。
- 親待機中断の通知後は初回読取exit0・sessionなし・report未保存を照合し、未読部分から継続した。具体的な中断tool errorは観測されていない。新しい検査の重複発行なし。最終保存はsingle quoted here-string→ASCII Python stdin、親prefix/他path/HEAD不変・末尾1newlineをguardして全文readbackした。

### I81-NR-003 修正検証 (fa80b23 / 2026-10-23)

同一通常レビュアー・元Sol/high・fork none・applied:nullを保持したbounded fix検証。前回report SHA532c733aa4a323f44f4f0af4c11cd0c9b211754e8131e169fc1ab9118a2f243fを最初に読み、旧failを保持した。技術targetはfa80b23aa362ed98c4beaf21605eeb5d1b18cbb2、前回f92。既読Skillの適用を継続し、新spawn/独立finalなし。

FA780 PowerShell login:false/require_escalated→WSL Python stdinでsource差分・固定binary/manifest/JSON・実装report matrix・既存CI rawを読み取った。修正completenessに必要な限定範囲だけを扱い、旧包括範囲の再レビューや新test/build/lint/CI/外部操作を行っていない。今回の全tool commandはexit0、同期終端・sessionなし。保存binaryの既知field/差分をstructで読んだだけで、checker/unitの再実行ではない。

## 対象ファイル

- 基準28f227dbcd0e7b1643203231942ce91718dfe72c→f92b5df2d5ebd4a9269e97e141ca53bb451d10ccの全256path（非evidence8＋evidence248）、後続exactf92証拠収納、本normalreport。Issue81/PR84、既存branch/baseを維持。
- 全行確認：scripts/tests/verify-debughost-portable-pdb.py（240行）、test-debughost-portable-pdb.py（100行/7unit）、verify-linux-publish-outputs.shのPDB接続差分と既存実publish/cleanup契約、DebugHost LinuxContainer.pubxmlのPathMap2行。親所有tasks/phases、実装report/調査reportと公開証拠を確認した。
- 直接依存：既存DebugHost Web SDK/net10.0/Release portable PDB出力、ProjectDirectoryからrepo rootのMSBuild正規化、既存3profile、通常csproj/Program/run条件、標準test後の実publish workflow。これらの既レビュー契約と現在差分を照合した。製品差分は明示DebugHost profileのPathMap＋日本語コメントだけ。通常build/run/csproj/他profile/依存version/workflow/主設計は基準から不変。
- 承認済み主設計 aspire-simulation-test-environment.md:226,229–231、調査report:42の同発行DLL/portable PDB照合と固定/src document契約、tasks I81-04B1を受入根拠とした。04B2/3の供給・platform/digest・vsdbgや05以降の配線/実機を今回へ追加要求しない。
- 固定3sourceは先行1e8f624/修正cd561ed/対象f92/disk/prepublish snapshotでbyte一致：parser 58e0beb075e195fcb9985a109fafe08580d1f83dbbf993945a5cc33d02465a24、unit a0dc746080a192c162fa5db7d894bb2a3d86906e1133eff9e652af032c7641c8、publish script 7c430e8daaa1ce5aa45e7490cbc7a3e6a25ab04a3144c08de48e120904635500。cd561→f92の非記録差分は0。

### I81-NR-003 修正検証 (fa80b23 / 2026-10-23)

最新技術reviewed HEAD=fa80b23aa362ed98c4beaf21605eeb5d1b18cbb2。限定差分はverifierのStamp取得/全20byte比較、testのsynthetic PE/PDB fixture3ケースと戻りtuple対応、関連tracking/report/evidence。既存PathMap/通常runtime/host project/他profile/workflow/依存/主設計とpublish shellはf92から不変。

現在sourceはverifier b91b4361c3995f9cae0c22e00bc63a10696dd4b2a649f7001042fa62a08ab561、test 4c1a1924bc2833a4d16360e5e3d14ceb27af7fe7a05542eb988e9bc42decd18a、shell旧7c430e8daaa1ce5aa45e7490cbc7a3e6a25ab04a3144c08de48e120904635500。3件ともHEAD/disk byte一致。実装report最新版8874488d8606873419eef6408744ba2c8a82f63ccdf4e92549823e2d7124ad08の4行matrixを照合した。

証拠はissue81-nr003-red-green-20261023（inventory-v3.tsv 48entry/SHA bd5cd8e11e4fbb8f3754ea33439bc56f5456fef02e04d3933c13f00d3ef306a5）、issue81-nr003-tracking-lint-20261023（12entry/SHA c84827f102f6f29f18da7c7769efba3b67089d0d68f867f50313f0e28b7703f8）、親untrackedのissue81-ci-fa80b23-20261023（17entry/SHA 0da17eb5c8df5600cba43256f5e459bcaca38b08fa52856729bd824fa869fc24）を全entry hash/size照合。v1/v2の過去manifestを保持している。

## 指摘事項

### I81-NR-003 / P2 / open — Portable PDBの照合IDからStampを落としている

- 位置：scripts/tests/verify-debughost-portable-pdb.py:211–212、直接原因は同185–196。read_portable_pdbは#PdbのID20byteを返すが、read_codeview_identityがdebug directoryのTimeDateStampを捨て、verify_contractは先頭GUID16byteだけを比較している。
- 根拠：主設計226/229の「対応するportable PDB」と、承認された04B1の「同じ発行結果の実行体との対応」。[dotnet公式PE/COFF addendumのPortable CodeView節](https://github.com/dotnet/runtime/blob/main/docs/design/specs/PE-COFF.md#codeview-debug-directory-entry-type-2)では、Portable PDBの照合IDはCodeView GUID16byteとdebug directory TimeDateStamp4byteの連結で、#PdbのIDと対応させる。Age=1だけを加えても残るStamp4byteの照合にはならない。
- 具体的影響：GUIDは同じ・Age=1・/src documentは有効で、#Pdb ID末尾4byteだけがPE側Stampと異なる組を現条件は受理し、DLL identity matchesと報告する。同発行物対応を検証する今回のCI正常経路の検査器欠陥であり、追加許可待ちのcapability gapではない。実際のGreen発行物が不一致だったと主張するものでもない。
- 必要修正：当該CodeView entryのStampを保持し、#Pdbの20byte全体と照合する。GUID/Stamp/Ageの役割を分け、Age=1制約と現在のdocument検査/CLI非0診断を保つ。修正範囲は検査器・必要なfixture/negative test・関連report/trackingに限定でき、PathMapや既存通常runtimeの再設計は不要。
- 必要な先行負例：実PE/portable PDBの小さなbinary fixtureまたは実発行物からの限定変異で、GUID同一・Stampだけ不一致・Age=1・/src文書有効を固定する。read_codeview_identity→read_portable_pdb→verify_contractを通し、現在実装では受理されることから、修正後に契約診断で拒否されることを確認する。単に判定関数をmockして期待する値を返すだけで代替しない。
- 元7unitの意味/負例を緩めず、整合する20byte IDの実fixture成功、GUID不一致拒否、Stamp不一致拒否、Age制約とsource path回帰を保つ。helperの戻り値形式を変える場合は既存mock fixtureの整合部分だけ理由を記録して更新する。元unitの固定3hash履歴は保持し、今回のfinding修正に必要な追加/変更fixtureは別hashで先行固定する。

### 必要対応のcompleteness matrix

| action | production / actual composition path | 必要なfocused evidence | 現在 |
| --- | --- | --- | --- |
| 完全IDを保持・照合 | read_codeview_identityのentry Stamp→verify_contract→#Pdb ID20byte | GUID+StampとAge=1を区別するsource差分 | open |
| Stamp不一致を先行検出 | 実PE/PDB fixtureを2readerとverify_contractへ渡す | GUID同一/Stampだけ不一致のruntime Red→同fixture Green、正常20byte組も成功 | open |
| 既存期待値と実発行を維持 | CI publish script→既存3実project→新checker | 既存7unit意図、GUID不一致/文書負例、標準776、3publish、実PDB/source契約と完全IDのGreen | open |
| 証拠・記録を同期 | exact修正target source/CIとreport/tracking | 固定hash/Red/Green/commands/exits、旧f92成功と新完全ID証拠を分離 | open |

### その他の包括確認

- 他の必須findingなし。metadata root/stream範囲、圧縮整数1/2/4byteと不完全入力、Document index幅とblob範囲、UTF-8 segment、PE32/PE32+・debug RVA/section・CodeView RSDS、欠落/破損の非0診断と既存7unitを静的確認した。
- Document.Nameの絶対path先頭index0は空stringとして扱い、現在のUnix区切りと/src固定名を復号する。今回の対象は既存SDKが発行するDebugHostのportable PDBで、任意形式/任意入力すべての完全validatorを保証するものではない。
- PathMapはGetFullPathでDebugHostからrepo rootを求めEnsureTrailingSlashでprefixを区切り、明示LinuxContainer発行だけへ適用する。通常local debugに無条件適用しない。日本語関数docstring/XML説明、旧3publish契約・static assets・終了cleanup・通常起動を保つ。
- 外部候補の8参照/3GHCR公開由来は未採用。package API403・vsdbg installer HEAD200を本体供給/実行許諾/実起動の成立へ読み替えず、追加必須条件へ拡大していない。

### I81-NR-003 修正検証 (fa80b23 / 2026-10-23)

**最新状態：I81-NR-003 / P2 closed（fa80b23）。追加必須findingなし、残る必須修正0。** 前掲f92のopen/failは発見時の履歴として残し、この修正検証で更新する。

read_codeview_identityは同じCodeView entryからGUID・TimeDateStamp・Ageを返す。verify_contractはGUID16byte＋struct.pack('<I',Stamp)4byteと#Pdb ID20byteを比較し、Age=1と既存source/欠落/破損の拒否を維持する。標準仕様のGUID/StampとAgeを取り違えておらず、元findingの原因を直接修正している。

| 元必須action | 今回確認したpath/証拠 | closure |
| --- | --- | --- |
| 完全ID保持・照合 | readerのentry[1]→verify_contract全20byte比較、元source判定は不変 | closed |
| Stamp-only先行負例/正常組 | 実structured PE/PDB fixtureを両reader→verify_contractへ渡すv3。正常成功/Age2拒否、Stampだけ不一致を旧checkerが受理して9/10 Red。修正後同fixture10/10 | closed |
| 旧7意図・実publish回帰 | mock4件とreaderassert2件のtuple形だけを戻すと現test全byteが凍結v3へ一致。exactfa80で旧7＋新3、標準776、3実publish、実PDB112/srcと完全ID比較が成功 | closed |
| 証拠/記録再確認 | source/fixture/hash/Red/Green/CI fullmerge/command exits、実装matrix4行とtracking2行証拠を照合。旧f92成功とは分離 | closed |

保存fixtureも直接読取確認した。validとstamp-mismatchのPEは0x204の1byteだけが異なり、Stampは0x12345678対0x12345679。GUID・Age1・PDB全byteは同じ。Age2 fixtureは0x230だけが異なる。全PDBはSHA5d70f096d0c9e98c1ac7c57d272b60ecae927e530531a1612e82b278d69ca9c1で一致。これらはsyntheticな構造fixtureであり、起動可能な製品バイナリや実コンテナとは表現しない。

## 結果

**I81-04B1通常判定：fail（要修正）。I81-NR-003 P2 open、必須1件。** exact対象CIは成功だが、完全PDB IDの契約を現在checkerが検証していないため通常レビュー合格にはしない。独立final/Issue全体完了/attestationではない。

### 先行Red→最小修正Greenとexact対象

- 1e8f624424c80f8015179c7b8437918f250d657a / PR CI39878834053 attempt1は標準776/0/0、3publishと基本出力検査、7unitが成功。その後実DebugHost PDBをparserで読み、固定/src/Tracker/Tracker.DebugHost/Program.cs欠落・runner絶対pathを検出してexit1。コンパイル失敗やmockの失敗ではなく、source mapping契約の実runtime Red。新Stamp負例のRedではない。
- cd561ed37e7f578d9289af6a392f2cb7333ec983 / PR CI39880689547 attempt1はrun completed/success。標準776/0/0、3publish、7unit、実PDBの112 normalized document /src/と現在のGUID比較が成功。profile2行以外の製品変更なし、固定3hashは不変。元source mappingのRed→Greenは有効で、未検査Stampを充足した証拠にはしない。
- exact f92b5df2d5ebd4a9269e97e141ca53bb451d10cc / PR CI39881770413 attempt1を直接照合。run全体completed/success、標準776passed/0failed/0skip、3publish、7unitと実PDB112 document契約の成功ログ。workflow checkout merge refは71e21ca941904f2fb788763015c7db76b2fcc230（base88cb20f）。候補CIの成功を移転していない。
- packet/full-stackはskip、artifact0/成功個別TRXなし。個別776件またはPDB実byteを成功artifactで再取得したとはしない。gh watch95355は中断後final exit不明、Unknown process idで非稼働。別のrun view/log/artifact/compareのexit0とrun終端metadataで成功を確認し、watch exit0とは推測しない。

### 証拠・文書・coverage

- Red-only31file時点の旧manifest90fe14ffは別名保存なしで現物照合不可。この監査履歴不足を明記し、最終55entry manifest33c42136aa5a342ac2d5fa466959384e009e91cbd5179739739b0dfc9be335f6でCI raw全件一致を確認した。初回Redの事実を失わせるものではないが、旧manifestを復元・保全済みとはしない。
- Green v1/v2は両方存在、採用v2は32entry/10ab23d5888ab5d56dcb84dbf79d693bfe5a0cc867df0658ed5f858f7c50e86a。exactf92もv1保持、v2は10entry/603d5b192ad2d326149a81ec19f706cecece2e0c327631fc87b1fcbfccdfb5ccで全hash一致。
- 先行文書入力はbaseline28f227→1e8f624の5本文行、Red後は1e8→cd561の2行、Green後はcd561→f92の2行としてsource/hashを分離。現2行（tasks34/phases19）の選択text hashと実mirrorの行内容一致、19config/checker/dictionary hash一致、selector2files・textlint/CSpell/Sudachi exit0を確認。保存snippet hash/bytesは末尾LFなしtextに対応し、実file byteはLF1byteを含むため、両者のhashを同一と主張しない。
- prepublish76entryには7unit/AST/bash syntax成功、最終5行focused三検査0と20config一致を含む。過去wrapperの未実行/null exit/exit1、Windows既定Pythonのyaml不足exit2と正規WSL venv成功を区別。新しい依存導入はしていない。
- 要件/設計/全差分/直接依存、通常互換、MSBuild設定適用、PE/PDB/Document境界、実CI組成、負例adequacy、TDD/source固定、報告/追跡/保守性を確認。完全IDはchecked_with_finding、他は現在のbounded scopeでchecked_no_finding。追加許可が必要なcapability gapや未探索の阻害scopeはない。
- 本report子5欄のみnormal_persistenceで保存し、親prefix/他path不変・末尾1newline・全文readbackを確認。全review process終端、未回収exit/session/保留approvalなし。元I81-NR-001/002 closedとEV-I81-04A-01 closedは変更しない。

### I81-NR-003 修正検証 (fa80b23 / 2026-10-23)

**最新I81-04B1通常判定：pass_with_held。I81-NR-003 P2 closed、残る必須修正0。** 技術対象fa80b23aa362ed98c4beaf21605eeb5d1b18cbb2。前回f92のfail/発見根拠/過去TDD・manifest不足を消さず、同じ担当のbounded closureとして更新する。全Issue完了・独立final・attestationではない。

先行v3 test source a7eea90ec9cbe20e02c4d13bcf7c9d7b18aa8bbf75a9e0321ae44de4bfcf3ba7は旧verifier58e0beb0...で10件中9成功/Stamp-only拒否1失敗、exit1。正しく/src/Tracker.csを読んだ実readerのContractError not raisedであり、初期fixtureのBlob prefix/先頭空segment不備とは別の有効なRed。修正後の同fixture10/10成功rawを確認。最初のpost-fix assertion typoは別履歴として保持し、期待値緩和でGreenにした扱いにはしない。

現testから4つのmock戻り値と2つのreader戻り値assertionのtuple形だけを旧形へ正規化すると、v3 sourceとbyte一致することを自ら確認した。fixture生成・20byte期待値・拒否assertion・元7ケース意図は不変で、日本語の関数説明も維持されている。

exactfa80 PR run39885155079 attempt1を照合：run全体completed/success、test job120191504297、base88cb20f、checkout merge9dcc20c0beb6991543d242dc777bd732f76a291b。標準776passed/0failed/0skip、3publish/basic contract、PDB10/10、実発行DebugHost112 document/srcと修正後の完全GUID+Stamp照合が成功。rerunなし、watch15503はexit0として保存されている。packet/full-stackはskip、artifact0/標準成功個別TRXなしを保持し、全776個別結果を取得したとは主張しない。

focused trackingはf92→fa80のtasks34/phases19各1本文行、source/行hashと5設定の不変、三検査exit0を保存証拠から確認した。tracking上の未解決/修正予定の文言は通常fix検証結果受領前の状態で、親の閉鎖同期工程を別に残す。全文gateは再実行していない。

本report子5欄へ追記し、親prefix/既存包括report内容・他path不変、末尾1newline、全文readbackを確認した。今回の全command exit0、未回収process/session/保留approvalなし。

## リスク

- 完全IDのP2は現在open。CIのvalid pair成功はStamp不一致拒否を証明しない。修正担当による先行fixture固定と実publish回帰、同じ通常レビュアーのbounded fix検証が必要。
- 04B2/3のplatform/digest採用・外部由来・デバッガー実体/固定供給/条件、05以降のDCP配線、Windows同一revision sourceFileMap/実接続・再作成所有照合・実機は未完了。PDBのpath検査だけでsource内容/実停止位置まで一致したと扱わない。
- 外部8digestは照会時の候補、3GHCRの公開build由来も採用/供給を保証しない。GHCR package API403や本体未取得を保持し、認証変更・再試行・取得/起動へ進んでいない。
- 旧I81-01/02の実装前Red欠落/PD-I81-02-TDDは今回で補填・免除しない。全文文書gate既知不合格と図本人最終確認held、packet/full-stack skipを保持し、今回新全文再検査はしていない。
- 中間Red manifest未保存は回復不能の履歴不足として残る。成功個別TRX/PDB配布物artifact不在、watch最終exit不明は証拠粒度/実行履歴の限界として明記し、成功へ書換えない。
- 技術targetはf92のまま。親が記録/修正をcommit/pushした後のHEADへ現在のCI成功を移転しない。新agent/入れ子Codex/orchestrator、製品修正/新test/build/lint/環境・外部変更/公開操作、独立final予約/freeze/attestationは行っていない。applied:null、report_attestation_allowed:false。

### I81-NR-003 修正検証 (fa80b23 / 2026-10-23)

元findingは完全ID比較と実fixture/実publish回帰によりclosed。旧manifest現物欠落、旧f92 watch最終exit不明、旧I81-01/02実装前Red不足、全文文書gate不合格、図本人確認、packet/full-stack skipを今回で解消しない。04B2/3供給・05以降配線・実コンテナ/デバッガーattach・Windows同revision source/実機は引き続きheld。

親のtracking閉鎖/記録commit/push後のHEADへfa80成功を移転しない。実装report未commit CI追記と新CI束は親所有として保存guardで保護した。新製品/test編集・追加検査実行・外部/環境変更・新agent・独立final予約/freeze/attestationなし。元Sol/high/applied:null、report_attestation_allowed:falseを維持する。

### I81-NR-003 閉鎖記録同期の限定確認（2026-10-23）

親が更新したtasks-status.md:34とphases-status.md:19の各1本文行だけを、技術target fa80b23aa362ed98c4beaf21605eeb5d1b18cbb2との差分から抽出した。NR003 closed、条件付き合格/必須0、先行Stamp負例→修正後10件成功、exactfa80標準776・3実publish、次04B2供給整理という記録は今回のfix検証と整合する。04B2供給や全体完了を成功とした追加記述ではない。

実行前にsource全hash・元行text/hash・入力file hash・2入力/2本文行・21config/checker同一性を固定。既存Windows Node v24.20.0 / cache、同じmirror設定と既存WSL venvでfocused textlint/CSpell/Sudachiを各1回実行し全exit0。CSpell2files/0issues、Sudachi単一--files＋2pathsで違反0。commands/raw/exits/source/config/input hashは /tmp/issue81-nr003-closure-tracking-verification-20261023 に保存した。全文gate/標準test/build/CIは実行していない。

本追記はnormalreport最後の子欄だけで、包括review/f92 fail・fa80 closureの全履歴、親prefix、他pathを保持し、末尾1newlineと全文readbackを確認。専用mirror/junctionを削除、既存cache保持、全process終端・未回収exit/sessionなし。新製品/test/親tracking編集なし。技術判定pass_with_held／NR003 closed／残る必須0を維持し、fa80成功を後続記録commitのHEADへ移転しない。旧TDD・全文gate・図・実機等のheldとreport_attestation_allowed:falseも維持する。
