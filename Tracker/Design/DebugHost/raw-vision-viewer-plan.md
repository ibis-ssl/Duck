# `SSL_WrapperPacket` 未加工映像表示設計

## 目的

`Tracker.DebugHost`[^tracker-debug-host] が `SslProto` の生成型を使って SSL-Vision の `SSL_WrapperPacket` を含むUDPパケットを直接受信し、最新の検出情報と競技場形状を Blazor UI 上で可視化できるようにする。旧プロジェクト名は `Tracker.Server` だが、現行のプロジェクト、名前空間、起動経路は `Tracker.DebugHost` とする。

## スコープ

- 設定された映像入力の受信先にバインドする UDP 受信サービスを追加する
- マルチキャストアドレスが設定されている場合はマルチキャストグループへの参加を行う
- `SSL_WrapperPacket.Parser.ParseFrom` でパケットをデコードする
- 最新のパケット、検出情報、競技場形状、受信に付随する情報、パケット数、エラー数を、単一のインスタンスを共有する状態保存先に保持する
- `/` にフィールドを描く SVG、検出情報、カメラの校正情報、受信パケットの JSON を表示する
- 画面移動は未加工映像の表示画面を中心に保つ
- 集約表示と、カメラごとの最新フレーム表示の両方を未加工映像の表示画面で扱う

## 非スコープ

- `TrackerConnectionLib` は使わない
- パケットの永続化はしない
- 未加工の検出情報を超える追跡処理、フィルター処理、競技場全体の状態モデルの解釈は入れない
- 実運用向けのトラッカーの実時間処理と将来の自動判定モードは `Tracker.RuntimeHost` の責務とし、DebugHost の Web UI の描画と診断ログの保存から切り離す

## 設定

`appsettings.json` の `VisionReceiver` 設定階層を使う。

- `MulticastAddress`: 既定値 `224.5.23.2`
- `Port`: 受信ポート
- `InterfaceAddress`: マルチキャストグループへの参加に使うローカル IPv4 アドレス。未設定時は候補インターフェースを自動解決する
- `Profiles.<name>`: 設定組ごとの受信設定の上書き。`MulticastAddress` / `Port` / `InterfaceAddress` を同名のトラッカーの設定組に追従させたい場合に使う

設定の解決規則:

- 起動時は `Tracker:ActiveProfileName` と同名の `VisionReceiver:Profiles.<name>` を優先する
- 同名の設定組が無い場合は、最上位の `VisionReceiver` の値をそのまま使う
- 実行中にトラッカーの設定組の切り替えが完了した後は、受信設定も同じ設定組名で再解決し、必要ならソケットを開き直す

## 受信設計

`VisionReceiverService` はバックグラウンドで動くサービスとして動作する。

- IPv4 UDP ソケットを作成する
- アドレスの再利用を有効にする
- `IPAddress.Any` と設定ポートにバインドする
- 取り消し要求が来るまでUDPパケットを継続受信する
- 受信設定が切り替わったら現在の受信処理を取り消し、新しい設定でソケットを開き直す

設定されたアドレスがマルチキャストの場合、参加する通信グループは次の規則で解決する。

- `InterfaceAddress` が設定されている場合、その IPv4 アドレスのみを使う
- 未設定の場合、利用可能なローカル IPv4 インターフェースを列挙して順に参加を試行する
- 少なくとも 1 つ成功すれば受信開始を継続する
- 一部インターフェースの失敗は警告ログに残す

デコード成功時は `VisionPacketStore` を更新し、失敗時はエラー数を増やし、直前の正常状態を保持する。

## 状態の保持

`VisionPacketStore` は、UI が参照する状態を複数スレッドから安全に利用できる形で保持する。

- 最新の受信パケット
- 最新の検出フレーム
- カメラごとの最新の検出情報のスナップショット
- 集約表示用に統合したボール、黄色チームのロボット、青色チームのロボット
- 最新の競技場形状
- パケット数
- エラー数
- 送信元アドレスとポート
- 受信時刻
- 最新の解析エラーメッセージ

UI には変更不能なスナップショットを返し、描画中にロックを保持しない。

## 入力する通信データ

未加工映像の表示画面が直接使う主な入力型は次の通り。

- `SSL_WrapperPacket`
  - 受信したUDPパケットのデータ全体
  - `Detection` と `Geometry` を内包する最上位のパケット
- `SSL_DetectionFrame`
  - カメラごとの未加工の検出情報
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
  - フィールドの寸法と線分・円弧の情報
  - 主に `FieldLength`, `FieldWidth`, `GoalWidth`, `GoalDepth`, `BoundaryWidth`, `BoundaryWidthGoalLine`, `PenaltyAreaDepth`, `PenaltyAreaWidth`, `CenterCircleRadius`, `LineThickness`, `FieldLines`, `FieldArcs`
- `SSL_GeometryCameraCalibration`
  - カメラの校正情報を一覧表示するために使う
  - 主に `CameraId`, `FocalLength`, `PrincipalPointX`, `PrincipalPointY`, `PixelImageWidth`, `PixelImageHeight`

## フィールドへの投影

`VisionFieldProjection` は、フィールドの mm 単位の座標を SVG の表示領域に写像する。

- 競技場形状がある場合はその競技場寸法を使う
- 競技場形状がまだない場合は既定の競技場寸法を使う
- `(0, 0)` は表示領域の中央に対応する
- フィールド本体だけでなく、境界とゴールの奥行きが見切れないよう外周の余白を加味する

## コンポーネント構成

未加工映像の表示画面の主要コンポーネントは次の通り。

- `Home.razor`
  - 画面全体の親
  - `VisionPacketStore.GetSnapshot()` の結果を定期取得し、小さな画面ヘッダーと左右の表示領域を構成する
