# Tracker DebugHost / CLI / UI CaptureOn 比較ログ 詳細設計


本書で raw vision は SSL-Vision の検出情報を指す。カメラの画像や動画そのものではない。
## 目的

`TRACKER-040` 以降では、CaptureOn 中に見えている公式形式の `TrackerWrapperPacket` をすべて保存し、キャプチャー後に自前トラッカーの出力、自前トラッカー自身の公式形式のパケット、外部トラッカーのパケットを再生・比較できるようにする。

この文書は CaptureOn 比較ログの DebugHost / CLI / UI 側の機能設計を定める。旧 `TRACKER-034` の巨大ファイル分割やコメント追加などの保守性改善は機能仕様に含めない。保守性改善の履歴と運用設計は `debug-host-maintainability-design.md` と `../Core/tracker-history-000-038.md` を参照する。

## 対象範囲

- `Tracker.DebugHost` の CaptureOn の記録単位と比較ログの関連付け
- `TrackerConnectionLib` を使った official tracker packet のキャプチャー
- CaptureOn の session folder へ、キャプチャー、capture metadata、diagnostics log、render snapshot、tracker packet snapshot を保存する snapshot sidecar（JSONL）をまとめる保存契約
- diagnostics log の読み取り、diagnostics replay、diagnostics playback から最新の記録方式で保存した補助ファイルを参照し、旧形式は旧形式としての表示または正確な対応を保証しない推定表示に留める入力契約
- `/diagnostics` または `Tracker.CaptureReplay` で、後から外部トラッカーのスナップショットを再生・比較表示するための入力契約

対象外:

- `Tracker.Core` の追跡アルゴリズム変更
- 自前トラッカーの公式形式のパケットの出力内容変更
- 既存のキャプチャー、診断ログ、render snapshot のデータ構造の破壊的変更
- 旧保守性改善作業の巨大ファイル分割、履歴の分離、進捗管理の簡略化

## 責務境界

- official tracker packet のキャプチャー処理の接続先は、`TrackerConnectionLib` を第一候補とする。
- `Tracker.DebugHost` へ組み込む際は、既存の `UdpTrackerReceiver` / `MultiTrackerManager` / `TrackerPacketAdapter` の責務を優先して使う。CaptureOn の開始・停止と session folder に合わせる必要がある場合だけ、必要最小限の接続処理を置く。
- official tracker packet はマルチキャストの受信先へ届く前提とし、受信処理は設定済みのマルチキャスト用の通信アドレスと通信ポートを使ってマルチキャストグループに参加する。同じ端末内へのユニキャスト受信だけでは、CaptureOn 比較ログの実行時の正常系証跡として扱わない。
- トラッカーの受信処理の通信アドレスと通信ポートは起動時に解決する。`Tracker:Receive:MulticastAddress` / `Port` を明示した場合は受信処理独自の受信先を監視し、未指定項目は、起動時に有効な設定プロファイルと `Tracker:RuntimeOverrides:Publish` から解決した自前トラッカーの送信先の値で代用する。`Tracker:Receive:InterfaceAddress` は従来通り、マルチキャストグループへの参加に使う、この端末の NIC を通信アドレスで指定する設定であり、受信先の代用規則とは独立して扱う。
- 実行中の設定プロファイルの切り替え後にトラッカーの受信処理のソケットを再構成することは、`TRACKER-054` の対象外とする。切り替え後も受信処理は起動時に解決した受信先を監視し続けるため、運用手順と README では起動時固定であることを明記する。
- `Tracker.DebugHost` は、CaptureOn の記録単位と tracker packet snapshot の記録を紐付ける統合層にする。キャプチャー本体、capture metadata、診断用の補助ファイル、render snapshot、tracker packet snapshot の snapshot sidecar を、同じ session folder 配下の成果物として扱う。
- `Tracker.Core` には、official tracker packet のキャプチャー、snapshot sidecar への保存、自前トラッカーと他のトラッカーの後処理での比較を入れない。`Tracker.Core` は自前トラッカーの内部の追跡フレームと公式形式のパケット生成だけを担当する。

## 保存形式

tracker packet snapshot の主記録は、既存の `.tracker-diagnostics.log` の破壊的拡張ではなく、CaptureOn の session folder 配下の sidecar JSONL とする。異なる CaptureOn の開始時点に属するログは別フォルダへ分け、同じ階層に多数のログファイルを横並びにしない。

session folder は `VisionReceiver:PacketCapture:DirectoryPath` 配下に作成し、名前には既存の `<prefix>-<timestamp>-<guid>` 形式の共通名を使う。フォルダ内のファイル名も同じ共通名を含めるか、または `packets.jsonl.gz` のような用途名を使う。どちらの場合も capture metadata から相対パスで辿れるようにし、既存の共通名を揃える考え方は、session folder 名またはフォルダ内のファイル名で維持する。

session folder には、少なくとも次を配置できるようにする。

- キャプチャー本体
- capture metadata
- トラッカーの診断用の補助ファイル
- render snapshot
- tracker packet snapshot の snapshot sidecar（JSONL）
- alignment sidecar（JSONL）

capture metadata には session folder の保存先と、キャプチャー本体、トラッカーの診断記録、render snapshot、tracker packet snapshot の snapshot sidecar（JSONL）、alignment sidecar（JSONL）などの、各ファイルへの相対パスを記録する。snapshot sidecar や alignment sidecar が未作成、または記録 0 件の場合も、その状態を capture metadata で表現できるようにする。

診断ログの保存・再生の主経路は、capture metadata から解決する新規の補助ファイル群と、ログを開くときに構築する索引を使う。既存の `.tracker-diagnostics.log` のデータ構造の破壊的変更は避けるが、旧診断ログや旧 render snapshot の補助ファイルの完全互換は要件にしない。旧形式を読む場合は、旧形式としての表示、正確な対応を保証しない推定表示、機能を制限した表示に留める。新規記録の書き込み周期、探索量を制限した参照、RuntimeHost / DebugHost の分離、diagnostics sample sidecar の設計を犠牲にしない。

- 既存の `key=value` 行は、旧記録を機能制限付きで開くために必要な範囲だけ、正確な復元を保証しない補助経路で読む。旧互換のために新規読み取り処理の主経路を旧ログの解析処理へ固定しない
- capture metadata から解決できる snapshot sidecar の相対パス、表示元数、分類別件数、近傍比較の概要などを省略可能な項目として追加する
- snapshot sidecar がない既存ログは、新規記録の正常系証跡ではなく、非対応または機能を制限した旧形式として扱う。表示できる場合も、探索量を制限した索引と新しい補助ファイルを使う読み取り処理の設計を迂回する、処理負荷の大きい代用処理は追加しない

sidecar JSONL の各記録は、後から外部トラッカーの追跡結果を再生し、自前トラッカーの追跡結果と再比較できるよう、次を保持する。スナップショットは表示用データとして扱ってよいが、表示用のスナップショットだけでは比較元データとして不十分である。通常経路では、受信したパケットの元データ、またはそれを復元できる参照を必ず保持する。round-trip（書き込み後の読み戻し）により、保存済み記録から元データを復元または再デコードできることを入力契約にする。

- `receivedAt`
- 送信元の通信アドレスと通信ポート
- `uuid`
- `sourceName`
- source role、source label、source metadata
- tracked frame number
- tracked frame timestamp
- 受信パケットのバイト列の Base64 表現、または元データを session folder 内で復元できる参照情報
- 元データから作れるボール・ロボット数、チーム・ロボット ID、代表位置、表示元の情報の概要など、比較・一覧表示用の概要情報
- デコード失敗、追跡フレームの欠落、時刻の欠落などを示す、処理を省略した理由やエラーの情報

