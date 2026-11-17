# Sub-agent実行レポート

## タスク

- 目的: Issue #128 / I128-UDP-02 について、Windows RuntimeHost の Vision UDP bind/interface join と health 503 判定を確認し、受信 0 件が何を証明するかを切り分ける。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: RuntimeHost listener、NIC 選択、health 判定の複数実装を読み取り専用で追跡する独立した調査であり、Simulator 送信側の実装と混同しないため。

## 対象範囲

- 対象: Duck HEAD 2ff447063ad35f423577bfdefdf600c3010e042f。Tracker/Tracker.RuntimeHost/RuntimeVisionReceiverService.cs、RuntimeVisionReceiverOptions.cs、RuntimeVisionDiagnosticsHttpService.cs、Testing/Duck.Testing.AppHost/Program.cs と必要な直接依存、既存の Issue #128 記録および artifacts/duck-simulation 保存物。
- 確認事項: bind address/port、multicast group join 時の IPv4 interface 候補、host profile が明示 interface を渡すか、health 503 条件、保存済みログに join/interface 結果があるか。変更・起動はしない。

## 対象外

- 対象外: Docker/AppHost 起動、Windows NIC や route 設定変更、Match type 変更、packet capture、image pull/build/tag 操作、Duck ソース編集、テスト実行、PR/Issue 更新。

## Dispatch profile

<!-- This section is parent-owned. The child must not infer or rewrite hidden runtime state or authorization evidence. -->

- selection inputs (parent, pre-dispatch): task_kind=investigation; work_class=judgment_heavy; uncertainty=high; change_radius=cross_system; criticality=ordinary; repetition=single; context_need=bounded_history.
- selection source (parent, pre-dispatch): current CodexSkill skills/sub-agent-task-manager/references/agent-profile-selection.md; the exact RuntimeHost receiver/health scope bounds the investigation.
- observed decomposability (parent, pre-dispatch): independent_workstreams.
- decomposition policy / disposition (parent, pre-dispatch): allowed / split into non-overlapping read-only Simulator and RuntimeHost source investigations to isolate sender and receiver evidence.
- proposed profile (parent, pre-dispatch if applicable): none.
- approval status / evidence (parent): not required; requested effort is high, not xhigh or max.
- requested profile (parent, pre-dispatch): gpt-6.1-sol / high; fork=none. Sol tier resolved to the current runtime model ID because the skill's older gpt-5.6-sol identifier is unavailable.
- agent role / default-role plan (parent, pre-dispatch): runtime default generic sub-agent; the collaboration spawn interface has no explicit agent_type parameter.
- role config evidence / profile effect (parent, pre-dispatch): no role-specific configuration was found in local Codex settings; requested profile aligns with the current account default gpt-6.1-sol / high.
- planned runtime profile after known role constraints (parent, pre-dispatch): gpt-6.1-sol / high.
- applied profile (parent, post-runtime exact evidence only; null when unverified): pending.
- application status (parent, post-runtime evidence only): pending.
- runtime profile observability (parent, post-runtime): pending.
- reviewer continuity (parent, if applicable): not applicable.
- fork policy (parent): none (fresh task with explicit profile; bounded task context supplied directly).
- reasons / constraints (parent): no image or network mutation; treat 503 and received=0 as separate observations; do not infer a missing listener from 503 alone.

## 実行コマンド

- 実行コマンド: FA780 / PowerShell、作業場所 `C:\Users\donabe\Documents\Codex\2026-11-15\task-9\Duck-PR136-FA780`。指定の `C:\Users\donabe\Documents\Codex\CodexSkill\skills\work-context-manager\SKILL.md` と `skills\report-writer\SKILL.md` を実ファイルから読了し、対象同一性、証拠と推論、未実施を区別した。development-orchestratorや親担当workflowは実行していない。
- `git status --short` / `git branch --show-current` / `git symbolic-ref -q HEAD` / `git rev-parse HEAD` / `git remote get-url origin` / `git log -1 --format='%H %s'` / `git diff --name-only HEAD`: origin `https://github.com/ibis-ssl/Duck.git`、HEAD `2ff447063ad35f423577bfdefdf600c3010e042f`、detached checkout。追跡済み差分なし、未追跡は親が用意した本報告とSimulator担当報告。branch/baseはcheckoutから未解決、対象PR #136 / Issue #128は親提供情報。
- `Get-Content -Encoding UTF8` と行番号付与で下記ファイルを読了。`Test-Path` / `rg --files -g AGENTS.md` による対象・祖先・関連ディレクトリ探索では適用可能なAGENTS.mdは見つからなかった。`rg --files --hidden --no-ignore artifacts/duck-simulation` で保存物を再確認した。通常の `rg --files` はignore対象AppHostログを表示しないため、初回の2ファイルのみという観測を3ファイルへ訂正した。
- `rg -n` でIssue #128報告と保存ログのlistener/join、503、interface、host-runtime記録を探索した。Windowsでpath引数のglob指定が解決されない初回エラーは `reports -g 'issue-128-*.md'` に訂正。日本語consoleの初回文字化けは `[Console]::OutputEncoding=[System.Text.Encoding]::UTF8` で再読した（プロセス出力形式のみ）。過去Issueログの大量出力は今回の実測根拠として採用していない。
- `git hash-object` の確認順のblob: ReceiverService `81f46b2aa08c67a3f6e46752bc9efbc60ff1238e`、ReceiverOptions `1cb5d823858891e40ac5a141abc2f72823f22299`、DiagnosticsHttpService `bea1d968fba7ed82aedea74c2866db26ab916f9b`、AppHost Program `a6065314055ea2b47daa7008fb0e16044ac743cc`、RuntimeHostServiceCollectionExtensions `518bebd036b8c2c6fe85a5504c03e03c50a3ed12`、ServiceReceiveDiagnostics `82e5f4bcb25678044f55bbe9715e81835c13e74b`、ResourceReadinessState `80768635f079005097a8114a449f54ee7ee3f98b`。
- 初回のdefault実行で本報告WriteAllTextはAccess deniedとなり、保存されなかった。初回スクリプトの無条件success表示は成功証拠ではない。親から通常ユーザー経路の追加許可を受領後、以降は `exec_command(sandbox_permissions=require_escalated)` のみ使用し `whoami` が `fa780\donabe`、HEAD一致を確認。NIC候補順、wrapper加算、health分岐を同経路で再読し、本報告の子担当欄のみ保存する。
- usable local read executorあり。ただし本調査はsource/records照合のみで、build/test、Docker/AppHost起動、packet capture、NIC/route操作、PR/Issue更新は未実施。commit/push/CI waitは対象外。既往CI成功の再確認はしていない。

