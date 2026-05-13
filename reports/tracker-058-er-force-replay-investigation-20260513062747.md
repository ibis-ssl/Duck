# Sub-agent実行レポート

## タスク

- 目的: TRACKER-058 diagnostics replay で ER-Force tracker snapshot が Field に再生されない原因を調査する。
- タスク種別: investigation

## sub-agentを使う理由

- 理由: ユーザー指示により、調査・設計・実装・テストは gpt-5.5 high sub-agent を使う。capture 実データ、設定、diagnostics replay / Field source 経路を独立に確認し、親は manager として判断する。

## 対象範囲

- 対象: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures` 配下の直近 capture、関連 metadata / tracker sidecar / diagnostics log、`Tracker:Receive` 設定、diagnostics replay / Field source / comparison reader 経路。
- 追加対象: ER-FORCE と ibis own snapshot の件数差、timestamp density、diagnostics entry との比率、scrub / playback tick ごとの nearest lookup と cache / index 構築方式。
- 追加対象: 既存 sidecar を読み込み時に対応付ける A案と、capture 保存時に対応表 JSON を作る B案の設計比較。

## 対象外

- 対象外: ER-Force tracker 実機または外部プロセスの停止・再起動、socket abstraction の大規模設計変更、PR #9 外の unrelated cleanup。

## 実行コマンド

- 実行コマンド:
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
- `sed -n '1,260p' reports/tracker-058-er-force-replay-investigation-20260513062747.md`
- `find Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures -maxdepth 2 -mindepth 1 -printf '%TY-%Tm-%Td %TH:%TM:%TS %p\n' | sort -r | head -80`
- `sed -n '1,220p' Tracker/Tracker.Server/appsettings.json`
- `sed -n '1,220p' Tracker/Tracker.Server/bin/Debug/net10.0/appsettings.json`
- `git status --short`
- `jq '.' Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef/ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef.metadata.json`
- `wc -l .../tracker-packet-snapshots.jsonl .../*.tracker-diagnostics.log .../tracker-diagnostics-20260512T212143957Z-64b19e37358d4ad88a93744dc0c80f0f.log`
- `rg -n -i 'er|force|source|label|role|uuid|snapshot|tracker' .../tracker-packet-snapshots.jsonl | head -80`
- `sed -n '1,80p' .../ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef.tracker-diagnostics.log`
- `jq -r '[.sourceLabel,.sourceRole,.sourceUuid,.remoteEndpoint] | @tsv' .../tracker-packet-snapshots.jsonl | sort | uniq -c | sort -nr`
- `jq -r 'select(.sourceLabel=="ER-FORCE") | [.trackedFrameNumber,.trackedFrameTimestampNs,.receivedAt,.semanticSummary.robotCount,.semanticSummary.ballCount] | @tsv' .../tracker-packet-snapshots.jsonl | awk ...`
- `jq -r 'select(.sourceLabel=="ibis") | [.trackedFrameNumber,.trackedFrameTimestampNs,.receivedAt,.semanticSummary.robotCount,.semanticSummary.ballCount] | @tsv' .../tracker-packet-snapshots.jsonl | awk ...`
- `rg -n 'ER-FORCE|farwvkgxsyjsnrpbqrvcdqtepjqsbbjl|TrackerSnapshot|sourceLabel|sourceRole|snapshot' .../*.tracker-diagnostics.log .../tracker-diagnostics-20260512T212143957Z-64b19e37358d4ad88a93744dc0c80f0f.log | head -60`
- `rg --files Tracker/Tracker.Server Tracker/Tracker.Tests | rg '(Diagnostics|CaptureReplay|Tracking|Field|Comparison|Replay)'`
- `rg -n --glob '!**/bin/**' --glob '!**/obj/**' 'TrackerSnapshot|SnapshotInputs|ComparisonSummaries|FieldSource|TrackerDiagnosticsFieldSource|FindNearest|Nearest|SourceLabel|SourceRole|External' Tracker/Tracker.Server/Components/Pages Tracker/Tracker.Server/Tracking Tracker/Tracker.Tests/*Diagnostics* Tracker/Tracker.Tests/*Replay* Tracker/Tracker.Tests/TrackerComparisonSourceTddTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
- `sed -n '1,280p' Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
- `sed -n '1,760p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- `sed -n '1,220p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `sed -n '1,1225p' Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- `sed -n '1,760p' Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
- `rg -n --glob '!**/bin/**' --glob '!**/obj/**' 'Confidence|visibility|Opacity|RobotsBlue|RobotsYellow|DetectionRobot|No drawable|SemanticSummary|CreateTrackerSource' Tracker/Tracker.Server/Components Tracker/Tracker.Server/Vision Tracker/Tracker.Tests`
- `sed -n '1,260p' Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
- `sed -n '1,180p' Tracker/Tracker.Server/Components/Vision/VisionRobotMarker.razor`
- `gzip -cd .../*.render-snapshots.jsonl.gz | wc -l`
- `gzip -cd .../*.render-snapshots.jsonl.gz | jq -r 'select(.frame.frameNumber==468) | [...] | @tsv'`
- `jq -r '[.sourceLabel,.sourceRole,.trackedFrameTimestampNs,.trackedFrameNumber,.receivedAt,.remoteEndpoint] | @tsv' .../tracker-packet-snapshots.jsonl | awk ...`
- `jq -r 'select(.sourceLabel=="ER-FORCE") | [.remoteEndpoint,.trackedFrameTimestampNs] | @tsv' .../tracker-packet-snapshots.jsonl | awk ...`

## 対象ファイル

- 変更または確認したファイル:
- 変更: `reports/tracker-058-er-force-replay-investigation-20260513062747.md`
- 確認: `Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef/ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef.metadata.json`
- 確認: `Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef/tracker-packet-snapshots.jsonl`
- 確認: `Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef/ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef.tracker-diagnostics.log`
- 確認: `Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef/ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef.render-snapshots.jsonl.gz`
- 確認: `Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/tracker-diagnostics-20260512T212143957Z-64b19e37358d4ad88a93744dc0c80f0f.log`
- 確認: `Tracker/Tracker.Server/appsettings.json`
- 確認: `Tracker/Tracker.Server/bin/Debug/net10.0/appsettings.json`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
- 確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
- 確認: `Tracker/Tracker.Server/Components/Vision/VisionRobotMarker.razor`
- 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs`
- 確認: `Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
- 指摘1: 直近 capture には ER-FORCE tracker snapshot が実在する。metadata の `TrackerSnapshotSidecarPath` は `ssl-vision-packets-20260512T212158834Z-ac05022b5aa54d62a06aa8b507e1daef/tracker-packet-snapshots.jsonl`、`TrackerSnapshotLog.RecordCount=22464`、`SkippedRecordCount=0`、`ErrorCount=0`。`TrackerSnapshotSources` には `SourceName=ER-FORCE`、`SourceRole=external`、`SourceLabel=ER-FORCE`、`SourceUuid=farwvkgxsyjsnrpbqrvcdqtepjqsbbjl` が 7 endpoint 分あり、それぞれ `RecordCount=2946`。`ibis` は own として endpoint あり / なしの 2 source summary、各 `RecordCount=921`。
- 指摘2: sidecar 実体でも ER-FORCE は `20622` records、`2946` unique timestamps、範囲 `1778620918834101760` から `1778620948283821312`、約 `29.449720` 秒、約 `700.244 records/sec`、`100.035 unique timestamps/sec`。ibis own は `1842` records、`921` unique timestamps、範囲 `81686157011402` から `81715596552087`、約 `29.439541` 秒、約 `62.569 records/sec`、`31.284 unique timestamps/sec`。diagnostics log は `837` entries、render snapshot は `921` records。diagnostics entry count と ER-FORCE record count の比率は約 `1:24.6`、ER-FORCE unique timestamp でも約 `1:3.52`。
- 指摘3: ER-FORCE と ibis own の `TrackedFrame.timestamp` は時刻系が一致していない。例: selected diagnostics frame `468` の ibis own timestamp は `81686157011402` だが、最初の ER-FORCE timestamp は `1778620918834101760`。`TrackerDiagnosticsComparisonViewStateReader` と `TrackerSnapshotReplayReader` は ibis own の data timestamp を基準に nearest snapshot を探すため、この状態では ER-FORCE は時系列対応せず、scrub / playback しても ER-FORCE の nearest は実質的に先頭側へ張り付く可能性が高い。
- 指摘4: scrub / playback tick ごとの処理は sidecar 全体の JSONL 再読込ではない。`TrackerDiagnosticsComparisonViewStateReader.GetOrBuildIndex` が `ComparisonSnapshotIndex` を cache し、`BuildIndex` で timestamp sort 済み配列、role 別配列、source label 別 dictionary、own frame dictionary を構築する。`FindNearest` は binary search であり、通常 tick ごとに source 全体を線形探索していない。ただし初回 index 構築は 22464 records を全読込し、`MaxCachedIndexes=2` なので複数 log を切り替えると再構築されうる。
- 指摘5: Field source selector の option は `External (20622)` と `ER-FORCE (20622)` を作れる構造で、source option selection 自体で ER-FORCE が欠落する証跡はない。`LoadFieldSourceFrame` も cache 済み index から selected entry の own snapshot timestamp に対する nearest source frame を返す。ただし時刻系不一致により、返る frame は再生位置に追従しない。
- 指摘6: 描画変換では `DiagnosticsFieldViewFactory.CreateTrackerSource*` が semantic summary を `SSL_Detection*` DTO に戻す。ER-FORCE semantic summary は robotCount=22、ballCount=1 だが、robot visibility は全件 `0`、ball visibility も集計上 `0`。`VisionRobotMarker` は confidence で表示を抑制していないため robot marker 自体は描画対象になるはずだが、外部 source を選んだ Field に `CandidateMissing` や空表示が出る場合は描画だけでなく nearest timestamp / selected source / UI responsiveness を疑うべき。
- 指摘7: `Tracker:Receive.Enabled` は source appsettings と bin appsettings の両方で `true`。metadata の `TrackerOptions.Receive.Enabled` も `true`。今回の直近 capture については receive 設定や sidecar 生成が直接原因で「ER-FORCE snapshot が入っていない」状態ではない。

## 結果

- 結果:
- 判定: 直近 capture には ER-FORCE tracker snapshot が十分に入っている。欠落原因は capture / receive / sidecar 未生成ではない。
- 判定: 現行 diagnostics replay / Field source 経路は metadata から sidecar を解決し、ER-FORCE source option を構築し、cache 済み index から nearest frame を返す実装になっている。tick ごとの全 JSONL 再読込や明白な source option 欠落は確認できない。
- 判定: 最重要の原因候補は、ibis own と ER-FORCE の `TrackedFrame.timestamp` の時刻系不一致。nearest timestamp 対応付けが成立していないため、ER-FORCE は UI Field へ正しい再生時刻の frame として渡らない。ユーザー仮説の performance / data volume については、初回 index 構築コストと 2 件 cache eviction リスクはあるが、今回の「再生されない」主因としては timestamp mismatch の方が強い。
- 設計方針案: diagnostics replay の external tracker source は、`TrackedFrame.timestamp` が own と同一時刻系でない場合に `receivedAt` または session-relative received offset で対応付ける fallback を持つ。source ごとに timestamp range を比較し、own timestamp range と外部 timestamp range が明らかに非重複の場合は data timestamp nearest を使わず、diagnostics entry timestamp / own receivedAt に近い source snapshot を使う。UI には matching rule を `nearest-received-at` などで表示する。
- performance 設計案: log open / sidecar index 構築時に `diagnostics entry -> source別 nearest snapshot` の対応表を作る設計は有効。現状の binary search は tick ごと全探索ではないため必須ではないが、ER-FORCE の 7 endpoint 重複と 100Hz density では playback tick ごとの複数 lookup / UI state rebuild を減らせる。まずは source 別 sorted arrays と fallback rule を保ち、必要なら selected diagnostics entries 向けの nearest map を cache key に含めて構築する。
- 設計比較 A案: 既存 sidecar 互換を維持し、ログを開いた直後または `ComparisonSnapshotIndex` 構築時に `diagnostics entry timestamp -> source別 nearest snapshot` 対応表を作る。既存 capture を救えるため今回の単独解にできる。100MB超 / 長時間ログでは初回構築コストは残るが、scrub / playback tick は対応表 lookup にできる。ER-FORCE / ibis の density 差は source 別配列と fallback rule で吸収しやすい。file format 変更は不要で design doc 更新は比較規則 / cache 方針の追記程度。実装範囲は `TrackerDiagnosticsComparisonViewStateReader` 中心で、regression test は既存 sidecar fixture を増やすだけで作りやすい。
- 設計比較 B案: capture 保存時に diagnostics entry / vision frame / tracker source snapshot の対応を JSON sidecar または metadata に保存する。将来 capture の scrub / playback 性能は最も安定し、長時間ログでも読み込み済み対応表を使える。ただし既存 capture には対応表が無いため、単独解ではユーザー手元の現ログを救えない。file format 追加になるため design doc、metadata schema、reader/writer、後方互換、破損時 fallback の更新が必要。regression test は writer と reader の両方が必要で、実装範囲は A案より広い。
- 推奨方針: 今回は A案を先に実装し、既存ログを即時に救う。B案は A案の対応表生成ロジックを再利用できる形で後続最適化として設計する。B案を入れる場合も、既存 capture 用の A案 fallback は残す。
- TDD対象案: `TrackerDiagnosticsComparisonViewStateTests` に「external timestamp range が own data timestamp range と非重複の場合、Field source frame は selected diagnostics entry の receivedAt に近い ER-FORCE snapshot を返し、scrub で tracked frame が進む」回帰テストを追加する。併せて `TrackerReplayIntegrationTddTests` または `CaptureReplayTests` に comparison summary の fallback rule 表示を固定する。
- 変更候補ファイル: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`、必要に応じて `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`、`Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`、`Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs`。

## リスク

- 未解決のリスクまたは後続対応:
- ER-FORCE 側 timestamp が絶対 UNIX ns なのか、別 epoch / monotonic clock なのかは外部 process を再起動せずには確定していない。ただし capture 内の own と external の range が桁違いに非重複であることは確認済み。
- ER-FORCE は同一 `sourceUuid` / `sourceLabel` が 7 remote endpoint で重複している。fallback を `receivedAt` にすると同一 data timestamp に複数候補があり、tie breaker は現行同様 `ReceivedAt` 先頭または endpoint 集約方針を明示する必要がある。
- 初回 sidecar index 構築は 22464 records を読み込むため、UI responsiveness 問題が別途ある可能性は残る。今回の証跡では tick ごとの全探索ではないが、log 切替で `MaxCachedIndexes=2` を超えると再読込される。
- B案を採用する場合でも、既存 capture には保存時対応表が存在しないため、A案相当の読み込み時 fallback を消すと今回のユーザー報告を解消できない。
- ER-FORCE semantic summary の visibility は ball / robot とも 0。現行 marker は confidence で非表示にしていないが、将来 opacity/filter を追加した場合は ER-FORCE が見えなくなる危険がある。TDD では visibility 0 の external snapshot でも Field source frame が drawable として扱われる期待を明示した方がよい。
- `Tracker/Tracker.Server/appsettings.json` の `Tracker:Receive:Enabled=true` は既存 dirty diff であり、ユーザー実行用ローカル設定の可能性があるため変更していない。
