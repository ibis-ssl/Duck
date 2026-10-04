# PR #28 TIGERs vs Crane design update

## Scope

PR #28 の Aspire 設計へ、TIGERs Sumatra AI と Crane の対戦モードを追加した。

## Crane reference

- repo: `ibis-ssl/crane`
- branch: `develop`
- SHA: `af6e0d3dec745415ce060ff5de2042afd3ec5145`
- checked: `docker/match-vs-tigers/docker-compose.yaml`
- checked: `config/simulation_protocol_fixed.xml`
- checked: `config/state-store-initial.json.stream`
- checked: `scripts/match_controller_pb.py`
- checked: `.github/workflows/match-vs-tigers.yaml`

Crane 側では Blue=`TIGERs Mannheim`、Yellow=`ibis`、Crane=`team:=ibis`、Sumatra=`--aiBlue`。
raw vision は `224.5.23.2:10020`、referee は `224.5.23.1:11003`、tracker は `224.5.23.2:11010`。
Game Controller 初期 command は `STOP`。match controller は referee / vision / GC API を確認し、試合を進行して `POST_GAME` または最大時間で終了する。

## Duck design

- `Testing:Mode=match` を `base` / `comparison` と排他的に追加。
- resource: `tigers-blue`, `autoref-tigers`, `ssl-log-recorder`, `match-controller`。
- match mode では `cm4-sim` を起動しない。
- Duck の 11010 publish は無効にし、AutoRef の tracker source を混在させない。
- 11003 の producer は `game-controller` のみ。
- Sumatra / Game Controller fixture は Duck 側で版管理し、起動時 clone はしない。
- image は固定 tag または digest を使う。
- acceptance: `ASPIRE-MATCH-001`〜`005`。
- tasks: `ASPIRE-006F`, `ASPIRE-006G`。
- 勝敗自体は CI 合否にせず、双方の active motion、AutoRef/tracker 経路、試合完了、結果と診断情報生成を確認する。

## Files

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`
- `Tracker/Design/tasks-status.md`
- `reports/pr28-tigers-vs-crane-design-update-20260929.md`

## Validation

- `git diff --check`: pass.
- Aspire design CSpell: 0 issues.
- tasks-status CSpell: existing `Sudachi` / `Blazor` 6 issues only; new diff 0.
- `npm run lint:md`: Windows `cmd.exe` では `xargs` が無いため textlint 段階で停止。さらに `rg --files .agents` でも `.agents/skills/review-enforcer` が存在しないことを確認した。
