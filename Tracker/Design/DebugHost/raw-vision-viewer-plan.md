# SSL_WrapperPacket Raw Vision Viewer 設計

## 目的

`Tracker.DebugHost`[^tracker-debug-host] が `SslProto` の生成型を使って SSL-Vision の `SSL_WrapperPacket` 受信データを直接受け取り、最新の検出情報と競技場形状情報を Blazor UI 上で可視化できるようにする。旧プロジェクト名は `Tracker.Server` だが、現行のプロジェクト名、名前空間、起動経路は `Tracker.DebugHost` とする。

## スコープ

- 設定された映像入力の受信先に結び付く UDP 常駐サービスを追加する
- マルチキャストアドレスが設定されている場合はマルチキャスト参加を行う
- `SSL_WrapperPacket.Parser.ParseFrom` で受信データを復号する
- 最新受信データ、最新検出情報、最新競技場形状、受信メタデータ、受信数、エラー数を単一の保持先に保存する
- `/` に競技場 SVG、検出情報、競技場形状のキャリブレーション、未加工受信データの JSON を表示する
- 画面遷移は `raw vision viewer` 中心に保つ
- 集約表示とカメラ別最新フレーム表示の両方を raw vision UI で扱う

## 非スコープ

- `TrackerConnectionLib` は使わない
- 受信データの永続化はしない
- 未加工検出を超える追跡、フィルタリング、競技場モデル解釈は入れない
- 本番寄りのトラッカー運用と将来 AutoRef mode は `Tracker.RuntimeHost` の責務とし、`Tracker.DebugHost` の Web 描画と診断ログ記録から切り離す

## 設定

`appsettings.json` の `VisionReceiver` セクションを使う。

- `MulticastAddress`: 既定値 `224.5.23.2`
- `Port`: 受信ポート
- `InterfaceAddress`: マルチキャスト参加に使うローカル IPv4 アドレス。未設定時は候補インターフェイスを自動解決する
- `Profiles.<name>`: プロファイルごとの受信設定上書き。`MulticastAddress` / `Port` / `InterfaceAddress` を同名トラッカープロファイルに追従させたい場合に使う

解決規則:

- 起動時は `Tracker:ActiveProfileName` と同名の `VisionReceiver:Profiles.<name>` を優先する
- 同名プロファイルが無い場合は最上位の `VisionReceiver` 値をそのまま使う
- 実行中のトラッカープロファイル切替完了後は、受信設定も同じプロファイル名で再解決し、必要ならソケットを開き直す

## 受信設計

`VisionReceiverService` は常駐バックグラウンドサービスとして動作する。

- IPv4 UDP ソケットを作成する
- アドレス再利用を有効にする
- `IPAddress.Any` と設定ポートに結び付ける
- キャンセルされるまで受信データを継続受信する
- 受信設定が切り替わったら現在の受信ループを停止し、新しい設定でソケットを開き直す

設定されたアドレスがマルチキャストの場合、グループ参加は次の規則で解決する。

- `InterfaceAddress` が設定されている場合、その IPv4 アドレスのみを使う
- 未設定の場合、利用可能なローカル IPv4 インターフェイスを列挙して順に参加を試行する
- 少なくとも 1 つ成功すれば受信開始を継続する
- 一部インターフェイスの失敗は警告ログに残す

復号成功時は `VisionPacketStore` を更新し、失敗時はエラー数を増やし、直前の正常状態を保持する。

## 状態保持設計

`VisionPacketStore` は UI 参照用のスレッドセーフな状態を保持する。

- 最新のラッパー受信データ
- 最新の検出フレーム
- カメラごとの最新検出スナップショット
- 集約表示用に統合したボール、黄色ロボット、青色ロボット
- 最新の競技場形状データ
- 受信数
- エラー数
- リモート受信先
- 受信時刻
- 最新の解析エラーメッセージ

UI には不変スナップショットを返し、描画中にロックを保持しない。

## プロトコル入力

`raw vision viewer` が直接使う主なプロトコル入力は次の通り。

- `SSL_WrapperPacket`
  - 受信データ全体
  - `Detection` と `Geometry` を内包する最上位データ
- `SSL_DetectionFrame`
  - カメラ単位の未加工検出
  - `FrameNumber`, `CameraId`, `Balls`, `RobotsYellow`, `RobotsBlue` を使う
- `SSL_DetectionBall`
  - ボール描画と詳細表示に使う
  - 主に `X`, `Y`, `Z`, `PixelX`, `PixelY`, `Confidence`
- `SSL_DetectionRobot`
  - ロボット描画と詳細表示に使う
  - 主に `RobotId`, `X`, `Y`, `Orientation`, `PixelX`, `PixelY`, `Confidence`
- `SSL_GeometryData`
  - 競技場形状全体
  - `Field` と `Calib` を使う
- `SSL_GeometryFieldSize`
  - 競技場の寸法と線分、円弧情報
  - 主に `FieldLength`, `FieldWidth`, `GoalWidth`, `GoalDepth`, `BoundaryWidth`, `BoundaryWidthGoalLine`, `PenaltyAreaDepth`, `PenaltyAreaWidth`, `CenterCircleRadius`, `LineThickness`, `FieldLines`, `FieldArcs`
- `SSL_GeometryCameraCalibration`
  - キャリブレーション表の表示に使う
  - 主に `CameraId`, `FocalLength`, `PrincipalPointX`, `PrincipalPointY`, `PixelImageWidth`, `PixelImageHeight`

## 競技場投影

`VisionFieldProjection` は競技場のミリメートル座標を SVG 表示領域に写像する。

