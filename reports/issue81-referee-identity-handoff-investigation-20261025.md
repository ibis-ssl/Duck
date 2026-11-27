# Duck Issue #81 / PR #84：審判受信の帰属未成立とReady未達の調査・修正報告

作業日時：2026年10月25日 JST
対象：ibis-ssl/Duck Issue #81、既存Draft PR #84
ブランチ：task/issue81-aspire-desktop-bridge
ベース：task/aspire-docker-desktop-496
開始HEAD：27288a7c1693defa619ce92a25678c92ad6f805f
今回の製品修正後HEAD：1a58875475b2d5889768f34bc7441971349c670f
担当：単独実装。別エージェント起動、Codex CLI追加起動、委譲なし。

## 変更前の問題と観測

2026-10-25にWindows AppHost / Docker Desktopで採取された旧run 605d805951d740c6a6c3f87701fa84ee は、RefereeDriver自身の UDP受信・復号成功とReady失敗を別々に示す。受信観測JSONLはWindows private Tempの issue81-referee-get-653cebdad37c490cbdeb57fb91323aaf.jsonl に存在する。

- RefereeDriver自身のGET /health/referee：12回ともHTTP 503 / isReady=false / reason=NoObservation。
- 正常復号件数：1,492から2,416へ924増加。復号失敗0。受信経過時間は短い。
- diagnostic.identity=null、diagnostic.reason=Unattributed、api=null。
- 同じrunのAppHost本体はCTRL_BREAKで終了コード0、実行89.33秒、強制終了なしと記録され、実行後のDockerコンテナは0件。ただし、今回改めて実測した値ではない。

HTTP 503をUDP受信失敗と解釈しない。上記は旧実測の再評価であり、今回の修正後のWindows実機結果ではない。

## コードと実測を突き合わせた原因

1. 旧GameControllerVisionOnly構成はRefereeDriverの起動有効化と内部Game Controller URIだけを渡し、診断のRunId/ResourceId/ContainerId/GenerationをRefereeDriverへ確定する経路がなかった。DuckのRuntimeHostには資源通知、所有ラベル、Docker Engine ID、完全なコンテナIDとUDSでの受渡し機構があったが、RefereeDriverには接続されていなかった。
2. RefereeDriverApplication.BuildでServiceReceiveDiagnostics.ParseIdentityがnullとなる。ServiceReceiveDiagnosticsは正常復号件数を蓄積しながらも、識別情報が無い間はUnattributedとしてIsReady=falseを返す。これは未帰属の誤Ready防止として設計通り。
3. RefereeApiMatcher.IsUsableは非null identityと有効なUDP証跡を必須とする。未帰属のときReadAsyncはAPI読み取りへ進まず、API観測はnullのままになる。直近の一致結果が保持されないGetCurrentはNoObservationを返すので、HTTPは503となる。
4. したがってUnattributed/GC API観測無し/NoObservationは同じ不足の連鎖として説明でき、独立した3不具合と決めつける必要はない。ただし、正しい帰属を与えた後の実WebSocket到達性と同一packet照合の実機成立までは、既存実測から断定できない。

## 変更した実装と設計

- commit 2917a298213e175099dc55a037bd292d933a3aa1：RefereeReceiveServiceで稼働中の単一受信を維持しつつ識別情報と受信世代を入れ替える。旧世代で既に待機した受信試行と保持中の指令を無効化し、受渡し後の新しい正常復号進捗を要求する。回帰テスト追加。
- commit 1a58875475b2d5889768f34bc7441971349c670f：明示的DesktopBridge選択時だけAppHostからRefereeDriverへDuckと同一RunId、固有roleラベル、独立監視を供給する。登録資源の現在状態、所有ラベル、固定Docker contextとEngine ID、完全なコンテナIDを照合してから、当該コンテナに短命の dotnet Duck.Testing.RefereeDriver.Host.dll --identity-control を実行する。
- RefereeDriver側に専用のUnix domain socket /tmp/duck-referee-identity.sock と prepare/challenge/commit制御を追加。同一接続のBootId、RunId、資源キー、role、完全なcontainer ID、正の世代を検査する。役割が異なる場合、BootId不一致、世代の後退は拒否し、同一識別情報の再送は冪等とする。変更前の観測専用・自動制御の既定値は維持する。
- 設計変更の影響はTracker/Design/Testing/aspire-simulation-test-environment.mdのDesktop BridgeでのRefereeDriver帰属確定節に追補した。ホスト側の新規UDP公開、Docker Desktop設定変更、WSL実行、外部3repo変更、依存追加はしていない。既存UDP受信経路そのものは再実装していない。

