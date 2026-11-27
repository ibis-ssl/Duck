# Issue81 I81-04A 通常レビュー報告

## タスク

Linux向け配布定義と実際の発行出力検査を通常レビューする。

## 対象範囲

基準4e3099e75c4afab8ba1f11c67500ebe29f17fed3以降の04A差分、3つの既存プロジェクトと配布定義、検査スクリプト、CI、設計・台帳・報告・検証証拠と直接依存。対象HEADは開始前に親が確定する。

## 対象外

04Bの固定外部成果物・vsdbg供給、コンテナ作成起動、DCP配線、実機は未完了であり今回の成功条件に追加しない。製品・試験・期待値の編集、commit/push、新agent、独立最終の予約・freeze・attestation、mergeは禁止。過去TDD不足は保持する。

## Dispatch profile

Parent-owned. selection_source=continuity_reuse; task_kind=normal_review; work_class=judgment_heavy; uncertainty=medium; change_radius=cross_module; criticality=medium; decomposability=independent_workstreams; decomposition_policy=forbidden; decomposition_disposition=prohibited_by_review_lifecycle; parallelism_mode=single_agent; context_need=bounded_history; existing_reviewer=/root/issue81_normal_review; original_actual_requested=gpt-6.1-sol/high; original_fork=none; role_plan=original default role reused; applied=null; application_status=reused_existing_agent_profile; invocation=completed; reviewed_head=5f280e33da9f0e93d7396373c9573b0f21424582; profile_observability=final_profile_hidden; report_persistence_mode=normal_persistence; verification_capability=remote_ci_only; report_attestation_allowed=false.

## 実行コマンド

- mode=normal_review、担当=/root/issue81_normal_review。work-context-manager / review-enforcer / review-worker / report-writer / report-output-manager の実ファイルを読み、built-in code reviewとして要求・設計・全差分・直接依存・検証証拠を1人で確認した。元gpt-6.1-sol/high・fork noneを継続し、applied:null、application_status=reused_existing_agent_profile。新spawn/nested agent/独立finalなし。
- FA780のPowerShell login:false / require_escalated → wsl.exe -d Ubuntu -- Python stdinで読取・保存。開始/保存前HEADは5f280e33da9f0e93d7396373c9573b0f21424582、branch=task/issue81-aspire-desktop-bridge、repo=/home/donabe/Duck-issue81-aspire-desktop-bridge。開始時のnormalreport親Dispatch変更を保持した。
- git rev-parse/status/diff/show/ls-files、行番号付きsource/report読取、JSON/XML/TRX/tar/SHA256照合だけを実施。新test/build/lint/host起動、外部通信、GitHub操作、依存・設定・認証・network変更、commit/pushなし。標準試験はローカル生成済みSslProto不足を補修せずremote_ci_onlyを維持し、保存済みCIを確認した。
- 旧218原本manifest（8a1e01df...）全件、parent-storage-mapの220entry（repo保存211＋省略9原本）と旧03C歴史参照38件をhash照合。省略source archiveは展開せずtar内の対象8fileを現sourceとbyte比較した。fixture/wrapper/options/test csprojは基準版ともbyte-equal、profile3と固定scriptもarchive/候補/対象/disk一致。
- exact5f CI原本の訂正済みmanifest63d5565a...は14entry/実file14、不一致・未登録0。repo収納16file＋親mapも同hash。修正前de3adea...manifestは採用せず、CI rawは不変として区別した。
- 文書追補manifest3492d0a6...の77entryとrepo収納79file＋親mapを照合。initial-56 manifest2762e36d...を保持し、最終preflightの現在source全hash・元行text/hash・2入力fragment・mirror実入力・既存config/checker9件・実system.dic hashを確認。差分の非空本文4行と追加空行1行を区別した。
- reviewer読取wrapper2件はexit1で終端。1件はmirror内にcheckerもあると仮定したFileNotFoundErrorで、実commandが使うrepo checkerを読んでhash一致へ回復。もう1件はphaseの追加空行も本文行数に含めたAssertionErrorで、非空本文4行＋空行1行の実差分へ対応してexit0。製品操作や新lint実行ではない。他のreview commandはexit0。
- 安全なsingle quoted here-string→ASCII Python stdinで本report子5欄だけ保存。親prefix/Dispatch・他pathの内容/HEADをguardし、末尾1newlineと全文readbackを確認した。全commandは同期終端、未回収exit/session/保留approvalなし。