- 競技場形状がある場合はその競技場寸法を使う
- 競技場形状がまだない場合は既定の競技場寸法を使う
- `(0, 0)` は表示領域の中心に対応する
- 競技場本体だけでなく、境界領域とゴール奥行きが見切れないよう外側余白を加味する

## コンポーネント構成

`raw vision viewer` の主要コンポーネントは次の通り。

- `Home.razor`
  - 画面全体の親
  - `VisionPacketStore.GetSnapshot()` の結果を定期取得し、コンパクトなヘッダーと左右ペインを構成する
- `VisionFieldCanvas.razor`
  - 競技場 SVG の親コンポーネント
  - 拡大縮小と移動状態、境界領域背景、競技場本体、子マーカーの配置、軸の重ね表示、カーソル座標の重ね表示を担当する
- `VisionFieldLines.razor`
  - 競技場の線分、円弧、ゴールの描画を担当する
  - `FieldLines` / `FieldArcs` がある場合はそれを優先し、不足時は競技場形状の寸法から代替描画する
- `VisionBallMarker.razor`
  - `SSL_DetectionBall` 1 件を SVG の円として描く
- `VisionRobotMarker.razor`
  - `SSL_DetectionRobot` 1 件をロボット形状として描く
  - チーム色、前面の隙間、先端マーカー、ラベルを担当する
- `VisionDetailsPanel.razor`
  - JSON、ボール、ロボット、競技場形状キャリブレーションの右ペイン表示を担当する
- `VisionPalette.cs`
  - チーム色とマーカー線色の定義を一箇所に集約する
- `VisionRenderOptions.cs`
  - ロボット半径など、将来設定から変更したい描画パラメータの受け口

## コンポーネント入力

各コンポーネントが受ける最小入力は次の通り。

### `Home.razor`

- `VisionPacketSnapshot`
  - 保持先から取得した UI 用スナップショット
- `selectedViewKey`
  - 集約表示とカメラ表示の切替状態
- サイドバー折りたたみ状態を前提に、競技場優先の主表示を構成する

### `VisionFieldCanvas.razor`

- `SSL_GeometryData? Geometry`
- `IReadOnlyList<SSL_DetectionBall> Balls`
- `IReadOnlyList<SSL_DetectionRobot> RobotsYellow`
- `IReadOnlyList<SSL_DetectionRobot> RobotsBlue`
- `VisionRenderOptions RenderOptions`
- カーソル座標表示に必要なホバー状態と描画領域サイズ

### `VisionFieldLines.razor`

- `VisionFieldProjection Projection`
- `SSL_GeometryFieldSize? Field`

### `VisionBallMarker.razor`

- `VisionFieldProjection Projection`
- `SSL_DetectionBall Ball`

### `VisionRobotMarker.razor`

- `VisionFieldProjection Projection`
- `SSL_DetectionRobot Robot`
- `string ClassName`
- `VisionRenderOptions RenderOptions`

### `VisionDetailsPanel.razor`

- `string ViewLabel`
- `string FrameLabel`
- `string CameraLabel`
- `string SourceLabel`
- `string RawJson`
- `IReadOnlyList<VisionCameraSnapshot> Cameras`
- `string SelectedViewKey`
- `EventCallback<string> OnSelectView`
- `SSL_GeometryData? Geometry`
- `IReadOnlyList<SSL_DetectionBall> Balls`
- `IReadOnlyList<SSL_DetectionRobot> RobotsYellow`
- `IReadOnlyList<SSL_DetectionRobot> RobotsBlue`

## 課題 #10 分割 / 重ね表示 source 設計

Vision 画面の分割表示と重ね表示で選択できる表示元[^source-term]候補は次の 4 種類に固定する。

- `Raw Aggregate`[^raw-aggregate]
  - `VisionPacketStore` の集約表示用スナップショットを使う
  - カメラごとの最新検出を UI 表示用に統合した raw SSL-Vision の表示元として扱う
- `Raw Camera`[^raw-camera]
  - `VisionPacketStore` のカメラごとの最新検出スナップショットを使う
  - カメラ ID を選択肢の内部キーに含め、表示ラベルだけで表示元を識別しない
- `Tracked`[^tracked-source]
  - ibis トラッカーの `TrackedSnapshotStore` から得た最新の `TrackerFrame` を `TrackedVisionViewState` 相当の競技場描画 DTO へ変換して使う
  - 未加工検出ではなく ibis トラッカー出力として扱う
- `3rd party tracker`[^third-party-tracker]
  - `MultiTrackerManager<TrackerPacketAdapter>`[^multi-tracker-manager] から受けた外部トラッカーのライブ状態[^live-state]を使う
  - UI は `MultiTrackerManager` の可変状態[^mutable-state]を直接読まず、ライブ UI[^live-ui] 用の不変スナップショット保持先[^immutable-snapshot-store]または合成器を必ず挟む

ライブ比較では、厳密な同一受信データ時刻[^packet-timestamp]や全表示元共通の同一受信コールバック[^receive-callback]は要求しない。raw SSL-Vision、ibis トラッカー、3rd party トラッカーは受信ストリームと更新コールバックが異なるため、ここを契約にすると通常表示の実装が過剰に結合する。採用方針は、1 回の `UI render tick`[^ui-render-tick] で各表示元の最新不変スナップショット[^immutable-snapshot]を固定し、その合成スナップショットを分割表示と重ね表示の Layer A/B に渡すことである。