## 対象ファイル

- 変更または確認したファイル: 変更は本報告の子担当欄のみ。製品ソースを変更していない。
- `Tracker/Tracker.RuntimeHost/RuntimeVisionReceiverService.cs:42–115,119–138,142–209,211–325,340–409`、`RuntimeVisionReceiverOptions.cs:11–34`、`RuntimeVisionDiagnosticsHttpService.cs:17–38,41–73,77–82`、`Testing/Duck.Testing.AppHost/Program.cs:688–715,788–857`。
- 直接依存: `Tracker/Tracker.RuntimeHost/Program.cs:26–35`、`RuntimeHostServiceCollectionExtensions.cs:32–38,73–85`、`RuntimeIdentityControlProtocol.cs:292–313`、`appsettings.json:2–6,145–149`、`Testing/Duck.Testing.Diagnostics/ServiceReceiveDiagnostics.cs:22,24–30,33–67,111–200,225–234`、`ResourceReadinessState.cs:88–180`、`ResourceDiagnosticIdentity.cs:10–27`。
- 既存報告: `reports/issue-128-host-runtimehost-implementation-20261115022628.md:95–118`、`reports/issue-128-cm4-namespace-implementation-20261115.md:3–13,155–168,191–211`。旧relay構成と旧HEADの結果を今回HEADの受信証拠に流用しない。
- 保存物: `artifacts/duck-simulation/local-20261115T203949Z-c2e44f5a0c2d487d84e22f8c6758e594/startup-mode-20261115T203949047Z-c8f307d20e5f4922ba3e9e6e60102bee.txt:1–6`、同所 `crane-fixture-20261115T203949047Z-c8f307d20e5f4922ba3e9e6e60102bee.json:1`、同所 `apphost-20261115T203949047Z-c8f307d20e5f4922ba3e9e6e60102bee.stdout-stderr.log:1–30`。AppHostログ内のlogin tokenは転記しない。

## 指摘事項

