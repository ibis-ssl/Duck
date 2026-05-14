# Sub-agent実行レポート

## タスク

Issue #10 overlay drag sync の Vision / diagnostics 横断調査。

## sub-agentを使う理由

drag 時に片方の layer だけ表示が変わる問題が Vision overlay 固有か diagnostics overlay にも存在するかを、実装前に切り分けるため。

## 対象範囲

Vision overlay と diagnostics overlay の drag / pan / zoom state、layer 同期、関連 component / tests。

## 対象外

製品コード変更、テスト変更、`Tracker/Tracker.Server/appsettings.json`、PR draft 解除。

## 実行コマンド

- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `sed -n '1,260p' reports/issue-10-overlay-drag-sync-investigation-20260514111008.md`
- `rg -n "overlay|drag|diagnostics|VisionFieldCanvas|DiagnosticsFieldOverlayCanvas|Issue #10|issue-10" /home/ibis/.codex/memories/MEMORY.md`
- `rg -n "drag|pan|zoom|wheel|pointer|mousedown|mousemove|mouseup|Transform|translate|scale|viewBox|overlay|layer|FieldCanvas|DiagnosticsFieldOverlayCanvas|VisionFieldCanvas" Tracker/Tracker.Server Tracker/Tracker.Tests`
- `rg --files Tracker/Tracker.Server/Components Tracker/Tracker.Tests | rg "(Vision|Diagnostics|Overlay|Field|Canvas|Home)"`
- `git status --short`
- `nl -ba Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor | sed -n '1,380p'`
- `nl -ba Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.js | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.css | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor | sed -n '1,620p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor.css | sed -n '1,620p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor | sed -n '1,360p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor.css | sed -n '1,320p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayModels.cs | sed -n '1,320p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs | sed -n '1,360p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor | sed -n '1,760p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '1,980p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css | sed -n '1,840p'`
- `rg -n "zoom|pan|drag|translation|Transform|VisionFieldCanvas|DiagnosticsFieldOverlayCanvas|FieldDisplayMode|CreateOverlayLayers|CreateOverlayLayerSources|Overlay" Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs Tracker/Tracker.Tests/DiagnosticsRenderLayoutStateTests.cs Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs Tracker/Tracker.Tests/VisionFieldProjectionTests.cs`
- `nl -ba Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs | sed -n '1,460p'`
- `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs | sed -n '680,840p'`
- `sed -n '1,220p' Tracker/Tracker.Tests/Tracker.Tests.csproj`
- `sed -n '1,220p' Directory.Packages.props`
- `nl -ba Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs | sed -n '1,160p'`
- `nl -ba Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs | sed -n '1,220p'`

`dotnet test` は未実行。今回は実装前調査であり、製品コード・テストを変更していないため、既存テスト実行よりも state / markup / CSS の直接確認を優先した。

## 対象ファイル

