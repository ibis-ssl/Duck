# CRANEロボット停止の原因調査

調査日: 2026-10-04 20:21 JST

## 対象と結論

親PRはDuck #28、直接の調査対象は進行中のIssue #37（ASPIRE-005）とDraft PR #61。
対象ブランチは `task/pr28-aspire-005`、調査時のcurrent HEADは `d48938896bcb65bfce3f656f7df4afd576522fe3`。
親PR #28の確認時HEADは `02ed5713f14200f14939a2ecf27be41eb0fce79a`。

**Game Controllerの準備判定が、実際には存在しないJavaプロセスを要求する誤設定を特定した。**
固定イメージはGoバイナリ `/app` を起動するが、AppHostは `ExpectedProcessName = "java"` を渡す。
この条件ではGame Controllerのwrapperが準備完了にならず、`WaitFor(gameController)` を持つCRANEの起動を阻止する。
保存済みCI artifactでCRANEの起動記録・イメージ・コンテナが見つからない状況と整合する。

これはコードと固定版ソース・既存実行証拠の照合による診断であり、修正後の実動作成功は未確認。
稼働中プロセスの `docker top` やwrapperのHTTP応答そのものは既存artifactにないため、実行時判定値の直接観測とは区別する。

## 実行環境と調査範囲

- 読取先: RDMCP接続先FA780、既存認証による `gh` と `rg` を使用。
- 独立した調査worktree: `C:/Users/donabe/RemoteDesktopWorkspace/Duck-pr61-crane-diagnosis-20261004`。
- 証拠保存先: `C:/Users/donabe/RemoteDesktopWorkspace/_diagnostics/duck-pr61-crane-20261004`。
- 既存の実装worktree、PR #61の実装ブランチ、製品・テスト・workflowは変更しない。
- テスト再実行、コンテナ起動・停止、依存導入、daemon設定変更、mergeは実施しない。
- FA780の今回の実行環境では `docker` がCommandNotFoundExceptionとなった。Docker未導入とは断定せず、既存CI証拠を用いた。

## 現HEADに一致するCI証拠

