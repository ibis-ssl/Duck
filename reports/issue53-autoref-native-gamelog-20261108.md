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
- GitHub Linux CI は実装コミットを push 後に照合する（現時点では未実行）。
- 実行ユーザー: `FA780\donabe`。
- ローカル Windows 環境に `java` / `javac` / `jar` がなく、Java agent のコンパイルは Linux CI で確認する。
- この作業では Docker を起動していない。Linux の match run、AutoRef の実ファイル生成、artifact summary はまだ未確認。

## 未完了

この変更だけでは #53 の実通信受入は完了しない。MATCH run の artifact で AutoRef の referee / vision 件数が正であること、AutoRef と Sumatra の 11010 出力、Duck の 11010 非送信を、対象 run・image・HEAD と対応づけて確認する必要がある。Docker を使う動作確認は、実行直前のユーザー承認後に行う。