- 指摘要約: profile上の受信先は `0.0.0.0:10020/UDP`、multicast groupは `224.5.23.2`。Optionsクラス既定portは10006（RuntimeVisionReceiverOptions:19–34）だがjson:2–6とAppHost Program:820–831が10020を指定。IPv4 UdpClient、ExclusiveAddressUse=false、ReuseAddress=trueでbind後にgroup参加（ReceiverService:218–234）。これは構成と実装の証拠であり、今回の実bind成功の証明ではない。
- NIC候補順: UpかつSupportsMulticastまたはLoopbackのNICからunicast addressを列挙（ReceiverService:316–325）。明示InterfaceAddressがあればIPv4を検証して単一候補（297–306）。未指定時はIPv4かつ0.0.0.0以外、Distinct、非loopbackを先・loopbackを後に並べる（309–313）。非loopback間のroute metricやDocker優先による並替えはなく、列挙由来の順である。候補すべてへ順にJoinMulticastGroupを試み、個別SocketExceptionを記録して継続し、候補0件または全候補失敗は例外（245–287）。
- host NIC固定: Program:800–835はVisionReceiver__InterfaceAddressを注入せず、json:5はnull。Testing:Duck:VisionInterfaceAddressの読取りは703、その転送はcontainer分岐854–856だけで、host分岐は835で先にreturnする。したがってこのAppHost設定によるhost NIC固定はない。実機継承のVisionReceiver__InterfaceAddress等の上書きは未採取。
- 件数: ReceiveAsync後にBeginReceive（ReceiverService:90–99）、ParseFrom成功後にwrapper累計を加算し、Detection付きだけvision成功件数を加算（159–167,403–409）。VisionPacketsReceivedTotalログはDetection成功累計（358）、DecodedWrapperPacketsReceivedTotalはgeometry-onlyも含むwrapper復号成功累計（359,405）。host healthのtop-level VisionPacketsReceivedは後者、nested Diagnostics.SuccessfullyDecodedCountは前者（DiagnosticsHttpService:30–38）。生UDP到着総数は公開されていない。protobuf failureはErrorCountへ記録（ServiceReceiveDiagnostics:152–167）。
- 503: host profileでsnapshot.IsReady=falseなら503。HTTP bindは127.0.0.1:39108（DiagnosticsHttpService:17–38、AppHost:811–831）。成功Detection、5秒以内の既知で有効な受信age、identityに対応するreadinessの件数進捗が必要（ServiceReceiveDiagnostics:22,175–200、ResourceReadinessState:111–180）。初回1件だけはAwaitingProgress、期限切れやdecode失敗も503となり得る。503だけでbind失敗やDocker経路断と断定できない。
- 独立した503候補: host executable登録（Program:812–835）はTesting:DiagnosticsのRunId/ResourceId/ContainerId/GenerationもIdentityControl有効化も注入しない。DIはその4値をParseIdentityへ渡し（RuntimeHostServiceCollectionExtensions:73–77）、不足時はnull（ServiceReceiveDiagnostics:24–30）。Identity=nullならreadinessを作らず、成功DetectionがあってもIsReady=false/Unattributed（182–200）。IdentityControlも未有効なら早期return（RuntimeIdentityControlProtocol:296–309）。外部identity注入がない標準構成では受信回復後も503が残る。実機health bodyのIdentity/Reason未保存のため今回発現したかは未確定。
- identityとcounterの厳密な分離: identityが欠落しただけではwrapper累計を抑止しない（ReceiverService:405は無条件加算）。current epochのDetection成功件数もidentity有無を条件にせず加算する（ServiceReceiveDiagnostics:135–145、TryInput:225–234はepoch一致を確認）。よってidentity欠落は今回の復号0件の説明にはならない。一方、明示的なidentity置換はshared Detection件数/ErrorCountをリセットしepochを更新して旧epoch試行を拒否する（33–67,225–234）。wrapper lifetime累計はReplaceIdentityでリセットされない（ReceiverService:368–375）。「identityは常に件数と無関係」と広く一般化してはいけない。
- 保存診断: 実装はbind/join後にendpoint、成功/失敗interface、countをログ出力し4秒ごとに再出力（ReceiverService:17,67–82,119–138,172–209）、初期化失敗は61に記録。保存txtはrun/stack id・時刻・testing_mode=base-host-runtime・game_controller_ui=manual、jsonはstack/team/colorのみ。AppHostログ:1–30はAppHost/DCP/dashboard起動記録でRuntimeHost receiver/joinやhealth bodyを含まない。読んだIssue #128報告にも今回HEADのjoin実測はない。したがって保存物から今回joinしたNICは確定できない。

## 結果

- 結果: 親提供の「RuntimeHost起動、health/vision=503、受信・復号0、CraneのTracker受信なし」は、新鮮な正常Detection受信が確認されなかったことを示す。host healthのwrapper countも0と確認された観測なら、そのプロセスの正常wrapper復号累計が0だったといえる。受信0という呼称だけでは観測fieldを特定できない。
- wrapper count=0なら正常なgeometry-only復号もなかったといえるが、datagram自体が未到着とはいえない。nested Detection countだけ0ならgeometry-onlyを除外できない。raw未到着とdecode失敗の区別にはErrorCount、field名付きhealth body、receiver/joinログの対応が必要。
- Simulator未送信、送信開始条件、Windows/Docker境界、NIC/membership等のどこが原因かは本範囲で確定しない。静的コードだけでDocker経路断とは判断しない。identity欠落による503候補は復号0件と独立して扱う。試合開始の前提も受信側実装からは判断できない。
- 製品修正、build/test、Docker/AppHost実行、packet captureは未実施。既存image/tag、Duck source mount、設定、依存、Match type、PR/Issueを変更していない。merge権限なし。

## リスク

- 未解決のリスクまたは後続対応: 実bind成功、列挙候補/成功join/失敗joinの実アドレス、継承設定、health JSONのIdentity/Reason/SuccessfullyDecodedCount/ErrorCount/top-level VisionPacketsReceived、Simulatorの送信先・開始条件・実datagram有無は未確認。送信側担当の証拠と突き合わせ、親が許可範囲で必要最小限の実機確認を判断する。
- 最小の追加証拠は同じrunのRuntimeHost開始・4秒診断ログとhealth body。NIC固定の修正前に実joinを把握する必要がある。identityを満たすため架空container IDを作る提案・操作はしていない。Match typeは人間の確認事項として維持する。
