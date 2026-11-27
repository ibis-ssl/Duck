# Issue #54 / ASPIRE-006G4 着手記録

## 対象と境界

- Issue: https://github.com/ibis-ssl/Duck/issues/54
- 親 Issue / 集約 PR: #50 / #118
- 依存: #53（完了、PR #121 は親ブランチへ統合済み）
- 作業 branch: `task/issue54-aspire-006g4`
- base: `task/issue50-aspire-006g`
- 開始 HEAD: `c9f693e5d32da89e8a986609016bdd088f110dbd`
- 対象: ASPIRE-MATCH-005 の試合終了・結果・SSL記録・Crane記録データ・全起動対象の標準出力／標準エラーを、失敗時にも run の成果物へ保存すること。
- 対象外: 親 PR #118 の main へのマージ、Crane 本体／planner／通信 protocol の挙動変更、Issue #53 の受信証拠を重複取得すること。

## 現行コードと既存 Linux 成果物の確認

run [42488259296](https://github.com/ibis-ssl/Duck/actions/runs/42488259296) は HEAD `078a3225871dcf05ecdaff2cd759f28906db1d56` で Ubuntu 24.04.5 / Docker 28.0.4 を使用した。これは #53 の MATCH-001/002 と#53固有の通信証拠を評価した run であり、#54 の完了証拠としては扱わない。

保存済み artifact はローカル `%TEMP%\issue53-run-42488259296` にある。MatchController の記録は `MATCH_MAXIMUM_TIME_REACHED: execution stopped without POST_GAME.`、`match-acceptance.json` は `fullMatchPassed=false`。したがって試合完了は未達である一方、失敗結果と SSL recorder archive (`match/2026-11-08_14-11-21.log.gz`, 25,712,250 bytes) は保存されている。

起動時診断には8 resource（MATCH参加対象7つとMatchController） の `*.startup.log` があるが、readiness 時点の直近250行であり完全な標準出力／標準エラーとは確認できない。後処理の `resource-logs/` には AutoRef の停止記録1件だけで、8 resource の完全な `*.stdout-stderr.log` は存在しない。`docker-containers-before-cleanup.txt` は見出しのみ、`created-container-ids.txt` は空であり、後処理時には AppHost/DCP が管理する container が既に残っていなかった。したがって container が片付く後処理だけではログを取得できない。

Crane container 内では `ros2 bag record` が実行されていたことを過去の process snapshot で確認できるが、Crane bag は artifact に存在せず、記録先・保存完了は未確認である。Recorder archive と Crane bag は別の証拠として扱う。

## 作業方針

既存の MATCH lifecycle と run-owned container の情報を使い、resource teardown の前に出力ログと既存 Crane recording を artifact へ取り出す最小変更を行う。失敗・最大時間到達を成功へ読み替えず、MATCH-005 の終了条件と証拠保存の成否を分けて残す。ログが採れない resource は欠落を明示し、取得済みの証拠を破棄しない。Crane の planner／起動動作は変更しない。

## 状態

- 既存の Docker/full-stack run は再実行していない。
- Issue #54 は OPEN。現在の HEAD で試合終了、Crane bag、完全な resource log の受入は未検証。
- 次: workflow と既存 record path の実装を確認し、許可済み範囲で記録取得を実装する。