tracker snapshot の alignment sidecar は、`tracker-packet-snapshots.jsonl` とは別の `tracker-snapshot-alignment.jsonl` とする。スナップショット記録自体へ診断記録との対応を埋め込むと、同じスナップショットを複数の診断記録、Field source、aggregate source から参照するときに、重複と後方互換の分岐が増える。また、snapshot sidecar の破損と、対応付け情報の破損を分けて扱いにくくなる。別ファイルにすることで、前者は受信パケットの主記録、後者は再生用の索引として責務を分け、対応付けが欠落または破損しても、元のスナップショット保存の成否を独立に診断できる。

alignment sidecar の各記録は、CaptureOn 中に診断記録、render snapshot、tracker source snapshot を同じキャプチャーの時系列で対応付けるため、少なくとも次を保持する。

- 診断記録の安定した識別子: 診断ログの行番号、tracked frame number、診断記録の時刻、自前トラッカーの `TrackerFrame.data_timestamp_ns`
- render snapshot への参照: render snapshot の追跡フレームの番号、保存順の位置番号またはそのキャプチャー内の相対位置
- 記録開始を基準とする相対時間: 記録開始から診断記録までの時間、SSL-Vision のパケットと render snapshot の `receivedAt` の相対時間、対応付けに使った収録時の `receivedAt`
- source key: source role、source label、UUID、送信元の通信アドレスと通信ポート、正規化済みの source key
- 選択した tracker snapshot への参照: tracker snapshot の保存順の位置番号、tracker snapshot の `receivedAt`、tracked frame number、tracked frame timestamp、比較や描画に使う概要情報の有無
- 対応規則: `saved-session-alignment`、`saved-session-received-at-nearest`、`legacy-nearest-timestamp`、`unsupported-alignment-missing` など
- 時刻差: 診断記録または収録時刻と tracker snapshot の `receivedAt` との差。必要なら自前トラッカーのデータ時刻と外部トラッカーの時刻との差
- 集約情報: source label や source role で集約して選ばれた場合の代表 source key、同条件の候補から選んだ理由、同一表示名・UUID の送信元の通信アドレスと通信ポートの組の数
- 状態: 準備済み、表示元なし、スナップショットなし、対応付けの省略、対応付けの破損など

alignment sidecar は、ログを開くときに軽く索引化できる形にする。読み取り処理は JSONL を 1 回だけ読み、診断記録の安定した識別子と Field source 識別子から、alignment record を直接取得できる辞書または並べ替え済み配列を構築する。timeline scrubber の操作、playback tick、Field source selector の変更時に、`tracker-packet-snapshots.jsonl` 全体や対応付け用 JSONL 全体を再読込・再探索しない。100メガバイト超または長時間の記録では、alignment record から tracker snapshot の保存順の位置番号と source key を引き、必要な概要情報だけを既存のスナップショット索引から参照する。

## 表示元の識別と分類

`Tracker:Uuid` と `Tracker:SourceName` は保存除外の条件ではなく、後続の表示・比較用に source role、source label、source metadata を付与するために使う。自前トラッカーの実行時の識別情報と一致する `TrackerWrapperPacket` も追跡パケットの補助ファイルへ保存してよく、自前トラッカーの詳細ログや render snapshot との重複保持を仕様として許容する。

どちらかが空、設定と異なる、または他のトラッカーと衝突する場合も、記録を破棄しない。送信元の通信アドレスと通信ポート、受信経路を併記し、分類を `unknown` や `ambiguous` として扱う。自前・外部・不明の判別は、保存後の表示名、絞り込み、比較対象の選択のための付随情報であり、保存可否を決める条件ではない。

同じ `uuid` で `sourceName` が異なる場合や、`sourceName` が空の場合も、記録を破棄せず、source identity の不足として保存する。表示元ごとに利用中のトラッカーを取得する API の扱いと、同じ `uuid` が衝突する場合の扱いは、表示元の概要と source role の判定に関するリスクとして、引き続き確認する。通常経路では元データと source identity を落とさず、衝突時も `unknown` / `ambiguous` として保存できれば比較元データは保持されるため、保存処理を止める理由にはしない。

ER-FORCE のように同じ source label と UUID が複数の送信元の通信アドレスと通信ポートから届く場合、保存上の source key は `sourceRole + sourceLabel + sourceUuid + remoteEndpoint` とし、送信元の通信アドレスと通信ポートの組ごとに分ける。UI の `External` や source label による選択は、aggregate source として扱う。集約時は、送信元ごとの識別子で分けた候補から、同じキャプチャーの開始時刻を基準とする診断記録の `receivedAt` に最も近いスナップショットを代表に選ぶ。同条件の候補から選ぶ規則は、時刻差の絶対値が小さい候補、同じ差なら同じ tracked frame timestamp のうち記録の位置番号が小さい候補、さらに同値なら送信元の通信アドレスと通信ポートを表す文字列の `StringComparer.Ordinal` による比較順とし、alignment record に選択理由を残す。送信元別の詳細が必要な場合は、source option に送信元の通信アドレスと通信ポートを含めた表示名を追加できるが、通常の Field source 名では集約した代表を描画する。

## 時刻による比較

自前トラッカーの確定済みの追跡フレームと tracker packet snapshot は、同じ tracked frame number や送信頻度を持つとは限らない。比較は、自前トラッカーの `TrackerFrame.data_timestamp_ns` とスナップショット側の `TrackedFrame.timestamp` の近傍時刻で行う。

初期実装では、時刻が最も近いものを使うか、latest-before snapshot を使うかを、作業内で固定する。採用した対応規則、許容する時間幅、該当する source identity は、出力と補助ファイルから後で確認できるようにする。

`TrackedFrame.timestamp` はトラッカー実装ごとの時刻系であり、自前と外部のトラッカーが同じ起点や単調増加する時計を使うとは限らない。新規キャプチャーの diagnostics replay と Field source の表示は、保存済みの alignment sidecar がある場合はこれを優先する。外部トラッカーの `TrackedFrame.timestamp` ではなく、CaptureOn 中に観測した `receivedAt`、記録開始からの相対時間、診断記録の時刻を使って、tracker source snapshot を対応付ける。自前と外部のトラッカーの時刻範囲が明らかに重ならない場合でも、保存時の対応付けがあれば `saved-session-alignment` として、再生や再生位置のドラッグに伴うフィールド表示を成立させる。

保存済みの alignment sidecar がない記録では、`/diagnostics` は外部トラッカーを表示元とするフィールド表示の正確な時刻対応を保証しない。既存の近傍時刻の選択を使う場合は `legacy-nearest-timestamp` と、対応を推定した表示であることを示す。時刻範囲が重ならないことを検出した場合は `unsupported-alignment-missing` とし、既存ログの欠落を既存互換で想定する正常な状態として扱う。既存ログを救済するための読み込み時の代用処理を、主経路へ昇格しない。

## CaptureOn の開始・停止と記録の切り替え

トラッカーのパケットをネットワークから受信する処理は、明示設定で有効化する。既定設定ではソケットのバインドや受信を始めず、運用者が公式形式の追跡パケットのマルチキャスト受信を有効化した場合だけ受信処理を起動する。

`Tracker:Receive:MulticastAddress` / `Port` が未指定なら、トラッカーの受信処理は起動時に解決した自前トラッカーの送信先と同じ通信アドレスと通信ポートを監視する。外部トラッカーが別の送信先へ送る運用では、`Tracker:Receive` に受信処理独自の受信先を明示する。`Tracker:Receive:Enabled=false` の場合、外部トラッカーのパケットは CaptureOn 中でも記録されない。

