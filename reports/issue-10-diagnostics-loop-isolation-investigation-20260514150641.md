# RAW-VISION-017 diagnostics overlay 遅延調査

## 対象

- capture: `Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T055027883Z-465bba2cd89345bfb13014e2390c4dd5`
- 症状: live overlay では `トラッカーなし` と `ER-FORCE` が重なるが、Diagnostics overlay では `トラッカーなし` が遅れて見える。

## 確認結果

- raw SSL-Vision packet は 9,810 packets / 79.284s、平均 interval は 8.083ms。
- ER-FORCE は unique tracked frame 7,927 frames / 79.300s、平均 interval は 10.005ms。
- render snapshot は 2,469 frames / 79.223s、平均 interval は 32.100ms。
- ibis own tracker snapshot も unique frame 2,469 frames / 79.223s、平均 interval は 32.100ms。
- tracker timeline tick 上の render snapshot hold は 52,491 ticks で平均 17.872ms、最大 104ms。

## 判断

Diagnostics の `Vision Input` は raw packet capture の 8ms cadence ではなく、`TrackerCoordinator.DispatchResult` の `WorldFrameCommitted` で保存される render snapshot を読んでいる。この render snapshot は ibis tracker の committed frame cadence と同じ 32.1ms で更新されるため、ER-FORCE の 10ms 前後 cadence と overlay すると raw/no-tracker 側だけが stale に見える。

live overlay は `VisionPacketStore` / `TrackedSnapshotStore` / external tracker の latest snapshot を UI render tick で固定しており、保存 replay の render snapshot cadence に縛られない。そのため live では重なり、Diagnostics だけ遅れて見える説明がつく。

## 方針

修正は marker の描画補正ではなく、loop isolation として扱う。

- tracker 処理ループは tracker state 更新と publish を担当する。
- server live 表示ループは store の latest immutable snapshot を UI render tick で固定する。
- diagnostics logging/replay ループは tracker 処理ループから直接書き込まず、別 loop で latest raw / latest tracker snapshot を読んで保存する。
- Diagnostics replay の `Vision Input` は tracker committed frame cadence ではなく、保存された raw/latest snapshot cadence に基づく。

## 実行コマンド

```bash
find Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T055027883Z-465bba2cd89345bfb13014e2390c4dd5 -maxdepth 2 -type f -printf '%p\t%s bytes\n' | sort
jq '. | {SessionFolder, TrackerSnapshotLog, TrackerSnapshotAlignmentLog, TrackerSnapshotSources}' Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T055027883Z-465bba2cd89345bfb13014e2390c4dd5/ssl-vision-packets-20260514T055027883Z-465bba2cd89345bfb13014e2390c4dd5.metadata.json
gzip -cd ...jsonl.gz | jq -r '.receivedAt' | awk ...
gzip -cd ...render-snapshots.jsonl.gz | jq -r '.receivedAt' | awk ...
jq -r 'select(.sourceRole=="external" and .sourceLabel=="ER-FORCE") | [.trackedFrameNumber,.receivedAt] | @tsv' tracker-packet-snapshots.jsonl | awk ...
jq -r 'select(.sourceRole=="own") | [.trackedFrameNumber,.receivedAt] | @tsv' tracker-packet-snapshots.jsonl | awk ...
jq -r 'select(.replayTimelineKind=="tracker-snapshot" and .renderReceivedAt != null) | [.replayTimelineIndex,.replayTimelineReceivedAt,.renderReceivedAt,.renderMatchRule] | @tsv' tracker-snapshot-alignment.jsonl | awk ...
```
