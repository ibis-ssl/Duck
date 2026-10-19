# Duck PR #73 独立再レビュー報告（2026-10-19 JST）

## 対象と結論

- 対象: https://github.com/ibis-ssl/Duck/pull/73
- 実装PRの確認済みHEAD: `5fcf3fea0248034807de1955040af777810d10b3`（`task/issue72-aspire-006c-integration`）
- 前回レビューHEAD: `4c0c2eb1f73c974806815fa3141a31283b5792f9`、前回コメント https://github.com/ibis-ssl/Duck/pull/73#issuecomment-6256244525
- このHEADの3コミット: `253d1f4` CM4改行正規化、`ff50682` 設計Markdownの日本語化、`5fcf3fe` 計測手順の明確化。前回HEADからの差分4ファイル（51行追加、17行削除）。
- **結論: 要修正（中1件、R05）。** 前回R04の未登録語違反は解消。CM4修正は既存のソケット所有検査を維持。新たに設計書の数値差分を件数へ誤って言い換えた意味変化を確認した。
- 本報告は実装ブランチとは独立したレビュー専用ブランチに保存する。PRの実装コードと設定は変更しない。mergeしない。

## 前回指摘の確認

### R04 — 用語ホワイトリスト: 解消（機械検査の範囲）

- 対象Markdown: `Tracker/Design/Testing/aspire-006c2-integration-plan.md`、GitHub lint対象一覧にも掲載されていることを確認。
- 既存の `tools/lint/scripts/check-markdown-whitelist-sudachi.py --files Tracker/Design/Testing/aspire-006c2-integration-plan.md` を、既存の分離Python環境で実行。終了コード **0**、stdout/stderrに違反なし。前回報告の106行の診断は再現しない。
- 既存の `textlint` CLI を共有依存の絶対パスから、当該リポジトリの `.textlintrc.json` および `tools/lint/scripts/textlint-rules` 指定で対象1ファイルへ実行し終了コード **0**。
- `npm run lint:md` の全対象と `cspell` はこの再レビュー環境では独立実行していない。依頼者が報告したWSL正式lint結果とは区別する。対象1ファイルのホワイトリスト検査・textlint成功を、全体lint成功の証明には使わない。

### CM4改行修正: 実装・検証で問題を確認せず

- `Testing/Duck.Testing.AppHost/DockerServiceReadiness.cs`: `CreateCm4SimListenerProbeArguments` を設け、実行直前に `Cm4SimListenerScript.ReplaceLineEndings("\n")` で改行をLFへ正規化する。
- `Testing/Duck.Testing.AppHost/DockerContainerWrapper.cs`: CM4 readiness実行時にこの引数生成関数を利用する。
- 既存の `/proc/net/udp*` によるポートのinode取得、`/proc/<pid>/comm` の `cm4_sim` 照合、`/proc/<pid>/fd/*` のソケット所有者照合と成功マーカー判定を保持している。接続ポートを開けるだけではreadyにならない条件は維持。
- 新規 `DockerContainerWrapperLifecycleTests.Cm4ListenerProbeNormalizesLineEndingsAndRetainsProcessOwnedSocketChecks` が、CRLF→LF、シェル引数、検査内容と不成功の扱いを確認。CM4ライフサイクル等を含むローカルfocused **36/36成功**。
- Linuxの実コンテナ経路は対象HEADのCI full-stack acceptanceで成功。スクリーンショット等ではなく実TRXとGitHub Actions job結果を確認。

## 新たな指摘

### R05 / 中（文書仕様の意味変化） — 差分値を件数として説明している

- 箇所: `Tracker/Design/Testing/aspire-006c2-integration-plan.md:12`。
- 前回の原文: 「一致した Robot / Ball 差分と、欠落・未対応オブジェクトの status/index が計算結果まで保たれることを確認する。」
- 今回の最終文: 「一致した機体・球の数と、欠落または対応付けできない対象の状態・一覧上の位置番号が計算結果まで保たれることを確認する。」
- 「機体・球の数」は一致オブジェクトの**件数**を表す一方、対象テスト `TrackerDifferenceSnapshotEvaluatorTests.AssertPair` は `PositionDelta.X` の**数値差分**（`expectedRobotDeltaX`、`expectedBallDeltaX`）を確認する。数の一致と差分値の伝播は異なる受入条件である。
- 影響: PR #70の差分計算器から本PRの製品接続まで「計算した差分値が失われないこと」を確かめる設計意図が読み取れなくなっている。実装上の再現済み計算不具合ではなく、文書の意味変化。
- 対応例: 「対応付けられた機体・球の数値差分と、欠落または対応付けできない対象の状態・一覧上の位置番号が、計算結果まで保たれることを確認する。」。元の技術的意味に戻すだけとし、英語の一括許可・引用符やコード囲みでlintを回避しない。修正後はwhitelist・textlint・yomiyasuを再実行する。

## yomiyasu検査と指摘の採否

