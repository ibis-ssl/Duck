# ASPIRE-004A Game Controller / referee-driver 実装報告

## 概要

ASPIRE-004A として、Aspire AppHost に Game Controller 資源を追加し、referee message の観測と Game Controller 制御 API の操作を分離した `referee-driver` fixture を実装した。

作業ブランチは `task/pr28-aspire-004a` とし、親の設計ブランチ `design/issue18-aspire-test-orchestration` とは分離した。実装完了前に最新の設計ブランチ HEAD `65ff0bc` を取り込み、ASPIRE-003 と ASPIRE-004A の application model 契約が共存する状態で再検証した。

## 作業開始時の CI 診断確認

`.github/workflows/dotnet-test.yml` を確認し、テスト失敗時に次を artifact として保存する既存経路があることを確認した。

- TRX
- 標準出力
- 標準エラー
- vstest 診断ログ
- MSBuild binlog
- 環境情報
- 調査用ソーススナップショット

このため ASPIRE-004A では診断 artifact workflow の重複追加は行っていない。

## 実装内容

### Game Controller 資源

`Testing/Duck.Testing.AppHost` に `game-controller` を追加した。

- image: `robocupssl/ssl-game-controller:3.20.3`
- Docker network: `host`
- SSL-Vision: `224.5.23.2:10020`
- tracker: `224.5.23.2:11010`
- referee publish: `224.5.23.1:11003`
- control API: `:8082`

application model test では、固定 image tag、host network、CLI 引数、および 11003 producer が `game-controller` だけであることを固定した。

### referee-driver fixture

`Testing/Duck.Testing.RefereeDriver` を追加し、次の責務を分離した。

- `IRefereeCommandSource`: 11003 の referee command 観測
- `IGameControllerControlClient`: Game Controller continue action 送信
- `RefereeDriverFixture`: HALT から active command までの状態遷移
- `UdpRefereeCommandSource`: multicast referee packet の受信と command counter 変化の検出
- `GameControllerWebSocketControlClient`: `ws://127.0.0.1:8082/api/control` への continue action 送信

状態遷移は、初期 `HALT` を必須とし、`NEXT_COMMAND` 後の状態に応じて `FORCE_START` または `NORMAL_START` を送信する。最終的に `NORMAL_START` または `FORCE_START` を active command として確認する。

WebSocket client は Crane の現行 `match_controller_pb.py` と同じく、接続後に初期 Output を受信してから JSON Input を送る。JSON field 名も `preserving_proto_field_name=True` と整合する snake_case の `continue_action` / `for_team` を使用する。

## TDD 記録

Game Controller application model 契約は未実装状態で `game-controller` 不在により RED を確認してから実装した。

referee-driver 状態遷移は 5 件すべて `NotImplementedException` で RED を確認してから実装した。

network adapter 契約は 4 件すべて `NotImplementedException` で RED を確認してから実装した。

主な commit:

- `5cb84b2` `test(aspire): add failing game controller model contract`
- `bbb4e84` `feat(aspire): add pinned game controller resource`
- `459a01d` `test(aspire): add referee driver fixture contract`
- `5946766` `feat(aspire): implement referee driver state transition`
- `79ce641` / `9cc9259` / `8a53ec7` network adapter RED 契約整理
- `4934a60` `feat(aspire): implement referee network adapters`
- `e967e37` 最新設計ブランチ取り込み

## 検証結果

最新ベース統合後、ASPIRE-003 / ASPIRE-004A 関連 focused test は 17 / 17 成功した。

`Tracker.Tests` 全体は 346 件中 335 件成功、11 件失敗した。失敗は CaptureOn / sidecar 系の既存 11 件である。

ASPIRE-004A 差分を含まない設計ブランチ HEAD `65ff0bc` でも全体 336 件中 325 件成功、同じ 11 件が失敗した。代表 1 件を個別実行し、`tracker-snapshot-alignment.jsonl` を別プロセスが使用中である `System.IO.IOException` が同様に再現した。このため ASPIRE-004A による新規回帰とは判定していない。

`git diff --check` は成功した。

`npm run lint:md` は Windows checkout に `xargs` が無く、さらに `.agents/skills/review-enforcer/scripts/list-markdown-targets.js` が存在しないため、Markdown 内容検査へ到達できなかった。この阻害は既存台帳にも記録されている checkout 側の検査基盤不足と同じである。

## 範囲外

ASPIRE-004A では Docker を実起動した 11003 packet / active-motion の受入確認は行わない。Simulator、Crane、`cm4-sim` を含む一括起動と実 packet の確認は ASPIRE-004B / ASPIRE-005 以降で扱う。

## CI

この報告 commit を含む最終 PR HEAD に対し、`head_sha` が完全一致する workflow run だけを最終 CI 判定対象とする。最終 run の結果は PR コメントへ記録する。
