# Sub-agent実行レポート

## タスク

- 目的: TRACKER-057 diagnostics Field重ね合わせ表示の設計具体化
- タスク種別: design

## sub-agentを使う理由

- 理由: ユーザー指示により設計は gpt-5.5 high のsub-agentに任せる。TRACKER-057はwant扱いだが固定残タスクに含まれ、TRACKER-056のField source modelを再利用できる範囲で実装可否と最小設計を先に確定する必要がある。

## 対象範囲

- 対象: diagnostics Field overlay の最小仕様、TRACKER-056単一Field source modelの再利用、source色分け/legend/visibility、実装する場合のtest/review観点、複雑化する場合のdefer判断。

## 対象外

- 対象外: overlay実装、PR ready化、receiver endpoint変更、TRACKER-056の再設計。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
  - `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,240p' reports/tracker-057-field-overlay-design-20260513014926.md`
  - `rg -n "TRACKER-057|Field overlay|重ね合わせ|field source|TRACKER-056" /home/ibis/.codex/memories/MEMORY.md`
  - `sed -n '1,240p' Tracker/Tracker.Core/Design/tasks-status.md`
  - `sed -n '1,220p' Tracker/Tracker.Core/Design/phases-status.md`
  - `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `sed -n '1,240p' reports/tracker-056-field-source-toggle-design-20260513010250.md`
  - `sed -n '1,240p' reports/tracker-056-field-source-toggle-implementation-20260513011324.md`
  - `sed -n '1,220p' reports/tracker-056-review-20260513013805.md`
  - `sed -n '1,620p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - `sed -n '1,620p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `sed -n '1,760p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.css`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.js`
  - `sed -n '1,340p' Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - `sed -n '1,1280p' Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - `sed -n '1,240p' Tracker/Tracker.Server/Components/Vision/VisionBallMarker.razor`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Vision/VisionRobotMarker.razor`
  - `sed -n '1,120p' Tracker/Tracker.Server/Components/Vision/VisionBallMarker.razor.css`
  - `sed -n '1,140p' Tracker/Tracker.Server/Components/Vision/VisionRobotMarker.razor.css`
  - `sed -n '1,220p' Tracker/Tracker.Server/Components/Vision/VisionPalette.cs`
  - `sed -n '1,220p' Tracker/Tracker.Server/Components/Vision/VisionRenderOptions.cs`
  - `rg -n "TRACKER-057|重ね合わせ|overlay|Field source 切替|後続タスクへの固定事項|完了条件" Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `git status --short`
  - `git diff --check`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - 変更: `Tracker/Tracker.Core/Design/tasks-status.md`
  - 変更: `Tracker/Tracker.Core/Design/phases-status.md`
  - 変更: `reports/tracker-057-field-overlay-design-20260513014926.md`
  - 確認: `reports/tracker-056-field-source-toggle-design-20260513010250.md`
  - 確認: `reports/tracker-056-field-source-toggle-implementation-20260513011324.md`
  - 確認: `reports/tracker-056-review-20260513013805.md`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.css`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor.js`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionBallMarker.razor`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionRobotMarker.razor`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionBallMarker.razor.css`
  - 確認: `Tracker/Tracker.Server/Components/Vision/VisionRobotMarker.razor.css`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。
  - TRACKER-057 は defer ではなく最小実装可能と判断する。ただし任意個数 source overlay、overlay source 専用 multi-select、表示設定の永続化、receiver / metadata schema 変更はこの PR では対象外とする。

## 結果

