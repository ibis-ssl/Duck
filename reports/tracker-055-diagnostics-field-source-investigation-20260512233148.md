# TRACKER-055 diagnostics field source investigation

## 目的

- `/diagnostics` の下部 Field 表示で、右側の tracker field を ibis tracker だけでなく 3rd party tracker snapshot に切り替えられるようにする設計・実装範囲を調査する。
- `Tracker Comparison` panel は補助情報として折り畳めるようにし、通常確認の主役を Field 表示に戻す。

## 背景

- ユーザーは `Tracker Comparison` の数値表示ではなく、下の Field に snapshot を表示して差を見たい。
- 現在は左に Vision、右に ibis tracker が固定表示されている。
- 右側表示を任意に 3rd party tracker へ切り替えられる必要がある。

## 調査結果

- 実行コマンド
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' reports/tracker-055-diagnostics-field-source-investigation-20260512233148.md`
  - `sed -n` / `nl -ba` で `Diagnostics.razor`、`Diagnostics.razor.cs`、`Diagnostics.razor.css`、`DiagnosticsFieldViewFactory.cs`、`TrackerDiagnosticsComparisonViewStateReader.cs`、`TrackerDiagnosticsComparisonUiState.cs`、`TrackerSnapshotReplayReader.cs`、`TrackerPacketSnapshotRecord.cs`、`TrackedVisionViewState.cs`、`TrackerRenderSnapshotLogReader.cs`、関連 tests を確認。
  - `rg -n "TrackerSnapshotReplayReader|TrackerPacketSnapshotRecord|TrackerDiagnosticsComparison|SnapshotInput|RawPayload|Semantic|TrackedSnapshot|VisionFieldCanvas|DiagnosticsFieldViewFactory" Tracker/Tracker.Server Tracker/Tracker.Tests`
  - `git status --short`
- 対象ファイル
  - 読み取り: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - 読み取り: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - 読み取り: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
  - 読み取り: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
  - 読み取り: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - 読み取り: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - 読み取り: `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
  - 読み取り: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
  - 読み取り: `Tracker/Tracker.Server/Components/Vision/TrackedVisionViewState.cs`
  - 読み取り: `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs`
  - 読み取り: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
  - 読み取り: `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
  - 読み取り: `Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`
  - 変更: `reports/tracker-055-diagnostics-field-source-investigation-20260512233148.md`
- 1. DTO / mapper の有無
  - `VisionFieldCanvas` に渡す DTO は既存どおり `SSL_GeometryData`、`SSL_DetectionBall`、`SSL_DetectionRobot`。
  - ibis tracker output 側は `selectedRenderSnapshot.Frame` を `TrackedVisionViewState.FromSnapshot(...)` に通して `SSL_Detection*` へ変換している。該当箇所は `Diagnostics.razor.cs` の `trackedRenderView`。
  - Vision input 側は `DiagnosticsFieldViewFactory.CreateRawBalls/CreateRawYellowRobots/CreateRawBlueRobots` が `TrackerFrame.SourceDetections` を `SSL_Detection*` として返すだけで、3rdparty tracker snapshot 用 mapper はまだない。
  - tracker snapshot sidecar は `TrackerPacketSnapshotRecord.PayloadBase64` と `TrackerPacketSnapshotSemanticSummary` を持つ。`TrackerSnapshotReplayReader` は `ComparisonSource` に raw payload と semantic summary を残すが、現行 `TrackerDiagnosticsComparisonViewStateReader` は counts だけを `TrackerDiagnosticsComparisonEntryComparison` へ落としているため、描画用 object list は UI へ出ていない。
  - 最小追加箇所は `DiagnosticsFieldViewFactory` に `TrackerPacketSnapshotSemanticSummary` から `SSL_DetectionBall` / team 別 `SSL_DetectionRobot` を作る mapper を追加し、`TrackerDiagnosticsComparisonViewStateReader` 側で nearest snapshot の semantic summary を含む Field 用 view-state を返すこと。raw payload decode ではなく semantic summary を使うと、既存の fallback / source metadata と同じ経路で扱える。
- 2. 選択 entry / source filter から右 Field snapshot を選ぶ方法
  - 既存 comparison は `selectedEntry.TrackedFrame` から同 frame の `own` snapshot を基準にし、その `TrackedFrameTimestampNs` に最も近い candidate snapshot を選ぶ。`All` filter では non-own があれば own を除外する。
  - この nearest-timestamp 選択は右側 Field の 3rdparty 表示にも流用すべき。Field と comparison が別々に sidecar を読み、別々に nearest を選ぶと表示差分の根拠がずれる。
  - 現行 `TrackerDiagnosticsComparisonEntryComparison` には source label / frame / timestamp / counts しかないため、そのままでは描画できない。`ComparisonSnapshot` に保持している `SemanticSummary` を返す `SelectedFieldSnapshot` などの view-state を追加するか、nearest 選択関数を共通化して comparison summary と field snapshot の両方を作るのが最小。
  - `Own` 選択時は現行の `selectedRenderSnapshot` / `trackedRenderView` を使うのが自然。`External` / source label / unknown では snapshot sidecar の nearest snapshot を描画する。`All` は既存 comparison と同じく non-own 優先にするなら挙動は一貫するが、右 Field の初期値は「現在どおり ibisTracker」を維持するなら Field 用 selected filter は `Own` 初期値にするのが要件に近い。
