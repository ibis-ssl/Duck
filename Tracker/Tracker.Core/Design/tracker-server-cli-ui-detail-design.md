# Tracker Server / CLI / UI 保守性改善 詳細設計

## 目的

`TRACKER-034` で `Tracker.Server`、`Tracker.CaptureReplay`、diagnostics UI の巨大ファイルを責務別に分割し、主要な class / property / method に日本語コメントを追加するための実行設計を定める。

この設計は実装の分割単位と作業順序を固定するものであり、tracker engine の契約、tracking 挙動、capture replay の出力形式、diagnostics UI の操作感は変更しない。

## 対象範囲

- `Tracker/Tracker.CaptureReplay/Program.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker.Server/Tracking` の diagnostics / render snapshot / profile switch 周辺
- `Tracker.Server/Vision` の capture / receiver / store 周辺の既存境界確認

対象外:

- `Tracker.Core` engine 実装の分割
- test file の分割
- UI の新機能追加
- diagnostics log / capture file の schema 変更
- official tracker packet の送信内容変更

## 現状の巨大ファイル

| ファイル | 行数 | 主な責務 | 分割優先度 |
| --- | ---: | --- | --- |
| `Tracker.CaptureReplay/Program.cs` | 1001 | CLI entrypoint、引数解析、capture 読み込み、settings 解決、summary 集計、detail filter、expect 条件評価 | 高 |
| `Tracker.Server/Tracking/TrackerCoordinator.cs` | 672 | raw packet 受け渡し、profile switch drain、event dispatch、snapshot 更新、UDP publish、diagnostics log、render snapshot capture、clone/equality helper | 高 |
| `Tracker.Server/Components/Pages/Diagnostics.razor` | 613 | diagnostics log 選択、timeline、scrubber、render snapshot 表示、metadata modal、geometry 変換、raw/tracked view model 変換 | 高 |
| `Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs` | 253 | diagnostics log file 列挙、行 parse、snapshot 化 | 中 |
| `Tracker.Server/Vision/VisionReceiverService.cs` | 239 | UDP 受信、interface 選択、packet decode、store 更新、capture 連携 | 中 |

`TRACKER-034` の主作業は上位 3 ファイルに限定し、`TrackerDiagnosticsLogReader` と `VisionReceiverService` は public/internal コメント補強と依存確認に留める。中規模ファイルまで同時分割すると検証対象が増え、Server / CLI / UI の正常系維持確認が散るためである。

## 共通分割方針

- 1 ファイル 1 主責務を基本とし、巨大ファイルから純粋 helper、I/O、view state、formatting、option parsing を分離する。
- public / internal の既存型名は可能な限り維持し、外部参照がある型の rename は避ける。
- 挙動維持のため、分割前後で同じ入力から同じ observable output を返すことを最優先にする。
- private helper を別型へ移す場合は、まず static な package-private 相当の `internal static` helper に分離し、状態を持たせる必要がある場合だけ instance class にする。
- ファイル移動後も namespace は既存と同じにし、DI 登録や Razor import の変更を最小化する。
- 分割とコメント追加を同じ commit に入れる場合でも、先に移動のみ、次にコメント補強の順で差分を作る。

## CaptureReplay の推奨分割

現状の `Program.cs` は top-level statements と複数の internal 型を同居させている。`Program.cs` は CLI entrypoint だけに縮小し、引数解析、実行、capture 読み込み、settings 解決、条件式を分ける。

推奨ファイル:

- `Program.cs`
  - `ReplayOptions.Parse(args)`、help/error handling、`CaptureReplayRunner.Run(...)` 呼び出し、標準出力、exit code 決定だけを残す。
- `CaptureReplayRunner.cs`
  - capture record を順に engine へ投入し、`ReplaySummary` を作る。
  - `TrackerEngine` 生成と `CommittedFrames` 集計をここへ集約する。
- `ReplaySummary.cs`
  - summary metric の保持と `GetMetric`。