- `VisionFieldCanvas.razor`
  - フィールドを描く SVG の親コンポーネント
  - 拡大縮小と表示位置の状態、外周の背景、フィールド本体、子マーカーの配置、座標軸とカーソル座標の重ね表示を担当する
- `VisionFieldLines.razor`
  - 競技場の線分、円弧、ゴールの描画を担当する
  - `FieldLines` / `FieldArcs` がある場合はそれを優先し、不足時は競技場の寸法から代わりの描画を行う
- `VisionBallMarker.razor`
  - `SSL_DetectionBall` 1 件を SVG の円として描く
- `VisionRobotMarker.razor`
  - `SSL_DetectionRobot` 1 件をロボットの形状として描く
  - チームの色、前面の切り欠き、正面方向の印、表示名を担当する
- `VisionDetailsPanel.razor`
  - JSON、ボール、ロボット、カメラの校正情報を右側の領域へ表示する
- `VisionPalette.cs`
  - チームの色とマーカーの線の色の定義を一箇所に集約する
- `VisionRenderOptions.cs`
  - ロボットの半径など、将来設定から変更したい描画パラメータの受け口

## コンポーネント入力

各コンポーネントが受ける最小入力は次の通り。

### `Home.razor`

- `VisionPacketSnapshot`
  - 状態の保存先から取得した UI 用スナップショット
- `selectedViewKey`
  - 集約表示とカメラ別表示の切り替え状態
- サイドバーを折りたたんだ状態を前提に、フィールドを中心とする主表示を構成する

### `VisionFieldCanvas.razor`

- `SSL_GeometryData? Geometry`
- `IReadOnlyList<SSL_DetectionBall> Balls`
- `IReadOnlyList<SSL_DetectionRobot> RobotsYellow`
- `IReadOnlyList<SSL_DetectionRobot> RobotsBlue`
- `VisionRenderOptions RenderOptions`
- カーソル座標の表示に必要な、カーソルが重なっている状態と描画領域の大きさ

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

## 課題 #10 分割表示・重ね表示に使う表示元の設計

映像画面の分割表示・重ね表示で選択できる表示元[^source-term]の候補は、次の 4 種類に固定する。

- `Raw Aggregate`[^raw-aggregate]
  - `VisionPacketStore` の集約表示用スナップショットを使う
  - カメラごとの最新の検出情報を UI 表示用に統合した、未加工 SSL-Vision 入力の表示元として扱う
- `Raw Camera`[^raw-camera]
  - `VisionPacketStore` のカメラごとの最新検出情報のスナップショットを使う
  - カメラ ID を選択肢の内部識別子に含め、表示名だけで表示元を識別しない
- `Tracked`[^tracked-source]
  - 自前トラッカーの `TrackedSnapshotStore` から得た最新の `TrackerFrame` を、`TrackedVisionViewState` 相当のフィールド描画用 DTO へ変換して使う
  - 未加工の検出情報ではなく、自前トラッカーの出力として扱う
- `3rd party tracker`[^third-party-tracker]
  - `MultiTrackerManager<TrackerPacketAdapter>`[^multi-tracker-manager] から受けた外部トラッカーの実行中の状態[^live-state]を使う
  - UI は `MultiTrackerManager` の変更可能な状態[^mutable-state]を直接読まず、ライブ表示[^live-ui]用の変更不能なスナップショットの保存先[^immutable-snapshot-store]、またはスナップショットを合成する処理を必ず挟む

ライブ比較では、パケット内の時刻[^packet-timestamp]の厳密な一致や、すべての表示元が同じ受信時の処理呼び出し[^receive-callback]で更新されることは要求しない。未加工の SSL-Vision 入力、自前トラッカー、外部トラッカーは、受信するデータ列と更新時の処理呼び出しが異なるため、ここを契約にすると通常表示の実装が過剰に結合する。採用方針は、1 回の `UI render tick`[^ui-render-tick] で各表示元の最新の変更不能なスナップショット[^immutable-snapshot]を固定し、まとめたスナップショットを分割表示・重ね表示の Layer A/B に渡すことである。

`RUNTIME-HOST-006` 以降のライブ表示では、`Home.razor` は未加工入力や追跡結果の保存先を直接受け取らず、`VisionLiveDisplaySnapshotProvider` から `VisionLiveDisplayRenderSnapshot` を取得する。`VisionLiveComparisonSnapshotComposer` は保存先を再読取せず、固定してまとめたスナップショットから比較用のスナップショットと表示状態を生成する。これにより、未加工入力・追跡結果・比較の各表示は、同じ描画更新時点のスナップショットから派生する。

`UI render tick` でまとめるスナップショットは、次を保持する。

- 描画更新の識別子[^render-tick-id]、または `SampledAt`
- 表示元の内部識別子[^source-key]と画面上の表示名[^display-label]
- 表示元ごとの受信時刻[^receive-timestamp]、フレーム時刻[^frame-timestamp]、パケット数など、時刻差を説明する付随情報
- ボール、ロボット、競技場形状の参照[^geometry-reference]、欠落理由を含む、変更不能な表示元のスナップショット

