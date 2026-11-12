# Issue #55 run 43120406926：終了判定とBlue 8の阻害要因

## 対象と参照した証跡

run `43120406926` の既存workflow artifactと、親エージェントが読み取り専用で調べたrosbag測定値を照合した。以下のrun証跡の取得にDocker操作や再実行は行っていない。

artifactは`%TEMP%\issue55-run-43120406926-artifacts\aspire-full-stack-43120406926-1`。主なファイルは`match/match-result.json`、`match/match-acceptance.json`、`match/full-match-gate.txt`、`match/motion-acceptance.json`、`match/participant-status.json`、`match/match-transitions.stdout.log`、`match/ssl-archive-preservation-status.txt`、`match/autoref/*`、および`crane-recording`配下の証跡である。

## MATCH-005の終了点とfull-match判定

Issue #55の受入範囲は、該当する`ASPIRE-MATCH-001`から`ASPIRE-MATCH-005`の通信確認と証跡保存であり、すべての実行が`POST_GAME`で終わることを条件としていない。参照設計（`dd9101deff40299907e5d9e25664af0fdce4d4e2`）は、MATCH-005の終了点を`POST_GAME`または設定済み最大実行時間とし、試合結果、各資源のログ、SSL、Crane、Game Controller、AutoRef、Sumatraの診断情報を保存するよう定めている。

このrunでは`.github/workflows/dotnet-test.yml`が`MatchController__MaximumDuration=00:15:00`を設定していた。`RefereeDriver.ContinueMatchUntilPostGameAsync`はこの時間をwall-clockの実行上限として使い、期限に達すると`MaximumTimeReached`を返す。これはMATCH-005で許容する設定済みrun終了点には該当するが、試合時計が最大時間まで進んだ証拠ではない。保存されたGame Controllerの終了状態は`NORMAL_FIRST_HALF`、`STOP`で、ステージ残り時間は15秒だった。

結果の`status=failed`と`fullMatchPassed=false`は維持する。`RefereeMatchTerminationResult.IsSuccess`は引き続き`PostGame`の場合だけtrueとなり、workerは`MATCH_MAXIMUM_TIME_REACHED`を記録する。MATCH-005の終了点・証跡保存statusは別項目とし、保存された終了種別が結果と一致し、必要なログとアーカイブが最終化された場合だけpassとする。別のfull-match statusには終了時の審判stage、command、残り時間、阻害理由を残す。これにより、MATCH-005の終了点・証跡要件が満たされたことと、試合完了を区別する。

この分離は`POST_GAME`にも適用する。終了点が`POST_GAME`と確認でき、resultの終端情報が相互に整合し、ログとarchiveが最終化されていれば、試合結果が失敗でもMATCH-005の終了点・証跡保存はpassにできる。その場合も`fullMatchPassed=false`と別のfull-match gateの未達statusを維持し、試合全体の成功とは扱わない。

ほかのrun結果も独立して扱う。`match-acceptance.json`がpassした範囲はG1 / MATCH-001・002のみ。`motion-acceptance.json`は291,884件の記録を含み、両チームが動いた時間帯を検出してpassした。最終participant readinessはcompleteで、SSL recorderは正常停止し、archiveも最終化された。

AutoRefについては、`match-result.json`にactive状態とfull container IDが記録され、出力を新たに確認した理由とfresh output時刻`23:53:43.020Z`、snapshot時刻`23:53:43.266Z`が保存されている。ただし、このparticipant-status DTOはsource endpoint、UUID、packet count、network fieldsを保持していない。AutoRef native archiveのtype histogramは全106,453 records、type 3が40,763件、type 4が65,690件、type 5が0件で、archiveはclean EOFで終了していた。したがってtype 5 recordが0件だったことは、AutoRefのUDP入力が0件だった証拠ではない。fresh outputの記録もsource attributionやpacket countを代替しない。次に必要な最小観測は、AutoRefがactiveな間の既存rich participant-status projectionを保持すること。新しい必須条件や追加実行は不要である。

