# Duck #128 kickoff next_command TDD 修正

## 対象と基準

- Repository: `ibis-ssl/Duck`
- PR: #160 (`task/issue-128-windows-match` → `main`)
- 基準 HEAD: `202b75b5275e4d1172f289e9dac0a7c3ba3854b0`
- 目的: 固定 GC 3.20.3 の同チーム `PREPARE_KICKOFF` UDP と、同一の新鮮な観測に整合する GC API の `NORMAL_START / READY_AUTO` を照合して一度だけ開始指令を送れるようにする。

## 範囲と対象外

- 範囲: Referee kickoff gate、関連する RefereeDriver 回帰テスト、既存の対戦開始設計文書の最小更新。
- 対象外: 固定イメージ、Planner、既存10秒開始期限、試合完走・終了条件、通常の確認済み開始経路、FA780、手動 CI、commit、push、merge。

## TDD 証拠

- 対象テスト: `Tracker/Tracker.Tests/RefereeDriverFixtureTests.cs` の標準 kickoff と stale/mismatch 対照、および `RefereeDriverHostAutoDriveTests.cs` の UDP→WebSocket統合試験。
- Red コマンド: `DOTNET_CLI_HOME=/tmp/duck128-dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /workspace/.duck-env/dotnet/dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~DriveToActiveAsyncAcceptsOfficialPrepareCommandForEitherKickoffTeam --logger 'console;verbosity=minimal'`。終了値1、黄/青2ケースが失敗。両方とも `WaitForNormalStartReadinessAsync` で `Unexpected additional lifecycle observation request`。固定 GC の Yellow command=4/nextCommand=4、Blue command=5/nextCommand=5 を正しく模した入力が現行ゲートで除外されることを再現した。
- Green: `DOTNET_CLI_HOME=/tmp/duck128-dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /workspace/.duck-env/dotnet/dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~RefereeDriverFixtureTests --logger 'console;verbosity=minimal'`。修正後52成功、0失敗。さらに下記統合フィルターで136成功、0失敗。
- 統合試験で最初に2ケース失敗し、Host packet fixture が command 5 の prepare packet に旧 nextCommand=2 を付ける不整合を特定。fixture を command=nextCommand の3.20.3形に直して再実行し、Host 自動開始を含む統合試験が成功した。
- ケース: 黄4/4と青5/5の成功と一度だけの `NORMAL_START`、別チーム・API stage・score 不一致を拒否して一致観測まで待つ、古い sequence・過去の packet timestamp・UDP nextCommand のチーム不一致で送信しないこと、既存の `READY_MANUAL` から `READY_AUTO` まで待つケースを確認。送信後の新しい `NORMAL_START` は既存 driver fixture と Host 自動開始試験で確認した。

## 設計記録と実装

- 変更: `RefereeDriver.IsKickoffLifecycleContext` が UDP nextCommand を同チームの準備指令と照合。コメントを修正。正常 kickoff fixtures を 4/4・5/5 に合わせ、Host packet emitter も prepare packet の nextCommand を自身の prepare command に変更。
- 観測相関: `RefereeApiMatcher` が同一要求内の新鮮な UDP packet・API read context を照合し、Driver は同じ observation sequence を使う。ゲートは API の `KICKOFF` / `KICKOFF`、同チーム、stage、score と、現在の continuation context の `NORMAL_START / READY_AUTO`、問題なしを照合した。
- 他経路との差: `SimulatorBallPlacementFixture.IsExpectedKickoffProgression` は別の保存済み形式として `API nextCommand=NORMAL_START` / `UDP nextCommand=NORMAL_START` も扱う。この別経路は変更せず、通常開始ゲートでは固定 GC 3.20.3 の同色 prepare 形式に限定した。標準起動の GC 参照は `robocupssl/ssl-game-controller:3.20.3`。match mode は lock の digest 付き `robocupssl/ssl-game-controller:latest` を使い、実ソフトウェア版は未確認。
- 設計文書: `Tracker/Design/Testing/aspire-simulation-test-environment.md` に UDP `next_command` と GC の `NORMAL_START` continuation action の違い、同観測の team/stage/score 照合、match-mode 版の未確認範囲を記録。`Tracker/Design/phases-status.md` にローカル検証と未完了条件を追記。

## 検証と未確認

- その他の関連検証: `DOTNET_CLI_HOME=/tmp/duck128-dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /workspace/.duck-env/dotnet/dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~RefereeApiMatcherTests|FullyQualifiedName~SimulatorBallPlacementFixtureTests|FullyQualifiedName~RefereeDriverHostAutoDriveTests|FullyQualifiedName~RefereeDriverFixtureTests' --logger 'console;verbosity=minimal'` は136成功、0失敗、0 skip。`git diff --check` 成功。Markdown textlint は root の `node_modules/.bin/textlint` がなく未実施（依存追加・npm ci はしていない）。
- 影響範囲: fixed image reference、Planner、10秒開始期限、full-match gate、POST_GAME、match completion/cleanup 条件は変更していない。match-mode の `latest` が3.20.3と同じ UDP 契約かは未確認。
- 未実施: FA780、手動 CI、実試合、push、commit。警告 NU1902/NU1903/NU1904 と Aspire010 は検証実行で表示された既存警告で、今回の変更はエラーを出していない。
- 次: 独立レビュー後、match-modeの固定 latest digest と実プロトコル形の整合を正規 CI で確認する。CI/FA780 の成功や #128 の完了とは扱わない。

## Dispatch profile

<!-- Parent-owned. Do not infer or rewrite hidden runtime state. -->

- selection inputs (parent, pre-dispatch): implementation + TDD; local protocol/state-machine gate; medium uncertainty due different match-mode image reference; local change radius; sequential test→fix dependency.
- selection source (parent, pre-dispatch): active CodexSkill feedback point FP-20260511-001 and explicit user-authored preference for sub-agent coding/testing at `gpt-5.5/high`.
- observed decomposability (parent, pre-dispatch): sequential_dependencies.
- decomposition policy / disposition (parent, pre-dispatch): allowed; no parallel split because Red→Green, test, implementation, and design update are coupled.
- proposed profile (parent, pre-dispatch if applicable): none.
- approval status / evidence (parent): not applicable; selected profile is the active explicit preference, no expensive-profile proposal.
- requested profile (parent, pre-dispatch): `gpt-5.5`, `high`, fork `none`.
- agent role / default-role plan (parent, pre-dispatch): runtime default role; no explicit role selector exposed by the spawn tool.
- role config evidence / profile effect (parent, pre-dispatch): no role override configuration found in workspace `.agents`/`.codex`; final effect not observable.
- planned runtime profile after known role constraints (parent, pre-dispatch): requested `gpt-5.5/high`, subject to runtime default-role application.
- applied profile (parent, post-runtime exact evidence only; null when unverified):
- application status (parent, post-runtime evidence only):
- runtime profile observability (parent, post-runtime):
- fork policy (parent): `none`.
- reasons / constraints (parent): no nested agent spawning; no commit, push, CI dispatch, FA780, image, or product-scope expansion.
- execution update after initial planning: user instructed not to spawn hidden sub-agents and directed direct implementation in this existing chat at `gpt-6-luna/high`. No sub-agent was started after that instruction. Actual runtime model application is not independently observable.
