# Sub-agent実行レポート

## タスク

Issue81 / PR84 の I81-03A 通常レビュー。単一常駐受信・自身の診断・購読分離・epoch/鮮度・UDP設定と所有Disposeを、設計・固定テスト・実測証拠と照合する。本書は通常レビューの記録先として準備した。

## sub-agentを使う理由

実装担当と異なる既存通常レビュアーを継続利用し、親は統合と台帳更新を担当する。重複レビュアーは起動しない。

## 対象範囲

repo=/home/donabe/Duck-issue81-aspire-desktop-bridge、branch=task/issue81-aspire-desktop-bridge、Issue81/PR84。I81-03A開始基準234067d3cc3027a20f3794a7ce8fea75f26220caからの該当製品/test/report/tracking/evidenceと直接依存。review-target HEADは開始時に親が別途通知する。コード候補381dcb6のCI39812786126は651件成功。今回の記録保存commitをレビュー対象として開始時に親が通知する。技術判定は担当が記入する。

## 対象外

03B/C以後の未実装機能を03Aの合格条件へ加えない。外部repo/配置/依存/環境変更、実機受入、コード修正、commit/push、Issue close/merge、独立最終レビューの代替、独立freeze/reservation/attestationは禁止。旧TDD欠落を今回Redで補填した扱いにしない。

## Dispatch profile

Parent-owned. task_kind=normal_review; work_class=judgment_heavy; uncertainty=medium; change_radius=cross_module; criticality=high; repetition=single; decomposability=independent_workstreams; decomposition_policy=forbidden; decomposition_disposition=prohibited_by_review_lifecycle; parallelism_mode=single_agent; context_need=bounded_history. existing_reviewer=/root/issue81_normal_review; original_actual_requested=gpt-6.1-sol/high; original_fork=none; role_plan=original default role reused; applied=null; application_status=reused_existing_agent_profile; profile_observability=final_profile_hidden; report_persistence_mode=normal_persistence. verification_capability=remote_ci_only for real parser/standard suite. parent supplies exact target HEAD/current CI and report evidence before review starts. report_attestation_allowed=false.

## 実行コマンド

- mode=normal_review、継続担当=/root/issue81_normal_review。既読の work-context-manager / review-enforcer / review-worker / report-writer / report-output-manager の実ファイルSkillを継続適用した。built-in code reviewとして1名で要求・差分・直接依存・固定テスト・実行証拠を確認。新spawn/nested agent/orchestrator再入場なし。
- 実行環境: FA780、Windows PowerShell login:false、既存 require_escalated 経路の wsl.exe -d Ubuntu -- Python stdin。repo=/home/donabe/Duck-issue81-aspire-desktop-bridge、branch=task/issue81-aspire-desktop-bridge。開始HEAD=95ce7801224f6d2a77fc2ab6487675fe44fcb433、開始時clean、所有=current task。write許可は本report5子欄だけ。
- git rev-parse / status / diff / grep、行番号付きsource/report読取、Python JSON/XML/SHA256/tar readは通常exit0。対象範囲の git diff --check 234067d3cc3027a20f3794a7ce8fea75f26220ca 95ce7801224f6d2a77fc2ab6487675fe44fcb433 -- Testing Tracker reports/issue81-referee-driver-scope-20261022.md reports/issue81-referee-receive-implementation-20261022.md reports/issue81-referee-receive-normal-review-20261022.md はexit0。raw証拠全体の空白を製品差分checkと混同していない。
- 検査script2件は読取形式の誤仮定でexit1。1件はtar suffix照合が過去evidence内copyまで3件選びAssertionError、もう1件は初回testの直置きcopyを想定してFileNotFoundError。前者は正確なrepo相対member、後者は先行source-manifestに切り替えて再照合exit0。製品/testのfailureや権限拒否ではなく、元ファイル変更なし。
- 1296件の証拠manifest entryをhash照合、branch外保持の22 raw zip/binlog/archiveも元hash照合し未解決0。source archiveの正確な119 memberを照合。8組のRed→Green間で Tracker/Tracker.Tests の git diff --exit-code は全exit0。先行test全11ファイル、旧blind v1全11/v2全9 entryも固定hash一致。
- exact target CIの/tmp証拠をread-only確認。inventory 29 entry、source23ファイルのdisk/95ce780/381dcb6一致、raw metadata/log/commands/終了値を確認した。reviewerから新build/test/lint/host/UDP受信/CI待機/再実行/GitHub操作は実行していない。
- 本findingは静的な通常競合順序から導いたもので、reviewerが新たな再現testを実行して得たRedではない。全reviewer commandは同期終端、exit回収済み。実行中process/未回収session/保留approval/拒否はない。