RUNTIME-HOST-006 以降のライブ表示では、`Home.razor` は未加工入力や追跡済み状態の保持先を直接注入せず、`VisionLiveDisplaySnapshotProvider` から `VisionLiveDisplayRenderSnapshot` を取得する。`VisionLiveComparisonSnapshotComposer` は保持先を再読取せず、固定済み合成スナップショットから比較用スナップショットと画面状態を生成する。これにより `Raw`、`Tracked`、`Compare` は同じ描画更新時点のスナップショットから派生する。

`UI render tick` の合成スナップショットは次を保持する。

- 描画更新 ID[^render-tick-id]または `SampledAt`
- 表示元キー[^source-key]と表示ラベル[^display-label]
- 表示元ごとの受信時刻[^receive-timestamp]、フレーム時刻[^frame-timestamp]、受信数など、時刻差を説明するメタデータ
- ボール、ロボット、競技場形状参照[^geometry-reference]、欠落理由を含む不変の表示元スナップショット

3rd party トラッカーのライブ接続では、`MultiTrackerManager<TrackerPacketAdapter>` から外部トラッカー受信データを受け、表示元の同一性は UUID を優先して集約する。同じ `uuid` のトラッカーはリモート受信先が異なっても 1 つの表示元として扱い、同じ `uuid` グループ内で最新 `ReceivedAt` のスナップショットを代表として描画する。ボールとロボットは複数受信先の間で和集合結合しない。`uuid` が空または不明な場合だけ、表示元名、リモート受信先、代替規則で識別する。`uuid` が異なるトラッカーは表示元名が同じでも別表示元とし、同じ表示ラベルが複数残る場合は短い `uuid` または受信先を補助表示して UI 上で区別できるラベルにする。ただし `TrackerState` や protobuf 受信データ参照を UI が直接保持しない。`ExternalTrackerSnapshotStore` が管理器の更新イベントから受信データとメタデータを複製し、`VisionLiveDisplaySnapshotProvider` が描画更新時点でその読み取り側 DTO を固定する。`TrackerPacketSnapshotLogWriter` や CaptureOn の補助書き込み器を Vision live store[^live-store] として使う方針は不採用とする。これは CaptureOn セッション保存用の仕組みであり、CaptureOff の通常ライブ Vision 画面では更新表示元として成立しないためである。

競技場形状の基準は未加工入力の競技場形状優先とする。`Raw Aggregate` または選択中の `Raw Camera` で得られる最新 `SSL_GeometryData` を重ね表示全体の競技場基準に使い、未加工入力の競技場形状がまだ無い場合のみ `Tracked` の競技場形状へ代替する。`3rd party tracker` の受信データから競技場形状を復元する方針は不採用とする。外部トラッカー受信データは比較対象の物体状態であり、競技場キャリブレーションの責任を持たせると表示元ごとの座標比較の意味が曖昧になる。

分割表示と重ね表示の UI 挙動は診断画面に寄せる。

- 分割モードは Layer A と Layer B を左右に並べる
- 重ね表示モードは 1 つの競技場に Layer A/B を重ねる
- 重ね表示と分割表示を相互に共通化するのではなく、それぞれのモードに必要な画面構造は分けて保つ。そのうえで、Vision live と diagnostics の競技場描画部[^field-rendering-part]は同じ責務境界に揃える
- 分割用競技場コンポーネント[^split-field-component]と重ね表示用競技場コンポーネント[^overlay-field-component]は別物として切り出す。重ね表示と分割表示を 1 つのコンポーネントへ統合する意味ではない
- Vision live と diagnostics は、分割表示では同じ分割用競技場コンポーネントを使い、重ね表示では同じ重ね表示用競技場コンポーネントを使う
- 競技場、境界、競技場形状、マーカーの描画責務は分割用または重ね表示用の競技場コンポーネントへ置き、表示元セレクター、時刻メタデータ、欠落理由、凡例、レイアウト外枠は Vision live と diagnostics のページ、外枠、付加コンポーネント側に持たせる
- 重ね表示モードでは層ごとに独立した `VisionFieldCanvas` を重ねる方針は不採用とする。重ね表示用競技場コンポーネントは競技場と競技場形状を 1 回だけ描き、Layer A/B のボールとロボットを層グループ[^layer-group]として同じ表示領域状態[^viewport-state]配下に描く
- 分割モードでは左右の競技場は独立表示領域[^split-independent-viewport]として扱う。左右の移動と拡大縮小の同期は要件にしないが、Vision live split と diagnostics split は同じ分割用競技場コンポーネント、同じマーカー描画方針、同じ競技場形状の代替方針を使う
- 詳細欄は表示元ごとの概要、時刻メタデータ、欠落理由、未加工入力、追跡済み状態、3rd party の違いを確認できる構成にする
- 凡例は診断画面と同じく層名、表示元ラベル、表示切替、準備済み状態と欠落状態を表示する
- 層の表示有無は Layer A/B ごとに切り替えられる
- 重ね表示モードでは diagnostics overlay と同じく Layer A/B を別の強調色[^accent-color]で表示し、競技場マーカーと凡例スウォッチの両方で層を識別できるようにする
- Layer A/B が同じ表示元を選んだ場合は same-source[^same-source] として 1 層表示にまとめ、重複描画で誤差があるように見せない
- 片方の層が欠落していても、準備済みの層は残して表示する
- 欠落層は競技場全体を空にせず、凡例と詳細欄に欠落理由を出す

## 診断時刻同期方針

診断再生と比較は選択中の再生タイムライン tick[^selected-replay-timeline-tick] を同期基準にする。旧形式と現在の制約[^old-format-current-limitation]では、Vision/Input と ibis トラッカーは選択 tick の描画フレームから得たスナップショットを使い、3rd party トラッカーは同じ `ReplayTimelineIndex`[^replay-timeline-index] の `saved-session-alignment`[^saved-session-alignment] 記録を使う。この描画スナップショット[^legacy-render-snapshot-sidecar]経路は `WorldFrameCommitted`[^world-frame-committed] に従うため、トラッカー確定フレーム周期[^tracker-committed-frame-cadence]に制限され、新規記録[^new-capture]の目標経路としては扱わない。