外部トラッカーのライブ接続では、`MultiTrackerManager<TrackerPacketAdapter>` から外部トラッカーのパケットを受け、表示元は UUID を優先して集約する。同じ `uuid` のトラッカーは、送信元アドレスとポートが異なっても 1 つの表示元として扱い、同じ UUID のグループ内で `ReceivedAt` が最新のスナップショットを代表として描画する。ボールやロボットの情報を、複数の送信元のパケットから寄せ集めて統合しない。`uuid` が空または不明な場合だけ、送信元名と送信元アドレス・ポートを代用して識別する。`uuid` が異なるトラッカーは送信元名が同じでも別の表示元とし、同じ表示名が複数残る場合は短い UUID または送信元アドレス・ポートを補助表示して、UI 上で区別できる名前にする。ただし、UI は `TrackerState` や protobuf パケットへの参照を直接保持しない。`ExternalTrackerSnapshotStore` が管理側の更新イベントからパケットと付随情報を複製し、`VisionLiveDisplaySnapshotProvider` が描画更新時にその読み取り用 DTO を固定する。`TrackerPacketSnapshotLogWriter` や CaptureOn の補助ファイルの書き込み処理を、映像のライブ表示用の状態保存先[^live-store]として使う方針は不採用とする。これは CaptureOn の記録単位を保存する仕組みであり、CaptureOff の通常の映像画面では更新元として成立しないためである。

競技場形状の基準は未加工入力の形状を優先する。`Raw Aggregate` または選択中の `Raw Camera` で得られる最新の `SSL_GeometryData` を重ね表示全体のフィールド基準に使い、未加工入力の形状がまだ無い場合のみ `Tracked` の形状で代用する。`3rd party tracker` のパケットから競技場形状を復元する方針は不採用とする。外部トラッカーのパケットは比較対象の物体の状態を表すものであり、競技場の校正の責任を持たせると、表示元ごとの座標比較の意味が曖昧になる。

分割表示・重ね表示の UI 挙動は、診断画面に寄せる。

- 分割表示では Layer A と Layer B を左右に並べる
- 重ね表示では 1 つのフィールドに Layer A/B を重ねる
- 重ね表示と分割表示を相互に共通化するのではなく、それぞれに必要な画面構造は分けて保つ。そのうえで、映像のライブ表示と診断画面のフィールド描画部[^field-rendering-part]は、同じ責務境界に揃える
- 分割表示用のフィールドコンポーネント[^split-field-component]と重ね表示用のフィールドコンポーネント[^overlay-field-component]は、別物として切り出す。重ね表示と分割表示を 1 つのコンポーネントへ統合する意味ではない
- 映像のライブ表示と診断画面は、分割表示では同じ分割表示用のフィールドコンポーネントを使い、重ね表示では同じ重ね表示用のフィールドコンポーネントを使う
- フィールド、外周、競技場形状、マーカーの描画責務は、分割表示用・重ね表示用のフィールドコンポーネントへ置く。表示元の選択欄、時刻に関する情報、欠落理由、凡例、レイアウトを構成する外枠は、映像のライブ表示と診断画面のページ、外枠、付加コンポーネント側に持たせる
- 重ね表示では、表示層ごとに独立した `VisionFieldCanvas` を重ねる方針は不採用とする。重ね表示用のフィールドコンポーネントはフィールドと競技場形状を 1 回だけ描き、Layer A/B のボール・ロボットを層ごとのグループ[^layer-group]として、同じ表示位置・倍率の状態[^viewport-state]の下に描く
- 分割表示では、左右のフィールドの表示位置と倍率を独立させる[^split-independent-viewport]。左右の表示移動・拡大縮小の同期は要件にしないが、映像のライブ表示と診断画面の分割表示は、同じフィールドコンポーネント、マーカー描画方針、競技場形状を代用する方針を使う
- 詳細表示は、表示元ごとの概要、時刻に関する情報、欠落理由、未加工入力・自前の追跡結果・外部の追跡結果の違いを確認できる構成にする
- 凡例には診断画面と同じく、層名、表示元名、表示・非表示の切り替え、準備済み・欠落の状態を表示する
- Layer A/B ごとに表示・非表示を切り替えられる
- 重ね表示では診断画面と同じく、Layer A/B を異なる強調色[^accent-color]で表示し、フィールド上のマーカーと凡例の色見本の両方で層を識別できるようにする
- Layer A/B が同じ表示元を選んだ場合[^same-source]は 1 層の表示にまとめ、重複描画で誤差があるように見せない
- 片方の層が欠落していても、準備済みの層は残して表示する
- 欠落した層があってもフィールド全体を空にせず、凡例と詳細表示に欠落理由を出す

## 診断再生での時刻の対応付け

診断記録の再生・比較は、選択中の再生時点[^selected-replay-timeline-tick]を同期基準にする。旧形式の既存制限[^old-format-current-limitation]では、映像入力と自前トラッカーは選択時点の描画フレームから得たスナップショットを使い、外部トラッカーは同じ `ReplayTimelineIndex`[^replay-timeline-index] の `saved-session-alignment`[^saved-session-alignment] にある対応記録を使う。この旧描画記録の経路[^legacy-render-snapshot-sidecar]は `WorldFrameCommitted`[^world-frame-committed] に従うため、トラッカーのフレーム確定周期[^tracker-committed-frame-cadence]に制限され、新規記録[^new-capture]の目標経路としては扱わない。

新規記録の診断再生・比較は、診断データの採取時点[^diagnostics-sample-tick]を保存単位にする。診断画面の `Vision Input` は選択時点の描画フレームではなく、診断データの採取時点で保存された最新の未加工入力のスナップショット[^latest-raw-snapshot]から復元する。自前トラッカーと外部トラッカーの比較対象には、同じ診断データの採取時点で保存された最新の追跡スナップショット[^latest-tracker-snapshot]、または同時点以前の `latest-before snapshot` を使う。このため、新規記録では映像入力、自前トラッカー、外部トラッカーを、トラッカーのフレーム確定周期ではなく診断データの採取時系列[^diagnostics-sample-timeline]上で比較する。

