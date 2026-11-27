# 過去の作業状況

この文書は当時の作業と完了判断を記録した履歴である。現在の状態は[現行の作業一覧](../../tasks-status.md)を参照する。変更前の文章は[原文の保存版](../../../../reports/history/core-tasks-before-terminology.md)に保持する。検証結果・条件・対象外事項・判断根拠は、この本文にも記載する。

規則: この文書は `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 当時の作業状態

ID、表題、状態、TDD・設計・実装・レビューの記録、規模、依存関係、完了条件はいずれもなし。工程は比較ログ。次の調査作業もなし。

## 固定作業

固定順は `TRACKER-047`、`TRACKER-048`、`TRACKER-049`、`TRACKER-050`、`TRACKER-051`、`TRACKER-052`、`TRACKER-054`、`TRACKER-055`、`TRACKER-056`、`TRACKER-057`、`TRACKER-053`、`TRACKER-058`、`TRACKER-059`、`TRACKER-060`、`TRACKER-061`、`TRACKER-062`、`TRACKER-063` とした。進捗管理と設計では補助番号を使わない。`TRACKER-063` の不具合修正と可変倍率追加は同じ再生速度制御の作業とし、別の作業番号へ分割しない。

## 作業一覧

| ID | 作業 | 工程 | 状態 | 依存関係 | 完了判断の本文 |
| --- | --- | --- | --- | --- | --- |
| `TRACKER-039` | 追跡フレーム 3448付近で青1番が11番へ変わる原因を調査・修正する | 調査 | 完了 | `TRACKER-038` | [履歴](#tracker-039) |
| `TRACKER-040` | CaptureOn比較ログの設計と進捗管理を追加する | 比較ログ | 完了 | `TRACKER-039` | [履歴](#tracker-040) |
| `TRACKER-041` | 自分自身を含む全トラッカーのパケットを保存する方針へ変更する | 比較ログ | 完了 | `TRACKER-040` | [履歴](#tracker-041) |
| `TRACKER-042` | 全トラッカーのパケットを保持する契約を実装する | 比較ログ | 完了 | `TRACKER-041` | [履歴](#tracker-042) |
| `TRACKER-043` | CaptureOnのsession folderとcapture metadataの相対パスを追加する | 比較ログ | 完了 | `TRACKER-042` | [履歴](#tracker-043) |
| `TRACKER-044` | CaptureOn中の全トラッカーのパケットを保存する | 比較ログ | 完了 | `TRACKER-043` | [履歴](#tracker-044) |
| `TRACKER-045` | 外部トラッカーの受信をsnapshot sidecarへの書き込みに接続する | 比較ログ | 完了 | `TRACKER-044` | [履歴](#tracker-045) |
| `TRACKER-046` | トラッカー受信処理を起動時に登録する | 比較ログ | 完了 | `TRACKER-045` | [履歴](#tracker-046) |
| `TRACKER-047` | tracker snapshotの再生読み取りを実装し、レビュー指摘を解消する | 比較ログ | 完了 | `TRACKER-046` | [履歴](#tracker-047) |
| `TRACKER-048` | CLIの再生結果へトラッカー比較の出力を追加する | 比較ログ | 完了 | `TRACKER-047` | [履歴](#tracker-048) |
| `TRACKER-049` | 診断画面での比較を設計と固定作業へ反映する | 比較ログ | 完了 | `TRACKER-048` | [履歴](#tracker-049) |
| `TRACKER-050` | 診断比較の読み取り処理と表示状態の契約を追加する | 比較ログ | 完了 | `TRACKER-049` | [履歴](#tracker-050) |
| `TRACKER-051` | 診断画面へ比較表示と入力元の絞り込みを接続する | 比較ログ | 完了 | `TRACKER-050` | [履歴](#tracker-051) |
| `TRACKER-052` | 比較ログの運用説明と手動検証の記録項目を更新する | 比較ログ | 完了 | `TRACKER-051` | [履歴](#tracker-052) |
| `TRACKER-054` | トラッカー受信先を個別に上書きできるようにする | 比較ログ | 完了 | `TRACKER-052` | [履歴](#tracker-054) |
| `TRACKER-055` | 診断再生とtimeline scrubberの操作が遅い問題を解消する | 比較ログ | 完了 | `TRACKER-054` | [履歴](#tracker-055) |
| `TRACKER-056` | Field sourceの左右切り替えと比較表示の折り畳みを追加する | 比較ログ | 完了 | `TRACKER-055` | [履歴](#tracker-056) |
| `TRACKER-057` | 2つのField sourceを重ねて表示する | 比較ログ | 完了 | `TRACKER-056` | [履歴](#tracker-057) |
| `TRACKER-053` | PR #9をレビュー可能な状態にするための判断材料をそろえる | 比較ログ | 完了 | `TRACKER-057` | [履歴](#tracker-053) |
| `TRACKER-058` | 診断再生でER-Forceのtracker snapshotを描画できない原因を修正する | 比較ログ | 完了 | `TRACKER-053` | [履歴](#tracker-058) |
| `TRACKER-059` | replay timelineを最も更新頻度が高いトラッカーに合わせる | 比較ログ | 完了 | `TRACKER-058` | [履歴](#tracker-059) |
| `TRACKER-060` | 等倍速再生を30fps相当の更新で実時間に追従させる | 比較ログ | 完了 | `TRACKER-059` | [履歴](#tracker-060) |
| `TRACKER-061` | 等倍速と調査用の倍率を別の再生操作として表示する | 比較ログ | 完了 | `TRACKER-060` | [履歴](#tracker-061) |
| `TRACKER-062` | 従来の再生ボタン配置に戻し、速度選択に等倍速を追加する | 比較ログ | 完了 | `TRACKER-061` | [履歴](#tracker-062) |
| `TRACKER-063` | 再生開始時の速度選択を維持し、早送り倍率を可変にする | 比較ログ | 完了 | `TRACKER-062` | [履歴](#tracker-063) |

## 作業別の履歴

### `TRACKER-039`

追跡フレーム 3448付近で青1番が11番へ変わる原因を調査・修正する。

診断ログの `trackedFrame` 3448 付近では、raw vision に青1番と青11番が同じ位置付近で重複し、青1番が別のロボットの位置にも現れていた。同じ追跡フレームにまとめる時間幅（`MergeWindowNs`）内で、後続の同一 ID 候補が既存の追跡位置に近い候補を上書きし、突然の ID 変更を位置ずれより起こりにくい事象として扱っていなかったことが原因だった。

同じ ID の既存の追跡位置に近い候補を優先し、別 ID の既存の追跡位置付近への突然の ID 変更を抑制した。調整値は `RobotTracker.IdentitySwitchDistanceMm` として外部設定にした。ID が突然変わることを失敗条件とした再発防止テストは、当時の `git stash` による旧実装との比較で失敗し、修正後に成功した。

初回レビューの中程度の指摘は進捗文書の同期漏れであり、対応済み。r2 レビューは指摘なし。PR #8（`https://github.com/ibis-ssl/Duck/pull/8`）は `2026-05-12T00:06:33Z` に統合済み。

