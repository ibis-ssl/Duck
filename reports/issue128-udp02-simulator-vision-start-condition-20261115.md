# Sub-agent実行レポート

## タスク

- 目的: Issue #128 / I128-UDP-02 について、固定 Simulator が Vision を送る条件と現在の起動設定上の送信先をソースから確認し、試合開始前に送信されるかを判定する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: pinned framework の C++ 送信経路と Duck AppHost の起動引数を独立して追跡する読み取り専用調査であり、実機試験や設定変更を伴わずに前提を確認できるため。

## 対象範囲

- 対象: Duck HEAD 2ff447063ad35f423577bfdefdf600c3010e042f。Testing/Duck.Testing.AppHost/ComposeSimulationStack.cs、関連する Simulator 引数設定、pinned framework commit a52b6bdecd97a277544dd6f275afbf4483781e12 の src/simulator/simulator.cpp、既存の Issue #128 記録と artifacts/duck-simulation の保存物。
- この作業は、Vision 出力先、送信処理が呼ばれる条件、Match start 依存の有無を静的証拠で答える。コード・image・設定を変更しない。

## 対象外

- 対象外: Docker/AppHost の起動、Match type や試合状態の変更、ネットワーク変更、packet capture、image pull/build/tag 操作、Duck ソースの編集、テスト実行、PR/Issue 更新。

## Dispatch profile

<!-- This section is parent-owned. The child must not infer or rewrite hidden runtime state or authorization evidence. -->

- selection inputs (parent, pre-dispatch): task_kind=investigation; work_class=judgment_heavy; uncertainty=high; change_radius=cross_system; criticality=ordinary; repetition=single; context_need=bounded_history.
- selection source (parent, pre-dispatch): current CodexSkill skills/sub-agent-task-manager/references/agent-profile-selection.md; the exact Simulator source/argument scope bounds the investigation.
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
- reasons / constraints (parent): no image or network mutation; do not infer runtime send from the absence or presence of application logs alone.

## 実行コマンド

- 実行コマンド:

  - 指定の `C:\Users\donabe\Documents\Codex\CodexSkill\skills\work-context-manager\SKILL.md` と `report-writer\SKILL.md` を実読。親所有 workflow・MCP・子担当追加・テスト・build・Docker/AppHost 起動・packet capture は実施していない。
  - 初回は default shell で source 読取・報告保存を行い、`gh api` は socket access permissions エラーで exit 1 になった。その後、親から当該 shell が禁止 ID `fa780\codexsandboxoffline` だったとの通知を受け、直ちに停止した。初回作業を通常ユーザー実施済みとは扱わない。
  - 親から新しい本人許可と通常ユーザー実行方法が共有された後、この同一調査を再開。再開後の全 worktree/GitHub コマンドと本報告保存は `functions.exec → exec_command(sandbox_permissions=require_escalated)` を用いた。`whoami` の実出力は `fa780\donabe`。GitHub source 読取も同じ通常ユーザーで成功した。別 ID・MCP・別接続への切替はない。
  - 再開後の場所は FA780 / PowerShell / `C:\Users\donabe\Documents\Codex\2026-11-15\task-9\Duck-PR136-FA780`。`git rev-parse HEAD` → `2ff447063ad35f423577bfdefdf600c3010e042f`。初回の branch 読取は detached HEAD、origin は `https://github.com/ibis-ssl/Duck.git`。再開後の `git status --short` は親予約の送受信側報告2件だけ untracked。
  - 通常ユーザーで `gh api 'repos/ibis-ssl/framework/contents/<path>?ref=a52b6bdecd97a277544dd6f275afbf4483781e12'` を使用。JSON `content` を Base64 decode し、行番号付き本文を読取。対象 path は下記。`gh api 'repos/ibis-ssl/framework/git/trees/a52b6bdecd97a277544dd6f275afbf4483781e12?recursive=1'` で直接依存の実パスを解決。再開後の source 取得はすべて exit 0。ソースを container に置かず、ローカル製品ソースへコピーしていない。
  - 通常ユーザーで `rg -n` により現行 Simulator registration を再確認。`git diff --quiet HEAD -- Testing/Duck.Testing.AppHost/ComposeSimulationStack.cs Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs Testing/Duck.Testing.AppHost/Program.cs scripts/start-duck-simulation.cs Testing/Duck.Testing.AppHost/linux-image-lock.json` → exit 0。調査対象 Duck source は指定 HEAD と一致する。
  - 通常ユーザーで `Get-ChildItem -File` と `Get-Content` により当該 run の3保存物を再確認。AppHost log の認証 URL は報告へ転記しない。検証は静的調査のみ。PowerShell executor は使用可能だがテスト実行能力や依存関係は今回未検査のため `verification_capability=unknown`。commit/push/CI wait は本担当では not_required。
  - 本報告保存時は親所有 prefix（Dispatch profile を含む）を読み取り、その文字列をそのまま保持して子所有5節だけ更新。保存後に prefix 一致を検査した。

## 対象ファイル