- `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
- `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.js`
- `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.css`
- `Tracker/Tracker.Server/Components/Pages/Home.razor`
- `Tracker/Tracker.Server/Components/Pages/Home.razor.css`
- `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs`
- `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor`
- `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor.css`
- `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayModels.cs`
- `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
- `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
- `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- `Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs`
- `Tracker/Tracker.Tests/DiagnosticsRenderLayoutStateTests.cs`
- `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
- `Tracker/Tracker.Tests/Tracker.Tests.csproj`
- `Directory.Packages.props`

## 指摘事項

- Vision overlay の直接原因は、`Home.razor` の overlay mode が `vision-comparison-overlay-stack` 内に layer ごとの `VisionFieldCanvas` を絶対配置で複数重ねていること。各 `VisionFieldCanvas` は `zoom`、`translationX/Y`、`activeTranslationX/Y`、`mouseDownPoint` を private field として保持し、`@onwheel` / `@onmousedown` / `@onmousemove` / `@onmouseup` を各 SVG に直接持つ。したがって drag / zoom はイベントを受けた 1 component instance だけに反映され、もう片方の layer instance には伝播しない。
- CSS 上も `Home.razor.css` は `.vision-comparison-overlay-layer` を `position: absolute; inset: 0;` で重ね、Layer B を `z-index: 2`、Layer A を `z-index: 1` にしている。Layer B が visible の場合、通常は Layer B 側の `VisionFieldCanvas` だけが pointer event を受けて pan / zoom state を更新する。Layer A は同じ見た目の field に重なっていても別 instance の state のまま残る。
- `VisionFieldCanvas.razor` の transform は `FieldTransform(projection)` で 1 つの `<g>` にだけ適用される。overlay で複数 instance を重ねる設計では、各 layer の transform が独立してしまう。`VisionLiveComparisonViewState` は layer source、visibility、accent、snapshot を扱うが、viewport / drag / pan / zoom state は持っていない。
- diagnostics overlay には同種の layer 間 drag sync 問題は確認できなかった。`DiagnosticsFieldOverlayCanvas.razor` は overlay mode でも単一の `<svg>` / 単一の field 描画の中に `Model.Layers` を `<g class=...>` として描く構造で、Layer A/B ごとの `VisionFieldCanvas` instance を重ねていない。現状、この overlay component 自体には pan / zoom / drag viewport state が実装されていないため、「片方の layer だけ pan / zoom する」状態は発生しない。
- diagnostics の split mode は左右それぞれに `VisionFieldCanvas` を置くため、左右 field の pan / zoom は独立する。ただしこれは overlay layer 同期ではなく split view の独立 viewport と見なせる。diagnostics overlay に今後 pan / zoom を追加する場合は、現在の単一 SVG 構造を維持する限り layer 間同期問題は避けられる。
- 既存テストは Vision / diagnostics ともに overlay source、same-source collapse、visibility、missing layer、accent color、sidecar reload 回避を固定しているが、drag / pan / zoom viewport state の共有・同期を固定するテストは見当たらない。`Tracker.Tests` は bUnit を導入しておらず、既存方針は view-state helper の unit test と Razor markup のファイル検査が中心。

## 結果

- 推奨する最小修正は、Vision overlay を「layer ごとに `VisionFieldCanvas` を重ねる」構造から外すこと。diagnostics と同じ考え方で、overlay 専用の単一 canvas component を作り、field / boundary / geometry を 1 回だけ描画し、その下に Layer A/B の balls / robots を layer group として描くのが最も事故が少ない。これなら pan / zoom state は 1 component に閉じ、layer 間同期が構造上崩れない。
- 既存 `VisionFieldCanvas` を流用する場合の代替最小案は、`VisionFieldCanvas` の viewport state を private field から外部注入可能な shared state に切り出し、overlay 親が 1 つの state を保持して Layer A/B の両 instance に渡すこと。ただし toolbar / cursor overlay / reset button も layer ごとに重なるため、根本的には overlay 専用 single SVG の方が diagnostics と揃い、UI の重なりも減る。
- TDD で最初に固定すべき期待は、Vision overlay が layer ごとの独立 viewport を持たないこと。候補は `VisionFieldViewportState` のような UI 非依存 helper を追加し、drag move / wheel / reset の state 遷移を unit test で固定すること。そのうえで overlay component または markup contract test で、overlay mode が複数の独立 `VisionFieldCanvas` を重ねず、単一 viewport state / 単一 overlay canvas で Layer A/B を描画することを固定する。
- 追加で固定すべき期待は、same-source collapse、missing layer、visibility off の既存挙動を壊さず、visible な ready layer だけが同じ viewport transform 配下で描画されること。既存の `VisionLiveComparisonViewStateTests` の source / visibility contract と、新規 overlay render model / viewport helper test を組み合わせるのが低コスト。
- diagnostics 側の TDD は、現時点では pan / zoom 同期バグ修正対象ではなく regression guard が中心。`DiagnosticsFieldOverlayCanvas.razor` が単一 overlay canvas と layer group 構造を維持すること、または将来 pan / zoom を入れる場合に viewport state が layer ではなく overlay canvas 単位で 1 つだけであることを file / helper test で固定するのがよい。

## リスク

- Vision overlay を単一 canvas 化する場合、`VisionFieldCanvas` の cursor coordinate 表示、Reset button、ResizeObserver による canvas size 取得、marker stroke / opacity の見え方を移植し忘れるリスクがある。
- `VisionFieldCanvas` の外部 shared state 化で済ませる場合、重なった toolbar / cursor / reset の pointer event と z-index が残り、表示上は直っても UI 操作面の不整合が残る可能性がある。
- diagnostics overlay は現状 pan / zoom を持たないため同種不具合はないが、将来 `VisionFieldCanvas` を diagnostics overlay に再利用して layer ごとに重ねる変更が入ると同じ問題を再発させる。
- 今回は実装前調査のみでブラウザ実機操作・Playwright 画面確認は未実施。pointer event の到達 layer はコードと CSS からの推定だが、Layer B が上に重なり各 `VisionFieldCanvas` が独立 state を持つ点はコード上確認済み。
- `git status --short` では既存 unrelated diff として `Tracker/Tracker.Server/appsettings.json` が変更状態、調査レポートが untracked。`appsettings.json` には触れていない。
