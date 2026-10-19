# PR #28 独立最終レビュー報告

## メタデータ

- 対象: ibis-ssl/Duck PR #28
- reviewed implementation HEAD: `17a58778058cfd9d995475b15fa346d7544d0121`
- base: `449296725fc69dc004818ede2e8ad59a52ef2d27`
- 種別: independent final review
- 判定: **fail**
- 本レビューでは実装・修正・mergeを行っていない。

current HEADを固定し、変更21ファイル、Duckの直接依存、Crane / TIGERs / ER-Forceの外部契約を独立に確認した。既存normal review記録は正本設計の独立確認後に履歴整合の対象として確認した。

## Findings

### I28-IFR-001 / High / open — referee / game-state供給契約がない

場所: `Tracker/Design/Testing/aspire-simulation-test-environment.md:91,191,247`

正本設計は初期AppHost自身は試験シナリオを生成しないとする一方、ASPIRE-005 / ASPIRE-NET-007ではCraneの指令が`cm4-sim`経由でSimulatorへ入り、SSL-Vision上のロボット位置変化として観測できることを必須条件にしている。しかし通常構成にはreferee/game-stateのauthoritative producerまたは試験driverが定義されていない。

現行Crane `35b700bdaa69fa1daa63bccd07b3dd2b84efbf96`では、sim時のreferee portは`11003`、`initial_session`は`HALT`。既存sim起動は`ssl-game-controller`を含み、scenario testは`FORCE_START`等を明示投入してactive motionを作る。よって現設計だけではASPIRE-NET-007の位置変化をdeterministicに成立させられず、UDP故障とgame-state不足を区別できない。

またTIGERs Sumatraの`simulation_protocol`は`11003`で`gameController=true`かつ`publishRefereeMessages=true`である。通常modeへ単純に別Game Controllerを追加するとcomparison modeでproducer重複の可能性がある。

Required action: 通常mode / comparison modeごとにreferee/game-stateのauthoritative producerを一つに決め、AppHost resourceまたは試験driver、`11003` ownership、起動依存、HALTから既知のactive motionへ遷移するfixture、Sumatra内蔵Game Controllerとの排他、対応するapplication-model / runtime TDD契約を正本設計へ固定する。

### I28-IFR-002 / Low / open — 比較resource名が不一致

主設計`aspire-simulation-test-environment.md:305`は`tigers-tracker` / `erforce-tracker`、比較正本`tracker-comparison-debug-design.md:33-34,215`は`tracker-tigers` / `tracker-erforce`を使う。

Required action: 一つの命名へ統一し、AppHost resource、dashboard、application-model testの契約を一致させる。

### I28-IFR-003 / Low / open — PR本文のexact-HEAD記載が古い

レビュー時のcurrent HEADは`17a58778058cfd9d995475b15fa346d7544d0121`だが、PR本文はFinal HEADを`f5950cbd5f830d2b87c8eb77fde2e9f7aef66d33`、Exact-HEAD CIをrun `36297572886`のままにしている。current HEADと一致する`.NET tests` run `36321822129`はsuccess。

Required action: 技術修正後のfinal current HEAD確定時に、PR本文をそのHEADと一致するrunへ更新する。別SHAのrunは代用しない。

## Coverage / validation

- requirement / correctness / API-config / tests: I28-IFR-001,002を検出。
- scope: docs / tracking / report / handoffの範囲内。
- failure diagnostics: `.github/workflows/dotnet-test.yml`はTRX、stdout、stderr、VSTest diagnostics、blame、binlog、exit code、環境・git情報、source archiveを失敗artifactへ保存するため追加workflowは不要。
- security: docs-only差分で秘密情報追加なし。
- `git diff --check 4492967..17a5877`: success。
- current-HEAD CI: run `36321822129`、head_sha `17a58778058cfd9d995475b15fa346d7544d0121`、completed / success。別SHAは代用していない。
- multicast共有: RuntimeHost / DebugHostは`ExclusiveAddressUse=false` + `ReuseAddress=true`で、10020共有設計と整合。
- TIGERs image疑義は解消。現行Sumatra release workflowがDocker Hub `tigersmannheim/sumatra`へpublishするためfindingにしない。
- 既存normal reviewで解決済みのUUID identity / logical role / multiple-ball assignmentはcurrent正本へ反映済み。
- AppHost未実装のためASPIRE-NET実packet試験は未実施であり、成功扱いにしていない。

## 判定

**fail**。High 1件、Low 2件がopen。

I28-IFR-001はASPIRE-005 / ASPIRE-NET-007の必須正常経路を一意に実装・検証するための入力契約不足であり、ASPIRE-002以降へ進む前に解消が必要。

本reviewerはfinding修正を行わない。修正後は同じ独立reviewer / 同じチャットでI28-IFR-001〜003のrequired actionに限定したclosure verificationを行う。mergeは行わない。
## 独立最終 closure verification（2026-09-28）

### Closure identity

- review mode: `independent_final_closure`
- initial independent reviewed HEAD: `17a58778058cfd9d995475b15fa346d7544d0121`
- closure reviewed implementation HEAD: `6e849863dfb7741ed52b7505fe2b49993cd55b65`
- reviewer continuity: 初回 independent final review と同じ chat / reviewer。
- closure scope: `I28-IFR-001`〜`I28-IFR-003` と exact-HEAD CI delta のみ。新しい exhaustive review criteria は追加していない。
- closure verdict: **pass**

