# PR #130 既定IDの常駐起動・再起動・明示停止

2026年11月19日 UTC、PR #130 の NR130-002 に対し、修正候補 `c292cb5b70a7440582f0c07c393f0bd3776b5c7e` の作業木で既定ID `duck-local-development` を明示した常駐起動、同じIDでの再起動、明示停止を確認した。作業者は `fa780\donabe`、Docker 接続先は `desktop-linux`、Engine ID は `5bc5ab8a-7340-47ba-8b7c-9f645375a54c`。

## 起動前確認

- `duck.aspire.stack=duck-local-development` のコンテナ、既定名に一致するコンテナ、同じラベルのネットワークは各0件。AppHost／ランチャーの競合プロセスも0件で、`duck-aspire-stack.lock` は空いていた。
- 次の固定イメージはすべて既存のローカルキャッシュにあり、取得・更新は行わなかった。
  - Simulator `ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd` — `sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79`
  - Crane `ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d` — `sha256:db6ab99009f98003c2f078e5d0820004a8e726c7a293965e7022ecfdb1bb2a49`
  - CM4 `ghcr.io/ibis-ssl/orion-cm4-sim:d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a` — `sha256:aef193194cce7c1c3729ac344065945b3d92c0a7ccdca0d5036cc472a8bfa61a`
  - Game Controller `robocupssl/ssl-game-controller:3.20.3` — `sha256:d62d772de063c2951f600a0b7898803f97822f4f46a412a731e42f7d82a99dc7`
- `Testing/README.md` の出典修正は起動前に完了していた。実行したコマンドは `dotnet run --file scripts/start-duck-simulation.cs -- --persistent --stack-id duck-local-development`、再起動も同じ引数、停止は `dotnet run --file scripts/start-duck-simulation.cs -- --stop-persistent --stack-id duck-local-development`。

## 起動と同一ID再起動

- 初回は7つの所有コンテナがすべて `Up` になった。AppHost は `Distributed application started` を出し、CM4 初期設定ログは `resultApplied=True`、Crane の coordinator probe は最終的に `Ready=true`、`coordinator_match`、終了値0となった。
- Ctrl+C 後、AppHost は `AppHost exited with code 0; stdout and stderr drained.` を出し、常駐スタックを保持する完了表示を出した。7コンテナと Aspire の常駐ネットワークは残った。
- 2回目の同一ID起動でも、7コンテナすべてが `Up` となり、Crane の coordinator probe が `Ready=true` となった。初回から再利用されたのは Simulator、CM4、Crane、RefereeDriver の4件。Duck、Game Controller、DebugHost は作り直された。

| リソース | 初回のコンテナID | 再起動後のコンテナID | 結果 |
| --- | --- | --- | --- |
| simulator | `93fbc944bf247b771429d8e3aad32abe4a8ef7d248d82890b5524df02a76b16d` | 同一 | 再利用 |
| cm4-sim | `b2ae73df3f93385cd02a05c0419fe8e675178dabdf4fe3314a9702bbb56de797` | 同一 | 再利用 |
| crane | `dbeb06a36fe6a6dba0e45918dda0daaf634e6083db11a5a4b1ab1cb89ded3339` | 同一 | 再利用 |
| referee-driver | `bb6bdffe21ada58150f449356c7e1923ac25aa33c0bcac4559bf4ad68401ca14` | 同一 | 再利用 |
| duck | `000cbdcf7d5b572b8f9b5700ebb13bbedddebbef4b2622ac2f1975858b22db18` | `02f32afaf45e5686e9a4444f37da10b40bfa4227dad2029fcdced5e1fb68eee0` | 再作成 |
| game-controller | `0b3c256422eef8e7b756d398174186f87c186453e691a4a9a8bf6893dec6f507` | `304b47228cb9f98056f0b10db9321328c09440de44359db8f03dd0382832a64d` | 再作成 |
| debug-host | `72b18197a4f26fa36421b06ffd92eca53d783f3955024c90f4c3fe37c7d61efc` | `3bffe664b9df250b6b17e896c995cc643369c8a0576ba7a6a33a04f58b9ea755` | 再作成 |

再起動後も Ctrl+C で AppHost は終了値0と標準出力・標準エラーの回収完了を記録し、スタックを残した。なお2回とも、Ctrl+C を送った対話型 `exec_command` セッションの最終終了値は1だった。AppHost のログ上の終了値0とは別の観測であり、この終了値1の発生元はこの試行では確定していない。以前の検証記録は分離したプロセス起動と Ctrl+C 送信で外側終了値0を記録しているため、今回の PTY 試験をその結果と同一視しない。

## 明示停止と後片付け

- `--stop-persistent --stack-id duck-local-development` は終了値0を返し、今回の7つのコンテナIDだけを停止・削除した。イメージは削除していない。
- 停止後に残ったネットワークは1件で、名前 `aspire-persistent-network-9a49eef4-Duck.Testing`、creator PID `12080` は今回の AppHost と一致し、接続コンテナは0件だった。この試験専用ネットワークだけを削除した。
- 最後の照合では既定IDのコンテナ0件、試験ネットワーク0件、関連 AppHost／ランチャープロセス0件、共有ロック空き。4つの固定イメージは同じ image ID で残っていた。
- 試験ログと起動メタデータは `artifacts/duck-simulation/duck-local-development/` に2回分保存した。Windows・Docker の永続設定は変更していない。Session モードの再試験はしていない。

## 検証範囲

この試行で、既定IDを明示した常駐起動、同じIDでの再起動、明示停止、所有資源の後片付けを実測した。再起動では7件中4件が再利用され、3件が再作成された。プログラム変更後にコンテナが更新されること、別の ID や並行起動時の動作、CI はこの試行では確認していない。

AppHost の生成は成功し、0エラー・9警告だった。警告は NU1902/NU1903/NU1904、ASPIRE010、および `SimulatorBallPlacementFixture.cs` の CS8604。対象 README の文章検査は成功。スペル検査の6件は変更段落外にある既存の `PowerShell` 表記で、whitelist 検査は変更段落に違反を検出しなかったが、README 内の別箇所には既存違反がある。詳細な生ログは上記の成果物ディレクトリにある。

関連する従前の停止実績は[常駐停止検証](issue-128-pr130-persistent-shutdown-verification-20261119.md)と[一時起動停止検証](issue-128-pr130-session-shutdown-verification-20261119.md)を参照。この記録は NR130-002 の既定ID系列に限る。