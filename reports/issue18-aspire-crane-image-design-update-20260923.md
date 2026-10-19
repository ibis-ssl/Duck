# Issue #18 Crane image 利用方針 設計更新報告

## メタデータ

- リポジトリ: `ibis-ssl/Duck`
- Issue: `#18 Aspire対応`
- PR: `#28 docs: design Aspire simulation test environment`
- ブランチ: `design/issue18-aspire-test-orchestration`
- 更新前 HEAD: `825cac68d377b8a47b79e807e8de219699cfc41a`
- 設計更新 technical HEAD: `863b36695f05b23f9d56b3ecf14d9f8ffa91676d`
- 実行環境: RDC 接続先 `FA780`
- 更新日時: `2026-09-23T17:59:57+09:00`

## 目的

利用者の追加要望により、AI を任意の Docker image または Dockerfile とする未確定設計を廃止し、Crane の既存 Docker image を使う構成へ設計を更新する。

今回も設計文書の更新だけを行い、Aspire AppHost、Docker 資源定義、製品コードは実装しない。

## 確認した Crane の現状

`ibis-ssl/crane` の既定ブランチは `develop` で、現在の README は Ubuntu 24.04 / ROS 2 Jazzy を動作環境としている。

`docker/Dockerfile` は ROS 2 Jazzy を基底とし、`scenario` target で Crane 本体をビルドする。

`.github/workflows/docker_build.yaml` は GitHub Container Registry へ次の Crane image を push する。

- `ghcr.io/ibis-ssl/crane:scenario-<github.sha>`
- `ghcr.io/ibis-ssl/crane:scenario-develop`

したがって Duck 側で Crane のソース取得や Docker build を行う必要はない。

旧 `ibis-ssl/crane_docker` は ROS 2 Foxy ベースの旧構成であり、今回の Aspire 構成には採用しない。

## Crane の実行経路

Crane の現在の `docker/scenario/docker-compose.yaml` では、既定 planner `visibility_graph` のとき Crane が mode 4 の位置指令を出す。

既存シナリオでは次の経路で実機相当の位置制御を再現している。

```text
Crane
  │ mode 4 / UDP 12345
  ▼
cm4-sim
  │ mode 3 / UDP 12346
  ▼
simulator-cli
```

このため Crane image だけを従来の汎用 SSL simulation protocol の UDP 10301 / 10302 へ直接接続する設計にはしない。

Crane の現在の挙動を維持するため、Aspire 側でも `cm4-sim` を補助 Docker 資源として管理する。

## 採用する image

初期設計では次の image family を使用する。

- Crane: `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>`
- CM4 simulator: `ghcr.io/ibis-ssl/orion-cm4-sim:<commit SHA>`
- ER-Force simulator: `ghcr.io/ibis-ssl/framework-simulatorcli:<tag>`

Crane は再現可能な試験では commit SHA に対応する固定タグを必須とする。`scenario-develop` は利用者が明示指定した開発確認だけで使用する。

指定した固定タグが registry に存在しない場合、`scenario-develop` など別タグへ暗黙に切り替えない。

Simulator と `cm4-sim` も固定タグを AppHost の設定として持たせ、外部の latest 相当の更新だけで試験結果が変わらないようにする。

## Simulator 設定

Simulator は Crane の現行シナリオ構成と同じ `framework-simulatorcli` image を使う。

起動引数の初期値は次とする。

```sh
./bin/simulator-cli -g 2020B --realism None --ibis-port 12346 --ibis-team-color yellow
```

AppHost から image tag、geometry、realism、`IBIS_PORT`、`IBIS_TEAM_COLOR` を設定できるようにする。

SSL-Vision は既存どおり UDP `224.5.23.2:10020` を Duck の `Tracker.RuntimeHost` が受信する。

## Crane 設定

Crane の起動は現在のシナリオ構成を基準にする。

```sh
bash -c "source /root/ibis_ws/install/setup.bash && ros2 launch crane_bringup crane.launch.xml sim:=true speak:=false team:=Yellow planner:=${PLANNER}"
```

`team` と `planner` は AppHost から設定可能にする。

初期 planner は既存シナリオと同じ `visibility_graph` とし、その場合は `cm4-sim` を必須資源とする。

`simulator`、`crane`、`cm4-sim` は host network を使用する。Duck はホスト上の .NET プロセスとして起動する。

## 実装分割への反映

- `ASPIRE-002`: AppHost 骨格と Duck の project resource。
- `ASPIRE-003`: `framework-simulatorcli` image の接続。
- `ASPIRE-004`: Crane image と `cm4-sim` の現在の制御経路を接続。
- `ASPIRE-005`: 全資源の一括起動と正常経路の試験。

AppHost のモデル検査では、Crane / Simulator / `cm4-sim` の image family、固定タグ、host network、Crane の `team` / `planner`、UDP 12345 / 12346 の接続を固定する。

## 診断 workflow

既存 `.github/workflows/dotnet-test.yml` は失敗時に TRX、標準出力、標準エラー、VSTest diagnostics、binlog、環境情報、ソースアーカイブを artifact として保存する。

今回の設計更新では workflow 変更は不要と判断した。

将来 Aspire のコンテナ統合試験を CI へ追加するときは、`simulator`、`crane`、`cm4-sim` のログも失敗 artifact に追加する。

## 検証結果

更新後の設計書に対して CSpell を実行し、指摘 0 を確認した。

`git diff --check` は終了値 0 で成功した。

リポジトリ全体の `npm run lint:md` は Git Bash から実行したが、`package.json` が参照する `.agents/skills/review-enforcer/scripts/list-markdown-targets.js` が checkout に存在しないため終了値 1 で停止した。これは今回の本文に対する lint 指摘ではなく、現行 PR 基点の検査実行経路が成立していないことによる阻害として記録する。

## 変更対象

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`
- `Tracker/Design/tasks-status.md`
- `reports/issue18-aspire-crane-image-design-update-20260923.md`
- handoff 文書

## 対象外

- Aspire AppHost の実装。
- Docker image の再ビルド。
- Crane のアルゴリズム変更。
- Duck の製品コード変更。
- merge。

## 次作業

PR #28 の設計確認後、`ASPIRE-002` を TDD で開始し、AppHost application model の失敗テストから実装する。

本報告と handoff は設計更新 technical HEAD `863b36695f05b23f9d56b3ecf14d9f8ffa91676d` の後に置く管理記録である。これらを commit / push した後は、PR current HEAD SHA と一致する `.NET tests` workflow run だけを CI 証跡として採用する。
