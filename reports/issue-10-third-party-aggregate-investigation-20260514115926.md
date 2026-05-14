# Sub-agent実行レポート

## タスク

Vision 画面で 3rd party tracker 表示時に `ER-FORCE` が複数出る原因と、統合レイヤー追加方針の調査。

## sub-agentを使う理由

source option 生成、3rd party tracker snapshot store / composer、Vision live comparison の UI 表示にまたがるため、実装前に sub-agent で原因と設計影響を切り分ける。

## 対象範囲

Vision 画面の 3rd party tracker source option、`ER-FORCE` label の重複表示、統合レイヤーの実装候補、関連 tests / design。

## 対象外

製品コード変更、テスト変更、PR draft 解除、`Tracker/Tracker.Server/appsettings.json`。

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `sed -n '1,260p' reports/issue-10-third-party-aggregate-investigation-20260514115926.md`
- `rg -n "Issue #10|split|overlay|source|Layer|RAW-VISION-016|3rd|third|tracker|External|Aggregate|aggregate" Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md`
- `rg --files reports | rg 'reports/issue-10-'`
- `sed -n '187,245p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
- `sed -n '1,240p' reports/issue-10-vision-overlay-investigation-20260514080106.md`
- `sed -n '1,240p' reports/issue-10-live-same-tick-investigation-20260514081135.md`
- `rg -n "VisionLiveComparison|ExternalTracker|MultiTracker|ThirdParty|3rd|SourceOption|Layer A|LayerA|LayerB|source option|SourceKey|DisplayLabel|External|Aggregate|aggregate|source label|SourceLabel" Tracker/Tracker.Server Tracker/Tracker.Tests -g '!**/bin/**' -g '!**/obj/**'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs | sed -n '1,180p'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs | sed -n '180,460p'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs | sed -n '460,720p'`
- `nl -ba TrackerConnectionLib/src/MultiTrackerManager.cs | sed -n '1,260p'`
- `nl -ba TrackerConnectionLib/src/TrackerState.cs | sed -n '1,180p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor | sed -n '1,320p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor | sed -n '320,620p'`
- `nl -ba Tracker/Tracker.Server/Program.cs | sed -n '1,170p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '220,460p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '460,700p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '900,1160p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '1160,1335p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '1450,1675p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs | sed -n '1,150p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs | sed -n '1,300p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs | sed -n '1,360p'`
- `rg -n "ER-FORCE|ThirdPartyTracker|third-party|source option|SourceOptions|RemoteEndpoint|SourceLabel" Tracker/Tracker.Tests -S`
- `nl -ba Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs | sed -n '1,890p'`
- `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs | sed -n '240,315p'`

## 対象ファイル

- 変更:
  - `reports/issue-10-third-party-aggregate-investigation-20260514115926.md`
- 確認:
  - `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `Tracker/Tracker.Server/Design/tasks-status.md`
  - `reports/issue-10-vision-overlay-investigation-20260514080106.md`
  - `reports/issue-10-live-same-tick-investigation-20260514081135.md`
  - `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs`
  - `Tracker/Tracker.Server/Components/Pages/Home.razor`
  - `Tracker/Tracker.Server/Program.cs`
  - `TrackerConnectionLib/src/MultiTrackerManager.cs`
  - `TrackerConnectionLib/src/TrackerState.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
  - `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
  - `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`

## 指摘事項