- 3. UI 配置と折り畳み状態
  - source 切替 control は `Tracker Comparison` panel 内ではなく、右側 Field の見出し行に置くのが要件に合う。`Tracker Comparison` を折り畳んでも右 Field の主操作が残るため。
  - 現在の `Tracker Comparison` 内 select は `Diagnostics.razor` の comparison header にある。ここに置いたままだと panel collapse 時に右 Field 切替も隠れる。
  - 推奨 UI は `Tracker Output Field` 見出しを `Tracker Field` / `Tracker Field Source` のような header row にし、右 field source select を配置する。左 Field は `Vision Input Field` のままでよい。
  - comparison 折り畳みは `Diagnostics.razor.cs` に `private bool isComparisonExpanded = false;` または `true` を追加して持てば足りる。ユーザー要件が「邪魔」なので初期値は collapsed が妥当。toggle は comparison header の button で、expanded 時だけ stats/detail を描画する。source selector は Field 側へ移すため collapsed 初期でも操作不能にならない。
- 4. focused tests
  - `TrackerDiagnosticsComparisonViewStateTests` に、selected entry + source label から nearest snapshot の object summary / field view-state が返ることを追加するのが第一候補。既に source filter、displayed-entry selection、nearest-timestamp を検証している。
  - `TrackerDiagnosticsComparisonViewStateTests` の `UiState_SelectFilterValue_RecomputesComparisonForSourceLabelOption` 近辺に、source label 変更で Field 用 selected snapshot も同じ source / frame / timestamp / ball / robot positions に更新される test を足せる。
  - mapper は `DiagnosticsFieldViewFactory` に追加するなら、新規 `DiagnosticsFieldViewFactoryTests` を追加して ball mm、visibility、yellow/blue team split、unknown team の扱いを固定するのがよい。既存の `TrackedVisionViewStateTests` は `TrackerFrame` to viewer mapping 用であり、3rdparty snapshot summary mapper とは入力型が違う。
  - render snapshot sidecar 経由の own 表示維持は `TrackerRenderSnapshotLogReaderTests` が近いが、右 Field source 切替の主契約は snapshot sidecar / comparison reader 側なので、広げすぎない方がよい。
- 5. 1 task で閉じられるか
  - 1 task で閉じられる。必要な変更は diagnostics page state/UI、comparison reader view-state 拡張、field DTO mapper、focused tests に収まる。
  - 分割が必要になるのは、3rdparty snapshot の raw payload を `TrackerWrapperPacket` から full fidelity で再描画し、semantic summary fallback と raw decode failure の UI を別設計する場合。ただし今回の要件は「snapshot を下の Field に表示」であり、既存 semantic summary の balls/robots で正常系を先に閉じるのが最小。

## 推奨タスク境界

- TRACKER-055 は単一 task として、次の範囲で閉じるのを推奨。
  - `TrackerDiagnosticsComparisonViewStateReader` / `TrackerDiagnosticsComparisonUiState` に Field 用 selected snapshot view-state を追加し、既存 nearest-timestamp 選択を Field と comparison で共用する。
  - `DiagnosticsFieldViewFactory` に `TrackerPacketSnapshotSemanticSummary` から `VisionFieldCanvas` DTO を作る mapper を追加する。
  - `Diagnostics.razor(.cs/.css)` で右 Field source select を Field 見出し側に移し、`Tracker Comparison` は toggle で折り畳み可能にする。初期 collapsed を推奨。
  - focused tests は `TrackerDiagnosticsComparisonViewStateTests` と新規 `DiagnosticsFieldViewFactoryTests` を中心に追加する。
- 実装時に触る主ファイル候補
  - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
  - `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
  - `Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs` または同等の focused test file

## リスク

- semantic summary がない、または payload decode 不能な古い sidecar では object list が空になる。現行 `EnsureSemanticSummary` は fallback で source/frame metadata だけは作るが、ball/robot position は復元できない。
- `All` filter の扱いを Field 初期値に使うと、既存 comparison と同じ non-own 優先になり、現在の「右は ibisTracker」から初期表示が変わる可能性がある。Field source は `Own` 初期、comparison は必要に応じて同じ source を表示、という分離が安全。
- comparison panel 内の select を Field 側へ移すと、既存 comparison test の意味は保てるが UI 表示文言と CSS は変わる。component-level test がないため、focused unit test だけでは実画面の折り畳み/配置崩れは検出しにくい。
- 現在の worktree には TRACKER-054 由来と思われる未コミット変更が複数ある。TRACKER-055 実装時はそれらを revert / stage しないように注意が必要。
- 調査では build/test は実行していない。コード編集禁止のため、検証は静的確認と既存 test 構造の確認に留めた。