## 検証証拠と限界

開始時に.github/workflows/dotnet-test.ymlを確認。失敗時はTRX、stdout、stderr、vstest診断ログ、binlog、環境情報をartifactへ保存する定義が既にあるため追加不要。

Windows .NET SDK 10.0.402、既存NuGet cacheだけでrestore。別worktreeの同一固定コミットと照合した既存protoファイルをオフライン複製し、追加ネットワーク取得なしでビルドした。

- RefereeDriverライブラリビルド：exit 0、警告0、エラー0。証拠はWindows RemoteDesktopWorkspace/pr84-referee-build-2-20261025.log。
- 受信系の回帰テスト：27件成功、失敗0、TRX=RemoteDesktopWorkspace/Duck-pr84-checks-20261025/referee-receive-group.trx。
- 受信中の識別情報入替えテスト：1件成功、TRX=同ディレクトリ/identity-handoff-result.trx。
- AppHostのRefereeDriver識別情報構成試験：未配線時に1件失敗するRedを採取。配線とテストfixtureの非同期破棄修正後1件成功、TRX=同ディレクトリ/apphost-identity-green2.trx。
- 実Unixソケットのrole/BootId制御テスト：正規commit、誤role拒否、誤BootId拒否の3件成功、失敗0。TRX=同ディレクトリ/identity-control-uds-v2.trx。新しいBootId負例の実行前拒否は履歴として残し、利用者の15:17 JST明示許可後に起動。標準出力の成功を確認したが、終了コードファイルの別途読取は安全性チェックで実行前拒否され、管理シェルは後から停止したため、その管理exit1をテスト自体のexitと混同しない。
- AppHost/RefereeDriver/Tracker.Tests統合ビルド：exit 0、エラー0、既存の警告あり。証拠はRemoteDesktopWorkspace/pr84-identity-build-20261025.log。
- Windows全Tracker.Testsは840件中829件成功、11件失敗、skip0。失敗はキャプチャ/sidecar系の試験名で、今回追加のRefereeDriver identityテストの失敗ではない。TRX=同ディレクトリ/pr84-local-full.trx、stderrには失敗11テスト名。TRX詳細抽出コマンドはOpenAI安全性チェックで実行前拒否、未実行で再試行無し。共通原因は未確定であり、全回帰合格とは記録しない。

GitHub CI 40166788899はHEAD 2917a298213e175099dc55a037bd292d933a3aa1 と一致したrunで、835件成功、1件失敗（Linux限定DockerContainerWrapperLifecycleTests.LogFollowerReapTimeoutReturnsFailureAndStillCleansContainer、期待終了値1/実際0）。当該失敗を今回変更の受信コードに帰属する証拠はない。次の1a588754...以降のHEADへこの結果を流用しない。packet-flow/full-stackジョブはskip。後続の同HEAD CI状態は最新のrun identityで別途照合する。

## 未検証・保留・次の判断

修正後の実機AppHost/DCP/RefereeDriverは今回起動していない。したがってReady=200、GC APIの実WebSocket観測、実パケットの同一期間照合、HALTからrunningへのUI操作、試合全体は未検証。新しい帰属経路は構成・局所UDSテストに合格した段階であり、実Docker Desktopでの所有証明完遂は未確認。

実機再確認をするなら、対象は明示的GameControllerVisionOnlyの4資源と自身のGET /health/referee。目的は帰属IDの成立、復号進捗、GC API状態、HTTP200を旧503から区別すること。既存のWindows有界起動スクリプトの固定実行版・SHAと所有対象を確認し、開始前に今回所有コンテナの有無を照合、75秒程度でCTRL_BREAK、AppHost exit回収と対象所有コンテナだけの残存確認を行う。既存単発起動の許可を無制限再起動へ解釈せず、今回の実機再起動は具体操作への明示許可未取得のため保留。Docker Desktop内部構成やネットワーク、認証、権限、依存、外部リポジトリを変更しない。

