# `ASPIRE-004A` 作業手順

## 目的

PR #28 の `ASPIRE-004A` `Game Controller` と `referee-driver` の試験補助を、失敗確認、成功確認、回帰確認、報告の順に分離して進める。

この文書は製品仕様ではなく作業手順を定義する。製品仕様は `aspire-simulation-test-environment.md` を正とする。

## 作業境界

- `game-controller` を `Aspire AppHost` から Docker で起動する資源として追加する。
- `Game Controller` は `ContainerImageAnnotation.Tag` を固定し、Docker の `--network host` 指定で起動して、`224.5.23.1:11003` の唯一の審判情報送信元とする。
- 制御 API は `127.0.0.1:8082/api/control` を使う。
- `referee-driver` は 11003 へ送信せず、`HALT` 確認後に API を操作して、試合進行中を示す `NORMAL_START` / `FORCE_START` への遷移を確認する。
- 上記契約を Docker を使わない `AppHost` 資源構成の試験と対象試験で固定する。
- `simulator`、`Crane`、`cm4-sim`、比較構成 / 対戦構成、実通信の受入は後続作業とする。

## 作業領域の分離

`ASPIRE-004A` は専用の Git `worktree` を使い、`RemoteDesktopMCP` の `session_id` もこの作業専用に1つ確保する。並行する `ASPIRE-003A/B` とは `worktree` と `session_id` を共有しない。PR #28 へ合流する直前に現在の `HEAD` を取得し、必要なら親側の最新履歴を取り込む。

## 共通条件

作業開始時に既存の `GitHub Actions` 定義 `.github/workflows/dotnet-test.yml` を確認する。この定義が試験結果、標準出力、標準エラー、失敗原因調査用ログを失敗時の成果物へ保存する限り、診断目的だけの重複定義は追加しない。

実装はTDDで進める。

1. 対象契約を固定するテストを追加する。
2. 未実装状態で期待理由により失敗することを確認する。
3. 失敗確認だけをコミットし、`git push` する。
4. 最小実装で成功確認にする。
5. 関連回帰、文書検査、`git diff --check` を実行する。
6. 成功確認を別コミットにし、`git push` する。

CI確認では PR #28 の現在の `HEAD SHA` と `GitHub Actions` 実行の `head_sha` が完全一致する実行だけを使用し、別の `head_sha` 値の実行は代用しない。

## WF28-004A-01 `Game Controller` 資源構成の失敗確認

- 資源名は `game-controller`。
- `ContainerImageAnnotation.Image` と `ContainerImageAnnotation.Tag` の固定値を検査する。
- Docker の `--network host` 指定を検査する。
- `-visionAddress 224.5.23.2:10020` を検査する。
- `-trackerAddress 224.5.23.2:11010` を検査する。
- `-publishAddress 224.5.23.1:11003` を検査する。
- `-address :8082` を検査する。
- `AppHost` 内に 11003 へ送信する別資源を作らない。

## WF28-004A-02 `Game Controller` 資源構成の成功確認

`AppHost` へ `game-controller` を追加する。`Crane` 基準構成の CLI 引数を維持し、`latest` ではなく実在確認済みの `ContainerImageAnnotation.Tag` 固定値を使う。

## WF28-004A-03 `referee-driver` 失敗確認

対象試験で次を固定する。

1. 最初の命令が `HALT` でなければ失敗し、API を操作しない。
2. `HALT` 確認後の最初の操作は `NEXT_COMMAND`。
3. 次の命令が `HALT` / `STOP` なら `FORCE_START`。
4. 準備状態なら `NORMAL_START`。
5. `NORMAL_START` / `FORCE_START` のいずれかを観測し、試合進行中の状態へ遷移した時点で成功する。
6. `NORMAL_START` / `FORCE_START` のいずれも確認できなければ失敗する。
7. `referee-driver` 自身は 11003 へ送信しない。

## WF28-004A-04 `referee-driver` 成功確認

状態遷移処理と、通信を抽象化する `IRefereePacketReceiver` / `IGameControllerWebSocketTransport` の境界を分離する。今回の対象試験では観測した命令列と送信した操作列の契約を Docker を使わず固定し、実通信実装は後続の一括起動試験から再利用できる形にする。

## WF28-004A-05 検証・報告

- `AppHostApplicationModelTests` と `referee-driver` の対象試験を実行する。
- `Tracker.Tests` の関連回帰、Markdown / 用語検査、`git diff --check` を実行する。
- `Tracker/Design/tasks-status.md` を更新する。
- 詳細報告書を `reports/` へ保存する。
- PR #28 の現在の `HEAD` へ合流後、同一コミット識別子の CI だけを確認する。
- PR #28 へ簡易報告を投稿する。`git merge` は行わない。
