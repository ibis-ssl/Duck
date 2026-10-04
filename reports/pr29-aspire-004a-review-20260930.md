# ASPIRE-004A 独立レビュー

## 対象

- 親設計: PR #28
- 実装 PR: #29 `feat(aspire): implement ASPIRE-004A Game Controller fixture`
- レビュー開始時の PR #29 HEAD: `0e994cfe0af0e28913b16b382d3e0f998789f5f8`
- base: `design/issue18-aspire-test-orchestration`
- レビュー対象: PR #29 の差分 12 ファイル
- 表記レビュー対象: `Tracker/Design/Testing/aspire-004a-workflow.md` と `Tracker/Design/tasks-status.md`
- `reports/` 配下は表記レビュー対象外とし、検証結果の事実関係だけ確認した。

重点確認項目は、設計文書における過剰な引用符による lint 例外化、一般的でない日本語、一般語の過度な英語表記である。

## 結論

受入れを保留する。実装ロジックについて ASPIRE-004A 固有の回帰は確認できなかったが、設計文書の表記と検証証跡に阻害指摘が3件ある。

## 指摘

### I29-004A-REV-001: 設計文書の表記が既存方針を満たしていない

重要度: 阻害

`Tracker/Design/Testing/aspire-004a-workflow.md` は設計配下の文書であり、レポートと異なり表記方針の対象である。本文に一般語の英語表記が多数残っている。

代表例:

- 11行目: `Docker image 資源`
- 12行目: `固定 tag`、`host network`、`referee producer`
- 14行目: `publish`、`active command`
- 15行目: `application model test`、`focused test`
- 16行目: `comparison / match mode`、`実 packet 受入`
- 24行目: `既存workflow`、`失敗時artifact`
- 35行目: `current HEAD`、`workflow run`
- 40行目: `image repository`、`固定tag`
- 54行目以降: `command`、`action`、`active command`、`fixture`
- 66行目: `network adapter境界`
- 73行目から75行目: `詳細report`、`簡易report`、`merge`

`Tracker/Design/tasks-status.md` の今回追加部分にも同じ傾向がある。

- 45行目: `単一 producer`、`active command`、`focused / application model test`
- 48行目: `resource`、`adapter`、`client`、`実 packet active-motion 受入`

Game Controller、Aspire AppHost、Docker、HALT / STOP / NEXT_COMMAND、URI、クラス名、ファイル名など、正式名称・識別子として残す必要があるものは対象外である。上記の一般語は、例えば「イメージ」「固定タグ」「ホストネットワーク」「送信元」「送信」「有効なレフェリーコマンド」「アプリケーションモデル試験」「対象試験」「比較 / 対戦モード」「実パケット」「ワークフロー」「アーティファクト」「ネットワークアダプター」「レポート」「マージ」など、自然な日本語または一般的なカタカナ表記にできる。

また、5行目の `「Game Controller と referee-driver fixture」` は、発言者・出典を伴う直接引用ではなく、作業名を日本語の引用符で囲っている。引用符で囲う必要性が本文から確認できず、引用部分を lint 例外とする仕組みがある場合は検査を迂回する形になる。lint 回避の意図までは断定しないが、設計文書の引用ルールには適合しない。

対応条件:

1. 正式名称・識別子以外の一般語を自然な日本語または一般的なカタカナ表記へ直す。
2. 5行目の引用符を外すか、引用として必要なら出典を明示する。
3. 今回追加した `tasks-status.md` の本文も同じ基準で直す。
4. 修正後に Markdown 用語検査を成功させる。

### I29-004A-REV-002: `git diff --check` が失敗する

重要度: 阻害

レビュー開始時の HEAD `0e994cf` に対して次を実行した。

`git diff --check 65ff0bc7032b614d75776eca480cb87fa3ea4239..HEAD`

結果:

`Tracker/Tracker.Tests/RefereeDriverFixtureTests.cs:125: new blank line at EOF.`

したがって current HEAD では `git diff --check` は成功していない。

