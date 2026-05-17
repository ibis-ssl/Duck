# 作業状況

規則: この文書は `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在の作業

- ID: `CAPTURE-REPLAY-001`
- 題名: Tracker.CaptureReplay に 映像 / `ibis` 自前追跡 遅延分析出力を追加する
- 段階: PR
- 状態: PR #19 公開中
- 規模: 中
- 依存関係: `RUNTIME-HOST-011` 完了.
- 完了条件:
  - `Tracker.CaptureReplay` が 記録保存先 を入力として、未加工 SSL-Vision 入力包 周期 と `ibis` 自前追跡 出力 周期 / 時刻差 を同一出力で比較できる。
  - 出力は今回の 記録 固有ではなく、次回以降の遅延・古い状態・周期 調査に再利用できる 指定名と概要 / 詳細形式にする。
  - 指定 記録 `/home/ibis/ssl/IbisDuck/Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T132706328Z-4a19e61b92c443929f91eccdff3512c9` に対して、`ibis` 自前追跡 が 映像 より遅れて見える原因を 報告に記録する。
  - 対象試験 / `Tracker.CaptureReplay` 構築 / 専用確認 を通し、進捗管理を同期する。
  - 実装証跡:
    - `reports/capture-replay-001-latency-investigation-20260516185833.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~CaptureReplayTests -m:1 /nr:false` は 11 件成功。
    - `dotnet build Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -m:1 /nr:false` は 警告 0 件 / 失敗 0 件。
    - 指定 記録 では通常設定 `ReorderWindowNs=100ms` で `avgCommitLagMs=111.813` / `maxCommitLagMs=117.302`。`--reorder-window-ns 0` の対照実行では `avgCommitLagMs=15.812` / `maxCommitLagMs=20.842` まで下がることを確認した。
  - 確認証跡:
    - `reports/pr19-review-capturereplay-20260516200807.md`
    - `reports/pr19-review-docs-tracking-20260516200807.md`
    - Tracker.CaptureReplay 範囲は 阻害指摘 なし。`ReceivedAt` 基準の 遅延 説明に関する 非阻害 懸念 は `README` / 調査報告文言を修正した。

- ID: `RUNTIME-HOST-012`
- 題名: Tracker.RuntimeHost 起動時に CLI 引数で 有効設定 を指定できるようにする
- 段階: PR
- 状態: PR #19 公開中
- 規模: 小
- 依存関係: `RUNTIME-HOST-011` 完了.
- 完了条件:
  - `Tracker.RuntimeHost` が `--profile <name>` と `--profile=<name>` を受け取り、`Tracker:ActiveProfileName` より優先して 有効設定 を選択できる。
  - 設定 引数が未指定の場合は既存 設定文書の `Tracker:ActiveProfileName` 挙動を維持する。
  - 不正な `--profile` 指定は起動時に明示失敗する。
  - 対象試験 / `Tracker.RuntimeHost` 構築 / 専用確認 を通し、進捗管理を同期する。
  - 実装証跡:
    - `reports/runtime-host-012-cli-profile-20260516195943.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostOperationLoopTests|FullyQualifiedName~RuntimeHostScaffoldContractTests" -m:1 /nr:false` は 確認指摘 修正後 17 件成功。
    - `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false` は 警告 0 件 / 失敗 0 件。
    - 命令行解析は `Microsoft.Extensions.Configuration.CommandLine` 提供機能 と 切り替え対応表 で実装した。
    - `--profile` / `--profile=` の値なし指定は 取り込み済み 設定文書 を読み込む実起動経路でも `ArgumentException` で即終了することを確認した。
  - 確認証跡:
    - `reports/pr19-review-runtimehost-profile-20260516200807.md`
    - `reports/pr19-review-runtimehost-profile-r2-20260516201757.md`
    - 初回 確認 の 重大指摘 は修正済み。r2 確認 で指摘なし、門を閉じる 可を確認した。

