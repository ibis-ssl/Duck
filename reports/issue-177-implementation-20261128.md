# Issue #177 実装報告

## 対象

- Issue / Draft PR: Duck #177 / #178
- ブランチ: `task/issue-177-match-recording-mcap`
- base: `main` / `a89f4a753a9e84b9df880e1c064178ed180da6fb`
- 実装候補: `de71ef5be984b2ed584ec9eca4d868702acf8a15` (tree `c5f1527834b94073a5a81afe134fc0a4d90679f0`)

## 実装範囲

RuntimeHost の受信から単体 MCAP の記録・検証、および CaptureReplay CLI による記録時設定での再計算までを実装した。raw 入力、処理順と設定世代、形状・reset 制御記録を同一ファイルに保持する。再計算不能な遅延入力の記録は共通 contract で拒否し、通常の未処理入力は区別して受け入れる。

変更は PR 公開 HEAD `5437cc4df243d78e99154ab0c0bdf79a9f026cce` から続く Issue #177 の設計、RuntimeHost、MCAP、CLI、テストに限る。実機検証と UI はこの実装確認に含めていない。

## 検証と状態

- SDK 10.0.401 の関連テスト: 31 成功、0 失敗、0 skip。試合録画、MCAP、CLI の焦点試験を実行した。
- 同じ通常レビュー担当からの関連試験報告: 13 成功。
- CI run `46136348535`、対象 HEAD `de71ef5be984b2ed584ec9eca4d868702acf8a15`: `.NET tests` 成功。`ASPIRE-NET-002 Linux packet flow` と `ASPIRE full-stack Linux acceptance` は skip。
- 実機確認、手動 CI、マージは未実施。

## 次の工程

通常レビューは pass。独立最終レビューへ引き継ぐ。

## AppHost 接続追補

- 対象: `b0792559db57370e7ea9d5d17e05290ed74809f1`。`Testing:MatchRecording:Enabled=true` を指定した direct AppHost の match-mode のみ、試行固有の Duck 録画ディレクトリーを `/recordings` に割り当てる。未指定時は録画設定と writable mount を追加しない。
- 焦点モデル試験: `MatchModeRoutesCraneThroughCm4WithoutSimulatorPacketAdapter`、`MatchRecordingOptInMountsTrialDirectoryAndConfiguresRuntimeHost` の 2/2 成功。
- 対象は direct AppHost＋Docker 経路。#160 の native RuntimeHost 経路は対象外。Windows 実機での確認は未実施。
- 自動 CI run `46137484219` (`b079255`): `.NET tests` success。
