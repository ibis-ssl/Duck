# Tracker DebugHost / CLI / UI 保守性改善 設計

## 目的

この文書は `TRACKER-034` で実施した旧 `Tracker.Server`、`Tracker.CaptureReplay`、診断画面の巨大ファイル分割と日本語コメント追加の保守性改善を記録する。現行のプロジェクト、名前空間、起動経路では、旧 `Tracker.Server` の Web UI と診断機能を担当する実行体を `Tracker.DebugHost` と呼ぶ。

CaptureOn 比較ログの機能仕様は `debug-host-cli-ui-detail-design.md` に分離し、この文書では扱わない。

## 対象範囲

- `Tracker/Tracker.CaptureReplay/Program.cs`
- `Tracker/Tracker.DebugHost/Tracking/TrackerCoordinator.cs`
- `Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor`
- `Tracker.DebugHost/Tracking` の診断、描画時点の記録、設定組の切り替えに関わる処理
- `Tracker.DebugHost/Vision` の記録、受信、状態の保存に関わる既存の責務境界の確認

対象外:

- `Tracker.Core` の追跡エンジン実装の分割
- テストファイルの分割
- UI の新機能追加
- 診断ログと受信記録ファイルの保存形式の変更
- 公式形式の追跡パケットの送信内容変更
- CaptureOn 比較ログの機能設計

## 設計履歴

`TRACKER-034` の主作業は、`Program.cs`、`TrackerCoordinator.cs`、`Diagnostics.razor` の責務分離と主要なクラス、プロパティ、メソッドへの日本語コメント追加だった。

詳細な実装証跡とレビューは次を正とする。

- `reports/tracker-034-server-worker-20260511074000.md`
- `reports/tracker-034-comment-followup-worker-20260511082000.md`
- `reports/tracker-034-review-20260511081000.md`
- `reports/tracker-034-review-r2-20260511083000.md`

旧タスク一覧上の位置づけは `tracker-history-000-038.md` の `TRACKER-034` と `maintenance` の段階に退避済み。

## 分割方針

- 1 ファイル 1 主責務を基本とする。起動処理、処理全体の制御、画面構造の記述から、副作用のない補助処理、入出力、表示状態の管理、表示内容の整形、オプション解析を分離する。
- `public` / `internal` の既存型名は可能な限り維持し、外部参照がある型の名前変更は避ける。
- ドット区切りのファイル名はフレームワークや開発ツールの慣習に限って許容する。手書き C# の責務を示すために `TypeName.Responsibility.cs` を使わない。
- partial class を責務別に分ける場合は、型名のフォルダを作り、その中のファイル名が責務を表す配置へ寄せる。
- 挙動維持のため、分割前後で同じ入力から外部に観測できる同じ出力を返すことを最優先にする。

## コメント追加基準

コメントは「何をしているか」ではなく「この型やメンバーがどの契約を守るか」を説明する。自明な値の設定処理や局所変数には追加しない。

C# のクラス、プロパティ、メソッドの契約説明は日本語の XML コメントを基本にする。通常コメント `//` はメソッド内の複雑な処理、不変条件、順序制約の直前だけに置く。

追加対象:

- `public` のクラス、レコード、インターフェース、列挙型
- `internal` のクラス、レコード、列挙型のうち、ファイル外から参照されるもの
- `public` / `internal` のプロパティのうち、設定値、出力する指標、UI の状態、外部のデータ形式と対応するもの
- `public` / `internal` のメソッド
- `private` のメソッドのうち、設定組の切り替え順序、診断ログや受信記録の形式、UI の選択状態など保守時に破壊しやすい契約を持つもの

追加しない対象:

- 単純な複製を行う非公開の補助処理すべてへの逐語的コメント
- `ToString`、小さな整形用の補助処理など、名前と型で十分に意図が読めるもの
- 通信形式の定義から自動生成した型や、外部ライブラリの型
- 局所変数や局所的な LINQ の説明

## 検証観点

CaptureReplay:

- `--help` が使い方を表示して終了コード 0 になる。
- `--capture` なし、未知のオプション、不正な数値、不正な指標の指定が、従来と同じエラーメッセージと終了コード 2 になる。
- `--expect` 成功時は終了コード 0、失敗時は終了コード 1 になる。
- `--detail-filter` と `--max-details` による詳細行数と省略件数が変わらない。
- `--settings` でアプリケーション設定と記録時の付随情報の両方を読める。

TrackerCoordinator:

- 1 入力に複数の `CommittedFrames` がある場合、すべてのフレームをイベント順に処理する。
- 確定フレームが 0 件の通常入力では、送信、保存状態の更新、オブザーバーへの通知をしない。
- 観測データを伴わない設定組の切り替え要求も、未加工のパケットなしで処理される。
- `ProfileSwitched` と `GeometryReset` に伴う保存状態の消去と、オブザーバーへの通知順序が変わらない。
- 送信設定は `ProfileSwitched` 後にだけ反映される。
- 診断ログの各行は確定フレームの元となった検出情報を使い、未加工の検出件数と追跡結果のフレームとの対応がずれない。
- 描画時点の記録を保存する補助ファイルは、診断ログと同じフレーム番号で参照できる。

診断画面:

- 診断ログがない場合、ログがないことを示す既存の通知を表示する。
- ログの選択と再読み込みで、選択する記録を先頭へ戻す。
- 時系列表示のクリック、範囲指定による移動、マウスホイールによる移動で、同じ記録を選択する。
- 描画時点の記録があるときは、未加工の入力と追跡結果のフィールド表示が並ぶ。
- 描画時点の記録がないときは、既存のエラー表示になる。
- 記録に付随する補助ファイルの情報があると、設定組の内容を示すダイアログを開ける。
- `VisionFieldCanvas` の競技場形状、ボール、黄色・青色のロボットの見え方が分割前と一致する。

## リスク

- `TrackerCoordinator` は順序制御が密なため、責務分離でメソッドの呼び出し順を読み違えると、設定組の切り替えと送信設定の切り替え時点がずれる。
- CaptureReplay の標準出力は調査・自動検証で使われるため、表示文言の整理でも互換性リスクがある。
- 診断画面は画面構造の記述と状態更新が結びついているため、partial class に分割する際に `selectedEntry`、`profileMetadata`、`selectedRenderSnapshot` の同期順序を崩しやすい。
- コメント追加時に設計意図を広げすぎると、実装契約と異なる将来仕様を書いてしまう。

## 完了状態

- `Program.cs`、`TrackerCoordinator.cs`、`Diagnostics.razor` は責務別ファイルへ分割済み。
- `public` / `internal` の主要なクラス、プロパティ、メソッドに日本語コメントを追加済み。
- CaptureReplay、TrackerCoordinator、診断画面の既存挙動の維持に関する検証観点は、`TRACKER-034` の報告書に記録済み。
