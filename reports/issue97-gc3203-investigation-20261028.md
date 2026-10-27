# Sub-agent実行レポート

## タスク

- 目的: Duck #97、GC 3.20.3 の AutoRef/Halt 抑止方法を一次資料で特定する。
- タスク識別: https://github.com/ibis-ssl/Duck/issues/97 / PR96 / task/issue78-referee-state / e82bef6101e6111369f06519eb3910f18515f3d1

## sub-agentを使う理由

- 指定されたIssue別調査担当による根拠収集。development-orchestrator適用、codex-delegation-executor経由。

## 対象範囲

- GC 3.20.3の一次資料、候補1件、操作前提・観測・安全停止条件。調査のみ。

## 対象外

- 実装、設定変更、GC/AutoRef本体、本番設定、Tracker UI、Issue98以降、merge。

## Dispatch profile

- selection inputs: investigation / bounded_technical / uncertainty medium / cross_system / ordinary / single / sequential_dependencies / fresh
- selection source: user explicit override; sub-agent-task-manager
- decomposition policy: forbidden (子Issue順次、同時最大2件)
- proposed profile: none; Astra not applicable
- approval evidence: top-level delegation transcript「それで進めてください」
- requested profile: gpt-6-luna / high
- role plan: runtime default, exposed spawn schema has no agent_type setting; explicit model/effort allowed with fork none
- planned runtime profile: gpt-6-luna / high
- applied profile: null
- application status: spawn_succeeded_profile_unverified; agent /root/issue97_research
- runtime profile observability: final_profile_hidden
- reviewer continuity: not applicable
- fork policy: none, task-local explicit context
- constraints: no nested agents; only Issue97; profile application must not be inferred from prompt

## 実行コマンド

- `whoami` (exit 0; 結果 `fa780\donabe`)、`gh issue view 97 --repo ibis-ssl/Duck --json number,title,body,url` (exit 0)、Git状態/HEAD確認は成功。レポート再構成PowerShell PID 24072/exit 0、追加追記PowerShell PID 9172/exit 0。GitHub API/公式rawソース取得は成功 (Contents APIの一括取得は404になったためraw tag URLで取り直し)。webソース取得は成功。web/execツールが内部で起動した各プロセスIDは表示されない。

- 限定検証対象: branch `task/issue78-referee-state`, HEAD `e82bef6101e6111369f06519eb3910f18515f3d1`; `git diff --binary` SHA-256 407A277FF0C06F582A988F48033BE6C34EF553E8C66A38714DF3D6D14959ECD8。差分は親所有のtasks-status/phases-status変更と本レポートのみ。実行runner PID 26780。
- 初回 `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false`: PID 2892、exit 1。
- 初回 `dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore -m:1 /nr:false`: PID 9932、exit 1。両方で `NETSDK1004`: `Testing/Duck.Testing.RefereeDriver/obj/project.assets.json` が欠落。
- `git diff --check`: PID 24888、exit 0、stdout空。stderrは親所有tracker変更のLF/CRLF変換warningのみ。
- `npm run lint:md`: PID 32424、exit 1。`lint:md:text`は完了、`lint:md:spell`で382 issues/7 filesとなり停止、`lint:md:whitelist`は未実行。対象には親編集の `Tracker/Design/tasks-status.md` と `phases-status.md`、既存の `Tracker/Design/Testing/aspire-simulation-test-environment.md`、`tracker-comparison-debug-design.md`、`.venv`配下ライセンスMarkdown、`artifacts/pr28-aspire-003-comment.md` が含まれる。
- 既存定義を変更せず `dotnet restore Testing/Duck.Testing.RefereeDriver/Duck.Testing.RefereeDriver.csproj --ignore-failed-sources` を実行 (PID 27440、exit 0)。RefereeDriverと既存SslProtoを復元し、project.assets.jsonを生成。再実行build: Tracker.Tests PID 18072/exit 0、AppHost PID 6072/exit 0。AppHostは `ASPIRE010` warning 1件、error 0。
- lint対象照合: 今回の調査レポートとartifactログは `npm run lint:md` の26対象に含まれない。今回の子所有レポートにCSpell指摘は確認されていない。

## 対象ファイル