CaptureOn は受信処理の起動条件ではなく、記録用の補助ファイルへの書き込み条件を制御する。Capture Off 中に受信処理がパケットを受けても、snapshot sidecar を作成・追記しない。

Capture Off 中は snapshot sidecar を作成・追記しない。Capture Off から再度記録を開始するときは session folder を更新し、前の session folder の snapshot sidecar へ追記しない。

CaptureOn 直後、キャプチャー本体の保存先がまだ作成されていない場合は、最初の保存対象パケットが来た時点で同一の session folder を確定し、snapshot sidecar をその配下へ関連付ける。

他のトラッカーが存在しない場合、既存のキャプチャー、診断ログ、render snapshot の内容上の挙動は変えない。capture metadata には、snapshot sidecar が未作成、または記録 0 件である状態を明示できるようにする。

## 診断ログ・再生の最新経路と旧形式の表示

診断ログの読み取り処理、`Tracker.CaptureReplay`、診断画面の再生は、capture metadata にある相対パスから、tracker packet snapshot の snapshot sidecar と diagnostics sample sidecar を取得する。新規記録では、最新の未加工入力と追跡結果のスナップショットを基準に追加情報を読む。既存のキャプチャーや診断ログで、session folder、snapshot sidecar、diagnostics sample sidecar が欠落する場合は、新規経路の正常系ではなく、旧形式、推定、機能制限付きの表示として扱う。

`Tracker.CaptureReplay` は、エージェントによる調査や自動検証で使う CLI として、外部トラッカーのスナップショットと自前トラッカーの確定済みの追跡フレームを、保存時の対応付けに基づいて比較する。または、既存記録について推定した対応であることを明示して比較する。結果は `trackerSnapshot` / `trackerComparison` 行として出力する。この CLI 比較実装は診断 UI の実装後も削除せず、UI と同じ読み取り契約の検証経路として維持する。

`/diagnostics` はユーザー向けに同じ比較を画面上で確認できるようにする。診断画面の再生は、選択中の replay timeline tick に合わせて tracker snapshot の比較を更新し、source identity、source role、source label で対象を切り替えられる comparison panel を持つ。新規記録では、保存済みの alignment sidecar と diagnostics sample sidecar を基準にする。旧形式で対応を推定する場合だけ、render snapshot ではなく、自前トラッカーのスナップショットの `TrackedFrame.timestamp` を比較基準の時刻とする。この推定表示は旧形式の救済であり、最新の記録方式の性能、探索量を制限した参照、RuntimeHost / DebugHost の分離を弱める要求にはしない。

新規記録の `/diagnostics` における tracker snapshot の比較と Field source は、capture metadata から解決した diagnostics sample sidecar と `tracker-snapshot-alignment.jsonl` を優先する。alignment sidecar が準備済みなら、selected replay timeline tick と Field source 識別子から保存済みの tracker snapshot の保存順の位置番号を引き、対応規則を `saved-session-alignment` として表示する。この補助ファイルがない、capture metadata にファイルパスがない、または破損している場合は、旧診断ログや旧 render snapshot の補助ファイルを新規経路へ昇格させない。外部トラッカーや表示元名によるフィールド選択は、`unsupported-alignment-missing`、または `legacy-nearest-timestamp` と推定であることを明示した表示として扱う。旧形式を表示できる場合も、再生更新や位置のドラッグごとに補助ファイル全体を読み直す互換処理は作らない。

tracker snapshot の比較は、timeline scrubber の操作や playback tick のたびに、snapshot sidecar や alignment sidecar の JSONL を再読込しない。ログ選択時に、capture metadata、snapshot sidecar、alignment sidecar、diagnostics log の各ファイルパスと、各ファイルの最終更新時刻・長さをキーにした軽量な索引を作成またはキャッシュする。選択中の診断記録や replay timeline tick が変わるときは、この索引から source option、保存済みの対応付け、または推定であることを明示した比較を生成する。

100メガバイトは上限ではなく、通常のキャプチャーで到達し得るサイズとして扱う。100メガバイト以上の補助ファイルでも、再生更新や位置のドラッグ時に、ファイルサイズへ比例した入出力、JSON 解析、protobuf 解析を発生させない。初回のログ選択時の索引構築には既存の補助 JSONL ファイルを活かし、使用メモリに上限を持つキャッシュによって、同じ状態のファイルの再読込を避ける。

### replay timeline と通常再生

`TRACKER-059` 以降の `/diagnostics` の再生、早送り、位置のドラッグは、診断記録の件数ではなく統合した replay timeline を選択軸にする。時刻軸は収録時の `ReceivedAt` とし、診断記録、render snapshot、tracker packet snapshot の時点を合わせた集合、または同等に利用可能な表示元のうち最速の source cadence を含む索引とする。外部トラッカーの `TrackedFrame.timestamp` は自前トラッカーと時刻系が違う場合があるため、この時刻軸には使わない。`TrackedFrame.timestamp` は各表示元内の表示・比較値として保持し、収録時刻での順序は `ReceivedAt` または記録開始からの相対受信時間で固定する。

通常再生は、統合した replay timeline の全時点を順番に描画しない。再生開始時に、開始時の wall-clock と、selected replay timeline tick の収録時刻 `ReceivedAt` を基準として保持する。表示更新は毎秒30回相当、つまり約33.3 msごとの描画間隔を目標に行い、各更新時に `targetReceivedAt = startTick.ReceivedAt + (currentWallClock - startWallClock)` を計算する。UI は replay timeline から `ReceivedAt <= targetReceivedAt` を満たす最新時点を選択し、そこへ直接追従する。トラッカーの更新が 100 Hz / 200 Hz 相当で存在する場合でも、通常再生では中間時点の表示を省略してよく、wall-clock に対する等倍速の再生位置を優先する。

この表示省略は通常再生専用の表示対象の選択であり、保存済みの第2版の対応付け、統合した replay timeline、tracker packet snapshot、比較用データを削らない。timeline scrubber の操作、Field source selector、`Tracker Comparison` 領域、`Tracker.CaptureReplay` は、引き続き任意の再生時点を選べる経路として維持する。ユーザーが重視する、保存済みの対応付け、再生位置、Field source、比較機能によって「確実に比較できる」能力を落とさない。毎秒30回の通常再生で描画しなかった中間時点でも、位置のドラッグや比較では、保存済みの alignment record から選択・比較できる必要がある。

### 再生操作と速度選択の変更履歴

`TRACKER-062` では、`TRACKER-061` の巨大な再生選択ボタンの配置を撤回し、操作部は従来どおり `Play` / `Fast Forward` のアイコンボタンと `Stop` ボタンの配置へ戻す。`等倍速`、`4x`、`16x`、`64x` は再生操作のボタンではなく、速度選択側の小さなタブ、または同等の選択部として並べる。選択欄へ単純に項目を追加するより、ユーザーの「選択肢のタブ」という意図と、`等倍速` と調査用の倍率が独立した選択肢に見える自然さを優先する。ただし、タブは再生位置を操作する行の補助部品として小さく置き、`TRACKER-061` のような巨大な操作ボタンの集合にはしない。数値の等倍ラベルは使わず、表示は必ず `等倍速` とする。