Issue #81の残り：全体統合、Windows実機受入、G1/G3外部経路、ドキュメント全文lint/yomiyasu、失敗CIとWindows全回帰の追跡、通常/独立最終レビュー、利用者の最終確認。報告を残してもIssue/PRをcloseしない。merge、Slack投稿、deploy、他担当への委譲、WSL操作は実施しない。

文書検査：今回の作業ツリーにはtextlintのnode_modulesと対応venvが無いことを確認。既存別配置のlint資材を確認する読取も安全性チェックで実行前拒否されたため、依存インストールや別経路の迂回はせず、今回の追補について正式文書lint合格を主張しない。

拒否があった操作、対象、理由非提示、実行前未実行はTodoに保持。古いRunの成果と新しい修正・局所テストを混同しない。

## 2026-10-25 17:32 JST 再開後追加検証：Windows全体テストの失敗11件

対象HEAD：`b935cd52f91fd4aa12d232bde092cb1c9f8ea154`。開始時に作業木clean、PR #84 Open/Draft、head SHA一致、関係するAppHost/dotnet稼働プロセス0を確認。先行のWindows全体テストは840件中829件成功・11件失敗・0件省略だった。今回は失敗11件だけを全件再実行し、11件すべて失敗を再現した。試験コマンドはWindowsの既存`Tracker.Tests.dll`を用いた`dotnet test --no-build --no-restore --filter （以下11件のOR条件） --logger console;verbosity=detailed`、process_id=`Rp4RD9ZOStfy5YxxEqKRJ6hRgvyiVQ_2e03Ilzsl_6M`、終了コード1。新たな試験stdout/stderrとTRXは、Windows`RemoteDesktopWorkspace/Duck-pr84-checks-20261025/capture-11detail.stdout.log`、`capture-11detail.stderr.log`、`pr84-win11-focused.trx`に保存した。旧TRXの抽出操作（実行前拒否）とは別の新規試験であり、その拒否を無断再試行していない。

| 既存テスト（`Tracker.Tests.`以下） | テストの期待結果 | Windowsでの実際の結果 |
| --- | --- | --- |
| `VisionPacketCaptureTests.DiagnosticsSampleCaptureLoop_WhenCaptureEnabled_WritesSamplesWithoutHomeUi` | UIなしCaptureOnで診断サンプルのフレーム・件数を読み戻せる | `diagnostics-samples.jsonl`の読取開始でIOException |
| `VisionPacketCaptureTests.Capture_WhenEnabled_WritesDiagnosticsSampleSidecarMetadataAndRecords` | metadata件数2・フレーム番号3000/4000などを検証 | `diagnostics-samples.jsonl`読取開始でIOException |
| `TrackerComparisonSourceTddTests.TrackerSnapshotSidecar_WriterAppendsRecordsAndUpdatesMetadataSourceSummary` | own/external/unknownの3記録と集計を検証 | `tracker-packet-snapshots.jsonl`読取開始でIOException |
| `TrackerRuntimeRegistrationTddTests.CaptureOn_LiveUdpReceiver_WritesOwnExternalAndUnknownPacketsToSessionSidecar` | UDPから3種の追跡記録を読み戻せる | `tracker-packet-snapshots.jsonl`読取開始でIOException |
| `TrackerLiveExternalTrackerReceiverTddTests.CaptureOn_LiveTrackerConnection_KeepsOwnExternalAndUnknownPackets` | 3種の送信元を保持・復元できる | `tracker-packet-snapshots.jsonl`読取開始でIOException |
| `TrackerLiveExternalTrackerReceiverTddTests.CaptureOn_LiveTrackerConnection_WritesTrackerConnectionLibPacketToSidecar` | 実追跡packetの副記録を読める | `tracker-packet-snapshots.jsonl`読取開始でIOException |
| `TrackerLiveExternalTrackerReceiverTddTests.CaptureOn_LiveTrackerConnection_PassesRawPayloadForReplayComparison` | 比較・再生用のraw payloadが一致する | `tracker-packet-snapshots.jsonl`読取開始でIOException |
| `TrackerLiveExternalTrackerReceiverTddTests.CaptureOnOffReOn_LiveTrackerConnection_WritesDifferentSessionsToDifferentFolders` | Off→Onで2つの別sessionに記録される | `tracker-packet-snapshots.jsonl`読取開始でIOException |
| `TrackerCaptureOnSessionSnapshotContractTests.TrackerSnapshotAlignmentWriter_WritesLatestSnapshotForEachSourceOnFastTrackerTick` | 入力元ごとの最新スナップショットを復元できる | `tracker-snapshot-alignment.jsonl`読取開始でIOException |
| `TrackerCaptureOnSessionSnapshotContractTests.TrackerSnapshotAlignmentWriter_WritesSeparateSidecarAndMetadata` | 別sidecar・metadataとその記録が整合する | `tracker-snapshot-alignment.jsonl`読取開始でIOException |
| `TrackerCaptureOnSessionSnapshotContractTests.TrackerSnapshotAlignmentWriter_WritesFastestSourceCadenceRecordsWithHeldRenderFrame` | 最速入力周期と表示フレーム保持の記録が一致する | `tracker-snapshot-alignment.jsonl`読取開始でIOException |