- `ReplayFrameFormatter.cs`
  - detail frame の文字列化、raw source frame/camera 表示、ball/robot 表示。
  - `CultureInfo.InvariantCulture` を維持する。
- `VisionPacketCaptureReader.cs`
  - `jsonl.gz` 読み込み、schema version 確認、payload 復元。
- `VisionPacketCaptureRecord.cs`
  - `SSL_WrapperPacket` への parse を保持する record。
- `TrackerSettingsFactory.cs`
  - `--settings`、capture metadata shape、`Tracker.Server/appsettings.json` 互換読み込み、runtime override 適用。
- `ReplaySettingsOptions.cs`
  - `ReplaySettingsFile`、`ReplayResolvedOptions`、`TrackerProfileOptions`、`TrackerEngineOptions`、`TrackerRobotTrackerOptions`、`TrackerBallTrackerOptions`、`TrackerKickDetectorOptions`。
- `ReplayOptions.cs`
  - CLI 引数解析、usage 出力、summary/detail metric 定義。
- `Condition.cs`
  - `Condition`、`ComparisonOperator`、`ComparisonOperatorExtensions`。

分割時の注意:

- `--capture` 必須、`--help`、未知 option、数値 validation、metric validation の error message は変えない。
- `--settings <capture.metadata.json>` と `--settings Tracker/Tracker.Server/appsettings.json` の両方を維持する。
- summary 出力の key 名、順序、`settings=...` の内容は自動検証や調査手順で使われるため変更しない。
- detail filter は条件が指定された場合だけ詳細行を出す現状を維持する。
- `maxDetails` は matching count ではなく出力件数上限であり、omitted count の計算を変えない。

## TrackerCoordinator の推奨分割

`TrackerCoordinator` は orchestration 本体、profile switch state、event dispatch、diagnostics 出力、formatting、clone/equality helper が同居している。外部公開 surface は `TrackerCoordinator` のまま維持し、内部協力型へ責務を押し出す。

推奨ファイル:

- `TrackerCoordinator.cs`
  - constructor、`ProcessPacket`、`RequestProfileSwitch`、`ExecuteUpdates` を残す。
  - lock、`isProcessingUpdate`、pending drain の制御はここに残し、処理順序を見えやすくする。
- `TrackerCoordinatorProfileSwitch.cs`
  - `PendingProfileSwitchRequest`、pending / in-flight 昇格、`ApplyProfileSwitch` 相当の local state 遷移。
  - `desiredOptions`、`desiredRuntimeOverrides`、`appliedOptions` の比較と更新をここへ寄せる。
- `TrackerCoordinatorDispatch.cs`
  - `TrackerUpdateResult` の `EmittedEvents` 順 dispatch、snapshot store 更新、render snapshot capture、publish、observer 通知。
  - `WorldFrameCommitted` は `framesByNumber` から該当 frame を取る現状を維持する。
- `TrackerCoordinatorDiagnostics.cs`
  - `LogTrackerDiagnostics`、diagnostics line 組み立て、sidecar/default path 解決、write failure cache。
- `TrackerDiagnosticsFormatter.cs`
  - raw ball / raw robot / tracked ball / tracked robot / source frame/camera の文字列化。
- `TrackerResolvedOptionsComparer.cs`
  - `TrackerResolvedOptions` と `TrackerRuntimeOverrides` の値比較。
- `TrackerOptionsCloner.cs`
  - `TrackerResolvedOptions`、`TrackerEngineSettings`、publisher、diagnostics、runtime overrides の clone。

分割時の注意:

