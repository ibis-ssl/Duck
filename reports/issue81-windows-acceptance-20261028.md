# Issue #81 Windows 4資源ランタイム受入結果

対象の技術HEADは `9841d99616795074ac14f95a57dd9b5baa0295ae`。検証器は、base `d67d2a4d3389445ea387b1935d0c79b411f3adc4` にある `scripts/verify_desktop_referee_acceptance.py` のblob `c77acd3036eeb10330de58aa2d0d6acd13ee6b2b` を、SHA-256 `db3101ea23bf8f158b59e791ba6b8aff5e7b1d316d4acf2c04f392c94435825b` で照合して外部から実行した。検証対象repoはHEAD `9841d99616795074ac14f95a57dd9b5baa0295ae` のままで、実行後も作業ツリーはcleanだった。

## 1. 実施範囲

承認されたWindows実機受入を、既存Docker Desktopの `desktop-linux` contextを通じて一度だけ実行した。実行前後のengine identityは一致した。検証器の上限は全体120秒、起動45秒、評価30秒、停止15秒である。検証器の対象はSimulator、Duck、Game Controller、RefereeDriverの4資源で、ホスト公開はloopbackの動的TCPに限定した。

この結果は当該HEADでの1回のWindows受入に限る。Issue #81全体の移行完了、通常・比較構成の完了、Issue #82の受入、PRの統合判断を示すものではない。

## 2. 画像と実行前確認

Simulatorは `ghcr.io/ibis-ssl/framework-simulatorcli@sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79`、Game Controllerは `robocupssl/ssl-game-controller@sha256:d62d772de063c2951f600a0b7898803f97822f4f46a412a731e42f7d82a99dc7` を使用した。必要なMicrosoft公式基底画像 `mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3` と `mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca` は、それぞれ指定digestで取得に成功した。初回のruntime画像取得はcredential helperの実行ファイルをPATHから解決できず終了コード1となった。既存のDocker Desktop配下helperの実体を確認した後、DockerプロセスだけのPATHで再実行し終了コード0を確認した。ユーザーまたはシステムのPATH、Docker設定、認証設定は変更していない。

実行前preflightは終了コード0で、HEAD、context、Engine IDと4画像を確認した。AppHost Debug DLL、RefereeDriver Host Release DLL、RuntimeHost Release DLLのSHA-256は順に `9439310b7eefda0a1e4cd522fa324df598d1bc32c855427194d615410caf9ad8`、`f2fde3c7154e8f5929426e92959679c4e26d4809e87eb4eddc06354199c062df`、`9699bdb2790eb422c2873fcb1620e98a62368cd2f574a4dbaeed5fc8cdbde931` だった。

## 3. 実行結果

唯一の `--run-once` は2026-10-28 13:37:41.3346093 UTCに始まり、13:38:41.3602026 UTCに終了した。検証器とAppHostはいずれも終了コード0、結果は `passed=true` だった。準備状態は12サンプル中12サンプルで成立し、失敗サンプルは0件だった。保存されたhealth probeは12件すべてHTTP status `200`、Docker probe exit `0`で、errorは0件だった。

## 4. 停止と資源の後片付け

検証器はAppHostへCtrlBreakを送り、DCPの通常停止・削除後に今回のrunで記録した4つの完全IDが消えたことを照合した。最大15秒の停止枠内で3回目の照合（約2.50秒）に残存IDは0件だった。実行前後とも無関係なコンテナはなく、検証器のエラーも0件だったため、今回の実機実行で他owner資源に触れていないことは確認できる。一方、別ownerの稼働コンテナを同時に置いた実機試験ではない。所有者判定は既存のpredicate testsで検査済みである。終了後も同じEngineで `docker ps -a` は空だった。

## 5. 証跡と文書チェック

検証器の出力とコマンド別ログは外部診断フォルダー `非公開の外部診断領域` に保持し、コンテナIDや環境全体のダンプはこの報告とmanifestに転載していない。今回の安全な集計値、HEAD、検証器hash、画像digest、ビルド成果物hashは `reports/evidence/issue81-windows-acceptance-20261028/acceptance.json` に記録する。

本報告本文にtextlintとYomiyasu strictを個別実行し、両者の終了コードは0、Yomiyasuは100点・指摘0件だった。`reports/` は通常のMarkdown対象一覧から除外されるため、この個別合格を標準全体ゲートとは扱わない。CSpellとSudachiの個別診断は未実施であり、未実施を合格とは扱わない。選定した診断結果のhashだけをこのmanifestに記録し、診断ログと全証跡一覧は非公開のまま保存している。

## 6. 未完了事項と統合境界

この受入だけでは、Issue #81の残る通常・比較構成の移行、全体CI、Issue #82の受入を完了扱いにしない。今回の報告はPRのマージ承認でもない。

baseとの統合はこの実機受入の範囲外であり、実行していない。この結果はmerge approvalを示さない。