[run 37195526353](https://github.com/ibis-ssl/Duck/actions/runs/37195526353) はpushイベント。
`gh pr view 61` の `headRefOid`、runの `headSha`、artifact内の `sha` がすべて調査HEADと一致することを確認した。
過去の別HEADのrunは現在のCI結果に代用していない。HEADは証拠取得後にも再取得して不変を確認した。

| 項目 | 確認結果 |
| --- | --- |
| 通常test job | success（job 111416478114） |
| ASPIRE-NET-002 | skipped |
| full-stack job | failure（job 111416620510） |
| 到達段階 | `active_robot_motion_wait` |
| 審判遷移 | HALT → PrepareKickoffYellow → NormalStart |
| Vision | テストが3,757 datagramを消費 |
| 移動 | Yellowのロボットが60秒以内に100 mm移動する条件を満たさず失敗 |
| Duck tracker | 受信検証段階へ未到達。0件受信と解釈しない |

artifact: [aspire-full-stack-37195526353-1](https://github.com/ibis-ssl/Duck/actions/runs/37195526353/artifacts/11301315776)

- ID: `11301315776`、サイズ: `41631` bytes。
- GitHubが返したdigest: `sha256:982b1d0dc33cc7ca6c59563be1ac3e275995e485c60b002851f72f75f46f1226`。
- 主要証拠: `acceptance-result.json`、`game-controller.inspect.txt`、`dcp-cleanup/dcp-log-tails.txt`、Game Controllerのcontainer inspect。
- 失敗時のTRX、標準出力、標準エラー、AppHost/DCP/コンテナログを保存する既存workflowを確認。今回はworkflowを変更していない。

## CRANE-START-001: Game Controllerのプロセス名誤設定

重要度: high。状態: 未修正。対象: PR #61の調査HEAD。

1. [Program.cs:54–69](https://github.com/ibis-ssl/Duck/blob/d48938896bcb65bfce3f656f7df4afd576522fe3/Testing/Duck.Testing.AppHost/Program.cs#L54-L69) はGame Controllerのwrapperへ `"java"` を渡す（68行）。
2. [DockerContainerWrapper.cs:301–344](https://github.com/ibis-ssl/Duck/blob/d48938896bcb65bfce3f656f7df4afd576522fe3/Testing/Duck.Testing.AppHost/DockerContainerWrapper.cs#L301-L344) は `docker top` の出力にその文字列があることを要求し、`processReady && serviceReady` が真の場合だけ準備待ちを終了する。
3. [同ファイル:97–99](https://github.com/ibis-ssl/Duck/blob/d48938896bcb65bfce3f656f7df4afd576522fe3/Testing/Duck.Testing.AppHost/DockerContainerWrapper.cs#L97-L99) でその後にだけ `ready.Set()` を呼ぶ。未完了の `/health/ready` は503（242–245行）。
4. [DockerWrapperResourceExtensions.cs:31–46, 63–65](https://github.com/ibis-ssl/Duck/blob/d48938896bcb65bfce3f656f7df4afd576522fe3/Testing/Duck.Testing.AppHost/DockerWrapperResourceExtensions.cs#L31-L65) がHTTP応答をAspireのhealthへ反映する。
5. [Program.cs:119–121](https://github.com/ibis-ssl/Duck/blob/d48938896bcb65bfce3f656f7df4afd576522fe3/Testing/Duck.Testing.AppHost/Program.cs#L119-L121) でCRANEはGame Controllerのhealthyを待つ。

実行証拠では `robocupssl/ssl-game-controller:3.20.3` の `Path` と `Entrypoint` は `/app`、起動引数はvision/tracker/referee/APIの設定だけだった。
`v3.20.3` のcommitは `8050f232c3130323bbd91d1d3d56e9553506c8e4` とghで確認。
[固定版Dockerfile](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/cmd/ssl-game-controller/Dockerfile#L7-L26) はGoでビルドし、最終Alpineイメージへバイナリを `/app` としてコピーして直接起動する。Javaを起動する定義ではない。

したがって、通常の固定イメージ起動ではJava必須判定が成立しない。CRANEはまだ制御指令を出す段階に到達できない。
この阻害条件は確認できるが、修正後に他の阻害条件が残らないことまでは保証しない。

必要な対応は、固定イメージの実プロセス `/app` と整合する所有コンテナ内の検査へ修正すること。
Java導入、準備判定の削除、CRANEの依存待ち削除による回避はしない。

## CRANE-START-002: 全構成の準備完了前に移動試験を開始

重要度: medium。状態: 未修正。対象: 同じHEADのworkflow。

[dotnet-test.yml:440–523](https://github.com/ibis-ssl/Duck/blob/d48938896bcb65bfce3f656f7df4afd576522fe3/.github/workflows/dotnet-test.yml#L440-L523) はGame Controllerコンテナの `State.Status == running` だけで起動gateを成功にし、移動受入へ進む。
CRANEのwrapper ready、CRANEコンテナの起動完了、所有者に対応した全構成の準備完了はこのgateの条件にない。
結果として、CRANEを起動できない問題が `UDP_MOTION_FAILURE` として現れる。
CRANEの準備完了を含むgateを設け、移動閾値・判定時間を緩めず、起動待ちの失敗と移動の失敗を区別する必要がある。

### Service Readyをアプリのhealthと同一視しない

保存済みDCPログとinspectの時系列:

| UTC | 事実 |
| --- | --- |
| 10:31:06.706 | Game Controller wrapperの起動記録 |
| 10:31:06.797 | DCPの `Service game-controller ... Ready` |
| 10:31:08.156 | Game ControllerコンテナのStartedAt |
| 10:31:09 | workflowのGame Controller起動gate完了 |
| 10:31:17.013 | Duck RuntimeHostのプロセス起動記録 |
| 10:32:14 | 移動試験が失敗しcleanupへ移行 |

DCPのService Readyはコンテナ起動より前に記録されている。この記録からGame Controllerのwrapper health成功を主張できない。
またwrapperの準備待ち既定値は180秒であり、移動試験の60秒失敗が先に終了処理を始めるため、準備待ちtimeoutのログがないことは今回の不整合を否定しない。

## 既存テストで検出されなかった理由

[AppHostApplicationModelTests.cs:70–106](https://github.com/ibis-ssl/Duck/blob/d48938896bcb65bfce3f656f7df4afd576522fe3/Tracker/Tracker.Tests/AppHostApplicationModelTests.cs#L70-L106) は固定image、起動引数、審判送信元、healthy依存を検査するが、Game Controllerの `ExpectedProcessName` を固定イメージの実体に照合していない。
モデル全体の引数シリアライズ整合性を検査しても、同じ誤った設定値をシリアライズした結果の一致ではこの誤設定を検出できない。
通常test jobの成功は、全構成の起動・移動成功を意味しない。

## 実装担当への引継ぎ

対応先は既存のIssue #37 / PR #61。新しい製品修正Issueや別の実装は本調査では作成しない。

1. 実AppHostの構成からGame Controllerのlaunch optionsを取り、実イメージのプロセス名との整合を検査するテストを追加する。現在の `java` で自然に失敗することを確認してから最小修正する。
2. プロセス判定に加え、既存の所有者検証、API・審判通信、初期状態と起動後の状態遷移を維持する。
3. CRANEのreadyを含む受入開始gateを追加する。失敗時は所有資源のプロセス一覧、health応答、依存待ち状態を終了処理前に保存する。
4. 修正後のcurrent HEADに一致するrunでCRANE起動、active motion、Duck tracker出力を検証する。調査HEADのrunを修正後の成功証拠に流用しない。

本調査では修正もテスト再実行もしていない。修正後の動作、残る他の準備条件、UDP指令経路、対戦・OS受入は未検証のまま。
詳細reportのみ独立した診断ブランチへ保存し、PR #61へ簡易報告する。実装ブランチは更新せず、mergeもしない。