- ID: `DOC-LINT-001`
- 題名: 文書検査 / 綴り検査 を導入し、英単語と片仮名語を意味付き 許可一覧で管理する
- 段階: 文書検査整備
- 状態: 確認完了、PR 待ち
- 規模: 小
- 依存関係: なし
- 完了条件:
  - 保存庫根 から 文書検査 を実行できる `npm` 実行名 を追加する。
  - `textlint` が利用者編集対象の `*.md` 全般に実行され、文書の表記揺れや文章規則を検出できる。
  - `cspell` が利用者編集対象の `*.md` 全般に実行され、英単語は専用 許可一覧 に登録されていない場合に失敗する。
  - 独自 許可一覧 検査 が、利用者編集対象の `*.md` 全般と 許可一覧 説明文に含まれる英単語と片仮名語を検査し、専用 許可一覧 に登録されていない場合に失敗する。
  - 許可一覧 は既存 文書 脚注から初版を収集した専用 `YAML` 1 文書を正本とし、単語名と説明の対を残す。
  - 生成物 / 外部取り込み物 / 構築出力 など利用者編集対象ではない 文書 は明示的に除外する。
  - 文書検査 導入意図、環境構築、実行方法、専用 許可一覧 更新手順、対象と除外を 保存庫 内の 文書 覚書に残す。
  - 委任実行 による 実装 / 検証 / 専用確認 を通し、進捗管理を同期する。
- 実装証跡:
  - `reports/doc-lint-001-implementation-20260516221628.md`
- 確認証跡:
  - `reports/doc-lint-001-review-20260516233048.md`
  - 全範囲 文書検査 は、既存 文書の未登録英単語と片仮名語を大量 許可一覧で勝手に通さないため意図的に 失敗状態のまま。専用 許可一覧 の内容変更は利用者明示確認必須。

## 完了済み作業

- `RUNTIME-HOST-001`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了した。`Tracker/Design/` へ設計資料と 有効 進捗管理を統合し、`Tracker.RuntimeHost` / `Tracker.DebugHost` の命名、責務境界、AutoRef 将来内包、実行周回分離、旧記録互換非要件、`BreakingChanges` 不要を設計へ固定した。高精度確認 は初回 阻害 2 件を修正し、r2 で 指摘なしを確認した。下書き PR #17 を作成した。
  - 確認証跡:
    - `reports/runtime-host-001-design-review-20260514155548.md`
    - `reports/runtime-host-001-design-fix-20260514160144.md`
    - `reports/runtime-host-001-design-review-r2-20260514160734.md`
- `RUNTIME-HOST-002`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 依存境界契約 を追加した。`Tracker.RuntimeHost` が `Tracker.DebugHost` / `Tracker.Server` / `Web UI` / 診断再生 UI 計画 を参照しないこと、Tracker.RuntimeHost のソースコードが 診断記録 / 再生 / Blazor UI 名前空間 を直接参照しないこと、Tracker.DebugHost が 読み取り側 実行体 であることを 失敗先行 契約 として固定した。
  - 実装証跡:
    - `reports/runtime-host-002-implementation-20260514163841.md`
    - `reports/runtime-host-002-boundary-context-20260514164124.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests -m:1 /nr:false` は 3 件失敗 / 0 件成功。現時点では `Tracker.RuntimeHost` 計画とソースコードの根 と `Tracker.DebugHost` 根 が未存在のため、意図した 失敗先行 契約 として 検証失敗 になっている。
  - 確認証跡:
    - `reports/runtime-host-002-review-20260514164528.md`
    - `reports/runtime-host-002-review-fix-20260514164850.md`
    - `reports/runtime-host-002-review-r2-20260514165133.md`
    - r2 確認 で 阻害指摘 なし。Tracker.DebugHost 実行周回所有目印 の将来 誤検出 可能性は 保留 として記録した。