上表の11件の実例外はすべて`System.IO.IOException: The process cannot access the file ... because it is being used by another process.`、スタックは`File.ReadLines`から各`ReadRecords`へ遷移する。値・件数・payloadのアサーションに到達していないため、それらの期待値不一致があったと主張しない。`DiagnosticsSampleLogReader`（`DiagnosticsSampleLog.cs:166`）、`TrackerPacketSnapshotLogReader.cs:38`、`TrackerSnapshotAlignmentLogReader.cs:27`の3種は`File.ReadLines`。対応するwriterは`FileStream(FileMode.CreateNew, FileAccess.Write, FileShare.Read)`で開き続ける。Windowsではreader側の共有方式が既存writerの書込アクセスを許可せず開けない。Linux上の同HEAD標準CI成功とも整合する、**既存実装とWindowsファイル共有の組合せ**が原因と判定する。

`git diff 27288a7c1693defa619ce92a25678c92ad6f805f..b935cd52f91fd4aa12d232bde092cb1c9f8ea154`の該当3 reader/writerと旧テスト群は差分0、少なくとも`TrackerPacketSnapshotLogReader`は2026-05-15の既存commit `0e599c0b`から残っている。今回の`RefereeReceiveService`・AppHost識別情報受渡しコミットによる失敗ではない。**今回の機能修正に起因するテスト失敗なし**と判定するが、Windows全体テスト成功とはせず、上記の既存Windows互換性問題は未修正。今回のIssue #81の識別情報受渡しとは独立するDebugHost保存読み戻しの変更を無断で追加しない。

## 2026-10-25 17:32 JST Windows実機受入の実行前照合

- 対象同HEADの標準GitHub CI `40169785227`は`test=success`、`ASPIRE-NET-002 Linux packet flow=skipped`、`ASPIRE full-stack Linux acceptance=skipped`。今回Windows全体テストとCIはOS・検査範囲が異なるため混同しない。
- 以前の実機run `605d805951d740c6a6c3f87701fa84ee`の保存済`process-result.safe.json`は、Docker Engine一致、75秒CTRL_BREAK、AppHost exit0、forcedTreeStop=false、deadlineObserved=falseを示す。これは修正前の実測で、今回の修正後Readyを示さない。
- 既存のDocker Desktop実体`C:\Users\donabe\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`に`--context desktop-linux`を付けた**読み取り専用**確認では、Engine ID `5bc5ab8a-7340-47ba-8b7c-9f645375a54c`、`docker ps -a`は0件、exit0。固定シミュレータ`f505fcb...`とGC`d62d772...`は既存image一覧にある。
- 実機Docker buildへ供する`Testing/Duck.Testing.RefereeDriver.Host`と`Tracker/Tracker.RuntimeHost`のRelease publishは両方`dotnet publish -c Release --no-restore`で既存キャッシュのみから成功し、終了コード0、標準エラーなし。配布物は各`bin/Release/net10.0/publish`。証拠はWindows`RemoteDesktopWorkspace/Duck-pr84-checks-20261025/{referee,runtime}-release-publish.stdout.log/.stderr.log/.exit`。この生成はコンテナ起動や新規依存取得ではない。
- Dockerfileに固定された`mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca`と`mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3`の2つは、`docker image inspect`でローカル未発見。既存image一覧にも.NET基底イメージが無い。これらを自動pullするDocker buildは未承認の依存取得となり得るため、**実行していない**。
- 既存診断取得スクリプト`RemoteDesktopWorkspace/issue81-referee-health-probe-20261025.ps1`は`-RunOnce`を要求し、RefereeDriverコンテナ内の`GET /health/referee`を65秒以内で取得する。旧実機runの有界AppHost起動スクリプトの正確なファイル名・ソースSHAは、既存Todo・既知レポート・Workspace直下のスクリプト・旧run専用フォルダでは特定できなかった。過去の単発許可が消費済みなので新規起動を無制限に許可したと解釈しない。
- 結論：**修正後Windows実機は未起動・実機診断値なし・終了処理未実施**。今回新しく作成・停止したAppHostプロセス/コンテナは0件。起動には「固定digestの.NET基底イメージ2件の取得」と「新規有界AppHost一回起動および停止方式」の操作単位ごとの明示許可と安全な実行スクリプトの用意が必要。既存許可だけで依存の取得・設定変更を進めない。承認を得た場合のみ、固定Engine確認→必要な固定版取得→バイナリーSHA照合→4資源を75秒程度起動→CTRL_BREAK・最大120秒→自己所有コンテナのみ後片付け→GET /health/refereeとAPI/Ready/件数の新実測を保存する。

