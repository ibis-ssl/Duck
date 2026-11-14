# 課題 49 フォローアップ：Game Controller API の各フェーズ

このメモは、既存の `match` モード AppHost を動かした際に見つかった準備完了判定の不一致を記録するものです。課題 49 の受入条件や承認済み設計は変更しません。

## 観測した実行

- 基準: `design` ブランチのコミット `59320715a1e5bca6ef9ac33aba74a3ecddbb987c`。Game Controller イメージは `robocupssl/ssl-game-controller:3.20.3`。
- 実行モード: `Testing:Mode=match`、`Testing:MatchReadinessOnly=false`。`match-controller` コンテナは存在し、有効でした。
- 結果: `readiness` で `apiReferee=NoObservation, visionReady=True, participantResourcesReady=True` となり失敗しました。試合は開始・終了に至らず、結果も合格していません。
- 読み取り専用の Game Controller WebSocket 観測には `stage=NORMAL_FIRST_HALF_PRE`、`command=KICKOFF`、`gameState=KICKOFF` が含まれていました。最初に観測したメッセージは 849、3433、1603 バイトで、クライアントの現行上限 4096 バイトを超えていません。
- 対応する制御サービスの正常性応答では、新しい審判 UDP 入力のデコードは確認できましたが、正規化された API 観測はありませんでした。

## 限定したフォローアップ

現在の `GameControllerStateMapping` は、`gameState` の `HALT`、`STOP`、`RUNNING` を受け入れ、`KICKOFF` は `STOP` と組み合わせた場合だけ一致させます。そのため、観測した `KICKOFF/KICKOFF` の判定は、観測なし（`NoObservation`）です。固定された Game Controller v3.20.3 の状態機械は `NORMAL_START` 適用時も直前の `game state` を保持します。したがって、試合が `RUNNING` になるまで有効な `NORMAL_START/KICKOFF` の区間を観測可能なままにする必要があります。[v3.20.3 の状態遷移処理](https://github.com/RoboCup-SSL/ssl-game-controller/blob/v3.20.3/internal/app/statemachine/change_command.go) を参照してください。

実装のフォローアップで行うべきことは、観測したキックオフと開始の状態ペアだけを追加し、取得した API 形式に基づく対象を絞ったテストを加えることです。`STOP`、`HALT`、`ForceStart`、未知の状態の扱いを維持した上で、実際の試合を再度実行すべきです。このメモは追跡の基準を記録するだけで、コードやテストの変更を含みません。

## `POST_GAME`

```json
{
  "status": "passed",
  "stageReached": "post_game",
  "terminationKind": "PostGame",
  "finalReadiness": { "isComplete": true }
}
```

[PR #116](https://github.com/ibis-ssl/Duck/pull/116)