新規記録の診断再生と比較は診断サンプル tick[^diagnostics-sample-tick] を保存単位にする。Diagnostics の `Vision Input` は選択 tick の描画フレームではなく、診断サンプル tick に保存された最新未加工スナップショット[^latest-raw-snapshot]から復元する。ibis トラッカーと 3rd party トラッカーの比較対象は同じ診断サンプル tick に保存された最新トラッカースナップショット[^latest-tracker-snapshot]、または同 tick 以前の `latest-before snapshot` を使う。このため、新規記録では Vision、ibis トラッカー、3rd party トラッカーをトラッカー確定フレーム周期ではなく診断サンプルタイムライン[^diagnostics-sample-timeline]上の比較として扱う。

選択 tick[^selected-tick] に対象 `3rd party tracker` 表示元の対応記録が無い場合でも、表示と比較を消さない。採用方針は、同じ表示元の選択 tick 以前に存在する最新の `latest-before snapshot`[^latest-before-snapshot] を Field source と比較に使うことである。UI と比較表示は照合規則が `latest-before` であること、表示元スナップショットの実際の `receivedAt`、選択 tick との差分、古い状態または latest-before 状態を明示する。これにより、対象表示元が選択 tick で未更新でも、ユーザーは直前まで得られていたトラッカー状態を未加工入力や ibis トラッカーと比較できる。

`latest-before snapshot` を使う場合も、再生と比較の基準タイムラインは選択中の再生タイムライン tick のまま固定する。表示元ごとにタイムラインカーソル[^timeline-cursor]をずらしたり、表示上の選択時刻をトラッカー表示元側へスライドしたりしない。Field と比較表示は「選択 tick に対して、この表示元は直前サンプルを保持している」として表示し、差分は選択 tick と保持した表示元スナップショットの差として扱う。これにより、表示が消えることを避けつつ、時間軸が表示元ごとにずれて異なる時刻のものを同時刻扱いで表示しているように見える状態を避ける。

選択 tick 以前に同じ表示元のスナップショットが一切無い場合だけ、Field source は `CandidateMissing`[^candidate-missing]、比較表示は `NoCandidateSnapshot`[^no-candidate-snapshot] 相当の欠落表示にする。この場合も Field 全体は消さず、準備済みの層は残し、凡例と詳細欄に欠落理由を出す。未来または後続スナップショット[^future-later-snapshot]への代替は行わない。未来 tick のトラッカー状態を現在 tick の比較へ混ぜると、再生タイムラインの因果関係が崩れ、比較差分が実際より良く見えるためである。diagnostics-line alignment[^diagnostics-line-alignment] や nearest timestamp[^nearest-timestamp] は、選択 tick 以前の同一表示元スナップショットを探すための補助索引として使ってよいが、選択 tick より後のスナップショットは候補に含めない。この挙動は既存 diagnostics time-sync regression contract として維持し、Tracker.RuntimeHost / Tracker.DebugHost 分離スコープでは新しい `RAW-VISION-*` タスクを追加しない。

## 診断ループ分離方針

ループ分離[^loop-isolation]の中心目的は、トラッカー運用ループ[^tracker-operation-loop]を Web サーバーのライブ表示処理[^web-server-live-display-processing]と診断ログ記録および再生処理[^diagnostics-logging-replay-processing]の両方から隔離することである。修正は UI-only display correction[^ui-only-display-correction] ではなく、保存と再生の入力周期をトラッカー確定フレーム周期から切り離す設計として扱う。

3 つのループの責務は次の通り分ける。

- トラッカー運用ループは未加工受信データ、プロファイル、制御入力をトラッカーエンジンへ渡し、トラッカー状態更新、公開、最新トラッカースナップショットの公開までを担当する。診断補助ファイルへのフレーム保存をこのループの `WorldFrameCommitted` コールバックへ直接結合しない。
- サーバーのライブ表示処理は `UI render tick` ごとに未加工入力、追跡済み状態、3rd party トラッカーの最新不変スナップショットを固定し、通常 Vision 画面の分割表示と重ね表示を描画する。これは表示用ループであり、診断ログ記録のサンプル周期を決めない。
- 診断ログ記録および再生処理はトラッカー運用ループから直接書き込まれた描画フレームを読むのではなく、`Tracker.DebugHost` 常駐サービスの独立した診断サンプル tick で最新未加工スナップショットと最新トラッカースナップショットを固定し、`diagnostics-samples.jsonl` 診断サンプル補助ファイル[^diagnostics-sample-sidecar]に保存する。tick 周期は `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` で設定し、既定値は `100` ms、0 以下は既定値へ戻す。記録メタデータは `DiagnosticsSampleSidecarPath` と `DiagnosticsSampleLog` を持ち、再生はこのサンプルタイムラインから `Vision Input` と `ibis tracker` の意味要約を通常経路として復元する。

ログ互換性はこのループ分離の必須要件にしない。新規記録の性能と周期維持を優先し、旧描画スナップショット補助ファイルに対する高コストな互換層は設計しない。旧形式の描画スナップショット補助ファイルしか持たないセッションは、この新機能では unsupported / degraded legacy session[^degraded-legacy-session] として扱ってよい。旧形式を読む場合も、旧経路がトラッカー確定フレーム周期に制限されることを UI と詳細欄で説明できれば足りる。

