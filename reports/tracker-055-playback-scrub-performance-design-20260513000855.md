# TRACKER-055 playback / scrub performance design

## 要件

- `/diagnostics` の timeline scrubber 移動と playback tick が、tracker snapshot sidecar の再読込で遅くならない。
- 100MB は上限ではなく、1-2分程度の capture で到達しうる通常サイズとして扱う。100MB 以上でも、tick / scrub 時に sidecar サイズへ比例した I/O / JSON parse / protobuf parse を発生させない。
- log 選択時の初回読み込みも巨大 sidecar で UI を固めすぎない。既存 JSONL sidecar を活かし、lightweight index、lazy / progressive load、background preload、per-source / time index、cancellation、file-state keyed cache、bounded memory policy を優先する。
- 今回 task は正常系優先とし、DB 化、大規模 storage 導入、sidecar format の破壊的変更は避ける。
- 通常 Play は実 timestamp delta を維持する。
- 調査用には実時間より大幅に速い playback を選べる。
- 既存の Stop、末尾到達時の先頭戻り、stale tick guard は維持する。

## 設計

### 推奨設計

`TrackerDiagnosticsComparisonViewStateReader` 側へ file-state keyed な lightweight comparison index cache を追加する。`TrackerDiagnosticsComparisonUiState` は selected log / selected entry / source filter の UI 状態調停だけを担当し、source options と selected entry comparison の生成責務は reader / index 側に寄せる。

現在の遅さは `Diagnostics.razor.cs` の `SelectEntryByIndex` / playback tick が `SyncComparisonState` を呼び、そのたびに `TrackerDiagnosticsComparisonUiState.Load` -> `TrackerDiagnosticsComparisonViewStateReader.Load` -> `TrackerSnapshotReplayReader.ReadSession` で sidecar 全体を読み直すことにある。TRACKER-055 では scrub / tick の処理を「選択済み diagnostics entry と source filter を、既存 index に問い合わせるだけ」に変える。

### lightweight index

新規 index は full `TrackerSnapshotReplaySession` や raw payload 本体を保持しない。UI comparison に必要な scalar projection だけを JSONL から作る。

- `ReceivedAt`
- `SourceRole`
- `SourceLabel`
- `TrackedFrameNumber`
- `TrackedFrameTimestampNs`
- `RawPayloadRestored` 相当の lightweight flag
- `BallCount`
- `RobotCount`

`RawPayloadRestored` は通常 writer が `PayloadBase64` と `SemanticSummary` を同時に保存する前提では、index build 時に全 record の protobuf parse をしない。まず `PayloadBase64` の存在と `SemanticSummary` の有無から UI 用 flag を作る。古い / 手書き record で `SemanticSummary` がない場合だけ、該当 record の fallback として既存 `EnsureSemanticSummary` 相当を使う。raw payload の厳密な protobuf restore 検証を行う場合も、全 sidecar に対してではなく selected nearest snapshot に限定して lazy に行う。

index は次を持つ。

- 全 projection の timestamp 昇順配列。
- own snapshot を `TrackedFrameNumber` から引くための `Dictionary<uint, ComparisonSnapshotProjection[]>`。
- role 別 bucket と source label 別 bucket の timestamp 昇順配列。
- `SourceOptions`。role と source label の件数は index build 時に集計する。
- metadata の record / skipped / error count と sidecar status。

selected entry comparison は次の流れで作る。

1. selected diagnostics entry の `TrackedFrame` を `uint` として読む。
2. own bucket から同じ tracked frame の own snapshot を取得する。複数ある場合は timestamp 昇順の先頭を現行互換として使う。
3. selected source filter に対応する candidate bucket を選ぶ。`All` の場合は non-own が存在すれば non-own を優先し、なければ all を使う。
4. candidate bucket は timestamp 昇順なので、own timestamp に対して binary search し、挿入位置の前後だけを比較して nearest timestamp を決める。
5. comparison result は既存 `TrackerDiagnosticsComparisonEntryComparison` の status / timestamp delta / source label / ball count / robot count へ変換する。

