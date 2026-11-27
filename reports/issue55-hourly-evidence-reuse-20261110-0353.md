# #55 毎時調査: 既存受入証拠の再利用範囲

- 調査依頼: 2026-11-10 03:50 UTC（ユーザー指定）。
- 前回の毎時調査担当: 2026-11-10 02:53:24 UTC起動、完了済み。次回安全起動時刻を超えたことを確認して新規担当を起動した。
- 指定プロフィール: `gpt-6.1-sol / high`。実適用プロフィールは観測できた場合のみ記載。
- Issue: #55 / ASPIRE-007A。読み取り専用。Docker、workflow dispatch、コード変更、GitHub書込みは禁止。
- 既存作業: PR #123 / branch `task/issue55-aspire-007a`。通常作業担当は#55の初期contextと既存run mappingを完了。今回の調査範囲は別runの証拠再利用判断のみ。

## 調査結果

- 確認時刻: 2026-11-10 03:53〜03:58 UTC。指定プロフィールは `gpt-6.1-sol / high`、実適用モデル／reasoning はランタイムから観測できず**未検証**。
- PR [#123](https://github.com/ibis-ssl/Duck/pull/123) は Draft / Open。ローカル・PR双方のHEADは `badfb36856477f06c50f3b53a7906a0ca2932a20`、baseは `design/issue18-aspire-test-orchestration` / `eebc6c1a4cb139003830730b63591af36e61d75e`。baseとの差分は既存context report 1ファイルのみ。
- 現HEADのPR CI [42752328785](https://github.com/ibis-ssl/Duck/actions/runs/42752328785) は completed / success。通常test jobは成功、NET-002とfull-stack受入jobはともに **skipped**。既存context記載の `6e9c8505...` / run `42751546705` は一つ前のHEADの記録である。

### runの同定と再利用可能範囲

| 項目 | [42370027080](https://github.com/ibis-ssl/Duck/actions/runs/42370027080) | [42720651766](https://github.com/ibis-ssl/Duck/actions/runs/42720651766) |
|---|---|---|
| head SHA / branch | `3239d266df5b888f941189f9c8ff4521894ac333` / `task/issue51-aspire-006g1` | `1d7973b4faa86903b9e1af45236597117cd29c25` / `task/issue54-aspire-006g4` |
| 実checkout / ref | `b14eb2923673ac640dbd47e5693c7e4922e84469` / `refs/pull/117/merge`。既存#51レビューはheadとのtree同一を記録 | headと同じSHA / `refs/heads/task/issue54-aspire-006g4` |
| 保存済み環境 | Linux / Ubuntu 24.04.5 LTS / Docker Engine 28.0.4 | Linux / Ubuntu 24.04.5 LTS / Docker Engine 28.0.4 |
| job結果 | 通常test・full-stack success、NET-002 skipped | 通常test・full-stack・NET-002 success |
| full-stack artifact | `aspire-full-stack-42370027080-1`、ID `13573636939`、17,322,160 bytes | `aspire-full-stack-42720651766-1`、ID `13719599069`、67,083,370 bytes |
| 追加artifact | runtime bundle ID `13573606161`。名称中のSHAはmerge checkout | NET-002 ID `13719183979`、9,584 bytes。runtime bundle ID `13719404712` |
| 限定された判定 | `acceptanceScope=ASPIRE-006G1 / ASPIRE-MATCH-001 and ASPIRE-MATCH-002`、`status=passed`。試合結果は `failed / maximum_time / MaximumTimeReached`、`fullMatchPassed=false` | 同じG1 scopeはpassed。試合結果は `passed / post_game / PostGame`、`fullMatchPassed=true` |
| 再利用先 | MATCH-001/002の過去HEAD構成・team・受信元tuple、SSL保存経路。最大時間終了も元MATCH-005との照合対象 | 過去HEADのNET-002、MATCH-001/002、POST_GAMEと元MATCH-005の終了・保存経路 |

artifact一覧はGitHub APIで再確認し、どちらも未失効だった。内容は既存展開先だけを読んだ。#51は `task\issue51-ci-42370027080\aspire-full-stack-42370027080-1`、#54は `%TEMP%\issue54-ci-42720651766\aspire-full-stack-42720651766-1`。再download、展開、テスト、Docker、dispatch、GitHub書込みは行っていない。

### 既存マトリクスへの補正

比較基準は[Issue #55](https://github.com/ibis-ssl/Duck/issues/55)が指定する元設計 [`dd9101deff40299907e5d9e25664af0fdce4d4e2`](https://github.com/ibis-ssl/Duck/blob/dd9101deff40299907e5d9e25664af0fdce4d4e2/Tracker/Design/Testing/aspire-simulation-test-environment.md)。以下は元条件との差分であり、現設計の追加条件をこの旧runへ遡及して合否判定しない。

1. **NET-003** はDuckからDebugHostへの送信ではなく、RuntimeHostとDebugHostが同じ10020を同一窓で受信して件数が増える条件。**NET-004** はTIGERs/ER-Forceが10020を受信し各tracker出力を生成する条件で、「TIGERs/ER-Force→Game Controller」ではない。NET-005は両trackerの11010をDebugHostが別sourceとして受信、NET-006はDuckの11010をDuck sourceとして受信する条件。今回の2 runをこれらcomparison条件の合格には拡張できない。
2. **NET-007** の元条件はrefereeのHALT→active遷移、その後のCrane→cm4-sim UDP 12345、cm4-sim→Simulator UDP 12346、Vision位置変化。元条件はWindows UI操作を必須としていない。STOPやPOST_GAMEだけでは充足しない。
3. **MATCH-003** は同じactive referee窓で両チームが移動する条件。card／退場の意図的発生は要件にない。両側motionの条件別証拠をこの差分確認では特定しておらず、card未発生を未充足理由にしない。
4. **MATCH-005** は「POST_GAME **または最大試合時間**で終了」し、対戦結果・全resource stdout/stderr・SSL log・Crane記録・GC/AutoRef/Sumatra診断を保存する条件。#51のMaximumTimeReachedだけを理由に未成立とは断定できない。#54の `full-match-gate.txt` は `allNineResourceLogs=complete`、`sslRecorderArchive=finalized`、`craneBag=finalized-and-archived` を記録し、live-log statusは9資源captured。保存済みSSL gzipは12,695,791 bytes、Crane tar.gzは52,902,973 bytes。これらは元MATCH-005の強い過去証拠であり、card／送信process帰属の未確認はこの条件固有の失敗理由ではない。今回読んだresultにはfixture時のscoreはあるが終局scoreを明示した項目は特定しておらず、終局scoreや全診断内容の完全性は追加の読み取り確認事項として残す。
5. **match時のactive-motion step skipとG1 passedは矛盾ではない**。workflowのstep 9内で `verify_aspire_match_acceptance.py` が動き、step 11には `apphost_mode != 'match'` 条件がある。既存#51通常レビューも同じ分岐を説明している。G1 verifierの成功を別のcomparison packet / active-motion suiteやMATCH-003/004の成功へ拡張しない。

### 残る空白と最小の次手

- 2 runとも現HEADの実通信受入ではない。NET-001の要求された比較資源起動、NET-002の現HEAD receiver自身の増分、NET-003〜006の同一窓・source対応、NET-007の制御往復とmotion、NET-008のinterface適用条件、NET-009のstack lock／制御port競合は、今回の過去2 runだけで現HEAD合格にできない。MATCH-004のAutoRef入力・Duck非送信もG1のpassedから推定できない。
- **最小の次手は読取によるcoverage補正**。上記5点を作業contextの判定に反映し、完了済み#50配下のG2/G3結果をMATCH-003/004へ参照し、#54保存済み結果から終局score／診断ファイルの場所を一度確認する。証拠が既にある条件のために旧runを再実行しない。
- その後、受入対象HEADを固定して、未証明のcomparison条件を既存workflowの `comparison` 1実行へまとめる。NET-002専用jobも同じmanual runで動くため別dispatchを重ねない。NET-008/009はその通常経路だけで網羅できるとは扱わず、適用項目を分けて計画する。現HEADのMATCH証拠が必要な分のみ `match` を1実行に集約する。これは実行提案であり、この担当の禁止範囲にあるDocker／Actions起動は行っていない。

今回更新したのは本レポートのみ。既存context本文の訂正、commit、push、PR更新は親担当へ引き継ぐ。