- 変更または確認したファイル:

  - Duck `Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs:51–66,120–132,396–401`：Compose command 登録と CM4 起動依存。`ComposeSimulationStack.cs:62,86–138,192–196,210–212`：配列の保持、YAML command、CM4 shared namespace。`Program.cs:688–732,757–785`：base/host profile の選択と既定引数。`scripts/start-duck-simulation.cs` と `linux-image-lock.json`：起動方式と pinned sourceCommit。
  - framework commit `a52b6bdecd97a277544dd6f275afbf4483781e12` の `src/simulator/simulator.cpp:578–655,1214–1245,1264–1307,1316–1399`：CLI、初期 Command、SimProxy 接続、IBIS/GC 分岐。
  - 同 commit `src/amun/simulator/include/simulator/simulator.h:69,76–100`、`src/amun/simulator/simulator.cpp:117–181,208–315,425–530,620–632,792–804,960–979`：manual-trigger 既定値、timer、world 更新、Vision 作成・送信 signal、有効化条件。
  - 同 commit `src/core/timer.cpp:35–79`、`src/core/include/core/sslprotocols.h:35–45`、`src/simulator/ssl_robocup_server.cpp:29–35,60–69`、同 `.h:38–47`：初期 scaling、Vision 宛先定数、UDP write。`config/simulator/2020B.txt:20–36` と `config/simulator-realism/None.txt:13–14`：camera と35ms Vision delay。
  - 既存二次記録 `reports/issue-128-cm4-namespace-implementation-20261115.md:128–132`、`reports/issue81-bridge-contract-investigation-20261022.md:72,104`。今回の結論は通常ユーザーで取得した上記 pinned 本文から再確認した。
  - `artifacts/duck-simulation/local-20261115T203949Z-c2e44f5a0c2d487d84e22f8c6758e594/`：`startup-mode-20261115T203949047Z-c8f307d20e5f4922ba3e9e6e60102bee.txt`（248 bytes）、`crane-fixture-20261115T203949047Z-c8f307d20e5f4922ba3e9e6e60102bee.json`（109 bytes）、`apphost-20261115T203949047Z-c8f307d20e5f4922ba3e9e6e60102bee.stdout-stderr.log`（2040 bytes）。
  - 変更は本報告の子所有5節だけ。製品ソース・image/tag・設定・既存報告・Dispatch profile・PR/Issue は変更しない。

## 指摘事項

