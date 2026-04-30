# Raw Vision Viewer Evidence

## 対象

- Task: RAW-VISION-001
- Scope: `Tracker.Server` の SSL_WrapperPacket raw vision 受信、store、field projection、root Blazor UI、単体テスト。

## 実行者

- Executor: main agent
- 理由: UI、store、receiver、tests の差分が密に結合しており、現在の設計/進捗更新と同じ文脈で統合する必要があったため。

## 変更概要

- `Tracker.Server` から `SslProto` を直接参照。
- `VisionReceiver` 設定を `appsettings.json` に追加。
- `VisionReceiverService` で UDP bind、multicast join、`SSL_WrapperPacket.Parser.ParseFrom` decode を実装。
- `VisionPacketStore` で latest packet / detection / geometry / count / error / endpoint / timestamp を保持。
- `VisionFieldProjection` で SSL field 座標から SVG 座標への変換を実装。
- `/` を raw vision viewer に差し替え、field SVG、balls/robots/calibration tables、raw JSON を表示。
- Navigation を Vision viewer 中心に整理。
- Store と projection の xUnit tests を追加。
- Central Package Management と衝突していた test package version 指定を修正。

## 検証

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj`
  - Result: passed
  - Tests: 6 passed
- `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj`
  - Result: passed
  - Warnings: 0
  - Errors: 0
- `dotnet run --project Tracker/Tracker.Server/Tracker.Server.csproj --launch-profile http`
  - Result: failed in this sandbox
  - Reason: TCP/UDP socket creation returned `Permission denied`; no local URL could be kept running from this environment.

## レビュー結果

- Reviewer: parent-side review
- Finding: no actionable findings
- Note: workflow skill の review-enforcer は sub-agent review を要求するが、このセッションではユーザーから sub-agent 実行の明示許可がないため、独立 sub-agent review は未実行。

## 未解決リスク

- 実機 SSL-Vision multicast の受信確認は未実行。
- UI は build-level verification までで、ブラウザ screenshot 検証は未実行。
- この sandbox では Kestrel の localhost bind も権限で失敗したため、dev server URL は未確認。
