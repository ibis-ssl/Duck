# PR #28 ASPIRE-003 実装報告

日付: 2026-09-30

## 対象

`ASPIRE-003A` の RuntimeHost SSL-Vision 受信診断と、`ASPIRE-003B` の ER-Force Simulator 資源を実装した。

`ASPIRE-003A` の完了条件は、正常 decode 済み packet の累積値 `VisionPacketsReceivedTotal`、受信 endpoint / 参加 interface / 累積値の診断ログ、decode 失敗を加算しない focused test を TDD で固定することとした。

`ASPIRE-003B` の完了条件は、固定 tag の Simulator image、host network、geometry / realism / IBIS 設定、Duck の sim 用 SSL-Vision 設定を application model test で固定し、Linux + Docker Engine で `ASPIRE-NET-002` の実 packet 受信を確認することとした。

## 開始前の診断 artifact 確認

既存の `.github/workflows/dotnet-test.yml` は `.NET tests` 失敗時に TRX、標準出力、標準エラー、vstest diagnostics、MSBuild binlog、exit code、runner / .NET / git / submodule 情報、source snapshot を artifact 保存する。

したがって `ASPIRE-003A/B` の通常の .NET test については、開始時点で失敗原因調査用 artifact workflow が存在していた。

`ASPIRE-NET-002` の Linux 実 packet 検証を追加した際は、同 workflow に専用 job を追加し、少なくとも次を保存するようにした。

- 受入判定結果
- RuntimeHost build の標準出力 / 標準エラー
- RuntimeHost の標準出力 / 標準エラー
- Simulator image pull の標準出力 / 標準エラー
- Simulator の標準出力 / 標準エラー
- Docker / IP / UDP socket / runner 環境情報
- Simulator inspect 結果

Linux 実 packet job は通常の pull request run では実行せず、`workflow_dispatch` 時だけ実行する。成功時も artifact を保存する。

## ASPIRE-003A: TDD

### 赤

commit: `4245400ce2ca2c33e8ca4e9b0e031fd253fb0832`

先に `RuntimeVisionReceiverDiagnosticsTddTests` を追加し、次を要求した。

- 正常な SSL-Vision packet を処理すると `VisionPacketsReceivedTotal` が増える。
- decode 失敗時は累積値を増やさず、packet buffer も更新しない。
- 診断ログに endpoint、interface、累積値を含める。

PR #28 の HEAD がこの commit と一致する `.NET tests` run `36622218277` は failure となり、未実装状態を確認した。

### 緑

commit: `efdcd79d885e94ab61c4eaa7fc5df89c24d2c5d0`

`RuntimeVisionReceiverDiagnostics` を追加し、正常 decode 後にだけ `VisionPacketsReceivedTotal` を加算するようにした。RuntimeHost 起動時と周期ログで endpoint、参加 interface、累積値を出力する。

同じ head SHA の `.NET tests` run `36622586717` は success だった。

RDMCP 上の focused test:

`dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeVisionReceiverDiagnosticsTddTests"`

結果は 3 / 3 成功。

### 受入窓への調整

commit: `3afd5d48ba972cbc4e0d7126b5ef28b432d2f7fa`

`ASPIRE-NET-002` は安定起動後 5 秒以内の packet 増加を判定するため、周期診断を 5 秒から 4 秒へ変更した。起動直後の baseline と受入窓内の次回診断を確実に得るための変更である。

## ASPIRE-003B: TDD

### 赤

commit: `2cbab11741df98c34f0278220a198e17c404e9ea`

先に `AppHostApplicationModelTests` へ Simulator 契約を追加した。要求した内容は次のとおり。

- resource 名 `simulator`
- image `ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd`
- entrypoint `tini`
- container runtime args `--network host`
- Simulator args:
  - `./bin/simulator-cli`
  - `-g 2020B`
  - `--realism None`
  - `--ibis-port 12346`
  - `--ibis-team-color yellow`
- Duck 環境変数:
  - `Tracker__ActiveProfileName=sim`
  - `VisionReceiver__MulticastAddress=224.5.23.2`
  - `VisionReceiver__Port=10020`
- Duck は Simulator の開始を待つ。
- Simulator image tag / geometry / realism / IBIS port / team color は AppHost configuration から上書きできる。

Simulator の既定値は、Crane の基準 commit `af6e0d3dec745415ce060ff5de2042afd3ec5145` の `docker/scenario/docker-compose.yaml` を `gh api` で確認して固定した。

PR #28 の HEAD がこの commit と一致する `.NET tests` run `36623158525` は failure となり、未実装状態を確認した。

### 緑

commit: `067be552e1c3ceb2fe87f92c6f806b6ecc7e6a93`

`Testing/Duck.Testing.AppHost/Program.cs` に ER-Force Simulator container resource を追加した。Simulator は固定 tag `a52b6bd`、host network、Crane と同じ既定引数を使う。Duck RuntimeHost には sim 用の SSL-Vision 設定を渡し、Simulator の開始を待つ。

同じ head SHA の `.NET tests` run `36624080235` は success だった。