この追加検証は再開後の観測であり、元のIssue #81全体のG1/G3/試合受入/独立レビューや文書全文lintを完了したものではない。

## 2026-10-25 18:52 JST 固定Docker基底キャッシュと実機起動の追加境界

前節ではdocker image inspectで固定.NET10基底2件が未登録と記した。その後、同じdesktop-linux builderでdocker buildx du --verboseを読み取り実行し、aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398caとruntime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3がBuildKitキャッシュにpulled fromとして記録されていることを確認した。従ってimage storeで不在でも、固定基底の新規取得が必須とは断定しない。ただしキャッシュ記録だけで今後のビルドが外部通信しないことまでは保証しない。

- キャッシュ確認 process_id=9gphVB-rWtOxZlhW46DTwgrJtaxUSK-HtVyHUHuujqw、finished/exit0、固定.NET10基底digestの既存キャッシュ記録6件を確認。

- RefereeDriver単独の固定digest既存キャッシュ利用ビルドは成功した。既存Release publishを入力にdocker --context desktop-linux buildx build --pull=false --network=none --loadを実行し、process_id=qjXHXMartvR1iCAlvsZFdtf7vDnnCWA-ZUZ_6JPePLI、親/子の終了コード0、約4.01秒。BuildKitログには固定ASP.NET基底のメタデータ解決、WORKDIR CACHED、1.25MBのローカルCOPY、image exportを記録し、新規レイヤーダウンロード行なし。Dockerイメージ名duck-issue81-refereedriver:c0d748e、イメージID sha256:d165970598c4a57facf0b57d81ee4569eb93fd4e04c263449b9ca59191ed4d82、entrypointはdotnet Duck.Testing.RefereeDriver.Host.dll。ログはWindows RemoteDesktopWorkspace/Duck-pr84-checks-20261025/offline-driver-image.stdout.log/.stderr.log/.exit。これはローカルイメージ作成であり、実コンテナでのReady成功の証拠ではない。

- Duck RuntimeHost側の同様の既存固定.NET Runtime基底を利用したオフラインイメージビルドは、RDMCP process_startでOpenAI安全性チェックにより実行前拒否された。対象はduck-issue81-runtime:c0d748eのビルド、具体的拒否理由の説明なし、process_idなし、実行なし。拒否された同操作をコマンド変更やAppHost起動などで迂回実行しない。

- この直後もdocker ps -a=コンテナ0件、終了コード0。設定・認証・ネットワーク・権限・WSLは変更していない。既存PRの製品コードにも変更なし。

残る検証は拒否されたRuntimeHostイメージビルドと新規4資源の単発AppHost実行である。実機Runを行う場合は追加の操作承認を確認し、simulator/duck/game-controller/referee-driver、RunIdと固定Engine照合、約75秒後CTRL_BREAK、最大120秒停止期限、自己所有コンテナだけの清掃、RefereeDriver自身のGET /health/refereeのidentity/復号件数/GC API/Ready/HTTP状態を新規収集する。旧受信数値と旧503を新実測に流用しない。現時点の修正後Ready=200とGC API状態は未検証で、Issue全体は未完了。