- `RUNTIME-HOST-003`: 診断標本 境界 と 旧形式縮退 契約 を追加した。診断標本 時点 が 自前追跡 確定枠 周期 / `WorldFrameCommitted` に依存しないこと、診断 `Vision Input` が 診断標本補助記録 から復元されること、旧 描画 時点記録 補助記録 が 非対応 / 縮退 旧形式 であることを 失敗先行 契約 として固定し、設計文書の 作業参照 を固定一覧へ同期した。
  - 実装証跡:
    - `reports/runtime-host-003-implementation-20260514165750.md`
    - `reports/runtime-host-003-boundary-context-20260514165750.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDiagnosticsSampleBoundaryContractTests -m:1 /nr:false` は 3 件失敗 / 0 件成功。`RuntimeHostDiagnosticsSampleBoundaryContractTests` は 構築済み 済みで、診断標本補助記録 未実装と 旧形式縮退 表示未実装を 検証失敗 として固定している。
  - 確認証跡:
    - `reports/runtime-host-003-review-20260514170652.md`
    - 確認 で 阻害指摘 なし。診断標本補助記録 構造 と 未加工 映像 内容 DTO の詳細は `RUNTIME-HOST-007` の 成功 実装側で確認する 保留 として記録した。
- `RUNTIME-HOST-004`: `Tracker.Server` を `Tracker.DebugHost` 計画 / 名前空間 / 起動経路へ 改名 した。有効 計画、名前空間、起動経路、`README`、構成 / 計画 参照、Tracker.CaptureReplay / 試験 の参照を `Tracker.DebugHost` へ揃え、既存診断正常系 を維持した。
  - 実装証跡:
    - `reports/runtime-host-004-implementation-20260514171550.md`
    - `reports/runtime-host-004-rename-impact-20260514171550.md`
    - `reports/runtime-host-004-verification-20260514172634.md`
    - 失敗先行: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDebugHostRenameContractTests -m:1 /nr:false` は 3 件失敗 / 0 件成功。`Tracker.DebugHost` 保管場所/計画 未存在と 有効 参照 未更新を 検証失敗 として確認した。
    - 成功: 同 対象試験 は 3 件成功。`dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false`、`dotnet build Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -m:1 /nr:false`、`dotnet build Duck.slnx -m:1 /nr:false` は成功した。
  - 確認証跡:
    - `reports/runtime-host-004-review-20260514172921.md`
    - 確認 で 阻害指摘 なし。全体 `Tracker.Tests` は `RUNTIME-HOST-002` / `RUNTIME-HOST-003` の既存 失敗先行 契約 があるため未実行とした。
- `RUNTIME-HOST-005`: 自前追跡 実行周回 の共有 実行時 境界 を `Tracker.Core/Runtime` へ抽出した。`TrackerCoordinator`、`ITrackerPacketPublisher`、`TrackerPublisherOptions`、`TrackedSnapshot`、`TrackedSnapshotStore`、`UdpTrackerPacketPublisher` を UI 非依存 `Core` 実行時 境界へ寄せ、Tracker.DebugHost は UDP 復号 / 未加工 保存 / 記録 後に `Core` 調整役 を呼ぶ 適合層 とした。旧 診断記録 / 描画 時点記録 補助記録 生成は `Core` 実行周回 から外し、性能 優先と Tracker.RuntimeHost 再利用境界を固定した。
  - 実装証跡:
    - `reports/runtime-host-005-implementation-20260514180031.md`
    - `reports/runtime-host-005-verification-20260514180308.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostSharedOperationLoopBoundaryTests|FullyQualifiedName~TrackerCoordinatorFrameFlowTests|FullyQualifiedName~TrackerCoordinatorResetAndProfileTests|FullyQualifiedName~TrackerProfileRequestServiceTests|FullyQualifiedName~TrackerCoordinatorDiagnosticsCaptureTests" -m:1 /nr:false` は 15 件成功。
    - `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false` は成功し、`git diff --check` も成功した。
  - 確認証跡:
    - `reports/runtime-host-005-review-20260514180308.md`
    - 確認 で 阻害指摘 なし。Tracker.DebugHost 読み取り側 UI 化、診断標本補助記録、Tracker.RuntimeHost 骨組み は `RUNTIME-HOST-006` 以降へ残す。
