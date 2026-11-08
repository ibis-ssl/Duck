# #53 AutoRef 内蔵ログの開始・保存

## 対象

- リポジトリ: `ibis-ssl/Duck`
- Issue / PR: #53 / #121（Draft、base `task/issue50-aspire-006g`）
- 作業ブランチ: `task/issue53-aspire-006g3`
- 実装前 HEAD: `80957bfd108fde5080f5b8db29156eb8d585578b`
- 実装コミット: `ec7853c4a4be531d5e295d7b87c71e840424502a`

## 実装

- MATCH 時のみ、AutoRef JVM に起動フックを渡す。フックは AutoRef の既存 `GameLogRecorder` が利用可能になった後、公開 API `setMatchInfo` と `setRecording(true)` を呼ぶ。
- AutoRef の復号・受信経路、Crane、planner、送信先は変更しない。独自のパケットカウンターも追加しない。
- AutoRef が生成する `data/gamelog/*.log` を run 固有のホスト保存先へ bind mount し、MATCH cleanup で AutoRef を停止してから gzip 形式の artifact に保存する。
- 既存の .NET `SslLogRecordReader` で保存済みログを読み、既存 `RefereeRecords` と `VisionDetectionRecords`、展開完了、読取失敗を JSON に出す。これは AutoRef の内蔵ログが記録した復号後メッセージ数であり、wire 上の全 UDP datagram 数とは区別する。
- Linux CI で agent JAR を生成し、AppHost の MATCH 実行時にその asset が見つからない場合は起動前に失敗させる。

## 確認状況