この設計により、tick / scrub 時の comparison 更新は O(log n) または小さい bucket の O(log n) になり、sidecar の file size に比例した I/O / parse は発生しない。

### cache key / invalidation

cache key は path だけにしない。次の file state を含む immutable key にする。

- diagnostics log path: `Path.GetFullPath` した絶対 path、存在有無、last write time UTC ticks、file length。
- metadata path: 解決済み絶対 path、存在有無、last write time UTC ticks、file length。
- sidecar path: metadata から解決した絶対 path、存在有無、last write time UTC ticks、file length。

metadata missing / corrupt、sidecar missing / not-created / empty / corrupt も同じ reader path で `TrackerDiagnosticsComparisonViewState` として表現する。ただし missing file の cache は毎回 `FileInfo` の存在確認だけを行い、file が作成されたら別 key として再評価する。

CaptureOn 中に sidecar が伸びている場合は、index build 開始時と完了時で sidecar file state を比較する。途中で length / last write time が変わった場合は、古い index を Ready として確定しない。正常系では capture 完了後の stable file を読む。capture 中の追従を扱う場合は background rebuild を予約するが、TRACKER-055 では scrub / tick を固めないことを優先し、過剰な live-tail indexer にはしない。

cache は `TrackerDiagnosticsComparisonViewStateReader` の内部に置く。`TrackerDiagnosticsComparisonViewStateReader` は `Program.cs` で singleton 登録されているため、thread-safe にする。bounded memory policy として、保持する index は lightweight projection のみ、raw payload 文字列は保持しない、かつ LRU で少数件に制限する。初期値は current + previous 程度の 2 index を推奨する。設定化する場合も TRACKER-055 では hard-coded default で十分。

### 初回読み込み

巨大 sidecar では log 選択時の初回 index build だけは sidecar size に比例する。ここは tick / scrub と違い一度だけ必要な処理だが、UI を固めすぎない工夫を入れる。

推奨は background preload である。log 選択時は diagnostics log / render snapshot index を先に表示し、comparison panel は `Indexing` 相当の状態を出す。`TrackerDiagnosticsComparisonUiState` は selected log の index build を cancellation token 付きで開始し、完了後に現在も同じ log / file-state なら view-state を Ready に更新する。別 log へ切り替わった場合や reload / Stop による stale task は破棄する。

既存 status enum に `Indexing` を足すか、既存 enum を増やしたくない場合は `SidecarStatus=Ready` を使わず `Error` に "Tracker snapshot index is loading." を出す暫定表現にする。ただし設計上は `TrackerDiagnosticsComparisonSidecarStatus.Indexing` を追加する方が UI と test が明確になる。

初回実装の正常系優先ラインは「log 選択で一度だけ lightweight index を作り、以後 scrub / tick で再読込しない」までを必須とする。background preload / cancellation は巨大 sidecar で UI 固着を避けるため TRACKER-055 内で可能な限り入れる。DB 化、別 index file 永続化、JSONL format 変更は対象外にする。

### 責務分担

- `TrackerDiagnosticsComparisonViewStateReader`: metadata / sidecar path 解決、file-state key 作成、cache lookup / invalidation、index build、source options 生成、selected entry comparison 生成を担当する。
- `TrackerDiagnosticsComparisonUiState`: selected source filter の保持、UI select value の parse、選択 filter が消えた場合の All fallback、loading 完了後の最新 selected entry への再問い合わせを担当する。sidecar I/O や source option 集計は担当しない。
- `Diagnostics.razor.cs`: selected log / selected entry / playback mode / cancellation を管理し、comparison には selected log path と displayed selected entry を渡すだけにする。scrub / tick では cache 構築を起動せず、既存 cache への lookup だけにする。
- `TrackerSnapshotReplayReader`: CLI / replay 用の full reader として維持する。UI responsiveness 改善のために full replay session reader の contract を変えない。UI comparison index は別 helper に分ける。

### playback speed UX

通常 Play は既存契約どおり実 timestamp delta を維持する。Play button は常に `1x Real time` で、押下中は既存どおり Stop に切り替える。

