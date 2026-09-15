# Tracker.RuntimeHost 設計

## 目的

`Tracker.RuntimeHost`[^tracker-runtime-host] は、トラッカーと将来の自動判定モード[^autoref-mode]を同一プロセス[^same-process]で低遅延に実行する、実運用を想定した画面なしの実行体[^headless-host]とする。Web UI[^web-ui]、診断記録の再生[^diagnostics-replay]、記録内容の確認画面[^capture-viewer]は `Tracker.DebugHost`[^tracker-debug-host] へ分離し、描画やログ保存の負荷がトラッカーと自動判定の実時間処理へ影響しないようにする。

## 命名

- `Tracker.RuntimeHost`: トラッカーの実時間処理[^tracker-operation]と将来の自動判定モードを同一プロセスで実行する、実運用向けの実行体。
- `Tracker.DebugHost`: 旧 `Tracker.Server` から名前を変更した診断用の実行体。Web UI、未加工の映像入力の表示[^raw-vision-viewer]、診断、記録と再生、比較表示を担当する。
- `Tracker.Core`: 追跡アルゴリズム[^tracker-algorithm]、契約、副作用のないモデル、実運用と診断の実行体に共通するロジックを置く。

`Tracker.Executer` / `Tracker.Executor` は採用しない。今回の実行体はトラッカー専用ではなく、将来の自動判定モードも同居する試合時の実行環境だからである。

## 責務境界

`Tracker.RuntimeHost` は次を担当する。

- SSL-Vision 入力の受信、または実運用向けの受信処理との接続。
- トラッカーの周期処理の実行。
- 追跡結果のパケット送信。
- 将来の自動判定モードを同一プロセスへ入れるための動作モードの分離。
- 実時間処理の性能を優先する設定と起動経路。

`Tracker.RuntimeHost` は次を担当しない。

- Web UI の描画。
- 診断記録を再生する UI。
- 記録内容の確認画面。
- 診断用の比較表示。
- 旧ログ形式の互換維持。

`Tracker.DebugHost` は次を担当する。

- Web UI と診断表示。
- 未加工の映像入力、自前の追跡結果、外部トラッカーの追跡結果の可視化。
- 記録、再生、比較。
- RuntimeHost または送信された追跡結果の購読と、診断用データの採取・保存。

`Tracker.DebugHost` はトラッカーの周期処理を主実行責務として持たない。診断用に同一リポジトリの共通部品を使っても、画面描画や診断ログの保存が RuntimeHost の処理周期を支配しない構造にする。

## AutoRef 方針

AutoRef 実装は今回の対象外とする。ただし `Tracker.RuntimeHost` は将来の自動判定モードを同一プロセスに内包できる名前と責務境界にする。想定する動作モードは次のように扱う。

- トラッカーのみを動かすモード。
- トラッカーと AutoRef を動かすモード。

自動判定モードは、追跡結果をプロセス外通信で再購読する前提にしない。試合時の性能を優先するため、RuntimeHost 内でトラッカーの状態と自動判定の処理を同居できる境界を残す。

## RuntimeHost 設定方針