- 読取: #97 Issue本文、公式GC v3.20.3 tag/commitとrelease、engine config proto/Go実装、AutoRef RCON受信、proposal processing、state machine event/passive handler、AutoRef設定UIコンポーネント。
- 書込: この事前作成済レポートのみ。AutoRef/GC設定やソースは変更していない。
- 当初/終了時の対象ブランチ `task/issue78-referee-state`、HEAD `e82bef6101e6111369f06519eb3910f18515f3d1`。親所有の `Tracker/Design/tasks-status.md` と `phases-status.md` は作業中に変更表示になったため保持し、編集していない。

## 指摘・結果

- 固定した公式リリース: [v3.20.3](https://github.com/RoboCup-SSL/ssl-game-controller/releases/tag/v3.20.3)、tag commit `8050f232c3130323bbd91d1d3d56e9553506c8e4`。
- 実在する候補: 試験AutoRef固有 `AutoRefConfig.game_event_behavior[event] = BEHAVIOR_LOG`。一次資料は[`ssl_gc_engine_config.proto`](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/proto/engine/ssl_gc_engine_config.proto#L34-L49)。`BEHAVIOR_LOG` はイベントを受動記録し、`BEHAVIOR_IGNORE` は破棄。
- UI実装[`AutoRefereeBehaviorConfig.vue`](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/frontend/src/components/settings/AutoRefereeBehaviorConfig.vue#L30-L52)の `update` は、指定AutoRef IDとイベントkeyで `control.ChangeConfig({autoRefConfigs: ...})` を送る。UI説明(L55-L68)はAccept=global behaviorに従い処理、Log=passive eventとしてprotocolに追加し処理しない、Ignore=黙って破棄と明記。設定候補ボタンは[`AutoRefereeBehaviorConfigInput.vue`](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/frontend/src/components/settings/AutoRefereeBehaviorConfigInput.vue#L8-L12)。接続AutoRef一覧は[`AutoRefereeConfigs.vue`](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/frontend/src/components/settings/AutoRefereeConfigs.vue#L8-L20)から確認できる。
- AutoRef固有IDは推測せず、登録プロトコルの[`AutoRefRegistration.identifier`](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/proto/rcon/ssl_gc_rcon_autoref.proto#L5-L10)を正とする。同版[`server_autoref.go`](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/internal/app/rcon/server_autoref.go#L31-L43)はこれをclient IDとして取得し、同一IDで重複接続を拒否、config keyにも同じIDを用いる(L81-L94)。接続後はAutoRef設定画面のtabに表示される名とGC接続状態の一致を確認し、そのIDを選択する。接続切断でstateから削除される(L73-L79)。
- [`engine/proposals.go`](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/internal/app/engine/proposals.go#L17-L49)でAutoRef設定がLogなら `AddPassiveGameEvent` をenqueueし、proposal/event処理へ送らない。[`change_passivegameevent.go`](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/internal/app/statemachine/change_passivegameevent.go#L6-L9)は受動イベントを記録し state changeを返さない。通常の受理後処理にはHaltが明示される例として、[`change_gameevent.go`](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/internal/app/statemachine/change_gameevent.go#L45-L50)の二度目のAttackerTooClose、possible goal (L82-L109)、bot substitution (L111-L120)がある。
- UIで対象にできるeventは無制限な任意文字列ではなく、[`GameEventsForBehaviorConfig`](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/internal/app/state/gameevent.go#L43-L79)が返す列挙値: BALL_LEFT_FIELD_TOUCH_LINE, BALL_LEFT_FIELD_GOAL_LINE, AIMLESS_KICK, ATTACKER_TOO_CLOSE_TO_DEFENSE_AREA, DEFENDER_IN_DEFENSE_AREA, BOUNDARY_CROSSING, KEEPER_HELD_BALL, BOT_DRIBBLED_BALL_TOO_FAR, BOT_PUSHED_BOT, BOT_HELD_BALL_DELIBERATELY, BOT_TIPPED_OVER, BOT_DROPPED_PARTS, ATTACKER_TOUCHED_BALL_IN_DEFENSE_AREA, BOT_KICKED_BALL_TOO_FAST, BOT_CRASH_UNIQUE, BOT_CRASH_DRAWN, DEFENDER_TOO_CLOSE_TO_KICK_POINT, BOT_TOO_FAST_IN_STOP, BOT_INTERFERED_PLACEMENT, POSSIBLE_GOAL, GOAL, INVALID_GOAL, ATTACKER_DOUBLE_TOUCHED_BALL, PLACEMENT_SUCCEEDED, PENALTY_KICK_FAILED, NO_PROGRESS_IN_GAME, TOO_MANY_ROBOTS. UI `gameEventTypes`は該当AutoRef設定mapのkeysを使うので、試験対象eventはこの一覧のうち接続後の設定UIに存在するkeyから選択し、未表示keyを手入力しない。
- 結論から導く #98用再現手順: 1) 試験GCがv3.20.3であると確認。異版なら中止。2) 試験AutoRefを接続し、AutoRef設定一覧にそのIDと接続状態が表示されることを確認。3) 対象eventをそのAutoRefの設定でLogへ変更 (種類未特定の評価ではそのAutoRefのAllをLog) し、設定表示を再確認。4) 対象条件を発生させ、passive eventとして記録される一方、AutoRef由来pending proposal group/受理済GameEvent/command stateが増えないことを観測。5) 試験終了後は試験環境の保存前configへ戻し、復元を確認。これはソースから導いた手順で、実機では未実行。
- 前提: v3.20.3の試験専用controller、試験AutoRef client、対象AutoRefとeventをUI上で特定できる運用者、passive logとGameController stateを監視できること。設定変更はcontroller configへ保存されるため、本番環境では試さない。
- 期待状態: 対象AutoRefのイベントがpassive記録され、AutoRef由来proposal group/game event/command transitionに入らない。特にpossible goalなど通常処理がHaltを起こすイベントも、そのAutoRef入力について処理から外れる。
- 安全停止条件: バージョン不一致、ID特定不可/切断、Logが設定/表示できない、passive eventが観測できない、あるいは予期しないproposal/state/command遷移があれば試験入力を止め、その試験を中断。危険状態なら運用者が既定の手順でGCを手動Haltし、原因未解明のまま再開しない。AutoRef Logは手動Haltや他のAutoRef、Tracker/GC内蔵ロジックを止める包括安全機能ではない。

## 結論

- 候補は試験用GameControllerで対象AutoRefの対象event (不特定なら当該AutoRefの全event) を `BEHAVIOR_LOG` にする方法1件に絞る。3.20.3のUI文言とengine処理でpassive記録かつ非処理が確認できるため、AutoRef提案による通常のHalt遷移を避けつつ観測を残す候補として根拠がある。実機再現と運用面の挙動は未確認で、#98側の実機確認が必要。

## 残リスク

- GC/AutoRef実機動作および復元手順は未試験。別経路のHaltは抑止しない。指定buildは限定restore後に両方成功。`npm run lint:md`はCSpell 382 issues/7 filesで失敗し、whitelist stageは未実行。build/test実行、commit/push/reviewは未実施 (本調査の対象外)。

## 親による工程判定

- development-orchestrator / work-context-manager / task-consistency-manager を適用。今回の調査は製品・設定の挙動を変更しないため設計変更なし、TDD非該当。後続実装のTDD要件は既存設計に従う。
- 最新性: CodexSkill 9ebde2cecc272bf65754f36628f1072805272117 は main と履歴が分岐するが、fetch後の origin/main 8ee6e9f80a0362a4d066c4ea39fd3e87002fc829 と skills/ 内容が一致。履歴変更なし。DuckのルートAGENTS.md/.agents/skillsは未存在と通知済み。
- 正式リンク: #78の正式Sub-issuesは#97〜#103、PR96のclosing referencesも全件確認。#97 Project項目はBacklog。Project状態の更新は通常担当へ引き継ぐ。
- Skill-gap decision: no skill action needed。今回固有のGC仕様は製品調査の範囲で、汎用Skill変更は不要。
- feedback-points-manager: 新規追加/既存統合なし。記録契機は親の工程確認、今回新しい再利用可能な工程欠陥は確認していないためIssue作成・台帳変更はスキップ。既存ledgerと他作業は保持。
- verification_capability: local_execution_available。実行地点FA780、Windows PowerShell、ユーザーfa780\donabe。build再実行2件は成功、初回のassets不足とlint失敗は上記のとおり保持。
- technical_head: e82bef6101e6111369f06519eb3910f18515f3d1 + この調査報告と追跡文書の差分。commit: commit_pending / push: push_pending / CI: 新変更に対して未実施。PR96既存CI run40588987224は旧公開HEADのtest成功、full-stack/packet-flowはskipであり今回の実機証拠ではない。
- 通常/独立レビューとIssue証拠記録は未完了。GC実機確認は#98。親#78とPR96を未完了のまま維持し、merge/closeしない。