RDMCP 上で `AppHostApplicationModelTests` は 4 / 4 成功し、`RuntimeVisionReceiverDiagnosticsTddTests` と合わせた focused test は 7 / 7 成功した。

## Linux ASPIRE-NET-002

### 初回失敗と原因調査

commit: `03147d1a3e577c461e7b5da5a69526dffa29409f`

Linux + Docker Engine で実 packet を確認する `ASPIRE-NET-002 Linux packet flow` job を `.github/workflows/dotnet-test.yml` に追加した。

同じ head SHA の pull request run `36624568832` は failure だった。既存 `.NET tests` job は success で、失敗は `ASPIRE-NET-002 Linux packet flow` だけだった。

失敗 artifact `aspire-net-002-failure-36624568832-1` を確認した結果:

- `baseline=0`
- `latest=0`
- `delta=0`
- `elapsed_ms=5050`
- RuntimeHost は `224.5.23.2:10006` を待受していた。

Simulator 側では標準エラーに異常はなかった。原因は、CI で RuntimeHost を直接起動したため、AppHost が渡す `VisionReceiver__Port=10020` 等の環境変数が付いていなかったことだった。

### CI 修正

commit: `7a5c0a7fafc6732fe833cbb40a91ea344330bd92`

RuntimeHost の起動へ次を渡すように修正した。

- `Tracker__ActiveProfileName=sim`
- `VisionReceiver__MulticastAddress=224.5.23.2`
- `VisionReceiver__Port=10020`

commit: `5c110dde15d547a79565d22c21c0bff41b47f7df`

RuntimeHost を project directory から起動する形へ修正した。

また、Linux 実 packet job は通常 PR では常時実行せず、`workflow_dispatch` 時だけ実行する。成功時も packet count と各ログを確認できるよう artifact を常時 upload する。

### 成功結果

current HEAD `5c110dde15d547a79565d22c21c0bff41b47f7df` を明示して workflow を dispatch した。

run: `36625776852`

結果:

- `.NET tests`: success
- `ASPIRE-NET-002 Linux packet flow`: success
- runner OS: Linux
- Ubuntu: 24.04
- Docker Engine: 28.0.4
- .NET SDK: 10.0.401
- RuntimeHost endpoint: `224.5.23.2:10020`
- joined interfaces: `10.1.1.140`, `127.0.0.1`
- baseline: 0
- latest: 232
- delta: +232
- elapsed: 3960 ms

`ASPIRE-NET-002` の「5 秒以内に `VisionPacketsReceivedTotal` が 10 以上増える」条件に対し、3.960 秒で +232 を確認した。

成功 artifact は `aspire-net-002-36625776852-1`。`test-result.txt`、RuntimeHost / Simulator の標準出力・標準エラー、runner / Docker / network 情報を保存している。

## CI SHA 照合

TDD と受入確認では、対象時点の PR HEAD SHA と workflow run の `headSha` が一致する run だけを使用した。

- `4245400...` → run `36622218277`: failure
- `efdcd79...` → run `36622586717`: success
- `2cbab11...` → run `36623158525`: failure
- `067be55...` → run `36624080235`: success
- `03147d1...` → run `36624568832`: `ASPIRE-NET-002` failure
- `5c110dd...` → run `36625776852`: `.NET tests` / `ASPIRE-NET-002` success

別 SHA の run は代用していない。

本 report と tasks-status を commit した後に PR の final HEAD が変わるため、その final HEAD と一致する CI は commit / push 後に再実行し、結果を PR コメントへ記録する。

## 文書検査

`git diff --check` は成功した。

`npm run lint:md` は Windows 作業環境に `xargs` が存在しないため `lint:md:text` で停止した。変更ファイルを個別に textlint しようとした経路も、checkout に `.agents/skills/review-enforcer/scripts/textlint-rules` が存在しないため実行できなかった。CSpell wrapper と Sudachi whitelist checker も同じ `.agents` 配下の実行物不足で完走できなかった。

CSpell 本体を直接実行した時点では、`Tracker/Design/tasks-status.md` の今回追加行に `RDMCP` 1件の新規指摘が出たため、本文を `Windows focused test` へ変更した。残る `Sudachi` / `Blazor` 6件は既存箇所である。whitelist は変更していない。

## 変更ファイル

主な変更は次のとおり。

- `Tracker/Tracker.RuntimeHost/RuntimeVisionReceiverService.cs`
- `Tracker/Tracker.Tests/RuntimeVisionReceiverDiagnosticsTddTests.cs`
- `Testing/Duck.Testing.AppHost/Program.cs`
- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs`
- `.github/workflows/dotnet-test.yml`
- `Tracker/Design/tasks-status.md`
- `reports/pr28-aspire-003-implementation-20260930.md`

## 完了状態

`ASPIRE-003A` と `ASPIRE-003B` の完了条件を満たした。

`ASPIRE-003B` 完了により `ASPIRE-004B` の依存関係のうち `ASPIRE-003B` 側は解消した。`ASPIRE-004B` の開始には、もう一方の依存である `ASPIRE-004A` の完了も必要である。

