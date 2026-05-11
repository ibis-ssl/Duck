# Sub-agent実行レポート

## タスク

- 目的: TRACKER-037 Tracker 保守性改善後の命名・配置・コメント基準を決め、現状ファイルが同じ基準に従っているか監査する
- タスク種別: investigation

## sub-agentを使う理由

- 理由: ユーザー要望により、親 Codex は manager として判断し、調査・設計判断・実装作業は `gpt-5.5 high` sub-agent に委譲するため。

## 対象範囲

- 対象: TRACKER-033 から TRACKER-035 で作成・分割された Tracker.Core / Tracker.Server / Tracker.CaptureReplay / Tracker.Tests のファイル命名、フォルダ配置、class / property / method / test のコメント付与基準。
- 重点: dot 区切りファイル名を許すケース、フォルダ分割を優先するケース、test の XML コメント化方針、既存コメント有無のばらつき。

## 対象外

- 対象外: 振る舞い変更、tracking algorithm の再設計、UI デザイン変更、PR #6 の範囲外の大規模リファクタ。

## 実行コマンド

- 実行コマンド:
  - `git status --short`
    - 結果: 出力なし。監査開始時点で worktree は clean。
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `sed -n '1,260p' reports/tracker-037-naming-comment-audit-20260511195008.md`
  - `rg --files Tracker/Tracker.Core/Engine Tracker/Tracker.Core/Configuration Tracker/Tracker.Core/Model Tracker/Tracker.Core/Proto Tracker/Tracker.Server/Tracking Tracker/Tracker.Server/Components/Pages Tracker/Tracker.CaptureReplay Tracker/Tracker.Tests/Contracts Tracker/Tracker.Tests --glob '!**/bin/**' --glob '!**/obj/**' | sort`
  - `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md`
  - `sed -n '260,520p' Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md`
  - `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-test-maintainability-detail-design.md`
  - `sed -n '260,520p' Tracker/Tracker.Core/Design/tracker-test-maintainability-detail-design.md`
  - `rg --files ... --glob '!**/bin/**' --glob '!**/obj/**' | awk ...`
    - 目的: source 対象に限定して dot 区切りファイル名を抽出。
  - `find Tracker/Tracker.Tests -path '*/bin/*' -prune -o -path '*/obj/*' -prune -o -name '*.cs' -print | sort | while read -r f; do ...; done`
    - 目的: `[Fact]` / `[Theory]`、`何を確認しているか` 通常コメント、XML コメントの有無を file 単位で確認。
  - `find Tracker/Tracker.Core/Engine Tracker/Tracker.Core/Configuration Tracker/Tracker.Core/Model Tracker/Tracker.Core/Proto Tracker/Tracker.Server/Tracking Tracker/Tracker.Server/Components/Pages Tracker/Tracker.CaptureReplay -path '*/bin/*' -prune -o -path '*/obj/*' -prune -o -name '*.cs' -print | sort | while read -r f; do ...; done`
    - 目的: production 対象の XML コメント、通常コメント、public/internal/protected 宣言数の分布を確認。
  - `nl -ba ... | sed -n ...`
    - 目的: 逸脱箇所の path / line を確認。
  - build / test は実行していない。今回は investigation/audit のみであり、コード・設計書変更を行っていないため。

## 対象ファイル