詳細レポート:

- `reports/tracker-039-evidence-20260512084929.md`
- `reports/tracker-039-review-20260512085258.md`
- `reports/tracker-039-review-r2-20260512090207.md`

### `TRACKER-040`

CaptureOn比較ログの設計と進捗管理を追加する。

比較ログの工程と後続作業を追加した。トラッカーのパケットをキャプチャーする処理の接続先の第一候補を `TrackerConnectionLib` とし、旧 `Tracker.Server` で CaptureOn の比較ログへ統合する設計とした。`Tracker.Core` はキャプチャーと比較用保存の対象外とした。

snapshot sidecar の JSONL を主記録とし、診断機能からの互換参照、入力元の識別、近い時刻での比較、Capture Off 後の再開、他のトラッカーがない場合の扱いを文書化した。同じ CaptureOn のキャプチャー、capture metadata、トラッカーの診断ログ、render snapshot、tracker packet snapshot の JSONL を1つの session folder にまとめる仕様とした。

当時の `tracker-server-cli-ui-detail-design.md` を比較ログの機能設計、`tracker-server-cli-ui-maintainability-design.md` を `TRACKER-034` の保守性設計として分離した。進捗管理の簡略化と履歴の分離は PR 準備の保守・運用作業であり、CaptureOn 比較ログの機能仕様には含めない。

実装前に下書きの PR #9（`https://github.com/ibis-ssl/Duck/pull/9`）を作成・更新した。この段階では製品とテストのソースコードは変更していない。`gpt-5.5 high` の初回レビューと、設計分離と session folder の修正後の r2 レビューはいずれも完了を妨げる指摘なし。機能設計と保守性設計の分離、session folder 構造を含む設計・進捗管理の差分は、2026-05-12 に利用者承認済み。設計分離、記録先修正、レビュー後・承認後の同期の記録を下記に保持する。

詳細レポート:

- `reports/tracker-040-design-review-20260512094448.md`
- `reports/tracker-040-design-review-r2-20260512102542.md`
- `reports/tracker-040-progress-sync-20260512094809.md`
- `reports/tracker-040-design-separation-fix-20260512100723.md`
- `reports/tracker-040-session-folder-design-fix-20260512101934.md`
- `reports/tracker-040-r2-progress-sync-20260512102917.md`
- `reports/tracker-040-approval-sync-20260512105353.md`

### `TRACKER-041`

自分自身を含む全トラッカーのパケットを保存する方針へ変更する。

自分自身のパケットを保存対象から除外する前提を撤回した。受信できた `TrackerWrapperPacket` はすべて snapshot sidecar に保存し、`ibis` 自身の official tracker packet と詳細ログが重複して保存されることを許容する。

自分自身・外部・不明という区分は、保存を除外するためではなく後の表示・比較で使う source role / source label などの情報として保持する。判別できない場合も保存を省略しない。

外部の tracker snapshot は、比較元のパケットのバイト列または復元可能な情報、入力元の `uuid` / `sourceName` / 送信元の通信アドレスと通信ポート、`receivedAt`、tracked frame number と tracked frame timestamp、集計情報を持つ。`Tracker.CaptureReplay`、診断画面、再生機能は session folder 内の記録から再生・比較できる設計へ変更した。設計修正と設計・実装の監査結果を下記に保持する。

詳細レポート:

- `reports/tracker-041-all-trackers-design-fix-20260512111628.md`
- `reports/tracker-041-all-trackers-design-audit-20260512111218.md`
- `reports/tracker-041-all-trackers-implementation-audit-20260512111218.md`

### `TRACKER-042`

全トラッカーのパケットを保持する契約を実装する。

`TrackerConnectionLibAllTrackerSnapshotContractTests.cs` の契約に合わせ、`MultiTrackerManager` が自分自身のパケットを直ちに処理対象外とする分岐を廃止した。自分自身・外部・不明を保存除外条件にせず、`TrackerState` の `SourceRole` / `SourceLabel` に保持した。

