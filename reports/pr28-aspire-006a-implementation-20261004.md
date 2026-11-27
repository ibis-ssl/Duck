# ASPIRE-006A comparison resource model 実装報告

## 対象

- Issue: #38 / ASPIRE-006A
- 基準 branch: `task/pr28-aspire-005`
- rebase 後の実装 HEAD: `6f0c744...`（報告作成時）
- 確認日: 2026-10-04 UTC

## 実装

`Testing:Mode=comparison` で `tracker-tigers`、`tracker-erforce`、`debug-host` をAppHostへ追加する。外部containerは固定されたSumatra `2025` とER-Force AutoRef `2025.1.0` image tag、host networkを使う。両trackerはSimulatorとGame Controllerの起動後に開始し、DebugHostはvision multicast `224.5.23.2:10020` とtracker multicast `224.5.23.2:11010` を受信する。DebugHost自身のtracker publishは無効にする。

TIGERsのSumatraは `simulation_protocol_comparison` moduliを指定する。Duck側のread-only fixtureはSumatra `2025` の公式 `simulation_protocol.xml` から派生し、referee入力 `NETWORK:11003` とvision/tracker設定を保持しつつ、内蔵Game Controllerとreferee再publishを無効にする。fixtureは `/Sumatra/config/moduli/simulation_protocol_comparison.xml` へbind mountされる。pathの根拠はSumatra 2025のJib build設定と、CraneのTIGERs match composeにあるSumatra config mount。

未対応の `Testing:Mode` 値は、ownership lockまたはresource宣言前に例外で拒否する。

## TDDと検証

- 初回comparison modelの失敗checkpoint: `580b5c6...`; 実装: `bf23e38...`
- Sumatra fixture契約の失敗checkpoint: `d636ac6...`; 実装: `685cc01...`
- 未知modeの失敗checkpoint: `7d726a6...`; 実装: `6571c04...`
- 最新PR #61 headへのrebase後、AppHost model testを再実行: **15 passed, 0 failed, 0 skipped**。
- `git diff --check`: 成功。
- Docker imageのpull/start、multicastの実runtime挙動はこの作業では検証していない。

## 設計との対応と未完了事項

この作業はASPIRE-006Aのresource model部分を固定する。親依存のASPIRE-005 (#37) は未完了のため、Issue #38も依存関係上は完了扱いにしない。三trackerの同時実パケット受信とsource identity確認は後続項目で実施する。Linux runtime受入またはTIGERs vs Crane対戦モードの受入をこのmodel testから推定しない。

根拠:

- [Sumatra 2025 protocol config](https://github.com/TIGERs-Mannheim/Sumatra/blob/0bb4c653ef8611f9e71a880165f101209be97b3c/config/moduli/simulation_protocol.xml)
- [Sumatra 2025 Jib build config](https://github.com/TIGERs-Mannheim/Sumatra/blob/0bb4c653ef8611f9e71a880165f101209be97b3c/build.gradle)
- [Crane TIGERs match Compose config mount](https://github.com/ibis-ssl/crane/blob/af6e0d3dec745415ce060ff5de2042afd3ec5145/docker/match-vs-tigers/docker-compose.yaml)