- `RUNTIME-HOST-006`: Tracker.DebugHost 実時表示 を 読み取り側 時点記録 境界へ寄せた。`VisionLiveDisplaySnapshotProvider` が 1 描画 時点 で 未加工 / 追跡済み / 外部 自前追跡 時点記録 を固定し、`Home.razor` は 未加工 / 追跡済み 保存 を直接 注入 せず同一 合成 時点記録 から 未加工 / Tracked / 比較 を派生する。`ExternalTrackerSnapshotStore` は `MultiTrackerManager` 更新通知 から 入力包 / 付随情報 を 複製 済み DTO として保持し、描画経路 が 可変管理状態 を直接読まない構造にした。
  - 実装証跡:
    - `reports/runtime-host-006-boundary-context-20260514181333.md`
    - `reports/runtime-host-006-implementation-20260514182342.md`
    - `reports/runtime-host-006-verification-20260514182549.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDebugHostReadSideSnapshotBoundaryTests|FullyQualifiedName~VisionLiveComparisonViewStateTests|FullyQualifiedName~TrackedVisionViewStateTests" -m:1 /nr:false` は 18 件成功。
    - `dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false` と `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false` は成功し、`git diff --check` も成功した。
  - 確認証跡:
    - `reports/runtime-host-006-review-20260514182549.md`
    - 確認 で 阻害指摘 なし。診断標本補助記録 と Tracker.RuntimeHost 骨組み は `RUNTIME-HOST-007` 以降へ残す。
- `RUNTIME-HOST-007`: Tracker.DebugHost 診断標本補助記録 高速経路 を実装した。UI 非依存 `DiagnosticsSampleHostedService` が設定値 `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` に従って 最新 未加工 / 自前追跡 時点記録 を `diagnostics-samples.jsonl` へ保存し、診断 再生 / `Field` は 標本補助記録 の 上限付き探索 と 意味 概要 を主経路にする。旧 描画 時点記録 補助記録 だけの 記録 は 非対応 / 縮退 旧形式 として扱い、高負荷な互換 経路 は復活させない。
  - 実装証跡:
    - `reports/runtime-host-007-implementation-20260514184219.md`
    - `reports/runtime-host-007-review-fix-20260514185807.md`
    - `reports/runtime-host-007-configurable-sample-interval-20260514191628.md`
    - `reports/runtime-host-007-verification-20260514184527.md`
    - 対象 / 影響範囲 試験、`Tracker.DebugHost` 構築、`Tracker.Tests` 構築、`git diff --check` は 委任実行 報告 で成功を確認した。
  - 確認証跡:
    - `reports/runtime-host-007-review-20260514184501.md`
    - `reports/runtime-host-007-review-r2-20260514190459.md`
    - `reports/runtime-host-007-review-r3-20260514191820.md`
    - `reports/runtime-host-007-review-r4-20260514192425.md`
    - 初回 確認 の 阻害 2 件を修正し、r2 / r3 / r4 で 指摘なしを確認した。