## 対象ファイル

- 技術reviewed HEAD=5f280e33da9f0e93d7396373c9573b0f21424582、baseline=4e3099e75c4afab8ba1f11c67500ebe29f17fed3、Issue81/PR84、PR base=task/aspire-docker-desktop-496。基準以降の全222path（非evidence10＋evidence212）を列挙・分類し、後続の親所有tracking文言/実装report訂正・exactCI/文書追補収納も確認した。
- 3profile: Tracker/Tracker.RuntimeHost、Tracker/Tracker.DebugHost、Testing/Duck.Testing.RefereeDriver.Host の各 Properties/PublishProfiles/LinuxContainer.pubxml。各12行・SHA256 54179c597446fe7382c2c80c38b1c005c71fa8014a12f5d0541bf605081dd1ab。
- scripts/tests/verify-linux-publish-outputs.sh 全142行（mode100755、SHA256 adfe7cd88a943a93bf59afc515b71f178b05d4255761280548d07ecd1cc2704f）、.github/workflows/dotnet-test.yml の全追加部分と既存標準test/失敗診断・後続job条件。
- 主設計追加12行と前後契約、tasks31〜33・phases19、実装report全子欄と親erratum、既存通常report。主設計SHA256 98424b1de762e707f0ea20e9815d29dafc45f11e58f51594229bfae132aa3284。比較設計のTDD要求、既存03C/04B以降の境界を適用した。
- 直接依存: 3hostの既存csproj/Program/設定とDebug静的assets、Tracker.Core/TrackerConnectionLib/SslProtoのproject参照、共有package設定、旧終了処理fixture/wrapper/options/test csproj。既存body/公開API/依存version/標準test期待値に04A差分なし。.gitignoreの*.pubxmlは不変、追加追跡は明示3pathだけ。
- 後続親所有の未commit tasks文言修正は配布対象と04Bの供給契約を変えず、実project/API/デバッグ資材の具体名は設計/実契約を保持する。5fの製品CI成功を、この未commit文書を含む新HEADのCI成功へ移転しない。

## 指摘事項

**現在の必須製品findingなし。検証証拠の補完所見は同review中に解消し、残る必須修正0。**

### 検証証拠所見とclosure

- EV-I81-04A-01 / P2相当・検証証拠：旧evidenceの sudachi-tracking-fragments-final.command.txt:2 は --files tasks --files phases を重ね、checkerのargparse nargs='+'により後指定だけへ置換された。tracking-fragment-inventory.json の「両断片成功」はtasks側未裏付けで、baseline以降の変更本文4行のうち2行も入力範囲外だった。製品/publishの欠陥とは分離する。
- 必要変更は全変更本文4行の正規再抽出、元行/hash/対象数固定、単一--files＋2paths（または個別stdin）で三検査、旧失敗と誤主張の訂正保存。親/実装担当が実施し、reviewerは実行せず証拠を検証した。
- pinned5fの正規初回はtextlint0、CSpell exit1/5token、Sudachi exit1/17finding。親の日本語修正後の中間には「シンボル」「コード」2件を保持。最終はtasks31〜33＋phases19を2fragmentへ元行番号付きで固定し、textlint/CSpell/Sudachi全exit0、CSpell2files/0issues、Sudachi単一--files/2paths/指摘0。config/辞書変更なし。現在sourceとpreflight/実mirrorの一致を確認し、EV-I81-04A-01はclosed。
- 実装report子欄はこの経緯と最終結果へ訂正済み（SHA256 addfa879ace93791fc2cd3887261478c8855d38e70edaa48e822d8290dd7fcec）。親Dispatchのerratum「補完中」は親が判定受領後に同期する管理状態で、旧rawを改稿していない。

### critical production path / composition / focused evidence