- 原因: Vision live の 3rd party tracker source option は、`VisionLiveComparisonSnapshotComposer.CaptureThirdPartyTrackerSnapshots()` が `externalTrackerManager.Trackers.Values` を `LastPacket != null` で全列挙し、`SourceLabel` と `RemoteEndpoint` で並べ替えた全 state を個別 snapshot にしている。`MultiTrackerManager` 側の state key は `uuid + sourceName + remoteEndpoint` なので、同じ `SourceName` / label の `ER-FORCE` が複数 remote endpoint または uuid から来ると manager entry が複数になる。参照: `TrackerConnectionLib/src/MultiTrackerManager.cs:35-45`, `TrackerConnectionLib/src/MultiTrackerManager.cs:95-120`, `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs:613-625`。
- 原因: snapshot から source option を作るとき、Vision live は各 3rd party snapshot をそのまま `VisionLiveComparisonSourceOption` に変換する。key は snapshot の `Key`、label は snapshot の `Label` であり、現在の 3rd party snapshot key は `$"third-party:{sourceLabel}"` だけで作られる。role / uuid / remote endpoint は key に入らず、`ER-FORCE` が複数 state ある場合でも同じ表示名、同じ option value の候補が複数出る。参照: `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs:591-598`, `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs:628-657`。
- 原因ではないもの: diagnostics replay の sidecar / comparison path が Vision live の option を直接生成しているわけではない。Vision live は `Program.cs` で singleton 登録された `VisionLiveComparisonSnapshotComposer` を `Home.razor` に inject し、100ms refresh ごとに `CaptureRenderTickSnapshot()` と `CreateViewState()` を呼ぶ経路だけで source option を作る。参照: `Tracker/Tracker.Server/Program.cs:49-62`, `Tracker/Tracker.Server/Components/Pages/Home.razor:8-10`, `Tracker/Tracker.Server/Components/Pages/Home.razor:274-283`, `Tracker/Tracker.Server/Components/Pages/Home.razor:575-586`。
- diagnostics 側との差分: diagnostics は `External` / `Own` / `Unknown` の role aggregate と、source label aggregate を明示的に option 化している。Field source も `Vision Input` / `ibis tracker` / `External` / `Unknown` / source label を持ち、`All` は Field source から外している。source label は `snapshotsBySourceLabel` で group 化され、個々の remote endpoint を選択肢として列挙しない。参照: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs:1166-1201`, `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs:1459-1637`。
- diagnostics の aggregate は balls / robots を複数 endpoint から union する意味ではない。`External` や source label 選択は filter で候補集合を作り、alignment / latest-before / nearest の規則で代表 snapshot を 1 つ選ぶ。保存済み alignment では source key / remote endpoint を持った record から代表を選ぶ contract がある。参照: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs:1043-1156`, `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs:251-315`。
- 推奨方針: Vision live には「source label ごとの aggregate option」を先に追加する。`ER-FORCE` が複数 endpoint / uuid で見えても、通常の Layer A/B selector では `ER-FORCE` を 1 つだけ表示し、その aggregate snapshot は同じ label 内の最新 `ReceivedAt` の代表 snapshot を使う。個別 endpoint / uuid は必要なら debug 用の詳細 option として残すが、key には role / label / uuid / remote endpoint を含めて一意化し、label も endpoint 付きにする。
- 推奨実装位置: `VisionLiveComparisonSnapshotComposer` の 3rd party snapshot 作成境界で source identity を保持し、`CreateSourceOptions()` の手前で aggregate snapshot を合成するのが自然。UI component 側で重複 `<option>` を後処理で潰すと、`ResolveThirdPartyTrackerSnapshot()` と same-source collapse の key contract がずれ、詳細 metadata も落ちる。
- 選ばなかった方針: `External aggregate` だけを追加する方針は、複数外部 tracker がいる環境で ER-FORCE 以外の tracker まで混ざるため、今回の「ER-FORCE がたくさん出る」症状の説明と操作性改善には粗すぎる。追加するなら source label aggregate の上位 option として別途扱う。
- 選ばなかった方針: 同じ label の全 endpoint の balls / robots を union して 1 layer に描く方針は初期実装では避けるべき。同じ robot id / ball が複数 endpoint に重複している可能性が高く、衝突時の優先順位、timestamp 差、visibility の扱いを決めないと field 上に実在しない重複 robot が出る。
- 選ばなかった方針: `TrackerPacketSnapshotLogWriter.GetLatestSnapshotsBySource()` や CaptureOn sidecar を Vision live の統合 source として使う方針は避ける。既存設計でも CaptureOn session 保存用で、通常 live Vision の source store ではないと明記されている。参照: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md:213-215`, `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs:156-160`, `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs:207-219`。

## 結果

- Vision 画面の Layer A/B source option は `Home.razor` の Compare mode toolbar が `comparisonViewState.SourceOptions` を `<select>` に列挙している。選択値は option key で、選択後は `FindSourceOption()` が key 一致の最初の option を使う。したがって現在のように duplicate key の `ER-FORCE` option が複数あると、UI 上は複数表示される一方で、実際に選べる対象は key 一致の先頭に寄る。参照: `Tracker/Tracker.Server/Components/Pages/Home.razor:125-147`, `Tracker/Tracker.Server/Components/Pages/Home.razor:377-395`, `Tracker/Tracker.Server/Components/Pages/Home.razor:589-595`。
- 3rd party tracker source option の元は `MultiTrackerManager<TrackerPacketAdapter>` で、`Program.cs` では publisher の uuid / sourceName を self identity にした singleton として登録される。receiver が有効な場合だけ hosted service が packet を manager へ投入する。参照: `Tracker/Tracker.Server/Program.cs:56-80`。
- `MultiTrackerManager` は `SourceRole` を own / unknown / external に分類し、`SourceLabel` は unknown 以外では `SourceName`、なければ uuid、なければ remote endpoint を使う。state key は label ではなく uuid / sourceName / remote endpoint なので、同じ `SourceName=ER-FORCE` でも endpoint が違えば複数 state として残る。参照: `TrackerConnectionLib/src/MultiTrackerManager.cs:78-120`。
- `VisionLiveComparisonThirdPartyTrackerSnapshot` は現在 key / label / receivedAt / timestamp / balls / robots だけを持ち、role / uuid / remote endpoint / aggregate 元の件数を持たない。統合レイヤーを説明可能にするなら、少なくとも source role、source uuid、remote endpoint、aggregate count または representative source metadata を持たせる余地がある。参照: `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs:122-129`。
- 現在の 3rd party snapshot key は `third-party:{sourceLabel}` であり、remote endpoint や uuid を含まない。これは duplicate key の直接原因であり、same-source collapse もこの key だけを見る。参照: `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs:277-286`, `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs:407-410`, `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs:655-657`。
- 設計上は Issue #10 で `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` を fixed source 候補にしており、3rd party tracker は `MultiTrackerManager` から external / source label ごとの latest packet を immutable snapshot 化する方針である。今回の追加はこの境界内で source label aggregate を具体化する設計追補にあたる。参照: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md:187-215`。
- TDD 候補:
  - `VisionLiveComparisonViewStateTests`: 同じ `SourceLabel=ER-FORCE` で remote endpoint が異なる複数 manager state を投入したとき、通常 source option に `ER-FORCE` が 1 つだけ出ること。
  - `VisionLiveComparisonViewStateTests`: source label aggregate の key が安定し、個別 option を残す場合は role / label / uuid / remote endpoint を含む一意 key になること。
  - `VisionLiveComparisonViewStateTests`: aggregate source は同 label 内の最新 `ReceivedAt` の代表 snapshot を使い、balls / robots を union しないこと。
  - `VisionLiveComparisonViewStateTests`: Layer A/B が aggregate `ER-FORCE` を同時選択した場合は same-source collapse で 1 layer 表示になること。
  - `VisionLiveComparisonViewStateTests`: role=own / unknown が `3rd party tracker` 通常候補へ混ざるべきかを仕様化すること。推奨は通常 3rd party option は external 優先、unknown は明示 option、own は `Tracked` と重複しやすいため除外または別扱い。
  - `Home` markup contract test: duplicate label / duplicate key の `<option>` が出ないこと。