調査用高速再生は Play の意味を変えず、Fast Forward 側へ明示的に寄せる。UI は Fast Forward button の近くに speed selector を置き、例として `4x`、`16x`、`64x` を選べるようにする。既定は `16x` を推奨する。Fast Forward 押下中は Fast Forward affordance を Stop に切り替え、既存 Stop button / end reset / stale tick guard を維持する。

`DiagnosticsPlaybackState` は `DiagnosticsPlaybackSpeed` または multiplier を受け取る形へ拡張する。Play は multiplier を無視して timestamp delta のまま、Fast Forward は `timestamp delta / multiplier` を基本にする。interval 下限により高速化が頭打ちになるため、Fast Forward は step も speed に応じて進める。例えば `4x` は step 1-2、`16x` は step 4-5、`64x` は step 16 程度を候補にし、`GetNextIndex` と `GetInterval` の組み合わせで総再生時間が概ね実時間 / multiplier へ近づくようにする。

既存の `ShouldApplyTick(activeMode, tickMode, isCancellationRequested)` は維持する。speed 変更中の stale tick を防ぐには、tick 側に speed version または selected speed を渡し、active mode だけでなく active speed と一致する場合だけ反映する設計にする。

### 既存 design doc へ追記すべき文言

`Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md` の `diagnostics / replay / playback 互換追加` へ次を追記する。

```markdown
`/diagnostics` の tracker snapshot comparison は、timeline scrubber 移動や playback tick のたびに tracker packet snapshot sidecar JSONL を再読込しない。log 選択時に metadata path、sidecar path、diagnostics log path と各 file の last write time / length を key にした lightweight index を作成または cache し、selected entry の変更時はその index から source options と nearest timestamp comparison を生成する。

100MB は上限ではなく通常の capture で到達しうるサイズとして扱う。100MB 以上の sidecar でも tick / scrub 時に sidecar size へ比例した I/O / JSON parse / protobuf parse を発生させない。初回 log 選択時の index build は既存 JSONL sidecar を活かし、background preload、cancellation、bounded memory cache により UI を固めすぎない。

通常 Play は diagnostics entry の実 timestamp delta を維持する。調査用の高速再生は Fast Forward または明示 speed selector として表現し、Play の 1x real-time 契約、Stop、末尾到達時の先頭戻り、stale tick guard を維持する。
```

## 実装対象

- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - full replay reader 依存を UI comparison path から外し、lightweight index cache / builder を使う。
  - constructor injection または internal seam を追加し、test で sidecar read / index build 回数を観測できるようにする。
- 新規候補: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonIndex.cs`
  - file-state key、projection、source buckets、nearest lookup、source option generation を閉じ込める。
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - filter fallback は維持しつつ、同一 file-state の index を二重 build しない。
  - background preload を入れる場合は loading / cancellation / stale completion の状態を持つ。
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - log 選択時に index preload を起動し、scrub / playback tick は cache lookup のみを行う。
  - playback speed selector の state と stale tick guard 用 speed version を保持する。
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - Fast Forward 近くに調査用 speed selector を追加する。
  - comparison index 作成中の status を表示する。
- `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
  - Play の real-time interval 契約を維持し、Fast Forward の multiplier / step を表現する。
- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - 上記「既存 design doc へ追記すべき文言」を反映する。今回 sub-agent は report 以外を編集しないため、実編集は親または design-executor 側で行う。

## テスト方針

focused test は I/O 再読込回数、interval / speed multiplier、UI state の三系統で固定する。

### I/O / cache / index

