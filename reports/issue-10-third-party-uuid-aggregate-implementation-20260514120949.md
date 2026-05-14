# Sub-agent実行レポート

## タスク

Vision live の 3rd party tracker source を uuid 優先で統合し、同じ uuid なら endpoint が違っても 1 source として表示する。

## sub-agentを使う理由

source option の意味と UI 表示が変わるため、設計追補、TDD、実装、検証を sub-agent に切り出して実行証跡を残す。

## 対象範囲

Vision live comparison の 3rd party tracker source option、uuid 統合、代表 snapshot 選択、関連設計、tracking、tests。

## 対象外

複数 uuid の balls / robots union merge、diagnostics replay の saved alignment 仕様変更、PR draft 解除、`Tracker/Tracker.Server/appsettings.json`。

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `sed -n '1,260p' reports/issue-10-third-party-aggregate-investigation-20260514115926.md`
- `sed -n '1,260p' reports/issue-10-third-party-uuid-aggregate-implementation-20260514120949.md`
- `rg -n "Issue #10|RAW-VISION-016|3rd party|third party|uuid|source" Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md`
- `nl -ba Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs | sed -n '1,760p'`
- `nl -ba Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs | sed -n '1,980p'`
- `rg -n "SourceKey|SourceLabel|sourceLabel|sourceUuid|RemoteEndpoint|remoteEndpoint|TrackerPacketSnapshotRecord|TrackerSnapshotAlignmentRecord|Create.*Source|aggregate|External|FieldSource|ComparisonSource" Tracker/Tracker.Server/Tracking Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '980,1260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '1450,1770p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '1,520p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs | sed -n '1,240p'`
- Red: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter 'FullyQualifiedName~VisionLiveComparisonViewStateTests' -m:1 /nr:false`
- Green: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter 'FullyQualifiedName~VisionLiveComparisonViewStateTests' -m:1 /nr:false`
- Green: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter 'FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests' -m:1 /nr:false`
- Build: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Server/Tracker.Server.csproj -m:1 /nr:false`
- Final Green: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter 'FullyQualifiedName~VisionLiveComparisonViewStateTests|FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests' -m:1 /nr:false`
- Final Build: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Server/Tracker.Server.csproj -m:1 /nr:false`

## 対象ファイル

- 変更:
  - `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `Tracker/Tracker.Server/Design/tasks-status.md`
  - `Tracker/Tracker.Server/Design/phases-status.md`
  - `Tracker/Tracker.Server/Tracking/TrackerSourceIdentity.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`
  - `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs`
  - `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
  - `reports/issue-10-third-party-uuid-aggregate-implementation-20260514120949.md`