## 対象ファイル

- reviewed implementation HEAD=95ce7801224f6d2a77fc2ab6487675fe44fcb433、baseline=234067d3cc3027a20f3794a7ce8fea75f26220ca、Issue81/PR84、branch=task/issue81-aspire-desktop-bridge。commit/push済みの固定対象をレビューし、途中の製品/test/HEAD変更はない。
- 全差分1370 pathを列挙・分類した。非evidenceは21 path（製品/project5、追加test11、tasks/phases2、scope/実装/通常report3）、evidenceは38 group/1349 path。重複source snapshotは現在製品と各履歴commit/manifestの対応を確認し、raw log/TRX/manifest/実引数/終了値を時系列で評価した。
- 製品/project全行: Testing/Duck.Testing.Diagnostics/ServiceReceiveDiagnostics.cs、Testing/Duck.Testing.RefereeDriver/{Duck.Testing.RefereeDriver.csproj,RefereeReceiveService.cs,RefereeUdpReceiverFactory.cs,UdpRefereeDatagramReceiver.cs}。
- 追加test全行: RefereeReceiveDiagnosticsTests / FailureTests / CancellationTests / SingleRunTests / RestartTests / ReadinessBoundaryTests / SubscriberTests / SubscriberRegressionTests / UdpLifecycleTests / UdpSettingsAndDisposalTests / RefereeUdpFactoryBoundaryTests。11ファイル、23 methods/36 cases。初回保存の期待値/source hashから不変。固定独立仕様67件も不変。
- 直接依存: 既存RefereeDriver.cs / RefereeDriverNetworkAdapters.csと旧public default/constructor/JSON、RefereeDriverFixtureTests / NetworkAdapterTests、共通診断のidentity/readiness/clock/epochと以前の固定契約、実parserのHasCommand/HasCommandCounter/enum境界、標準Tracker.Tests組成/CI。新内部型はInternalsVisibleTo経由の試験対象で、03B/CのProgram/HTTP/DI公開入口は今回まだない。
- 要求根拠: reports/issue81-referee-driver-scope-20261022.md の親採用03Aと末尾訂正、tasks-status I81-03A、主設計の自身診断/有限freshness、比較設計260のTDD。03B API照合、03C HTTP、後続Desktop/DCPは合格条件へ追加していない。
- exactCI証拠=/tmp/issue81-referee-receive-review-target-ci-20261022/。ci-evidence.json SHA256 07d538e172b590421335596837ecdf4bb268364c47722d230152e0d001b3b159、inventory.json SHA256 6d662deb5bee0cd524da5ebcb752212e3fdc2a37a1c052ada35e381f6c02d215。成功時artifact0のため、当該runの個別TRX/source archiveなし。
- 親prefix/Dispatchの保存前SHA256=b314b68ff41769b01213d27bb89518612fbaeea996b7fe4f56ed212cfae98788。本report5子欄だけをnormal_persistenceで記入し、他report・tracking・製品・testは編集しない。

## 指摘事項

### I81-NR-002 — medium（P2）／通常pathの修正必須

**位置:** Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs:120–125。関連箇所は88–94（診断受理後にcommand公開）、137–145（別lockでcache置換）。

**欠陥:** cacheの同source/同epochだけを照合し、そのcache自身ではなく「現在の最新診断packet」のDecodeAgeUpperBoundを使って返している。同endpointの次packetのRecordSuccessとPublishCommandは原子的でないため、期限切れcacheを次packetの新鮮度で復活させられる。

**成立する通常の競合順序:**

1. t0に正常command A（counter7）を受け、latestCommandへ保持する。
2. t6などFreshnessWindow超過後、WaitForChangeAsync(null)がcommandGateを取得し、GetSnapshotsを呼ぶ前に切り替わる。
3. pumpが同endpoint/同epochの正常command B（counter8）をRecordSuccessする。診断のstamp/statusはBへ更新される。続くPublishCommandは購読のcommandGateを待つ。
4. 購読はcache AのSourceKey/ReceiverEpochと、BのSucceeded/age0を照合してAを返す。A自身は既に期限切れで、現在のsource/epoch一致は同じpacketの証拠ではない。

**影響・根拠:** 古い指令を新しい観測としてIRefereeCommandSourceへ返し、呼出者が旧HALT/STOP等を現在の指令として制御判断に使い得る。03Aの有限freshnessと旧cache非延命に反する。既存 RefereeReceiveSubscriberRegressionTests.cs:48–74 はpollだけの非延命を確認するが、別packetの診断受理とcache公開の間を制御していない。multi-subscriberの公平性等を新仕様へ加えた指摘ではなく、通常の1 subscriber＋1 pumpの競合である。

