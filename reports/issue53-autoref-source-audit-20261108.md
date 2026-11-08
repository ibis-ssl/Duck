# Issue #53 AutoRef受信経路のソース監査

確認日: 2026-11-08  
対象: AutoReferee upstream commit `1cb2545b81f3568767139a145e3c1aa062399848`。これはリポジトリ設計履歴に記録された AutoRef 1.2.0 の調査基準である。MATCH実行時に使った `latest` digestとのsource revision対応は確認できておらず、この監査は対象コンテナの実装・実行時受信カウンターを確定するものではない。

## 確認できた通信方向

- `AutoReferee` の `--trackerAddress` は `VisionTrackerSender.setCustomAddress` / `setCustomPort` に適用される。`VisionTrackerSender` は tracker packet を設定先へ送信する。したがって AutoRef の `11010` は送信先である。([AutoReferee.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/1cb2545b81f3568767139a145e3c1aa062399848/src/main/java/edu/tigers/autoref/AutoReferee.java#L45-L47), [VisionTrackerSender.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/1cb2545b81f3568767139a145e3c1aa062399848/modules/moduli-wp/src/main/java/edu/tigers/sumatra/wp/exporter/VisionTrackerSender.java#L46-L77))
- AutoRef の SSL-Vision 経路は `SSLVisionCam` で UDP datagram を受け、`SSL_WrapperPacket.parseFrom` により復号する。審判情報は `NetworkRefereeReceiver` で UDP datagram を受け、`Referee.parseFrom` により復号する。([SSLVisionCam.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/1cb2545b81f3568767139a145e3c1aa062399848/modules/moduli-cam/src/main/java/edu/tigers/sumatra/cam/SSLVisionCam.java#L130-L145), [NetworkRefereeReceiver.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/1cb2545b81f3568767139a145e3c1aa062399848/modules/moduli-referee/src/main/java/edu/tigers/sumatra/referee/source/NetworkRefereeReceiver.java#L86-L108))
- 共通の `MulticastUDPReceiver` は packet / decode counter を保持しない。公開された observer callback は socket timeout の通知だけである。([MulticastUDPReceiver.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/1cb2545b81f3568767139a145e3c1aa062399848/modules/common/src/main/java/edu/tigers/sumatra/network/MulticastUDPReceiver.java#L69-L78), [同 receive loop](https://github.com/TIGERs-Mannheim/AutoReferee/blob/1cb2545b81f3568767139a145e3c1aa062399848/modules/common/src/main/java/edu/tigers/sumatra/network/MulticastUDPReceiver.java#L148-L172), [IReceiverObserver.java](https://github.com/TIGERs-Mannheim/AutoReferee/blob/1cb2545b81f3568767139a145e3c1aa062399848/modules/common/src/main/java/edu/tigers/sumatra/network/IReceiverObserver.java#L9-L15))

## Issue / 設計への影響

現在の `Tracker/Design/tasks-status.md` の ASPIRE-006G3 行は、AutoRef が 10020 / 11003 を受信し、11010 へ追跡情報を出力する、と方向を区別している。これが上記ソースと一致する。一方、設計履歴の旧 readiness 表には 11010 を AutoRef の「入力数」とする記述があり、固定版ソースの送信経路と矛盾する。11010 を AutoRef の入力受信数として要求するのは誤りであり、AutoRef 由来の出力は Duck 側で source UUID / endpoint / 件数増加と対応づけて確認する。

既存 run の `Received geometry from vision`、`ACTIVE`、Game Controller 登録ログは AutoRef の状態・処理を示すが、10020 / 11003 の受信・復号件数ではない。Recorderや Duck の診断、外部 multicast capture も AutoRef 自身の受信数の代わりにはならない。

## 残作業

ASPIRE-MATCH-004 を満たすには、AutoRef のプロセス内で復号成功を数える証跡が必要である。参照した upstream source には、そのカウンターや per-packet 診断は見つからなかった。実際に使う `latest` digestが同じコードか、コンテナの診断に別のカウンターがあるかは未確認である。実コンテナにも証跡がなければ、受信経路を観測する検証アダプターが必要となる。既存の Java 受信処理を計測する範囲・実装方式・実行時への影響はまだ決めておらず、この監査だけではアダプター実装や受入を完了扱いにしない。

この監査ではリポジトリの製品コード変更、Docker起動、テスト実行、AutoRef / Crane の実行動作変更は行っていない。
