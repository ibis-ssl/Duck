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