**必要変更:** cacheとその受理済みpacketのstamp/sequence等を結び、別packetの診断ageを旧cacheへ流用しない。診断とcacheの原子的公開、または同packetの照合・cache固有の単調age/clock世代など、契約を満たす方法を実装担当が選ぶ。単一pump、同counter全packet計数、購読だけdedup、失敗/epoch/取消/再開時cache無効化、既存public defaultを維持する。

**必要な組成fixtureとfocused証拠:** productionのRefereeReceiveService実体＋実parser/診断＋手動時計を使う固定競合fixture。Aを期限切れにし、同endpointのBを診断受理済み・cache公開前の境界で制御して、Aを返さないこと、公開後はBを返すことを示す。必要なら内部の制御seamを設けるが、製品処理をtestへ複製しない。元実装での実行時assertion Red→同じ固定testのGreenとsource fingerprint、既存36件/固定67件/標準gateの影響を保存する。新testを通すための待ち時間緩和/skip化はしない。

| required action | production path | actual composition fixture | focused evidence／現在状態 |
| --- | --- | --- | --- |
| cacheと同packetの鮮度を結び付ける | RefereeReceiveService.RecordSuccess→PublishCommand→WaitForChangeAsync | 実service/実parser/手動時計でA期限切れ＋B受理/公開の競合を制御 | 未修正。静的interleavingを本reviewで確認 |
| 期限切れA非返却とB公開後返却を固定 | 同serviceの既存公開IRefereeCommandSource経路 | 上記fixture、正常packet件数とcounter対応も確認 | 先行runtime Red/同一test Greenは未取得 |
| 既存単一pump/epoch/取消/Dispose/defaultを保持 | 同service、共通診断、UDP adapter/factory、旧public経路 | 新競合fixture＋既存03A36/固定67/本番socket組成 | current651集計成功はあるが、この競合の修正証拠ではない |

### その他確認結果

- 単一Run admission、Run公開後の開始、caller/内部stop連携、EndEpoch参照一致、取消/想定外faultのfinally解除、並行反復Disposeの共有Task、owned/borrowed socketの区別と設定途中cleanupは、読取範囲で追加findingなし。
- 正常decodeは同counterでも計数し、破損/未知/command・counter欠落を失敗としてpump継続。受信直後・parse前のstampを使い、owner欠落/未知age/期限切れをreadiness成功にしない。endpointはproducer観測で、自己owner認証やGC自身G3へ流用していない。
- 新関数説明/XMLコメントは日本語。legacy public receiver/control/client/default/既存JSONと旧AppHostは未変更。新NuGet/version/外部wire/HTTP/DCP契約を推測していない。
- 文書gate不合格は別の既存制約。表示上限由来の旧行誤判定は訂正済みで、今回のP2と混ぜない。03B/C・実機等はscope外held。

## 結果

**通常技術判定: fail。I81-NR-002 medium(P2)が1件open。** reviewed implementation HEAD=95ce7801224f6d2a77fc2ab6487675fe44fcb433。全差分/直接依存/証拠の確認を終え、実装担当へ上記required actionを返す。独立finalではない。

### TDD・固定証拠の評価

| 単位 | 実runtime Red CI／失敗 | 固定testを維持したGreen CI |
| --- | --- | --- |
| 正常同counter計数 | 39784129851：Expected2/Actual0 | 39785567950：616/616 |
| 不正入力4case | 39787367706：pump停止または成功件数2→3 | 39788803859：620/620 |
| 取消後not-ready | 39789901203：ready True残留 | 39791056335：621/621 |
| 単一Run | 39792254453：最大同時Receive Expected1/Actual2 | 39793648955：622/622 |
| 購読dedup | 39799276013：同counterでsubscription完了 | 39802072353：626/626 |
| 実UDP/所有Dispose | 39804723964：payload[]、DisposeCalls0 | 39806540458：633/633 |
| production設定順 | 39808111037：設定5操作に対してActual[] | 39809637502：640/640 |
| 不成立group/IF拒否 | 39811429720：unicast/IPv6 IFに例外なし | 39812786126：651/651 |