`TRACKER-062` における速度選択と再生操作の対応は、次の通り固定する。`等倍速` は `DiagnosticsPlaybackMode.Play` と、`TRACKER-060` の毎秒30回相当の実時間追従に対応する。`4x` / `16x` / `64x` は `DiagnosticsPlaybackMode.FastForward` と該当倍率に対応し、`TRACKER-059` の時点を間引かない挙動を維持する。この段階では、再生ボタンを押すと `等倍速` を選択して再生を開始する。早送りボタンは選択中の早送り倍率で開始し、現在の選択が `等倍速` の場合は既定の早送り倍率へ切り替えて開始する。

動作中のモードは、対応する再生・早送りの操作部が停止ボタンへ入れ替わるか、同じ位置の停止ボタンで止める構成とし、速度タブ自体を停止操作に変えない。停止、末尾到達時の先頭戻り、モード切り替え、速度切り替えでは、古い更新要求を破棄する保護を通常再生・早送りの両方で維持する。旧モードまたは旧倍率で待機していた更新要求が選択位置を進めないことを契約にする。保存済みの第2版の対応付け、timeline scrubber、Field source selector、`Tracker Comparison` 領域、`Tracker.CaptureReplay` による任意時点の比較経路は変更しない。

`TRACKER-063` では、上記の `TRACKER-062` のボタン配置を維持したまま、再生ボタンの意味を「現在選択中の速度で再生開始」に補正する。選択中の速度が `等倍速` なら `DiagnosticsPlaybackMode.Play`、早送り倍率なら再生ボタンでも `DiagnosticsPlaybackMode.FastForward` とその倍率で開始する。これにより、早送り速度の選択中に再生ボタンを押しても、選択速度を `等倍速` へ戻さない。

早送りボタンは早送りを明示する操作部として維持し、選択中の速度が早送り倍率なら同じ倍率で開始する。`等倍速` の選択中なら既定の早送り倍率へ切り替えて `FastForward` を開始する。動作中モードの停止ボタンへの入れ替え、末尾到達時の先頭戻り、モード・速度切り替え後の古い更新要求の破棄は、`TRACKER-062` と同じ契約とする。有効な更新要求かどうかの判断では、モードと倍率の両方を照合する。

速度選択 UI は巨大な操作ボタンの集合へ戻さず、再生位置を操作する行の小さな部品として扱う。最小形は、`等倍速` タブと `早送り倍率` の数値入力を同じ速度選択領域へ置く。`4x` / `16x` / `64x` は固定の選択肢一覧ではなく、数値入力へあらかじめ決めた倍率を入れる小さなボタンとして残してよい。表示名は `等倍速` または現在の早送り倍率の `${multiplier}x` とし、数値の等倍ラベル `1x` は使わない。

早送り倍率は固定の `[4, 16, 64]` のいずれかへ丸めず、UI と状態の契約で定める範囲の上下限に制限する。初期範囲は `2x` 以上 `1024x` 以下を目安にし、範囲外の入力は最寄りの有効値へ制限する。既定の早送り倍率は従来の `16x` を維持する。

64倍超の実効速度は、早送りで時点を間引かず、1 再生時点ずつ進める `TRACKER-059` の契約を維持したうえで、タイマー間隔を `max(FastForwardTimerFloor, normalizedTimelineDelta / fastMultiplier)` として計算する。現行の `FastForwardMinimumInterval = 30ms` を固定下限として残すと、`64x` 以上が同じ30 ms間隔へ丸められ、64倍超の可変倍率が実効速度へ反映されない。

`TRACKER-063` では `30ms` の下限を早送りの実効速度の上限として使わず、待機なしで処理を繰り返すことを避けるためのタイマー間隔の小さな下限値、例えば `1ms` 程度へ分離する。`MinimumPlaybackInterval` は時刻差が0以下の場合の保護には使ってよいが、早送り倍率を適用した後の間隔を30 msへ再拡大してはならない。非常に密な時系列では、タイマー下限によって高速化が頭打ちになる可能性は許容する。ただし、少なくとも代表的な1.6秒の時刻差などでは、`64x` より `128x` / `256x` の間隔が短くなり、実効速度が上がることを固定する。


### 異なる更新周期の保持と対応記録

高速なトラッカーの更新時点では、raw vision と render snapshot は、その時点以前の最新のものを保持する。先頭だけ、それ以前の render snapshot がない場合は、後続で最も近いものを代用することを許容する。例えば、raw vision と render snapshot が 0 ms / 100 ms、ER-FORCE のスナップショットが 0 / 20 / 40 / 60 / 80 / 100 ms の場合、20 / 40 / 60 / 80 ms の再生時点は同じ 0 ms の raw vision と render snapshot を参照し、100 ms で 100 ms の raw vision と render snapshot へ進む。これにより再生は高速なトラッカーの更新周期に合わせて進み、低速な raw vision 側は同じ描画内容を保持して段階的に動いて見える。

保存時の alignment sidecar は、診断ログ行単位だけでは不足する。新規記録では、ER-FORCE のような高速トラッカーの各 source sample に対する alignment record も `tracker-snapshot-alignment.jsonl` に保存し、同じ raw vision と render snapshot を複数の高速トラッカーの記録から参照できるようにする。低速な raw vision と render snapshot の更新時点でも、その時点の最新の tracker snapshot に対応する記録を残す。これにより、UI が後から補助ファイルを使って推定するだけでなく、保存済みの比較点として、最速の source cadence に応じた比較根拠を再現できる。

保存形式は既存ファイル名 `tracker-snapshot-alignment.jsonl` を維持し、第2版の整理された記録へ置き換えることを推奨する。別の補助ファイルは、capture metadata、状態、読み取り処理、手動検証の分岐が増え、性能面でも不利なため作らない。互換性は `TRACKER-059` の非要件とし、第1版の読み取りによる代用、省略可能な項目による代用、旧位置引数のコンストラクターを維持するための分岐は入れない。

第2版の記録は、`replayTimelineIndex`、`replayTimelineReceivedAt`、`replayTimelineKind`、`diagnosticsLineNumber?`、`renderFrameNumber?`、`renderReceivedAt?`、`renderMatchRule`、`sourceKey`、`sourceRole`、`sourceLabel`、`remoteEndpoint`、`trackerSnapshotRecordIndex?`、`trackerSnapshotReceivedAt?`、`receivedAtDeltaTicks`、`status` を明示的な項目として持つ。読み取り処理はログを開くときに第2版の JSONL を 1 回だけ読み、再生時点の配列、表示元識別子の索引、基準時点以前の最新描画を探す索引、トラッカーの表示元の索引を構築する。

再生処理・診断画面の出力または UI 表示では、少なくとも次を確認できるようにする。

- 自前トラッカーの確定済みの追跡フレームの時刻
- 対応する source identity、source role、source label
- 採用した時刻の対応規則
- alignment sidecar の有無、alignment record の状態、集約や同条件の候補選択の理由
- tracker snapshot の tracked frame number と tracked frame timestamp
- 時刻差
- ボール・ロボット数
- 処理を省略した件数とエラー数
- 元データへの参照または復元状態

外部トラッカーのパケットはスナップショットとして保持する。`Tracker.CaptureReplay` と `/diagnostics` の再生は、session folder 内の snapshot log と alignment sidecar を読み、保存時の対応規則で自前トラッカーの確定済みの追跡フレームと並べて再生・比較表示できるようにする。再生は未加工入力・追跡結果の render snapshot だけに依存せず、source identity / source role ごとの tracker packet snapshot の時系列と、診断記録の対応付けを入力として扱える必要がある。

capture metadata がない、capture metadata に snapshot sidecar や alignment sidecar のファイルパスがない、補助ファイルがない、capture metadata の `TrackerSnapshotLog.IsCreated=false`、記録件数0、対応記録0、読み取りエラーは、それぞれ UI 上の状態として区別する。これらは、既存の診断ログや render snapshot の表示を壊す理由にはしない。

