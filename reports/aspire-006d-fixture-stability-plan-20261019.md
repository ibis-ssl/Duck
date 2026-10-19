# 全体試験でCraneのチーム認識と審判状態を確かめる

状態: 一部実装済み。試験開始前のチーム認識は確認済みです。移動中の審判状態監視とAutoRef対策は未実装です。
Issue: #75（親 #18）
関連: #37、#41、PR #74、PR #76
分割した子Issue: #77（PR #76で実装済み）、#78（未着手、#77とPR #76完了後に着手）
base: `design/issue18-aspire-test-orchestration`
base HEAD: `a5105173bb2527f0d7147eb44eeca562a8bd3033`
作業ブランチ: `task/aspire-fullstack-fixture-stability`
検証したコードHEAD: `96b70b648c9341a6c349bedc90182219e3afb8f7`

## 変更前の困り事

全体受入テストは、審判状態がHALTから始まることを一度確認した後、Vision入力からYellowロボットが100 mm以上動くまで待っていました。以前の実行では、Game Controllerが配信するチーム名が`Unknown`のまま試験が進み、Craneが自チームを認識したか確かめていませんでした。また、ER-Force AutoRefの提案が適用されてGame ControllerがHaltへ戻った際も、試験は状態の中断を検出せず、最後に一般的な`UDP_MOTION_FAILURE`で失敗していました。

## 今回実装した範囲

Craneの`team`引数はチーム名、`team_color`引数は初期色として分け、既定値をそれぞれ`crane`、`YELLOW`にしました。`Testing:Crane:TeamName`はCraneとGame Controllerの両方へ渡します。値はASCII英数字、`_`、`-`に限り、それ以外はAppHost起動時に拒否します。

Game Controllerの公式`UpdateTeamState` APIでYellowのチーム名を設定し、審判データグラムに期待名が現れるまで準備完了にしません。送信は1秒間隔で最大3回です。現在受信している名前が期待値と一致する間は送信しません。後続データグラムで一致しなくなれば、残り回数内で再送できます。

Yellow情報のない審判データグラムは不健康な状態として扱います。次に期待名を含む有効なデータグラムを受けると、準備状態が回復します。全体受入テストは、ロボット移動前にGame ControllerのYellowチーム名を確認する構成です。

## 残っている作業

Issue #75本文は、移動中も審判状態を監視し、試合進行中の状態を失った場合に審判コマンドとカウンターを添えた専用の失敗理由を記録することを求めています。また、Game Controller 3.20.3で確認した試験専用の制御方法を使い、AutoRefの提案によって試験がHaltへ戻る状況を避けることも完了条件です。今回の変更は移動前のチーム認識までで、これらの機能は実装していません。

移動中の状態喪失を検出し、専用理由と審判履歴を記録する変更、AutoRef提案を抑える確認済みの試験設定、そして両者の受入試験が残っています。これらは未着手の子Issue #78で追跡します。分割を反映済みです。子Issue #77は試験開始前のチーム名一致、子Issue #78は移動中の審判状態監視とAutoRef対策を追跡します。#78は#77およびPR #76の変更が親baseへ取り込まれた後に着手します。

## 対象外

本番の試合設定、Game Controller/AutoRef本体、Tracker UI、Issue #41のsnapshot pair・時刻差表示、依存関係、権限、ネットワーク設定は変更していません。新しい依存関係は追加していません。

## 現HEADの検証結果

HEAD `96b70b648c9341a6c349bedc90182219e3afb8f7`で確認しました。AppHostと全体受入用Tracker.Testsのビルドはいずれも成功。関連テストは54件すべて通過し、Tracker.Tests全体は447/447、比較モードの全体受入は2/2でした。

受入ではHALT→PrepareKickoffYellow→NormalStartを確認し、Yellowロボットは112.08897 mm移動しました。5秒の比較測定でDuck 156フレーム、TIGERs 812フレーム、ER-Force 1000フレーム、Vision 313データグラムを取得しました。評価1000回のP50は179.923 µs、P95は298.002 µsでした。終了後、対象スタックのコンテナは0件、AppHostプロセスは終了しました。

ビルド警告はNU1902（MessagePack 2.5.302）とASPIRE010（Aspire CLI bundle無効）です。正式なMarkdown lintは未完了です。既存venvのyomiyasu_lint.pyでは100/100、指摘なしでした。WSLのnpmがWindows shimへ解決され、同一HEADのWindows作業場所にも`node_modules`と`textlint`がないため、依存を追加せず実行を見送っています。`git diff --check`は成功しました。

追加の`capture_tracker_vision_inputs.py`はTIGERsプロセスへの`strace`接続にsudoの対話認証を要求したため実行していません。受入テスト内の比較測定は完了しています。

## 以前の検証記録

初期実装時は、対象テスト群47/47、Tracker.Tests 440/440、Yellowロボット移動100.31528 mmでした。これは現HEADの結果ではありません。現HEADの最新数値は上の「現HEADの検証結果」に記載しています。

## Issue #75本文との対応

Issue #75はチーム認識に加えて、移動中の試合進行状態の監視、状態中断の専用失敗理由と審判履歴、AutoRef提案への試験用対策を完了条件に含めています。現在の実装と受入成功だけではIssue全文を満たしません。PR #76で`Closes #77`を維持するなら、元の完了条件は変更せず、#77と#78の両方が完了するまでIssue #75は未完了のままです。#77はPR #76の試験開始前チーム認識の実装を追跡し、PR本文はCloses #77のみを記載します。#78は移動中の審判状態喪失検出と試験専用AutoRef対策を追跡し、#77およびPR #76の変更が親baseへ取り込まれた後に着手します。PR #76のbaseは非既定ブランチなので、GitHubによる#77の自動closeは保証されません。
