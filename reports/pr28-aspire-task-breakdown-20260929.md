# PR #28 Aspire 実装タスク分割報告

日付: 2026-09-29

## 目的

PR #28 で確定した Aspire シミュレーション試験環境の設計を、実装担当が TDD で着手でき、変更を小さくレビューできる作業単位へ分割する。

## 確認した資料

- PR #28 の current design と implementation split。
- Tracker/Design/tasks-status.md の更新規則と ASPIRE-001 の完了条件。
- Tracker/Design/Testing/aspire-simulation-test-environment.md の ASPIRE-002〜006、ASPIRE-NET-001〜009、診断・完了条件。
- Tracker/Design/Testing/tracker-comparison-debug-design.md の ASPIRE-006A〜006E と focused test 契約。
- .github/workflows/dotnet-test.yml の失敗時診断 artifact。

## 分割方針

- 既存設計の ASPIRE-002〜005 と ASPIRE-006A〜006E を親の実装順序として維持した。
- RuntimeHost 診断と Simulator 接続の変更範囲を分離するため ASPIRE-003 を ASPIRE-003A / 003B に分けた。
- referee/game-state と Crane 制御経路を分離するため ASPIRE-004 を ASPIRE-004A / 004B に分けた。
- ASPIRE-006A〜006E は既に責務境界が明確なため、そのまま独立タスクとして維持した。
- ASPIRE-NET-001〜009 は製品実装タスクではなく受入ケースとして維持した。
- Linux / Windows Docker Desktop / macOS Docker Desktop の実 packet 検証は ASPIRE-007A / 007B / 007C に分離し、他 OS の結果で代用できないようにした。
- 最終レビュー、進捗同期、詳細報告、PR current HEAD と一致する CI 確認を ASPIRE-008 とした。

## TDD と診断 artifact

各実装タスクは focused test または application model test を先に追加し、未実装状態の失敗を確認してから実装する。失敗確認と実装はレビュー可能な論理単位で commit / push する。

既存 .NET tests workflow は失敗時に TRX、stdout、stderr、vstest diagnostics、binlog、exit code、環境情報、source snapshot を artifacts/test-results へ集約し、actions/upload-artifact で保存する。将来 Docker/Aspire 統合 workflow を追加する場合も、これに加えて Aspire と各コンテナの原因調査ログを保存する。

## 変更

Tracker/Design/tasks-status.md に実装タスク表を追加し、ASPIRE-001 の状態を独立最終レビュー合格後の現状へ同期した。

タスク分割 commit: 31bc966b6705fd4341b0ec98d6facef2c62aba0a

## 検証

- git diff --check: 成功。
- 追加した ASPIRE task ID を rg で確認。
- tasks-status.md 全体の CSpell は既存箇所の Sudachi / Blazor 6件で失敗。今回追加したタスク分割行には新規指摘なし。
- 31bc966b... と head SHA が一致する .NET tests run 36559420881 は report 作成時点で in_progress。別 SHA の run は代用していない。