選択時点[^selected-tick]に、対象の `3rd party tracker` の表示元に対する対応記録が無い場合でも、表示と比較を消さない。採用方針は、同じ表示元について選択時点以前に存在する最新の `latest-before snapshot`[^latest-before-snapshot] を、フィールドの表示元と比較に使うことである。UI と比較結果には、対応規則が `latest-before` であること、表示元のスナップショットの実際の `receivedAt`、選択時点との差、古い記録または選択時点以前の記録を使用している状態を明示する。これにより、対象の表示元が選択時点で未更新でも、ユーザーは直前まで得られていた追跡状態を、未加工入力や自前トラッカーと比較できる。

`latest-before snapshot` を使う場合も、再生・比較の基準時系列は選択中の再生時点のまま固定する。表示元ごとに再生位置[^timeline-cursor]をずらしたり、画面上の選択時刻をトラッカー側の時刻へ移動したりしない。フィールド表示と比較は「選択時点に対して、この表示元は直前の記録を保持している」として表示し、時刻差は選択時点と保持した表示元のスナップショットとの差として扱う。これにより、表示が消えることを避けつつ、時間軸が表示元ごとにずれ、異なる時刻のものを同時刻として表示しているように見える状態を避ける。

選択時点以前に同じ表示元のスナップショットが一切無い場合だけ、フィールドの表示元は `CandidateMissing`[^candidate-missing]、比較は `NoCandidateSnapshot`[^no-candidate-snapshot] 相当の欠落表示にする。この場合もフィールド全体は消さず、準備済みの層は残し、凡例と詳細表示に欠落理由を出す。選択時点より後のスナップショット[^future-later-snapshot]での代用は行わない。未来の追跡状態を現在時点の比較へ混ぜると、再生時系列の因果関係が崩れ、比較差分が実際より良く見えるためである。診断ログ行との対応付け[^diagnostics-line-alignment]や近傍時刻の検索[^nearest-timestamp]は、選択時点以前の同じ表示元のスナップショットを探すための補助索引として使ってよいが、選択時点より後のスナップショットは候補に含めない。この挙動は既存の診断再生での時刻対応に関する回帰テストの契約として維持し、RuntimeHost / DebugHost の分離範囲では新しい `RAW-VISION-*` 作業を追加しない。

## 診断処理の周期の分離方針

処理周期を分離する[^loop-isolation]中心目的は、トラッカーの周期処理[^tracker-operation-loop]を、Web UI を提供するサーバーのライブ表示処理[^web-server-live-display-processing]と診断ログの保存・再生処理[^diagnostics-logging-replay-processing]の両方から隔離することである。修正は表示だけの補正[^ui-only-display-correction]ではなく、保存と再生の入力周期をトラッカーのフレーム確定周期から切り離す設計として扱う。

3 つの周期処理の責務は、次の通り分ける。

- トラッカーの周期処理は、未加工の入力パケットと設定組・制御入力を追跡エンジンへ渡し、追跡状態の更新、送信、最新の追跡スナップショットの公開までを担当する。診断用の補助ファイルへフレームを保存する処理を、この周期処理の `WorldFrameCommitted` コールバックへ直接結合しない。
- サーバーのライブ表示処理は、`UI render tick` ごとに未加工入力、自前の追跡結果、外部トラッカーの最新の変更不能なスナップショットを固定し、通常の映像画面の分割表示・重ね表示を描画する。これは表示用の周期処理であり、診断ログ用のデータの採取周期を決めない。
- 診断ログの保存・再生処理は、トラッカーの周期処理から直接書き込まれた描画フレームを読むのではなく、DebugHost のバックグラウンドサービスが独立して診断データを採取する時点で、最新の未加工入力と追跡結果のスナップショットを固定し、診断の定期記録を保存する補助ファイル[^diagnostics-sample-sidecar] `diagnostics-samples.jsonl` に保存する。採取周期は `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` で設定し、既定値は `100` ms、0 以下は既定値へ戻す。記録の付随情報は `DiagnosticsSampleSidecarPath` と `DiagnosticsSampleLog` を持ち、再生処理はこの採取時系列から `Vision Input` と `ibis tracker` の意味上の概要を通常経路として復元する。

ログの互換性は、この処理周期の分離の必須要件にしない。新規記録の性能と周期維持を優先し、旧描画記録の補助ファイルに対する処理負荷の大きい互換処理は設計しない。旧形式の描画記録の補助ファイルしか持たない記録単位は、この新機能では非対応または機能を制限した旧形式[^degraded-legacy-session]として扱ってよい。旧形式を読む場合も、旧経路がトラッカーのフレーム確定周期に制限されることを UI と詳細表示で説明できれば足りる。

診断データの採取周期は、トラッカーのフレーム確定周期と同義にしない。未加工の SSL-Vision 入力の最新スナップショットがフレーム確定より高頻度に更新される場合、新しいログ保存経路は未加工入力の更新周期[^raw-snapshot-cadence]を失わない保存境界を持つ。追跡結果のスナップショットは診断データの採取時点での最新値を読むが、トラッカーの実時間処理自体を診断データの採取処理から駆動しない。これにより、トラッカーの実時間処理、サーバーのライブ表示、診断ログの保存・再生のいずれかの負荷や周期が、他の処理のユーザーに見える表示や保存周期を支配しない。

## UI 方針

ルート画面では次を表示する。

- 受信状態と最新の受信に付随する情報
- フィールドを中心とした SVG 表示
- 受信内容の JSON
- ボール、ロボット、カメラの校正情報の詳細表示

フィールドの見せ方は `RoboCup-SSL/ssl-vision-client` の方向性を踏襲する。

- フィールドの描画領域を主表示にする
- フィールド背景は外周の余白を考慮する
- マウスホイールで拡大縮小し、ドラッグで表示位置を移動する
- フィールドの表示面積を優先するため、画面タイトルは省略し、表示元の選択欄はフィールド上端から外す
- +X / +Y 方向が分かる座標軸をフィールド上に固定して重ね表示する
- カーソル座標はカーソルの上下で表示位置を切り替え、フィールドの視認性を落とさない
- デスクトップ表示のサイドバーは、表示面積の確保のため折りたたみ可能にする