| criterion | production path / actual composition | focused evidence / disposition |
| --- | --- | --- |
| 明示配布と通常起動非破壊 | 3既存projectに明示LinuxContainer profileだけ追加。Release/net10.0、SelfContained=false、UseAppHost=false、singlefile/ReadyToRun=false。RID指定なし。旧Program/csprojの既定実行条件は不変 | 同profile3hash・基準差分・実Release publish log。checked_no_finding |
| 実出力契約 | script:10–109が既存projectをdotnet publish --configuration Release --no-restoreで生成。新規mktemp出力の必要fileとnative apphost不在、runtimeconfigの.NET10共有frameworkとincludedFrameworks不在を検査 | RuntimeHostのdll/deps/runtimeconfig/appsettings、DebugHostは同群＋wwwroot/app.css/favicon.png、RefHostはdll/deps/runtimeconfig。固定呼出script:111–140とexactCI3publish成功。checked_no_finding |
| 既存依存/静的資産 | testの既存restore済みgraphを使いReleaseを再build。DebugHostのWeb SDK資産処理と既存設定copy、RefHostのASP.NET共有framework依存を保つ | 3projectと直接依存、現source資産存在、CIの3実project出力。新一覧だけの自己検査ではない。checked_no_finding |
| CI/失敗伝播/所有cleanup | workflow:58–92は標準test成功後に実script、失敗時専用log artifact。scriptはset -euo pipefail、publish/checkの非0を伝播、各段階exit記録、専用mktempをEXIT cleanup | RuntimeHost profile欠落の実構成Red、candidate初回test失敗によるpublish skip、候補再実行/対象CIの実publish。資源起動/外部取得なし。checked_no_finding |
| 期待値/TDD/報告と境界 | 先行固定script→profile追加。出力を検査し、新たな標準xUnit件数は追加しない。後続04Bの固定base/digest/vsdbgとDCP/実機を分離 | script/modeのRed→Green同一性、全manifest/source、修正前後raw/追補4行。checked_no_finding |

- scriptの関数説明と追加コメントは日本語。識別子/コマンド/診断文字列を翻訳対象に広げない。通常起動への暗黙Linux profile適用やRID追加、既存hostのHTTP/UDP/制御契約変更はない。
- 新規runtimeの起動・socket/環境変更・イメージ操作をpublish検査から行わず、共有実行環境を含まないmanaged配布の成立だけを評価した。04B以降の未実装を04Aの必須不足へ追加していない。

## 結果

**I81-04A通常判定: pass_with_held。必須製品finding0、検証証拠所見closed、残る必須修正0。** 技術対象5f280e33da9f0e93d7396373c9573b0f21424582＋親所有の限定tracking文言/報告/証拠補完。独立finalや全Issue完了ではない。

### TDD / 失敗履歴 / exactCI

- 先行aaa903db2b4b6da8645df4c6e321e3f117cbdd6e / CI39865903670は標準776成功後、RuntimeHost profile欠落check exit1、publish not-run。実際に失敗測定したのは最初の構成だけで、DebugHost/RefHostのRedやpublish時実行失敗へ広げない。scriptはその先行版と現在で同一byte/mode。
- 候補fdff6ec8e5851a4a7d1d20d0dad4f69503b926a9 / CI39867225445 attempt1は775passed/1failed/0skip、publish未到達。TRXの唯一失敗はDockerContainerWrapperLifecycleTests.LogFollowerReapTimeoutReturnsFailureAndStillCleansContainer:586、expected1/actual0。後続stop/rm assertionへは未到達。同TRXで旧固定67＋03A race1は個別Passed。
- 失敗経路のfixture/wrapper/options/test csprojは前成功基準・候補CI archive・現在でbyte一致。新profileは明示publish時だけ有効、workflowの新stepはtest後。原因を04A製品変更と示す証拠はなく、終了競合・タイミング依存は可能性に留める。承認された同HEAD failed job再実行1回のattempt2は776成功と3実publish成功。原因解明・既存失敗修復済みとはしない。
- exact5f PR CI39870778199 attempt1はrun全体completed/success、test job120144362822。raw checkoutは5fをbase88cb20fへmergeした32feabd、標準Tracker.Tests無filterで776passed/0failed/0skip、3projectをLinuxContainerで実publishし最終contract passedを確認。候補attempt2の成功を移転した判定ではない。packet-flow/full-stackはskip。
- exact成功artifact0・個別TRXなし。個別776件または生成ファイルbyteを成功artifactから再取得したとはしない。固定scriptの全必須assertを含む実publish段階の成功と標準suite集計を支持する。publish一時出力はscriptの終了cleanup対象で、実行可能性/配布イメージを別途確認した扱いにしない。

