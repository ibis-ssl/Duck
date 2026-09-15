# 作業状況

この文書は過去の作業記録の要約です。要約前の条件・検証結果・参照先は[変更前の原文](../../../../reports/history/core-tasks-before-terminology.md)に省略せず保存しています。原文の基準は `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4` です。現在の作業状態は[現行の作業一覧](../../tasks-status.md)を参照してください。

規則: この文書は `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在の作業

- ID: なし
- 表題: なし
- 工程: 比較記録
- 状態: なし
- TDD 記録: なし
- 設計記録: なし
- 実装記録: なし
- 確認記録: なし
- 規模: なし
- 依存関係: なし
- 完了条件: なし

## 次の調査作業

- なし。

## 固定残作業

- 固定一覧は `TRACKER-047`、`TRACKER-048`、`TRACKER-049`、`TRACKER-050`、`TRACKER-051`、`TRACKER-052`、`TRACKER-054`、`TRACKER-055`、`TRACKER-056`、`TRACKER-057`、`TRACKER-053`、`TRACKER-058`、`TRACKER-059`、`TRACKER-060`、`TRACKER-061`、`TRACKER-062`、`TRACKER-063` とする。
- `TRACKER-047`: tracker snapshotの再生読み取り確認条件を閉じた。
- `TRACKER-048`: 診断、再生、出力の比較表示へ接続した。
- `TRACKER-049`: 診断比較の設計と進捗管理を再同期した。
- `TRACKER-050`: 診断比較の読み取りと表示状態契約を追加した。
- `TRACKER-051`: `/diagnostics` へ比較表示と表示元絞り込みを接続した。
- `TRACKER-052`: 比較記録の運用文書と手動証跡項目を更新した。
- `TRACKER-054`: 実行中追跡器受信の接続先上書きを追加した。
- `TRACKER-055`: 診断再生と位置移動の低速問題を解消した。
- `TRACKER-056`: 診断フィールドの左右表示元切替と比較欄折り畳みを追加した。
- `TRACKER-057`: 診断フィールドの重ね合わせ表示を追加した。
- `TRACKER-053`: PR #9 の準備完了材料を整理した。
- `TRACKER-058`: `ER-Force` tracker snapshotがフィールド表示へ戻らない問題を修正した。
- `TRACKER-059`: 診断の replay timeline を最速追跡器表示元の周期へ合わせた。
- `TRACKER-060`: 等倍速再生を 30fps 相当の実時間追従へ変更した。
- `TRACKER-061`: 等倍速と倍率の表示を分離した。
- `TRACKER-062`: 再生操作配置を戻し、速度選択へ等倍速を追加した。
- `TRACKER-063`: 再生開始時の選択速度維持と可変早送り倍率を追加した。

## 作業一覧

| ID | 作業 | 工程 | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| `TRACKER-039` | 青1番が11番へ化ける追跡不具合を調査して修正する | 調査 | 完了 | `TRACKER-038` | 原因調査、再発防止試験、修正、確認、PR #8 統合まで完了。 |
| `TRACKER-040` | 比較記録拡張の設計と進捗管理を追加する | 比較記録 | 完了 | `TRACKER-039` | 設計、進捗管理、下書き PR #9、確認まで完了。 |
| `TRACKER-041` | 全追跡器通信内容保存方針へ設計と進捗管理を修正する | 比較記録 | 完了 | `TRACKER-040` | 保存対象方針を修正し、確認まで完了。 |
| `TRACKER-042` | 全追跡器保存契約を実装する | 比較記録 | 完了 | `TRACKER-041` | 契約試験、実装、検証、確認まで完了。 |
| `TRACKER-043` | `CaptureOn` 記録先と付帯情報相対経路を追加する | 比較記録 | 完了 | `TRACKER-042` | 記録先、付帯情報、読み取り経路、検証、確認まで完了。 |
| `TRACKER-044` | `CaptureOn` 中に全追跡器通信内容を保存する | 比較記録 | 完了 | `TRACKER-043` | 保存、要約、集計、検証、確認まで完了。 |
| `TRACKER-045` | 実行中外部追跡器受信を書き込みへ接続する | 比較記録 | 完了 | `TRACKER-044` | 書き込み接続、検証、確認まで完了。 |
| `TRACKER-046` | 実行中追跡器受信器の起動登録を完了する | 比較記録 | 完了 | `TRACKER-045` | 起動登録、多宛先通信確認修正、検証、再確認まで完了。 |
| `TRACKER-047` | tracker snapshot 再生読み取り確認条件を閉じる | 比較記録 | 完了 | `TRACKER-046` | 読み取り実装、確認修正、再確認まで完了。 |
| `TRACKER-048` | 診断、再生、出力の比較表示へ接続する | 比較記録 | 完了 | `TRACKER-047` | 比較出力、既存表示維持、検証、確認まで完了。 |
| `TRACKER-049` | 診断比較の設計と進捗管理を再同期する | 比較記録 | 完了 | `TRACKER-048` | 固定作業再定義、設計同期、確認まで完了。 |
| `TRACKER-050` | 診断比較読み取りと表示状態契約を追加する | 比較記録 | 完了 | `TRACKER-049` | 表示状態契約、選択ずれ修正、検証、再確認まで完了。 |
| `TRACKER-051` | `/diagnostics` へ比較表示と表示元絞り込みを接続する | 比較記録 | 完了 | `TRACKER-050` | 画面接続、表示項目追加、検証、再確認まで完了。 |
| `TRACKER-052` | 比較記録の運用文書と手動証跡項目を更新する | 比較記録 | 完了 | `TRACKER-051` | 通常確認経路と手動証跡項目の文書更新、確認まで完了。 |
| `TRACKER-054` | 実行中追跡器受信の接続先上書きを追加する | 比較記録 | 完了 | `TRACKER-052` | 設定追加、文書更新、検証、確認まで完了。 |
| `TRACKER-055` | 診断再生と位置移動の低速問題を解消する | 比較記録 | 完了 | `TRACKER-054` | 軽量索引、速度選択、同時刻優先修正、検証、再確認まで完了。 |
| `TRACKER-056` | 診断フィールドの左右表示元切替と比較欄折り畳みを追加する | 比較記録 | 完了 | `TRACKER-055` | 表示元切替、折り畳み、検証、確認まで完了。 |
| `TRACKER-057` | 診断フィールドの重ね合わせ表示を追加する | 比較記録 | 完了 | `TRACKER-056` | 重ね合わせ、同一表示元修正、検証、再確認まで完了。 |
| `TRACKER-053` | PR #9 を準備完了化する | 比較記録 | 完了 | `TRACKER-057` | PR 本文案、最終検証、確認証跡、危険整理、再確認まで完了。 |
| `TRACKER-058` | `ER-Force` 再生不具合を調査して修正する | 比較記録 | 完了 | `TRACKER-053` | 保存時対応付け追加、検証、確認まで完了。 |
| `TRACKER-059` | 診断の replay timeline を最速追跡器表示元周期に合わせる | 比較記録 | 完了 | `TRACKER-058` | 高速表示元周期接続、検証、再確認まで完了。 |
| `TRACKER-060` | 診断等倍速再生を実時間追従にする | 比較記録 | 完了 | `TRACKER-059` | 30fps 相当の表示更新、検証、再確認まで完了。 |
| `TRACKER-061` | 診断再生 UI で等倍速と倍率を分離する | 比較記録 | 完了 | `TRACKER-060` | 表示分離、検証、再確認まで完了。 |
| `TRACKER-062` | 診断再生 UI を従来配置へ戻し速度選択へ等倍速を追加する | 比較記録 | 完了 | `TRACKER-061` | 配置再調整、検証、再確認まで完了。 |
| `TRACKER-063` | 再生開始時の速度選択維持と可変早送り倍率を追加する | 比較記録 | 完了 | `TRACKER-062` | 可変倍率、選択速度維持、検証、確認まで完了。 |

## 詳細な検証・証跡履歴

上の作業一覧は読みやすさのため要約し、代表的な検証結果と報告書参照を以下に示す。省略された詳細は冒頭から参照できる原文に保持する。

- `TRACKER-039`: 再発防止テストは旧実装で失敗し、修正後に成功。初回確認の進捗同期漏れを修正し、再確認は指摘なし。PR #8 は 2026-05-12 に統合済み。
  - 証跡: `reports/tracker-039-evidence-20260512084929.md`、`reports/tracker-039-review-20260512085258.md`、`reports/tracker-039-review-r2-20260512090207.md`
- `TRACKER-040`: 設計と進捗管理の変更のみ。初回・再確認とも阻害指摘なしで、設計分離と記録先構造は利用者承認済み。製品とテストのソースコードはこの段階では変更していない。
  - 証跡: `reports/tracker-040-approval-sync-20260512105353.md`、`reports/tracker-040-design-review-20260512094448.md`、`reports/tracker-040-design-review-r2-20260512102542.md`、`reports/tracker-040-design-separation-fix-20260512100723.md`、`reports/tracker-040-progress-sync-20260512094809.md`、`reports/tracker-040-r2-progress-sync-20260512102917.md`、`reports/tracker-040-session-folder-design-fix-20260512101934.md`
- `TRACKER-041`: 全追跡パケット保存へ設計を変更し、保存除外方針を撤回。実装は後続作業へ分離した。
  - 証跡: `reports/tracker-041-all-trackers-design-audit-20260512111218.md`、`reports/tracker-041-all-trackers-design-fix-20260512111628.md`、`reports/tracker-041-all-trackers-implementation-audit-20260512111218.md`
- `TRACKER-042`: 対象限定テスト 5 件、全体 `Tracker.Tests` 163 件成功。確認は阻害指摘なし。補助保存物、CaptureOn 付随情報、記録先、診断再生は後続作業の範囲。
  - 証跡: `reports/tracker-042-all-trackers-implementation-20260512113459.md`、`reports/tracker-042-all-trackers-tdd-contract-20260512112546.md`、`reports/tracker-042-progress-sync-20260512114544.md`、`reports/tracker-042-review-20260512114147.md`、`reports/tracker-042-verification-20260512114147.md`
- `TRACKER-043`: 先に失敗テストを追加。対象限定 5 件、関連 13 件、全体 168 件成功。確認は阻害指摘なし。比較元データ保持は次作業で扱う。
  - 証跡: `reports/tracker-043-review-20260512120832.md`、`reports/tracker-043-review-followup-sync-20260512121304.md`
- `TRACKER-044`: 先に失敗テストを追加。対象限定 7 件、関連 30 件、全体 175 件成功。追加確認後も同件数成功。通常経路外の無効元データ対策は後続リスクとして保持。
  - 証跡: `reports/tracker-044-review-20260512123921.md`、`reports/tracker-044-review-followup-20260512124330.md`
- `TRACKER-045`: 先に失敗テストを追加。対象限定 5 件、関連 35 件、全体 180 件成功。確認は阻害指摘なし。起動登録は次作業へ分離し、CaptureOff 競合時の例外伝播は再確認事項として保持。
  - 証跡: `reports/tracker-045-live-receiver-implementation-20260512125847.md`、`reports/tracker-045-progress-sync-20260512131047.md`、`reports/tracker-045-review-20260512130623.md`
- `TRACKER-046`: 初回は対象限定 3 件、関連 38 件、全体 183 件成功。確認で多宛先通信の通常経路に阻害指摘が出たため修正し、修正後は対象限定 4 件、関連 42 件、全体 187 件成功。再確認は阻害指摘なし。ソケット抽象化と DI 起動試験は非阻害の後続事項、PR #9 の準備完了化は対象外。
  - 証跡: `reports/tracker-046-multicast-review-fix-implementation-20260512135310.md`、`reports/tracker-046-multicast-review-fix-tdd-20260512134307.md`、`reports/tracker-046-review-r2-20260512140145.md`、`reports/tracker-046-runtime-registration-implementation-20260512132555.md`
- `TRACKER-047`: 初回は対象限定 4 件、関連 39 件、全体 191 件成功し、確認で阻害指摘 2 件。修正後は対象限定 5 件、関連 40 件、全体 192 件成功し、再確認は阻害指摘なし。診断 UI 改善、起動堅牢化、PR 準備完了化は対象外。
  - 証跡: `reports/tracker-047-design-audit-after-review-20260512151541.md`、`reports/tracker-047-review-20260512150929.md`、`reports/tracker-047-review-fix-implementation-20260512152742.md`、`reports/tracker-047-review-r2-20260512153751.md`
- `TRACKER-048`: `CaptureReplayTests` 8 件、関連 47 件、全体 194 件成功、`git diff --check` 問題なし。確認は阻害指摘なし。依存方向と `--settings` の操作性は保留事項として後続設計で扱う。
  - 証跡: `reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md`、`reports/tracker-048-completion-readiness-20260512163550.md`、`reports/tracker-048-review-20260512160935.md`
- `TRACKER-049`: 診断比較の設計と進捗管理を再同期。専用確認は阻害指摘なし。
  - 証跡: `reports/tracker-049-design-review-20260512201915.md`、`reports/tracker-049-design-tracking-sync-20260512201328.md`、`reports/tracker-049-diagnostics-comparison-task-breakdown-20260512200244.md`
- `TRACKER-050`: 対象限定 8 件、関連 38 件、全体 202 件成功、`git diff --check` 問題なし。1 万件超ログでの選択ずれを初回指摘後に修正し、再確認は阻害指摘なし。
  - 証跡: `reports/tracker-050-diagnostics-comparison-contract-implementation-20260512202753.md`、`reports/tracker-050-progress-sync-20260512211517.md`、`reports/tracker-050-review-20260512204924.md`、`reports/tracker-050-review-fix-implementation-20260512205728.md`、`reports/tracker-050-review-r2-20260512210935.md`
- `TRACKER-051`: 比較表示の対象限定 10 件、関連 33 件、`CaptureReplayTests` 8 件成功。既存の render snapshot、設定画面、timeline scrubber、再生操作を維持。再確認は阻害指摘なし。ブラウザ手動証跡と README 更新は次作業へ分離。
  - 証跡: `reports/tracker-051-diagnostics-ui-comparison-implementation-20260512212409.md`、`reports/tracker-051-progress-sync-20260512215710.md`、`reports/tracker-051-review-20260512213715.md`、`reports/tracker-051-review-fix-implementation-20260512214442.md`、`reports/tracker-051-review-r2-20260512215156.md`
- `TRACKER-052`: 文書のみの変更のため `dotnet test` は未実施。`git diff --check` 問題なし、専用確認は阻害指摘なし。手動証跡として残す比較項目を README に固定。
  - 証跡: `reports/tracker-052-docs-manual-evidence-implementation-20260512220318.md`、`reports/tracker-052-progress-sync-20260512221551.md`、`reports/tracker-052-review-20260512221019.md`
- `TRACKER-054`: `TrackerConfigurationBindingTests` 6 件、関連 10 件 / 5 件成功、`git diff --check` 問題なし。専用確認は阻害指摘なし。
  - 証跡: `reports/tracker-054-receive-endpoint-implementation-20260512231920.md`、`reports/tracker-054-review-20260512233050.md`
- `TRACKER-055`: 対象限定全体 32 件成功、`git diff --check` 問題なし。同一時刻の優先規則に関する初回回帰を追加テストで修正し、再確認は阻害指摘なし。100MB 超の補助保存物でも再生位置操作が大きさに比例して遅くならないことを要件として保持。
  - 証跡: `reports/tracker-055-playback-scrub-performance-implementation-20260513001906.md`、`reports/tracker-055-progress-sync-20260513005919.md`、`reports/tracker-055-review-20260513003935.md`、`reports/tracker-055-review-r2-20260513005448.md`
- `TRACKER-056`: 対象限定テスト 36 件成功、`git diff --check` 問題なし。専用確認は阻害指摘なし。`DiagnosticsFieldViewFactory` の変換処理を直接確認する試験不足は保留事項。
  - 証跡: `reports/tracker-055-diagnostics-field-source-investigation-20260512233148.md`、`reports/tracker-056-field-source-toggle-design-20260513010250.md`、`reports/tracker-056-field-source-toggle-implementation-20260513011324.md`、`reports/tracker-056-progress-sync-20260513014628.md`、`reports/tracker-056-review-20260513013805.md`
- `TRACKER-057`: 対象限定テスト 45 件成功、`git diff --check` 問題なし。同一表示元の二重描画を初回確認後に単一表示層へ修正し、再確認は阻害指摘なし。
  - 証跡: `reports/tracker-057-field-overlay-design-20260513014926.md`、`reports/tracker-057-field-overlay-implementation-20260513015935.md`、`reports/tracker-057-progress-sync-20260513023929.md`、`reports/tracker-057-review-20260513022102.md`、`reports/tracker-057-review-r2-20260513023505.md`
- `TRACKER-053`: `Tracker.Server` 構築成功、全体 `Tracker.Tests` 227 件成功、`git diff --check` 成功。古い PR 本文案の阻害指摘を再確認前に解消。ブラウザ手動証跡未実施は利用者指示に基づく残留リスクで、単独の阻害要因にはしない。
  - 証跡: `reports/tracker-053-final-validation-fix-20260513025052.md`、`reports/tracker-053-pr-ready-evidence-20260513024248.md`、`reports/tracker-053-progress-sync-20260513030702.md`、`reports/tracker-053-review-20260513025530.md`、`reports/tracker-053-review-r2-20260513030250.md`
- `TRACKER-058`: 対象限定検証 45 件成功、`git diff --check` 成功、専用確認は阻害指摘なし。全体は 229 件成功 / 1 件失敗で、失敗は今回変更外の手元の `appsettings.json` の既定無効契約違反によるもの。
  - 証跡: `reports/tracker-058-er-force-replay-investigation-20260513062747.md`、`reports/tracker-058-progress-sync-20260513070654.md`、`reports/tracker-058-review-20260513070147.md`、`reports/tracker-058-saved-alignment-design-20260513063637.md`、`reports/tracker-058-saved-alignment-implementation-20260513064540.md`
- `TRACKER-059`: 対象限定検証 62 件、関連検証 32 件成功、`git diff --check` 成功、再確認は阻害指摘なし。全体は 238 件成功 / 1 件失敗で、失敗理由は `TRACKER-058` と同じ変更外の手元の設定。
  - 証跡: `reports/tracker-059-fastest-timeline-design-20260513175146.md`、`reports/tracker-059-fastest-timeline-implementation-20260513181201.md`、`reports/tracker-059-fastest-timeline-investigation-20260513173834.md`、`reports/tracker-059-review-20260513184442.md`、`reports/tracker-059-review-fix-implementation-20260513185336.md`、`reports/tracker-059-review-r2-20260513190058.md`
- `TRACKER-060`: 対象限定検証 19 件、関連検証 48 件成功、`git diff --check` 成功、再確認は阻害指摘なし。全体は 237 件成功 / 1 件失敗で、失敗理由は同じ変更外の手元の設定。
  - 証跡: `reports/tracker-060-realtime-playback-design-20260513194832.md`、`reports/tracker-060-realtime-playback-implementation-20260513195439.md`、`reports/tracker-060-review-20260513200044.md`、`reports/tracker-060-review-fix-implementation-20260513200441.md`、`reports/tracker-060-review-r2-20260513200634.md`
- `TRACKER-061`: `DiagnosticsPlaybackStateTests` 23 件、関連検証 51 件成功、`git diff --check` 成功。初回確認 N1 を追加修正で解消し、再確認は指摘なし。
  - 証跡: `reports/tracker-061-playback-ui-separation-design-20260513204405.md`、`reports/tracker-061-playback-ui-separation-implementation-20260513205042.md`、`reports/tracker-061-review-20260513205647.md`、`reports/tracker-061-review-fix-implementation-20260513210059.md`、`reports/tracker-061-review-r2-20260513210407.md`
- `TRACKER-062`: `DiagnosticsPlaybackStateTests` 27 件、関連検証 56 件成功、`git diff --check` 成功。初回確認 B1 を追加修正で解消し、再確認は指摘なし。
  - 証跡: `reports/tracker-062-playback-speed-choice-design-20260513213014.md`、`reports/tracker-062-playback-speed-choice-implementation-20260513213716.md`、`reports/tracker-062-review-20260513214513.md`、`reports/tracker-062-review-fix-implementation-20260513214808.md`、`reports/tracker-062-review-r2-20260513215222.md`
- `TRACKER-063`: `DiagnosticsPlaybackStateTests` 44 件、関連検証 73 件成功、`git diff --check` 成功。専用確認は阻害指摘なし。実画面での小型 UI 未確認とタイマー粒度は保留事項。
  - 証跡: `reports/tracker-063-review-20260513224028.md`、`reports/tracker-063-variable-playback-speed-design-20260513222344.md`、`reports/tracker-063-variable-playback-speed-implementation-20260513223102.md`
