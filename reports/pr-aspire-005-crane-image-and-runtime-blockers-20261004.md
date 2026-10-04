# ASPIRE-005 Crane image と runtime 受入の状況

## 対象

- Issue: #37 / ASPIRE-005
- PR: #61 (`task/pr28-aspire-005`)
- 確認したPRの基準HEAD: `b0dacf41a432d78d49492b22339f6b3678b30f60`
- 確認日: 2026-10-04 UTC

## Crane の固定 image

AppHost の既定値 `scenario-a544db92b72b137c8974285b36940d0d4b5e7e69` は registry に存在しない。Crane の `a544db92b72b137c8974285b36940d0d4b5e7e69` は `[skip ci]` 付きのリリースコミットであり、[Docker workflow](https://github.com/ibis-ssl/crane/blob/a544db92b72b137c8974285b36940d0d4b5e7e69/.github/workflows/docker_build.yaml) は `develop` への push または手動実行でのみ image を構築し、コミットSHAのタグを付ける。このSHAでの build run はなく、`scenario-a544...` は `manifest unknown` となる。

最新の成功 build は [run 36966869325](https://github.com/ibis-ssl/crane/actions/runs/36966869325) で、ソースSHAは `4063cd31cd5b11b1cc919003907f5f4c527b252d`。対応する固定タグ `ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d` は存在し、linux/amd64 manifest digest は `sha256:ce12b180bc689c792178b9c0990038f5f3acab9fd461f914356167cd170d47ac`。`4063cd31` から `a544db92` までの差分はREADME badgeとpackage version metadata (`1.0.563` から `1.0.564`) で、scenario構成・実行コードの差分はない。したがって同じ設計意図のシナリオを実行する既存の最新固定参照として、AppHost既定値とモデルテストを `scenario-4063cd31...` に更新した。設定によるtag上書きは引き続き可能。

## Docker daemon の観測

- Docker context `default`、Docker 28.4.0、storage driver `vfs`。daemon は containerized Debian 上で動作し、Docker root は `/var/lib/docker`。
- `docker system df -v` は image 約1.43 GB、container / volume / build cache は0。
- daemon root は見える32 GB overlay上にあり、約23 GB free、inode使用率は約9%。daemon rootの権限や内部quotaはこのプロセスから確認できない。
- Craneのlayer unpackは `no space left on device` で失敗したが、見えるoverlayの残量・inodeだけでは内部quota / ephemeral layer limitの有無を特定できない。image削除、再pull、daemon設定変更は行っていない。

## Linux runtime workflow

既存の `.github/workflows/dotnet-test.yml` に `workflow_dispatch` 専用の `ASPIRE-NET-002 Linux packet flow` job がある。このjobはSimulatorとRuntimeHostだけを起動してSSL-Vision受信を検証する。Crane、cm4-sim、Game Controller、referee遷移、Duck tracker出力、active motion、stack ownershipは対象外であり、ASPIRE-005全体の代替証跡にはならない。

2026-10-04にPRのソースSHA `b0dacf41a432d78d49492b22339f6b3678b30f60` を指定してworkflow dispatchした。run [37178355697](https://github.com/ibis-ssl/Duck/actions/runs/37178355697) は成功。artifact `aspire-net-002-37178355697-1` の `test-result.txt` でbaseline 0、latest 173、delta 173、3.964秒を確認し、Simulator image pull、RuntimeHost build、Simulator起動、packet counter増加も成功した。これはASPIRE-NET-002だけの受入であり、ASPIRE-005全体の試験ではない。完全なruntime受入を再開するには、Docker daemonの実効storage quotaを確認でき、必要なCrane imageを展開できるLinux Docker環境が必要。

## 現時点の判定

Stack ownership guardとmodel contractは実装済みだが、ASPIRE-005の一括起動および実パケット受入は未完了。部分的なCIやRunning状態のみを完了証跡としない。