自分自身と外部のパケットを `uuid` / `sourceName` / 送信元の通信アドレスと通信ポートごとに観測状態と tracker snapshot として保持し、保存した状態から元の official tracker packet の内容を復元できるようにした。対象の `TrackerConnectionLibAllTrackerSnapshotContractTests` は5件、全体の `Tracker.Tests` は163件成功。`gpt-5.5 high` のレビューは完了を妨げる指摘なし。

入力元ごとに利用中のトラッカーを取得する API の扱いと、同じ `uuid` が衝突する場合の扱いは、`TRACKER-043` 以降で追跡するリスク・後続候補とし、この作業の完了を妨げる条件とはしない。snapshot sidecar の JSONL、CaptureOn の capture metadata / session folder、診断再生はこの段階では未実装であり、後続作業の範囲とした。

詳細レポート:

- `reports/tracker-042-all-trackers-tdd-contract-20260512112546.md`
- `reports/tracker-042-all-trackers-implementation-20260512113459.md`
- `reports/tracker-042-review-20260512114147.md`
- `reports/tracker-042-verification-20260512114147.md`
- `reports/tracker-042-progress-sync-20260512114544.md`

### `TRACKER-043`

CaptureOnのsession folderとcapture metadataの相対パスを追加する。

先に失敗するテストを作成した。`TrackerCaptureOnSessionSnapshotContractTests` では、同じ CaptureOn のキャプチャー、capture metadata、診断ログ、render snapshot、tracker snapshot の相対パスを、1つの session folder 内の記録としてたどれることを固定した。Capture Off 後の再開では別の session folder に切り替え、自分自身・外部・不明と再生に必要な情報を記録し、記録の読み取り処理を再生・診断の入力として使えることも確認した。

session folder を基準とした相対パス、snapshot log の情報、空の入力元一覧、`TrackerPacketSnapshotRecord` / `TrackerPacketSnapshotLogReader` を追加した。既存の診断ログ読み取り処理も session folder 配下を列挙できるようにした。対象テスト5件、関連13件、全体の `Tracker.Tests` 168件が成功し、`gpt-5.5 high` のレビューは完了を妨げる指摘なし。

表示用データとしての tracker snapshot 自体は問題ないが、比較元データを保持することは `TRACKER-044` の通常の処理経路で必須とした。関連ファイルを名前で対応付ける従来の考え方は session folder 名とその中のファイル名で維持する。入力元ごとに利用中のトラッカーを取得する API の扱いと、同じ `uuid` が衝突する場合の扱いは、次作業の入力元集計・役割判定で扱うリスクとし、パケットのバイト列と source identity を失わない限り、この作業の完了を妨げる条件とはしない。

詳細レポート:

- `reports/tracker-043-review-20260512120832.md`
- `reports/tracker-043-review-followup-sync-20260512121304.md`

### `TRACKER-044`

CaptureOn中の全トラッカーのパケットを保存する。

先に失敗するテストを作成した。`TrackerComparisonSourceTddTests` で パケットのバイト列の round-trip（保存後の読み戻し）と解析、CaptureOn の書き込み処理、元データに基づく `SemanticSummary`、自分自身・外部・不明の全保存、同じ `uuid` が衝突した場合の source identity の保持を固定した。

`TrackerPacketSnapshotLogWriter` を追加し、パケットのバイト列を持つ JSONL への追記と書き出し、元データからの `SemanticSummary` 作成、読み取り時の不足情報の補完、capture metadata の `RecordCount` / `SkippedRecordCount` / `ErrorCount` と入力元集計の更新を実装した。対象の `TrackerComparisonSourceTddTests` は7件、関連30件、全体の `Tracker.Tests` は175件成功。`gpt-5.5 high` のレビューは完了を妨げる指摘なし。

レビュー後に `SemanticSummary` のボール数、ロボット数、tracked frame、入力元集計の値が一致する検証を追加した。追加後も対象7件、関連30件が成功した。実行中の外部トラッカー受信との接続は次作業とした。`Append` を直接使った際の不正なパケットのバイト列への対策は通常の処理経路外の追加課題として保持する。

詳細レポート:

- `reports/tracker-044-review-20260512123921.md`
- `reports/tracker-044-review-followup-20260512124330.md`

### `TRACKER-045`

外部トラッカーの受信をsnapshot sidecarへの書き込みに接続する。

先に失敗するテストを作成した。`TrackerLiveExternalTrackerReceiverTddTests` で、CaptureOn 中の `TrackerConnectionLib` の受信から書き込みへの接続、自分自身・外部・不明の全保存、CaptureOff 中は書き込まないこと、再開時に別の session folder を使うこと、比較元のパケットのバイト列 / `SemanticSummary` の保持を固定した。

`TrackerConnectionLibSnapshotRecorder` を追加し、`MultiTrackerManager<TrackerPacketAdapter>` の更新から `TrackerPacketSnapshotLogWriter` へパケットと入力元の情報を渡した。旧 `Tracker.Server` から `TrackerConnectionLib` への `ProjectReference` を追加した。CaptureOff と再開は書き込み処理と記録単位の開始・終了に従う。対象テスト5件、関連35件、全体の `Tracker.Tests` 180件が成功し、`gpt-5.5 high` のレビューは完了を妨げる指摘なし。

起動時の登録は `TRACKER-046` の範囲として残した。CaptureOff と処理が競合した際の書き込み例外の伝播は、受信処理を常駐させる段階で再確認するリスクとして保持する。

詳細レポート:

- `reports/tracker-045-live-receiver-implementation-20260512125847.md`
- `reports/tracker-045-review-20260512130623.md`
- `reports/tracker-045-progress-sync-20260512131047.md`

### `TRACKER-046`

