# Tracker DebugHost / CLI / UI 保守性改善 設計

## 目的

この文書は `TRACKER-034` で実施した旧 `Tracker.Server`、`Tracker.CaptureReplay`、diagnostics UI の巨大ファイル分割と日本語コメント追加の保守性改善を記録する。現行の project / namespace / 起動経路では、旧 `Tracker.Server` の Web UI / diagnostics 責務を `Tracker.DebugHost` と呼ぶ。

CaptureOn 比較ログの機能仕様は `debug-host-cli-ui-detail-design.md` に分離し、この文書では扱わない。

## 対象範囲

- `Tracker/Tracker.CaptureReplay/Program.cs`
- `Tracker/Tracker.DebugHost/Tracking/TrackerCoordinator.cs`
- `Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor`
- `Tracker.DebugHost/Tracking` の diagnostics / render snapshot / profile switch 周辺
- `Tracker.DebugHost/Vision` の capture / receiver / store 周辺の既存境界確認

対象外:

- `Tracker.Core` engine 実装の分割
- test file の分割
- UI の新機能追加
- diagnostics log / capture file の schema 変更
- official tracker packet の送信内容変更
- CaptureOn 比較ログの機能設計

## 設計履歴

`TRACKER-034` の主作業は、`Program.cs`、`TrackerCoordinator.cs`、`Diagnostics.razor` の責務分離と主要 class / property / method の日本語コメント追加だった。

詳細な実装証跡とレビューは次を正とする。

- `reports/tracker-034-server-worker-20260511074000.md`
- `reports/tracker-034-comment-followup-worker-20260511082000.md`
- `reports/tracker-034-review-20260511081000.md`
- `reports/tracker-034-review-r2-20260511083000.md`

旧タスク一覧上の位置づけは `tracker-history-000-038.md` の `TRACKER-034` と `maintenance` phase に退避済み。

## 分割方針

- 1 ファイル 1 主責務を基本とし、entrypoint / orchestration / markup から純粋 helper、I/O、view state、formatting、option parsing を分離する。
- public / internal の既存型名は可能な限り維持し、外部参照がある型の rename は避ける。
- dot 区切りファイル名は framework / toolchain 慣習に限って許容する。手書き C# の責務 marker として `TypeName.Responsibility.cs` を使わない。
- partial class を責務別に分ける場合は type-owned folder を作り、folder が型名、file が責務名を表す配置へ寄せる。
- 挙動維持のため、分割前後で同じ入力から同じ observable output を返すことを最優先にする。

## コメント追加基準

コメントは「何をしているか」ではなく「この型や member がどの契約を守るか」を説明する。自明な setter や局所変数には追加しない。

C# の class / property / method の契約説明は日本語 XML documentation comment を基本にする。通常コメント `//` は method 内の複雑な block、不変条件、順序制約の直前だけに置く。

追加対象:

- public class / record / interface / enum
- internal class / record / enum のうち、ファイル外から参照されるもの
- public / internal property のうち、設定値、出力 metric、UI state、外部 schema と対応するもの
- public / internal method
- private method のうち、profile switch 順序、diagnostics schema、capture schema、UI selection state など保守時に破壊しやすい契約を持つもの

追加しない対象:

- 単純な clone の private helper 全てへの逐語的コメント
- `ToString`、小さな formatting helper など名前と型で十分に意図が読めるもの
- generated proto 型や外部 library 型
- local variable や局所的な LINQ の説明

## 検証観点

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

## 完了状態

- `Program.cs`、`TrackerCoordinator.cs`、`Diagnostics.razor` は責務別ファイルへ分割済み。
- public/internal の主要 class / property / method に日本語コメントを追加済み。
- CaptureReplay、TrackerCoordinator、Diagnostics UI の既存挙動維持観点は `TRACKER-034` の report に記録済み。