## 診断画面の Field source の切り替え

`TRACKER-056` では、`/diagnostics` の下部フィールド表示を、左右それぞれ独立した Field source selector で切り替えられるようにする。既定は現在の表示を維持し、左は `Vision Input`、右は自前トラッカーの出力とする。右側は既存の render snapshot の補助ファイルから作る `TrackedVisionViewState` を優先して使い、追跡パケットの補助ファイルに `own` の記録がなくても現行の表示を維持する。

Field source selector は、`Tracker Comparison` 領域内ではなく左右のフィールド表示の見出し行に置く。`Tracker Comparison` 領域は、その見出し行にある切り替えボタンで折りたためるようにし、折りたたみ中も左右の選択欄とフィールド描画は使える状態を維持する。比較領域の折りたたみ状態と左右の表示元選択は、`Diagnostics.razor.cs` のページ状態として保持し、URLクエリ、`sessionStorage`、`localStorage`には保存しない。ログファイルの変更時は左を `Vision Input`、右を自前トラッカーの出力へ戻し、timeline scrubber の操作や playback tick では選択状態を維持する。再読み込み時はページ状態を保持してよいが、選択した source option が新しい表示状態に存在しない場合は既定へ戻す。

Field source の選択肢は次の通りとする。

- `Vision Input`: 選択中の診断記録に対応する render snapshot の `SourceDetections` を既存の変換処理で描画する、仮想的な表示元。
- `ibis tracker`: 選択中の診断記録に対応する render snapshot を、既存の `TrackedVisionViewState.FromSnapshot(...)` で描画する、仮想的な表示元。分類としては `own` に相当するが、既定表示を維持するため補助ファイルの有無には依存させない。
- `External`: 保存済みの alignment sidecar がある場合は、分類が `external` の表示元を集約した代表スナップショットを描画する。対応付けのない既存記録では `unsupported-alignment-missing` とするか、推定であることを明示して既存の近傍時刻の選択を使う。
- `Unknown`: 保存済みの alignment sidecar がある場合は、分類が `unknown` の表示元を集約した代表スナップショットを描画する。対応付けのない既存記録では `External` と同じ状態表示の方針を使う。
- source label: snapshot sidecar 内の正規化済みの source label と完全一致するスナップショット群から、保存済みの対応付けが示す代表を描画する。同じ表示名・UUID に複数の送信元の通信アドレスと通信ポートがある場合は、同条件の候補から代表を選んだ理由を対応記録に残す。

`All` は Field source には使わない。複数の表示元のうち何を描くかが曖昧であり、既存の比較の絞り込みが持つ自前以外を優先する規則をフィールドへ持ち込むと、既定表示や左右比較の根拠が不明確になるためである。`All` は `Tracker Comparison` 領域の数値比較の絞り込みにだけ残す。

トラッカーを表示元とするフィールド描画データは、`TrackerDiagnosticsComparisonViewStateReader` のキャッシュ済み索引、または `TRACKER-059` で置き換える `TrackerDiagnosticsReplayTimelineIndex` 相当の UI に依存しない状態表現から作る。`TRACKER-059` 以降の `Load` は、selected replay timeline tick、比較の絞り込み、左右 Field source 選択を受け取り、既存の `SelectedEntryComparison` 相当の比較結果に加えて、左右の `TrackerDiagnosticsFieldSourceFrame` を返せるようにする。この型は少なくとも次を持つ。

- フィールドの側: 左または右
- 選択中の表示元の種類: `VisionInput` / `IbisOwn` / `External` / `Unknown` / `SourceLabel`
- 状態: 準備済み、診断記録なし、診断記録の追跡フレームなし、render snapshot なし、補助ファイル利用不可、自前トラッカーの比較基準スナップショットなし、比較候補のスナップショットなし、描画対象なし、エラー
- source role と source label
- 対応規則: `saved-session-alignment` / `legacy-nearest-timestamp` / `unsupported-alignment-missing`
- 自前トラッカーの比較基準時刻（ns）
- 診断記録の時刻と、記録開始からの相対受信時間
- 近傍の tracker snapshot の tracked frame number、tracked frame timestamp（ns）、時刻差（ns）
- 対応付けに使った source key と、同条件の候補から代表を選んだ理由
- 元データを復元できたかを示すフラグ
- `TrackerPacketSnapshotSemanticSummary`、または同等のボール・ロボットの位置を表す描画用データ

新規記録での表示元選択は、selected replay timeline tick の安定した識別子から保存済みの alignment record を引き、その記録が参照する tracker snapshot を、source role / source label 別の候補として使う。フィールドと比較領域で別々の規則によってスナップショットを選ばないよう、対応付けの検索と旧形式の近傍選択は、キャッシュ済み索引内の共通処理を使う。対応付けのない既存記録で近傍選択を許可する場合だけ、保持中の診断記録の tracked frame number から自前の `own` スナップショットを引き、その `TrackedFrame.timestamp` を基準時刻として、source role / source label 別の候補から最も近いものを選ぶ。

`TRACKER-055` のキャッシュと索引の経路を維持するため、timeline scrubber の操作、playback tick、Field source selector の変更で、snapshot sidecar や alignment sidecar の JSONL 全体を再読込しない。索引構築は、diagnostics log、capture metadata、snapshot sidecar、alignment sidecar の各ファイルパス、最終更新時刻、長さをキーにした既存のキャッシュ経路へ統合する。フィールド用には元データ全体ではなく、描画に必要な概要情報または最小限の描画用データだけを索引に保持する。通常の書き込み処理が作る記録では `SemanticSummary` を使い、古い記録などで概要情報がない場合だけ、索引構築時に元データから復元する。統合した replay timeline もログ選択時の索引構築で作成し、更新や位置のドラッグごとに補助ファイルを全件再読込しない。

ただし、`TRACKER-059` は性能第一とし、既存の `TrackerDiagnosticsComparisonViewStateReader`、対応付けの読み取り処理、選択中の診断記録を前提とする状態表現が、統合した replay timeline の性能を制限する場合は温存しない。必要なら `TrackerDiagnosticsReplayTimelineIndex` 相当の UI やファイルの入出力から独立した索引を主経路にし、既存の読み取り処理は削除するか、必要最小限の接続処理へ縮小する。判断基準は、ログを開くときに一度だけ構築し、再生・早送り・位置のドラッグ・表示元変更では、補助ファイルの再読込なしで探索量を制限して参照できるかどうかとする。

フィールド描画はすべて `VisionFieldCanvas` を使う。フィールド形状は選択中の render snapshot の形状を使い、トラッカーの表示元の補助ファイルだけから復元しようとしない。トラッカーのボール・ロボットは、`TrackerPacketSnapshotSemanticSummary` から `SSL_DetectionBall` と黄色・青色チーム別の `SSL_DetectionRobot` へ変換する処理を、`DiagnosticsFieldViewFactory` に追加する。チームを黄色・青色と判定できないロボットは無理に描画せず、`TrackerDiagnosticsFieldSourceFrame` の状態や概要で、描画対象が欠落し得ることを示す。

欠落・空・エラー時もフィールド表示領域は消さず、空の `VisionFieldCanvas` または同等の空表示を出し、見出し付近に状態を示す。`Vision Input` / `ibis tracker` で render snapshot がない場合は、既存の render snapshot のエラーを優先する。トラッカーの表示元では、capture metadata なし、補助ファイル未作成・欠落・空・破損、自前の比較基準なし、比較候補なし、近傍スナップショットに描画対象なしを区別し、既存の診断ログや render snapshot の表示を壊す理由にはしない。

