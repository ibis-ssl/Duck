# Issue #55 / ASPIRE-007A 作業コンテキスト

- 対象: [ibis-ssl/Duck#55](https://github.com/ibis-ssl/Duck/issues/55) / ASPIRE-007A
- Issue: `[ASPIRE-007A] Linux実機のネットワーク受入を完了する` / Open、Duck Project In progress
- 指定プロフィール: `gpt-6-luna / high`。実際に適用されたモデル・reasoningは観測できていない。
- リポジトリ／作業場所: `ibis-ssl/Duck` / `C:\Users\donabe\Documents\Codex\2026-11-07\task\Duck-issue55-aspire-007a`
- ブランチ／base: `task/issue55-aspire-007a` / `design/issue18-aspire-test-orchestration`
- PR #123 の作業開始時HEAD: `6e9c8505a6e727e7d9bd7bbd88209889df97ac77`（この記録の初版を含むtracking-only commit）。
- 担当調査: 依存、設計、workflow、既存runの読み取り確認。コード、Docker、workflow dispatchの実行なし。

## 受入範囲

Issue #55 は、Linux + Docker Engineで該当する `ASPIRE-NET-001`〜`009` と対戦モードの `ASPIRE-MATCH-001`〜`005` を実packetで確認し、OS/Docker版、ネットワーク設定、packet件数、送信元、stdout/stderrおよび失敗時の途中証跡を保存する。未実施・skip・失敗を成功として扱わない。

依存Issue #42（ASPIRE-006E）と#50（ASPIRE-006G）はClosed / Done。tasks-status.mdも#55の依存と実packet条件を同様に記載している。

設計書の製品実行案はWindows + Docker Desktopで、利用者管理のUbuntu Docker Engineをその製品経路に含めない。一方、Linux/macOSの互換性はOSごとの別受入で確認するとしている。したがってLinux Docker Engineは#55のOS別受入対象であり、製品実行経路変更との矛盾はない。Linux結果をWindows Docker Desktop受入へ代用してはならない。#55は受入専用で、製品実装を重複させるIssueではない。

## 既存の実行経路

`.github/workflows/dotnet-test.yml` にはLinux実行経路がある。

- `ASPIRE-NET-002 Linux packet flow` は `workflow_dispatch` のとき実行。
- `ASPIRE full-stack Linux acceptance` は `workflow_dispatch` と限定されたpush/PRブランチで実行。手動dispatchには `comparison` と `match` のmodeがあり、`ubuntu-24.04` 上でDocker・ネットワーク事前情報と受入artifactを記録する。
- #55の作業ブランチは自動full-stack対象のallowlistに入っていない。PR CIだけでは受入jobがskipになる。
- 現在の依頼にはPR作成・commit・push・commentの許可がある。Docker許可として確認できるのは#50配下に限る既存指示で、#55のmanual dispatchを実行する個別許可は未確認。したがってこの作業ではdispatchもDockerも起動していない。

## #55の元条件と既存runの照合

次の2 runはどちらも#55の現在HEADではない。再利用できる狭い証拠と未充足を分ける。run成功やartifactの一部PASSをIssue全体の成功とは扱わない。

| 元条件 | run 42370027080 — #51 G1、SHA `3239d266df5b888f941189f9c8ff4521894ac333` | run 42720651766 — #54 PR、SHA `1d7973b4faa86903b9e1af45236597117cd29c25` |
|---|---|---|
| NET-001 | 資源準備・match設定が見える範囲。setup証拠のみ。 | 期待された7資源のreadyと設定。readiness証拠のみ。 |
| NET-002 | 独立packet-flow jobはskip。 | Linux packet-flow job成功。ただしprior-HEADの狭いNET-002証拠。 |
| NET-003 | Duck→DebugHostの条件別証拠なし。 | Duck→DebugHostの条件別証拠なし。 |
| NET-004 | TIGERsとER-Forceが同じ10020 multicastを受信し、tracker outputを生成する条件。条件別証拠なし。 | 同条件の証拠はscope別に未確認。 |
| NET-005 | TIGERs / ER-Forceが11010へ送り、DebugHostが別sourceとして受ける条件。条件別証拠なし。 | 同条件の証拠はscope別に未確認。 |
| NET-006 | Duckが11010へ送り、DebugHostがDuck sourceとして受ける条件。条件別証拠なし。 | 同条件の証拠はscope別に未確認。 |
| NET-007 | 初期STOPとreferee UDP観測はあるが、HALT→active遷移後のCrane→cm4-sim→Simulator UDPとVision motionの全条件を示さない。 | POST_GAMEとmotion記録はあるが、制御遷移・12345/12346転送の条件別証拠は特定できていない。 |
| NET-008 | interface address・代替network経路のmatrixなし。 | 同じmatrixなし。 |
| NET-009 | 複数AppHost分離、port競合、multicast条件なし。 | 同じ条件なし。 |
| MATCH-001 | run scopeはASPIRE-006G1 / MATCH-001・002。resource/team設定とreadinessを参考再利用可能。 | artifactのreadiness/setupは参考情報だが、現在HEADのMATCH-001証拠ではない。 |
| MATCH-002 | 初期STOP、観測window中のUDP送信元tuple一意性、packet記録あり。MaximumTimeReachedで終了し、送信process/container帰属は不明。 | 初期STOPとmatch setupあり。送信process帰属は不明。 |
| MATCH-003 | 同一active referee window内でYellow CraneとBlue TIGERs双方のVision位置変化を要する。#51 G1 scopeでは証明しない。 | motion archiveはあるが、同一window双方の位置変化を条件別に確認した記録はこの照合では特定していない。card/退場発生は要件外。 |
| MATCH-004 | AutoRef/tracker routingとDuck非送信の条件別証拠なし。 | 同じ条件別証拠なし。 |
| MATCH-005 | 元条件はPOST_GAMEまたは最大試合時間で終了し、結果・全resource stdout/stderr・SSL・Crane記録・GC/AutoRef/Sumatra診断を保存する。MaximumTimeReachedだけを理由に不成立としない。#51 G1 artifactで必要証跡一式を満たすかは本照合では未確認。 | POST_GAME、9資源ログ、SSL/Crane archiveが既存artifactにあるため、終了・保存経路の強い過去証拠として再利用可能。match-result.jsonの終局result欄はこの照合では精査していない。cardや送信process帰属はMATCH-005の追加条件ではない。 |

### run metadata上の注意

- run 42370027080 はworkflow全体がsuccessでも、試合結果は `MaximumTimeReached` / `fullMatchPassed=false`。MATCH-005の終了条件は最大時間も許すため、この結果だけで同条件不成立とは言えない。必要な結果・ログ・archive一式が揃うかは#51 G1 artifactの内容と照合する。
- run 42720651766 はworkflow_dispatchのLinux / Ubuntu 24.04.5、Docker Engine 28.0.4。artifactにPOST_GAME、9資源text log、SSL/Crane archiveが記録される。
- run 42720651766のmatch modeでは「Run full-stack packet and active-motion acceptance」stepのskipはworkflow条件上の想定分岐で、同じjobの既存stepでG1 verifierが動く。artifactの `acceptanceScope=ASPIRE-006G1 / MATCH-001 and MATCH-002` を他のMATCH条件へ拡張しない。

再利用可能な範囲は資源/team設定・readiness、run 42720651766のprior-HEAD NET-002、各資源ログとSSL/Crane archiveの保存経路、G1のMATCH-001/002 evidence、さらにMATCH-005の終了・保存証拠候補。run 42370027080のMaximumTimeReachedも元MATCH-005条件に照らす候補。欠けるのは現在HEADの受入とNET-001〜009全体、MATCH条件ごとの証拠整理。どちらのrunも単独で#55全体を完了させない。

## 現在のPRとCI

- Draft PR: [#123](https://github.com/ibis-ssl/Duck/pull/123)、baseは`design/issue18-aspire-test-orchestration`、Issue #55をPR本文でRefsしIssue側にもリンクコメント済み。
- tracking report初版以外の実装変更なし。
- この証拠表更新前のHEAD `badfb36856477f06c50f3b53a7906a0ca2932a20` に対するPR CI [42752328785](https://github.com/ibis-ssl/Duck/actions/runs/42752328785) はsuccess。`.NET tests` pass。NET-002とfull-stack受入はskip。この記録修正は新しいreport-only commitになるため、更新後HEADの通常CI guardは別途確認する。

## 次の最小手順・許可境界

1. 完了済み#50のG2/G3記録をMATCH-003/004の元条件へ対応づけ、#54保存artifactをMATCH-005のresult・診断保存要件に対して読み取り確認する。条件固有の証拠がある場合はrunを重ねない。
2. 現HEADで不足するcomparison条件を既存workflowの`comparison` modeへまとめ、NET-001〜009のどれを実測できるか事前に対応づける。NET-008/009が通常comparison経路だけで満たせるとは仮定しない。
3. 現HEADで不足するMATCH条件を`match` modeへまとめる。`MaximumTimeReached`はMATCH-005の許容終端として扱い、結果・全resource logs・SSL/Crane/diagnosticsを確認する。
4. #55のLinux Engine workflow dispatch許可は現時点で未確認。以前のDocker許可は#50配下を対象としており、新しい#55への適用は確認できないため、明示があるまでdispatch / Dockerは行わない。

Issue本文は特定Linux hostを指定せず、既存workflowはGitHub-hosted `ubuntu-24.04`を使用する。ユーザーが別のhostを想定しているかは未確認。