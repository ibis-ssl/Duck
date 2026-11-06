> Historical local-validation snapshot before the later scoped Docker/publication authorization. Current commit, CI and real-run state are recorded separately in the PR progress report.

# 実装・検証報告：#49 審判指令の実試合形式への接続

## 対象と保存状態

- repository: ibis-ssl/Duck
- absolute path: <workspace>
- branch: task/issue49-kickoff-api-phase
- HEAD: 3df13f1497b26ee572ff06bb5abcf3b858ff16e4（未commitのローカル差分）
- source blob: 056c45b8947781733c8a3904c4151b40c74f1bf3
- 既存Issue #49 / Draft PR #116、base design/issue18-aspire-test-orchestration。新Issue/PRは作成していない。
- 変更：Tracker/Tracker.Tests/AspireFullStackAcceptanceTests.cs、290追加/6削除。
- この実装報告・通常レビュー報告は今回作成した未追跡ファイル。既存の未追跡証跡は保持。
- ASPIRE-006F6の既存タスク範囲内。tasks-status/phases-status、Issue/Project/Todoは変更していない。

## 変更前と今回できること

acceptanceは既存Driverのlifecycle sourceを渡さず、HTTP healthから得点/NextCommand等を欠損させていた。既存のREADY_AUTO待機自体は正しかった。
同じHTTP source・readGateを使い、元のAPI requestSequenceとUDP/API文脈を既存Driverへ渡す。実acceptanceと回帰試験が同じprivate composerを使う。
固定2秒待機、新しい受信器/poller、ローカル採番やfreshnessの延命は追加していない。数値enumとnullable JSONを扱う。

## 実装・検証の経過

| 区分 | 結果 |
|---|---|
| 既存HostAutoDrive baseline | 16成功、exit0 |
| 先行Red：lifecycle interface | 1失敗、exit1。実装前にinterface未実装を確認 |
| opt-in指定なしの試行 | 新テスト0件検出。成功/Redに数えない |
| 初回Green | 18成功、exit0。その後レビューで不備が判明した中間結果 |
| RF-NR001 null回帰の修正前 | exit1、JSON Nullを数値として読むInvalidOperationException。生stdout未回収、結果要約を保存 |
| 最初のcomposition試行 | session11552が待機。Ctrl-Cでexit1、assert結果なし。Red/Greenに数えない |
| 名称/queue/有限キャンセル修正後 | 2成功、exit0、124ms |
| 関連回帰の一括確認 | 21成功、失敗0、skip0、exit0、1秒 |
| 最終assert追加後の同じ一括確認 | 21成功、失敗0、skip0、exit0、1秒、session84556 |
| git diff --check | exit0 |

通常のtest buildではacceptanceファイルがCompile Removeされるため、対象検証には RunAspireFullStackAcceptance=true が必要。クラス全体のDocker試験は実行していない。

## 正確な対象コマンド

先行Red：

```powershell
dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj --no-restore -p:RunAspireFullStackAcceptance=true --filter "FullyQualifiedName~HttpRefereeAcceptanceSourceExposesLifecycleObservations" --logger "console;verbosity=minimal"
```

RF-NR001修正前：

```powershell
dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj --no-restore -p:RunAspireFullStackAcceptance=true --filter "FullyQualifiedName~HttpRefereeLifecycleObservationPreservesMatchedUdpAndApiContext" --logger "console;verbosity=minimal"
```

最終Green：

```powershell
dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj --no-restore -p:RunAspireFullStackAcceptance=true --filter "FullyQualifiedName~HttpRefereeLifecycleObservationPreservesMatchedUdpAndApiContext|FullyQualifiedName~HttpLifecycleWaitsForFreshMatchedKickoffAndReadyAutoBeforeNormalStart|FullyQualifiedName~HttpRefereeCommandSourceFailsClosedWhenCommandCounterSkips|FullyQualifiedName~HttpRefereeSourceSerializesGuardAndFinalReadAndDetectsPendingChange|FullyQualifiedName~HttpRefereeSourceCarriesPostGameStageIntoTheTerminationSnapshot|FullyQualifiedName~RefereeDriverHostAutoDriveTests" --logger "console;verbosity=minimal"
```

baselineと中間試行のコマンドは下記JSONにも保存。試行ごとの件数であり、重複した試験を合算した固有テスト総数ではない。

## 通常レビュー

