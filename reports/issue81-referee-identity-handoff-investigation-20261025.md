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