トラッカー受信処理を起動時に登録する。

先に失敗するテストを作成し、`TrackerConnectionLibReceiverHostedService` と DI 登録を追加した。役割が不明のパケットも捨てずに復元し、1件の処理で発生した例外が受信処理全体を停止させないよう `UdpTrackerReceiver` を修正した。通常の起動時に `MultiTrackerManager<TrackerPacketAdapter>`、`TrackerConnectionLibSnapshotRecorder`、`UdpTrackerReceiver` を接続し、CaptureOn 中の実際の UDP 受信から自分自身・外部・不明のパケットが session folder 内の snapshot sidecar に流れることを確認した。初回の対象テスト3件、関連38件、全体の `Tracker.Tests` 183件が成功した。

`gpt-5.5 high` の初回レビューで、公式のマルチキャスト受信が通常の処理経路で動作しない指摘が出た。`TrackerMulticastReceiverReviewFixTddTests` を追加し、マルチキャストグループへの参加、起動時の受信先の受け渡し、明示的に有効化したときだけ受信することと既定の無効状態、CaptureOff 中に書き込まないことを固定した。

`UdpTrackerReceiver` の指定したマルチキャストグループへの参加、`Program.cs` の `Tracker:Receive:Enabled` による起動判定と受信先の受け渡し、既定で無効の設定を実装した。修正後の対象テスト4件、関連42件、全体の `Tracker.Tests` 187件が成功し、r2 レビューは完了を妨げる指摘なし。

ソケットの抽象化と DI による起動テストは、完了を妨げない後続の改善とした。診断・再生への統合は次作業であり、PR #9 は下書きのままとした。レビュー可能な状態に切り替えるための準備は、この作業の対象外である。

詳細レポート:

- `reports/tracker-046-runtime-registration-implementation-20260512132555.md`
- `reports/tracker-046-multicast-review-fix-tdd-20260512134307.md`
- `reports/tracker-046-multicast-review-fix-implementation-20260512135310.md`
- `reports/tracker-046-review-r2-20260512140145.md`

### `TRACKER-047`

tracker snapshotの再生読み取りを実装し、レビュー指摘を解消する。

先に失敗するテストを作成して `TrackerSnapshotReplayReader` を実装した。既存の診断ログ読み取りとの互換性を保ち、capture metadata の相対パスから session folder 内の snapshot sidecar を取得できることを確認した。自分自身・外部・不明の tracker snapshot を時刻順の再生入力にし、表示用の tracker snapshot と比較元のパケットのバイト列 / `SemanticSummary` を区別する。`ibis` の詳細ログと tracker snapshot を重複して保持しても、時刻が最も近い記録との比較結果の概要を取得できることを確認した。

初回の `TrackerReplayIntegrationTddTests` は4件、関連39件、全体の `Tracker.Tests` は191件成功したが、`gpt-5.5 high` のレビューには完了を妨げる指摘が2件あった。設計監査では固定作業一覧の作り直しは不要と判断した。

修正では `ibis` の `TrackerFrame.data_timestamp_ns` と tracker snapshot の `TrackedFrame.timestamp` を同じ時間軸で照合するようにした。また、公開する再生用 DTO の位置引数に対応するプロパティへ XML documentation comment を追加した。修正後は対象テスト5件、関連40件、全体192件が成功し、r2 レビューは完了を妨げる指摘なし。

診断・再生 UI の仕上げ、ソケット抽象化と DI 起動テストの強化、PR をレビュー可能な状態にする準備は対象外とした。

詳細レポート:

- `reports/tracker-047-review-20260512150929.md`
- `reports/tracker-047-design-audit-after-review-20260512151541.md`
- `reports/tracker-047-review-fix-implementation-20260512152742.md`
- `reports/tracker-047-review-r2-20260512153751.md`

### `TRACKER-048`

CLIの再生結果へトラッカー比較の出力を追加する。

`Tracker.CaptureReplay` が capture metadata の相対パスから snapshot sidecar を読み、`trackerSnapshot` / `trackerComparison` として入力元の役割と表示名、tracked frame timestamp、ボール数とロボット数、パケットのバイト列の復元結果、時刻が最も近い記録との比較結果の概要を出力できるようにした。snapshot sidecar を持たない旧形式の capture metadata では従来の再生結果の概要を維持する再発防止テストを追加した。

対象の `CaptureReplayTests` は8件、関連47件、全体の `Tracker.Tests` は194件成功し、`git diff --check` は問題なし。`gpt-5.5 high` のレビューと完了可否の監査は、完了を妨げる指摘なし。

`Tracker.CaptureReplay` が旧 `Tracker.Server` を参照する依存関係と、`--settings` の指定先を capture metadata の候補にも使う CLI の操作性は保留事項とした。直ちに新しい作業を追加するのではなく、`TRACKER-049` の運用説明と必要時の設計見直しで扱う。既存のキャプチャー、診断、render snapshot の表示を維持する。

詳細レポート:

- `reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md`
- `reports/tracker-048-review-20260512160935.md`
- `reports/tracker-048-completion-readiness-20260512163550.md`

### `TRACKER-049`

診断画面での比較を設計と固定作業へ反映する。

`Tracker.CaptureReplay` の CLI 比較をエージェントによる調査・検証用として維持し、`/diagnostics` の画面での比較を、PR をレビュー可能な状態にする前に必要な固定作業へ追加した。

当時の `tracker-server-cli-ui-detail-design.md` と `tracker-architecture-plan.md` に、CLI と画面での比較の役割、入力元の絞り込み、snapshot sidecar がない・空・エラーの場合の扱いを明記した。`TRACKER-050` から `TRACKER-053` までの依存関係と完了条件を固定し、設計と進捗管理を同期した。`gpt-5.5 high` のレビューは完了を妨げる指摘なし。