RuntimeHost の実行周期はコード内に数値を直接埋め込まない。`Tracker.RuntimeHost` の初期構成では `RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開し、主処理と制御処理はこの値を使って周期を決める。0 以下の値は性能調整の意図を曖昧にするため、既定値で代用せず、起動時の設定検証エラーとする。

`Tracker.RuntimeHost` の実装で追加する調整値は、実運用で変更する可能性があるものを設定オブジェクトや設定ファイルに出す。通信規約の名前、補助ファイル名、記録に付随する情報の項目名など、再生や通信形式の契約として固定すべき値は設定化しない。

## 周期処理の分離方針

トラッカーの周期処理は、Web UI を提供するサーバーのライブ表示処理と、診断ログの保存・再生処理の両方から切り離す。

- トラッカーの周期処理は、追跡状態の更新と送信を最優先する。
- DebugHost のライブ表示は、変更不能な最新のスナップショットまたは送信された出力を読む側に回る。
- 診断ログの保存・再生は DebugHost 側で一定周期でデータを採取する処理として扱い、トラッカーのフレーム確定周期と同じ周期での保存を要求しない。
- 旧描画記録の補助ファイルとの互換は非要件とし、新規ログ保存と新規記録の性能を優先する。

### `RUNTIME-HOST-005`: `Tracker.Core` の共通実行処理の境界

`RUNTIME-HOST-005` では新規 `Tracker.RuntimeHost` プロジェクトの初期構成は作らない。先に `Tracker.DebugHost` が持っていたトラッカーの周期処理を `Tracker.Core` 内の UI 非依存の実行処理へ抽出し、将来 `Tracker.RuntimeHost` プロジェクトからそのまま再利用できる形にする。

`Tracker.Core` の共通実行処理は次を担当する。

- `ITrackerEngine.Update` の直列実行。
- 設定組の切り替え要求の待機中・処理中の管理と、観測データを伴わない制御要求の処理。
- `TrackerUpdateResult.EmittedEvents` の順でのイベント処理。
- 確定フレームごとの最新スナップショットの更新。
- 公式形式の `TrackerWrapperPacket` の生成と、`ITrackerPacketPublisher` への送信依頼。
- 送信設定の反映、送信成功・失敗の統計、オブザーバーへの通知。

`Tracker.Core` の共通実行処理は次を参照しない。

- `Tracker.DebugHost` の名前空間とプロジェクト。
- Blazor / Web UI。
- 診断ログのファイル保存。
- 受信記録の書き込み・読み取り処理。
- `VisionPacketCaptureSession`。
- `TrackerRenderSnapshot`。
- `TrackerPacketSnapshotLog`。
- `TrackerSnapshotAlignmentLog`。

DebugHost の `VisionReceiverService` は、UDP のデコード、未加工入力の状態保存、受信内容の記録の後に `Tracker.Core.TrackerCoordinator.ProcessPacket` を呼ぶ接続用の処理として残してよい。DebugHost 固有の診断設定の解決結果は `TrackerResolvedOptions` として残すが、`Tracker.Core` の周期処理が受け取る設定の構造は `TrackerRuntimeResolvedOptions` に分離し、`Tracker.Core` が DebugHost の型を参照しないようにする。

### `RUNTIME-HOST-006`: DebugHost のライブ表示用スナップショットの境界

`RUNTIME-HOST-006` では、DebugHost のライブ表示で必要な状態を、描画更新ごとにまとめて固定する。`Home.razor` は `VisionPacketStore` / `TrackedSnapshotStore` を直接受け取らず、`VisionLiveDisplaySnapshotProvider` から `VisionLiveDisplayRenderSnapshot` を 1 回取得する。このスナップショットは、同一の描画更新時点の未加工 SSL-Vision 入力、自前の追跡結果、外部トラッカーの読み取り用の状態、比較用の `VisionLiveComparisonRenderSnapshot` を同時に保持する。

`VisionLiveComparisonSnapshotComposer` は状態の保存先を直接読まない。提供側が固定済みの未加工入力、自前の追跡結果、外部トラッカーのスナップショットを渡し、合成処理はその値から比較する表示元の候補、Layer A/B、詳細表示を生成する。これにより、未加工入力・追跡結果・比較の各表示は同一の描画更新時点のスナップショットから派生し、比較のために未加工入力や追跡結果の保存先を再読取しない。

外部トラッカーについては、`MultiTrackerManager<TrackerPacketAdapter>` の変更可能な状態を描画経路で直接読まない。`ExternalTrackerSnapshotStore` が管理側の更新イベントからパケットと付随情報を複製済み DTO として保持し、ライブ表示への提供側はその読み取り用スナップショットだけを読む。`TrackerConnectionLibReceiverHostedService` と CaptureOn の記録処理は従来どおり管理側の更新経路に接続し、`RUNTIME-HOST-006` では診断の定期記録を保存する補助ファイルや RuntimeHost の初期構成へ踏み込まない。

### `RUNTIME-HOST-007`: DebugHost の診断記録を補助ファイルへ保存する高速経路

`RUNTIME-HOST-007` では RuntimeHost の初期構成へ踏み込まず、DebugHost の CaptureOn による記録単位に、診断の定期記録を保存する補助ファイルを追加する。`DiagnosticsSampleHostedService` は UI 表示の有無に依存せず、一定周期で診断用データを採取する処理として動作する。`VisionLiveDisplaySnapshotProvider` から未加工入力と自前の追跡結果の最新スナップショットを固定し、同じ採取時点の記録として `diagnostics-samples.jsonl` へ保存する。採取周期は `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` で設定し、既定値は `100` ms、0 以下は既定値へ戻す。`Home.razor` の更新処理はライブ表示の描画更新だけを担当し、診断ログの保存周期を決めない。記録の付随情報は `DiagnosticsSampleSidecarPath` と `DiagnosticsSampleLog` を持つ。

診断記録の再生・比較は、診断の定期記録を保存する補助ファイルが存在する記録単位では、採取時点の列を再生時系列の主経路にする。表示元としての `Vision Input` と `ibis tracker` は、旧描画記録の補助ファイルではなく、採取した診断記録に含まれる入力・追跡結果の概要から復元する。旧描画記録の補助ファイルだけを持ち、診断の定期記録を保存する補助ファイルを持たない記録単位は、非対応または機能を制限した旧形式として扱い、高コストな互換経路は復活させない。

### `RUNTIME-HOST-009`: RuntimeHost の通常処理

`RUNTIME-HOST-009` では `Tracker.RuntimeHost` に画面なしで動作する SSL-Vision 受信処理とトラッカーの周期処理を実装する。RuntimeHost は `VisionReceiver` の設定階層から、SSL-Vision のマルチキャストアドレス、UDP ポート、必要に応じて指定するローカル IPv4 インターフェースのアドレスを読み取り、DebugHost の `VisionReceiverService`、未加工入力の保存処理、受信記録の書き込み処理、診断画面に依存せずに `SSL_WrapperPacket` を受信する。

受信処理は、カメラごとに最新パケットを保持するバッファへ、パケットと受信時刻を保存する。トラッカーの周期処理は、このバッファを `RuntimeHost:OperationLoopIntervalMilliseconds` に従う周期で読み取り、未処理のカメラごとの最新パケットを受信時刻順に `TrackerCoordinator.ProcessPacket` へ渡す。同じカメラから処理周期の間に複数パケットが届いた場合は最新だけを残し、異なるカメラのパケットを単一の保存先への上書きで落とさない。実行周期はコード内の固定値にせず、`RuntimeHostOptions` の検証済み設定値だけから決める。

RuntimeHost は `Tracker` の設定階層から、追跡の有効化、追跡結果の送信元名、UUID、UDP 送信の有効化、設定組ごとの送信先、エンジン設定を解決して `TrackerRuntimeResolvedOptions` を作る。`Tracker.Core` 側の `TrackerCoordinator`、`TrackedSnapshotStore`、`ITrackerPacketPublisher` / `UdpTrackerPacketPublisher`、`TrackerPacketGenerator` を DI で組み立て、確定フレームごとに公式形式の `TrackerWrapperPacket` を送信し、同じ共通実行処理の最新の追跡スナップショットを更新する。

起動時の設定組の選択は、設定ファイルの `Tracker:ActiveProfileName` を既定にする。ただし、運用時の切り替え確認では `Tracker.RuntimeHost` の CLI 引数 `--profile <name>` または `--profile=<name>` がこれを上書きできるようにする。CLI 引数の解決は .NET のコマンドライン設定を読み込む仕組みと、引数から設定項目への対応表を使い、将来の短縮オプションも同じ対応表に追加できる形にする。CLI による設定組の上書きは `Tracker:Profiles:<name>` の既存の設定組だけを選択し、設定組の定義自体は CLI から生成しない。不正な空指定や値なし指定は起動時に明示的に失敗させ、誤って `default` の設定組で代用しない。

DebugHost が読む最新の追跡スナップショットは、RuntimeHost から DebugHost プロジェクトへ直接依存して公開しない。DebugHost 側は、公式形式の追跡パケットの送受信経路、または `Tracker.Core` の共通実行処理の境界に沿った読み取り用スナップショットを読む側として成立させる。`RUNTIME-HOST-009` では RuntimeHost の正常系を実行可能な契約テストで固定し、DebugHost の UI、診断記録の再生、記録内容の確認画面の手動検証は `RUNTIME-HOST-010` に残す。

## 設計資料配置

設計資料は `Tracker/Design/` を正本のルートとする。

- `Tracker/Design/Core/`: 追跡アルゴリズム、契約、副作用のないロジック。
- `Tracker/Design/DebugHost/`: Web UI、診断、未加工の映像入力の表示、記録と再生。
- `Tracker/Design/RuntimeHost/`: RuntimeHost、プロセス分離、将来の自動判定モード。
- `Tracker/Design/Archive/`: 旧進捗管理ファイルの保存先。現在の進捗管理には使わない。

## 非スコープ

- 自動判定の処理の実装。
- レフェリープログラムの判定規則を実行するエンジンの実装。
- 旧診断ログ形式の完全互換。
- `BreakingChanges` の作成。

## テスト方針

- RuntimeHost が Web UI プロジェクトを参照しないことを、プロジェクト参照・依存関係のテストで固定する。
- RuntimeHost のトラッカーの周期処理が、診断ログの保存・再生 API を直接呼ばないことを契約テストで固定する。
- DebugHost が追跡結果を読む側であり、トラッカーの周期処理を画面描画の更新周期から駆動しないことを契約テストで固定する。
- 診断記録の採取周期がトラッカーのフレーム確定周期に依存しないことを回帰テストで固定する。

[^tracker-runtime-host]: Tracker.RuntimeHost: トラッカーの実時間処理と将来の自動判定モードを同一プロセスで動かす、実運用を想定した画面なしの実行体。
[^autoref-mode]: 自動判定モード: レフェリープログラム相当の判定処理をトラッカーと同一プロセスで動かす将来のモード。今回の実装対象ではない。
[^same-process]: 同一プロセス: トラッカーと将来の自動判定処理を、プロセス外通信なしで同じ OS プロセス内に置く実行形態。
[^headless-host]: 画面なしの実行体: Web UI を持たず、入出力と実時間処理を主目的に起動する実行体。
[^web-ui]: Web UI: ブラウザーで見る診断画面。RuntimeHost の実時間処理から分離する。
[^diagnostics-replay]: 診断記録の再生: 採取した診断データやログを DebugHost 側で再生し、未加工入力と追跡結果を比較する診断機能。
[^capture-viewer]: 記録内容の確認画面: 保存済みの記録単位の内容を確認する診断用の表示機能。
[^tracker-debug-host]: Tracker.DebugHost: 旧 `Tracker.Server` から名前を変更した診断用の実行体。Web UI、診断、記録と再生、比較表示を担当する。
[^tracker-operation]: トラッカーの実時間処理: SSL-Vision 入力から追跡状態を更新し、公式形式の追跡パケットを送信する処理。
[^raw-vision-viewer]: 未加工の映像入力の表示: SSL-Vision の検出情報と競技場形状をフィールド上に表示する DebugHost の画面。
[^tracker-algorithm]: 追跡アルゴリズム: 未加工の検出情報からボールとロボットの追跡状態を決定的に生成する `Tracker.Core` 側のロジック。
