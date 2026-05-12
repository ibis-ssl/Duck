# タスク状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在のタスク

- ID: TRACKER-040
- Title: CaptureOn 比較ログ拡張の設計と tracking を追加する
- Phase: comparison-logging
- Status: in_progress
- Size: small
- Dependencies: TRACKER-039
- Exit Criteria:
  - `TRACKER-039` が PR #8 merge 済みであることを tracking に同期している
  - `comparison-logging` phase と `TRACKER-041` 以降の後続小タスクが `tasks-status.md` / `phases-status.md` に登録されている
  - `TrackerConnectionLib` を 3rdparty tracker 傍受の第一候補統合点とする設計方針が文書化されている
  - `Tracker.Server` を CaptureOn session と比較ログの統合層とし、`Tracker.Core` へ 3rdparty tracker 傍受・比較保存処理を入れない責務境界が文書化されている
  - 比較ログを既存 diagnostics log の破壊的拡張ではなく sidecar JSONL 主記録にする方針と、diagnostics 側の参照/集計互換追加方針が文書化されている
  - self除外、`uuid` / `sourceName` / remote endpoint、timestamp近傍比較、Capture Off / 再On、他 tracker 不在時の扱いが設計に含まれている
  - 実装前 draft PR に載せる差分として、実装コードやテストコードを変更していない
  - 作業レポート `reports/topic-tracker-captureon-compare-design-sync-20260512092613.md` に実行コマンド、変更ファイル、結果、リスクが記録されている

## 次の調査タスク

- none

## タスク一覧

