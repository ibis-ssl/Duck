# PR #28 TIGERs vs Crane task breakdown

## Scope

TIGERs vs Crane 対戦設計の親タスク `ASPIRE-006F` / `ASPIRE-006G` を、TDD と小さな commit / push が可能な実装・受入単位へ分割した。

## Breakdown

### ASPIRE-006F: 対戦資源

- `ASPIRE-006F1`: 対戦 fixture の版管理
- `ASPIRE-006F2`: `match` mode と resource topology
- `ASPIRE-006F3`: Simulator / Game Controller 対戦資源
- `ASPIRE-006F4`: TIGERs / AutoRef / SSL log 資源
- `ASPIRE-006F5`: Crane / Duck 対戦設定
- `ASPIRE-006F6`: `match-controller` と試合 lifecycle

依存関係は fixture / topology を先に固定し、Simulator / Game Controller と TIGERs 系資源を組み立て、Crane / Duck 契約を固定した後に `match-controller` を完成させる順序とした。

### ASPIRE-006G: 一括対戦試験

- `ASPIRE-006G1`: topology / team / referee 受入
- `ASPIRE-006G2`: 双方 active motion 受入
- `ASPIRE-006G3`: AutoRef / tracker 経路受入
- `ASPIRE-006G4`: 試合完了 / 証跡受入

`ASPIRE-MATCH-001`〜`005` を、構成契約、双方の移動、AutoRef / tracker、試合終了と artifact の順に独立して確認できるようにした。

## TDD / diagnostics

各製品実装タスクは focused test または application model test を先に追加して未実装状態で失敗を確認し、その後に実装する。`.NET tests` workflow には失敗時の test results、stdout、stderr、診断ログを artifact として保存する処理が既に存在するため、今回 workflow 変更は不要。

## Files

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`
- `Tracker/Design/tasks-status.md`
- `reports/pr28-tigers-vs-crane-task-breakdown-20260929.md`

## Validation

- `git diff --check`: pass.
- Aspire design CSpell: 0 issues.
- tasks-status CSpell: existing `Sudachi` / `Blazor` 6 issues only; new diff 0.
