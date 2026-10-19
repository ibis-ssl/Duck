# 全体試験でCraneのチーム認識と審判の動作状態を安定させる

- 状態: 実装済み。AppHost build、全体受入テストbuild、Focused 47/47、Tracker.Tests 440/440、全体受入2/2を確認。
- Issue: #75（親 #18）
- 関連: #37、#41、PR #74
- base: `design/issue18-aspire-test-orchestration`
- base HEAD: `a5105173bb2527f0d7147eb44eeca562a8bd3033`
- 対象branch: `task/aspire-fullstack-fixture-stability`

## 変更前の困り事

全体受入テストは初期HALTを確認した後、GameControllerのチーム名が `Unknown` のままCraneを起動し、Vision入力からYellowロボットが100 mm以上動くまで待っていた。以前の保存ログには、ER-Force AutoRefの提案適用後にGameControllerがHaltへ戻り、最後に `UDP_MOTION_FAILURE` が記録された形跡もある。

## できること

試験用構成ではCraneのteam引数をチーム名 `crane` にし、色は別の `team_color` 引数で指定する。GameControllerのreferee datagramでYellowのチーム名が `crane` になるまでreadyとせず、Crane側のROS parameterも確認する。受入テストは移動を始める前にGameControllerのチーム名を検証する。

## 動作の要点

1. 固定版GameController 3.20.3の公式APIと固定Craneイメージのlaunch helpを確認した。以前の `team:=Yellow` は色指定ではなく、チーム名に `Yellow` を設定する誤設定だった。Craneの `team` はチーム名、`team_color` は初期色であり、今回の起動値は `team:=crane team_color:=YELLOW` とした。
2. AppHostは `Testing:Crane:TeamName` と `Testing:Crane:TeamColor` を分けて読み、既定値を `crane` と `YELLOW` にした。起動後のprobeは `/world_model_publisher` の `team_name` が期待値に一致することと既存coordinatorを確認する。
3. GameController readiness monitorはHALTを受信した後に公式 `UpdateTeamState` control APIでYellowの名前を設定し、配信datagramに期待名が現れるまでhealthyにしない。
4. UDP adapterはYellowのteam nameをsnapshotに保持する。全体受入テストは初期HALT後、active状態へ進む前にチーム名 `crane` をassertする。
5. 手動base-mode起動ではcomparison resourcesがなく、TIGERs/ER-Forceの入力検査とAppHost process recordの検査が失敗した。AppHostをcomparison modeで起動し、必要なprocess recordも作成して再実行すると、readiness helperと全体受入2/2が成功した。refereeはHALT→PrepareKickoffYellow→NormalStartへ進み、Yellow robotの移動100.31528 mmとDuck・TIGERs・ER-Forceのtracker入力を記録した。

## 対象外/後続

本番の試合設定、GameController/AutoRef本体、Tracker UI、#41のsnapshot pair・時刻差表示は変更しない。依存、権限、ネットワーク設定も変更しない。AutoRef提案を抑止する設定やactive審判状態を移動中に別途監視する変更は今回実装していない。受入ではAutoRefによるHaltを観測せず、移動条件を達成した。

## 検証済み/未検証

固定Craneイメージのlaunch helpは `team` をチーム名、`team_color` を初期referee colorと説明する。隔離した実イメージ起動では `/world_model_publisher` の `team_name` が `crane`、`initial_team_color` が `YELLOW` と返ることを実測した。固定GameController v3.20.3のAPIにはYellow向け `UpdateTeamState.team_name` がある。AppHost build、`RunAspireFullStackAcceptance=true` を指定したTracker.Tests build、Focused suite 47/47、Tracker.Tests 440/440、comparison mode readiness、全体受入2/2が成功した。全体受入ではYellow robot 100.31528 mmと3系統のtracker入力を確認した。ビルド警告はNU1902（MessagePack 2.5.302）とASPIRE010（Aspire CLI bundle無効）。

比較measurementでは5秒の入力窓でDuck 150 frames、TIGERs 800 frames、ER-Force 1000 frames、Vision 313 packetsを取得した。evaluation 1000 callsのP50は158.173 µs、P95は283.204 µs。実行ログと `acceptance-result.json` は `artifacts/aspire-full-stack-comparison-local` に保存した。AppHost停止後、今回のstackに属するコンテナはDockerから削除され、AppHost PIDは終了した。

## 必要判断と影響

この変更は試験用構成とreferee test adapterに限定する。全体受入では指定したチーム名、active refereeへの遷移、100 mm以上の移動、比較用tracker/vision経路を確認した。試験専用設定以外の動作は変更していない。