`TRACKER-057` の重ね表示は `TRACKER-056` の対象外とする。ただし `TrackerDiagnosticsFieldSourceFrame` は単一表示元の描画入力として独立させ、後続で複数の `TrackerDiagnosticsFieldSourceFrame` を同じ `VisionFieldCanvas` 相当の重ね描画処理へ渡せる最小限の状態表現として再利用する。`TRACKER-056` では重ね合わせ、色分け、凡例、表示・非表示の切り替えは実装しない。

対象を絞ったテストでは、少なくとも次を固定する。

- 統合した replay timeline は診断記録の件数ではなく、利用可能な表示元のうち最速の source cadence を含む。raw vision と render snapshot を 0 ms / 100 ms、ER-FORCE のスナップショットを 0 / 20 / 40 / 60 / 80 / 100 ms にしたテストデータで、20 / 40 / 60 / 80 ms の再生時点を含むことを固定する。
- 保存時の対応付けは診断ログの 2 行だけに減らず、高速トラッカーの source sample 数以上の第2版の alignment record を持つ。20 / 40 / 60 / 80 ms の記録は同じ 0 ms の raw vision と render snapshot を参照し、100 ms の記録は 100 ms の raw vision と render snapshot を参照する。
- ER-FORCE の `TrackedFrame.timestamp` を自前トラッカーと重ならない値にしても、replay timeline の順序と描画内容の保持は `ReceivedAt` または記録開始からの相対受信時間で決まる。
- `/diagnostics` の再生・早送り・位置のドラッグは統合した replay timeline の索引を使い、高速トラッカーの更新時点では、`Vision Input` / `ibis tracker` のフィールド表示が、その時点以前の最新の render snapshot を保持する。
- 等倍速の `Play` は毎秒30回相当の表示更新で、開始時の wall-clock と開始時点の `ReceivedAt` から目標の収録時刻を計算し、その時刻以下の最新再生時点へ追従する。200 Hz の更新を持つテストデータでは、開始から1秒後に約30個目の逐次更新位置ではなく、wall-clock で1秒相当の時点へ進むことを固定する。
- 通常再生で表示を省略した中間時点でも、timeline scrubber の操作、Field source selector、比較は、selected replay timeline tick と保存済みの第2版の alignment record から、任意時点を選択・比較できることを固定する。
- 早送りは通常再生専用の実時間追従へ巻き込まず、既存の調査用の収録時刻差を倍率で割る挙動を維持する。
- 再生操作は `Play` / `Fast Forward` のアイコンボタンと `Stop` ボタンの従来配置を持ち、`等倍速` / `4x` / `16x` / `64x` を巨大な操作ボタンとして描画しないことを固定する。
- 速度選択は小さなタブ、または同等の選択部として `等倍速`、`4x`、`16x`、`64x` を並べ、数値の等倍ラベルを表示しないことを固定する。
- `等倍速` の選択は `DiagnosticsPlaybackMode.Play` と `TRACKER-060` の毎秒30回相当の実時間追従に対応し、早送り倍率を変更しないことを、UI の状態とコンポーネントの契約で固定する。
- `4x` / `16x` / `64x` の選択は `DiagnosticsPlaybackMode.FastForward` と該当倍率に対応し、`TRACKER-059` の時点を間引かない挙動を維持することを固定する。
- 再生ボタンは現在選択中の速度で開始し、`等倍速` なら通常再生、早送り倍率なら `FastForward` とその倍率で開始することを固定する。早送りボタンは選択中または既定の早送り倍率で `FastForward` を開始することを固定する。
- 動作中のモードは停止ボタンで停止でき、停止・モード切り替え・速度切り替えの後は、待機中の古い更新要求が破棄されることを固定する。
- `TRACKER-063` の速度選択は、`等倍速` タブと可変の `早送り倍率` 入力を小さく並べ、`4x` / `16x` / `64x` はあらかじめ決めた倍率を数値入力へ入れる任意の補助ボタンに留める。状態の契約は固定の選択肢一覧ではなく、範囲の上下限によって64倍超を扱うことを固定する。
- `TRACKER-063` の早送り間隔は、`normalizedTimelineDelta / fastMultiplier` にタイマー間隔の小さな下限値だけを適用する。30 msの固定下限や固定の選択肢への丸めによって、`128x` / `256x` などが `64x` と同じ実効速度に制限されないことを固定する。
- Field source の選択肢は `Vision Input`、`ibis tracker`、`External`、`Unknown`、source label を持ち、`All` を含めない。
- 既定は左 `Vision Input`、右が自前トラッカーの出力であり、ログ変更時に既定へ戻る。
- selected replay timeline tick と source label / source role から、比較と同じ対応付け、または推定であることを明示したスナップショットの概要情報が、`TrackerDiagnosticsFieldSourceFrame` に返る。
- 新規記録では、selected replay timeline tick と source label / source role から、保存済みの alignment sidecar が参照する tracker snapshot の概要情報が、`TrackerDiagnosticsFieldSourceFrame` に返る。
- 回帰検証は、先に失敗するテストから追加する。外部トラッカーの `TrackedFrame.timestamp` の範囲が、自前トラッカーの `TrackedFrame.timestamp` の範囲と重ならないテストデータを作り、データ時刻の近さだけに戻る実装では失敗することを固定する。
- 保存時の対応付けの回帰検証では、外部の時刻が自前と重ならなくても、収録時刻による対応付けで、選択中の再生時点と保持中の描画内容に対応する外部スナップショットが、再生フィールドに選ばれることを検証する。
- 対応付け結果の時間軸検査として、選択中の再生時点の `ReceivedAt` と、選ばれた外部スナップショットの収録時の `ReceivedAt` との差が、許容範囲内であることを検証条件に含める。許容範囲は対応する機能の実装時に明示し、テストデータのパケット間隔より十分小さい値に固定する。
- alignment sidecar がない既存記録では、外部トラッカーや表示元名で選ぶフィールドの状態が、非対応または推定であることを明示した状態になる。
- 表示元変更、再生位置のドラッグ、再生更新で補助ファイルや対応付けの全件再読込へ戻らず、`TRACKER-055` の索引キャッシュと、ログ選択時に構築した replay timeline の索引を使う。
- 欠落、空、破損、自前の比較基準なし、比較候補なし、描画対象なしの状態が、フィールド表示用の状態表現に残る。
- `DiagnosticsFieldViewFactory` が、概要情報のボールと黄色・青色チームのロボットを `VisionFieldCanvas` 用 DTO へ変換する。

## 診断画面のフィールド重ね合わせ表示

`TRACKER-057` では、`TRACKER-056` の左右の Field source selector と `TrackerDiagnosticsFieldSourceFrame` を再利用し、選択中の診断記録に対する 2 つの表示元の重ね表示を追加する。必須ではない要望として扱うため、選択欄を別体系に増やす実装や、任意個数の表示元を重ねる機能は、この PR の最小実装に含めない。

重ね表示の切り替えは、フィールド表示領域の見出し行へ置く。左右の表示元選択は維持し、表示方式は `Split` / `Overlay` を選ぶ操作部、または同等の二択操作で、フィールド表示領域全体に対して切り替える。`Split` は現行どおり左右のフィールドを並べ、`Overlay` は同じ左右の選択結果を `Layer A` / `Layer B` として 1 枚のフィールドに重ねる。`Tracker Comparison` 領域の折りたたみ状態とは独立させ、折りたたみ中も表示方式の切り替え、左右の選択欄、重ね表示の凡例と表示・非表示操作を使えるようにする。