詳細レポート:

- `reports/tracker-049-diagnostics-comparison-task-breakdown-20260512200244.md`
- `reports/tracker-049-design-tracking-sync-20260512201328.md`
- `reports/tracker-049-design-review-20260512201915.md`

### `TRACKER-050`

診断比較の読み取り処理と表示状態の契約を追加する。

`TrackerDiagnosticsComparisonViewStateTests` と `TrackerDiagnosticsComparisonViewStateReader` を追加した。診断ログのファイルパスから capture metadata / snapshot sidecar を取得し、選択中の記録、入力元一覧、選択した入力元による絞り込み、選択中の記録の比較、snapshot sidecar の状態、省略数・エラー数を、画面処理から独立したデータとして扱う契約を固定した。

絞り込みは、すべて・外部・自分自身・不明・入力元の表示名を扱う。snapshot sidecar の欠損、空、破損、未作成、capture metadata に参照がない場合も、既存の診断表示を妨げない。

初回の `gpt-5.5 high` レビューで指摘された、10,000件超のログを省略した後の選択ずれに対する再発防止テストを追加した。`selectedEntryIndex` ではなく、画面に表示された記録から作った `TrackerDiagnosticsComparisonSelectedEntry` を渡す契約へ修正した。対象テスト8件、関連38件、全体の `Tracker.Tests` 202件が成功し、`git diff --check` は問題なし。r2 レビューは完了を妨げる指摘なし。

詳細レポート:

- `reports/tracker-050-review-r2-20260512210935.md`
- `reports/tracker-050-diagnostics-comparison-contract-implementation-20260512202753.md`
- `reports/tracker-050-review-20260512204924.md`
- `reports/tracker-050-review-fix-implementation-20260512205728.md`
- `reports/tracker-050-progress-sync-20260512211517.md`

### `TRACKER-051`

診断画面へ比較表示と入力元の絞り込みを接続する。

`Diagnostics.razor` / `Diagnostics.razor.cs` で、選択中のログ・記録・再生時点と比較用の表示状態を同期した。入力元の役割と表示名、tracked frame number と tracked frame timestamp、比較する tracker snapshot の tracked frame number、時刻差、対応付け規則、ボール数・ロボット数、パケットのバイト列の復元結果、snapshot sidecar の欠損・空・エラーを表示できるようにした。

初回の `gpt-5.5 high` レビューの指摘に対応し、時刻が最も近い tracker snapshot の tracked frame number を比較データと画面表示へ追加した。比較の対象テスト10件、関連33件、`CaptureReplayTests` 8件が成功し、`git diff --check` は問題なし。r2 レビューは完了を妨げる指摘なし。

未加工入力と追跡結果の render snapshot、設定プロファイルのダイアログ、timeline scrubber、`Play` / `Fast Forward` / `Stop`、4K表示向けに大きさを調整できるレイアウトを維持するテストも成功した。ブラウザでの手動検証の証跡と README の更新は `TRACKER-052` で扱う。

詳細レポート:

- `reports/tracker-051-review-r2-20260512215156.md`
- `reports/tracker-051-diagnostics-ui-comparison-implementation-20260512212409.md`
- `reports/tracker-051-review-20260512213715.md`
- `reports/tracker-051-review-fix-implementation-20260512214442.md`
- `reports/tracker-051-progress-sync-20260512215710.md`

### `TRACKER-052`

比較ログの運用説明と手動検証の記録項目を更新する。

旧 `Tracker.Server/README.md` に、CaptureReplay CLI はエージェントによる調査、自動検証、再発防止の確認用として残し、通常の確認では `/diagnostics` の入力元の絞り込みと `Tracker Comparison` で差を見ることを説明した。

snapshot sidecar がない場合、記録が0件の場合、省略・エラーがある場合の読み方を記載した。手動検証では、選択した追跡フレームと時刻、入力元の絞り込み、snapshot sidecar の状態、記録数・省略数・エラー数、選択中の記録の状態、入力元の役割と表示名、tracker snapshot の tracked frame number、自分自身と最も近い比較対象の時刻・その差、ボール数・ロボット数、パケットのバイト列の表示を証跡として残す。

文書のみの変更のため `dotnet test` は未実施。`git diff --check` は問題なし。`gpt-5.5 high` のレビューは完了を妨げる指摘なし。画面比較の実装後の状態に説明を合わせた。

詳細レポート:

- `reports/tracker-052-review-20260512221019.md`
- `reports/tracker-052-docs-manual-evidence-implementation-20260512220318.md`
- `reports/tracker-052-progress-sync-20260512221551.md`

### `TRACKER-054`

トラッカー受信先を個別に上書きできるようにする。

`Tracker:Receive:MulticastAddress` / `Port` を省略可能な上書き設定として追加した。未指定時は起動時に確定した `ibis` の送信先と同じ通信アドレスと通信ポートで受信し、指定時は受信用の個別の接続先を `UdpTrackerReceiver` へ渡す。`InterfaceAddress` は、この端末でマルチキャストに使う NIC を通信アドレスで指定する設定として維持する。

README と設計書へ設定の優先順位、起動時の受信先は実行中に設定プロファイルを切り替えても変わらないこと、外部トラッカーが記録されない場合の確認点を追記した。対象の `TrackerConfigurationBindingTests` は6件、関連テストは10件と5件が成功し、`git diff --check` は問題なし。`gpt-5.5 high` のレビューは完了を妨げる指摘なし。

