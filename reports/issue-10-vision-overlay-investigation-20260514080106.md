# Sub-agent実行レポート

## タスク

- 目的: Issue #10「Vision画面にも左右分割＆オーバーレイモードがほしい」の既存実装影響範囲、設計前に確認すべき仕様、再利用可能な既存 diagnostics overlay 実装を調査する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指示により、調査は gpt-5.5 high の sub-agent に委譲し、親はマネージャーとして report を裁定する。

## 対象範囲

- 対象: `Tracker.Server` の Vision / diagnostics / Field 描画関連コード、Issue #10 に関係する設計・追跡ファイル、既存の左右分割・overlay 実装。

## 対象外

- 対象外: コード変更、テスト作成、設計書編集、PR本文更新、Issue #10 の仕様決定。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `rg -n "Issue #10|issue-10|Vision|overlay|diagnostics|左右分割|オーバーレイ" /home/ibis/.codex/memories/MEMORY.md`
  - `sed -n '1,220p' reports/issue-10-vision-overlay-investigation-20260514080106.md`
  - `git status --short`
  - `gh issue view 10 --repo ibis-ssl/Duck --json title,body,state,url,comments`
  - `rg --files Tracker/Tracker.Server | rg 'Vision|Diagnostics|Design|\\.razor|\\.cs|\\.css'`
  - `rg --files Tracker | rg 'tasks-status|phases-status|Design|diagnostics|vision|Vision|Diagnostics'`
  - `nl -ba /home/ibis/.codex/memories/MEMORY.md | sed -n '191,326p'`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Pages/Home.razor`
  - `sed -n '1,280p' Tracker/Tracker.Server/Components/Pages/Home.razor.css`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.css`
  - `sed -n '1,240p' Tracker/Tracker.Server/Components/Vision/VisionDetailsPanel.razor`
  - `sed -n '1,220p' Tracker/Tracker.Server/Components/Vision/VisionRenderOptions.cs`
  - `sed -n '220,520p' Tracker/Tracker.Server/Components/Pages/Home.razor`
  - `sed -n '1,280p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - `sed -n '1,320p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `sed -n '1,320p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Pages/DiagnosticsRenderLayoutState.cs`
  - `sed -n '280,620p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - `sed -n '320,760p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `sed -n '320,760p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
  - `sed -n '1,320p' Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor.css`
  - `sed -n '1,280p' Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayModels.cs`
  - `sed -n '1,320p' Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs`
  - `sed -n '1,320p' Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - `sed -n '1,280p' Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
  - `sed -n '1,260p' Tracker/Tracker.Tests/DiagnosticsRenderLayoutStateTests.cs`
  - `sed -n '1,320p' Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs`
  - `sed -n '320,760p' Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - `sed -n '1,280p' Tracker/Tracker.Server/Design/tasks-status.md`
  - `sed -n '1,240p' Tracker/Tracker.Server/Design/phases-status.md`
  - `sed -n '1,260p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `sed -n '1,280p' Tracker/Tracker.Server/Components/Vision/TrackedVisionViewState.cs`
  - `sed -n '1,220p' Tracker/Tracker.Server/Tracking/TrackedSnapshot.cs`
  - `sed -n '1,280p' Tracker/Tracker.Server/Vision/VisionPacketStore.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Vision/VisionPacketSnapshot.cs`
  - `rg -n "Overlay|Split|FieldDisplayMode|diagnostics-render|vision-grid|ViewerMode|Raw|Tracked|Layer A|Layer B" Tracker/Tracker.Server Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs Tracker/Tracker.Tests/DiagnosticsRenderLayoutStateTests.cs`
  - `sed -n '40,90p' Tracker/Tracker.Server/README.md`
  - `sed -n '220,236p' Tracker/Tracker.Server/README.md`
  - `sed -n '1,220p' Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`
  - `sed -n '1,180p' Tracker/Tracker.Server/Components/Vision/TrackedDetailsPanel.razor`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/issue-10-vision-overlay-investigation-20260514080106.md`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Home.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Home.razor.css`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.css`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionDetailsPanel.razor`
  - 確認: `Tracker/Tracker.Server/Components/Vision/TrackedDetailsPanel.razor`
  - 確認: `Tracker/Tracker.Server/Components/Vision/TrackedVisionViewState.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionPacketStore.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionPacketSnapshot.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsRenderLayoutState.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor.css`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayModels.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackedSnapshot.cs`
  - 確認: `Tracker/Tracker.Server/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.Server/Design/phases-status.md`
  - 確認: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - 確認: `Tracker/Tracker.Server/README.md`
  - 確認: `Tracker/Tracker.Tests/DiagnosticsRenderLayoutStateTests.cs`
  - 確認: `Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
  - ユーザー確認が必要: Vision 画面の overlay 対象を確定する必要がある。現状の `/` は `Raw` と `Tracked` の排他切替で、Issue 本文の「重ね合わせ描画」は `Raw Aggregate vs Tracked`、`Raw Camera vs Tracked`、`Raw Camera A vs Camera B`、または diagnostics と同様の任意 Layer A/B source selector のどれにも解釈できる。
  - ユーザー確認が必要: 左右分割の source selector をどの粒度にするか未確定。diagnostics は `Vision Input` / `ibis tracker` / 外部 tracker source を扱うが、live Vision 画面には外部 sidecar source がなく、現実的な候補は `Raw Aggregate`、`Raw Camera <id>`、`Tracked` に限られる。
  - ユーザー確認が必要: overlay 時の時刻合わせを `latest raw` と `latest tracked` の単純重ね合わせでよいか、または raw packet と tracker frame の timestamp 対応を表示・警告する必要があるかで成果物の意味が変わる。現状の live store は raw と tracked を独立に 100ms polling しており、diagnostics の alignment sidecar 相当はない。
  - ユーザー確認が必要: overlay の geometry 基準を確定する必要がある。raw geometry、tracked geometry、fallback geometry のどれを基準にするかで、重ね合わせ位置と empty state が変わる。
  - ユーザー確認が必要: details panel を split/overlay でどう扱うか未確定。既存は Raw 用 `VisionDetailsPanel` と Tracked 用 `TrackedDetailsPanel` が別物で、左右 split 時に両方を下部/右側へ並べるか、選択 source 片側だけを詳細表示するか、overlay legend のみにするかで UI 密度が変わる。
  - ユーザー確認が必要: overlay layer の見た目を diagnostics と同じ Layer A/B accent stroke にするか、raw/tracked の意味が分かる専用色・凡例へ変えるか未確定。既存 team color と accent stroke を併用すると視認性は保てるが、色の意味が増える。

## 結果

- 結果:
  - Issue #10 は open、title は `Vison画面にも左右分割＆オーバーレイモードがほしい`、body は `重ね合わせ描画してほしい。`、コメントは 0 件だった。
  - 既存 Vision 画面は `Home.razor` が `VisionPacketStore` と `TrackedSnapshotStore` を 100ms 間隔で読み、`ViewerMode.Raw` / `ViewerMode.Tracked` の排他切替で 1 枚の `VisionFieldCanvas` と 1 つの details panel を出す構造。CSS は `.vision-grid` で field と details の固定 2 カラムを構成しており、左右 field 分割や split/overlay 表示 mode はまだない。
  - raw 側は `VisionPacketStore` が camera ごとの latest detection と aggregate detection を保持し、`ResolveSelectedView` で aggregate/camera の ball/robot list を `VisionFieldCanvas` に渡している。camera 選択資産は再利用できる。
  - tracked 側は `TrackedVisionViewState.FromSnapshot` が `TrackerFrame` を `SSL_DetectionBall` / `SSL_DetectionRobot` / `SSL_GeometryData` へ変換済みで、Vision overlay 用 DTO として再利用しやすい。
  - `VisionFieldCanvas` は geometry、balls、yellow robots、blue robots を受け取る汎用 field component で、split 表示ならほぼそのまま 2 枚並べられる。ただし overlay component としては単一 data set 前提で、layer visibility、legend、layer accent、複数 source status は持っていない。
  - diagnostics 側は `TrackerDiagnosticsComparisonUiState` が `Split` / `Overlay` mode、左右 Field source、Layer A/B visibility を管理し、`DiagnosticsFieldOverlayRenderModelFactory` と `DiagnosticsFieldOverlayCanvas` が重ね合わせ描画を実装済み。UI pattern、layer model、legend checkbox、accent stroke、同一 source の Layer A/B 統合、missing layer を空にして ready layer を残す考え方は再利用可能。
  - diagnostics の左右/高さ調整は `DiagnosticsRenderLayoutState` が rem clamp と CSS custom property を作り、`Diagnostics.razor.cs` の mouse/key handler と `Diagnostics.razor.css` の resizer で実現している。Vision へは同じ clamp-and-drag 方針を採れるが、名前は diagnostics 専用なので、そのまま依存するより Vision 用 state か共通化を設計で決めるのがよい。
  - 確認不要で選べる方針候補: 実装範囲は `Tracker.Server` に閉じる。`Tracker.Core` には raw vision / overlay UI 処理を入れない。既存 `VisionFieldCanvas` / `VisionFieldLines` / marker component / `TrackedVisionViewState` を再利用し、diagnostics sidecar 専用 reader には依存しない。
  - 確認不要で選べる方針候補: 設計書と tracking は先に `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`、`tasks-status.md`、`phases-status.md` を更新する。現在の tracker は RAW-VISION-012 完了で止まっており、Issue #10 用の新 task はまだ存在しない。
  - 設計/TDD前の推奨タスク分割案: 1) Issue #10 の仕様確定と設計・追跡更新、2) Vision split/overlay 用 view-state と layout state の単体テスト追加、3) Vision field split と draggable divider の実装、4) live Vision overlay model/component の実装と overlay model tests、5) README/manual evidence と task-scoped review。仕様が `Raw vs Tracked` 固定でよい場合は 2-4 を小さめに統合できるが、任意 source selector まで入れるなら分けるべき。
  - 今回は調査のみの指示のため、コード変更、設計書編集、テスト作成、build/test 実行はしていない。

## リスク

- 未解決のリスクまたは後続対応:
  - overlay 対象を未確認のまま設計すると、`Raw/Tracked` 排他切替の延長で済むのか、diagnostics と同等の任意 source selector が必要なのかが後戻りになる。
  - `latest raw` と `latest tracked` を単純に重ねると、見た目は実装できても時刻ずれを比較結果の差分と誤認する可能性がある。live Vision に alignment を入れるか、時刻差表示だけに留めるかの判断が必要。
  - diagnostics overlay component をそのまま持ち込むと、Vision 既存の zoom / pan / axis / cursor overlay が失われる可能性がある。再利用は model / marker / legend 方針中心にして、操作性は `VisionFieldCanvas` 側と合わせる設計が必要。
  - split で field を 2 枚表示し details も残す場合、4K では有効でも通常 desktop / mobile で密度が高くなる。draggable splitter、details collapse、responsive stacking のどれを採るかを設計で固定しないと UI が破綻しやすい。
  - geometry 基準が未決のまま overlay を作ると、raw と tracked の field dimensions が異なる capture/profile で位置比較の意味が曖昧になる。
  - `Tracker/Tracker.Server/appsettings.json` に既存の未コミット変更がある。今回の調査では触れていないが、後続実装時も unrelated change として保護する必要がある。