これらの結果をもって、失敗したmatch resultをfull-match passに読み替えることはできない。

## Blue 8の物理状態と続行阻害

`match-result.json`の終了時continuation stateには、`STOP`、`NORMAL_FIRST_HALF`、残り15秒、次のcommandがYellowの`DIRECT`、唯一記録された続行actionが`NEXT_COMMAND=BLOCKED`で、その理由が`Robots too close to ball: 8 BLUE`とある。同じ終了時の審判状態について、別のready actionは記録されていない。

親エージェントは既存のCrane rosbagを、固定済みイメージから読み取り専用で調査した。bagはmetadata 713,584件を含み、`/world_model`と`/sent_robot_commands` topicがあった。`is_yellow=true`では`robot_info_theirs`がBlue側を指す。bag offset 305秒（header時刻`1794440615.9837`）では、ballはおよそ`(-2.5, 0.0917322)`、Blue 8は`(-2.61529, -0.011887)`、速度`(-0.27043, -0.20953)`、速さ`0.3421`だった。offset 914秒（header時刻`1794441224.9891`）ではballは同じ位置、Blue 8は`(-2.599976, -0.0000167)`、速さは約`8.6e-17`だった。Blue 8がballから約0.136 mの位置に残り、10分以上後にはほぼ停止していたことが分かる。

この証拠はGame Controllerが報告した近接阻害と整合する、継続した物理状態を示す。一方、Crane bagからはTIGERsがBlue 8へ送った指令、Blue 8がそれを受け取ったか、停止した理由までは分からない。`/sent_robot_commands`はBlue 8自身の送信制御commandではない。このbagだけからTIGERs planner、transport、simulator制御の障害を推定しない。

未コミットの`Testing/Duck.Testing.RefereeDriver/RefereeDriver.cs`と`Tracker/Tracker.Tests/RefereeDriverFixtureTests.cs`には、停止中の通常ハーフで`stageTimeLeft <= 0`の場合に`NEXT_STAGE` / `END_GAME`を選ぶ差分がある。このrunは残り15秒だったため、この差分では当該runの終了状態を解消できない。`ContinueMatchUsesOfferedNextStageAtRoundedZeroAndDoesNotChooseLaterKickoff`テストは合成した`NEXT_STAGE=READY_MANUAL`状態でaction選択を確認するものであり、このrunのstage/counterでそのactionが提示されたことや`POST_GAME`への到達を示してはいない。この2ファイルの差分は本報告より前から存在し、ここでは変更も検証もしていない。別目的でレビュー・検証するまでは今回のcommit対象に含めない。

## 次に必要な最小証拠

Blue 8がstop radius内に残った理由を確認するには、TIGERs側でBlue 8に対するrobot別target/skillまたはsimulator向け送信commandを取得し、観測poseとGame Controllerの続行阻害時刻に対応付ける必要がある。既存rosbagとテキストログにはそのsource側の証拠がない。現在のartifactだけでは挙動変更の根拠にならない。

## MATCH-005 status modelの実装

`MatchControllerResult`は`match005EndpointReached`、`match005EndpointKind`、`configuredMaximumDuration`、`fullMatchPassed`を独立して記録する。watchdog終了は`maximum_duration_watchdog`と明記し、結果をfailed、`fullMatchPassed=false`のままにする。workflow gateは、保存された15分の設定時間と終了情報が一致することを確認したうえで、必要な資源ログ、SSL archive、Crane archive、sanitize結果、terminal transition証跡の最終化を要求する。終了点・証跡保存statusとfull-match statusは別々に記録し、Actions summaryにも区別して表示する。full-match statusには審判stage、command、残り時間、阻害理由を残すため、成功したMATCH-005終了点判定を試合完了と誤認しない。

ローカル検証では`dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -c Release --filter FullyQualifiedName~MatchControllerWorkerTests`が13/13 pass、`dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore -c Release`が0 errorで成功した。このstatus model変更についてworkflow、Docker、match runは起動していない。