診断サンプル tick の周期はトラッカー確定フレーム周期と同義にしない。raw SSL-Vision の最新スナップショットがトラッカー確定より高頻度に更新される場合、新しいログ記録は未加工スナップショット周期[^raw-snapshot-cadence]を失わない保存境界を持つ。トラッカースナップショットはサンプル tick 時点の最新を読むが、トラッカー運用自体をサンプル tick から駆動しない。これにより、トラッカー運用、サーバーのライブ表示、診断ログ記録と再生のいずれかの負荷や周期が、他のループのユーザー可視表示や保存周期を支配しない。

## UI 方針

ルートページでは次を表示する。

- 受信状態と最新受信メタデータ
- 競技場優先の SVG 表示
- raw JSON
- ボール、ロボット、競技場形状キャリブレーションの詳細表示

競技場の表示方針は `RoboCup-SSL/ssl-vision-client` の方向性を踏襲する。

- 競技場描画領域を主表示にする
- 境界領域を考慮した競技場背景
- ホイール拡大縮小とドラッグ移動
- 競技場面積を優先するため、画面タイトルは省略し、表示元セレクターは競技場上端から外す
- +X / +Y 方向が分かる軸の重ね表示を競技場上に固定表示する
- カーソル座標はカーソルの上下で表示位置を切り替え、競技場視認性を落とさない
- デスクトップのサイドバーは viewer 表示面積確保のため折りたたみ可能にする

## レイアウト追補

- `Home.razor` は大きなタイトルブロックを持たず、状態表示と主表示を優先する
- 表示元セレクターは `VisionDetailsPanel.razor` 側へ移し、競技場の縦方向面積を確保する
- `VisionFieldCanvas.razor` は競技場本体に加えて軸の重ね表示とカーソル座標の重ね表示を管理する
- カーソル座標の重ね表示はプロトコル由来の競技場形状と `VisionFieldProjection` の逆写像から求める
- サイドバー折りたたみはレイアウトレベルで扱い、viewer 専用コンポーネントへ閉じ込めない
- `Diagnostics.razor` の描画スナップショット表示は、Vision Input / トラッカー Output の競技場表示領域と下部詳細領域の境界をドラッグで変更できるようにする
- diagnostics の競技場表示と詳細欄の比率は表示領域高さに依存した固定上限だけにせず、4K など高解像度環境で競技場を大きく広げられる上限を持つ
- 詳細領域は縮小時も最低高さとスクロールを維持し、Vision Input / トラッカー Output の文字列確認を壊さない
- `Diagnostics.razor` の左側フレームタイムラインは、右側詳細欄との境界をドラッグして幅を変更できるようにする
- フレームタイムラインは右側の競技場表示と詳細欄を広げたい場合に小さくでき、最小幅でもフレーム選択操作と省略表示を維持する
- `MainLayout.razor.css` と `NavMenu.razor.css` は raw vision / diagnostics の濃色 green UI と同じ配色・密度を使い、既定 Blazor テンプレート由来の青紫グラデーションや浮いた画面遷移表現を残さない
- 側面ナビゲーションの選択中、ホバー、折りたたみ、モバイル切替は既存操作を維持しつつ、viewer と同じ境界線、背景、文字色の階調で表現する
- `Diagnostics.razor` のタイムラインつまみには再生、停止、早送り操作を置き、選択エントリを順方向に進める
- 通常再生はログエントリの時刻差分を使い、上限丸めなしで実際の記録速度に合わせて進める
- 再生中は再生ボタンを停止ボタン表示へ切り替え、早送り中は早送りボタンを停止ボタン表示へ切り替える
- 再生/早送りは最後のエントリに到達したら停止して先頭エントリに戻し、ログ切替やエントリ不在時には再生状態を停止状態へ戻す

## テスト方針

