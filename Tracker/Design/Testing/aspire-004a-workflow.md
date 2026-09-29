# ASPIRE-004A 作業フロー

## 目的

PR #28 の `ASPIRE-004A`「Game Controller と referee-driver fixture」を、RED、GREEN、回帰確認、報告の順に分離して進める。

この文書は製品仕様ではなく作業手順を定義する。製品仕様は `aspire-simulation-test-environment.md` を正とする。

## 作業境界

- `game-controller` を Aspire AppHost の Docker image 資源として追加する。
- Game Controller は固定 tag、host network、`224.5.23.1:11003` の唯一の referee producer とする。
- 制御 API は `127.0.0.1:8082/api/control` を使う。
- `referee-driver` は 11003 を publish せず、`HALT` 確認後に API を操作して active command への遷移を確認する。
- 上記契約を Docker 不要の application model test と focused test で固定する。
- Simulator、Crane、`cm4-sim`、comparison / match mode、実 packet 受入は後続タスクとする。

## セッション分離

`ASPIRE-004A` は専用worktreeと専用のリモートデスクトップ作業セッションで作業し、並行する `ASPIRE-003A/B` のworktreeを共有しない。PR #28 へ合流する直前に current HEAD を取得し、必要なら最新ベースを取り込む。

## 共通ゲート

作業開始時に `.github/workflows/dotnet-test.yml` を確認する。既存workflowがテスト結果、標準出力、標準エラー、失敗原因調査用ログを失敗時artifactへ保存する限り、診断目的だけの重複workflowは追加しない。

実装はTDDで進める。

1. 対象契約を固定するテストを追加する。
2. 未実装状態で期待理由により失敗することを確認する。
3. REDだけをcommit / pushする。
4. 最小実装でGREENにする。
5. 関連回帰、lint、`git diff --check` を実行する。
6. GREENを別commit / pushする。

CI確認ではPR #28のcurrent HEAD SHAとworkflow runの `head_sha` が完全一致するrunだけを使用し、別SHAのrunは代用しない。

## WF28-004A-01 Game Controller model RED

- 資源名は `game-controller`。
- image repositoryと固定tagを検査する。
- host networkを検査する。
- `-visionAddress 224.5.23.2:10020` を検査する。
- `-trackerAddress 224.5.23.2:11010` を検査する。
- `-publishAddress 224.5.23.1:11003` を検査する。
- `-address :8082` を検査する。
- AppHost内に11003をpublishする別資源を作らない。

## WF28-004A-02 Game Controller model GREEN

AppHostへ `game-controller` を追加する。Crane基準構成のCLI引数を維持し、`latest` ではなく実在確認済みの固定tagを使う。

## WF28-004A-03 referee-driver RED

focused testで次を固定する。

1. 最初のcommandが `HALT` でなければ失敗し、API操作しない。
2. `HALT` 確認後の最初のactionは `NEXT_COMMAND`。
3. 次のcommandが `HALT` / `STOP` なら `FORCE_START`。
4. 準備状態なら `NORMAL_START`。
5. active commandを観測した時点で成功する。
6. active commandを確認できなければ失敗する。
7. fixture自身は11003へ送信しない。

## WF28-004A-04 referee-driver GREEN

状態遷移ロジックとnetwork adapter境界を分離する。今回のfocused testでは観測command列と送信action列の契約をDocker不要で固定し、実network adapterは後続の一括起動試験から再利用できる形にする。

## WF28-004A-05 検証・報告

- `AppHostApplicationModelTests` と referee-driver focused testを実行する。
- `Tracker.Tests` の関連回帰、Markdown / 用語lint、`git diff --check` を実行する。
- `Tracker/Design/tasks-status.md` を更新する。
- 詳細reportを `reports/` へ保存する。
- PR #28 current HEADへ合流後、同一SHAのCIだけを確認する。
- PR #28へ簡易reportをコメントする。mergeは行わない。
