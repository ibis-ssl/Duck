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
- Red: `DriveToActiveAsyncAcceptsPinnedAndLinuxPreparePacketVariantsForEitherKickoffTeam` の Linux 形2ケースは修正前に失敗し、`WaitForNormalStartReadinessAsync` が追加観測を要求した。通常 CI の9件も `MatchControllerWorkerTests` 全体で再現: 17件中9失敗、8成功、主に開始 stage で止まる。
- Green: API matcher、ball-placement fixture、Host auto-drive、RefereeDriver と `MatchControllerWorkerTests` を含むフィルター試験112件が成功、0失敗。MatchControllerWorkerTests は全17件成功。既存 helper の UDP `next_command=NORMAL_START` は Linux image の実契約を表すため書き換えていない。
- `RuntimeIdentityControlNonCooperativeLivenessTests.RelayReturnsWhenFlushIgnoresCancellationToken` は個別再実行で成功 (211 ms)。根因は未確認で、別の非協力 flush ストリームのliveness試験であり kickoff 経路に結び付く証拠なし。コード変更なし。
- ケース: 黄4/4と青5/5の成功と一度だけの `NORMAL_START`、別チーム・API stage・score 不一致を拒否して一致観測まで待つ、古い sequence・過去の packet timestamp・UDP nextCommand のチーム不一致で送信しないこと、既存の `READY_MANUAL` から `READY_AUTO` まで待つケースを確認。送信後の新しい `NORMAL_START` は既存 driver fixture と Host 自動開始試験で確認した。

## 設計記録と実装

- 変更: `RefereeDriver.IsKickoffLifecycleContext` は UDP nextCommand が同チームの準備指令、または Linux 形の `NORMAL_START` の場合にだけ続行する。API の同チーム `KICKOFF` など他の照合は維持。Linux 形の API team 不一致と黄/青 command-nextCommand の不正組合せを拒否する対照を追加した。`MatchControllerWorkerTests` の Linux packet helper は実契約として維持。
- 観測相関: `RefereeApiMatcher` が同一要求内の新鮮な UDP packet・API read context を照合し、Driver は同じ observation sequence を使う。ゲートは API の `KICKOFF` / `KICKOFF`、同チーム、stage、score と、現在の continuation context の `NORMAL_START / READY_AUTO`、問題なしを照合した。
- 他経路との差: Windows 標準 GC 3.20.3 の UDP 形は Yellow 4/4、Blue 5/5。Linux match image に対応する上流 GC `e267c58abfe351b4051cd28e9380e60de6c028bb` (`internal/app/publish/messagegenerator.go:116-122`) は PREPARE 中の UDP `next_command` を `NORMAL_START` に上書きして Yellow 4/2、Blue 5/2 を出す一方、internal `NextCommand` と API は同じチームの `KICKOFF` を保つ。Driver は2組だけ許し、API freshness/command/phase/stage/team/score、continuation `NORMAL_START / READY_AUTO` と issues なしの照合を維持する。`SimulatorBallPlacementFixture` の別保存形互換分岐は変更しない。
- 設計文書: `Tracker/Design/Testing/aspire-simulation-test-environment.md` に UDP `next_command` と GC の `NORMAL_START` continuation action の違い、同観測の team/stage/score 照合、match-mode 版の未確認範囲を記録。`Tracker/Design/phases-status.md` にローカル検証と未完了条件を追記。

## 検証と未確認

- その他の関連検証: 最終フィルターは上記112成功で全関連 gate・fixture・Host test と `MatchControllerWorkerTests` を含む。`git diff --check` 成功。Markdown textlint は root の `node_modules/.bin/textlint` がなく未実施（依存追加・npm ci はしていない）。
- 影響範囲: fixed image reference、Planner、10秒開始期限、full-match gate、POST_GAME、match completion/cleanup 条件は変更していない。Linux match の UDP 契約は上記 e267c58 source として確認した。別の非協力 flush test の CI失敗は1回のローカル成功だけでは原因確定しない。
- 未実施: FA780、手動 CI、実試合、push、commit。警告 NU1902/NU1903/NU1904 と Aspire010 は検証実行で表示された既存警告で、今回の変更はエラーを出していない。
- 次: 独立レビュー後、変更候補で正規 CI を確認する。今回のローカル試験は Linux match の実運用成功、full match 完走、FA780、#128 完了とは扱わない。

## 公開 d9f5d8b 後の通常 CI 失敗フォローアップ

- CI `45763811159` で .NET test job `139719786935` は1646成功・4 skip・10失敗。公開 `d9f5d8b3728a7cdf584f64b64037e3f03f89fc61` から `MatchControllerWorkerTests` を全実行し、17件中9失敗・8成功を再現した。9件は開始 stage で停まり、公開コードの UDP `next_command==PREPARE_KICKOFF_*` 限定と一致した。
- 上流 GC `e267c58abfe351b4051cd28e9380e60de6c028bb` の `internal/app/publish/messagegenerator.go:116-122` を確認。Linux match GC は PREPARE 中に UDP next_command を `NORMAL_START` にするが internal NextCommand は準備 abort 後に再発行できるよう team-specific kickoff のまま。Linuxの有効な wire tuple は Yellow 4/2、Blue 5/2。Windows 3.20.3 は team-specific PREPARE next_command を出す (Yellow 4/4、Blue 5/5)。API は `KICKOFF`, same-team nextCommand, stage, score、continuation `NORMAL_START / READY_AUTO` を使う既存相関を維持した。
- 最小修正では UDP next_command が準備 command と一致する形式、または `NORMAL_START` の形式のみ受け入れ、同じ新鮮な API/continuation checks を必須にする。APIの別チーム/stage/score mismatch、古いsequence/packet、command mismatchは送信しない回帰対照を追加。linux-teamのAPI team不一致と不正Yellow-Blue wire pairも拒否する。
- 最終ローカル検証は `DOTNET_CLI_HOME=/tmp/duck128-dotnet-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 /workspace/.duck-env/dotnet/dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~MatchControllerWorkerTests|FullyQualifiedName~RefereeDriverFixtureTests|FullyQualifiedName~RefereeDriverHostAutoDriveTests|FullyQualifiedName~RefereeApiMatcherTests' --logger 'console;verbosity=minimal'`:112件成功、0失敗。MatchControllerWorkerTests 単独は17/17。`RuntimeIdentityControlNonCooperativeLivenessTests` 単独は4/4、対象 `RelayReturnsWhenFlushIgnoresCancellationToken` も成功。CIでの同テスト1件失敗の根因は未確認であり、無関係なコードを変更していない。
- 公開 HEAD は引き続き `d9f5d8b...`。この追補は未コミット・未公開。新しいCI、push、FA780は行っていない。

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
