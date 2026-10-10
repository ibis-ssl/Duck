# Crane 起動判定の再調査・修正記録

2026-10-05 / 親PR28・Issue37（ASPIRE-005）・作業PR61・追加修正PR63

## 最終の実構成検証

[成功run37244425991](https://github.com/ibis-ssl/Duck/actions/runs/37244425991) は、検証HEAD `06794bf5e4a6a51242e9dcb74c418489113d2d89` と一致する。通常.NET、NET-002、full-stackはすべて成功。報告作成中の確認で終了済みだった結果を取得し、CIの完了待機はしていない。

実artifact `11318074050`（`aspire-full-stack-37244425991-1`）をダウンロードして確認した。

| 段階 | 実結果 |
| --- | --- |
| 開始gate | 41回目の確認で4資源が同時に200、`owned_stack_valid=true` |
| ROS setup・一覧取得 | 完了21試行すべてsetup完了、終了0、timeoutなし。初回2200ms、以後452〜583ms |
| graph照合 | 全21試行に `/session_controller` が存在。旧期待名は全試行に存在せず、旧判定なら不一致 |
| 審判 | `Halt(0)` → `PrepareKickoffYellow(1)` → `NormalStart(2)` |
| 移動 | 黄色2番が108.118965mm移動。100mm/60秒の条件は変更なし。受入試験全体は15.942秒 |
| 通信・Tracker | harnessがVision959件、Duck Tracker1件を実際に読み取った。到達段階は `duck_tracker_output_wait`、結果passed |
| 診断保存 | 実JSONLは43行（開始21・完了21・終了集計1）。sanitizer再適用で内容不変 |
| 後片付け | このrunの所有コンテナだけを削除。イメージ削除・Docker daemon設定変更なし |

cm4-simログのmode4入力12345→mode3出力12346という起動構成と、SimulatorのVision上の移動を照合した。各区間のパケット数を個別計測した結果ではない。team Unknownやcm4設定パケットのWrongSize警告は、本runの受入成功を妨げていないが、個別原因の解消とは扱わない。

実診断JSONLのSHA256は `64be4df7f0d43426efa465f50128ea11755d4ae983848db50bab92e602fdeb5a`。機械検証の要約は [検証記録](verification/crane-readiness-20261005.json)。受入成功後も独立レビュー・文書lint残件・マージは別であり、PR63はDraftのままとする。後続の文書コミットを実行検証HEADと同一視しない。

## 確定事項と修正

固定Crane版4063cd31のcoordinatorはROSノード名session_controllerを登録する。Duckは/crane_session_coordinatorを期待していた。この契約不一致を修正した。ただし旧runにprobe終了値・実graphはなく、503の全試行がこの不一致だけで失敗したとは断定できない。

根拠: https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_session_coordinator/src/crane_session_coordinator.cpp#L21-L22 。同版のmainとlaunchに名前上書きはない。旧artifactのloggerはsession_controllerで、実coordinator引数にも名前変更はない。旧ログの2026-10-04T13:44:22.291584381Zに設定読込完了があるが、本体のsetupと別実行のprobe内setupは区別する。

所有コンテナ稼働、setup完了の固定行、一覧取得の終了値0、/session_controllerの行完全一致を要求する。旧名とのOR、部分一致、プロセス生存への代替はない。probe上限10秒・TERM後2秒のKILL、開始gate300秒、移動100mm/60秒は維持した。

## 診断保存

未公開診断を引き継ぎ、Testing__Crane__DiagnosticsPathをAppHostからCrane wrapperだけへ渡す。CI保存先はartifacts/aspire-full-stack/crane-probe.jsonl。標準エラーの間接採取だけに依存しない。開始、完了試行番号・所要時間・終了値・setup完了・対象行一致・秘匿後の512文字抜粋、終了要求時の集計を保存する。照合は省略前の全文を用い、中断試行へ終了値を補わない。124は期限超過、137は強制終了で、137だけで期限超過とは断定しない。

JSONLを正規表現だけで加工すると引用符・改行の構造が壊れる問題も先行テストで再現した。cleanupはJSON解析後に文字列と秘密キーの値を秘匿して再保存する。不完全なJSONや秘匿失敗時はアップロードしない。書込み失敗は固定の診断だけを出し、readyや片付けの契約を変えない。

## 検証結果と未到達

旧HEAD2094d035dabd11c58ec2175af8cc3a0548d86cad、run37206501213、artifact11304629862の一致を実確認。gate失敗、Simulator/GC/cm4は200、Crane503。coordinator生存。probeのsetup・終了値・timeout・graphは未記録。移動とDuck Trackerは未到達であり、受信0件とはしない。team Unknownは観測したが原因未確定。

先行テストは.NET2件が期待値不一致で失敗、Python19件中3件失敗。修正後は対象.NET15/15（診断8件＋モデル7件）、Python19/19成功。Windowsで早期returnするLinux専用試験を15件へ含めていない。Windows全体は386件中375成功、11件失敗。11件はTracker snapshot/diagnostics sidecarのファイル共有IOExceptionで、今回も実確認した別件。

f583743abe26644aefbcee3bef9f846f5dcc16f6のrun37243996778は確認時点で終了済み。Linux385/386、NET-002成功、通常試験1件失敗でfull-stack未開始。artifactのTRXは期待password=[REDACTED]、実値は末尾LF付き。模擬Dockerのechoの仕様に合わせ06794bf5e4a6a51242e9dcb74c418489113d2d89で試験期待値を訂正。通常.NETのPR run37244430977は成功。

模擬診断5記録を実ファイルに保存し、標準エラーが失われても記録されること、CIと同じsanitizer成功、秘密値なし、JSON再読込、圧縮後バイト一致を確認した。これは実Craneのgraphや移動結果ではない。JSONL SHA256=8e40d7e4c83a323e65a8101567c3912ad2f40dd2a0691340e0e4ddf4c5cc5fd1。

## 公式構成との差

同版docker/scenario/docker-compose.yamlはCrane→cm4-sim→Simulatorを構成し、STOP_ROBOT_SPEED.pyはrcst_comm.change_referee_commandを呼ぶ。DuckはGame Controllerだけを審判送信元とし、公式の審判送信やプロセス生存だけの開始条件は移植しない。

## 保全と設計

元作業先C:/Users/donabe/Documents/Codex/2026-10-04/task-3/Duck-independentの変更7件・未追跡5件をSHA256付きmanifestとpatchで保全。新作業先C:/Users/donabe/RemoteDesktopWorkspace/Duck-pr61-crane-probe-20261005、新ブランチtask/pr61-crane-probe-20261005。保全先は同階層のDuck-pr61-crane-probe-20261005-handoff-verified。元ファイルをresetしていない。引継ぎba8461f、先行試験77b7fca、製品修正f583743、Linux期待値訂正06794bfを分離した。

正本設計Tracker/Design/Testing/aspire-simulation-test-environment.mdと台帳を更新。textlintは通過、cspellとホワイトリストの全文検査は未合格。固定元HEADにも先頭のWarning: truncated output等の混入文と未許可語がある。今回記載の製品名への指摘も含めて残し、引用符による回避や無断の許可語追加をしていない。

## 再現手順

git fetch origin task/pr61-crane-probe-20261005後、git rev-parseで完全HEADを固定する。Linux/Docker環境では同HEADの.github/workflows/dotnet-test.ymlのfull-stack手順を使う。手動時はTesting__Crane__DiagnosticsPathを絶対パスへ設定する。gh workflow run dotnet-test.yml --repo ibis-ssl/Duck --ref task/pr61-crane-probe-20261005で起動でき、追加入力はない。

wrapper-readiness.jsonとcrane-probe.jsonlを照合し、setup失敗、graph取得失敗、強制終了、対象行欠落を分類する。欠落したファイルは採取失敗で、試行0件や空graphではない。ready達成後に審判遷移、Crane/cm4-sim/Simulatorの移動、Duck Trackerを順番に確認する。未到達を成功にしない。今回は上記の実構成受入まで成功したが、独立レビュー・文書lint・マージは未完了である。CI完了の待機は行っていない。