- `RUNTIME-HOST-008`: `Tracker.RuntimeHost` 画面なし 計画 骨組み と 設定 を追加した。`Tracker.RuntimeHost` 計画、`Program`、選択肢 / `DI` 起動処理、構成 項目 を追加し、`Web UI` / 診断再生 / 記録 確認画面 を持たない 画面なし 実行体 として起動できる 骨組み を作った。`RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開し、0 以下は 実行体 起動 検証 失敗 になる 契約 を追加した。
  - 実装証跡:
    - `reports/runtime-host-008-implementation-20260514192917.md`
    - 調整後 R008 対象 は 7 件成功。広めの 対象 は 23 件成功 / 1 件失敗 で、失敗は R008 範囲外の既存 Tracker.DebugHost 実行周回 所有 検証 として 確認 で確認した。
    - `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`、`dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`、`git diff --check` は 委任実行 報告 で成功を確認した。
  - 確認証跡:
    - `reports/runtime-host-008-review-20260514193633.md`
    - `reports/runtime-host-008-review-fix-20260514194021.md`
    - `reports/runtime-host-008-review-r2-20260514194042.md`
    - 初回 確認 の `XML` 概要 阻害 を修正し、r2 で 指摘なしを確認した。
- `RUNTIME-HOST-009`: Tracker.RuntimeHost 自前追跡 実行周回 と 公式 入力包 送信 正常系 を実装した。Tracker.RuntimeHost は 画面なし SSL-Vision 受信部、最新 入力包 保管領域、`RuntimeHost:OperationLoopIntervalMilliseconds` に従う 実行周回、`Core` `TrackerCoordinator` / `TrackedSnapshotStore` / `UdpTrackerPacketPublisher` を `DI` で組み立て、擬似 SSL-Vision 入力 が 調整役 / 送信部 / 最新 時点記録 保存 へ届く 正常系 を固定した。欠落 有効設定 は Tracker.DebugHost と同じく明示失敗に揃えた。
  - 実装証跡:
    - `reports/runtime-host-009-implementation-20260514194405.md`
    - `reports/runtime-host-009-review-fix-20260514200105.md`
    - R009 対象 は 3 件成功、確認-修正 後 `RuntimeHostOperationLoopTests` は 5 件成功。広めの 対象 は 26 件成功 / 1 件失敗 で、失敗は R009 範囲外の既存 Tracker.DebugHost 所有 検証 として 確認 で確認した。調整後 対象 は 26 件成功。
    - `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`、`dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`、`git diff --check` は 委任実行 報告 で成功を確認した。
  - 確認証跡:
    - `reports/runtime-host-009-review-20260514195653.md`
    - `reports/runtime-host-009-review-r2-20260514200945.md`
    - 初回 確認 の 欠落 有効設定 代替 阻害 を修正し、r2 で 指摘なしを確認した。最新 入力包 保管領域 は 最新 優先のまま R010 手動証跡 後判断の 保留 とした。
- `RUNTIME-HOST-010`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離 の 対象 検証 と 手動証跡 を揃えた。`Tracker.RuntimeHost` / `Tracker.DebugHost` の 対象試験 と 構築、診断標本 証跡、旧形式縮退 証跡、Tracker.DebugHost UI 正常系、Tracker.RuntimeHost 画面なし 正常系 を 委任実行 報告に残し、`.gitignore` に 実行時 / 診断 記録 生成物 を追加して手元生成物が通常差分へ混入しないことを確認した。
  - 検証証跡:
    - `reports/runtime-host-010-validation-20260514201701.md`
    - Tracker.RuntimeHost 対象試験 は 10 件成功、調整後 境界 対象 は 10 件成功、診断 対象 は 15 件成功。
    - `Tracker.RuntimeHost` / `Tracker.DebugHost` / `Tracker.Tests` 構築、Tracker.RuntimeHost 短時間 画面なし 起動、Tracker.DebugHost HTTP 200 起動確認、`git diff --check`、`.gitignore` 生成物除外 確認は 委任実行 報告 で成功を確認した。
    - 広めの 対象 は既知の `RuntimeHostDependencyBoundaryContractTests.DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop` 1 件のみ 失敗。現設計では Tracker.DebugHost の `Core` 実行周回 適合層 残存を許容しているため、R010 阻害 ではなく R011 最終 確認 で扱う 保留 とした。
  - 確認証跡:
    - `reports/runtime-host-010-review-20260514202428.md`
    - 確認 で 阻害指摘 なし。既知 Tracker.DebugHost 所有 検証失敗 は 保留 継続が妥当と確認した。
- `RUNTIME-HOST-011`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離 の 最終 確認 / 進捗管理 同期 / PR 提出可能 を完了した。最終 確認 で 取り込み済み 失敗先行 契約 が 阻害 と判定されたため、`RuntimeHostDependencyBoundaryContractTests` を現設計に合わせ、Tracker.DebugHost 全体の `Core` 実行周回 適合層 を禁止する 契約 から UI / 診断再生 / 描画ソースコード が 実行周回 を直接駆動しない 契約 へ狭めた。r2 確認 で 阻害指摘 なし、PR 提出可能 可を確認した。
  - 確認証跡:
    - `reports/runtime-host-011-final-review-20260514203109.md`
    - `reports/runtime-host-011-review-fix-20260514203809.md`
    - `reports/runtime-host-011-final-review-r2-20260514204526.md`
    - 初回 最終 確認 は 取り込み済み 失敗先行 契約 を 阻害 と判定した。確認-修正 後、`RuntimeHostDependencyBoundaryContractTests` は 3 件成功、分離/境界 対象 は 11 件成功、`Tracker.Tests` 構築 と `git diff --check` は 成功。r2 確認 で 指摘なし / PR 提出可能 可を確認した。

## 固定残作業

- 固定一覧は `RUNTIME-HOST-001` から `RUNTIME-HOST-011` とする。`Tracker.RuntimeHost` / `Tracker.DebugHost` 分離 範囲 では `RAW-VISION-*` や `TRACKER-*` を追加しない。
- `RUNTIME-HOST-001`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する。設計資料を `Tracker/Design/` 配下へ移動し、有効 進捗管理を統合し、`Tracker.RuntimeHost` / `Tracker.DebugHost` の責務境界、AutoRef 将来内包、実行周回分離、旧記録互換非要件を設計へ反映する。
- `RUNTIME-HOST-002`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 依存境界契約 を追加する。Tracker.RuntimeHost が Tracker.DebugHost / `Web UI` / 診断再生 UI に依存しないこと、Tracker.DebugHost が 自前追跡 実行周回 の主責務を持たず 読み取り側 であることを 失敗先行 試験 として固定する。
- `RUNTIME-HOST-003`: 診断標本 境界 と 旧形式縮退 契約 を追加する。診断標本 時点 が 自前追跡 確定枠 周期 に依存しないこと、診断 `Vision Input` が 診断標本補助記録 から復元されること、旧 描画 時点記録 補助記録 が 非対応 / 縮退 旧形式 であることを 失敗先行 試験 として固定する。
- `RUNTIME-HOST-004`: `Tracker.Server` を `Tracker.DebugHost` 計画 / 名前空間 / 起動経路へ 改名 する。現 `Tracker.Server` の `Web UI` / 診断 / 再生 / 記録 確認画面 責務を Tracker.DebugHost として明確化し、既存 診断正常系 を壊さない。
- `RUNTIME-HOST-005`: 自前追跡 実行周回 の共有 実行時 境界 を抽出する。SSL-Vision 入力、自前追跡 更新、公式 自前追跡 入力包 送信、最新 自前追跡 時点記録 公開の境界を UI / 診断記録 から分離し、Tracker.RuntimeHost から再利用できる形にする。
- `RUNTIME-HOST-006`: Tracker.DebugHost 実時表示 を 読み取り側 時点記録 境界へ寄せる。UI 描画 時点 ごとに 未加工 / 追跡済み / 外部 自前追跡 の 最新 不変 時点記録 を固定し、`Web` 描画 時点 が 自前追跡 実行周回 を駆動しない構造にする。
- `RUNTIME-HOST-007`: Tracker.DebugHost 診断標本補助記録 高速経路 を実装する。診断標本 時点 で 最新 未加工 時点記録 と 最新 自前追跡 時点記録 を固定して保存し、新規 記録 / 記録 の 上限付き探索 を主経路にする。
- `RUNTIME-HOST-008`: `Tracker.RuntimeHost` 画面なし 計画 骨組み と 設定 を追加する。`Web UI` / 診断再生 / 記録 確認画面 を持たない 画面なし 実行体 として起動できる 計画 / `Program` / 選択肢 / `DI` 起動処理 / 構成 項目 を追加し、`RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開する。
- `RUNTIME-HOST-009`: Tracker.RuntimeHost 自前追跡 実行周回 と 公式 入力包 送信 正常系 を実装する。SSL-Vision 入力、自前追跡 状態 更新、公式 自前追跡 入力包 送信、Tracker.DebugHost が読める 最新 自前追跡 時点記録 公開を 画面なし 実行体 の正常系として成立させ、Tracker.RuntimeHost 実行周期を `RuntimeHost:OperationLoopIntervalMilliseconds` で制御する。
- `RUNTIME-HOST-010`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離 の 対象 検証 と 手動証跡 を揃える。`Tracker.RuntimeHost` / `Tracker.DebugHost` 構築、対象試験、診断標本 証跡、旧形式縮退 証跡、Tracker.DebugHost UI 正常系、Tracker.RuntimeHost 画面なし 正常系 の証跡を 報告に残す。
- `RUNTIME-HOST-011`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離 の 最終 確認 / 進捗管理 同期 / PR 提出可能 を完了する。高精度確認、必要な修正と r2、進捗管理 同期、報告 参照、検証 証跡、下書き PR #17 提出可能 化を完了する。

