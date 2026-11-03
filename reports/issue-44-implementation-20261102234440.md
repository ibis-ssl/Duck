# Issue #44 implementation report

## Task and scope

Issue #44 / ASPIRE-006F1: add the Sumatra configuration, Game Controller initial state, and match-time configuration as Duck-side fixtures. The focused test locks Blue=`TIGERs Mannheim`, Yellow=`ibis`, `FRIENDLY`, and initial command/game state `STOP`. No work was done for #43 or siblings #45–#49. No resource registration or lifecycle behavior, live Docker/network acceptance, merge, approval, or deployment was included.

## Design and source

Added the fixture contract to `Tracker/Design/Testing/aspire-simulation-test-environment.md`. All three fixture blobs match `ibis-ssl/crane` commit `af6e0d3dec745415ce060ff5de2042afd3ec5145`, paths under `docker/match-vs-tigers/config`.

The initial JSON has command `STOP`, game state `STOP`, Blue `TIGERs Mannheim`, Yellow `ibis`, and match type `FRIENDLY`. The Sumatra protocol listens to referee UDP 11003 with `source=NETWORK` and `gameController=false`. The Game Controller YAML points to `/tmp/state-store.json.stream` and defines 45-second normal and 30-second overtime halves.

## Validation

Red run, before adding fixture files:

`dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~MatchFixturesKeepTigersConfigurationAndInitialFriendlyStopState`

The build succeeded; the focused test exited 1 at the expected missing fixture path (`DirectoryNotFoundException` for `Fixtures/match/simulation_protocol_tigers.xml`). This recorded the pre-implementation failure.

After adding the fixtures and AppHost copy metadata, the same focused command exited 0: 1 passed, 0 failed, 0 skipped. After normal review requested a missing assertion for `state.command.type == STOP`, the assertion was added and the same focused command again exited 0: 1 passed, 0 failed, 0 skipped. The command emitted existing NU1902 MessagePack and Aspire/analyzer warnings; no build/test failure remained.

## Files and commits

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`
- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs`
- `Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj`
- `Testing/Duck.Testing.AppHost/Fixtures/match/simulation_protocol_tigers.xml`
- `Testing/Duck.Testing.AppHost/Fixtures/match/ssl-game-controller-match.yaml`
- `Testing/Duck.Testing.AppHost/Fixtures/match/state-store-initial.json.stream`

Commits: `f81a804` (design and Red test), `7e4fa41` (fixtures and copy metadata), `5489cda` (review-requested command assertion). All were pushed to `task/issue44-aspire-006f1`.

## PR and formal linkage

PR: https://github.com/ibis-ssl/Duck/pull/106

At readback after the latest push, PR #106 is still Draft at head `5489cda6af13ff182a747cdfc0c3a6950abe87e6`. Its `closingIssuesReferences` contains #44 only. Project 16 item #44 has `Linked pull requests` set to PR #106 and status `In progress`. #43 and #18 are not closing references. The issue/PR relationship was created through `addCloseIssueReferences` for #44 only.

## Review

Normal review on `7e4fa41` found NR44-001 (P2): the test checked `gameState.type == STOP` but did not lock `command.type == STOP`. The assertion was added in `5489cda`; the same reviewer rechecked the finding and confirmed closure with focused test 1/1 passing. Requested review profile was `gpt-6.1-sol/high`; exact applied runtime profile is unverified (`applied: null`). Independent final review is reserved at code/report-freeze candidate HEAD `5489cda6af13ff182a747cdfc0c3a6950abe87e6`; no independent findings are claimed yet.

## Requested profile and evidence limits

Implementation/investigation requested profile: `gpt-6-luna/high`, as explicitly set by the user. The runtime does not expose exact applied model metadata; record `applied: null`, `spawn_succeeded_profile_unverified`. Review profile requested: `gpt-6.1-sol/high`; runtime applied metadata is likewise unverified. No performance or live Docker/network claim is made because this issue only versions fixture inputs and verifies their content.

## Skill-gap and handoff disposition

No missing or conflicting Skill instruction was discovered during this bounded lifecycle; no Skill update is proposed. The child remains In progress until independent review and the PR report-comment readback are complete. Parent owns aggregate #43/#18 reporting and final status. The pre-freeze Japanese PR report draft is below; post it only after independent review, then read back its full text.

## Japanese PR report draft

1. **変更前の困り事**: TIGERs 対 Crane の対戦用に使う Sumatra 設定、Game Controller の初期状態、試合時間が Duck 側の版管理された入力になっておらず、必要な初期条件を試験で固定できませんでした。
2. **今回できること**: Duck AppHost の出力に3つの対戦用 fixture を含め、Blue=`TIGERs Mannheim`、Yellow=`ibis`、`FRIENDLY`、初期 command/game state=`STOP` を focused test で確認できます。
3. **動く要点**: pinned Crane commit `af6e0d3dec745415ce060ff5de2042afd3ec5145` の Sumatra protocol、Game Controller match YAML、initial state を `Fixtures/match` に追加しました。Sumatra は NETWORK source で 11003 を読み、match YAML は通常45秒・延長30秒、state fixture は指定のチームと friendly/STOP を保持します。
4. **含まない/後続**: match モードや resource 登録、Simulator/Game Controller 起動、外部 TIGERs/AutoRef/Crane 接続、match-controller lifecycle、live Docker/network acceptance は含みません。後続 #45–#49 の範囲です。
5. **検証済み/未検証**: Red で fixture 不在により focused test が失敗し、fixture と content-copy metadata 追加後および command STOP の review fix 後は focused test が 1/1 pass しました。既存 NU1902、Aspire/analyzer 警告はありました。live Docker/network 動作は未検証です。
6. **判断操作と影響**: fixture 値は pinned Crane source に合わせ、差分を fixture/test/design に限定しました。PR #106 は Draft のまま、formal closing reference は #44 のみで、Project 16 の #44 Linked pull requests に #106 が登録されています。マージ、承認、deploy は行っていません。

## Remaining

Complete the independent final review against the frozen candidate, post the Japanese six-section PR report comment, and read it back. Do not merge; leave PR #106 Draft and report final evidence to the parent.