- 各Redの実TRXはcompile成功後のassertion失敗で、timeout/型解決をRedにしていない。Red→Greenの8組ともTracker.Tests差分0、先行test11ファイルのhashは現在まで一致。回帰として最初から通ったrestart/owner/freshness/購読/cleanup等へ人工Redを追加していない。
- production socket設定そのものを注入した RefereeUdpFactoryBoundaryTests.RealProductionSetupUsesDynamicPortAndFeedsSingleServicePump は、境界Red runのTRXでPassed（約0.069秒）。Linux上のIPv4/Any/dynamic port/reuse/loopback IF membership設定、unicast loopback受信、実parser、service診断/購読、所有Disposeを組み合わせた証拠。Desktop multicast配送の実証ではない。
- 最終旧コードHEAD381dcb6のCI39812786126と、今回exact95ce780のCI39817012736を分離した。exact runはpull_request/completed/success、test job119958024007、651passed/0failed/0testskipped。raw logのcheckoutは95ce780を含むPR merge427b7af（base88cb20f）である。branch headとPR merge refを混同しない。
- exactCIのsource23は381dcb6/disk/95ce780のbyteが一致。標準targetは無filterで36追加分を含む構成と整合するが、当該成功runはartifact0/個別TRX・個別成功名なし。36件/固定67件の当該run個別Passed行を直接確認したとは主張しない。過去RedのTRX上の個別成功を今回へ付け替えない。packet/fullstack jobsはskip。
- 旧run39795794024のjob625成功と、当時のrun未終端・local monitor終了255は履歴保持。後日のGETでcompleted/successとなったことも別記録であり、watch255を0へ書き換えたり重複watchを起動したりしていない。reviewerにも未回収processなし。

### 文書gate・coverage・終端

- 保存済み最終full gateは24 Markdown＋説明359入力、textlint exit0、CSpell exit1/232、Sudachi exit1/890で総合不合格。source/current/mirror/config/dictionary hash一致を確認した。新lint実行/辞書変更なし。
- CSpellは1文書100件と重複上限で旧行の出力位置が変わり得る。tasks旧25/26行のbyte同一を優先し、旧I81-02/I81-Vの翻訳提案撤回を正当と確認。親変更行のみ2断片は同じpath/configでCSpell exit0/0指摘、Sudachi新規正規化語0。これは全文gate合格ではない。
- coverage disposition: 有限freshnessと購読cache同packet対応=checked_finding（I81-NR-002）。単一pump/計数/parse失敗/owner/epoch/取消/再起動/UDP設定/所有cleanup/互換、日本語説明、TDDとfixed source、CI/report/trackingの証拠区別、scope/安全/保守性=checked_no_finding（上記P2以外）。成功CI個別結果、文書full gate不合格、Desktop/Windows/DCP/外部自身診断=held。03B/API照合・03C/HTTP・後続配置の実装完成はnot_applicable。今回必要範囲の未探索を理由に結論を省略していない。
- 旧I81-NR-001 closedを維持。旧I81-01/02実装前Red欠落は今回TDDで解消/免除されたとはしないが、今回の修正を進める再承認待ち条件にしない。
- original_actual_requested=gpt-6.1-sol/high、original fork none、applied:null、application_status=reused_existing_agent_profile、final profile非公開。report_attestation_allowed:false、独立final予約/freeze/attestationなし。
- 本report5子欄だけ保存、親prefix/Dispatch保持、製品/test/他report/trackingのhash guard不変を確認し、全文readbackして返す。reviewerはcommit/push/merge/closeなし。全command終了コード回収済み、実行中process/未回収session/保留approvalなし。

## リスク

- I81-NR-002が未修正であり、現HEADの通常技術reviewは合格ではない。次は同じ実装担当が固定の競合testでruntime Redを取得し、修正と同一test Green、production path/actual composition/focused証拠のcompleteness matrixを満たして同じ通常reviewerへ戻す。
- current651成功は本findingを否定しない。既存testは期限切れcacheへ別packetの新鮮度が流用される競合を制御しておらず、CI集計だけでは対応を証明できない。
- 文書full gateは不合格のまま。断片成功を全文合格へ変換せず、旧行翻訳・語彙追加・上限変更による見かけ上の合格を行っていない。図の本人最終確認も未了。
- 実Desktop multicast/Windows/DCP/実機は未検証。03Aの隔離UDP組成成功を、固定11003への現実配置、外部資源自己復号、完全所有ID取得、GC producer認証、03B API一致や03C HTTPへ拡大しない。
- 外部3repo改変必須という以前の前提は撤回された。既存送信先設定・namespace共有等は未採用候補で、port競合、再作成時の追従、DCP NetworksとRunArgsの矛盾等は未解決。外部ソース変更ゼロ候補を適用/成功したとは扱わず、03Aの追加合格条件にも加えない。
- exactCI証拠は/tmpにidentity/hash付きで保存されている。今後のrepository保存は親所有であり、報告/追跡変更と製品/test修正を区別して次のreview targetを固定する。今回のnormal reportは独立finalやIssue全体完了の代替ではない。