- 確認のみ:
  - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
  - `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- 触らなかったもの:
  - `Tracker/Tracker.Server/appsettings.json`: ユーザー指定どおり未編集。
  - diagnostics replay の saved alignment 仕様: 既存 normal path を壊さないため、endpoint-sensitive key の意味は維持。

## 指摘事項

- Vision live の既存実装は `MultiTrackerManager.Trackers.Values` を endpoint ごとに列挙し、3rd party snapshot key を `third-party:{sourceLabel}` にしていた。そのため同じ `uuid` / `sourceName=ER-FORCE` で endpoint だけ違う state が複数あると、UI option は同じ label / key のまま複数出ていた。
- 追加 TDD では、同じ uuid で endpoint が違う 2 state が 1 source へ統合されること、代表 snapshot は同じ uuid group 内の最新 `ReceivedAt` だけを使い balls / robots を union merge しないこと、同じ source name でも uuid が違うものは別 source として残り label が区別可能であること、uuid 空の場合は source name + endpoint fallback で識別することを固定した。
- Red 証跡: 追加直後の `VisionLiveComparisonViewStateTests` は 13 件中 10 pass / 3 fail。失敗内容は、同じ uuid endpoint 違いが 2 snapshot のまま残る、key が `third-party:ER-FORCE` のまま、uuid 違い / uuid 空 fallback の label が区別されない、という今回の既知 gap だった。
- 実装では `TrackerSourceIdentity` helper を追加し、diagnostics saved alignment 用の endpoint-sensitive key と、Vision live 用の uuid-priority key を同じ正規化 helper に寄せた。`TrackerSnapshotAlignmentRecord.CreateSourceKey()` は helper 経由にしたが、出力形式は従来どおり `sourceRole + sourceLabel + sourceUuid + remoteEndpoint` の endpoint-sensitive key。
- Vision live は 3rd party snapshot 作成時に `uuid` があれば `third-party:uuid:{uuid}` で group 化し、group 内の最新 `ReceivedAt` snapshot だけを採用する。`uuid` が空の場合は `third-party:fallback:{label}\u001f{endpoint}` を使う。
- UI label は、同じ display label が複数残る場合だけ短い uuid または endpoint を括弧で補助表示する。単一 uuid group に統合された `ER-FORCE` は過剰表示を避けて `ER-FORCE` のまま。
- diagnostics 確認結果: `TrackerDiagnosticsComparisonViewStateReader` は source label aggregate / role aggregate の filter 集合から代表 snapshot を 1 つ選ぶ構造で、Field source / comparison source は saved alignment がある場合 `TrackerSnapshotAlignmentRecord` の record index / endpoint-sensitive key / remote endpoint を保持した record に従う。今回の Vision 修正のように saved alignment key を uuid priority に変更すると既存 capture の対応付け仕様へ影響するため、diagnostics の挙動変更は実施しなかった。
- diagnostics UI 確認結果: `TrackerDiagnosticsComparisonUiState` は `SourceLabel` option value を source label 文字列として扱う。source label が同じで uuid が違う diagnostics sidecar record は現状 source label aggregate にまとまるため、uuid 単位 option が必要かは別仕様確認が必要。今回は replay saved alignment normal path を守るため、調査結果と後続候補として扱う。

## 結果

- `raw-vision-viewer-plan.md` に、3rd party tracker live source identity は uuid 優先、同じ uuid は endpoint 違いでも 1 source、代表は最新 `ReceivedAt` snapshot、union merge なし、uuid 空のみ source name / endpoint fallback、同名複数時は補助 label で区別、geometry は raw 優先 / tracked fallback 維持、という設計追補を追加した。
- `VisionLiveComparisonViewState.cs` は 3rd party tracker snapshot に source role / uuid / endpoint metadata を保持し、uuid priority key で group 化して代表 snapshot を選ぶようにした。
- `TrackerSourceIdentity.cs` を追加し、Vision live の uuid-priority key と diagnostics saved alignment の endpoint-sensitive key を同じ identity helper に集約した。
- `TrackerSnapshotAlignmentRecord.CreateSourceKey()` は `TrackerSourceIdentity.CreateEndpointSensitiveKey()` を呼ぶだけに変更し、diagnostics replay の saved alignment key 仕様は維持した。
- `VisionLiveComparisonViewStateTests` に uuid 統合、最新代表 snapshot、uuid 違い別 source + label 区別、uuid 空 fallback、same-source collapse の契約テストを追加した。
- Green 証跡:
  - `VisionLiveComparisonViewStateTests`: 13 件 pass。
  - `TrackerDiagnosticsComparisonViewStateTests`: 28 件 pass。
  - final combined focused tests: 41 件 pass。
  - `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj`: succeeded、0 warning / 0 error。
- `tasks-status.md` / `phases-status.md` は RAW-VISION-016 の追補実装と検証結果へ同期した。

## リスク

- diagnostics の source label aggregate は現在、uuid 優先 option ではなく source label / role filter 寄りの UI である。同じ source label かつ uuid 違いを diagnostics 上でも個別 option にしたい場合は、既存 saved alignment / replay UI contract への影響があるため別タスクで仕様確認が必要。
- Vision live の label 補助表示は同一 label が複数残る場合のみ行う。短い uuid が衝突するような特殊ケースでは追加の endpoint 表示が必要になる可能性がある。
- 実画面 UI は未確認。契約テストと build は通っているが、select の見た目や長い endpoint label の収まりはユーザー側 UI 確認または別途ブラウザ確認が必要。
