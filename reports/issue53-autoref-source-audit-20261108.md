# Issue #53 AutoRef受信経路のソース監査

確認日: 2026-11-08  
対象: MATCHで使う Linux/amd64 image `tigersmannheim/auto-referee@sha256:96dd8417c1fcf4025ee0a20c8210954068440991989587b68ecdea7d27db6b3d`。

Docker Hubの `latest` manifestを読み取り、同digestがLinux/amd64 child manifestであることを確認した。TIGERs-Mannheim/AutoReferee の [Build and push to docker run 40291567450](https://github.com/TIGERs-Mannheim/AutoReferee/actions/runs/40291567450) はsource commit `3617de906213971972aa89da6982623b6058caf8` で同じ `latest` tagをbuild/pushし、ログ中のamd64 manifest digest prefixは `96dd8417c1fcf4025` と一致する。image configにsource revision labelはなく、commit対応はこのbuild記録とregistry manifestの照合に基づく。

## 確認できた通信方向

- `AutoReferee` の `--trackerAddress` は `VisionTrackerSender.setCustomAddress` / `setCustomPort` に適用される。`VisionTrackerSender` は tracker packet を設定先へ送信する。したがって AutoRef の `11010` は送信先である。([AutoReferee.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/src/main/java/edu/tigers/autoref/AutoReferee.java#L45-L47), [VisionTrackerSender.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/modules/moduli-wp/src/main/java/edu/tigers/sumatra/wp/exporter/VisionTrackerSender.java#L46-L77))
- AutoRef の SSL-Vision 経路は `SSLVisionCam` で UDP datagram を受け、`SSL_WrapperPacket.parseFrom` により復号する。審判情報は `NetworkRefereeReceiver` で UDP datagram を受け、`Referee.parseFrom` により復号する。([SSLVisionCam.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/modules/moduli-cam/src/main/java/edu/tigers/sumatra/cam/SSLVisionCam.java#L130-L145), [NetworkRefereeReceiver.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/modules/moduli-referee/src/main/java/edu/tigers/sumatra/referee/source/NetworkRefereeReceiver.java#L86-L108))
- 共通の `MulticastUDPReceiver` は packet / decode counter を保持しない。公開された observer callback は socket timeout の通知だけである。([MulticastUDPReceiver.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/modules/common/src/main/java/edu/tigers/sumatra/network/MulticastUDPReceiver.java#L69-L78), [同 receive loop](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/modules/common/src/main/java/edu/tigers/sumatra/network/MulticastUDPReceiver.java#L148-L172), [IReceiverObserver.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/modules/common/src/main/java/edu/tigers/sumatra/network/IReceiverObserver.java#L9-L15))

## Issue / 設計への影響

現在の `Tracker/Design/tasks-status.md` の ASPIRE-006G3 行は、AutoRef が 10020 / 11003 を受信し、11010 へ追跡情報を出力する、と方向を区別している。これが上記ソースと一致する。一方、設計履歴の旧 readiness 表には 11010 を AutoRef の「入力数」とする記述があり、固定版ソースの送信経路と矛盾する。11010 を AutoRef の入力受信数として要求するのは誤りであり、AutoRef 由来の出力は Duck 側で source UUID / endpoint / 件数増加と対応づけて確認する。

既存 run の `Received geometry from vision`、`ACTIVE`、Game Controller 登録ログは AutoRef の状態・処理を示すが、10020 / 11003 の受信・復号件数ではない。Recorderや Duck の診断、外部 multicast capture も AutoRef 自身の受信数の代わりにはならない。

## 残作業

AutoRefには、復号後のvision packetを `SSL_VISION_2014` として、Referee messageを `SSL_REFBOX_2013` として保存する既存 `GameLogRecorder` がある。([SSLVisionCam.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/modules/moduli-cam/src/main/java/edu/tigers/sumatra/cam/SSLVisionCam.java#L169-L190), [Referee.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/3617de906213971972aa89da6982623b6058caf8/modules/moduli-referee/src/main/java/edu/tigers/sumatra/referee/Referee.java#L200-L209))。Duck側には `SSL_LOG_FILE` のrecord typeを数える既存.NET `SslLogRecordReader` がある。

ただし、この recorder は通常起動時に自動開始しない。`GameLogRecorder.setRecording(true)` がwriterを開き、writerが閉じている間の `write` は何も保存しない。match構成はAutoRefを `--headless` で起動し、CLIには録画開始引数がない。既存run artifactにもAutoRef native gamelogはない。このため、現状のままでは内蔵機構から件数を取り出せない。

最短の非破壊経路は、MATCH時だけ内蔵GameLogRecorderを開始し、出力ファイルをrun artifactへ保存して既存.NET readerで件数を数えること。受信・復号処理へ独自カウンターを差し込む方式は不要である。headless起動での記録開始方法とartifact保存は未実装であり、現時点で受入完了とはしない。

この監査ではリポジトリの製品コード変更、Docker起動、テスト実行、AutoRef / Crane の実行動作変更は行っていない。