## 統合済み履歴

- `Core` / 自前追跡 処理系 系の旧 進捗管理 は `Tracker/Design/Archive/Core/tasks-status.md` と `Tracker/Design/Archive/Core/phases-status.md` に保存する。
- Tracker.DebugHost / 未加工 映像 / 診断 系の旧 進捗管理 は `Tracker/Design/Archive/DebugHost/tasks-status.md` と `Tracker/Design/Archive/DebugHost/phases-status.md` に保存する。
- 旧 `RAW-VISION-013` から `RAW-VISION-016` は PR #15 `Issue #10 Vision画面に分割表示とオーバーレイを追加する` として `2026-05-14T03:29:25Z` に 統合 済み。
- `RAW-VISION-017` として開始した 実行周回分離 設計は、`Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針へ 範囲 を拡張したため、以後は `RUNTIME-HOST-001` へ統合する。

## 作業一覧

| ID | 作業 | 段階 | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| `RUNTIME-HOST-001` | `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する | 設計 | 完了、下書き PR #17 | PR #15 統合 完了 | `Tracker/Design/` へ設計資料と 有効 進捗管理を統合し、`Tracker.RuntimeHost` / `Tracker.DebugHost` の命名、責務境界、AutoRef 将来内包、実行周回分離、旧記録互換非要件、`BreakingChanges` 不要を設計へ固定し、高精度確認 r2 確認 で 阻害指摘 なしを確認した。 |
| `RUNTIME-HOST-002` | `Tracker.RuntimeHost` / `Tracker.DebugHost` 依存境界契約 を追加する | 検証 | 完了、下書き PR #17 | `RUNTIME-HOST-001` | Tracker.RuntimeHost が Tracker.DebugHost / `Web UI` / 診断再生 UI に依存しないこと、Tracker.DebugHost が 自前追跡 実行周回 の主責務を持たず 読み取り側 であることを 失敗先行試験 として固定し、r2 確認 で 阻害指摘 なしを確認した。 |
| `RUNTIME-HOST-003` | 診断標本 境界 と 旧形式縮退 契約 を追加する | 検証 | 完了、下書き PR #17 | `RUNTIME-HOST-002` | 診断標本 時点 が 自前追跡 確定枠 周期 / `WorldFrameCommitted` に依存しないこと、診断 `Vision Input` が 診断標本補助記録 から復元されること、旧 描画 時点記録 補助記録 が 非対応 / 縮退 旧形式 であることを 失敗先行 契約 として固定し、確認 で 阻害指摘 なしを確認した。 |
| `RUNTIME-HOST-004` | `Tracker.Server` を `Tracker.DebugHost` 計画 / 名前空間 / 起動経路へ 改名 する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-003` | 現 `Tracker.Server` の `Web UI` / 診断 / 再生 / 記録 確認画面 責務を `Tracker.DebugHost` として明確化し、既存 診断正常系、`README`、起動設定、構成 / 計画 参照 を維持し、確認 で 阻害指摘 なしを確認した。 |
| `RUNTIME-HOST-005` | 自前追跡 実行周回 の共有 実行時 境界 を抽出する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-004` | `Tracker.Core/Runtime` に UI 非依存 共有 実行周回、送信部、最新 時点記録 保存 を抽出し、Tracker.DebugHost を `Core` 調整役 呼び出し 適合層 に寄せた。対象試験 / 構築 / 確認 で 阻害指摘 なしを確認した。 |
| `RUNTIME-HOST-006` | Tracker.DebugHost 実時表示 を 読み取り側 時点記録 境界へ寄せる | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-005` | `VisionLiveDisplaySnapshotProvider` と `ExternalTrackerSnapshotStore` により Tracker.DebugHost 実時表示 が UI 描画 時点 ごとに 最新 不変 時点記録 を固定し、`Web` 描画 時点 が 自前追跡 実行周回 を駆動しないことを 対象試験 / 構築 / 確認 で確認した。 |
| `RUNTIME-HOST-007` | Tracker.DebugHost 診断標本補助記録 高速経路 を実装する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-003`, `RUNTIME-HOST-006` | UI 非依存 `DiagnosticsSampleHostedService` が設定値 `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` に従って 最新 未加工 / 自前追跡 時点記録 を `diagnostics-samples.jsonl` へ保存し、診断 再生 / `Field` は 標本補助記録 の 上限付き探索 と 意味 概要 を主経路にする。対象 / 影響範囲 試験、構築、差分検査 は 委任実行 報告 で 成功。初回 確認 は 阻害 2 件、確認-修正 後 r2 は 成功、設定化後 r3 は 指摘なし、Tracker.RuntimeHost 実行周期設定化要件追加後 r4 は 指摘なし。 |
| `RUNTIME-HOST-008` | `Tracker.RuntimeHost` 画面なし 計画 骨組み と 設定 を追加する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-005` | `Web UI` / 診断再生 / 記録 確認画面 を持たない `Tracker.RuntimeHost` 計画、`Program` / 選択肢 / `DI` 起動処理 / 構成 項目 を追加し、自前追跡 のみ と将来 自前追跡 + AutoRef mode の境界を表現する。`RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開し、0 以下は起動時 検証 失敗 とする 契約 を 対象試験 / 構築 / 確認 / 履歴登録 / 下書き PR #17 更新 付きで固定した。 |
| `RUNTIME-HOST-009` | Tracker.RuntimeHost 自前追跡 実行周回 と 公式 入力包 送信 正常系 を実装する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-007`, `RUNTIME-HOST-008` | Tracker.RuntimeHost が SSL-Vision 入力 を受け、`RuntimeHost:OperationLoopIntervalMilliseconds` で制御される実行周期に従って 自前追跡 状態 を更新し、公式 自前追跡 入力包 を 送信 し、Tracker.DebugHost が読める 最新 自前追跡 時点記録 を公開する正常系を 対象試験 / 構築 / 確認 / 履歴登録 / 下書き PR #17 更新 付きで成立させた。 |
| `RUNTIME-HOST-010` | `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離 の 対象 検証 と 手動証跡 を揃える | 確認 | 完了、下書き PR #17 | `RUNTIME-HOST-009` | `Tracker.RuntimeHost` / `Tracker.DebugHost` の 対象試験 と 構築、診断標本 証跡、旧形式縮退 証跡、Tracker.DebugHost UI 正常系、Tracker.RuntimeHost 画面なし 正常系 の証跡を 報告に残し、作業 確認 で 指摘なしを確認した。 |
| `RUNTIME-HOST-011` | `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離 の 最終 確認 / 進捗管理 同期 / PR 提出可能 を完了する | 確認 | 完了、PR #17 提出可能 | `RUNTIME-HOST-010` | 最終 確認、阻害 修正、r2 確認、進捗管理 同期、報告 参照、検証 証跡、履歴登録 履歴、下書き PR #17 説明 最新化、PR 提出可能 判断を完了した。 |