- `VisionPacketStore` が検出のみの受信データを保持できる
- `VisionPacketStore` が競技場形状のみの受信データを保持できる
- `VisionPacketStore` がカメラごとの最新状態と集約状態を返せる
- `VisionPacketStore` が復号失敗時にエラー数を増やす
- `VisionReceiverService.ResolveMulticastJoinAddresses` が設定済みアドレスと自動探索を正しく処理する
- `VisionFieldProjection` が `(0, 0)` を中心に写像する
- `VisionFieldProjection` が競技場、境界領域、ゴール奥行きを含めても表示領域内に収める
- diagnostics render snapshot の競技場表示と詳細欄の可変高さは、最小値、最大値、ドラッグ差分、丸めを単体テストで確認する
- diagnostics frame timeline の可変幅は、最小値、最大値、ドラッグ差分、丸めを単体テストで確認する
- diagnostics timeline playback は、次の索引計算、最後での停止と先頭復帰、通常再生と早送りの進行、時刻差分に基づく実速度間隔を単体テストで確認する
- Vision split / overlay contract は、表示元候補、分割表示と重ね表示のモード、Layer A/B の表示元選択、層の表示有無、same-source の 1 層化、欠落層があっても準備済み層を残す挙動、diagnostics 寄せの凡例と詳細欄を単体テストで先に固定する
- Vision overlay color contract は、Layer A/B の競技場マーカーと凡例が diagnostics と同じ考え方の別強調色を持ち、same-source collapse 時は 1 層の色へまとまることを単体テストで先に固定する
- Vision / diagnostics overlay field contract は、重ね表示モードが層ごとに独立した競技場描画領域を重ねず、Vision live overlay と diagnostics overlay が同じ重ね表示用競技場コンポーネントの単一表示領域状態配下で Layer A/B を描くことを単体テストで先に固定する
- Vision / diagnostics split field contract は、左右競技場の表示領域は独立のまま、Vision live split と diagnostics split が同じ分割用競技場コンポーネントと同じマーカー、競技場形状の描画方針を使うことを単体テストで先に固定する
- Vision live comparison contract は、1 回の `UI render tick` で `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` の最新不変スナップショットを固定し、後続の保持先更新で描画中スナップショットが変化しないことを単体テストで先に固定する
- 3rd party トラッカー live source contract は、`MultiTrackerManager<TrackerPacketAdapter>` の可変状態を UI が直接読まず、不変スナップショット保持先または合成器を通して表示元候補と競技場 DTO を作ることを単体テストで先に固定する
- diagnostics time-sync regression は、選択 `ReplayTimelineIndex` に対象 3rd party 表示元の対応記録が無い場合でも、選択中の再生タイムライン tick 自体は動かさず、同じ表示元の選択 tick 以前の `latest-before snapshot` を Field source と比較に使い、照合規則、表示元スナップショットの実際の `receivedAt`、選択 tick との差分、古い状態または latest-before 状態を表示することを単体テストで先に固定する
- diagnostics missing regression は、選択 tick 以前に同じ表示元のスナップショットが一切無い場合だけ `CandidateMissing` / `NoCandidateSnapshot` 相当になり、未来または後続スナップショットへ代替せず、準備済み層は残ることを単体テストで先に固定する
- RUNTIME-HOST-002 の TDD contract は、`Tracker.RuntimeHost` / `Tracker.DebugHost` のプロジェクト依存境界と `Tracker.DebugHost` の読み取り側責務を固定する
- RUNTIME-HOST-003 の TDD contract は、診断サンプル境界と旧形式の性能低下契約を固定する。診断サンプル tick がトラッカー確定フレーム周期に依存せず最新未加工スナップショットと最新トラッカースナップショットを保存すること、Diagnostics `Vision Input` が描画スナップショット補助ファイルではなく診断サンプル補助ファイルから復元されること、`Tracker.DebugHost` の診断ログ記録および再生処理が `Tracker.RuntimeHost` のトラッカー運用ループやサーバーのライブ表示処理の `UI render tick` スナップショット契約を壊さないこと、旧描画スナップショット補助ファイルだけを持つセッションは unsupported / degraded legacy session として扱われ高コストな互換保証を持たないことを単体テストで先に固定する

## 前提

- raw vision information は `vision/ssl_vision_wrapper.proto` の `SSL_WrapperPacket` を指す
- 既定の SSL-Vision マルチキャスト受信先は一般には `224.5.23.2:10006` だが、実行時設定で変更できる
- 既存の無関係な作業ツリー変更は保護する

