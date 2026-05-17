# Tracker.RuntimeHost 設計

## 目的

`Tracker.RuntimeHost`[^本番実行体] は、トラッカー と将来の AutoRef 動作形態[^将来動作形態] を同一実行単位[^同一実行単位] で低遅延に動かす本番寄りの画面なし実行体[^画面なし実行体] とする。画面機能[^画面機能]、診断再生[^診断再生]、収録表示[^収録表示] は `Tracker.DebugHost`[^確認用実行体] へ分離し、描画や記録の負荷が トラッカー / AutoRef の実時間処理へ影響しないようにする。

## 命名

- `Tracker.RuntimeHost`: トラッカー運用処理[^トラッカー運用処理] と将来の AutoRef 動作形態を同一実行単位で実行する本番実行体。
- `Tracker.DebugHost`: 旧 `Tracker.Server` から改名した確認用実行体。画面機能、未加工映像表示[^未加工映像表示]、診断、収録 / 再生、比較表示を担当する。
- `Tracker.Core`: トラッカー追跡処理[^トラッカー追跡処理]、契約、純粋な状態表現、本番実行体と確認用実行体の共通処理を置く。

`Tracker.Executer` / `Tracker.Executor` は採用しない。今回の実行体は トラッカー 専用の実行器ではなく、将来の AutoRef 動作形態も同居する試合時の実行基盤だからである。

## 責務境界

`Tracker.RuntimeHost` は次を担当する。

- SSL-Vision 入力の受信、または実行時向け受信境界。
- トラッカー運用周期処理の実行。
- トラッカー出力情報の発行。
- 将来の AutoRef 動作形態を同一実行単位へ入れるための境界。
- 実時間処理の性能を優先する設定と起動経路。

`Tracker.RuntimeHost` は次を担当しない。

- 画面描画。
- 診断再生画面。
- 収録表示。
- 確認用の比較表示。
- 旧記録形式の互換維持。

`Tracker.DebugHost` は次を担当する。

- 画面機能と診断表示。
- 未加工映像 / 追跡済み結果 / 外部トラッカーの確認表示。
- 収録 / 再生 / 比較。
- `Tracker.RuntimeHost` または発行済みトラッカー出力の購読と、確認用標本の保存。

`Tracker.DebugHost` は トラッカー運用周期処理を主実行責務として持たない。確認用に同一保存庫の共通部品を使っても、画面描画や診断記録が `Tracker.RuntimeHost` の処理周期を支配しない構造にする。

## AutoRef 方針

AutoRef 実装は今回の対象外とする。ただし `Tracker.RuntimeHost` は将来の AutoRef 動作形態を同一実行単位に内包できる名前と責務境界にする。想定する動作形態は次のように扱う。

- トラッカー単独の動作形態。
- トラッカー + AutoRef の動作形態。

AutoRef の動作形態は トラッカー出力を実行単位外の通信で再購読する前提にしない。試合時の性能を優先するため、`Tracker.RuntimeHost` 内で トラッカー状態と AutoRef 判定処理を同居できる境界を残す。

## Tracker.RuntimeHost 設定方針

