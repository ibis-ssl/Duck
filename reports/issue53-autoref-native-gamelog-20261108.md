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
