# PR #130 Persistent 停止の追加調査と XML コメント確認

- 確認日: 2026-11-18 UTC
- リポジトリ: `ibis-ssl/Duck`
- 作業ブランチ: `task/issue-128-launcher-dotnet`
- 調査開始時の候補 HEAD: `08d220373ecee3e50ed4365195e86230a5b4d732`
- 通常レビュー対象として指定された作業木 HEAD: `38e6cb88ad50dd8d01b7e4d68b47a0c2edd84c23`
- GitHub PR #130 の公開 head: `5e1eabd17686980c37a34c3745f7dbc169e48086`（公開側に今回のローカル差分は未反映）

## 読み合わせた根拠

GitHub 上の PR #130 本文・差分と公開レビューコメント [NR130](https://github.com/ibis-ssl/Duck/pull/130#issuecomment-6994631850) を確認した。レビューは `NR130-001` から `NR130-006` の必須修正を挙げ、特に Ctrl+C 時の停止・終了値・ログ・所有資源の同時確認と、日本語 XML コメントを求めている。CodexSkill `implementation-executor` と `report-output-manager`、および参照先の `work-context-manager`、`implementation-worker`、`report-writer` を、指定された CodexSkill main commit `8ee6e9f80a0362a4d066c4ea39fd3e87002fc829` から読み合わせた。これらの手順に従い、実測と推論を区別し、作業範囲を #130 に限定した。

## Persistent Ctrl+C の比較

既存の成功した Session 記録では、同じ分離コンソール用ハーネスが `AttachConsole` 後に Ctrl+C を送り、子 AppHost が終了コード 0 で終了した。記録には `AppHost exited with code 0; stdout and stderr drained.` があり、Session 終了後のコンテナ・ネットワーク・イメージは事前状態と一致した。

Persistent の既存試験では、同じハーネスが対象 PID とプロセスツリーを検証して Ctrl+C を送った後も AppHost が残り、完了マーカー／出力排出完了ログが出なかった。Ctrl+Break でも終了せず、試験で起動した正確なプロセスツリーを終了してから、明示的な `--stop-persistent --stack-id` でその試験所有資源を片付けた。別の隔離プロセス対照では、同じハーネスによる Ctrl+C が通常の親と子の双方で `CancelKeyPress` を発生させ、親の終了と子の終了コード 0 が記録された。

この対照は送信ハーネスと一般的な Windows コンソール信号配送が親子へ届くことを示す。一方、実際の AppHost が Ctrl+C を受信したか、それを受信後に AppHost／Aspire の Persistent 停止処理が待機し続けたかは、既存ログだけでは判別できない。ランチャーの `RunAppHostAsync` の Ctrl+C 処理は Session と Persistent で共通であり、観察されたモード差は AppHost リソースの `ContainerLifetime.Persistent` 設定である。したがって停止境界を AppHost／ホストのライフタイム処理まで絞れるが、受信失敗と受信後の停止停滞のどちらかを確定する計測はない。再現済み試験を根拠なく繰り返していない。

## このターンの変更と検証

`scripts/start-duck-simulation.cs` は既存文字コードを厳密 UTF-8 と確認し、既存方針どおり BOM を維持した。作業コピーの実バイトは BOM 付き UTF-8、369 LF、CRLF 0 件。日本語 XML コメントの実コードポイントも確認した。クラス `Launcher` と `Options`、公開メンバー `RunAsync` と `Options.Parse` に日本語 XML コメントがあり、`Options` の各 positional parameter にも説明を付けた。Persistent 所有ラベル確認、ログの `CreateNew`、子プロセスと標準出力／標準エラーの排出待ち、出力障害時のプロセスツリー終了について、判断理由が伝わる remarks またはインラインコメントを追加した。差分はコメントのみで、実行コードの変更はない。

候補ファイル群の作業コピーと Git の記録は `git ls-files --eol` でいずれも `i/lf w/lf` と確認した。C# ファイルの BOM は維持し、BOM のないファイルへ追加していない。PC 全体の Git 設定は変更していない。システムの `core.autocrlf=true` による「次に Git が触ると CRLF に置換」の警告は残るため、この作業コピー内の物理 LF と Git blob の LF は確認済みだが、別 Windows checkout が物理 LF になる設定までは変更・保証していない。

実行した検証:

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~LauncherProcessContractTests --no-restore` — 成功、12/12。
- `git diff --check` — 成功。
- 公開 PR、作業木、追補記録を読み取り確認。Docker を起動する試験は追加していない。

## 状態と未解決点

Persistent モードの Ctrl+C 後に AppHost が正常終了しない問題は、この証拠では未解決であり、レビュー必須指摘 `NR130-001` を解消したとは扱わない。レビュー候補として返す前に、AppHost 内で信号受信と停止処理の進行を区別できる、短時間・観測点付きの追加計測または修正根拠が必要である。今回のローカルコメント差分とこの追補は未公開で、push・PR 編集・main 更新はしていない。既存の `issue-128-pr130-fix-verification-20261118.md` は内容を編集していない。