重ねる対象は追加の複数選択ではなく、現在の左右のフィールド表示の 2 つの表示元に限定する。既定は左 `Vision Input`、右が自前トラッカーの出力であるため、初期の重ね表示は両者の重ね合わせになる。`External`、`Unknown`、source label は `TRACKER-056` の `TrackerDiagnosticsFieldSourceFrame` を使い、新規記録では保存済みの対応付け、既存記録では非対応または推定であることを明示した経路で解決する。`All` は引き続き使わない。左右が同じ表示元なら 1 層として扱い、凡例に同一の表示元であることを示す。

色分けは表示層の識別用であり、黄色・青色チームの意味を置き換えない。最小仕様では、`Layer A` をシアン系の線と表示名、`Layer B` をマゼンタ系の線と表示名とし、ロボット本体の黄色・青色の塗りは維持する。ボールは層の色の円形の目印または線で区別する。重なりを読めるように、`Layer B` は破線または半透明の線を使う。凡例は重ねたフィールドの近くへ置き、各層の表示名、source role / source label、状態、対応付けの時刻差または推定比較の時刻差、記録件数または描画対象数を最小限表示する。

表示・非表示は凡例内の層ごとのチェックボックスまたは切り替え操作で制御し、既定は両層とも表示とする。この状態は `Diagnostics.razor.cs` のページ状態に保持し、URLクエリ、`sessionStorage`、`localStorage`には保存しない。ログファイルの変更時は両層とも表示へ戻し、再生位置のドラッグ、再生更新、表示元変更では現在の表示・非表示を維持する。片方を非表示にしても、表示元の選択自体は変えない。

描画部品は、既存の `VisionFieldCanvas` を複数表示元の入力へ拡張するのではなく、`Tracker.DebugHost` の診断用の重ね表示コンポーネントを追加する。`VisionFieldCanvas` は raw vision や単一表示元の汎用コンポーネントとして維持する。重ね表示側は `VisionFieldProjection`、`VisionFieldLines`、`VisionRenderOptions`、既存のフィールド形状 DTO を再利用する。層別のマーカーの見た目が必要な場合は、`VisionBallMarker` / `VisionRobotMarker` に任意の `class` と `stroke` の指定を最小限追加するか、重ね表示側で層のマーカーを直接描く。既存の単一表示元の表示、拡大縮小、表示位置の移動、カーソル座標の重ね表示を壊さない範囲に留める。

重ね表示用の状態表現は `TRACKER-056` の `TrackerDiagnosticsFieldSourceFrame` を直接再利用し、未加工の `Vision Input` と自前トラッカーの出力も同じ表示層へ変換できる小さな表示用の状態表現を `Diagnostics.razor.cs` または専用の生成処理で作る。トラッカーの層は `TrackerPacketSnapshotSemanticSummary` を `DiagnosticsFieldViewFactory` の変換処理で、ボール・黄色チームのロボット・青色チームのロボットへ変換する。render snapshot 由来の層は、既存の source detections と `TrackedVisionViewState.FromSnapshot(...)` を使う。近傍の選択、自前の比較基準時刻、比較候補なし等の状態判定は、`TrackerDiagnosticsComparisonViewStateReader.LoadFieldSourceFrame(...)` とキャッシュ済み索引を使い、重ね表示専用に補助 JSONL ファイルを再読込しない。

欠落、空、フィールド形状なし、比較候補なしの扱いは `TRACKER-056` と揃える。render snapshot geometry がない場合は、形状なしの空表示とし、トラッカーの表示元の補助ファイルだけから形状を復元しない。capture metadata なし、補助ファイル未作成・欠落・空・破損、自前の比較基準なし、比較候補なし、描画対象なしは、層の状態として凡例へ示す。他に準備済みの層があれば、その層だけを描画する。両層とも描画不可でも領域は消さず、空のフィールドと状態を表示する。

対象を絞ったテストでは、少なくとも次を固定する。

- 表示方式の状態は `Split` / `Overlay` を持ち、ログファイル変更時に `Split` または既定の方式へ戻す。再生位置のドラッグと再生更新では、表示方式と表示・非表示を維持する。
- 重ねる対象は左右の選択欄の 2 つの表示元であり、専用の表示元一覧や `All` を追加しない。
- 表示層は `Vision Input`、自前トラッカー、`External`、`Unknown`、source label を混在できる。トラッカーの表示元は `TRACKER-056` と同じ `TrackerDiagnosticsFieldSourceFrame`、対応付けの検索、キャッシュ済み索引を使う。
- 補助ファイル利用不可、自前の比較基準なし、比較候補なし、描画対象なし、フィールド形状なしが層の状態として残り、準備済みの層まで消さない。
- 表示・非表示の切り替えは表示元の選択を変えず、非表示の層を重ね描画から除外する。
- コンポーネントまたは生成処理で、Layer A/B の色分け、凡例の表示値、概要情報からボールと黄色・青色それぞれのロボットへの変換を検証する。

実装対象は `TrackerDiagnosticsComparisonUiState`、`TrackerDiagnosticsComparisonViewStateReader`、`Diagnostics.razor` / `.cs` / `.css`、診断用の重ね表示コンポーネント、`DiagnosticsFieldViewFactory`、関連する対象を絞ったテスト、必要なら `Tracker.DebugHost/README.md` に限定する。対象外は、受信処理、スナップショットの書き込み、capture metadata のデータ構造、`Tracker.Core` の追跡アルゴリズム、`Tracker.CaptureReplay` の出力変更、任意個数の表示元の重ね合わせ、設定の永続化とする。

## 後続作業への固定事項