## 2026-10-25 21:34 JST：Windows実機の安全な停止結果とGC起動依存の修正

上記は旧時点の保留状態である。利用者の個別許可後、Duck RuntimeHostを既存BuildKitの固定.NET10基底だけで画像化し、`duck-issue81-runtime:fe3eeef` のビルド終了コード0を確認した。process_id `IVMN5BpXttJHSLg55HVhiX2I0ec1r1vg8OwdnUXC7T0`。ログはWindows `Duck-pr84-checks-20261025/offline-runtime-image.*`。追加pull指示とDocker Desktop設定変更なし。

実機Runの経過は次のとおり。全て公開HEADを開始前に照合し、独立RunIdでWindows AppHostを起動、約75秒でCTRL_BREAK、強制停止なし・AppHost exit0、最終Dockerコンテナ0件を確認した。

| RunId | HEAD | 実機観測 | 受入結果 |
| --- | --- | --- | --- |
| `148e5d137195479d8e2974c47fefe94c` | `fe3eeef` | DCPがDocker CLIをPATH上で検出できずコンテナ0。AppHost PID27380 | 審判診断0、未達 |
| `758027dbfedc463b8293398d8cb1b2c2` | `fe3eeef` | AppHost子PATHのみにDocker CLI場所を追加。simulatorとDuckは起動、GC/Driverなし。AppHost PID30828 | GET診断0、未達 |
| `ed66819cec354a5b984d053e0a45bb7e` | `68c7b2d` | DCP Startingを候補にする変更後もsimulator/Duckのみ。AppHost PID31588 | GET診断0、未達 |

各実機Runの証拠はWindows `RemoteDesktopWorkspace/issue81-postfix-acceptance-<RunId>/process-result.safe.json` と `identity.safe.jsonl`。前のRunから審判復号件数やHTTP503を転用していない。

ResourceProjectionの全拒否を受けて、完全IDがあるDCP `Starting` だけを後続のDocker実稼働・固定Engine/所有ラベル照合候補にする変更 `68c7b2d` をTDDで追加。先行REDは5件中1失敗、後続GREEN5件。これにより旧テストのStarting拒否期待だけがCIで失敗したため修正 `2d85fb9` をpush。同HEADのCI `40190674986` はtest成功、packet-flow/full-stackはskipである。

さらに四資源のAppHostモデルを精査すると、Bridge明示選択時にもGCがDuckの健康を待つ `WaitFor(gcVisionDuck)` が残っていた。Duckが識別未帰属で503の間GCと後続のRefereeDriverが起動しない構成上の循環である。モデル試験で期待 `WaitUntilStarted` / 実際 `WaitUntilHealthy` の先行RED1件を保存。明示Bridge時のみ `WaitForStart(gcVisionDuck)`、Bridge無効時の `WaitFor(gcVisionDuck)` は維持した。GCモデル/RefereeDriverモデルの2件Green、TRX `Duck-pr84-checks-20261025/pr84-gc-wait-green.trx`、exit0、修正commit `7134755` を通常pushし、設計書「準備完了、依存、停止」に契約を追補した。

**最新GC起動条件の実Docker Desktop受入は未実施**であり、Ready200、GC API観測、審判UDP復号の修正後実測は存在しない。新たな有界単発起動には別途承認を確認し、固定HEAD/Engine/DLL SHA、事前の資源0、RunId、75秒CTRL_BREAK/最大120秒、自己所有資源のみ後片付けと診断を保存する。Windows全体11件の既存FileShare問題、文書全文lint、独立レビュー、packet-flow/full-stackも未完了である。

追加のモデル回帰はWindowsで `AppHostApplicationModelTests` 全体・`RegisteredContainerEventProjectionTests`・`StartingResource` の計45件を実行し、45成功・失敗0・省略0、保存終了コード0。診断は `RemoteDesktopWorkspace/Duck-pr84-checks-20261025/pr84-apphost-model-group.stdout.log` と同名 `.stderr.log` / `.exit` / `.trx`。今回の変更した Markdown は `git diff --check` で検査するが、Windows作業木には既存の `textlint` / `yomiyasu` CLI および `node_modules` が見つからず、新たな依存を導入しないため正式文書lint/yomiyasuの通過を主張しない。