| ID | タスク | フェーズ | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| TRACKER-000 | Tracker の設計書と進捗管理ファイルを作成する | preparation | done | Tracker の事前調査が完了していること | 設計書、task/phase 管理、調査レポート、レビュー報告が揃い、ユーザー承認の上で設計を完了できる。 |
| TRACKER-001 | `Tracker.Tests` から `Tracker.Core` を参照可能にし契約テスト基盤を作る | contracts | done | TRACKER-000 approved | `Tracker.Tests` から `Tracker.Core` を参照でき、contract test 用の fixture と test data 基盤が存在する。 |
| TRACKER-002 | packet generator の契約テストを追加する | contracts | done | TRACKER-001 | 単位変換、primary/secondary ball 並び、capabilities、`kicked_ball` 寿命、timestamp 出力を定義する failing test が存在する。 |
| TRACKER-003 | engine の時系列契約テストを追加する | contracts | done | TRACKER-001 | reorder、`MergeWindow`、`0..N CommittedFrames`、late packet、geometry reset、profile switch、event publish 順を定義する failing test が存在する。 |
| TRACKER-004 | `TrackerFrame` / state 型 / `TrackerUpdateResult` / observer-event 契約を実装する | contracts | done | TRACKER-002, TRACKER-003 | 内部フレーム、state 型、`TrackerUpdateResult`、domain event、observer 契約が存在し、契約テストが参照できる。 |
| TRACKER-005 | `TrackerPacketGenerator` を実装する | contracts | done | TRACKER-004 | official tracker proto 出力、primary/secondary ball 並び、timestamp、`kicked_ball`、capabilities がテストを通過する。 |
| TRACKER-006 | `TrackerEngine` の reorder buffer と flush pipeline を実装する | engine | done | TRACKER-003, TRACKER-004 | event-time buffer、flush 判定、`0..N CommittedFrames`、`WorldFrameCommitted` までの基本 pipeline が決定的に動作する。 |
| TRACKER-007 | `TrackerEngine` の profile switch / geometry reset / event publish 順を実装する | engine | done | TRACKER-006 | profile switch 要求、pending buffer clear、geometry reset、observer/event publish 順が契約どおりに動作する。 |
| TRACKER-008 | robot tracking と robot merge を実装する | engine | done | TRACKER-006 | camera-local robot track、位置/角度の別 filter、robot merge、visibility/quality が raw vision 入力から生成される。 |
| TRACKER-009 | ball tracking と primary/secondary ball 選定を実装する | engine | done | TRACKER-006 | camera-local ball track、uncertainty-weighted merge、primary ball 選定、secondary ball stable sort が raw vision 入力から生成される。 |
| TRACKER-010 | kick と contact metadata を実装する | engine | done | TRACKER-007, TRACKER-008, TRACKER-009 | `KickEventState`、`BallContactState`、`KickDetected`、`ContactChanged` が生成され、関連契約テストが通る。 |
| TRACKER-011 | ball left field metadata を実装する | engine | done | TRACKER-007, TRACKER-009 | `BallLeftFieldState` と `BallLeftField` event が生成され、関連契約テストが通る。 |
| TRACKER-012 | `Tracker.Server` へ engine と packet 配信を統合する | integration | done | TRACKER-005, TRACKER-007, TRACKER-010, TRACKER-011 | raw vision 入力が engine へ流れ、`TrackerUpdateResult` が snapshot store・observer・official packet 配信へ反映される。 |
| TRACKER-013 | tracker/network 設定束縛を統合する | integration | done | TRACKER-012 | tracker/network 設定が外部設定から束縛され、起動時設定が engine と publisher に反映される。 |
| TRACKER-014 | profile 切替要求経路を統合する | integration | done | TRACKER-012, TRACKER-013 | profile 切替要求が server から engine へ流れ、切替結果が observer/UI 側へ反映される。 |
| TRACKER-015 | tracked viewer と raw/tracked toggle を追加する | ui | done | TRACKER-012 | UI で raw/tracked を切り替えられ、tracked field と主要 object を描画できる。 |
| TRACKER-016 | tracked diagnostics 表示を追加する | ui | done | TRACKER-015 | tracked diagnostics、profile 名、kick/contact/field 状態を表示できる。 |
| TRACKER-017 | runtime profile 表示・操作 UI を追加する | ui | done | TRACKER-014, TRACKER-016 | profile 名表示と profile 切替要求 UI が表示・操作できる。 |
| TRACKER-018 | Tracker v1 の build/test 証跡を取得する | verification | done | TRACKER-017 | build/test の証跡が記録され、主要 unit/contract 観点の結果が reports に存在する。 |
| TRACKER-019 | Tracker v1 の integration 観点検証を行う | verification | done | TRACKER-018 | late packet、geometry reset、profile switch、observer/event、viewer 切替の確認結果が reports に存在する。 |
| TRACKER-020 | Tracker v1 の最終レビューと追跡ファイル同期を行う | review | done | TRACKER-019 | sub-agent レビュー結果が記録され、致命的な指摘が残っておらず、tracking files が最終状態と一致する。 |
| TRACKER-021 | `Tracker.Server` の使い方 README を追加する | documentation | done | TRACKER-020 | `Tracker/Tracker.Server/README.md` が存在し、起動手順、画面の使い方、主要設定値の意味が記載されている。 |
| TRACKER-022 | `VisionReceiver` を profile-aware にする | integration | done | TRACKER-021 | `VisionReceiver` 設定が複数 profile を持てて、起動中 profile と runtime switch に追従でき、関連検証結果が存在する。 |
| TRACKER-023 | camera-local tracking を線形 Kalman filter 標準へ是正する | engine | done | TRACKER-013, TRACKER-022 | ball / robot の camera-local track 更新が線形 Kalman filter ベースになり、`ProcessNoise` / `MeasurementNoise` / `Gate` / `VisibilityHalfLifeSeconds` が runtime 挙動へ反映され、既存 contract に矛盾しない。 |
| TRACKER-024 | Kalman 標準準拠の検証と release 判定をやり直す | verification | done | TRACKER-023 | Kalman 化後の focused/full test と review report が存在し、設計書の「v1 は直線運動前提の Kalman filter を標準とする」に対して未解決 blocker が残っていない。 |
| TRACKER-025 | Tracked 表示へ低 visibility の stale object を出さない | engine | done | TRACKER-024 | 欠測で十分 decayed した robot / ball track が `TrackerFrame` に出力されず、1 frame 程度の短期欠測を残す既存契約は維持される。設定差分は `reports/tracker-025-tigers-config-diff-20260510153510.md`、review は `reports/tracker-025-review-20260510154020.md` に記録済み。 |
| TRACKER-026 | Tracked 表示の raw/tracked diagnostics log を追加する | investigation | done | TRACKER-025 | raw SSL-Vision detection と tracked 出力を同じログで比較でき、誤検出の発生源を切り分けられる。`dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore` は 0 warning / 0 error。 |
| TRACKER-027 | Tigers 由来の近接重複 robot / 短命 ball 抑制を追加する | engine | done | TRACKER-026 | 近接別 ID robot を raw detection 単位で抑制し、短命 secondary ball ghost を 1 frame で出力しない。継続観測された genuine な複数 ball は stable sort で出力できる。実装・検証は `reports/tracker-027-evidence-20260510161437.md`、review は `reports/tracker-027-review-20260510161549.md` に記録済み。 |
| TRACKER-028 | capture 1680 付近の複数 ball 再発を解析して修正する | engine | done | TRACKER-027 | 指定 diagnostics log の trackedFrame 1680 付近で複数 ball になる原因を記録し、成長済み secondary ball が fresh observation を失った後に出続けないよう修正した。実装・検証は `reports/tracker-028-evidence-20260510215726.md`、review は `reports/tracker-028-review-20260510215726.md` に記録済み。 |
| TRACKER-029 | tracked object の小刻みな振動を抑制する | engine | done | TRACKER-028 | stationary に近い tracked ball / robot の表示揺れを抑制しつつ、実移動している object の追従性を過度に落とさない。振動抑制 tuning 値は profile 設定から外部調整できる。実装・検証は `reports/tracker-029-evidence-20260510221200.md`、review は `reports/tracker-029-review-20260510221200.md` に記録済み。 |
| TRACKER-030 | Tracked field 表示を Vision field geometry と揃える | ui | done | TRACKER-029 | tracked view でも defense area / goal / center / field arcs など Vision field と同等の線を描画し、raw Vision 画面との差分を `reports/tracker-030-evidence-20260510222529.md` に記録済み。review は `reports/tracker-030-review-20260510222529.md` に記録済み。 |
| TRACKER-031 | camera 間の同一 robot ID 遠方 outlier で robot が瞬間移動する問題を修正する | engine | done | TRACKER-030 | 同じ frame の別 camera に正常な同一 robot ID 観測がある場合、遠方 outlier camera 観測を tracked merge に混ぜない。原因・実装・検証は `reports/tracker-031-evidence-20260510223916.md`、review は `reports/tracker-031-review-20260510223916.md` に記録済み。 |
| TRACKER-032 | Tracker 保守性改善の詳細設計書を分割作成する | maintenance | done | TRACKER-031 | Core engine、Server/CLI/UI、test 保守性改善の詳細設計を日本語の分割ファイルとして作成した。worker report は `reports/tracker-032-core-design-worker-20260511063428.md`、`reports/tracker-032-server-design-worker-20260511063428.md`、`reports/tracker-032-test-design-worker-20260511063428.md`、review は `reports/tracker-032-review-20260511063428.md` に記録済み。 |
| TRACKER-033 | Core tracker engine の巨大ファイルを責務別に細分化し日本語コメントを追加する | maintenance | done | TRACKER-032 | `TrackerExecutionContracts.cs`、`TrackerModelContracts.cs`、`TrackerPacketGenerator.cs` を Core の責務別ファイルへ分割し、主要 class / property / method に日本語コメントを追加した。実装・検証は `reports/tracker-033-core-worker-20260511070200.md`、review は `reports/tracker-033-review-20260511072000.md` に記録済み。 |
| TRACKER-034 | Server / CLI / UI の巨大ファイルを責務別に細分化し日本語コメントを追加する | maintenance | done | TRACKER-032 | `Tracker.CaptureReplay/Program.cs`、`TrackerCoordinator.cs`、`Diagnostics.razor` などを責務別に分割し、主要 class / property / method に日本語コメントを追加した。実装・検証は `reports/tracker-034-server-worker-20260511074000.md`、追加コメント補強は `reports/tracker-034-comment-followup-worker-20260511082000.md`、review は `reports/tracker-034-review-20260511081000.md` と `reports/tracker-034-review-r2-20260511083000.md` に記録済み。 |
| TRACKER-035 | Tracker tests を読みやすく分割し確認内容の日本語コメントを追加する | maintenance | done | TRACKER-033, TRACKER-034 | 巨大 test file を責務別に分割し、対象 test 81 件に何を確認しているかの日本語コメントを追加した。実装・検証は `reports/tracker-035-test-worker-20260511085000.md`、review は `reports/tracker-035-review-20260511091000.md` に記録済み。 |
| TRACKER-036 | 保守性改善全体の検証・レビュー・PR 完了通知を行う | verification | done | TRACKER-033, TRACKER-034, TRACKER-035 | 保守性改善全体の最終検証と final review を実施した。最終検証は `reports/tracker-036-final-verification-20260511093000.md`、final review は `reports/tracker-036-final-review-20260511094000.md` に記録済み。 |
| TRACKER-037 | Tracker 保守性改善の命名・配置・コメント基準を決めて一貫性を確認する | maintenance | done | TRACKER-036 | dot 区切りファイル名とフォルダ分割の使い分け、コメント付与対象、test の XML コメント化方針を日本語で明文化した。監査は `reports/tracker-037-naming-comment-audit-20260511195008.md`、実装分担は `reports/tracker-037-design-rules-worker-20260511195640.md`、`reports/tracker-037-core-server-worker-20260511195640.md`、`reports/tracker-037-test-xml-comments-worker-20260511195640.md`、修正は `reports/tracker-037-review-fix-worker-20260511200910.md`、最終再レビューは `reports/tracker-037-review-r2-20260511201410.md` に記録済み。 |
| TRACKER-038 | diagnostics log の trackedFrame 3483 付近で黄色8番が首振りする原因を調査して修正する | investigation | done | TRACKER-037 | 原因は raw Vision 入力、camera 間 merge、表示処理ではなく、robot orientation filter が位置 mm 用 covariance を向き axis に流用して過去の角速度を残すことだった。rad 単位の orientation covariance と angular velocity clamp を `RobotTracker` 設定へ外出しし、既存 Kalman scale 契約も維持した。`Tracker.CaptureReplay` に frame detail filter と robot orientation / angular velocity 出力を追加し、appsettings / resolved metadata replay で Kalman scale を保持するようにした。証跡は `reports/tracker-038-evidence-20260512080732.md` に記録済み。focused test 26 件、full test 155 件は passed。初回 review と r2 review の Medium finding は対応済み。r3 review は `reports/tracker-038-review-r3-20260512082903.md` に記録済みで no findings。 |
| TRACKER-039 | diagnostics log の trackedFrame 3448 付近で青1番が11番へ化ける原因を調査して修正する | investigation | done | TRACKER-038 | 原因は raw Vision で青1番 / 青11番が同一位置近傍に重複し、さらに青1番が別 robot 位置にも現れたとき、merge window 内の後続同一 ID 候補が既存 track 近傍候補を上書きし、突然の ID 入れ替わりを位置ズレより低確率として扱っていなかったことだった。既存同一 ID track 近傍候補の優先と、既存別 ID track 近傍への突然の ID 入れ替わり抑制を `RobotTracker.IdentitySwitchDistanceMm` として外出しして実装した。番号ワープを失敗条件にした再発防止テストは stash で旧実装が失敗し、修正後に成功した。証跡は `reports/tracker-039-evidence-20260512084929.md`、初回 review は `reports/tracker-039-review-20260512085258.md`、r2 review は `reports/tracker-039-review-r2-20260512090207.md` に記録済み。初回 review の Medium 指摘は進捗ファイル同期漏れで対応済み。r2 review は指摘なし。PR #8 `https://github.com/ibis-ssl/Duck/pull/8` は `2026-05-12T00:06:33Z` に merge 済み。 |
| TRACKER-040 | CaptureOn 比較ログ拡張の設計と tracking を追加する | comparison-logging | in_progress | TRACKER-039 | `comparison-logging` phase と後続小タスクを追加し、`TrackerConnectionLib` を 3rdparty tracker 傍受の第一候補統合点、`Tracker.Server` を CaptureOn session への比較ログ統合層、`Tracker.Core` を傍受・比較保存対象外とする責務境界を設計書に明記する。sidecar JSONL 主記録、diagnostics 互換参照/self除外/timestamp近傍比較/Capture Off 再On/他 tracker 不在時の扱いを文書化し、実装前 draft PR 用の差分として実装コード・テストコードは変更しない。 |
| TRACKER-041 | 他 tracker packet 受信・識別の契約テストを追加する | comparison-logging | todo | TRACKER-040 | `TrackerConnectionLib` を第一候補として、ibis と異なる `uuid` / `sourceName` の `TrackerWrapperPacket` を比較候補として扱い、ibis 自身の packet は除外し、複数 source の最新状態を保持する failing test を追加する。実装は test を通す最小限に限定し、review report に blocking finding が残らない。 |
| TRACKER-042 | CaptureOn session に比較 sidecar path と metadata を追加する | comparison-logging | todo | TRACKER-041 | CaptureOn session metadata に比較 sidecar path と比較ログ設定を記録し、Capture Off / 再On で新しい session へ切り替わる契約を test で固定する。既存 packet capture、diagnostics sidecar、render snapshot の basename 関係を壊さない。 |
| TRACKER-043 | CaptureOn 中に他 tracker packet を比較 sidecar JSONL へ保存する | comparison-logging | todo | TRACKER-042 | CaptureOn 中に受信した他 tracker packet を `receivedAt`、remote endpoint、`uuid`、`sourceName`、tracked frame number/timestamp、payload または summary として sidecar JSONL に保存し、self除外、flush、壊れた packet の skipped/error count を満たす。 |
| TRACKER-044 | ibis committed frame と他 tracker 最新 packet を diagnostics / replay で比較可能にする | comparison-logging | todo | TRACKER-043 | 既存 diagnostics log reader の互換性を壊さず、比較 sidecar を reader または `Tracker.CaptureReplay` から読めるようにし、ibis committed frame の timestamp 近傍にある他 tracker frame の source、frame number、ball/robot count を出せる。 |
| TRACKER-045 | 比較ログの UI/README/運用証跡を整える | comparison-logging | todo | TRACKER-044 | `/diagnostics` または README から比較ログの場所と読み方が分かり、既存 capture / diagnostics / render snapshot 表示を壊さない。focused/full test、必要な manual evidence、gpt-5.5 high review report が揃い、blocking finding が残っていない。 |