- 対象は新規修正の `Tracker/Design/Testing/aspire-006c2-integration-plan.md` **1ファイルのみ**。`reports/`は対象外。
- 対象Markdown SHA256: `31256b6ef5024d2e99e44dc7290724f741defef6e9ac19422f660614c38f92ee`。
- `nanaism/yomiyasu` v1.1.0、固定Git SHA `0df47749139dfd64ad3d55e7d53d3839e5874848`。`python -B .../yomiyasu_lint.py <対象Markdown> --json` で実際に検査した。
- 終了コード **0**、score **95/100**、`is_clean=false`、findingsは `excess_list` (warn) **1件**。15行/非空行20行=75%に対しツールの推奨は15%以下。
- **指摘は不採用**。実装範囲・重点連携試験・性能測定・検証順序に分けて要件を列挙するほうが追跡しやすい。比率だけのために文章を連結すると読みにくくなる。ツールの終了コード0を文書品質や意味保持の証明とはしない。R05はyomiyasuの指摘とは独立して採用する。

## 独立検証

| 項目 | 実結果・証拠 |
| --- | --- |
| HEAD | 指定 `5fcf3fea0248034807de1955040af777810d10b3` にdetached worktreeを固定して確認 |
| ローカルテスト | Release `DockerContainerWrapperLifecycleTests` + `TrackerDifferenceSnapshotEvaluatorTests` + `DebugHostHealthEndpointTests`: **36成功・失敗0・skip0**。TRX・stdout・stderr・vstest diagあり |
| ローカル全体ビルド | `dotnet build Duck.slnx -c Release`: **成功・0エラー**。依存のNU1902とASPIRE010警告は残存（本PRで導入したと断定しない） |
| 既存文書whitelist focused | 対象設計Markdownで **exit 0** |
| 既存textlint focused | 対象設計Markdownで **exit 0**（共有依存を使用） |
| cspell / 全体 `npm run lint:md` | このレビューでは未実行（成功の主張はしない） |
| yomiyasu | **95/100**、`excess_list`警告1件は理由を示して不採用。reports除外 |
| GitHub Actions | [.NET tests run #39178783660](https://github.com/ibis-ssl/Duck/actions/runs/39178783660) は `headSha=5fcf3fea0248034807de1955040af777810d10b3` と一致 |
| CI内の.NET tests | success |
| CI内のLinux full-stack | success、証拠artifact `aspire-full-stack-39178783660-1` を実際に取得。`acceptance-result.json: status=passed`、TRX **2/2 passed** |
| CI内のASPIRE-NET-002 | skipped（成功扱いにしない） |
| CI後処理 | `cleanup-result.txt`: スタック `gha-39178783660-1` の自分のコンテナのみ除去、`images_removed=false`、`daemon_settings_changed=false` |

### Linux実測（HEAD一致runに限る）

- データグラム観測窓: **5001.6972 ms**、Duck 132（26.39 packets/s）、TIGERs 316（63.18 packets/s）、ER-Force 1426（285.10 packets/s）、Vision 295 packets。
- 製品経路の呼出1000回: p50 **371.855µs**、p95 **2040.026µs**、割当量 **94,064.04 bytes/call**（.NET10.0.13 / Ubuntu 24.04.5 / 4論理プロセッサ）。snapshot複製等は含むがHTTPシリアライズ・通信は含まない。
- これらの数値は今回のCI実測であり、別SHAのrunやユーザーのWSL測定値を代用していない。更新頻度は受信パケットの到着率であり、重複排除後の発生源固有Hzを意味しない。
- WSLの9 readiness endpoint HTTP 200・ロボット移動103.76mm・cleanup済みという結果はPRコメント https://github.com/ibis-ssl/Duck/pull/73#issuecomment-6256976151 の報告値。今回のレビューからWSLの独立再実行はしていない。

## 確認範囲、留保と次の対応

- 対象差分は4ファイルを全件確認。CM4修正の既存サービスソケット所有確認を緩める変更、差分計算への回帰は確認されなかった。
- `.github/workflows/dotnet-test.yml` にテスト失敗時のTRX、stdout、stderr、vstest診断ログ収集・artifactアップロードが既存。今回追加は不要。
- Issue #41のUI共有配線、以前のHALT counter 3停止の原因究明、親PR #28のmain統合は今回の修正証拠ではない。
- **R04の用語違反は解消、R05の意味保持は未解消。** 設計書12行目の修正と再度のfocused文書検査を依頼する。
- 再レビュー時は更新されたPR HEADとCI runのheadSha一致を改めて確認する。レビュアーはPR実装ブランチへ変更せず、mergeもしない。

## 診断証拠の手元保存先

`C:/Users/donabe/RemoteDesktopWorkspace/review-artifacts/Duck-pr73-5fcf3fea-20261019/` に `focused.trx`・`focused.stdout.log`・`focused.stderr.log`・`vstest.log`・`build.*.log`・`yomiyasu.stdout.json`・`yomiyasu.stderr.log`・`whitelist.*.log`・`textlint.*.log` および `ci-exact-head/` を保管。レビュー専用の未追跡ファイルを本PRのコード差分に追加していない。