[^source-term]: source: 画面に描画する balls / robots / geometry の由来を表す設計上の概念。Vision split / overlay では Layer A/B で何を選び、何と比較するかを決める単位であり、`Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` が候補になる。
[^raw-aggregate]: Raw Aggregate: raw SSL-Vision の camera ごとの latest detection を UI 表示用に統合した source。複数 camera の balls / robots をまとめて Vision 画面で見るための候補で、camera 単体ではなく集約表示を選ぶときに使う。
[^raw-camera]: Raw Camera: 特定 camera ID の raw SSL-Vision latest detection を表示する source。camera ごとの見え方や検出差を確認するための候補で、選択肢の内部 key には camera ID を含める。
[^tracked-source]: Tracked: ibis トラッカー が生成した `TrackerFrame` を Vision 表示用 DTO に変換した source。raw detection ではなく ibis トラッカー 出力を Layer A/B や overlay 比較へ出すために使う。
[^third-party-tracker]: 3rd party tracker: ibis トラッカー 以外の外部 トラッカー から受けた トラッカー packet source。外部 トラッカー の出力を raw SSL-Vision や ibis トラッカー と比較するための候補。
[^live-state]: live state: Vision split / overlay の `3rd party tracker` で、実行中に外部 トラッカー から最後に受けた状態。Layer A/B で `3rd party tracker` を選んだときの描画元になるが、UI はこの状態を直接保持せず、snapshot 化された表示用データを読む。
[^mutable-state]: mutable state: `MultiTrackerManager` 内で後から内容が変わる状態オブジェクト。Vision 画面の split / overlay では、描画中に値が変わることを避けるため、この状態を直接読まず snapshot 化してから比較に使う。
[^live-ui]: live UI: CaptureOn の保存 replay ではなく、CaptureOff の通常 Vision 画面で現在受信している source を比較する画面。raw SSL-Vision、ibis トラッカー、3rd party トラッカー の現在値を split / overlay で見比べる。
[^immutable-snapshot-store]: immutable snapshot store: Vision 画面の通常表示で、Layer A/B に渡す前に描画中に変わらない表示用データを保持する境界。外部 トラッカー の更新と field 描画のタイミングを切り離すために使う。
[^packet-timestamp]: packet timestamp: packet に入っている時刻。Vision split / overlay の通常表示では、raw SSL-Vision、ibis トラッカー、3rd party トラッカー の packet timestamp が厳密に同一であることは比較条件にしない。
[^receive-callback]: receive callback: packet 到着時の受信処理呼び出し。通常表示では、すべての source が同じ receive callback で更新されたことを Layer A/B の比較条件にしない。
[^ui-render-tick]: UI render tick: Blazor UI が 1 回の表示更新を行う単位。この tick 内で各 source の latest snapshot を固定し、split / overlay へ渡す。
[^immutable-snapshot]: immutable snapshot: 描画中に内容が変わらないよう clone / DTO 化された読み取り専用 snapshot。後続の store 更新で描画中 snapshot が変化しないことを保証するために使う。
[^render-tick-id]: render tick ID: `UI render tick` ごとの表示更新単位を識別する ID。Layer A/B が同じ表示更新で固定された snapshot を見ていることを説明するために使う。
[^source-key]: source key: Vision 画面の source を識別する内部 key。`Raw Camera` では camera ID を含め、display label だけで source を取り違えないようにする。
[^display-label]: display label: UI に出す表示名。source key と違い内部識別には使わず、details や選択肢でユーザーに source を読ませるために使う。
[^receive-timestamp]: receive timestamp: packet を受け取った時刻。details で source 間の時刻差を確認し、`latest-before snapshot` が selected tick からどれだけ古いかを説明するために使う。
[^frame-timestamp]: frame timestamp: detection frame や トラッカー frame 側の時刻。receive timestamp と併せて、source 同士の比較がどの時刻情報に基づくかを details で説明する。
[^geometry-reference]: geometry reference: field 描画に使う geometry 参照。overlay では raw geometry を優先し、無い場合だけ `Tracked` の geometry へ fallback するため、source 同士の座標比較の基準を示す。
[^source-label]: source label: legend や details に出す source 名。ユーザーが Layer A/B でどの raw / tracked / 3rd party トラッカー を選んでいるか確認するために使う。
[^live-store]: live store: 通常 Vision 画面の現在表示を更新するための store。CaptureOn session 保存用の `TrackerPacketSnapshotLogWriter` や sidecar writer はこの役割に使わない。
[^accent-color]: accent color: Layer A/B を見分けるために marker の stroke や legend swatch に使う強調色。Issue #10 の Vision overlay では diagnostics overlay と同じ考え方で、重なった layer を色で判別できるようにする。
[^field-rendering-part]: field 描画部: field の背景、boundary、geometry line、ball marker、robot marker を SVG に描く部品境界。source 選択や詳細 metadata 表示ではなく、field 上に何をどう描くかを担当する部分を指す。
[^split-field-component]: split 用 field コンポーネント: split mode の左右それぞれの field を描くコンポーネント。左右の viewport は独立させるが、Vision live と diagnostics は同じコンポーネント境界を使う。
[^overlay-field-component]: overlay 用 field コンポーネント: overlay mode で 1 つの field 上に Layer A/B を重ねて描くコンポーネント。field と geometry を layer ごとに描き直さず、object marker だけを Layer A/B の group として重ねる。
[^overlay-field-rendering]: overlay field 描画部: overlay 用 field コンポーネントと同じ責務範囲を指す。field と geometry を layer ごとに描き直さず、object marker だけを Layer A/B の group として重ねる。
[^layer-group]: layer group: 同じ field 上で Layer A または Layer B に属する balls / robots をまとめる SVG group。group ごとに visibility や accent color を持たせるが、pan / zoom の基準は overlay field 描画部のものを共有する。
[^viewport-state]: viewport state: field 表示の zoom、pan、drag 中の移動量など、画面上で field をどの位置と倍率で見るかを表す状態。overlay mode では layer ごとに別々に持たず、1 つの viewport state を共有する。
[^split-independent-viewport]: split の独立 viewport: split mode の左右 field が、それぞれ別の表示位置と倍率を持つこと。左右は比較対象を並べる表示なので、片方を drag してももう片方を自動追従させる要件ではない。
[^same-source]: same-source: Layer A/B が同じ source を選んだ状態。Vision overlay では重複描画で誤差があるように見せないため、1 layer 表示にまとめる。
[^multi-tracker-manager]: MultiTrackerManager / TrackerPacketAdapter: `MultiTrackerManager` は `TrackerConnectionLib` の トラッカー state 管理コンポーネントで、own / external / unknown トラッカー の latest state を保持する。`TrackerPacketAdapter` は 3rd party トラッカー packet を `MultiTrackerManager` で扱うための adapter。
[^selected-replay-timeline-tick]: selected replay timeline tick: diagnostics replay でユーザーが現在選択している再生タイムライン上の基準 tick。Vision/Input、ibis トラッカー、3rd party トラッカー を比較するとき、この tick は source ごとへ移動させない。
[^replay-timeline-index]: ReplayTimelineIndex: diagnostics replay の selected timeline tick を識別する index。`saved-session-alignment` record と結び付けて、どの tick の比較かを特定する。
[^saved-session-alignment]: saved-session-alignment: CaptureOn session に保存された、replay timeline tick と トラッカー source snapshot の対応 record 群。diagnostics replay で 3rd party トラッカー の snapshot を同じ selected replay timeline tick に合わせるために使う。
[^alignment-sidecar]: alignment sidecar: CaptureOn session の主ログとは別に保存される対応付け用の補助ファイル。diagnostics replay で、選択中の replay timeline tick と トラッカー source snapshot の関係を後から復元するために使う。
[^alignment-record]: alignment record: alignment sidecar 内の 1 件の対応付け。diagnostics replay で特定の replay timeline tick を選んだとき、どの トラッカー source snapshot を比較に使うかを示す。
[^selected-tick]: selected tick: selected replay timeline tick の短縮表現。本文で「選択 tick」と書く場合も同じ意味を指す。
[^latest-before-snapshot]: latest-before snapshot: selected replay timeline tick に対象 source の alignment record が無い場合に使う、同じ source で selected tick 以前に存在する最新 snapshot。future / later snapshot は含めない。timeline cursor は selected tick のまま固定し、この snapshot は直前 sample の hold として扱う。
[^timeline-cursor]: timeline cursor: diagnostics replay 画面で現在選ばれている再生タイムライン上の位置。`latest-before snapshot` を使う場合でも、timeline cursor は selected tick のまま動かさない。
[^candidate-missing]: CandidateMissing: Field source に選択 source の候補 snapshot が無いことを示す missing 状態。Field 全体は消さず、ready な layer は残し、legend / details に missing reason を出す。
[^no-candidate-snapshot]: NoCandidateSnapshot: comparison に選択 source の候補 snapshot が無いことを示す missing 状態。比較対象が無い理由を UI に出すための状態であり、future / later snapshot を代わりに使う合図ではない。
[^future-later-snapshot]: future / later snapshot: selected replay timeline tick より後に存在する snapshot。Issue #10 の diagnostics では、未来側の トラッカー 状態を現在 tick の比較に混ぜないため、Field source や comparison の代替候補にしない。
[^diagnostics-line-alignment]: diagnostics-line alignment: diagnostics のログ行と トラッカー source snapshot の対応付け。selected tick 以前の同じ source の snapshot を探す補助情報としてだけ使い、selected tick より後の snapshot を候補にするためには使わない。
[^nearest-timestamp]: nearest timestamp: selected tick に近い時刻を探す検索方法。Issue #10 では近さだけで future / later snapshot を選ばず、同じ source の selected tick 以前の snapshot だけを候補にする。
[^old-format-current-limitation]: 旧形式 / current limitation: 既存の diagnostics capture が render frame 単位の sidecar に依存している状態。新規 capture の target ではなく、トラッカー committed frame cadence に制限される既存制約として扱う。
[^legacy-render-snapshot-sidecar]: render snapshot / legacy render snapshot sidecar: 既存 `.render-snapshots.jsonl.gz` のように トラッカー render frame 単位で保存された sidecar。loop isolation 後の新規 capture では主要な `Vision Input` 復元元にしない。
[^world-frame-committed]: WorldFrameCommitted: ibis トラッカー が world frame を commit したタイミングを表す dispatch result。既存 render snapshot 保存はこの callback に結合しており、raw Vision の保存 cadence としては遅くなり得る。
[^tracker-committed-frame-cadence]: トラッカー committed frame cadence: ibis トラッカー が `WorldFrameCommitted` を出し、`TrackerFrame` を publish する周期。raw Vision Input の新規保存周期として扱わない。
[^new-capture]: new capture: loop isolation 設計後に作る CaptureOn session。旧 render snapshot sidecar 互換より、latest raw / latest トラッカー snapshot を高頻度に保存できることを優先する。
[^diagnostics-sample-tick]: diagnostics sample tick: diagnostics logging / replay processing が latest raw snapshot と latest トラッカー snapshot を同じ保存単位として固定する tick。トラッカー committed frame と同義にしない。
[^latest-raw-snapshot]: latest raw snapshot: `VisionPacketStore` 相当の raw SSL-Vision latest detection / geometry を snapshot 化したもの。Diagnostics の `Vision Input` は新規 capture ではこの snapshot 系から復元する。
[^latest-tracker-snapshot]: latest トラッカー snapshot: ibis トラッカー または 3rd party トラッカー の最新出力を diagnostics sample に含めるために snapshot 化したもの。
[^diagnostics-sample-timeline]: diagnostics sample timeline: diagnostics sample tick を時系列に並べた replay 用 timeline。selected replay timeline tick の考え方を維持しつつ、render frame ではなく diagnostics sample を基準にする。
[^loop-isolation]: loop isolation: トラッカー operation、server live display、diagnostics logging / replay の周期と責務を分け、片方の cadence や負荷が別 loop の表示や保存を支配しないようにする方針。
[^tracker-operation-loop]: トラッカー operation loop: raw packet や profile / control input を トラッカー engine に渡し、トラッカー state の更新、publish、latest トラッカー snapshot の公開までを担当する処理 loop。
[^web-server-live-display-processing]: web server live display processing: 通常 Vision 画面が `UI render tick` ごとに latest immutable snapshot を固定して描画する処理。diagnostics logging / replay とは別扱いにする。
[^diagnostics-logging-replay-processing]: diagnostics logging / replay processing: CaptureOn 中に latest raw snapshot と latest トラッカー snapshot を独立した sample として保存し、Diagnostics 画面でその sample timeline を replay する処理。
[^ui-only-display-correction]: UI-only display correction: 保存済み data の cadence は変えず、描画時の補正だけで遅延を隠そうとする修正方針。RAW-VISION-017 の loop isolation では不採用とする。
[^diagnostics-sample-sidecar]: diagnostics sample sidecar: loop isolation 後に diagnostics logging / replay processing が保存する latest raw / latest トラッカー snapshot の sidecar。RUNTIME-HOST-007 では `diagnostics-samples.jsonl` として固定し、record は `schemaVersion`、`sampleIndex`、`sampleReceivedAt`、`sampleKind`、`rawFrameNumber`、`rawCameraId`、`worldFrameCommitted`、`renderFrameNumber`、`rawSemanticSummary`、`trackedSemanticSummary` を基本 field とする。
[^degraded-legacy-session]: unsupported / degraded legacy session: 旧 render snapshot sidecar しか持たない capture session。新しい diagnostics sample path の性能や cadence 保証を受けず、表示できる範囲だけを旧形式として扱う。
[^raw-snapshot-cadence]: raw snapshot cadence: SSL-Vision packet / raw latest snapshot が更新される周期。Diagnostics の `Vision Input` 表示は新規 capture でこの cadence を失わない保存経路を持つ。
[^tracker-debug-host]: Tracker.DebugHost: 旧 `Tracker.Server` から rename した debug 用 host。Web UI、raw vision viewer、diagnostics、capture / replay、比較表示を担当する。