- 変更または確認したファイル:
  - 変更したファイル:
    - `reports/tracker-037-naming-comment-audit-20260511195008.md`
  - 確認した skill:
    - `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
    - `/home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - 確認した design docs:
    - `Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md`
    - `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
    - `Tracker/Tracker.Core/Design/tracker-test-maintainability-detail-design.md`
  - 確認した production source:
    - `Tracker/Tracker.Core/Engine/*.cs`
    - `Tracker/Tracker.Core/Configuration/*.cs`
    - `Tracker/Tracker.Core/Model/*.cs`
    - `Tracker/Tracker.Core/Proto/*.cs`
    - `Tracker/Tracker.Server/Tracking/*.cs`
    - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
    - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
    - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
    - `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
    - `Tracker/Tracker.Server/Components/Pages/DiagnosticsProfileMetadataLoader.cs`
    - `Tracker/Tracker.Server/Components/Pages/DiagnosticsProfileMetadataView.cs`
    - `Tracker/Tracker.CaptureReplay/*.cs`
  - 確認した test source:
    - `Tracker/Tracker.Tests/Contracts/*.cs`
    - `Tracker/Tracker.Tests/TrackerCoordinator*.cs`
    - `Tracker/Tracker.Tests/TrackerConfigurationBindingTests.cs`
    - `Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs`
    - `Tracker/Tracker.Tests/TrackerProfileControlViewStateTests.cs`
    - `Tracker/Tracker.Tests/TrackerProfileRequestServiceTests.cs`
    - `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
    - `Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`
    - `Tracker/Tracker.Tests/VisionFieldProjectionTests.cs`
    - `Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`
    - `Tracker/Tracker.Tests/VisionPacketStoreTests.cs`
    - `Tracker/Tracker.Tests/VisionReceiverConfigurationResolverTests.cs`
    - `Tracker/Tracker.Tests/VisionReceiverServiceTests.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
  - Blocking: あり。runtime 挙動の blocker ではないが、TRACKER-037 の「命名・配置・コメント基準を確定して TRACKER-033 から 035 の保守性改善を閉じる」観点では blocking。

### ルール案: dot 区切りファイル名

- 許容する dot 区切り:
  - framework / toolchain 慣習のファイル名。
  - 例: `.csproj`, `.sln`, `.razor.cs`, `.razor.css`, `.g.cs`, `.Designer.cs`, `.AssemblyInfo.cs`, generated / build output。
  - 既存対象では `Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj`、`Tracker/Tracker.Tests/Tracker.Tests.csproj`、`Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`、`Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css` は許容。
- 許容しない dot 区切り:
  - 手書き C# の責務 marker としての `Type.Responsibility.cs`。
  - 例: `TrackerEngine.FrameCommit.cs` は framework 慣習ではなく、責務を path で表せるため folder / 通常ファイル名へ寄せる。
- 推奨形:
  - partial aggregate が複数責務へ分かれる場合は type-owned folder を作る。
  - 例: `Tracker/Tracker.Core/Engine/TrackerEngine/FrameCommit.cs`、`Tracker/Tracker.Core/Engine/TrackerEngine/BallTracking.cs`。
  - 同じ理由で `TrackerCoordinatorDiagnostics.cs` など dot なしの partial responsibility file も、長期的には `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs` へ寄せると粒度が揃う。

### ルール案: フォルダ分割とファイル名の粒度

- 1 public / internal top-level type 1 file を基本にする。
- 複数 top-level type を同居させるのは、親子 DTO、密結合した small enum / extension、同じ external schema の一部であり、単独参照されない場合に限る。
- partial class は `TypeName/Responsibility.cs` 形式を基本にし、file 名から型名を重複させない。folder が型名、file が責務名を表す。
- namespace は既存どおり維持し、folder 移動だけで public contract を変えない。
- `Configuration`, `Model`, `Proto` は現状の folder 意味が明確なので維持。ただし 1 file に複数 public DTO が並ぶ場合は、外部設定 schema と 1 対 1 の group として残すか、DTO 単位へ分けるかを design doc に明示する。

### ルール案: production の XML コメント基準

- `public` / `internal` の class / record / interface / enum と、外部設定・DTO・schema・UI state に関わる property / method には日本語 XML コメントを付ける。
- `private` method は原則コメントなしでよい。ただし partial file の入口、flush / profile switch / diagnostics schema / render snapshot / capture schema / Kalman / identity assignment など、順序や不変条件を壊しやすい method は XML コメントにする。
- 通常コメント `//` は method や type の説明には使わず、method 内の複雑な block、不変条件、順序制約の直前にだけ使う。
- 既に method 直前に置かれた通常コメントが method 契約を説明している場合は XML コメントへ置換する。

### ルール案: test の XML コメント基準

- `[Fact]` / `[Theory]` method には日本語 XML コメントで「何を確認しているか」を書く。
  - 推奨形式:
    - `/// <summary>`
    - `/// 何を確認しているか: ...`
    - `/// </summary>`
- test class には、責務別 test group を説明する XML コメントを付ける。特に TRACKER-035 で分割した contract test class は対象。
- test helper class / fixture / support double にも、複数 test から共有されるものは XML コメントを付ける。
- 既存の `// 何を確認しているか:` は test method 冒頭から XML summary へ移す。method 内の補助説明、assertion group の区切り、複数 packet の意味を示す block comment は通常コメントのまま残してよい。
- 現行 `tracker-test-maintainability-detail-design.md` は通常コメントを必須形式としているため、ユーザー要望を反映するには design doc 側も「test は XML コメント」に更新が必要。

### 逸脱箇所: dot 区切りと partial 配置

- `Tracker/Tracker.Core/Engine/TrackerEngine.BallLeftField.cs`
- `Tracker/Tracker.Core/Engine/TrackerEngine.BallTracking.cs`
- `Tracker/Tracker.Core/Engine/TrackerEngine.Contact.cs`
- `Tracker/Tracker.Core/Engine/TrackerEngine.DetectionBuffer.cs`
- `Tracker/Tracker.Core/Engine/TrackerEngine.FrameCommit.cs`
- `Tracker/Tracker.Core/Engine/TrackerEngine.Geometry.cs`
- `Tracker/Tracker.Core/Engine/TrackerEngine.Kalman.cs`
- `Tracker/Tracker.Core/Engine/TrackerEngine.Kick.cs`
- `Tracker/Tracker.Core/Engine/TrackerEngine.RobotTracking.cs`
- `Tracker/Tracker.Core/Engine/TrackerEngine.Settings.cs`
  - 推奨対応: `Tracker/Tracker.Core/Engine/TrackerEngine/` 配下へ移し、`BallLeftField.cs` など dot なし責務名にする。`TrackerEngine.cs` 本体も同 folder に寄せるか、root に残す場合は design doc で例外化する。
- `Tracker/Tracker.Server/Tracking/TrackerCoordinatorDiagnostics.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinatorDispatch.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinatorProfileSwitch.cs`
  - 推奨対応: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs`、`Dispatch.cs`、`ProfileSwitch.cs` へ寄せる。dot 区切りではないが、partial responsibility file の表現方式を Core と揃えるため。

### 逸脱箇所: production コメント

- Core / CaptureReplay / Diagnostics helper は概ね XML コメントが付いている。
- `Tracker/Tracker.Core/Engine/TrackerEngine.Settings.cs`
  - 現状: file-level の partial summary はあるが、settings resolver / unit helper の private method 群はほぼコメントなし。
  - 推奨対応: tiny math helper はコメント不要でよいが、`GetBallTrackMatchDistanceMm`、`GetBallMergeDistanceMm`、`GetBallTrackLifetimeNs`、`PassesOutputVisibility`、`ComputeDecayVisibility`、`ConvertSecondsToNanoseconds` など設定値の意味を合成する境界 method は XML コメントを追加する。
- `Tracker/Tracker.Server/Tracking/ITrackerPacketPublisher.cs:3`
- `Tracker/Tracker.Server/Tracking/TrackedSnapshot.cs:5`
- `Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs:5`
- `Tracker/Tracker.Server/Tracking/TrackerConfigurationResolver.cs:5`
- `Tracker/Tracker.Server/Tracking/TrackerOptions.cs:5`
- `Tracker/Tracker.Server/Tracking/TrackerOptions.cs:28`
- `Tracker/Tracker.Server/Tracking/TrackerOptions.cs:33`
- `Tracker/Tracker.Server/Tracking/TrackerOptions.cs:46`
- `Tracker/Tracker.Server/Tracking/TrackerOptions.cs:53`
- `Tracker/Tracker.Server/Tracking/TrackerPublisherOptions.cs:3`
- `Tracker/Tracker.Server/Tracking/TrackerResolvedOptions.cs:5`
- `Tracker/Tracker.Server/Tracking/TrackerProfileRequestService.cs:6`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs:7`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs:109`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:8`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:258`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:262`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:266`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:272`
- `Tracker/Tracker.Server/Tracking/UdpTrackerPacketPublisher.cs:7`
  - 推奨対応: public surface / external schema / DI entry のため XML コメントを追加する。特に `TrackerOptions` 系は appsettings schema なので property 単位も対象。
- `Tracker/Tracker.Server/Tracking/TrackerCoordinatorDiagnostics.cs:8`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinatorProfileSwitch.cs:239`
  - 現状: method 直前の通常コメントで契約を説明している。
  - 推奨対応: method 契約は XML summary へ移し、method 内の順序補足だけ通常コメントに残す。

### 逸脱箇所: test コメント

- TRACKER-035 で分割された engine contract test は `// 何を確認しているか:` があるが XML コメントではない。
  - `Tracker/Tracker.Tests/Contracts/TrackerEngineBallLeftFieldContractTests.cs`
  - `Tracker/Tracker.Tests/Contracts/TrackerEngineBallTrackingContractTests.cs`
  - `Tracker/Tracker.Tests/Contracts/TrackerEngineBufferingContractTests.cs`
  - `Tracker/Tracker.Tests/Contracts/TrackerEngineGeometryProfileContractTests.cs`
  - `Tracker/Tracker.Tests/Contracts/TrackerEngineKickContactContractTests.cs`
  - `Tracker/Tracker.Tests/Contracts/TrackerEngineRobotTrackingContractTests.cs`
  - 推奨対応: 各 `[Fact]` の直前へ XML summary として移動する。method 内の追加 block comment は必要なら残す。
- TRACKER-035 で分割された coordinator test も同様に通常コメントのみ。
  - `Tracker/Tracker.Tests/TrackerCoordinatorDiagnosticsCaptureTests.cs`
  - `Tracker/Tracker.Tests/TrackerCoordinatorFrameFlowTests.cs`
  - `Tracker/Tracker.Tests/TrackerCoordinatorResetAndProfileTests.cs`
  - 推奨対応: 各 `[Fact]` の直前へ XML summary として移動する。
- TRACKER-035 対象または関連 contract test で「何を確認しているか」コメント自体がない。
  - `Tracker/Tracker.Tests/Contracts/TrackerContractFixtureTests.cs`
  - `Tracker/Tracker.Tests/Contracts/TrackerContractTestDataTests.cs`
  - `Tracker/Tracker.Tests/Contracts/TrackerCoreContractSurfaceTests.cs`
  - `Tracker/Tracker.Tests/Contracts/TrackerCoreReferenceTests.cs`
  - `Tracker/Tracker.Tests/Contracts/TrackerPacketGeneratorContractTests.cs`
  - `Tracker/Tracker.Tests/TrackerConfigurationBindingTests.cs`
  - `Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs`
  - `Tracker/Tracker.Tests/TrackerProfileControlViewStateTests.cs`
  - `Tracker/Tracker.Tests/TrackerProfileRequestServiceTests.cs`
  - `Tracker/Tracker.Tests/VisionFieldProjectionTests.cs`
  - `Tracker/Tracker.Tests/VisionPacketStoreTests.cs`
  - `Tracker/Tracker.Tests/VisionReceiverConfigurationResolverTests.cs`
  - `Tracker/Tracker.Tests/VisionReceiverServiceTests.cs`
  - 推奨対応: TRACKER-035 の対象範囲に含めるなら各 `[Fact]` / `[Theory]` に XML summary を追加する。VisionReceiver 系は TRACKER-035 分割対象外寄りなので後続でもよい。
- `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`、`Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`、`Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`
  - 現状: 通常コメントあり、XML コメントなし。
  - 推奨対応: method summary は XML へ移す。assertion group の通常コメントは block comment として維持してよい。

## 結果

- 結果:
  - 現状は「Core / CaptureReplay は XML コメント中心」「Server Tracking は public surface でも XML コメントなしが多い」「test は通常コメント中心またはコメントなし」が混在しており、ユーザー指摘は妥当。
  - dot 区切りファイル名は framework 慣習に限定し、手書き partial responsibility marker は folder で表すルールにするのが一貫する。
  - 今すぐ修正すべきもの:
    - `TrackerEngine.*.cs` の dot 区切り partial file を `TrackerEngine/` folder へ移す方針を design doc に反映し、実ファイルも移動する。
    - `TrackerCoordinator*` partial file も同じ type-owned folder 方針に合わせるか、今回は Core だけ対象にするなら例外理由を design doc に明記する。
    - `tracker-test-maintainability-detail-design.md` の test コメント基準を通常コメントから XML コメントへ改める。
    - TRACKER-035 分割済み test の `// 何を確認しているか:` を XML summary へ置換する。
    - `Tracker.Server/Tracking` の public options / snapshot / publisher / render snapshot reader/writer に XML コメントを追加する。
  - 後続でよいもの:
    - `VisionReceiver*Tests`、`VisionPacketStoreTests`、`VisionFieldProjectionTests` など TRACKER-035 分割主対象外の test への XML summary 追加。
    - `TrackerEngine.Settings.cs` の tiny helper コメント整理。境界 method だけ優先し、単純数式 helper は無理にコメントしない。
    - `Configuration` / `Model` / `Proto` の複数型同居の粒度再検討。現状は大きな逸脱ではない。
  - 今回は audit のみで、production code、test、design docs は変更していない。

## リスク

- 未解決のリスクまたは後続対応:
  - ファイル移動は namespace を変えなければ挙動リスクは低いが、review diff が大きくなる。dot 命名修正と XML コメント修正を同時に行う場合は、rename-only と comment-only を分けて確認した方がよい。
  - test の通常コメントを XML summary へ置換すると差分量が多い。assertion 変更を混ぜないよう、機械的置換後に `git diff --word-diff` または focused diff で確認する必要がある。
  - `Tracker.Server/Tracking` の public options は appsettings schema と対応するため、コメント追加時に将来仕様や未保証 behavior を書くと design と実装がずれる。
  - `tracker-test-maintainability-detail-design.md` は現時点で通常コメントを必須形式としている。design doc を直さず test だけ XML 化すると、以後の作業者がどちらを正とするか迷う。
