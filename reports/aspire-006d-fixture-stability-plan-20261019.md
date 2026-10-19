# 全体試験でCraneのチーム認識と審判の動作状態を安定させる

- 状態: 実装予定／未実装／未検証（このPRの初期Draft時点）
- Issue: #75（親 #18）
- 関連: #37、#41、PR #74
- base: `design/issue18-aspire-test-orchestration`
- base HEAD: `a5105173bb2527f0d7147eb44eeca562a8bd3033`
- 対象branch: `task/aspire-fullstack-fixture-stability`

## 変更前の困り事

全体受入テストは `Halt → PrepareKickoffYellow → NormalStart` を一度確認した後、Vision入力からYellowロボットが100 mm以上動くまで待つ。今回の保存ログではGame Controllerが配信するチーム名が `Unknown` のままで、Craneが自チームを認識したことを確かめないまま試験が進んだ。また、AutoRefの提案適用後にGame ControllerがHaltへ戻ったが、試験はactive状態の中断を捉えず、最後に `UDP_MOTION_FAILURE` を記録した。

## できること

試験開始前に、Game Controllerが配信するチーム情報にチーム名 `crane` があり、Craneが自チームとして認識したことを確認する。チーム色の設定はチーム名とは別の条件として扱う。移動試験中はactive審判状態を監視し、AutoRef提案によるHaltを試験用設定で避ける。active状態が失われた場合は移動タイムアウトと区別して記録する。

## 動作の要点

1. 固定Game Controller 3.20.3イメージでチーム名の設定方法とAutoRef提案の制御方法を確認する。既存checkoutのGame Controller submoduleは `v3.20.3-5-g9b0765f` で、イメージと一致するか未確認である。既存Crane実装の大小文字・照合仕様も未確認なので、推測でAPIや名前を決めない。
2. チーム情報に `crane` が含まれること、審判データグラムで配信されること、Craneが自チームを認識することを試験前に実測する。認識できなければ移動試験を開始しない。`team:=Yellow` 等の色設定とは分ける。
3. AutoRef提案でactive状態が終了しないよう、実イメージでサポートを確認した試験専用設定を使う。
4. Yellowロボットの100 mm条件を確認する間、active審判状態とVisionを継続観測する。状態が失われた場合はreferee command/counterとともに専用の失敗理由を残す。
5. Duck tracker受信、既存の比較測定、今回の試験用stackのcleanupも確認する。

## 対象外/後続

本番の試合設定、Game Controller/AutoRef本体、Tracker UI、#41のsnapshot pair・時刻差表示は変更しない。親設計が要求する時刻差警告も対象外。依存、権限、ネットワーク設定を変更しない。

## 検証済み/未検証

今回のログにはUnknownチーム名を示すCrane警告、ER-Force AutoRefの提案、提案適用後のHaltが記録されている。PR73の成功記録（親提示のCI39186264332、WSL Yellow 103.76 mm）は参考となる過去証拠であり、この作業ではraw artifact未照合である。

Game Controller 3.20.3イメージのAPI、Craneのチーム名照合仕様、変更後の全体受入成功は未検証。初期Draft PR時点で実装・検証は行っていない。

## 必要判断と影響

実イメージ・既存実装で確かめた方法だけを採用する。試験専用構成とコードに限定し、本番試合設定やIssue #41の機能実装へ影響させない。もし固定イメージで必要な制御方法が確認できなければ、推測で設定せず追加調査結果を記録する。