詳細レポート:

- `reports/tracker-054-receive-endpoint-implementation-20260512231920.md`
- `reports/tracker-054-review-20260512233050.md`

### `TRACKER-055`

診断再生とtimeline scrubberの操作が遅い問題を解消する。

先に失敗するテストを作成し、`TrackerDiagnosticsComparisonViewStateReader` に、診断ログ、capture metadata、snapshot sidecar のファイルパス、更新時刻、長さをキーとする軽量な索引キャッシュを追加した。選択中の記録が変わるたびに100MB超の tracker snapshot を読み直すのではなく、キャッシュから入力元の選択肢と最も近い時刻の比較結果を作る。100MBは上限ではなく通常到達しうる大きさであり、再生時点の更新や timeline scrubber の操作時間がファイルの大きさに比例しないことを重視した。

通常の `Play` は記録された時刻差を維持し、`Fast Forward` は `4x` / `16x` / `64x` を選べるようにした。`Stop`、末尾に到達した後の先頭戻り、古い再生更新を無視する条件を維持し、速度変更前の更新も古いものとして扱う。

初回レビューで指摘された同時刻の優先順位の不具合は、再発防止テストと `FindTimestampStartIndex` で修正した。対象範囲のテストは32件成功し、`git diff --check` は問題なし。`gpt-5.5 high` の r2 レビューは完了を妨げる指摘なし。

詳細レポート:

- `reports/tracker-055-playback-scrub-performance-implementation-20260513001906.md`
- `reports/tracker-055-review-20260513003935.md`
- `reports/tracker-055-review-r2-20260513005448.md`
- `reports/tracker-055-progress-sync-20260513005919.md`

### `TRACKER-056`

Field sourceの左右切り替えと比較表示の折り畳みを追加する。

先に失敗するテストを作成し、Field source の選択肢、時刻が最も近い tracker snapshot の描画データ、キャッシュの再利用、画面状態の契約を固定した。`Tracker Comparison` を折り畳めるようにし、左右に `Vision Input`、`ibis` トラッカー、外部、不明、入力元の表示名を選べるようにした。既定は左が `Vision Input`、右が `ibis` の追跡結果である。Field source には曖昧な `All` を含めない。

外部トラッカーは、選択中の診断記録に時刻が最も近い tracker snapshot を `TRACKER-055` の索引キャッシュから取得し、`VisionFieldCanvas` に描画する。対象の `TrackerDiagnosticsComparisonViewStateTests` と `DiagnosticsPlaybackStateTests` は36件成功し、`git diff --check` は問題なし。`gpt-5.5 high` のレビューは完了を妨げる指摘なし。

`DiagnosticsFieldViewFactory` の変換処理を直接確認するテストが不足していることは、保留事項として保持する。事前調査、設計具体化、実装、レビュー、進捗同期の参照を下記に残す。

詳細レポート:

- `reports/tracker-055-diagnostics-field-source-investigation-20260512233148.md`
- `reports/tracker-056-field-source-toggle-design-20260513010250.md`
- `reports/tracker-056-field-source-toggle-implementation-20260513011324.md`
- `reports/tracker-056-review-20260513013805.md`
- `reports/tracker-056-progress-sync-20260513014628.md`

### `TRACKER-057`

2つのField sourceを重ねて表示する。

希望機能として、先に失敗するテストを作成し、`TRACKER-056` の左右の選択と `TrackerDiagnosticsFieldSourceFrame` を再利用する最小構成を実装した。表示モードは `Split` / `Overlay` とし、左右の Field source を `Layer A` / `Layer B` として同じフィールドに重ねる。表示の有無、凡例、field geometry がない場合の空表示、片方が欠損していても表示可能なもう片方を残す処理を追加し、既存の `TrackerDiagnosticsFieldSourceFrame` とキャッシュを再利用した。

初回レビューでは同じ入力元を二重描画する点が保留事項になったため、追加修正で同じ入力元は1層として扱うようにした。対象の `TrackerDiagnosticsComparisonViewStateTests`、`DiagnosticsFieldViewFactoryTests`、`DiagnosticsPlaybackStateTests` は45件成功し、`git diff --check` は問題なし。`gpt-5.5 high` の r2 レビューは完了を妨げる指摘なし。

詳細レポート:

- `reports/tracker-057-field-overlay-design-20260513014926.md`
- `reports/tracker-057-field-overlay-implementation-20260513015935.md`
- `reports/tracker-057-review-20260513022102.md`
- `reports/tracker-057-review-r2-20260513023505.md`
- `reports/tracker-057-progress-sync-20260513023929.md`

### `TRACKER-053`

PR #9をレビュー可能な状態にするための判断材料をそろえる。

`TRACKER-057` を完了するか、重ね合わせ表示を明示的に延期した後、`TRACKER-040` から最終状態までを PR 本文に反映する作業として定義した。PR 本文案、最終検証、手動検証の証跡不足の扱い、全作業のレビュー記録、保留事項と残るリスク、進捗同期、下書きを解除するための判断材料を整理した。

最終検証は、旧 `Tracker.Server` のビルド成功、全体の `Tracker.Tests` 227件成功、`git diff --check` 成功。初回レビューで指摘された古い PR 本文案は r2 までに修正した。`gpt-5.5 high` の r2 レビューは完了を妨げる指摘なし。

ブラウザでの手動検証の証跡は未取得であり、利用者指示に照らして残るリスクとして扱い、それだけで完了を妨げる条件とはしなかった。この判断は当時の PR 準備に関するもので、今回の用語確認の完了条件を緩めるものではない。

詳細レポート:

- `reports/tracker-053-pr-ready-evidence-20260513024248.md`
- `reports/tracker-053-final-validation-fix-20260513025052.md`
- `reports/tracker-053-review-20260513025530.md`
- `reports/tracker-053-review-r2-20260513030250.md`
- `reports/tracker-053-progress-sync-20260513030702.md`

### `TRACKER-058`

診断再生でER-Forceのtracker snapshotを描画できない原因を修正する。

利用者が指定した再生不具合として追加した。直近のキャプチャーに `ER-FORCE` の tracker snapshot が含まれることを確認し、`ibis` と `ER-FORCE` の `TrackedFrame.timestamp` が同じ時刻系ではないことを、フィールド表示不具合の主な原因候補として記録した。既存ログの救済は不要という利用者方針に従い、新しいキャプチャーの保存時に時刻を対応付ける alignment sidecar を追加した。

`tracker-snapshot-alignment.jsonl` に診断記録、描画用の render snapshot、入力元ごとの tracker snapshot の保存時の対応を記録し、capture metadata の `TrackerSnapshotAlignmentPath` / `TrackerSnapshotAlignmentLog` からたどれるようにした。`/diagnostics` の Field source と `Tracker.CaptureReplay` は保存済みの対応がある場合に `saved-session-alignment` を優先し、外部と自分自身のトラッカーの tracked frame timestamp が重ならなくても比較対象を選べる。再発防止テストでは、選択中の診断記録と選ばれた外部 tracker snapshot の `receivedAt` の差が許容範囲内であることを固定した。

対象の検証は45件成功し、`git diff --check` は成功。`gpt-5.5 high` のレビューは完了を妨げる指摘なし。全体の `Tracker.Tests` は229件成功・1件失敗だった。失敗は、その作業のコミットに含まれないローカルファイル `Tracker/Tracker.Server/appsettings.json` で `Tracker:Receive:Enabled=true` としていたことにより、既定で受信が無効という契約に違反したものとして分けて記録する。全体成功へ読み替えない。

詳細レポート:

- `reports/tracker-058-er-force-replay-investigation-20260513062747.md`
- `reports/tracker-058-saved-alignment-design-20260513063637.md`
- `reports/tracker-058-saved-alignment-implementation-20260513064540.md`
- `reports/tracker-058-review-20260513070147.md`
- `reports/tracker-058-progress-sync-20260513070654.md`

### `TRACKER-059`

replay timelineを最も更新頻度が高いトラッカーに合わせる。

利用者の追加要望として、診断記録の件数・選択中の記録を中心にした従来の `Play` / `Fast Forward` / timeline scrubber では、高頻度トラッカーの観測が再生時点にならないことを調査した。受信時刻 `ReceivedAt` を軸とする統一された replay timeline、raw vision と render snapshot は対象時刻以前の最新状態を保持すること、既存の `tracker-snapshot-alignment.jsonl` を `SchemaVersion` が2の記録へ置き換えること、最速の入力元の周期で保存時の対応付けを記録すること、TDD の受け入れ条件を設計した。

`SchemaVersion = 1` との互換性は要件とせず、`SchemaVersion = 2` の索引による性能を優先した。ログを開く時点で索引を作成し、再生位置を更新するときは索引から高速に取得する。raw vision よりトラッカーの更新が速い場合、raw vision が低い更新頻度で見えることは期待動作である。

`TrackerDiagnosticsReplayTimelineIndex` を追加し、最も更新頻度が高い入力元の周期で保存時の対応付けを出力した。`/diagnostics` の `Play` / `Fast Forward` / timeline scrubber / Field source / 比較表示を、選択中の replay timeline の時点を基準とする処理へ接続した。`Fast Forward` は途中の時点を間引かず、時刻の差を倍率で割って高速化する。

レビュー指摘の修正後は対象の検証62件、関連32件が成功し、`git diff --check` は成功。`gpt-5.5 high` の r2 レビューは完了を妨げる指摘なし。全体の `Tracker.Tests` は238件成功・1件失敗で、失敗は `TRACKER-058` と同じく、コミット外のローカルファイル `Tracker/Tracker.Server/appsettings.json` の `Tracker:Receive:Enabled=true` による既定無効の契約違反として保持する。

詳細レポート:

- `reports/tracker-059-fastest-timeline-investigation-20260513173834.md`
- `reports/tracker-059-fastest-timeline-design-20260513175146.md`
- `reports/tracker-059-fastest-timeline-implementation-20260513181201.md`
- `reports/tracker-059-review-20260513184442.md`
- `reports/tracker-059-review-fix-implementation-20260513185336.md`
- `reports/tracker-059-review-r2-20260513190058.md`

### `TRACKER-060`

等倍速再生を30fps相当の更新で実時間に追従させる。

利用者の追加要望として、等倍速の `Play` は replay timeline の全時点を順番に表示するのではなく、30fps相当の表示更新で実際の経過時間に追従させることとした。再生開始時の wall-clock と選択中の時点の `ReceivedAt` からキャプチャー内の再生対象時刻を計算し、その時刻以下で最も新しい replay timeline の時点を表示する。`Fast Forward` とは処理の役割を分け、200Hzの入力と30fpsの表示更新を TDD の受け入れ条件として固定した。

高頻度のトラッカーの観測は保存時の対応付け・比較用にはすべて保持し、timeline scrubber、Field source、比較表示から任意の時点を比較できる経路を維持する。等倍速表示だけは途中の時点を必要に応じて省略し、実時間での遅れの確認を優先する。