- `ProcessPacket` と `RequestProfileSwitch` は同じ `gate` で直列化する。
- `RequestProfileSwitch` が処理中でない場合に control-only `Update` を即時実行する挙動を維持する。
- `ExecuteUpdates` は pending request が残る間、同じ受信時刻で control-only update を drain し続ける。
- `ProfileSwitched` 受信前に publisher 配信先や active profile 表示を切り替えない。
- `ProfileSwitched` では applied/current settings、publisher configuration、snapshot store active profile、store clear、observer 通知の順序を変えない。
- `GeometryReset` は latest frame clear 後に observer 通知する。
- `WorldFrameCommitted` は store 更新、render snapshot capture、UDP publish、observer 通知の順序を変えない。
- diagnostics log は newest committed frame を対象にし、source detections は committed frame に紐づくものを使う。
- diagnostics write failure cache は path 単位で維持し、失敗 path への再試行抑制を変えない。

## Diagnostics UI の推奨分割

`Diagnostics.razor` は markup、page state、log loading、timeline selection、profile metadata、render snapshot、geometry 変換を同居させている。UI 表示を変えず、page component を薄くする。

推奨ファイル:

- `Diagnostics.razor`
  - route、inject、ページ全体の markup、event binding だけを残す。
- `Diagnostics.razor.cs`
  - page state と lifecycle / event handler を partial class へ移す。
- `DiagnosticsTimelineState.cs`
  - selected entry、index 計算、timeline item class、scrubber / wheel selection。
- `DiagnosticsProfileMetadataLoader.cs`
  - metadata path 解決、metadata JSON 読み込み、configured profile / resolved settings の view model 化。
- `DiagnosticsRenderSnapshotSelector.cs`
  - diagnostics log path から render snapshot index を読み、tracked frame number で選択する処理。
- `DiagnosticsFieldViewFactory.cs`
  - `TrackerGeometrySnapshot` から `SSL_GeometryData`、raw source detections から `SSL_DetectionBall` / `SSL_DetectionRobot`、tracked frame から `TrackedVisionViewState` を作る。
- `DiagnosticsProfileMetadataView.cs`
  - modal 表示用 record。

分割時の注意:

- `/diagnostics` route、inject される reader、既存 CSS class 名、button/select/range の DOM 構造は維持する。
- timeline click、range scrubber、wheel 操作、Ctrl/Shift step の挙動を変えない。
- render snapshot がない場合の error text と、render snapshot がある場合だけ shell class に `diagnostics-shell--render` を付ける挙動を維持する。
- profile metadata は capture sidecar diagnostics log のときだけ読める現状を維持する。
- `VisionFieldCanvas` に渡す raw/tracked geometry と object の生成規則を変えない。
- modal の open/close は選択 entry 更新や metadata error 時に閉じる現状を維持する。

## 日本語コメント追加基準

コメントは「何をしているか」ではなく「この型や member がどの契約を守るか」を説明する。自明な setter や局所変数には追加しない。

追加対象:

- public class / record / interface / enum
- internal class / record / enum のうち、ファイル外から参照されるもの
- public / internal property のうち、設定値、出力 metric、UI state、外部 schema と対応するもの
- public / internal method
- private method のうち、profile switch 順序、diagnostics schema、capture schema、UI selection state など保守時に破壊しやすい契約を持つもの
- Razor partial class の event handler で、選択状態・metadata・render snapshot を同期するもの

追加しない対象:

- 単純な clone の private helper 全てへの逐語的コメント
- `ToString`、小さな formatting helper など名前と型で十分に意図が読めるもの
- generated proto 型や外部 library 型
- local variable や局所的な LINQ の説明

書き方:

- C# の public / internal API には XML doc comment を使い、日本語で書く。
- private helper には必要な場合だけ通常コメントを使う。
- Razor markup 内に説明用の可視テキストを追加しない。
- コメントは現状の挙動と不変条件に限定し、将来機能の約束を書かない。
- diagnostics / capture / CLI の出力 schema に触れるコメントでは、互換性を維持する理由を明記する。

例:

```csharp
/// <summary>
/// 保存済み vision capture を tracker engine に順序通り再投入し、調査用 summary を作る。
/// </summary>
internal static class CaptureReplayRunner
```