### Finding closure

#### I28-IFR-001 / High — closed

Required action の各要素を current design で確認した。

- 通常 mode / comparison mode とも `game-controller` が `224.5.23.1:11003` の唯一の authoritative producer。
- `referee-driver` は 11003 を直接 publish せず、`game-controller` の `ws://127.0.0.1:8082/api/control` を操作する client。
- `referee-driver` integration fixture は 11003 の `HALT` を確認してから `NEXT_COMMAND` と必要な `FORCE_START` / `NORMAL_START` を送り、active command を確認してから Crane → `cm4-sim` → Simulator の経路を検査する。
- `crane` は `game-controller` に `WaitForStart` し、comparison mode の `tracker-tigers` / `tracker-erforce` も `simulator` / `game-controller` の開始後に起動する。
- Sumatra は `source=NETWORK` / `port=11003` / `gameController=false` / `publishRefereeMessages=false` に固定し、内蔵 Game Controller を使わない。
- AppHost application-model test、integration fixture、`ASPIRE-NET-007`、診断ログの契約が正本設計に入っている。

#### I28-IFR-002 / Low — closed

`Tracker/Design/Testing/aspire-simulation-test-environment.md` と
`Tracker/Design/Testing/tracker-comparison-debug-design.md` の比較 resource 名を
`tracker-tigers` / `tracker-erforce` に統一した。対象2設計書に
`tigers-tracker` / `erforce-tracker` の残存はない。

#### I28-IFR-003 / Low — closed

closure review 開始時の PR current HEAD は
`6e849863dfb7741ed52b7505fe2b49993cd55b65`。PR 本文の Final HEAD は同 SHA、
Exact-HEAD CI は `.NET tests` run `36330110940` であり、この run は同 SHA に
紐づく `completed / success`。別 SHA の run は代用していない。

### Completeness matrix

| Finding | Required action | Production path | Actual composition fixture / administrative evidence | Focused evidence | Disposition |
| --- | --- | --- | --- | --- | --- |
| `I28-IFR-001` | referee producer、11003 ownership、起動依存、HALT→active fixture、Sumatra 排他、TDD 契約を固定 | 主設計の「資源構成」「レフェリー / game-state の所有権」「起動順序」「ASPIRE-005」「テスト方針」「ASPIRE-NET-007」と比較設計の referee 契約 | `referee-driver` integration fixture が Game Controller API を操作し、11003 の遷移を観測した後に composed UDP path を検査する契約 | `dd00080dcd1409d1773fe0499f4fd53d308eb1a8`; current design の `gameController=false` / `publishRefereeMessages=false`; exact-head CI | closed |
| `I28-IFR-002` | 比較 resource 名を一つに統一 | 主設計 / 比較設計 / AppHost model test 契約 | comparison mode の AppHost resource model が `tracker-tigers` / `tracker-erforce` を使う | `54354a97abfbc720c9849444e6920396dd3e18a3`; 対象2設計書で旧名称残存なし | closed |
| `I28-IFR-003` | PR本文の Final HEAD / Exact-HEAD CI を current HEAD と一致させる | PR #28 本文 Validation | runtime fixture は非該当。PR本文と GitHub Actions run の head SHA が administrative evidence | `6e849863dfb7741ed52b7505fe2b49993cd55b65` / run `36330110940` success | closed |

### Validation assessment

- `39318c3fe117f952b4db89edb642aab9c8617986..6e849863dfb7741ed52b7505fe2b49993cd55b65` は、2設計書の finding fix と対応 report / handoff に限定されている。
- 技術設計 HEAD `54354a97abfbc720c9849444e6920396dd3e18a3` では `git diff --check` success、対象2設計書 CSpell 0 issues。
- `54354a97abfbc720c9849444e6920396dd3e18a3..6e849863dfb7741ed52b7505fe2b49993cd55b65` は対応 report / handoff の追加だけで、設計内容は変わっていない。
- closure reviewed HEAD と一致する `.NET tests` run `36330110940` は `completed / success`。
- full `npm run lint:md` は接続 Windows 環境に `xargs` がなく blocked のまま。成功扱いにはしていない。この unavaile check は I28-IFR-001〜003 の closure を妨げる新規 finding ではない。
- `.github/workflows/dotnet-test.yml` は TRX、stdout、stderr、VSTest diagnostics、blame、binlog、exit code を `artifacts/test-results` に保存するため、失敗診断 artifact 契約は存在する。

### Final verdict and attestation

**pass**。`I28-IFR-001`〜`I28-IFR-003` はすべて closed。新規 finding は追加していない。

技術 verdict は closure reviewed implementation HEAD
`6e849863dfb7741ed52b7505fe2b49993cd55b65` に適用する。本報告書
`reports/pr28-independent-final-review-20260927.md` は、同 HEAD を first parent とする
一つの administrative report-attestation commit でのみ更新する。attestation commit は
この reserved report path 以外を変更してはならない。attestation SHA は commit 後に PR
本文 / コメントへ外部記録する。attestation 後に別の Git commit が追加された場合は、
通常 fix verification と同じ independent reviewer による bounded closure を再度必要とする。

merge は行わない。