`Tracker.RuntimeHost` の実行周期は実装内の固定値にしない。`Tracker.RuntimeHost` の初期構成では `RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開し、`Tracker.RuntimeHost` の主周期処理 / 制御周期処理はこの値を使って周期を決める。0 以下の値は性能調整の意図を曖昧にするため、既定値への代替ではなく起動時の検証失敗とする。

`Tracker.RuntimeHost` の実装で追加する調整値は、実運用で変更する可能性があるものを設定 / `appsettings` に出す。通信方式名、補助記録名、付加情報の項目名など、再生や通信上の契約として固定すべき値は設定化しない。

## 周期処理分離方針

トラッカー運用周期処理は、画面側の生表示処理と診断記録 / 再生処理の両方から切り離す。

- トラッカー運用周期処理は トラッカー状態更新と発行を最優先する。
- `Tracker.DebugHost` の生表示は最新の不変断面、または発行済み出力を読む側に回る。
- 診断記録 / 再生は `Tracker.DebugHost` 側の標本周期処理として扱い、トラッカー確定済み処理時点の周期を保存周期として要求しない。
- 旧描画断面補助記録の互換は非要件とし、新規記録 / 新規収録の性能を優先する。

### `RUNTIME-HOST-005`: 中核共有実行境界

`RUNTIME-HOST-005` では新規 `Tracker.RuntimeHost` 構成単位の初期構成は作らない。先に `Tracker.DebugHost` が持っていた トラッカー運用周期処理を `Tracker.Core` 内の画面非依存な実行境界へ抽出し、将来 `Tracker.RuntimeHost` 構成単位からそのまま再利用できる形にする。

`Tracker.Core` の共有実行境界は次を担当する。

- `ITrackerEngine.Update` の直列実行。
- 設定組切り替え要求の保留 / 実行中管理と、制御専用更新の排出。
- `TrackerUpdateResult.EmittedEvents` 順の配信。
- 確定済み処理時点ごとの最新断面保存先更新。
- 公式 `TrackerWrapperPacket` 生成と `ITrackerPacketPublisher` への発行。
- 発行器設定反映、発行成功/失敗統計、観測者通知。

`Tracker.Core` の共有実行境界は次を参照しない。

- `Tracker.DebugHost` の名前空間 / 構成単位。
- `Blazor` / 画面機能。
- 診断記録文書への記録。
- 収録の書き込み器 / 読み取り器。
- `VisionPacketCaptureSession`。
- `TrackerRenderSnapshot`。
- `TrackerPacketSnapshotLog`。
- `TrackerSnapshotAlignmentLog`。

`Tracker.DebugHost` の `VisionReceiverService` は UDP 復号、未加工情報保存、収録の後に `Tracker.Core.TrackerCoordinator.ProcessPacket` を呼ぶ適合層として残してよい。`Tracker.DebugHost` 固有の診断設定解決結果は `TrackerResolvedOptions` として残すが、`Tracker.Core` の周期処理が受け取る設定形状は `TrackerRuntimeResolvedOptions` に分離し、`Tracker.Core` が `Tracker.DebugHost` 型を参照しないようにする。

### `RUNTIME-HOST-006`: Tracker.DebugHost 生表示の読み取り側断面境界

`RUNTIME-HOST-006` では `Tracker.DebugHost` の生表示を、描画刻みごとの合成読み取り側断面境界へ寄せる。`Home.razor` は `VisionPacketStore` / `TrackedSnapshotStore` を直接注入せず、`VisionLiveDisplaySnapshotProvider` から `VisionLiveDisplayRenderSnapshot` を 1 回取得する。この断面は同一描画刻みの未加工 SSL-Vision 断面、自側追跡済み断面、外部トラッカーの読み取り側断面、比較用 `VisionLiveComparisonRenderSnapshot` を同時に保持する。

`VisionLiveComparisonSnapshotComposer` は保存先を直接読まない。提供側が固定済みの未加工 / 追跡済み / 外部トラッカー断面を渡し、構成器はその値から比較入力元の選択肢、`Layer A/B`、詳細を生成する。これにより `Raw` / `Tracked` / `Compare` の表示は同一描画刻み断面から派生し、比較のために未加工 / 追跡済み保存先を再読取しない。

外部トラッカーは `MultiTrackerManager<TrackerPacketAdapter>` の可変状態を描画経路で直接読まない。`ExternalTrackerSnapshotStore` が管理器の更新事象から通信内容と付加情報を複製済み DTO として保持し、生表示の提供側はその読み取り側断面だけを読む。`TrackerConnectionLibReceiverHostedService` と CaptureOn 記録器は従来どおり管理器の更新経路に接続し、`RUNTIME-HOST-006` では診断標本の補助記録や `Tracker.RuntimeHost` 初期構成へ踏み込まない。

### `RUNTIME-HOST-007`: Tracker.DebugHost 診断標本補助記録の高速経路

`RUNTIME-HOST-007` では `Tracker.RuntimeHost` 初期構成へ踏み込まず、`Tracker.DebugHost` の CaptureOn 収録単位に診断標本の補助記録を追加する。`DiagnosticsSampleHostedService` は画面表示有無に依存しない診断標本周期処理として `VisionLiveDisplaySnapshotProvider` から最新の未加工断面と最新の自側トラッカー断面を固定し、同じ標本記録として `diagnostics-samples.jsonl` へ保存する。標本周期処理の周期は `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` で設定し、既定値は 0.1 秒、0 以下は既定値へ戻す。`Home.razor` の更新刻みは生表示の描画更新だけを担当し、診断記録周期を決めない。収録付加情報は `DiagnosticsSampleSidecarPath` と `DiagnosticsSampleLog` を持つ。

診断再生 / 比較は診断標本補助記録が存在する収録単位では、標本刻みを再生時間軸の主経路にする。`Vision Input` の競技場形状入力元と `ibis tracker` の競技場形状入力元は旧描画断面補助記録ではなく、診断標本記録の意味要約から復元する。旧描画断面補助記録だけを持ち診断標本補助記録を持たない収録単位は非対応 / 機能低下した旧形式として扱い、高負荷な互換経路は復活させない。

### `RUNTIME-HOST-009`: Tracker.RuntimeHost 正常系

`RUNTIME-HOST-009` では `Tracker.RuntimeHost` に画面なし SSL-Vision 受信器と トラッカー運用周期処理を実装する。`Tracker.RuntimeHost` は `VisionReceiver` 節から SSL-Vision 多重配信宛先、UDP 番号、任意の自機 IPv4 接続面指定を読み取り、`Tracker.DebugHost` の `VisionReceiverService` / 未加工情報保存先 / 収録書き込み器 / 診断画面に依存せずに `SSL_WrapperPacket` を受信する。

受信処理は撮像機ごとの最新受信内容保持領域へ、通信内容と受信時刻を保存する。トラッカー運用周期処理はこの保持領域を `RuntimeHost:OperationLoopIntervalMilliseconds` に従う周期で読み取り、未処理の撮像機ごとの最新通信内容を受信時刻順に `TrackerCoordinator.ProcessPacket` へ渡す。同じ撮像機から刻み間に複数の通信内容が届いた場合は最新だけを残し、異なる撮像機の通信内容は単一最新値の上書きで落とさない。実行周期は実装内の固定値にせず、`RuntimeHostOptions` の検証済み設定値だけから決める。

`Tracker.RuntimeHost` は `Tracker` 節から トラッカー有効化、入力元名、uuid、UDP 発行有効化、設定組単位の発行先、追跡処理設定を解決して `TrackerRuntimeResolvedOptions` を作る。`Tracker.Core` 側の `TrackerCoordinator`、`TrackedSnapshotStore`、`ITrackerPacketPublisher` / `UdpTrackerPacketPublisher`、`TrackerPacketGenerator` を依存性注入で組み立て、確定済み処理時点ごとに公式 `TrackerWrapperPacket` を発行し、同じ共有境界の最新トラッカー断面を更新する。

起動時の設定組選択は `appsettings` の `Tracker:ActiveProfileName` を既定にするが、運用時の切り替え確認では `Tracker.RuntimeHost` の CLI 引数 `--profile <name>` または `--profile=<name>` がこれを上書きできるようにする。CLI 引数の解決は .NET の命令行設定提供器と切り替え対応表を使い、将来の短縮選択肢追加時も同じ対応表に増やせる形にする。CLI 設定組上書きは `Tracker:Profiles:<name>` の既存設定組だけを選択し、設定組定義自体は CLI から生成しない。不正な空指定や値なし指定は起動時に明示失敗させ、誤って `default` 設定組へ代替しない。

`Tracker.DebugHost` が読む最新トラッカー断面は、`Tracker.RuntimeHost` から `Tracker.DebugHost` 構成単位へ直接依存して公開しない。`Tracker.DebugHost` 側は公式トラッカー通信内容の発行 / 受信経路、または `Tracker.Core` の共有実行境界に沿った読み取り側断面を読む側として成立させる。`RUNTIME-HOST-009` では `Tracker.RuntimeHost` の正常系を実行可能契約で固定し、`Tracker.DebugHost` 画面 / 診断再生 / 収録表示の手動証跡は `RUNTIME-HOST-010` に残す。

## 設計資料配置

設計資料は `Tracker/Design/` を正本の根とする。

- `Tracker/Design/Core/`: トラッカー追跡処理 / 契約 / 純粋な判定処理。
- `Tracker/Design/DebugHost/`: 画面機能、診断、未加工映像表示、収録 / 再生。
- `Tracker/Design/RuntimeHost/`: `Tracker.RuntimeHost`、実行単位分離、将来の AutoRef 動作形態。
- `Tracker/Design/Archive/`: 旧追跡文書の保存先。現在の追跡対象ではない。

## 対象外

- AutoRef 判定処理の実装。
- 審判処理相当の判定規則処理。
- 旧診断記録形式の完全互換。
- `BreakingChanges` の作成。

## 試験方針

- `Tracker.RuntimeHost` が画面機能構成単位を参照しないことを構成単位参照 / 依存関係試験で固定する。
- `Tracker.RuntimeHost` の トラッカー運用周期処理が診断記録 / 再生 API を直接呼ばないことを契約試験で固定する。
- `Tracker.DebugHost` が トラッカー出力を読む側であり、トラッカー運用周期処理を画面描画刻みから駆動しないことを契約試験で固定する。
- 診断標本刻みが トラッカー確定済み処理時点の周期に依存しないことを回帰試験で固定する。

[^本番実行体]: `Tracker.RuntimeHost`: トラッカー運用処理と将来の AutoRef 動作形態を同一実行単位で動かす本番寄りの画面なし実行体。
[^将来動作形態]: AutoRef 動作形態: 審判処理相当の判定処理を トラッカー と同一実行単位で動かす将来形態。今回の実装対象ではない。
[^同一実行単位]: 同一実行単位: トラッカー と将来の AutoRef 判定処理を実行単位外通信なしで同じ OS 実行単位内に置く実行形態。
[^画面なし実行体]: 画面なし実行体: 画面機能を持たず、入出力と実時間処理を主目的に起動する実行体。
[^画面機能]: 画面機能: ブラウザーで見る確認 / 診断画面。`Tracker.RuntimeHost` の実時間処理から分離する。
[^診断再生]: 診断再生: 保存済み標本 / 記録を `Tracker.DebugHost` 側で再生し、未加工入力と トラッカー出力を比較する確認機能。
[^収録表示]: 収録表示: 保存済み収録単位の内容を確認する表示機能。
[^確認用実行体]: `Tracker.DebugHost`: 旧 `Tracker.Server` から改名した確認用実行体。画面機能、診断、収録 / 再生、比較表示を担当する。
[^トラッカー運用処理]: トラッカー運用処理: SSL-Vision 入力から トラッカー状態を更新し、公式トラッカー通信内容を発行する実時間処理。
[^未加工映像表示]: 未加工映像表示: SSL-Vision の検出結果 / 競技場形状を競技場上に表示する `Tracker.DebugHost` の表示機能。
[^トラッカー追跡処理]: トラッカー追跡処理: 未加工検出結果からボール / ロボットの追跡済み状態を決定的に生成する `Tracker.Core` 側の追跡処理。
