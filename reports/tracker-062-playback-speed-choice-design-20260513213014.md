# TRACKER-062 playback speed choice design

## 概要

- タスク: TRACKER-062 diagnostics playback UI を従来ボタン配置に戻し、速度選択に等倍速を追加する
- 種別: design
- 担当: TRACKER-062 design sub-agent（ユーザー指定: gpt-5.5 high）
- 結論: TRACKER-061 の巨大な playback choice button 群は撤回し、Play / Fast Forward / Stop の従来 transport button 配置を戻す。速度選択側には `等倍速`、`4x`、`16x`、`64x` を compact tabs として並べる。

## 参照した前提

- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`
- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Server/README.md`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
- `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
- `reports/tracker-061-playback-ui-separation-design-20260513204405.md`
- `reports/tracker-061-review-r2-20260513210407.md`

## 変更ファイル

- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Server/README.md`
- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`
- `reports/tracker-062-playback-speed-choice-design-20260513213014.md`

## 設計判断

TRACKER-061 は `等倍速`、`4x`、`16x`、`64x` を playback action button として並べたが、ユーザー意図は「従来のボタン配置を変える」ことではなく、「速度選択側に等倍速を追加する」ことだった。したがって、TRACKER-062 では Play icon button、Fast Forward icon button、Stop button の transport 配置を戻す。

速度選択は select へ項目追加する案も可能だが、ユーザーが「選択肢のタブ」と言っていること、`等倍速` と調査用倍率が独立 choices として見える必要があることから、compact segmented/tabs を採用する。tabs は scrubber 行の補助 control として小さく置き、TRACKER-061 のような巨大な action button 群にはしない。

`1x` 表記は使わない。等倍速の表示は必ず `等倍速` とする。

速度 choice と playback mode の対応は次で固定する。

- `等倍速`: `DiagnosticsPlaybackMode.Play`、TRACKER-060 の30fps相当 realtime stepping。
- `4x` / `16x` / `64x`: `DiagnosticsPlaybackMode.FastForward`、該当 multiplier、TRACKER-059 の tick 非間引き Fast Forward。

transport 操作は次で固定する。

- Play button は `等倍速` choice を選択して Play を開始する。
- Fast Forward button は選択中の fast multiplier で FastForward を開始する。現在 choice が `等倍速` の場合は既定 fast multiplier へ切り替えて開始する。
- active mode の停止は active Play / Fast Forward affordance が Stop button へ入れ替わる、または同じ位置の Stop button で止める構成とする。
- 速度 tab 自体を Stop action に変えない。

saved alignment v2、timeline scrubber、Field source selector、`Tracker Comparison` panel、`Tracker.CaptureReplay` の任意 tick 比較経路は変更しない。

## 実装への指示

- `DiagnosticsPlaybackState.PlaybackChoices` を action button 群として使う構成から、速度 choice model へ読み替えるか、新しい `PlaybackSpeedChoices` 相当へ分離する。
- `Diagnostics.razor` の playback controls は Play / Fast Forward / Stop の icon button 配置に戻す。
- 速度 choices は `等倍速`、`4x`、`16x`、`64x` の compact segmented/tabs として描画する。
- `Diagnostics.razor.css` は compact tabs の fixed height / stable width を持たせ、scrubber 行の text overlap や layout shift を避ける。
- `Diagnostics.razor.cs` は selected speed choice と active playback mode を分けて管理する。
- `StartPlaybackAsync(DiagnosticsPlaybackMode.Play)` は TRACKER-060 の realtime stepping を維持する。
- `StartPlaybackAsync(DiagnosticsPlaybackMode.FastForward)` は選択中 fast multiplier を使い、TRACKER-059 の tick 非間引きを維持する。
- `StopPlayback`、末尾到達時の先頭戻り、mode switch / speed switch stale guard は既存契約を維持する。
- `Tracker/Tracker.Server/appsettings.json` は既存 dirty のため変更・revert しない。

## TDD acceptance

- playback controls に Play icon button、Fast Forward icon button、Stop button の従来配置が存在し、`等倍速` / `4x` / `16x` / `64x` が巨大な action button として描画されない。
- 速度選択 tabs に `等倍速`、`4x`、`16x`、`64x` がこの順で表示され、`1x` 表記が存在しない。
- `等倍速` 選択時に Play を押すと `DiagnosticsPlaybackMode.Play` が開始し、Fast Forward multiplier を変更しない。
- `4x` / `16x` / `64x` 選択時に Fast Forward を押すと `DiagnosticsPlaybackMode.FastForward` が該当 multiplier で開始する。
- `等倍速` 選択中に Fast Forward を押す場合は既定 fast multiplier へ切り替えて FastForward を開始する。
- Play は30fps相当 realtime stepping の既存 tests を維持する。
- Fast Forward は tick を間引かず 1 replay timeline tick ずつ進む既存 tests を維持する。
- Stop / mode switch / speed switch 後の queued tick は stale guard で破棄される。
- timeline scrubber、Field source、comparison は selected replay timeline tick と saved alignment v2 record から任意 tick を選択・比較できる。
- source text 検査だけでなく、可能な範囲で UI state / component contract に寄せた regression test を追加する。

## 残リスク

- 実装前のため、現行コードはまだ TRACKER-061 の choice button UI のままである。
- compact tabs の見た目は実装時に scrubber 幅、timeline meta、mobile 幅での折り返しを確認する必要がある。
- 既存 focused test には source text 検査寄りのものが残っているため、実装時は component contract に寄せた test を追加する余地がある。
- full `Tracker.Tests` は既存 dirty `Tracker/Tracker.Server/appsettings.json` の影響で default-off contract failure が出る可能性がある。今回設計では同 file を変更しない。
