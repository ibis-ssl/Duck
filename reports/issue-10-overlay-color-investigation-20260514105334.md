# Sub-agent実行レポート

## タスク

Issue #10 Vision overlay の Layer A/B 色分け調査。

## sub-agentを使う理由

diagnostics overlay の既存実装と Vision overlay の現状を、親の実装判断から分離して確認するため。

## 対象範囲

`Tracker.Server` の diagnostics overlay 色分け、Vision overlay 表示、関連 view-state / test。

## 対象外

製品コードの変更、テスト変更、`Tracker/Tracker.Server/appsettings.json`。

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `sed -n '1,260p' reports/issue-10-overlay-color-investigation-20260514105334.md`
- `rg -n "diagnostics overlay|VisionLiveComparison|Vision overlay|DiagnosticsFieldOverlay|overlay color|Layer A|Layer B|issue-10" /home/ibis/.codex/memories/MEMORY.md`
- `rg -n "DiagnosticsFieldOverlay|VisionLiveComparison|Layer A|Layer B|overlay" Tracker reports -g '!Tracker/Tracker.Server/appsettings.json'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor | sed -n '1,240p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayModels.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css | sed -n '1,260p'`
- `rg -n "diagnostics-overlay-field|overlay-field|layer-group|swatch|LayerAAccent|AccentColor|Layer A|Layer B" Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css Tracker/Tracker.Server/Components/Pages/Home.razor Tracker/Tracker.Server/Components/Pages/Home.razor.css Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor | sed -n '1,360p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor | sed -n '360,760p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor.css | sed -n '1,420p'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs | sed -n '1,420p'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs | sed -n '420,760p'`
- `nl -ba Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs | sed -n '1,380p'`
- `rg -n "AccentColor|OverlayLayerAccentColor|layer-group|Layer A/B|CreateOverlayRenderModel|DiagnosticsFieldOverlay" Tracker/Tracker.Tests Tracker/Tracker.Server/Tracking Tracker/Tracker.Server/Components/Pages -g '!Tracker/Tracker.Server/appsettings.json'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor.css | sed -n '1,180p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '130,210p;540,590p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '500,560p'`
- `nl -ba Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs | sed -n '60,190p'`
- `rg -n "public .*VisionFieldCanvas|MarkerStroke|VisionRobotMarker|VisionBallMarker|Style" Tracker/Tracker.Server/Components/Vision`
- `nl -ba Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Components/Vision/VisionRobotMarker.razor | sed -n '1,180p'`
- `nl -ba Tracker/Tracker.Server/Components/Vision/VisionBallMarker.razor | sed -n '1,120p'`
- `dotnet build` / `dotnet test` は未実行。今回は調査レポートのみで、製品コード・テスト変更は禁止のため。

## 対象ファイル

- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayModels.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor.css`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/Home.razor`
- 確認: `Tracker/Tracker.Server/Components/Pages/Home.razor.css`
- 確認: `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs`
- 確認: `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
- 確認: `Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs`
- 確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
- 確認: `Tracker/Tracker.Server/Components/Vision/VisionRobotMarker.razor`
- 確認: `Tracker/Tracker.Server/Components/Vision/VisionBallMarker.razor`
- 対象外維持: `Tracker/Tracker.Server/appsettings.json`

## 指摘事項

- diagnostics overlay の Layer A/B 色分けは、model に `AccentColor` を持たせ、component が marker stroke / ball style / legend swatch へ直接渡す構造で実現されている。`DiagnosticsFieldOverlayLayerRenderModel.AccentColor` は `DiagnosticsFieldOverlayModels.cs:27-40` で定義され、`DiagnosticsFieldOverlayRenderModelFactory.cs:11-12` の `LayerAAccentColor = "#68d8ff"`、`LayerBAccentColor = "#ff7ad9"` を `OverlayLayerAccentColor()` 経由で layer ごとに設定する。`DiagnosticsFieldOverlayCanvas.razor:17` は swatch、同 `:78` は ball stroke style、同 `:86` / `:94` は robot marker stroke に `layer.AccentColor` を渡している。
- diagnostics overlay の CSS 側は主に透明度と凡例表示を担当している。`DiagnosticsFieldOverlayCanvas.razor.css:81-85` は swatch 形状、同 `:103-109` は Layer A/B group の opacity を分ける。色そのものは CSS class ではなく render model の `AccentColor` が source of truth。指定された `Diagnostics.razor.css` にはこの layer 色定義はなく、diagnostics ページ全体の layout / theme が中心だった。
- diagnostics overlay の layer source は `TrackerDiagnosticsComparisonUiState.CreateOverlayLayerSources()` で作られる。通常は `Layer A` と `Layer B` の 2 layer、同一 source の場合は `Layer A/B` 1 layer に畳み `LegendNote = "same source"` を付ける。`Diagnostics.razor.cs:523-545` はこの source list を `DiagnosticsFieldOverlayRenderModelFactory.Create()` に渡している。
- Vision overlay は `Home.razor:163-173` で `ComparisonLayers` を回し、各 layer を絶対配置した `div` の中に通常の `VisionFieldCanvas` を丸ごと描画している。`Home.razor.css:217-238` は Layer A/B の stacking、Layer B の opacity / `mix-blend-mode: screen` だけを変えている。legend も `Home.razor:181-188` / `Home.razor.css:248-260` で text badge を表示するだけで、layer swatch や marker stroke 色はない。
- Vision の view-state model には layer 色を運ぶプロパティがない。`VisionLiveComparisonLayer` / `VisionLiveComparisonLegendItem` は `LayerName`、`Status`、`IsVisible`、`IsSameSourceCollapsed`、`SourceLabel`、metadata を持つが、`AccentColor` 相当を持たない。`VisionLiveComparisonViewState.CreateLayer()` も色を設定していない。
- Vision の描画 component も layer 色を受け取れない。`VisionFieldCanvas.razor:70-88` は `VisionBallMarker` / `VisionRobotMarker` を既定設定で描画する。`VisionRobotMarker.razor:52` は `MarkerStroke` parameter を持つが `VisionFieldCanvas` から渡されていない。`VisionBallMarker.razor:18` は `Style` parameter を持つが `VisionFieldCanvas` から渡されていない。このため overlay で複数 layer を重ねても、各 layer の marker は既定色のままになり、Layer A/B が同色に見える。
- 現行テスト `VisionLiveComparisonViewStateTests.cs` は source option、same render tick、same-source collapse、missing layer 維持、visibility、legend/details metadata を固定しているが、Layer A/B の `AccentColor` や legend swatch 用の色は固定していない。diagnostics 側も `DiagnosticsFieldViewFactoryTests.cs` は missing layer / geometry empty / legend note を確認しているが、accent color の値検証は現状ない。

## 結果

- 結論: Vision overlay が Layer A/B 同色になる主因は、diagnostics overlay と異なり `AccentColor` を view-state / render model で保持せず、`Home.razor` が layer ごとの色を `VisionFieldCanvas` / marker component へ渡していないこと。現状の CSS は Layer B の透明度と blend mode だけを変えるため、diagnostics のような明示的な水色 / ピンクの識別にはならない。
- 最小実装方針: diagnostics の色 contract を流用し、Vision comparison layer model に `AccentColor` を追加する。候補は `VisionLiveComparisonLayer` と `VisionLiveComparisonLegendItem` に `AccentColor` を追加し、`VisionLiveComparisonViewState.CreateLayer()` で `Layer A = "#68d8ff"`、`Layer B = "#ff7ad9"`、same-source collapsed `Layer A/B = "#68d8ff"` を設定する。色定数は `VisionLiveComparisonViewState` 内に置くか、diagnostics と共有する小さな palette helper に切り出す。
- 最小 UI 実装: `VisionFieldCanvas` に任意 parameter `MarkerStroke` / `BallStyle` または `MarkerAccentColor` を追加し、`VisionBallMarker Style` と `VisionRobotMarker MarkerStroke` へ渡す。既存 Raw / Tracked 単体表示は parameter 未指定で既定色を維持し、`Home.razor` の Compare overlay だけ `layer.AccentColor` を渡す。legend には diagnostics と同じく swatch を追加し、`Home.razor.css` に `.vision-comparison-overlay-legend__swatch` 程度を追加する。
- 代替案: diagnostics の `DiagnosticsFieldOverlayCanvas` と同様に Vision overlay 専用 canvas を作り、1 つの SVG に Layer A/B をまとめて描く。この方が重複する field 背景や zoom/pan の状態差を避けやすいが、Issue #10 の色分けだけなら `VisionFieldCanvas` parameter 追加の方が小さい。
- TDD で固定すべき期待: `VisionLiveComparisonViewStateTests.cs` に、`CreateOverlayLayers()` が異なる source で `Layer A` は `#68d8ff`、`Layer B` は `#ff7ad9` を返すこと、same-source collapsed `Layer A/B` は 1 layer かつ `#68d8ff` を返すこと、missing layer でも `AccentColor` が失われないこと、`LegendItems` が同じ色を保持することを追加する。
- TDD で追加検討すべき期待: component bUnit 等が既存 test stack にある場合は、Compare overlay の `Home.razor` が `VisionFieldCanvas` に `MarkerStroke` / `BallStyle` を layer 色で渡すこと、legend swatch の inline style または class が `AccentColor` を反映することを固定する。bUnit が無い場合は view-state contract test を先に Red にし、UI 側は markup / CSS review で補う。

## リスク

- `VisionFieldCanvas` は Raw / Tracked 単体表示でも使われる共通 component のため、parameter 追加時は既定値を維持しないと既存画面の marker 色が変わる。
- Vision overlay は現在、複数の `VisionFieldCanvas` を絶対配置しており、各 canvas が field 背景・線・zoom/pan JS を個別に持つ。色だけを足しても、diagnostics overlay のような「1 つの SVG 内で layer を重ねる」構造とは一致しない。
- CSS の `mix-blend-mode: screen` と `opacity: 0.68` は追加する accent color の見え方を変える可能性がある。特にピンク系 Layer B の視認性は実画面確認が必要。
- 今回は調査のみで `dotnet test` / UI 実画面確認は未実行。提案実装後は `VisionLiveComparisonViewStateTests` の focused test と `Tracker.Server` build、可能ならブラウザで Compare overlay の凡例・marker 色確認が必要。
