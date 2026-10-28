# 課題81 基盤取り込み報告

## 1. 対象と結論

対象は課題81の #84 対応ブランチで進行中の基盤取り込みである。現在の先端コミットIDは `27ecd5decf55977a0a4b5e4d75844a1e3b493d43`、取り込み対象は `d67d2a4d3389445ea387b1935d0c79b411f3adc4`。競合した3ファイルの作業内容を解決し、受入判定試験と AppHost ビルドは成功した。統合は未確定であり、準備領域への解決記録・統合コミット・共有先への送信はまだ行っていない。

## 2. 競合解消

`.github/workflows/dotnet-test.yml` は現在の先端コミットの内容を保持した。基盤側にない Linux配布物出力の検査と失敗時の調査資料の追加を残している。`scripts/tests/test_desktop_referee_acceptance.py` と `scripts/verify_desktop_referee_acceptance.py` は基盤 `d67d2a4` の内容を採用した。これらのファイル内容IDはそれぞれ `649306538a67e0dc571b466b770ed816a3bddd83`、`c77acd3036eeb10330de58aa2d0d6acd13ee6b2b` と一致する。自動検査定義は現在の先端内容ID `d84d495f9b3ece866226543b39c6e64398070d51` を維持し、基盤内容ID `d9687022063db610b21d1d98c037fdcc3be5b76e` と異なる。

## 3. 検証

`python -m unittest scripts.tests.test_desktop_referee_acceptance -v` は48件すべて成功した。試験は模擬化を使い、DockerやWindows実機を起動していない。`dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore -m:1 /nr:false` は成功し、0エラー、既知の `ASPIRE010` 警告1件だった。`git diff --check` も終了値0だった。

親が更新した `tasks-status.md` と `phases-status.md` の追加行だけを検査した。`textlint`、`whitelist`、`CSpell` は両断片で終了値0、`Yomiyasu` は両断片で終了値0・評価値100・指摘0。文書全体の検査合格とは扱わない。

## 4. 既存の実機受入結果との関係

Windows四資源の実機受入は以前の `9841d99616795074ac14f95a57dd9b5baa0295ae` を対象に1回実施し、成功した記録がある。今回の統合後作業内容では実機受入を実行していない。受入検証用検査手順と試験が変わるため、過去の実機結果を今回の統合先端での再実行結果として転用しない。製品実装全体や課題81全体の完了を示すものでもない。

## 5. 未完了と作業境界

競合した3ファイルの作業内容は解決済みである。Gitの準備領域には未解決状態が残っているため、通常の独立レビュー、統合コミット、共有先への送信は未実施。GitHub上でのPR統合、課題終了、追加のDocker起動・イメージ取得、実機再実行も行っていない。親が更新した追跡文書2件は親管理の変更として維持している。

## 6. 証拠

機械可読な検証記録は [`verification.json`](evidence/issue81-base-integration-20261028/verification.json) に保存する。記録対象は統合前の先端コミット、基盤、採用内容ID、試験・ビルド・差分検査・文書断片検査の終了値である。