- `Tracker.Tests` の `SslLogRecordReaderTests`: 16/16 成功。
- 同テスト実行中に AppHost を含む .NET プロジェクト群がビルド成功。
- `git diff --check` と agent build script の `bash -n`: 成功。
- 実装を含む CI 対象 HEAD: `332214d42fa7ad33e18b1dd470cfe4fb543d5cb3`。
- exact-HEAD CI run [42465310887](https://github.com/ibis-ssl/Duck/actions/runs/42465310887): 成功。Java agent のコンパイルと JAR 化、1363 件（成功1359、skip4）の .NET tests、Linux publish を確認。
- 同runの ASPIRE-NET-002 と full-stack Linux acceptance job は skip。Docker 実受入を行ったrunではない。
- 実行ユーザー: `FA780\donabe`。
- ローカル Windows 環境に `java` / `javac` / `jar` はなく、Java agent のコンパイルは Linux CI run 42465310887 で確認済み。
- Docker実受入 run [42467650903](https://github.com/ibis-ssl/Duck/actions/runs/42467650903) をHEAD `9c2a64d87d0dae21935bc3fc5a9d8873e2ccd176` で実行した。HEADのAppHost build / publish / startとfull-stack readinessは成功した。
- 受入結果では `MatchControllerOutcome=MaximumTimeReached`、`fullMatchPassed=false`。packet/active-motion acceptance stepはskip。別の verifier summary の `status=passed` は ASPIRE-006G1 / ASPIRE-MATCH-001/002 の範囲で、#53 の完了証拠ではない。
- AutoRef native game-log capture statusは `missing`。AutoRef `.log` と既存.NET reader summaryは得られず、AutoRef自身の10020/11003件数は未確認。Recorder側の全体件数は referee 16,764、vision detection 25,993、tracker 66,695 だが、AutoRef個別の受信数を示さない。
- Referee 11003 captureは11,776 datagrams、観測tuple `172.18.0.3:33222`。送信プロセスの帰属は未確認で、11010のAutoRef/Sumatra送信およびDuck非送信も未確認。
- Full-stack artifact uploadは、AutoRef logがない場合の未展開globが literal `*` のまま渡されたため失敗し、full-stack artifactはアップロードされなかった。修正 `705731b` で空配列として扱う。したがって今回の詳細ログartifactは取得できていない。
- 試験直前に Docker Hub `latest` manifestを再確認し、Linux/amd64 child digest が指定値 `sha256:96dd8417c1fcf4025ee0a20c8210954068440991989587b68ecdea7d27db6b3d` と一致した。

## 未完了

この変更だけでは #53 の実通信受入は完了しない。MATCH run の artifact で AutoRef の referee / vision 件数が正であること、AutoRef と Sumatra の 11010 出力、Duck の 11010 非送信を、対象 run・image・HEAD と対応づけて確認する必要がある。Docker を使う動作確認は、実行直前のユーザー承認後に行う。

## 追記: MATCH run 42469970045 と起動診断

- run [42469970045](https://github.com/ibis-ssl/Duck/actions/runs/42469970045) は HEAD `d947c07543202118571447760691143e20b266a5`、AutoRef Linux/amd64 image `sha256:96dd8417c1fcf4025ee0a20c8210954068440991989587b68ecdea7d27db6b3d` で実施。artifact upload は glob 修正後に成功し、match result、recorder archive、motion summary、container logs を取得できた。
- recorder archive は complete、127,071 records。既存 motion verifier は passed を返し、referee counter 2 / 6 / 10 の3窓で Crane と TIGERs が各11台動いた。同時に match result は `MaximumTimeReached` / `fullMatchPassed=false` であり、試合全体の完了は未確認。初期 setup の SimulatorResponse は5秒以内に観測されなかったが、これはsetup未受信・未適用の証明ではない。
- agent JAR と `JAVA_TOOL_OPTIONS` は読み込まれ、AutoRef 自体は ACTIVE に移行し Game Controller 登録と Vision geometry 受信がログに残った。一方、180秒後に `DUCK_AUTOREF_GAMELOG_STARTED=false reason=startup-timeout`。例外スタックはなく、Agent code が待つ Sumatra module state の `ACTIVE` と `GameLogRecorder` module の存在のどちらで止まったかは、このartifactだけでは判別できない。
- AutoRef native game-log は生成されず、AutoRef 自身の10020 / 11003受信・復号件数は未確認。11003外部captureは11,774件、単一tuple `172.18.0.3:54721` だが、送信プロセス帰属は未確認。Duckのtracker observerは異なるUUIDの2 sourceを記録し件数は35,029 / 35,552、双方のsourceNameは `TIGERs`。起動ログではAutoRefとSumatra双方が11010送信先を設定しているが、UUIDを実プロセスへ結び付けられず、送信元別の受入条件およびDuckの11010非送信は未確認。
- この2条件だけを識別する最小診断を Agent の timeout 出力へ追加した（commit `e74d967`）。直近に観測した module state と recorder module の有無を出すだけで、AutoRef/Craneの起動・通信動作は変更しない。exact-HEAD CI run [42471622288](https://github.com/ibis-ssl/Duck/actions/runs/42471622288) はJava agent build、.NET tests 1,359 passed / 4 skipped、Linux publish が成功。ローカル Windows suiteはJava agent JAR未生成とLinux verifier非対応のため 1,346 passed / 2 skipped / 15 failed だった。Linux agent buildを含むCI結果を対象HEADの検証とする。
- 診断値を得る Docker 再実行は行っていない。次に必要なのはPR #121の診断入りHEADで同一imageを使う1回のMATCHであり、実行前に対象・目的を提示する。#53 は未完了。

## 追記: 診断付き MATCH run 42472375043

- run [42472375043](https://github.com/ibis-ssl/Duck/actions/runs/42472375043) は HEAD `2c0ea80f97205678208cf827a1f629bc6ea3d95d`、AutoRef Linux/amd64 image digest `sha256:96dd8417c1fcf4025ee0a20c8210954068440991989587b68ecdea7d27db6b3d` で実施した。CI の .NET test、agent JAR build、Linux publish と全リソース readiness は成功した。実行後の scoped cleanup も成功し、full-stack artifact は保存された。
- run 全体は失敗。MatchController は `MaximumTimeReached` で終了し `POST_GAME` に到達せず、`fullMatchPassed=false`。これは #53 の完了判定ではなく、#54 の POST_GAME 条件とも区別する。
- 新しい timeout 診断は `modulesState=ACTIVE recorderModuleAvailable=false` を記録した。AutoRef は `ACTIVE` に移行し Game Controller へ登録、Vision geometry を受信していたが、`GameLogRecorder` が見つからず、180秒で `DUCK_AUTOREF_GAMELOG_STARTED=false reason=startup-timeout`。AutoRef native game-log archive は生成されなかったため、AutoRef 自身の 10020 / 11003 受信・復号件数は今回も未確認。timeout の事実は絞れたが、Recorder module が無い理由の特定や解決はこの診断では行っていない。
- motion archive は complete（130,056 records）で、counter 2 / 6 / 10 / 14 / 20 の5窓で Crane と TIGERs がそれぞれ11台動いた。これは実通信試合の全完了や AutoRef の内部受信件数を示すものではない。
- 外部 referee 11003 capture は11,837件、観測 tuple `172.18.0.3:41395`。送信プロセス／コンテナへの帰属は未確認。Duck tracker observer は異なる2 sourceを35,572件と35,570件復号したが、送信プロセス帰属と各11010送信元は未確認。Duck が11010へ送信しないことも確認できていない。
- 初回 setup の SimulatorResponse は5秒以内に観測されなかった。これは simulator が setup datagram を受信・適用しなかった証拠ではない。配置イベントには、その後の同一チーム BALL_PLACEMENT から DIRECT/FREE_KICK への進行を新鮮な UDP/API 観測で確認したものが3件ある。
- full-stack job の最後の archive gate は失敗した。recorder archive 自体は finalized され、motion verifier も成功したが、AutoRef native archive が無かったため gate が必要な全 archive 検証を完了できなかった。従って Docker run は合格として扱わない。
- この実行は診断結果を得る今回承認済みの一回であり、追加の Docker run は行っていない。PR #121 は Draft、Issue #53 は Open のままとする。
## 追記: recorder が有効 module に無かった根因と修正

### 根因の確認

- 対象 image の対応ソースは AutoReferee commit `3617de906213971972aa89da6982623b6058caf8`。その既定 [`config/moduli/autoreferee.xml`](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/config/moduli/autoreferee.xml) には `GameLogRecorder` module がなく、`RecordManager` は既存の `AutoRefRecordManager` を使う。AutoRef 起動コードは MATCH 時も `autoreferee.xml` を選ぶ。実行時診断 `modulesState=ACTIVE recorderModuleAvailable=false` と整合する。
- fixture にあった `GameLogRecorder` は Sumatra の `tigers-blue` 用 `simulation_protocol_tigers.xml` の定義であり、AutoRef コンテナには適用されていなかった。AutoRef の classpath に recorder class は含まれるが、active `SumatraModel` に module instance が登録されていなかった。
- 保存先にも別の配線不一致があった。対象 image の Jib `workingDirectory` は `/`、既存 upstream [`GameLogWriter`](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/modules/moduli-gamelog/src/main/java/edu/tigers/sumatra/gamelog/GameLogWriter.java) の相対出力先は `data/gamelog`。したがって実際の出力先は `/data/gamelog` であり、従来の `/home/default/data/gamelog` mount には書かれない。

### 最小修正

- pinned AutoRef source の既定 `autoreferee.xml` を match fixture とし、`GameLogRecorder` module 1件だけを追加。既存の AutoRef module、`AutoRefRecordManager`、ACam / AReferee の設定、送受信先は維持する。
- fixture を AutoRef の `/config/moduli/autoreferee.xml` に read-only mount し、run 固有の writable directory を正しい `/data/gamelog` に mount する。
- `AppHostApplicationModelTests.MatchModeRegistersTigersAutoRefAndLogRecorderOnSharedDcpBridge` に、fixture の module 構成、AutoRef config mount、出力 mount の向き先と読み書き属性を確認する assertions を追加。

### 検証と未確認

- fixture は .NET `XmlDocument` で parse でき、9 module と `GameLogRecorder` / `AutoRefModule` / `AutoRefRecordManager` を確認。
- AppHost と test assembly の compile は focused `dotnet test` build で成功。focused model test は 1/1 成功。ただしこの Windows 環境には有効な agent JAR を作る `javac` が無く、モデル構成だけをテストするためテストプロセスの output directory に一時的な空ファイルを置いて AppHost の存在チェックを通し、テスト後に削除した。これは Java agent build や Docker runtime の検証ではない。
- 正規 agent JAR を生成する CI の current-HEAD run は push 後に確認する。新しい Docker MATCH は未実施。追加の Docker run を行う前に対象 HEAD・image digest・目的をユーザーへ報告する。
- AutoRef 自身の 10020 / 11003 受信・復号件数、11010 送信、Duck の 11010 非送信は未確認であり、#53 は未完了。