- `TRACKER-047` では、既存の `TrackerSnapshotReplayReader` / `TrackerReplayIntegrationTddTests` のレビューを完了する。対象を絞った4件、関連する39件、`Tracker.Tests` 全体191件が成功した実装検証済み状態を保持し、`gpt-5.5 high` によるレビューで進行を妨げる指摘がないことを確認する。指摘が出た場合は、修正・再検証・第2回レビューまで完了する。
- `TRACKER-048` では、診断ログ・再生の比較表示と出力へ接続する。capture metadata の相対パスから snapshot sidecar を読み、source role / source label、tracked frame timestamp、ボール・ロボット数、元データの復元状態、近傍時刻の比較概要を、`Tracker.CaptureReplay` または診断再生で確認可能にする。既存のキャプチャー・診断・ render snapshot の表示を壊さない。
- `TRACKER-049` では、診断比較の設計と進捗管理を再同期する。CLI 比較実装は保持し、`/diagnostics` の UI 比較を PR のレビュー準備完了前の固定作業へ入れ、後続作業の依存関係と完了条件を明確にする。
- `TRACKER-050` では、診断比較の読み取り処理と表示状態の契約を追加する。診断ログのファイルパスから capture metadata と補助ファイルを解決し、表示元一覧、選択中の表示元の絞り込み、選択中の記録の比較、補助ファイルの状態、省略・エラー件数を、UI から独立した状態表現として固定する。
- `TRACKER-051` では、`/diagnostics` の UI へ比較表示と表示元の絞り込みを接続する。選択中のログ・記録・再生時点と比較の表示状態を同期し、既存の render snapshot、設定ダイアログ、時系列表示、再生操作、大きさを変更可能なレイアウトを壊さない。
- `TRACKER-052` では、CaptureOn 比較ログの運用文書と手動検証の証跡を、UI 比較完了後の実態へ更新する。CLI はエージェント・検証用、通常確認は `/diagnostics` の comparison panel を主経路として説明する。
- `TRACKER-054` では、トラッカーの受信処理の受信先を上書きする設定を追加する。既定は起動時に解決した自前トラッカーの送信先を監視し、`Tracker:Receive:MulticastAddress` / `Port` の指定時は受信処理独自の受信先を監視する。実行中の設定プロファイルの切り替え後のソケット再構成は対象外とし、起動時固定として README と設計に明記する。
- `TRACKER-055` では、diagnostics playback と timeline scrubber の操作の低速問題を解消する。更新時は軽量な索引キャッシュから比較を更新し、補助ファイルのサイズに比例する再読込へ戻さない。
- `TRACKER-056` では、`Tracker Comparison` 領域を折りたたみ可能にし、左右の Field source を `Vision Input`、`ibis tracker`、`External`、`Unknown`、source label から選べるようにする。既定は左 `Vision Input`、右が自前トラッカーの出力とする。トラッカーの表示元は、選択中の診断記録に対する対応付け、または当時の近傍時刻のスナップショットを描画する。`All` は Field source には使わない。
- `TRACKER-057` では、必須ではない要望としてフィールドの重ね合わせ表示を追加し、`TRACKER-056` の左右の Field source selector と `TrackerDiagnosticsFieldSourceFrame` を再利用する。最小実装は左右2つの表示元の重ね合わせ、層の色分け、凡例、表示・非表示に限定し、任意個数の重ね合わせや設定の永続化は含めない。実装が複雑化する場合は、PR のレビュー準備完了前に延期の判断を報告書へ明記する。
- `TRACKER-053` では、PR #9 のレビュー準備を完了する。PR 本文を `TRACKER-040` から最終状態まで更新し、最終検証、レビュー証跡、リスク整理、進捗同期、下書き解除の判断材料を揃える。
- `TRACKER-058` では、新規キャプチャーの保存時の alignment sidecar を追加し、外部トラッカーの時刻が自前と異なる時刻系でも、Field source と CLI 比較が保存済みの対応付けを優先できるようにする。
- `TRACKER-059` では、診断再生の時系列を利用可能な最速の source cadence へ合わせる。再生・早送り・位置のドラッグは診断記録の件数ではなく統合した replay timeline を使い、高速トラッカーの時点では、raw vision と render snapshot をその時点以前の最新のもので保持する。対応付け用の補助ファイルは既存ファイル名のまま第2版の整理された記録へ置き換え、診断ログ行だけでなく高速トラッカーの source sample ごとの alignment record を残す。互換用の代用処理は入れず、ログを開くときの索引構築と、更新・位置移動時の `O(1)` または探索量を制限した参照を優先する。
- `TRACKER-060` では、等倍速の `Play` だけを毎秒30回相当の表示更新で wall-clock の経過時間へ追従させる。開始時点の `ReceivedAt` と開始時の wall-clock から目標の収録時刻を計算し、その時刻以下の最新再生時点を表示対象にする。保存済みの第2版の対応付け、位置の操作、Field source 選択、比較は任意時点を比較できる経路として維持し、早送りでは既存の調査用の収録時刻差を倍率で割る挙動を壊さない。
- `TRACKER-061` では診断再生の `等倍速` と `4x` / `16x` / `64x` を別の再生選択肢として表現したが、ユーザー意図より操作ボタンが大きく変わりすぎたため、`TRACKER-062` で UI 形状を修正する。`TRACKER-062` では再生・早送り・停止の従来配置を戻し、速度選択側の小さなタブに `等倍速`、`4x`、`16x`、`64x` を並べる。`等倍速` は `DiagnosticsPlaybackMode.Play`、各倍率は `DiagnosticsPlaybackMode.FastForward` と該当倍率に対応する。保存済みの第2版の対応付け、位置の操作、Field source 選択、比較、`TRACKER-060` の実時間追従、`TRACKER-059` の早送りで時点を間引かない挙動は壊さない。
- `TRACKER-063` では `TRACKER-062` のボタン配置を維持したまま、早送り速度の選択中に再生ボタンが `等倍速` へ戻す挙動を修正する。再生ボタンは現在選択中の速度で開始する操作とし、早送り倍率を選択中なら `FastForward` で開始する。固定の `4x` / `16x` / `64x` だけに依存せず、`等倍速` と可変の早送り倍率入力を小さく並べ、倍率の正規化やタイマー下限によって64倍超の倍率を無効化しない。
- `TRACKER-059` 以降のソケット抽象化等の堅牢性の改善は、今回の PR へ含める判断が明示された場合、またはユーザー承認がある場合だけ追加する。

## 完了条件

- CaptureOn 中に見えている追跡パケットを、自前のパケットも除外せず補助 JSONL ファイルに保存できる。
- トラッカーの受信処理は起動時に解決した自前トラッカーの送信先を既定で監視し、`Tracker:Receive:MulticastAddress` / `Port` の指定時は受信処理独自の受信先を監視できる。
- 同じ CaptureOn の記録単位で生成されるキャプチャー、capture metadata、トラッカーの診断記録、render snapshot、tracker packet snapshot の snapshot sidecar（JSONL）が一つの session folder にまとまる。異なる CaptureOn の開始時点に属するログは、別フォルダに分かれる。
- capture metadata から session folder と、各ファイルへの相対パスを辿れる。
- Capture Off から再度記録を開始するときに、session folder とスナップショットの書き込み処理が切り替わり、前のフォルダへ追記しない。
- 他のトラッカーが存在しない新規記録では、キャプチャー、診断ログ、render snapshot の通常挙動を変えず、旧形式は機能制限付きで表示できる範囲だけ扱う。
- 旧診断ログ・旧 render snapshot の補助ファイルは旧形式または推定表示として扱う。snapshot sidecar や diagnostics sample sidecar がある最新経路では、性能を最優先し、探索量を制限した参照で追加の比較情報を読める。
- 外部トラッカーのスナップショットを、`Tracker.CaptureReplay` の CLI 出力と `/diagnostics` の comparison panel と再生機能で、再生・比較表示できる。
- `/diagnostics` の左右のフィールド表示で `Vision Input`、`ibis tracker`、`External`、`Unknown`、source label を選択でき、既定は左 `Vision Input`、右が自前トラッカーの出力のまま維持される。
- `Tracker Comparison` 領域を折りたたんでも、Field source selector とフィールド描画を使える。
- `/diagnostics` の重ね表示方式で、左右の選択欄の2つの表示元を同じフィールドへ重ね、層ごとの色分け、凡例、表示・非表示を確認できる。
- raw vision より高速なトラッカーの表示元がある場合、再生位置の操作や再生更新は統合した replay timeline の高速トラッカーの時点を含み、raw vision と render snapshot は、その時点以前の最新の描画内容を保持する。
- 等倍速の `Play` は毎秒30回相当の表示更新で、wall-clock の経過時間に対応する最新の replay timeline tick へ追従し、高頻度な更新を全件逐次描画して遅れ続けない。
- 再生 UI は再生・早送り・停止の従来のボタン配置を持ち、速度選択側に `等倍速`、`4x`、`16x`、`64x` の小さなタブを表示する。
- 保存時の alignment sidecar は、最速の source cadence に応じた比較記録を持ち、複数の高速トラッカーの記録が同じ raw vision と render snapshot を参照できる。
- 再生位置のドラッグ、再生更新、表示元変更で、追跡パケットや対応付けの補助 JSONL ファイル全体を再読込しない。
- 小さな作業ごとに TDD、レビュー、コミット、PR の確認を完了している。
