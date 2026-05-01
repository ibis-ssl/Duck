# Raw Vision Source Selector Evidence

## 対象

- Task: RAW-VISION-007
- Scope: raw vision viewer に aggregate / per-camera views を追加し、field presentation を `RoboCup-SSL/ssl-vision-client` の source selector / field canvas 方向に寄せる。

## 参照元

- `RoboCup-SSL/ssl-vision-client` README
- `frontend/src/App.vue`
- `frontend/src/components/FieldCanvas.vue`

## 変更概要

- `VisionPacketStore` が latest frame 1件の上書きだけでなく、camera ごとの最新 detection / packet を保持するようにした。
- `VisionPacketSnapshot` に camera snapshots と aggregate detection snapshot を追加した。
- raw vision viewer に aggregate / camera 切替の source selector を追加した。
- field canvas に boundary-aware background、wheel zoom、drag pan、reset control を追加して `ssl-vision-client` の field-first presentation に寄せた。
- aggregate view は全 camera の最新 frame を合成して balls / yellow robots / blue robots を描画し、camera view は選択 camera の latest packet raw JSON を表示する。

## 検証

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj`
  - Result: passed
  - Tests: 10 passed
- `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj`
  - Result: passed
  - Warnings: 0
  - Errors: 0

## レビュー結果

- Reviewer: parent-side review
- Finding: no actionable findings

## 未解決リスク

- 実機ブラウザ上で zoom / pan の操作感は runtime 依存なので、必要なら delta 感度の微調整はありうる。
- `ssl-vision-client` の backend-generated shapes モデルまでは持ち込んでおらず、field line / arc / robots / balls の描画を現在の Blazor 実装で近づけている段階。
