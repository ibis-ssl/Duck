# PR #28 設計レビュー指摘対応報告

- 対象: ibis-ssl/Duck PR #28
- mode: review follow-up
- publication 基点 HEAD: `e39ee1ebb3c930d0f67105b98ced70ce6e71d45b`
- 作業開始時に確認した HEAD: `ccfd9f2fb3649196a8e01fcdc4e6a0d22b06cff7`
- 対象 finding: I28-DR-001 / I28-DR-002 / I28-DR-003 / I28-DR-004
- 作成時刻: 2026-09-27 13:24:59 +09:00
- I28-DR-004 対応開始 HEAD: `a0d5391d7ee11395234e6498bcc2d9ffcf16fdd7`
- I28-DR-004 設計修正 commit: `2052d1f294e770c8d52323098666e686af493216`
- merge: 実施しない

## 変更

### I28-DR-001

`tracker-comparison-debug-design.md` の source identity 契約を既存 DebugHost と一致させた。

- UUID を source identity の優先キーとする。
- 同一 UUID / 複数 endpoint は一つの source に集約し、最新 `ReceivedAt` の snapshot を代表にする。
- endpoint ごとの物体を寄せ集めて snapshot を合成しない。
- comparison mode の `Ready` は Duck / TIGERs / ER-Force の三 role を別 source identity に解決できることを条件とする。
- 同一 UUID に複数 role が対応して区別不能な場合は endpoint ごとに分裂させず、role 解決不足として `Ready` にしない。
- ASPIRE-006B の focused test 契約へ同一 UUID、role 衝突、UUID 不明時の endpoint fallback を追加した。

### I28-DR-002

`aspire-simulation-test-environment.md` の port 競合契約を用途別に分離した。

- UDP 10020 / 11010 は共有 multicast port とし、単純な bind / in-use 判定を二重 stack 判定に使わない。
- 制御 listener port は占有 port として外部競合を事前確認する。
- 二重 Duck Aspire stack は AppHost が resource 起動前から終了まで保持する host-local stack ownership lock で拒否する。
- lock file の存在だけでは失敗とせず、排他 handle の取得可否を判定にする。
- ASPIRE-NET-009 と focused test をこの契約へ更新した。

### I28-DR-003

RuntimeHost 自身が受信した packet count を取得できる production diagnostics を設計へ固定した。

- `RuntimeVisionReceiverService` が正常 decode 後に buffer へ渡した packet の累積値 `VisionPacketsReceivedTotal` を保持する。
- endpoint、interface、累積値を診断ログへ出す。
- ASPIRE-NET-002/003 はこの累積値の前後差分を RuntimeHost の受信証跡にする。
- 外部 packet sniffer の count は補助証跡に限定し、RuntimeHost 受信数の代用にしない。
- DebugHost 側は既存 raw input snapshot の packet count を使う。

### I28-DR-004

`aspire-simulation-test-environment.md` の起動順序を Aspire の開始依存として一意にした。

- `WithReference` は接続情報の参照に限定し、起動順序の根拠にしない。
- 既定 `visibility_graph` 構成は `simulator` を先行させ、`cm4-sim` と `duck` がそれぞれ `simulator` へ `WaitForStart` する。
- `cm4-sim` と `duck` の間には開始依存を置かず、`simulator` 開始後の並行起動を許可する。
- `crane` は `cm4-sim` と `duck` の両方へ `WaitForStart` する。
- `cm4-sim` を使わない planner では、`crane` は `duck` のみに `WaitForStart` し、存在しない `cm4-sim` への依存を作らない。
- `WaitForStart` は起動済み状態だけを保証し、UDP 正常性は保証しない。正常性確認を導入する場合は観測可能な条件を定義したうえで `WaitFor` を使う。
- AppHost のアプリケーションモデル検査へ、上記 `WaitForStart` 依存の既定構成と `cm4-sim` 無効構成を追加した。

## 検証

- `git diff --check`: success
- 対象2設計書 CSpell: 2 files / 0 issues
- `npm run lint:md`: blocked（exit 255）。Windows 実行環境に `xargs` が無く `lint:md:text` で停止した。stdout / stderr / exit code は `artifacts/pr28-design-fix/i28-dr-004-lint-md.*` に保存した。
- workflow 相当 `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`: blocked（exit 9009）。接続先に `dotnet` が存在せず、テスト開始前に停止した。stdout / stderr / exit code は `artifacts/pr28-design-fix/i28-dr-004-dotnet-test.*` に保存した。
- ローカルで .NET test の合否は得られていない。最終判定には push 後の current HEAD と一致する GitHub Actions run だけを使用する。

## 変更していない範囲

- 製品コード、テストコード、workflow は変更していない。
- `Tracker/Design/tasks-status.md` は本文の更新規則が task-breakdown-planner / task-consistency-manager / progress-sync-manager のみに限定しているため変更していない。
- merge は行わない。

## 次の確認

push 後は PR current HEAD と `head_sha` が一致する `.NET tests` workflow run のみを CI 証跡として確認する。別 SHA の run は代用しない。