- 変更対象になりそうなファイル:
  - `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `Tracker/Tracker.Server/Design/tasks-status.md`
  - `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs`
  - `Tracker/Tracker.Server/Components/Pages/Home.razor`
  - `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
  - 必要に応じて `Tracker/Tracker.Tests/VisionFieldRenderContractTests.cs`
  - PR / manual evidence 対象として `Tracker/Tracker.Server/README.md`

## リスク

- ユーザー確認が必要な仕様は 1 点ある。「統合レイヤー」が同じ source label の代表 snapshot 1 つを表示する意味でよいか、それとも複数 endpoint / uuid の balls / robots を衝突解決しながら merge する意味かを確認したい。推奨は前者で、理由は重複 robot id / ball の誤描画を避け、diagnostics の External / source-label aggregate と同じ「候補集合から代表 snapshot を選ぶ」考え方に揃えられるため。
- 追加確認が必要になり得る点: `External aggregate` を source label aggregate と別に UI へ出すか。複数外部 tracker を横断して 1 layer にする用途があるなら有効だが、ER-FORCE 重複だけを解消するなら最初の必須範囲ではない。
- role の扱いに注意が必要。現在の Vision live は `Trackers.Values` の全 state を拾うため、own / unknown も `ThirdPartyTracker` として option 化され得る。`3rd party tracker` と呼ぶ通常候補は external に限定し、unknown は別 label、own は `Tracked` との重複を避ける方が自然。
- timestamp metadata に注意が必要。aggregate が代表 snapshot を選ぶ場合、details には aggregate label だけでなく representative の `ReceivedAt`、timestamp、remote endpoint / uuid、aggregate count を表示しないと、どの ER-FORCE を見ているか説明できない。
- geometry fallback は既存方針を維持するべき。3rd party aggregate 追加後も field geometry は raw geometry 優先、tracked fallback、3rd party packet から復元しない。参照: `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs:477-485`。
- same-source collapse は key に依存するため、aggregate key と individual key の設計を先に固定する必要がある。duplicate key のままでは UI の選択、same-source collapse、details がすべて曖昧になる。
- 今回は調査のみで、製品コード・テストコード・`Tracker/Tracker.Server/appsettings.json` は編集していない。
