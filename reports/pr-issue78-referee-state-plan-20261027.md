# Issue #78 movement-time referee-state plan

## 1. 背景と既存成果

Issue #78 は親 Issue #75 の残件を分けた子課題です。子 Issue #77 と PR #76 は、試験開始前にCraneとGame Controllerのチーム名を一致させる範囲を扱い、PR #76 は親用の非既定ブランチへマージ済みです。#78 は移動中の審判状態監視と AutoRef 提案による Halt 抑止を担当し、Issue本文では未着手とされています。この計画 PR は #76 の実装を再掲・再実装しません。

## 2. 対象範囲

- Yellow ロボットの移動中も審判 datagram を監視する。
- active 状態を失った場合に移動 timeout より先に停止し、専用の失敗理由、最後に観測した審判コマンド、カウンターを記録する。
- Game Controller 3.20.3 の固定条件で、AutoRef 提案が受入中に Halt へ戻すことを防ぐ試験専用手段を調べ、実際の機能で確認する。未確認 API や推測した設定値は使用しない。
- 移動中に Halt を注入する回帰試験を追加し、専用失敗理由と審判履歴を検証する。
- 比較モード全体の受入で active 維持、Yellow robot の100 mm移動、Duck/TIGERs/ER-Force入力、および cleanup を確認する。

## 3. 完了条件

- Halt 抑止の試験専用方式が実機能で確認され、固定Game Controller条件が記録される。
- 移動中の状態監視と、active喪失時のtimeout前停止・診断記録が実装される。
- Halt注入回帰試験が専用失敗理由と最終審判履歴を検証する。
- 比較モードの全体受入とcleanupが完了し、実行結果が対象HEADと結び付いて記録される。
- 親 #75 の元の受入条件を維持し、#77 と #78 の両方が完了するまで親課題を未完了のまま保つ。

## 4. 検証計画

この PR は計画文書のみで、製品コード、テスト、Game Controller、AutoRef を変更・実行しません。文書差分に `git diff --check` を実行します。後続の実装では固定試験、移動中の Halt 注入回帰、比較モード全体の実受入とcleanupを実施し、未実施の環境条件を成功扱いしません。

## 5. 未実施と対象外

- 状態監視、Halt抑止、回帰テスト、全体受入は未実施です。
- Game Controller/AutoRef本体、本番試合設定、Tracker UI、親 #75 の完了条件変更は対象外です。
- 未確認API、推測の設定値、新規依存、権限・ネットワーク設定変更は対象外です。
- この Draft PR は Issue #78 や親 #75 を解決せず、自動 close を意図しません。

## 6. Issue と PR の関係

- 対象: [Issue #78](https://github.com/ibis-ssl/Duck/issues/78)
- 親: [Issue #75](https://github.com/ibis-ssl/Duck/issues/75)
- 先行する別範囲: [Issue #77](https://github.com/ibis-ssl/Duck/issues/77) / [PR #76](https://github.com/ibis-ssl/Duck/pull/76)
- この PR の Issue 参照は非 closing とし、#78 の完了条件と親の残件を維持します。
