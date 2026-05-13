# Sub-agent実行レポート

## タスク

- 目的: TRACKER-056 diagnostics Field source切替とTracker Comparison折り畳みの設計具体化
- タスク種別: design

## sub-agentを使う理由

- 理由: ユーザー指示により、設計は gpt-5.5 high のsub-agentに任せる。TRACKER-056はユーザー可視UI挙動とField表示モデルを変更するため、実装前に設計を確定する。

## 対象範囲

- 対象: `Tracker Comparison` panel の折り畳み、左右 Field の source selector、`Vision Input` / ibis own / external / unknown / source label の Field描画モデル、TRACKER-055 cache / playback性能を壊さない実装方針。

## 対象外

- 対象外: TRACKER-057のField重ね合わせ表示の実装、backend receiver設定変更、TRACKER-055 cache再設計、コード実装。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,260p' reports/tracker-056-field-source-toggle-design-20260513010250.md`
  - `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
  - `sed -n '1,220p' Tracker/Tracker.Core/Design/phases-status.md`
  - `rg -n "TRACKER-055|TRACKER-056|Diagnostics|Tracker Comparison|Comparison|Field|source|overlay|scrub|playback|sidecar|cache|index" Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `sed -n '1,190p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `sed -n '1,260p' reports/tracker-055-diagnostics-field-source-investigation-20260512233148.md`
  - `sed -n '1,260p' reports/tracker-055-playback-scrub-performance-implementation-20260513001906.md`
  - `rg --files Tracker/Tracker.Server/Components | rg 'VisionFieldCanvas|Diagnostics\\.razor'`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - `sed -n '1,320p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `sed -n '1,280p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
  - `sed -n '1,240p' Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.css`
  - `sed -n '1,220p' Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.js`
  - `sed -n '320,760p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `sed -n '240,620p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - `sed -n '280,760p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
  - `sed -n '1,360p' Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - `sed -n '360,820p' Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - `sed -n '820,1120p' Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - `sed -n '1,300p' Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
  - `git status --short`
  - `git diff --check`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - 変更: `Tracker/Tracker.Core/Design/tasks-status.md`
  - 変更: `Tracker/Tracker.Core/Design/phases-status.md`
  - 変更: `reports/tracker-056-field-source-toggle-design-20260513010250.md`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.css`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.js`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
  - 確認: `reports/tracker-055-diagnostics-field-source-investigation-20260512233148.md`
  - 確認: `reports/tracker-055-playback-scrub-performance-implementation-20260513001906.md`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。設計上の主な注意点は、Field source に `All` を含めないこと、左右 selector を `Tracker Comparison` panel 外へ出すこと、tracker source Field data を TRACKER-055 の cached index から作ること。

## 結果

- 結果:
  - `tracker-server-cli-ui-detail-design.md` に `diagnostics Field source 切替` 節を追加し、TRACKER-056 のUI単位、状態保存、view-state/API shape、source filter、missing時表示、TRACKER-057への再利用境界、focused test観点を確定した。
  - Field source selector は左右 Field の見出し行に置く。`Tracker Comparison` panel は header toggle で折り畳み可能にし、折り畳み中も selector と Field 描画は使える。
  - selector 状態は `Diagnostics.razor.cs` の page state に保持する。query string、session storage、local storage には保存しない。log file 変更時は左 `Vision Input` / 右 ibis tracker output に戻し、scrub / playback tick では選択状態を維持する。
  - Field source option は `Vision Input`、ibis tracker、`External`、`Unknown`、source label とする。`All` は Field source として曖昧なため使わず、comparison panel の数値比較 filter にだけ残す。
  - ibis tracker output は既存 render snapshot sidecar の `TrackedVisionViewState` を使い、tracker packet snapshot sidecar の `own` record がない capture でも現行右 Field 表示を維持する。
  - external / unknown / source label は selected diagnostics entry の tracked frame から ibis own snapshot timestamp を求め、同じ cached index 内の nearest timestamp selection で `TrackerDiagnosticsFieldSourceFrame` を作る。
  - `TrackerDiagnosticsFieldSourceFrame` は side、source kind、status、source role / label、matching rule、baseline timestamp、nearest frame / timestamp / delta、raw payload restored、semantic summary または同等 projection を持つ最小 model とする。
  - TRACKER-055 の cache / index 経路を壊さないため、scrub / playback tick / selector変更で sidecar JSONL 全体を再読込しない。Field 用 index には raw payload 全体ではなく semantic summary または最小 projection を保持する。
  - tracker source の Field は `TrackerPacketSnapshotSemanticSummary` を `DiagnosticsFieldViewFactory` で `SSL_DetectionBall` / yellow・blue別 `SSL_DetectionRobot` に変換し、geometry は選択中 render snapshot の geometry を使う。
  - missing / empty / error は Field 領域を消さず status と empty Field で表示する。metadata missing、sidecar not-created、sidecar missing、sidecar empty、sidecar corrupt、own baseline missing、candidate missing、drawable objects empty を区別する。
  - TRACKER-057 overlay は対象外。ただし `TrackerDiagnosticsFieldSourceFrame` を単一sourceの描画入力として独立させ、後続 overlay renderer に再利用できるようにする。
  - `tasks-status.md` と `phases-status.md` の TRACKER-056 記述を、設計確定内容と report 参照に合わせて最小更新した。
  - `git diff --check` は問題なし。

## リスク

- 未解決のリスクまたは後続対応:
  - `TrackerDiagnosticsComparisonViewStateReader` の index は現在 counts 中心の projection を保持している。TRACKER-056 実装では semantic summary または描画用 projection を追加するため、巨大 sidecar での memory 増加を focused test / review で確認する必要がある。
  - 古い sidecar や手書き record で `SemanticSummary` がなく payload decode もできない場合、source metadata と frame metadata だけ残り、Field 上の ball / robot は空になる。この状態は error ではなく drawable objects empty として扱う。
  - tracker source sidecar は geometry を持たないため、render snapshot がない selected entry では tracker source Field も geometry なしの empty state になる。
  - component-level UI test がないため、comparison panel 折り畳みと Field header selector の実画面崩れは review / manual evidence で補う必要がある。
  - 今回は設計のみ。コード実装、dotnet build、dotnet test、sub-agent起動、nested Codex は実施していない。