## レイアウト追補

- `Home.razor` は大きなタイトル表示を持たず、状態と主な表示内容を優先する
- 表示元の選択欄は `VisionDetailsPanel.razor` 側へ移し、フィールドの縦方向の表示面積を確保する
- `VisionFieldCanvas.razor` はフィールド本体に加えて、座標軸とカーソル座標の重ね表示を管理する
- 重ね表示するカーソル座標は、通信形式から得た競技場形状と `VisionFieldProjection` の逆写像から求める
- サイドバーの折りたたみはレイアウト全体で扱い、表示専用コンポーネントへ閉じ込めない
- `Diagnostics.razor` の描画記録の表示は、映像入力とトラッカー出力のフィールド表示領域と下部の詳細領域との境界を、ドラッグで変更できるようにする
- 診断画面のフィールドと詳細領域の比率は、表示領域の高さに依存した固定上限だけにせず、4K などの高解像度環境でフィールドを大きく広げられる上限を持つ
- 詳細領域は縮小時も最低高さとスクロールを維持し、映像入力とトラッカー出力の文字列の確認を壊さない
- `Diagnostics.razor` の左側のフレーム時系列一覧は、右側の詳細領域との境界をドラッグして幅を変更できるようにする
- フレーム時系列一覧は右側のフィールド・詳細領域を広げたい場合に小さくでき、最小幅でもフレーム選択操作と省略表示を維持する
- `MainLayout.razor.css` と `NavMenu.razor.css` は、未加工映像の表示画面や診断画面の濃い緑色の UI と同じ配色・密度を使い、Blazor の既定テンプレート由来の青紫色のグラデーションや、周囲から浮いた画面移動用の表示を残さない
- 側面の画面移動メニューの選択中・カーソルが重なった状態・折りたたみ・モバイル向けの切り替えは、既存操作を維持しつつ、表示画面と同じ枠線、背景、文字色の階調で表現する
- `Diagnostics.razor` の再生位置を操作する部品には、再生、停止、早送りの操作部を置き、選択する記録を順方向に進める
- 通常再生はログ記録の時刻差を使い、間隔を上限で制限せず、実際の記録速度に合わせて進める
- 再生中は再生ボタンを停止ボタン表示へ切り替え、早送り中は早送りボタンを停止ボタン表示へ切り替える
- 再生・早送りは最後の記録に到達したら停止して先頭へ戻し、ログの切り替え時や記録が無いときには再生状態を停止状態へ戻す

## テスト方針

- `VisionPacketStore` が検出情報のみを含むパケットを保持できる
- `VisionPacketStore` が競技場形状のみを含むパケットを保持できる
- `VisionPacketStore` がカメラごとの最新状態と集約した状態を返せる
- `VisionPacketStore` がデコード失敗時にエラー数を増やす
- `VisionReceiverService.ResolveMulticastJoinAddresses` が明示指定したアドレスと自動探索を正しく処理する
- `VisionFieldProjection` が `(0, 0)` を中央に写像する
- `VisionFieldProjection` がフィールド、外周、ゴールの奥行きを含めても表示領域内に収める
- 診断画面の描画記録のフィールド・詳細領域の可変高さは、最小値・最大値・ドラッグによる移動量の制限を単体テストで確認する
- 診断画面のフレーム時系列一覧の可変幅は、最小値・最大値・ドラッグによる移動量の制限を単体テストで確認する
- 診断画面の時系列再生は、次の位置の計算、最後での停止と先頭復帰、通常再生と早送りで進める幅、時刻差に基づく実速度の間隔を単体テストで確認する
- 映像画面の分割表示・重ね表示の契約は、表示元候補、表示方式、Layer A/B の表示元選択、層の表示・非表示、同じ表示元を 1 層へまとめる処理、欠落した層があっても準備済みの層を残す挙動、診断画面に合わせた凡例と詳細表示を単体テストで先に固定する
- 映像画面の重ね表示の色に関する契約は、Layer A/B のフィールド上のマーカーと凡例が診断画面と同じ考え方の異なる強調色を持ち、同じ表示元をまとめる場合は 1 層の色になることを単体テストで先に固定する
- 映像画面と診断画面の重ね表示の契約は、表示層ごとに独立したフィールド描画領域を重ねず、両画面が同じ重ね表示用のフィールドコンポーネントを使い、単一の表示位置・倍率の状態の下で Layer A/B を描くことを単体テストで先に固定する
- 映像画面と診断画面の分割表示の契約は、左右フィールドの表示位置・倍率は独立のまま、両画面が同じ分割表示用のフィールドコンポーネントと、同じマーカー・競技場形状の描画方針を使うことを単体テストで先に固定する
- 映像画面のライブ比較の契約は、1 回の `UI render tick` で `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` の最新の変更不能なスナップショットを固定し、後続の保存状態の更新で描画中のスナップショットが変化しないことを単体テストで先に固定する
- 外部トラッカーのライブ表示元に関する契約は、UI が `MultiTrackerManager<TrackerPacketAdapter>` の変更可能な状態を直接読まず、変更不能なスナップショットの保存先または合成処理を通して、表示元の選択肢とフィールド用 DTO を作ることを単体テストで先に固定する
- 診断再生での時刻の対応付けの回帰テストでは、選択した `ReplayTimelineIndex` に対象の外部表示元の対応記録が無い場合でも、選択中の再生時点自体は動かさない。同じ表示元の選択時点以前の `latest-before snapshot` をフィールドの表示元と比較に使い、対応規則、スナップショットの実際の `receivedAt`、選択時点との差、古い記録または選択時点以前の記録を使用している状態を表示することを単体テストで先に固定する
- 診断画面の欠落表示の回帰テストでは、選択時点以前に同じ表示元のスナップショットが一切無い場合だけ `CandidateMissing` / `NoCandidateSnapshot` 相当になり、後続のスナップショットで代用せず、準備済みの層は残ることを単体テストで先に固定する
- `RUNTIME-HOST-002` の TDD 契約は、RuntimeHost / DebugHost のプロジェクト依存境界と、DebugHost が読み取り側を担当する責務を固定する
- `RUNTIME-HOST-003` の TDD 契約は、診断データの採取境界と旧形式の機能制限に関する契約を固定する。診断データの採取時点がトラッカーのフレーム確定周期に依存せず、最新の未加工入力と追跡結果のスナップショットを保存することを確認する。また、診断画面の `Vision Input` が旧描画記録の補助ファイルではなく診断の定期記録を保存する補助ファイルから復元されること、DebugHost の診断ログの保存・再生処理が RuntimeHost のトラッカーの周期処理やサーバーのライブ表示処理の `UI render tick` におけるスナップショット契約を壊さないことを確認する。旧描画記録の補助ファイルしか持たない記録単位は非対応または機能を制限した旧形式として扱い、処理負荷の大きい互換保証を持たないことも単体テストで先に固定する