```csharp
// ProfileSwitched を受け取るまでは、UI 表示と publisher 設定を新 profile へ進めない。
```

## TRACKER-034 実行順序

1. `git status --short` で他 worker の差分を確認し、自分の対象外ファイルを触らない。
2. `Program.cs` を CaptureReplay 系ファイルへ分割し、CLI の help/error/summary 出力を維持する。
3. CaptureReplay の分割後に `Tracker.CaptureReplay` の build と既存 capture replay 関連 test を実行する。
4. `TrackerCoordinator.cs` から diagnostics formatter、option comparer/cloner、profile switch、dispatch を段階的に分離する。
5. 各段階で `Tracker.Server` build または `Tracker.Tests` の focused test を実行し、profile switch と diagnostics log の正常系を崩していないことを確認する。
6. `Diagnostics.razor` を partial class と helper へ分割し、markup と CSS class を維持する。
7. UI 分割後に `Tracker.Server` build を実行し、可能なら `/diagnostics` で log 選択、timeline、scrubber、render snapshot、profile modal を手動確認する。
8. public/internal class / property / method から順に日本語コメントを追加し、private helper は契約があるものだけ補う。
9. 最後に full test または task 指定の focused test を実行し、report にコマンド・結果・未確認リスクを書く。

## 挙動維持の検証観点

CaptureReplay:

- `--help` が usage を出して exit code 0 になる。
- `--capture` なし、未知 option、不正数値、不正 metric が従来と同じ error message と exit code 2 になる。
- `--expect` 成功時は exit code 0、失敗時は exit code 1 になる。
- `--detail-filter` と `--max-details` の詳細行数と omitted count が変わらない。
- `--settings` で appsettings と capture metadata の両方を読める。

TrackerCoordinator:

- 1 入力に複数 `CommittedFrames` がある場合、全 frame を event 順に処理する。
- 0-frame の通常入力では publish / store update / observer 通知をしない。
- control-only profile switch が raw packet なしで drain される。
- `ProfileSwitched` と `GeometryReset` の store clear と observer 通知順序が変わらない。
- publisher 設定は `ProfileSwitched` 後にだけ反映される。
- diagnostics line は committed frame の source detections を使い、raw count と tracked frame の対応がずれない。
- render snapshot sidecar は diagnostics log と同じ frame number で引ける。

Diagnostics UI:

- diagnostics log がない場合、既存の empty alert を表示する。
- log 選択と reload が selected entry を先頭へ戻す。
- timeline click、range scrub、wheel scrub が同じ entry 選択を行う。
- render snapshot ありのとき raw field と tracked field が並ぶ。
- render snapshot なしのとき既存 error 表示になる。
- capture sidecar metadata があると profile settings modal が開ける。
- `VisionFieldCanvas` の geometry、ball、yellow/blue robot の見え方が分割前と一致する。

## リスク

- `TrackerCoordinator` は順序制御が密なため、責務分離で method 呼び出し順を読み違えると profile switch と publisher 設定の切替点がずれる。
- CaptureReplay の標準出力は調査・自動検証で使われるため、表示文言の整理でも互換性リスクがある。
- Diagnostics UI は markup と state 更新が結びついているため、partial 化で `selectedEntry`、`profileMetadata`、`selectedRenderSnapshot` の同期順序を崩しやすい。
- コメント追加時に設計意図を広げすぎると、実装契約と異なる将来仕様を書いてしまう。

## 完了条件

- `Program.cs`、`TrackerCoordinator.cs`、`Diagnostics.razor` が責務別ファイルへ分割され、各巨大ファイルが entrypoint / orchestration / markup 中心へ縮小している。
- public/internal の主要 class / property / method に日本語コメントが付いている。
- CaptureReplay、TrackerCoordinator、Diagnostics UI の既存挙動維持観点が report に記録されている。
- focused test / build / 必要な手動確認の結果が report に残っている。