- 指摘要約または「指摘なし」:

  1. **現行 Compose Simulator に `--localhost` / Vision 宛先指定なし（直接コード証拠）。** `BaseSimulationResourceRegistration.cs:120–132` の全 command は `tini -- ./bin/simulator-cli -g <geometry> --realism <realism> --ibis-port <port> --ibis-team-color <color> --ibis-feedback-addr 127.0.0.1`。`AddComposeContainer:51–66` と `ComposeSimulationStack.cs:131,192–196` がこれをそのまま YAML に渡す。Simulator は follower なので同135行と210–212行から CM4 namespace を共有する。`Program.cs:725–732` の base defaults は tag `a52b6bd` / `2020B` / `None` / `yellow`、688–689行の IBIS port は `12346`。これは現在 source/model 上の argv であり、当該 run の実 argv を Docker inspect で証明したものではない。
  2. **現行引数での Vision 出力先は `224.5.23.2:10020`（直接コードからの静的帰結）。** [CLI simulator.cpp:1220–1223](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L1220-L1223) が `--localhost` を登録し、[1267行](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L1267) は有りなら `SSL_VISION_ADDRESS_LOCALHOST`、無しなら `SSL_VISION_ADDRESS`、port は `SSL_SIMULATED_VISION_PORT` を選ぶ。[sslprotocols.h:38–45](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/core/include/core/sslprotocols.h#L38-L45) はそれぞれ `127.0.0.1` / `224.5.23.2` / `10020` を定義。CLI option 全体1214–1245行に別の Vision address 指定はない。`--ibis-feedback-addr` は1227行で別 option として登録され、1321行・1359行で IBIS feedback adaptor に渡されるため、Vision loopback 指定ではない。既存報告130行の `simulator.cpp:1130–1179` というリンク範囲は今回取得した pinned 本文の address 選択位置と一致せず、正しい直接参照は1267行である。過去報告自体は変更していない。
  3. **Game Controller の match start は Vision 開始条件ではない（初期化と timer の直接コード証拠）。** [CLI main:1284–1307](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L1284-L1307) は初期 Command に両チーム11台ずつの specs、geometry、realism を設定して `commands.sendCommand(c)` を emit。設定読込失敗なら1300–1304行で終了するが、GC 試合状態は参照しない。1277行の接続から [SimProxy::handleCommand:635–655](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L635-L655) へ入り、setup があると `new Simulator`、signal 接続、`set_enable(true)`、`gotCommand(m_teamCommand)` を実行する。Simulator constructor の manual trigger は [header:69](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/amun/simulator/include/simulator/simulator.h#L69) で既定 false。実装117–134行は scaling=1、QTimer timeout→process を設定。[Simulator::handleCommand:798–803](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/amun/simulator/simulator.cpp#L798-L803) は enable と current time を反映し、[setScaling:960–979](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/amun/simulator/simulator.cpp#L960-L979) は enabled=true / scaling>0 なら `m_trigger->start(qMax(1,8/scaling))`。`Timer::Timer/reset`（timer.cpp:35–56）も最初から scaling=1。CLI は1397行で受信 thread を開始、1399行で `app.exec()` に入り、その event loop で timer が動く。従って通常の初期化成功・event loop 継続を前提に、外部 simulator command や GC の start を待たず world 更新と Vision 処理が始まる。
  4. **Vision の具体的呼出経路と生成条件。** [Simulator::process:272–312](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/amun/simulator/simulator.cpp#L272-L312) は world を更新し、simulation frame counter が偶数の回（初回counter=0を含む）に `createVisionPacket()` を呼ぶ。125Hz base loop の毎2フレームなので既定62.5Hz。通常 mode は Vision queue と single-shot QTimer を作り、`m_visionDelay / m_timeScaling` 後に `sendVisionPacket()`。現行 realism `None.txt:13` は35ms。`2020B.txt:20–36` にはcamera 0 があり、createVisionPacket:428–430 は detection frame を作り、474–476行は空 detection でも wrapper を作る。従って ball/robot の動作や match start は packet 作成の前提ではない。[sendVisionPacket:620–624](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/amun/simulator/simulator.cpp#L620-L624) → `Simulator::gotPacket` → CLI644行の `SimProxy::gotPacket` →1276行の `SSLVisionServer::sendVisionData` → [582–584行](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L582-L584) の `m_server.send(data)` → [RoboCupSSLServer::send:60–69](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/ssl_robocup_server.cpp#L60-L69) の `QUdpSocket::writeDatagram`。送信 socket はconstructor:29–35で multicast TTL=1。`SSLVisionServer` は send 戻り値を集計せず、低層 send も成功時ログを出さない。失敗時は bytes 不一致に対するログがあるが、ログ不在から成功とは判定できない。
  5. **GC 依存と Vision を混同しない。** CLI1323行は `--ibis-use-referee` の有無を読む。RefereeTeamDetector の生成は1376–1385行の同 flag 分岐内で、teamDetected は IBIS command/feedback adaptor に接続される。現在の registration は明示 `--ibis-team-color yellow` を渡し、`--ibis-use-referee` は渡さない。この分岐にも Vision timer を GC match start まで止める処理はない。今回、Match type や試合状態は変更していない。
  6. **保存物から当該 run の実送信は証明できない。** 通常ユーザーで再確認した startup-mode の4–6行は `2026-11-15T20:39:49.0478689Z` / `base-host-runtime` / `manual`。AppHost log は18行に distributed application started、20行に dashboard listen があるが、Simulator resource の send 呼出・UDP send 成否・packet payload/capture/counter は保存されていない。fixture は crane/YELLOW の設定記録だけ。これらから実際に送られなかったとも、Windows に届いたとも断定しない。

## 結果

- 結果:

  - 静的送信側調査は完了。通常ユーザーで取得した固定 SHA 本文により、**正常初期化した Simulator は GC の試合開始前から world 更新・Vision 送信処理を進める**ことを確認した。今回の「試合を開始しなかった」という観測だけでは受信0を説明できず、試合開始を必須手順として要求する根拠はない。
  - 現行 source の Vision 宛先は `224.5.23.2:10020`、`--localhost` は未指定。CM4 shared namespace 内の multicast 送信処理を持つことはソース上の結論であり、当該 FA780 run の送信実行、send 成功、Windows/Docker 境界通過、RuntimeHost NIC 適合、Windows 到達の実証ではない。
  - 親供給の観測は「Simulator/CM4/Crane/GC/Windows RuntimeHost 起動、health/vision 503、受信・復号0、試合開始操作なし、Crane Tracker受信なし」。本担当の再実行観測ではない。I128-UDP-02 の解消、Windows packet 到達、full-stack 成功は判定していない。
  - 初回のアクセス制限 blocker は新たな本人許可に基づく `fa780\donabe` 実行で解消。報告は repository_file、reserved path は本ファイル。製品変更・commit/push・PR更新・実行試験・merge は行っていない。

## リスク

- 未解決のリスクまたは後続対応:

  - 当該 run の実 argv、runtime enable/scaling、timer 動作、実 packet、UDP send 成否は未確認。今回の保存3 artifact だけでは送信側停止と Windows/Docker/NIC 経路のどちらかに原因を一意確定できない。別担当の保存証拠がある可能性は排除しない。
  - 次の切分けは source 上の match-start gate を探す段階を終え、許可された最小実機確認で、Simulator 側の実送信と Windows RuntimeHost 側の受信を別々に確認すること。既存 image/tag を変更せず、Duck source を container に入れず、Match type は人間確認とする。新依存・設定変更・権限拒否迂回はこの調査の許可に含めない。
  - `--localhost` を足すだけでは同 shared namespace の loopback になり、Windows host loopback に変わるわけではない。ソースにない任意 Vision destination option を前提に修正を提案しない。