対象の検証19件、関連48件が成功し、`git diff --check` は成功。`gpt-5.5 high` の r2 レビューは完了を妨げる指摘なし。全体の `Tracker.Tests` は237件成功・1件失敗で、失敗は `TRACKER-058` と同じコミット外のローカルファイルでの設定 `Tracker:Receive:Enabled=true` による既定無効の契約違反として保持する。

詳細レポート:

- `reports/tracker-060-realtime-playback-design-20260513194832.md`
- `reports/tracker-060-realtime-playback-implementation-20260513195439.md`
- `reports/tracker-060-review-20260513200044.md`
- `reports/tracker-060-review-fix-implementation-20260513200441.md`
- `reports/tracker-060-review-r2-20260513200634.md`

### `TRACKER-061`

等倍速と調査用の倍率を別の再生操作として表示する。

従来の `Fast forward` ボタンと速度選択では、倍率が等倍速の設定値に見えるため、利用者の追加要望として再生操作を分離した。`DiagnosticsPlaybackState.PlaybackChoices` を追加し、`等倍速` は `DiagnosticsPlaybackMode.Play`、`4x` / `16x` / `64x` は `DiagnosticsPlaybackMode.FastForward` と対応する倍率で開始するようにした。

選択中の操作は `等倍速 停止` / `4x 停止` / `16x 停止` / `64x 停止` と表示した。旧速度選択と数値による等倍表記は使わない。`SchemaVersion` が2の保存済みの対応付け、timeline scrubber、Field source、任意の時点の比較を維持し、`TRACKER-059` の早送りでは時点を間引かない条件と、`TRACKER-060` の30fps相当の実時間追従を保持した。

対象の `DiagnosticsPlaybackStateTests` は23件、関連51件が成功し、`git diff --check` は成功。初回レビューの N1（停止表示に `Stop` が混在する問題）は追加修正で解消し、`gpt-5.5 high` の r2 レビューは指摘なし。全体の `Tracker.Tests` は240件成功・1件失敗だったことも保持する。失敗は `TRACKER-058` と同じコミット外のローカルファイルでの設定 `Tracker:Receive:Enabled=true` による既定無効の契約違反であり、全体成功とは扱わない。

詳細レポート:

- `reports/tracker-061-playback-ui-separation-design-20260513204405.md`
- `reports/tracker-061-playback-ui-separation-implementation-20260513205042.md`
- `reports/tracker-061-review-20260513205647.md`
- `reports/tracker-061-review-fix-implementation-20260513210059.md`
- `reports/tracker-061-review-r2-20260513210407.md`

### `TRACKER-062`

従来の再生ボタン配置に戻し、速度選択に等倍速を追加する。

利用者の確認により `TRACKER-061` の UI を再調整した。`Play` / `Fast Forward` / `Stop` の従来のボタン配置を維持し、速度選択側に小さなタブとして `等倍速`、`4x`、`16x`、`64x` を並べた。`等倍速` は30fps相当で実時間に追従する `Play`、各倍率は対応する倍率の `FastForward` で開始する。

`FastForward` 中に `等倍速` を選ぶと `Play` へ、`Play` 中に倍率を選ぶと `FastForward` へ切り替え、表示と実際の再生モードを一致させる。`TRACKER-059` の早送り時の非間引き、`TRACKER-060` の実時間追従、`SchemaVersion` が2の保存済みの対応付けと任意の時点を比較する経路を維持した。

対象の `DiagnosticsPlaybackStateTests` は27件、関連56件が成功し、`git diff --check` は成功。初回レビューの B1 は追加修正で解消し、`gpt-5.5 high` の r2 レビューは指摘なし。

詳細レポート:

- `reports/tracker-062-playback-speed-choice-design-20260513213014.md`
- `reports/tracker-062-playback-speed-choice-implementation-20260513213716.md`
- `reports/tracker-062-review-20260513214513.md`
- `reports/tracker-062-review-fix-implementation-20260513214808.md`
- `reports/tracker-062-review-r2-20260513215222.md`

### `TRACKER-063`

再生開始時の速度選択を維持し、早送り倍率を可変にする。

再生ボタンを押すと選択速度が `等倍速` に戻る不具合の修正と、固定の `64x` では不足する早送り倍率の可変化を、同じ再生速度制御の作業として追加した。`Play` / `Fast Forward` / `Stop` の従来配置を維持し、速度選択を `等倍速` と可変の `早送り倍率` に変更した。再生ボタンは選択速度を尊重し、倍率を選択している場合は `FastForward` として開始する。

可変の早送り倍率は `2x..1024x` の範囲に制限する。固定の `4x` / `16x` / `64x` に依存せず、64倍を超える倍率も UI と状態の契約で扱う。タイマー間隔や状態の正規化で64倍超を無効にせず、実効速度を上げられるようにした。`FastForward` の更新間隔には30msという固定の下限ではなく、より小さいタイマー間隔の下限を使う。

`等倍速` の開始では `TRACKER-060` の実時間追従を維持し、`TRACKER-059` の早送り時の非間引き、`SchemaVersion` が2の保存済みの対応付け、timeline scrubber、Field source、任意の時点の比較を壊さない。対象の `DiagnosticsPlaybackStateTests` は44件、関連73件が成功し、`git diff --check` は成功。`gpt-5.5 high` のレビューは完了を妨げる指摘なし。実画面での小さな UI の見え方は未確認であり、タイマーの分解能とともに保留事項として保持する。

詳細レポート:

- `reports/tracker-063-variable-playback-speed-design-20260513222344.md`
- `reports/tracker-063-variable-playback-speed-implementation-20260513223102.md`
- `reports/tracker-063-review-20260513224028.md`