一方、`reports/pr28-aspire-004a-implementation-20260930.md` と PR #29 の既存コメントには `git diff --check passed` 相当の記録がある。現在の HEAD の実態と一致しないため、末尾空行を修正したうえで検査結果を取り直し、報告を同期する必要がある。

### I29-004A-REV-003: 設計文書の Markdown lint 成功証跡がない

重要度: 阻害

current HEAD で `npm run lint:md` を実行したが、Windows 環境に `xargs` がないため文章検査へ到達せず停止した。

さらに `.agents/skills/review-enforcer/scripts` 自体がこの checkout に存在せず、`rg --files .agents/skills/review-enforcer/scripts` もパス不在で失敗した。

`.github/workflows` 内を `rg` で確認したが、`lint:md`、textlint、cspell、許可一覧検査を実行する workflow は確認できなかった。PR #29 の current HEAD に紐づくチェックも `.NET tests` のみである。

したがって、今回追加された設計文書について Markdown lint が成功した証跡はない。設計書は表記規約を厳守する対象なので、lint を実行できる環境で成功を確認するまで受入れ条件を満たさない。

## 実装確認

### TDD

コミット順は RED と GREEN が分離されている。

- `5cb84b2`: Game Controller model の RED
- `bbb4e84`: Game Controller resource 実装
- `459a01d`: referee-driver 状態遷移の RED
- `5946766`: 状態遷移実装
- `8a53ec7`: network adapter の RED
- `4934a60`: network adapter 実装

`459a01d` の `RefereeDriverFixture.DriveToActiveAsync` は `NotImplementedException`、`8a53ec7` の network adapter も未実装例外を返す状態であり、テスト先行の形を確認した。

### Game Controller / Crane 契約

Crane の参照コミット `af6e0d3dec745415ce060ff5de2042afd3ec5145` の `match_controller_pb.py` を `gh` で確認した。

- WebSocket endpoint は `/api/control`
- Input JSON は `MessageToJson(..., preserving_proto_field_name=True)` を使用する
- continue action は `NEXT_COMMAND`、必要に応じて `FORCE_START` / `NORMAL_START`

ASPIRE-004A の snake_case JSON と基本状態遷移はこの参照実装と整合する。

`robocupssl/ssl-game-controller:3.20.3` は 2026-09-30 時点で Docker Hub のタグ一覧に存在し、linux/amd64 と linux/arm64 のイメージが公開されていることを確認した。接続先 Windows 環境には `docker` コマンドがないため、ローカル pull / 起動確認は今回行っていない。

### テスト

current HEAD で次を確認した。

- 対象テスト: 14 / 14 成功
  - `AppHostApplicationModelTests`
  - `RefereeDriverFixtureTests`
  - `RefereeDriverNetworkAdapterTests`
- `Tracker.Tests` 全体: 346 件中 335 件成功、11 件失敗

11件の失敗はいずれも CaptureOn / sidecar 読み取り時の `System.IO.IOException` で、ASPIRE-004A の追加コード上の失敗ではない。実装報告に記録された既存失敗の種類と一致した。

## CI

レビュー開始時の PR #29 current HEAD は `0e994cfe0af0e28913b16b382d3e0f998789f5f8`。

この SHA と完全一致する `.NET tests` run は `36632160936` で、結果は success。別 SHA の workflow run は判定に使用していない。

レビュー報告を commit / push した後は HEAD が更新されるため、新しい HEAD と一致する run が存在する場合だけ最終 CI として扱う。一致する run がなければ CI 未実施として扱う。

## 受入れ条件

- I29-004A-REV-001 の設計文書表記を修正する。
- I29-004A-REV-002 の末尾空行を修正し、`git diff --check` を成功させる。
- I29-004A-REV-003 の Markdown lint を実際に成功させる。
- 修正後に対象テストを再実行する。
- 更新後の PR #29 current HEAD と完全一致する CI run だけを確認する。

merge は利用者判断とし、レビュー担当では行わない。