- 結果:
  - `tracker-server-cli-ui-detail-design.md` に `diagnostics Field 重ね合わせ表示` 節を追加し、UI配置、source選択、色分け、legend、visibility、component方針、TRACKER-056 model再利用、missing系status、focused tests、実装対象/非対象を具体化した。
  - overlay mode の UI は Field 表示領域の見出し行に置く。表示 mode は `Split` / `Overlay` の二択とし、`Tracker Comparison` panel の折り畳みとは独立させる。
  - 左右 Field source selector は維持する。`Overlay` では現在の左 selector を `Layer A`、右 selector を `Layer B` として同一 Field に重ねる。overlay 専用の source list は作らず、Field source に `All` は追加しない。
  - overlay 対象 source は `Vision Input`、ibis tracker、`External`、`Unknown`、source label のうち左右 selector で選ばれた 2 source とする。既定 overlay は左 `Vision Input` / 右 ibis tracker output。左右が同じ source の場合は 1 layer として扱い、legend に同一 source であることを表示する。
  - 色分けは source layer 識別用に限定し、yellow / blue team の意味は維持する。最小仕様では `Layer A` を cyan 系、`Layer B` を magenta 系とし、robot body の team fill は残したまま stroke / ring / label / 破線または opacity で source を区別する。
  - legend は overlay Field の近くに表示し、各 layer の表示名、source role / label、status、nearest timestamp delta、record count または drawable count を最小限表示する。
  - visibility は legend 内の layer ごとの checkbox / toggle で制御し、既定は両 layer visible とする。state は `Diagnostics.razor.cs` の page state に保持し、query string / session storage / local storage には保存しない。log file 変更時は両 visible に戻し、scrub / playback tick / source selector 変更では維持する。
  - `VisionFieldCanvas` は single source 汎用 component として維持し、overlay は diagnostics 用の別 component を追加する方針にした。新 component は `VisionFieldProjection`、`VisionFieldLines`、`VisionRenderOptions`、既存 geometry DTO を再利用する。必要なら `VisionBallMarker` / `VisionRobotMarker` に任意 class / stroke option を最小追加する。
  - TRACKER-056 の `TrackerDiagnosticsFieldSourceFrame` / `TrackerPacketSnapshotSemanticSummary` / `DiagnosticsFieldViewFactory` mapper を再利用する。external / unknown / source label は `LoadFieldSourceFrame(...)` と cached index の nearest timestamp selection を使い、overlay 専用に sidecar JSONL を再読込しない。
  - raw `Vision Input` と ibis tracker output は、render snapshot 由来 layerとして既存 raw detections と `TrackedVisionViewState.FromSnapshot(...)` から overlay layer view model へ変換する。
  - render snapshot geometry がない場合は geometryなしの empty state とし、tracker source sidecar だけから geometry を復元しない。metadata missing、sidecar not-created、sidecar missing、sidecar empty、sidecar corrupt、own baseline missing、candidate missing、drawable objects empty は layer status として legend に表示する。片方の layer が missing でも ready layer は描画する。
  - focused tests は overlay mode state、左右 selector 2 source contract、`All` 非追加、TRACKER-056 frame/cache再利用、missing系status、visibility toggle、layer色分け/legend、semantic summary mapper を対象にする。
  - 実装対象は `TrackerDiagnosticsComparisonUiState`、`TrackerDiagnosticsComparisonViewStateReader`、`Diagnostics.razor` / `.cs` / `.css`、diagnostics Field overlay component、`DiagnosticsFieldViewFactory`、関連 focused tests、必要なら `Tracker.Server/README.md`。非対象は receiver / snapshot writer / metadata schema / `Tracker.Core` tracking algorithm / `Tracker.CaptureReplay` 出力変更 / 任意個数 source overlay / 永続化設定。
  - `tasks-status.md` と `phases-status.md` は、TRACKER-057 の設計結果と最小実装判断だけを最小更新した。
  - `git diff --check` は問題なし。

## リスク

- 未解決のリスクまたは後続対応:
  - 実画面 browser evidence は未実施。overlay legend、visibility control、Field header mode control が 4K / 狭幅で崩れないことは実装後に manual evidence または review で確認する必要がある。
  - `VisionBallMarker` / `VisionRobotMarker` は現状 source layer styling を直接受け取らない。overlay component 内で marker を直接描くか、既存 marker に任意 class / stroke option を追加するかを実装時に狭く決める必要がある。
  - `DiagnosticsFieldViewFactory` の tracker source projection mapper は TRACKER-056 review で直テスト不足が held concern として残っている。TRACKER-057 では overlay layer色分けと合わせて mapper の ball / yellow / blue 変換を focused test に含める。
  - 2 source overlay を超える任意個数 source、source間差分ハイライト、timestamp delta window調整、設定永続化はこの PR では扱わない。
  - 今回は設計のみ。コード実装、dotnet build、dotnet test、sub-agent起動、nested Codex は実施していない。