### 文書 / completeness / 保存

- 主設計currentとmirrorは36178bytes/SHA98424b1d...一致、baselineとの差分12行/1632bytes/SHA b0f4978b01d42723bf63d94a35f7e1af38d4b267d0f3d4093aee07c69f5766f5。textlint current0、CSpell baseline/currentは同じTailscale1件（284→296）で各exit1、追加12行0。Sudachiの正規stdin captureは同じ既存10件（TCP9/Tailscale1）で各exit1、追加0。10件すべて元行byte/token/kindを照合。Yomiyasu baseline/current/追加行は100/strict0・findings0。
- 最終trackingの非空本文4行（tasks31–33/phases19）は現在source全hashと元行hashが対応。fragment2件SHAはtasks9fe3763d0f7460eaa46bb6eab74e6e655d3b69db6295ef12c449dd98388bf413、phases a6399bcb78087127bb2083d026025046249ef3085101902a045facb9d9be8c76。9config/checkerと実Sudachi system.dic（6c1d5adc...）不変。断片の成功を全文合格へ変換しない。
- 04A新規全文gateは実行していない。既知full24 gateのCSpell232/Sudachi890不合格を保持し、旧03C38logを04A成功へ流用しない。未到達/誤入力の試行、process substitutionの空blob比較、raw未保存の最初のUNC失敗、default-sandbox gh socket失敗は正規成功と分離した。
- 要求/設計/全差分/直接依存、配布契約と通常互換、失敗伝播/cleanup、安全境界、固定期待値/TDD、CI source/証拠粒度、文書/追跡正確性、保守性をchecked_no_finding。現在04Aを阻害するunexplored/capability gapなし。既存終了試験の原因不明と全文gate・実機等はheld。
- normal_persistenceで子5欄のみ保存し、親prefix/他path不変・全文readback・末尾1newlineを確認済み。新test/build/lint/製品変更/公開操作なし。I81-NR-001/002 closedを維持、旧I81-01/02実装前Red欠落/PD-I81-02-TDDを今回で充足・免除しない。

## リスク

- 候補attempt1の既存終了処理失敗は原因未確定。再実行とexact対象成功は成功時の事実であり、初回の失敗やそのassertion到達範囲を消さない。
- 成功時個別TRX/生成配布物artifactは不在。managed publish出力検査までを確認し、実際に共有.NET10環境から3hostを起動した証拠やコンテナ成立ではない。
- 04Bの固定base/外部digest/Debug用資材・vsdbg供給、05以降のDCP配線/localhost公開、実GC/DNS/外部G3、Docker Desktop/Windows実機/実流量、統合受入は未完了。今回の合格条件へ追加せず、未検証として保持する。
- 全文文書gate不合格と図の本人最終視認held。旧38件の歴史的参照、元誤入力/誤比較と正規検証を区別し、旧rawの改稿なし。最初の直接UNC失敗raw欠落は再構成しない。
- Windows Get-CimInstanceによるlint process照会は実装担当の歴史的アクセス拒否/exit1・再試行なし。default権限gh読取はsocket errorでauto-reviewの実行前拒否ではなく、個別exit未観測を保持。今回reviewerの拒否/実行中processとは別。
- 親の限定tracking/実装report/証拠同期は未commit。5f一致CIを将来の公開HEAD成功へ読み替えず、親のcommit/push/公開CI確認は別工程。report親Dispatch状態の最終同期も親が行う。
- 全review process終端・未回収exit/session/保留approvalなし。applied:null、report_attestation_allowed:false。新agent、独立final予約/freeze/attestation、merge/Issue closeを行っていない。
