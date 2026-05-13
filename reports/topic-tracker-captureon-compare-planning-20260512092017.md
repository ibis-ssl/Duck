# Sub-agent実行レポート

## タスク

Tracker CaptureOn 比較ログ拡張の設計・タスク分解案作成。

## sub-agentを使う理由

親エージェントは実装・調査を行わず、サブエージェントの報告を読んで判断するため。

## 対象範囲

- 既存 Tracker / CaptureOn / ログ関連構造の確認
- 設計更新が必要な文書候補
- task-status / phase-status へ追加すべき計画案
- TDD で先に置くべきテスト案
- 実装を小さく分けるタスク案

## 対象外

- コード変更
- 設計文書変更の確定
- テスト作成
- 実装

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/task-breakdown-planner/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/task-consistency-manager/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
- `nl -ba reports/topic-tracker-captureon-compare-planning-20260512092017.md`
- `rg -n "CaptureOn|Capture|Log|Logger|Diagnostics|Replay|tracker|Tracker|Comparison|Compare|VisionReceiver|Tracked" Tracker TrackerConnectionLib TrackerConnectionLibExample -g '*.cs' -g '*.razor' -g '*.md' -g '*.json'`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,220p' Tracker/Tracker.Core/Design/phases-status.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `rg --files Tracker | rg 'Capture|Diagnostics|TrackerCoordinator|TrackerOptions|PacketCapture|Replay|TrackedVision|Home\.razor|Diagnostics\.razor|README\.md'`
- `rg -n "Capture On|Capture Off|PacketCapture|CaptureReplay|tracker-diagnostics|Diagnostics|TrackerDiagnostics|PublishUdp|TrackerReceiver|TrackerConnection|TrackerWrapperPacket|TrackedFrame|source_name|Uuid|uuid|SourceName|source" Tracker/Tracker.Server Tracker/Tracker.CaptureReplay Tracker/Tracker.Tests Tracker/Tracker.Core -g '*.cs' -g '*.razor' -g '*.md' -g '*.json'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionPacketCaptureRuntimeControl.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs | sed -n '1,240p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs | sed -n '1,340p'`
- `nl -ba Tracker/Tracker.Server/Program.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/UdpTrackerPacketPublisher.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerOptions.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/appsettings.json | sed -n '1,230p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionPacketCaptureWriter.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionPacketCaptureFile.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Tests/TrackerCoordinatorDiagnosticsCaptureTests.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Tests/VisionPacketCaptureTests.cs | sed -n '1,280p'`
- `find TrackerConnectionLib TrackerConnectionLibExample -maxdepth 4 -type f | sort`
- `rg -n "TrackerWrapperPacket|TrackedFrame|ITrackerReceiver|Receive|Udp|Multicast|source|uuid|SSL_" TrackerConnectionLib TrackerConnectionLibExample -g '*.cs' -g '*.md' -g '*.json'`
- `nl -ba TrackerConnectionLib/src/MultiTrackerManager.cs | sed -n '1,180p'`
- `nl -ba TrackerConnectionLib/src/UdpTrackerReceiver.cs | sed -n '1,180p'`
- `nl -ba TrackerConnectionLib/src/TrackerPacketAdapter.cs | sed -n '1,120p'`
- `nl -ba TrackerConnectionLibExample/Program.cs | sed -n '1,120p'`
- `nl -ba Tracker/Tracker.Core/Proto/TrackerPacketGenerator.cs | sed -n '1,240p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs | sed -n '1,240p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs | sed -n '1,240p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs | sed -n '1,240p'`
- `nl -ba Tracker/Tracker.CaptureReplay/ReplayFrameFormatter.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/README.md | sed -n '100,245p'`
- `nl -ba Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md | sed -n '1,80p;180,245p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '35,140p;260,360p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md | sed -n '380,430p'`
- `git status --short`

## 対象ファイル

- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
- `Tracker/Tracker.Server/README.md`
- `Tracker/Tracker.Server/appsettings.json`
- `Tracker/Tracker.Server/Program.cs`
- `Tracker/Tracker.Server/Components/Pages/Home.razor`
- `Tracker/Tracker.Server/Vision/VisionPacketCaptureRuntimeControl.cs`
- `Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`
- `Tracker/Tracker.Server/Vision/VisionPacketCaptureWriter.cs`
- `Tracker/Tracker.Server/Vision/VisionPacketCaptureFile.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs`
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsFormatter.cs`
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs`
- `Tracker/Tracker.Server/Tracking/TrackerOptions.cs`
- `Tracker/Tracker.Server/Tracking/TrackerPublisherOptions.cs`
- `Tracker/Tracker.Server/Tracking/UdpTrackerPacketPublisher.cs`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs`
- `Tracker/Tracker.Core/Proto/TrackerPacketGenerator.cs`
- `Tracker/Tracker.CaptureReplay/ReplayFrameFormatter.cs`
- `Tracker/Tracker.Tests/TrackerCoordinatorDiagnosticsCaptureTests.cs`
- `Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs`
- `Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`
- `Tracker/Tracker.Tests/TrackerConfigurationBindingTests.cs`
- `Tracker/Tracker.Tests/TrackerCoordinatorTestFactory.cs`
- `TrackerConnectionLib/src/MultiTrackerManager.cs`
- `TrackerConnectionLib/src/UdpTrackerReceiver.cs`
- `TrackerConnectionLib/src/TrackerPacketAdapter.cs`
- `TrackerConnectionLibExample/Program.cs`

## 指摘事項

- 設計更新は必須。要求は Capture On 時の保存物と diagnostics log schema を拡張し、他 tracker の official `TrackerWrapperPacket` を後で比較可能にする外部観察性の追加である。`Tracker.Core/Design/tracker-architecture-plan.md` は official 出力と観察性方針、`Tracker.Core/Design/tracker-server-cli-ui-detail-design.md` は diagnostics / capture / CLI schema、`Tracker.Server/README.md` は運用手順、必要なら `Tracker.Server/Design/raw-vision-viewer-plan.md` は viewer/capture 側の実行体設計を追記対象にするのが妥当。
- tracking file は `Tracker/Tracker.Core/Design/tasks-status.md` と `phases-status.md` を先に更新する必要がある。現状は `TRACKER-039` が in_progress だが実装・検証・review 完了、commit / PR 待ちであり、残りフェーズは none。親はまず `TRACKER-039` のcommit / PR gateを閉じるか、新規 draft PR 用 branch に未commit差分を持ち越さない判断が必要。
- Capture On の入口は `Home.razor` の `ToggleCaptureAsync` で、`VisionPacketCaptureRuntimeControl` を有効化するだけである。実際の session 開始は `VisionPacketCaptureWriter.Capture`、`TrackerCoordinator/Diagnostics.cs`、`TrackerRenderSnapshotCaptureWriter.CaptureFrame` が `VisionPacketCaptureSession.EnsureStarted` を呼んだ時点で遅延開始される。
- 現在の capture session は同じ basename で `.jsonl.gz`、`.metadata.json`、`.tracker-diagnostics.log`、`.render-snapshots.jsonl.gz` を作る。metadata には自tracker設定と resolved settings は入るが、同時に存在する他 tracker の情報は入らない。
- 現在の diagnostics log は ibis tracker の raw detection と tracked output の1行比較に特化している。`TrackerDiagnosticsLogReader.TryParseLine` は key=value の可変 field を拾えるが、record model は `otherTracker...` 系の保持先を持たない。
- `Tracker.Server` は自tracker packetを `UdpTrackerPacketPublisher` で publish するが、他 tracker packet を受信する service は DI に存在しない。既存の `TrackerConnectionLib` には `UdpTrackerReceiver`、`MultiTrackerManager`、`TrackerPacketAdapter` があり、uuid / source name ごとに tracker packet を識別する候補実装として利用または参考にできる。
- `appsettings.json` は `sim` profile で ibis tracker を port `11010` へ publish している。要求文の「他のトラッカーも存在していそうなら」は、同じ official tracker multicast/port 上に ibis 以外の `uuid` / `source_name` が流れている場合の検出を意味すると解釈するのが自然。ただし、自分が送った packet を自分で拾う loopback を比較対象に混ぜない除外規則が必要。
- 比較用ログの設計候補は、既存 `.tracker-diagnostics.log` へ互換追加 field を足す案と、capture sidecar として `<prefix>-... .tracker-comparison.jsonl.gz` のような別ファイルを増やす案がある。後で比較しやすいこと、他 tracker packet の頻度が ibis committed frame と一致しないこと、schema拡張の安全性を考えると、別 sidecar JSONL を主記録にして diagnostics log には `otherTrackers=<count>` や `comparisonPath=...` 程度の参照を足す案が低リスク。
- 他 tracker の受信は `Tracker.Server` の責務に入る。`Tracker.Core` engine に他 tracker 比較処理を入れる必要はない。Core は ibis の official packet生成と内部 frame維持のままにし、他 tracker packetの保存・比較・表示・CLI解析は Server / CaptureReplay / diagnostics 側に閉じ込めるべき。
- draft PR を実装前に作る方針なら、PR本文には「設計案とtracking追加のみ」「実装は後続commit」と明記し、設計承認前に code/test を足さない運用にするのがよい。

## 結果

- 提案する新規 phase:
  - `comparison-logging`: Capture On 中に ibis tracker と同時刻近傍の他 tracker packet を保存し、capture 後に比較できる状態にする。design、contracts、implementation、verification、review をこの phase 内の小タスクとして扱う。
- 提案する新規タスク分解:
  - `TRACKER-040`: Capture On 比較ログ拡張の設計と tracking を追加する。完了条件は、対象文書に「他 tracker packet 受信対象、保存 schema、self除外、時刻合わせ、UI/CLIの責務境界、互換性」を記述し、`tasks-status.md` / `phases-status.md` に後続小タスクを追加し、設計レビューで blocking finding がないこと。
  - `TRACKER-041`: 他 tracker packet 受信・識別の契約テストを先に追加する。完了条件は、ibis と異なる `uuid` / `source_name` の `TrackerWrapperPacket` を受信候補として保持し、ibis 自身の packet は除外し、複数 source を最新状態として扱う failing test が存在すること。想定 test file は新規 `Tracker/Tracker.Tests/TrackerComparisonReceiverTests.cs` または既存 `TrackerCoordinator...` 系とは別の Server-side focused test。
  - `TRACKER-042`: capture session に比較 sidecar path と metadata を追加する。完了条件は、Capture On で metadata に比較 sidecar path と比較ログ設定が入り、Capture Off / 再On で新しい session に切り替わる failing/passing test があること。想定 test file は `VisionPacketCaptureTests.cs`。
  - `TRACKER-043`: Capture On 中に他 tracker packet を比較 sidecar JSONL へ保存する。完了条件は、受信した他 tracker packet が receivedAt、remote endpoint、uuid、sourceName、trackedFrame frameNumber/timestamp、payload base64 または必要最小 summary として保存され、self除外と flush規則を満たすこと。想定実装候補は `Tracker.Server/Tracking` に `TrackerComparisonCapture...` 系 writer/service、必要なら `TrackerConnectionLib` の receiverを参照。
  - `TRACKER-044`: ibis committed frame と他 tracker 最新packetの対応を diagnostics / replay で比較できるようにする。完了条件は、既存 diagnostics log reader の互換性を壊さず、比較 sidecar を `CaptureReplay` または新 reader から読め、frame時刻近傍で ibis / other の ball/robot count、source uuid、frame number を出せること。想定 test file は `TrackerDiagnosticsLogReaderTests.cs`、新規 `TrackerComparisonLogReaderTests.cs`、`CaptureReplayTests.cs`。
  - `TRACKER-045`: UI/README/運用証跡を整える。完了条件は、`/diagnostics` または README から比較ログの場所と読み方が分かり、既存 capture / diagnostics / render snapshot の表示を壊さず、`dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore -m:1 /nr:false` と focused/full test が通り、gpt-5.5 high review report に blocking finding がないこと。
- 最初に追加すべき失敗テスト案:
  - `VisionPacketCaptureTests.Capture_WhenEnabled_MetadataIncludesTrackerComparisonSidecarPath`: Capture On session metadata に comparison sidecar path が入り、basename が packet/diagnostics/render snapshot と一致すること。
  - `TrackerComparisonCaptureTests.Capture_WhenOtherTrackerPacketArrives_WritesOtherTrackerRecord`: ibis 以外の `uuid` を持つ `TrackerWrapperPacket` を受けたら comparison JSONL に `uuid`、`sourceName`、`trackedFrame.frameNumber`、`trackedFrame.timestamp`、remote endpoint、receivedAt が出ること。
  - `TrackerComparisonCaptureTests.Capture_WhenOwnTrackerPacketArrives_DoesNotWriteSelfRecord`: `TrackerPublisherOptions.Uuid` / `SourceName` と一致する packet は比較対象にしないこと。
  - `TrackerComparisonCaptureTests.Capture_WhenCaptureDisabled_DoesNotCreateComparisonLog`: Capture Off では比較 sidecar も作らず、Capture On 後だけ作ること。
  - `TrackerComparisonLogReaderTests.ReadRecords_KeepsMultipleTrackerSourcesByUuid`: 複数 uuid/source の record を読め、source ごとに最新 frame を引けること。
  - `CaptureReplayTests.Run_WithComparisonLog_CanReportNearestOtherTrackerFrame`: 保存済み capture と comparison log を使い、ibis committed frame 時刻に最も近い他 tracker frame の summary を出せること。
- 受け入れ条件:
  - Capture On 中、packet capture 本体、metadata、diagnostics sidecar、render snapshot に加え、他 tracker 比較ログが同一 capture session として関連付けられる。
  - ibis tracker 自身の packet は比較対象から除外される。
  - 他 tracker が存在しない場合、既存 capture / diagnostics の挙動は変わらず、空の比較ログまたは metadata 上の未作成状態を明確に扱える。
  - 複数の他 tracker が存在する場合、uuid / sourceName / remote endpoint / tracked frame number / timestamp で後から識別できる。
  - 既存 `.tracker-diagnostics.log` の parse と `/diagnostics` の既存表示は互換維持する。
  - Capture Off / 再On で writerとsessionがリセットされ、前sessionに追記しない。
  - 設計書、tracking file、README、report、review が実装差分と同期している。
- 境界条件:
  - 自trackerと他trackerが同じ multicast group / port 上に流れるが、自tracker packetを受信できる環境とできない環境がある。
  - 他 tracker が同じ `uuid` で別 `sourceName`、または空 sourceName を出す場合の識別規則が必要。
  - 他 tracker packet が壊れている、tracked_frame が無い、timestamp が 0、frame number が巻き戻る場合は記録不能にせず skipped/error count として扱う。
  - 他 tracker の publish frequency と ibis committed frame frequency は一致しないため、比較は exact frame match ではなく nearest timestamp / latest-before など明示規則が必要。
  - capture session 開始前に他 tracker packet が届く場合、Capture On の有効状態と session開始タイミングを統一する必要がある。
  - 大量 packet でI/Oが増えるため、`FlushEachPacket` の扱いと上限なし保存のリスクを設計で明記する必要がある。

## リスク

- 要求の「他のトラッカーも存在していそうなら」はネットワーク上の official tracker packet を受ける意味だと推定した。親は、比較対象を「同一 multicast/port の全 tracker packet」にするか、「設定で明示した追加 endpoint のみ」にするかを確定する必要がある。
- `TrackerConnectionLib` は既存だが `Tracker.Server` へ未統合で、multicast join や interface selection は raw vision receiver ほど整っていない。使い回す場合も Server 側の運用要件に合わせた設計確認が必要。
- 自tracker packet の self除外は `uuid` / `sourceName` に依存する。ユーザーが他 tracker と同じ uuid/sourceName を設定した場合は区別不能になりうるため、remote endpoint や publish socket loopback の扱いも設計で決める必要がある。
- diagnostics log の1行 schema を大きく拡張すると `TrackerDiagnosticsLogReader`、`/diagnostics`、既存調査ログの互換性リスクが高い。比較情報は別 sidecar JSONL に寄せるのが安全だが、親が「同じログ1行で比較したい」要求を優先するなら設計を変える必要がある。
- 実装前 draft PR 方針では、`TRACKER-039` のcommit / PR待ち状態と今回の新規計画差分が混ざる危険がある。親は先に `TRACKER-039` を閉じるか、今回用のbranch/PRに設計・trackingだけを載せるかを判断する必要がある。