- 同じ diagnostics log path / metadata path / sidecar path / file state で `TrackerDiagnosticsComparisonViewStateReader.Load` を複数回呼んでも、sidecar index build が 1 回だけであること。
- `TrackerDiagnosticsComparisonUiState.Load` が filter fallback のため reader を再問い合わせしても、同じ cache を使い、sidecar read が増えないこと。
- selected entry を連続変更しても selected comparison が変わるだけで、sidecar read / parse count は増えないこと。
- sidecar の last write time または file length が変わった場合は cache が invalidated され、次回 load で index build が 1 回増えること。
- metadata の last write time / length が変わり sidecar path が変わった場合、古い index を使わないこと。
- metadata missing / corrupt、sidecar missing / not-created / empty / corrupt の既存 status contract が維持されること。
- source options は index から生成され、role count / source label count が既存期待値と一致すること。
- nearest timestamp は full scan ではなく sorted bucket lookup の結果として、既存 `Load_ResolvesSourcesFilterAndSelectedEntryComparisonFromDiagnosticsLogPath` と同じ comparison を返すこと。
- background preload を入れる場合、log 切替や cancellation 後に古い build result が現在の view-state を上書きしないこと。

I/O 回数検知は `TrackerDiagnosticsComparisonViewStateReader` に internal constructor seam を足し、test double の index source / sidecar reader が `BuildCount` や `ReadLineCount` を記録する形を推奨する。実 file size を大きくする test より、同じ sidecar を何回読むかを観測する test を優先する。

### playback interval / speed

- Play は speed selector に関係なく、現在 entry と次 entry の timestamp delta を返すこと。
- Play は長い timestamp delta でも最大 clamp せず実時間を維持すること。
- Fast Forward `4x` / `16x` / `64x` で interval が delta / multiplier を基準に短くなること。
- Fast Forward は minimum interval を下回らないこと。
- high multiplier では step が増え、総再生時間が実時間 / multiplier に近づくこと。
- Stop 後、mode 変更後、speed 変更後の stale tick は entry selection を更新しないこと。
- 末尾到達時は既存どおり Stop して先頭 index へ戻ること。

### UI state

- source filter selector を変更しても sidecar read は増えず、index から selected entry comparison だけが再計算されること。
- log 選択時に comparison index が未完了なら status は loading / indexing として表示され、既存 diagnostics log、timeline、render snapshot 表示は壊れないこと。
- index build 完了後、現在の selected entry / selected filter に対する comparison が表示されること。
- log 切替後に旧 log の background index completion が戻っても、現在 log の comparison を上書きしないこと。

focused command 候補:

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
NUGET_HTTP_CACHE_PATH="$PWD/.codex-nuget-http-cache" \
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore \
  --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~DiagnosticsPlaybackStateTests" \
  -m:1 /nr:false -p:NuGetAudit=false
```

## リスク

- 初回 index build は sidecar size に比例する。tick / scrub の比例 I/O は消せるが、log 選択時の一度だけの scan は残る。巨大 sidecar では background preload / cancellation / loading status を入れないと UI が固まるリスクが残る。
- full payload や full replay session を cache するとメモリ使用量が sidecar size に強く引きずられる。cache は scalar projection だけにし、raw payload 文字列を保持しない。
- singleton reader に cache を置くため、thread safety と bounded eviction が必要。LRU なしで session ごとに index を溜めると長時間運用でメモリが膨らむ。
- path だけの cache key では CaptureOn 継続中の append や同名 file 更新で stale index になる。diagnostics log / metadata / sidecar の file state を key に含める。
- build 中に sidecar が更新されると、metadata count と index 内容がずれる可能性がある。build 前後の file state 比較で stale build を破棄する。
- `RawPayloadRestored` の厳密性を全 record の protobuf parse で維持すると、初回 index build が重くなる。UI 用 index では通常 writer の semantic summary を信頼し、厳密検証は selected nearest snapshot へ lazy 化するのが妥当。
- Fast Forward の multiplier だけを増やしても minimum interval に当たり、体感速度が上がらない可能性がある。interval と step の両方で設計する必要がある。
- Play の既定速度を変えると、実 timestamp delta 再生の既存契約を壊す。高速化は Fast Forward / 調査用 speed selector に閉じる。
- 今回は正常系優先のため、永続 index file、DB、別 storage、sidecar format 変更は避ける。将来さらに巨大な long capture を常用する場合は、別 task で sidecar writer 時の index sidecar 生成を検討する余地がある。
