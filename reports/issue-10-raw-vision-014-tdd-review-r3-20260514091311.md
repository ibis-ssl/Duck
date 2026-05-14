# Sub-agent実行レポート

## タスク

- 目的: RAW-VISION-014 r2 blocking finding 修正後の r3 review として、Vision split / overlay contract、diagnostics latest-before contract、RAW-VISION-013 design との整合を確認する。
- タスク種別: レビュー

## sub-agentを使う理由

- 理由: ユーザー指示により、直前 reviewer として gpt-5.5 high 相当の r3 review 実務を担当し、`review-enforcer` に従って結果を report に記録する。

## 対象範囲

- 対象: `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`、`Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`、`reports/issue-10-raw-vision-014-tdd-fix-r2-20260514090645.md`、`reports/issue-10-raw-vision-014-tdd-review-r2-20260514090315.md`、関連する RAW-VISION-013 design / tracking。

## 対象外

- 対象外: production code、test code、設計書、PR本文、`Tracker/Tracker.Server/appsettings.json` の既存 unrelated 差分。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,240p' reports/issue-10-raw-vision-014-tdd-review-r3-20260514091311.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `git status --short`
  - `git diff --name-status`
  - `nl -ba Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs | sed -n '1,360p'`
  - `nl -ba Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs | sed -n '339,760p'`
  - `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs | sed -n '430,570p'`
  - `sed -n '1,240p' reports/issue-10-raw-vision-014-tdd-fix-r2-20260514090645.md`
  - `sed -n '1,220p' reports/issue-10-raw-vision-014-tdd-review-r2-20260514090315.md`
  - `nl -ba Tracker/Tracker.Server/Design/tasks-status.md | sed -n '1,32p;118,150p'`
  - `nl -ba Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md | sed -n '187,230p;280,294p'`
  - `rg -n "geometry|Geometry|raw geometry|fallback|Raw Aggregate|Raw Camera|CreateViewState|CaptureRenderTickSnapshot" Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs reports/issue-10-raw-vision-014-tdd-contract-20260514084547.md reports/issue-10-raw-vision-014-tdd-fix-r2-20260514090645.md reports/issue-10-raw-vision-014-tdd-review-r2-20260514090315.md Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md`
  - `git diff --check`
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~VisionLiveComparisonViewStateTests" -m:1 /nr:false`

## 対象ファイル

- 変更または確認したファイル:
  - 確認: `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
  - 確認: `reports/issue-10-raw-vision-014-tdd-fix-r2-20260514090645.md`
  - 確認: `reports/issue-10-raw-vision-014-tdd-review-r2-20260514090315.md`
  - 確認: `reports/issue-10-raw-vision-014-tdd-contract-20260514084547.md`
  - 確認: `Tracker/Tracker.Server/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - 変更: `reports/issue-10-raw-vision-014-tdd-review-r3-20260514091311.md`
  - 非対象維持: `Tracker/Tracker.Server/appsettings.json` の既存差分には触れていない。

## 指摘事項

- 指摘要約または「指摘なし」:
  - [Blocking normal-path] `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs:157`: RAW-VISION-014 の exit criteria にある raw geometry 優先 fallback がまだ値として固定されていない。test は geometry 付き raw camera packet を投入して `RawCameraSnapshots` の `ReceivedAt` / `CameraId` / `Detection.FrameNumber` は確認しているが、`renderSnapshot.Geometry` が raw geometry を保持すること、または raw geometry が無い場合に tracked geometry へ fallback し、3rd party tracker packet から geometry を復元しないことを検証していない。`tasks-status.md:14` と `raw-vision-viewer-plan.md:215` の contract に対し、RAW-VISION-015 が geometry を常に null にしても現在の Vision contract tests は pass し得る。
  - r2 finding の中心だった `CreateSplitLayers` / `CreateOverlayLayers` / `CaptureRenderTickSnapshot` / `CreateViewState` の実呼び出し、source candidates、same-source collapse、missing layer と ready layer 維持、Layer A/B visibility、後続 store 更新で旧 render snapshot が変化しないことの値検証は追加済み。
  - diagnostics latest-before test は r2 時点の解消状態を維持している。`Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs:490` 以降で source `receivedAt`、selected timeline `receivedAt`、latest-before / stale 状態、delta を引き続き要求している。
  - ユーザー確認が必要な capability gap: なし。
  - Non-blocking concern: なし。

## 結果

- 結果:
  - Review outcome: blocking finding あり。r2 blocking finding の主要部分は解消しているが、RAW-VISION-014 の raw geometry 優先 fallback contract が未固定のため、このまま RAW-VISION-014 complete にはできない。
  - `git diff --check`: pass。
  - focused test: expected failing。35 件中 26 pass / 9 fail。fail は Vision live comparison API 未実装 7 件、diagnostics latest-before が現状 `saved-session-alignment` になる 1 件、future-only source が現状 `Ready` になる 1 件で、production 未実装の contract gap と対応している。
  - Disposition: Vision live comparison contract に raw geometry 優先、tracked fallback、3rd party geometry 非採用を値で確認する TDD test を追加してから再レビューが必要。

## リスク

- 未解決のリスクまたは後続対応:
  - geometry contract が未固定のままだと、RAW-VISION-015 で overlay field の基準 geometry が null または誤 source 由来になっても TDD gate を通過するリスクがある。
  - diagnostics 側は Red として妥当だが、実装後は latest-before delta が selected timeline `ReceivedAt` と source snapshot `receivedAt` の差分であることを再確認する必要がある。
  - `Tracker/Tracker.Server/appsettings.json` の既存差分は非対象として未確認・未変更。