## 前提

- 未加工映像の情報は `vision/ssl_vision_wrapper.proto` の `SSL_WrapperPacket` を指す
- SSL-Vision の既定のマルチキャスト受信先は一般には `224.5.23.2:10006` だが、実行時の設定で変更できる
- 作業ツリーにある既存の無関係な変更は保護する

[^source-term]: 表示元: 画面に描画するボール、ロボット、競技場形状の由来。映像画面の分割表示・重ね表示では Layer A/B で何を選び、何と比較するかを決める単位であり、`Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` が候補になる。
[^raw-aggregate]: Raw Aggregate: 未加工 SSL-Vision 入力のカメラごとの最新検出情報を UI 表示用に統合した表示元。複数カメラのボール・ロボットをまとめて映像画面で見るための候補で、カメラ単体ではなく集約表示を選ぶときに使う。
[^raw-camera]: Raw Camera: 特定のカメラ ID の未加工 SSL-Vision 入力の最新検出情報を表示する表示元。カメラごとの見え方や検出差を確認するための候補で、選択肢の内部識別子にはカメラ ID を含める。
[^tracked-source]: Tracked: 自前トラッカーが生成した `TrackerFrame` を映像表示用 DTO に変換した表示元。未加工の検出情報ではなく、自前トラッカーの出力を Layer A/B や重ね表示の比較へ出すために使う。
[^third-party-tracker]: 3rd party tracker: 自前トラッカー以外の外部トラッカーから受けたパケットの表示元。外部トラッカーの出力を未加工の SSL-Vision 入力や自前トラッカーと比較するための候補。
[^live-state]: 実行中の状態: 映像画面の分割表示・重ね表示の `3rd party tracker` で、実行中に外部トラッカーから最後に受けた状態。Layer A/B で `3rd party tracker` を選んだときの描画元になるが、UI はこの状態を直接保持せず、スナップショット化された表示用データを読む。
[^mutable-state]: 変更可能な状態: `MultiTrackerManager` 内で後から内容が変わる状態オブジェクト。映像画面の分割表示・重ね表示では、描画中に値が変わることを避けるため、この状態を直接読まず、スナップショット化してから比較に使う。
[^live-ui]: ライブ表示: CaptureOn の保存記録の再生ではなく、CaptureOff の通常の映像画面で現在受信している表示元を比較する画面。未加工の SSL-Vision 入力、自前トラッカー、外部トラッカーの現在値を分割表示・重ね表示で見比べる。
[^immutable-snapshot-store]: 変更不能なスナップショットの保存先: 映像画面の通常表示で、Layer A/B に渡す前に、描画中に変わらない表示用データを保持する境界。外部トラッカーの更新とフィールド描画の時点を切り離すために使う。
[^packet-timestamp]: パケット内の時刻: パケットに入っている時刻。映像画面の分割表示・重ね表示の通常表示では、未加工の SSL-Vision 入力、自前トラッカー、外部トラッカーのパケット内の時刻が厳密に同一であることは比較条件にしない。
[^receive-callback]: 受信時の処理呼び出し: パケット到着時に呼び出される受信処理。通常表示では、すべての表示元が同じ受信時の処理呼び出しで更新されたことを Layer A/B の比較条件にしない。
[^ui-render-tick]: UI render tick: Blazor UI が 1 回の表示更新を行う単位。この更新内で各表示元の最新スナップショットを固定し、分割表示・重ね表示へ渡す。
[^immutable-snapshot]: 変更不能なスナップショット: 描画中に内容が変わらないよう、複製して DTO にした読み取り専用のスナップショット。後続の保存状態の更新で、描画中のスナップショットが変化しないことを保証するために使う。
[^render-tick-id]: 描画更新の識別子: `UI render tick` ごとの表示更新単位を識別する ID。Layer A/B が同じ表示更新で固定されたスナップショットを見ていることを説明するために使う。
[^source-key]: 表示元の内部識別子: 映像画面の表示元を識別する内部キー。`Raw Camera` ではカメラ ID を含め、画面上の表示名だけで表示元を取り違えないようにする。
[^display-label]: 画面上の表示名: UI に出す名前。表示元の内部識別子とは違い、内部識別には使わず、詳細表示や選択肢でユーザーに表示元を示すために使う。
[^receive-timestamp]: 受信時刻: パケットを受け取った時刻。詳細表示で表示元間の時刻差を確認し、`latest-before snapshot` が選択時点からどれだけ古いかを説明するために使う。
[^frame-timestamp]: フレーム時刻: 検出フレームや追跡フレーム側の時刻。受信時刻と併せて、表示元同士の比較がどの時刻情報に基づくかを詳細表示で説明する。
[^geometry-reference]: 競技場形状の参照: フィールド描画に使う形状情報の参照。重ね表示では未加工入力の競技場形状を優先し、無い場合だけ `Tracked` の形状で代用するため、表示元同士の座標比較の基準を示す。
[^source-label]: 表示元名: 凡例や詳細表示に出す名前。ユーザーが Layer A/B で、どの未加工入力、自前の追跡結果、外部トラッカーを選んでいるか確認するために使う。
[^live-store]: ライブ表示用の状態保存先: 通常の映像画面の現在表示を更新するための保存先。CaptureOn の記録単位を保存する `TrackerPacketSnapshotLogWriter` や補助ファイルの書き込み処理は、この役割に使わない。
[^accent-color]: 強調色: Layer A/B を見分けるために、マーカーの線や凡例の色見本に使う色。課題 #10 の映像画面の重ね表示では、診断画面の重ね表示と同じ考え方で、重なった層を色で判別できるようにする。
[^field-rendering-part]: フィールド描画部: フィールドの背景、外周、競技場の線、ボールとロボットのマーカーを SVG に描く部品の境界。表示元の選択や付随情報の詳細表示ではなく、フィールド上に何をどう描くかを担当する部分を指す。
[^split-field-component]: 分割表示用のフィールドコンポーネント: 分割表示の左右それぞれのフィールドを描くコンポーネント。左右の表示位置と倍率は独立させるが、映像のライブ表示と診断画面は同じコンポーネント境界を使う。
[^overlay-field-component]: 重ね表示用のフィールドコンポーネント: 1 つのフィールド上に Layer A/B を重ねて描くコンポーネント。フィールドと競技場形状を層ごとに描き直さず、物体のマーカーだけを Layer A/B のグループとして重ねる。
[^overlay-field-rendering]: 重ね表示のフィールド描画部: 重ね表示用のフィールドコンポーネントと同じ責務範囲を指す。フィールドと競技場形状を層ごとに描き直さず、物体のマーカーだけを Layer A/B のグループとして重ねる。
[^layer-group]: 層ごとのグループ: 同じフィールド上で Layer A または Layer B に属するボール・ロボットをまとめる SVG グループ。グループごとに表示・非表示や強調色を持たせるが、表示位置の移動・拡大縮小の基準は、重ね表示のフィールド描画部のものを共有する。
[^viewport-state]: 表示位置・倍率の状態: フィールドの拡大縮小、表示位置の移動、ドラッグ中の移動量など、画面上でフィールドをどの位置と倍率で見るかを表す状態。重ね表示では層ごとに別々に持たず、1 つの状態を共有する。
[^split-independent-viewport]: 分割表示の独立した表示位置・倍率: 左右のフィールドが、それぞれ別の表示位置と倍率を持つこと。左右は比較対象を並べる表示なので、片方をドラッグしても、もう片方を自動追従させる要件ではない。
[^same-source]: 同じ表示元を選んだ状態: Layer A/B が同じ表示元を選んだ状態。映像画面の重ね表示では、重複描画で誤差があるように見せないため、1 層の表示にまとめる。
[^multi-tracker-manager]: MultiTrackerManager / TrackerPacketAdapter: `MultiTrackerManager` は `TrackerConnectionLib` の追跡状態を管理するコンポーネントで、自前・外部・不明のトラッカーの最新状態を保持する。`TrackerPacketAdapter` は、外部トラッカーのパケットを `MultiTrackerManager` で扱うための変換用部品。
[^selected-replay-timeline-tick]: 選択中の再生時点: 診断再生でユーザーが現在選択している、再生時系列上の基準時点。映像入力、自前トラッカー、外部トラッカーを比較するとき、この時点は表示元ごとに移動させない。
[^replay-timeline-index]: ReplayTimelineIndex: 診断再生の選択時点を識別する位置番号。`saved-session-alignment` の対応記録と結び付けて、どの時点の比較かを特定する。
[^saved-session-alignment]: saved-session-alignment: CaptureOn の記録単位に保存された、再生時系列上の時点とトラッカーの表示元スナップショットの対応記録群。診断再生で外部トラッカーのスナップショットを、同じ選択時点に合わせるために使う。
[^alignment-sidecar]: 対応付け用の補助ファイル: CaptureOn の記録単位の主ログとは別に保存されるファイル。診断再生で、選択中の再生時点とトラッカーの表示元スナップショットの関係を後から復元するために使う。
[^alignment-record]: 対応記録: 対応付け用の補助ファイル内の 1 件の対応付け。診断再生で特定の時点を選んだとき、どのトラッカーの表示元スナップショットを比較に使うかを示す。
[^selected-tick]: 選択時点: 選択中の再生時点の短縮表現。本文で「選択時点」と書く場合も同じ意味を指す。
[^latest-before-snapshot]: latest-before snapshot: 選択中の再生時点に対象表示元の対応記録が無い場合に使う、同じ表示元で選択時点以前に存在する最新スナップショット。選択時点より後のものは含めない。再生位置は選択時点のまま固定し、このスナップショットは直前の記録を保持したものとして扱う。
[^timeline-cursor]: 再生位置: 診断再生画面で現在選ばれている再生時系列上の位置。`latest-before snapshot` を使う場合でも、再生位置は選択時点のまま動かさない。
[^candidate-missing]: CandidateMissing: フィールドの表示元として選択した表示元の候補スナップショットが無いことを示す欠落状態。フィールド全体は消さず、準備済みの層は残し、凡例と詳細表示に欠落理由を出す。
[^no-candidate-snapshot]: NoCandidateSnapshot: 比較に選択した表示元の候補スナップショットが無いことを示す欠落状態。比較対象が無い理由を UI に出すための状態であり、選択時点より後のスナップショットを代わりに使う合図ではない。
[^future-later-snapshot]: 選択時点より後のスナップショット: 選択中の再生時点より後に存在するスナップショット。課題 #10 の診断画面では、未来の追跡状態を現在時点の比較に混ぜないため、フィールドの表示元や比較の代替候補にしない。
[^diagnostics-line-alignment]: 診断ログ行との対応付け: 診断ログ行とトラッカーの表示元スナップショットの対応付け。選択時点以前の同じ表示元のスナップショットを探す補助情報としてだけ使い、選択時点より後のスナップショットを候補にするためには使わない。
[^nearest-timestamp]: 近傍時刻の検索: 選択時点に近い時刻を探す検索方法。課題 #10 では、近さだけで選択時点より後のスナップショットを選ばず、同じ表示元の選択時点以前のスナップショットだけを候補にする。
[^old-format-current-limitation]: 旧形式の既存制限: 既存の診断記録が、描画フレーム単位の補助ファイルに依存している状態。新規記録の目標ではなく、トラッカーのフレーム確定周期に制限される既存制約として扱う。
[^legacy-render-snapshot-sidecar]: 旧描画記録の補助ファイル: 既存の `.render-snapshots.jsonl.gz` のように、トラッカーの描画フレーム単位で保存された補助ファイル。処理周期の分離後の新規記録では、主要な `Vision Input` の復元元にしない。
[^world-frame-committed]: WorldFrameCommitted: 自前トラッカーが競技場全体のフレームを確定した時点を表す処理結果。既存の描画記録の保存はこのコールバックに結合しており、未加工映像入力の保存周期としては遅くなり得る。
[^tracker-committed-frame-cadence]: トラッカーのフレーム確定周期: 自前トラッカーが `WorldFrameCommitted` を出し、`TrackerFrame` を送信する周期。未加工映像入力の新規保存周期として扱わない。
[^new-capture]: 新規記録: 処理周期を分離する設計の後に作る CaptureOn の記録単位。旧描画記録の補助ファイルとの互換より、最新の未加工入力と追跡結果のスナップショットを高頻度に保存できることを優先する。
[^diagnostics-sample-tick]: 診断データの採取時点: 診断ログの保存・再生処理が、最新の未加工入力と追跡結果のスナップショットを同じ保存単位として固定する時点。トラッカーのフレーム確定と同義にしない。
[^latest-raw-snapshot]: 最新の未加工入力のスナップショット: `VisionPacketStore` 相当の未加工 SSL-Vision 入力の最新検出情報と競技場形状をスナップショット化したもの。診断画面の `Vision Input` は、新規記録ではこのスナップショットから復元する。
[^latest-tracker-snapshot]: 最新の追跡スナップショット: 自前トラッカーまたは外部トラッカーの最新出力を、診断の採取記録に含めるためにスナップショット化したもの。
[^diagnostics-sample-timeline]: 診断データの採取時系列: 診断データの採取時点を時系列に並べた再生用の時系列。選択中の再生時点の考え方を維持しつつ、描画フレームではなく診断データの採取記録を基準にする。
[^loop-isolation]: 処理周期の分離: トラッカーの実時間処理、サーバーのライブ表示、診断ログの保存・再生の周期と責務を分け、片方の周期や負荷が別の処理の表示や保存を支配しないようにする方針。
[^tracker-operation-loop]: トラッカーの周期処理: 未加工の入力パケットや設定組・制御入力を追跡エンジンに渡し、追跡状態の更新、送信、最新の追跡スナップショットの公開までを担当する周期処理。
[^web-server-live-display-processing]: Web UI を提供するサーバーのライブ表示処理: 通常の映像画面が `UI render tick` ごとに最新の変更不能なスナップショットを固定して描画する処理。診断ログの保存・再生とは別扱いにする。
[^diagnostics-logging-replay-processing]: 診断ログの保存・再生処理: CaptureOn 中に最新の未加工入力と追跡結果のスナップショットを独立した記録として保存し、診断画面でその採取時系列を再生する処理。
[^ui-only-display-correction]: 表示だけの補正: 保存済みデータの周期は変えず、描画時の補正だけで遅延を隠そうとする修正方針。`RAW-VISION-017` の処理周期の分離では不採用とする。
[^diagnostics-sample-sidecar]: 診断の定期記録を保存する補助ファイル: 処理周期の分離後に診断ログの保存・再生処理が保存する、最新の未加工入力と追跡結果のスナップショットの補助ファイル。`RUNTIME-HOST-007` では `diagnostics-samples.jsonl` として固定し、記録は `schemaVersion`、`sampleIndex`、`sampleReceivedAt`、`sampleKind`、`rawFrameNumber`、`rawCameraId`、`worldFrameCommitted`、`renderFrameNumber`、`rawSemanticSummary`、`trackedSemanticSummary` を基本の項目とする。
[^degraded-legacy-session]: 非対応または機能を制限した旧形式: 旧描画記録の補助ファイルしか持たない記録単位。新しい診断データの採取経路の性能や周期の保証を受けず、表示できる範囲だけを旧形式として扱う。
[^raw-snapshot-cadence]: 未加工入力の更新周期: SSL-Vision パケットや、未加工入力の最新スナップショットが更新される周期。診断画面の `Vision Input` 表示は、新規記録でこの周期を失わない保存経路を持つ。
[^tracker-debug-host]: Tracker.DebugHost: 旧 `Tracker.Server` から名前を変更した診断用の実行体。Web UI、未加工映像の表示、診断、記録と再生、比較表示を担当する。