同じ通常レビュワー /root/issue49_orchestrator/issue49_normal_review を継続。
初回blob71629a7ではRF-NR001/P1（nullable数値の例外）とRF-NR002/P2（実HTTP経路と待機の回帰保証不足）でfail。
blob22555a4ではRF-NR001 closed、RF-NR002の旧sequence/503待機assertがpartial。
最終blob056c45bでは、HTTP request index [4,6,9] とNORMAL_START送信request9をassertし、両件closed、残る必須指摘なし/source-pass。
ローカルtest用のrequest countはassert専用であり、API実requestSequenceの生成には使っていない。

## 実行環境・権限・Skill

- Windows FA780 / PowerShell、本人経路authorized FA780 user。既存dotnetによるlocal_execution_available。
- 初回sandbox obj/cacheとreport書込みのAccess deniedはfilesystem error。auto-review拒否ではない。承認されたrequire_escalatedの対象dotnet/記録操作のみ実行し、認証・safe.directory・OS設定は変更していない。
- CodexSkillローカル9ebde2cと公式8ee6e9fのtreeは8e2352f54af86ae530d862a04850e9dec785c3d7で一致、入口は改行正規化後一致。必須依存存在。AGENTS.mdは対象階層・上位で未検出、追加停止条件にしていない。
- 既存実装担当requested Luna/high、通常レビュワーrequested Sol6.1/highを継続。applied:null、実設定未確認。新規高コストprofile/spawnなし。
- FA780毎時監査Todo/TODO_STALE process_startは操作・再試行していない。

## 証拠

task-11は <evidence-root>。

- issue49-realistic-referee-validation-20261105.json：baseline/最初のRed/中間18Green
- issue49-realistic-referee-focused-20261105.log：中間18Greenのログ。最終21件のログではない
- issue49-realistic-referee-review-fix-20261105.json：修正後の試行・最終source identity
- issue49-realistic-referee-review-fix-20261105.log：回収できた元terminal抜粋。省略されたbuild出力とRF-NR001 Red生stdout欠落を明示
- issue49-realistic-referee-aborted-focused-20261105.json：中断試行。rootは元process終了を確認し追加Stop-Processを実行していない
- reports/issue49-realistic-referee-flow-normal-review-20261105.md：同レビュワーの指摘・closure

## 未実施と残る境界

ローカル修正・Docker不要focused検証・通常レビューは完了。既存Driver/製品/Craneソースや以前のCrane修正は変更・組込していない。
Docker実通信、実Crane動作、実試合完走・独立実通信受入、push/新規Issue/PR/merge/CIは未実施。単体試験の制御入力を実試合成功の代替にしていない。
今回の差分は未commit・未公開。旧HEADのCIを新差分の成功証拠に転用していない。#49/#43全体の完了やマージ可能を宣言しない。


The recorded Red/Green executions preceded the later history-preservation commits. The test-only addition and implementation are saved as separate commits without rewriting or backdating history.

## Current bounded verification closure (2026-11-06)

- Reviewed source commit: `b428864d79084900bb81a0792af299a8d34392bf` (parent `5f3fc9ae44b94e81f205067dcb009e57577ffab1`); seven-file change. Existing normal source review and independent source review both pass with no required findings.
- Exact-HEAD focused validation was saved as raw stdout/stderr and TRX: 44 passed, 0 failed, 0 skipped, exit 0. A distinct HTTP-source/HostAutoDrive regression filter also passed: 21 passed, 0 failed, 0 skipped, exit 0. The sets are non-overlapping; Docker was not started. These records are retained in the task evidence area.
- Existing exact-HEAD CI run 42173421992 completed successfully for .NET tests; optional Linux packet-flow and full-stack acceptance jobs were skipped.
- Runtime evidence remains bounded: the existing supervised runs observed NORMAL_START and Crane state transitions; the later run ended under the existing 10-second movement watchdog. These observations do not establish POST_GAME/full-match completion or a complete pre-PREPARE placement acknowledgement. Those are not claimed as completed here.
- Historical review-enforcer reservation/freeze/attestation metadata was not present before this recovery cycle. That process gap is recorded; this report does not describe the earlier reviews as having been reserved or frozen retroactively.
- No run-specific private telemetry, credentials, local paths, container identifiers, or rejected-comment payloads are included. The prior refused PR-analysis comment update remains withheld.
