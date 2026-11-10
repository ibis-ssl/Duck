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
| NET-004 | TIGERs/ER-Force→Game Controllerの条件別証拠なし。 | 同じ条件別証拠なし。 |
| NET-005 | tracker送信元識別の条件別証拠なし。 | 同じ条件別証拠なし。 |
| NET-006 | Duck→DebugHost feedbackの条件別証拠なし。 | 同じ条件別証拠なし。 |
| NET-007 | 初期STOPとreferee UDP観測はあるが、UI/API遷移とmotion確認全体の証拠ではない。 | POST_GAME到達は記録されたが、条件所定のUI/API遷移とmotion確認の証拠は特定できていない。 |
| NET-008 | interface address・代替network経路のmatrixなし。 | 同じmatrixなし。 |
| NET-009 | 複数AppHost分離、port競合、multicast条件なし。 | 同じ条件なし。 |
| MATCH-001 | run scopeはASPIRE-006G1 / MATCH-001・002。resource/team設定とreadinessを参考再利用可能。 | artifactのreadiness/setupは参考情報だが、現在HEADのMATCH-001証拠ではない。 |
| MATCH-002 | 初期STOP、観測window中のUDP送信元tuple一意性、packet記録あり。MaximumTimeReachedで終了し、送信process/container帰属は不明。 | 初期STOPとmatch setupあり。送信process帰属は不明。 |
| MATCH-003 | 試合完了・条件別active-motion証拠なし。MaximumTimeReached。 | POST_GAMEだがcardを意図的に発生させていないため、制御されたMATCH-003結果とは扱わない。 |
| MATCH-004 | AutoRef/tracker routingとDuck非送信の条件別証拠なし。 | 同じ条件別証拠なし。 |
| MATCH-005 | 未成立。MaximumTimeReachedでPOST_GAMEなし。 | 選択されたPOST_GAMEとartifactは試合終了・記録保存経路の一部に再利用可能。9資源のtext log、SSL/Crane archiveの記録あり。ただし退場イベントは意図的に発生させず、送信process帰属も不明。完全な条件別受入ではない。 |

### run metadata上の注意

- run 42370027080 はrun全体はsuccessだが、full-stack結果は `MaximumTimeReached`、`match-result.json` はPOST_GAME前に停止と記録。workflow全体の緑を試合受入PASSと読み替えない。
- run 42720651766 はworkflow_dispatchのLinux / Ubuntu 24.04.5、Docker Engine 28.0.4。artifactにPOST_GAME、9資源text log、SSL/Crane archiveが記録される。
- ただしGitHub job metadataでは「Run full-stack packet and active-motion acceptance」stepがskip。既存artifactのmatch-acceptance/result JSONは `passed` / `POST_GAME` と記し、scopeをASPIRE-006G1 / MATCH-001・002と表示する。このstep/artifact間の不一致を限界として残し、いずれかだけを根拠に全条件達成としない。

再利用可能な範囲は資源設定・readiness、artifact/各資源ログの収集経路、run 42720651766のprior-HEAD NET-002限定結果、同artifactのPOST_GAME/保存結果。欠けるものは現在HEADの結果、NET-001〜009の体系的確認、MATCH-001〜005各条件を意図した制御受入である。どちらのrunも#55を完了させない。

## 現在のPRとCI

- Draft PR: [#123](https://github.com/ibis-ssl/Duck/pull/123)、baseは`design/issue18-aspire-test-orchestration`、Issue #55をPR本文でRefsしIssue側にもリンクコメント済み。
- tracking report初版以外の実装変更なし。
- PR #123のexact-HEAD CI run [42751546705](https://github.com/ibis-ssl/Duck/actions/runs/42751546705) は `6e9c8505a6e727e7d9bd7bbd88209889df97ac77` でsuccess。`.NET tests` pass。NET-002とfull-stack受入はskip。このrunは受入成功ではなく通常CIガード。

## 次の最小手順・許可境界

1. 現在HEADに対し既存 `workflow_dispatch` の`comparison` modeを実施し、artifactとfail diagnosticsを確認する。
2. その結果が明らかにmatch継続を妨げない場合に`match` modeを別実行し、各modeの成否を分けて報告する。
3. issueが指定するLinux EngineとしてGitHub-hosted `ubuntu-24.04` runnerを使う場合、現在の証拠ではworkflow dispatchの個別許可が未確認。ユーザーの以前のDocker許可は#50配下を対象としており、新しい#55への適用は確認できていない。許可を確認するまでDocker/Actions dispatchは行わない。

ユーザーが指定する別のLinux hostがあるかも未確認。Issue本文自体は特定ホスト名を指定せず、既存workflowはGitHub-hosted `ubuntu-24.04`を使う。