# 追加独立仕様対応表 v2

本人による境界訂正を受け、前回未実行0byteだった追加公開宣言の読取/hashを同じcmd・同じ通常承認経路で一度だけ再審査した。今回は成功（exit0）。
公開宣言抽出は元々成功しており、最初に拒否されたのは本担当の追加pack読取。今回の成功は新しい明確な本人指示に基づく再審査結果であり、別経路への迂回ではない。

期待動作の根拠は引き続き承認設計。deltaはAPIの形だけに使う。製品本体・既存試験・差分/実装履歴は未読。
元v1の11file・期待値・試験・初回BCL/CI結果は保持する。本版は追加分のみで、過去Redの再現・TDD例外承認ではない。

| ID | fixture | 期待値 | 承認設計/固定schemaの行 | 根拠と限界 | ケース数 |
| --- | --- | --- | --- | --- | --- |
| C01 | 正式source name三者 | Duck/TIGERs/ER-Forceへ指定どおり解決 | designs/tracker-comparison-debug-design.md:140-146 | source nameの完全一致対応表 | 3 |
| C02 | 大小文字・余白・未定義・空の8名 | LogicalRoleなし | designs/tracker-comparison-debug-design.md:146,148 | 未定義名称からroleを推測しない | 8 |
| C03 | 未定義nameと既知label | LogicalRoleなし | designs/tracker-comparison-debug-design.md:138,146,148 | roleの根拠はsource nameであり表示labelではない | 3 |
| C04 | SourceRole own/external/unknown、各三名 | 既存SourceRoleによらず正式logical role | designs/tracker-comparison-debug-design.md:138,280 | 既存SourceRoleとlogical roleは別概念 | 3 |
| C05 | 同name/同UUID、別endpoint/label | 同じ非空IdentityKey | designs/tracker-comparison-debug-design.md:136,148,150,282 | UUID優先、endpoint数でsourceを分裂させない | 3 |
| C06 | 同name/endpoint、別UUID | 別IdentityKey | designs/tracker-comparison-debug-design.md:148,150,281,284 | UUIDがidentityを決める | 1 |
| C07 | 同UUID、別role名/endpoint | roleは各名どおり、IdentityKeyは同一 | designs/tracker-comparison-debug-design.md:148,150,283 | 競合をendpoint分裂で隠さないためのresolver前提。health判定の試験ではない | 1 |
| C08 | UUID null/空、同name/endpoint、別label | 同じ非空fallback IdentityKey | designs/tracker-comparison-debug-design.md:136,148,285 | UUID欠落時はsource name+endpointで代用 | 2 |
| C09 | UUID null/空、同name、別endpoint | 別fallback IdentityKey | designs/tracker-comparison-debug-design.md:136,148,285 | 異なるendpointの証跡を混ぜない | 2 |
| C10 | UUID null/空、同endpoint、別name | 別fallback IdentityKey | designs/tracker-comparison-debug-design.md:136,148,285 | source nameとendpointの組で分離 | 2 |
| C11 | 同UUID/endpoint/name、別SourceRole | 同一IdentityKey | designs/tracker-comparison-debug-design.md:138,148 | UUID identityを既存roleから独立させる | 1 |
| V01 | 独立公式vision datagram10件、1msずつ進行 | count10/error0/最新受信時刻/検出frame10/ball座標123,-456 | designs/aspire-simulation-test-environment.md:158,230-231; designs/tracker-comparison-debug-design.md:249; schemas/ssl-game-controller/proto/vision/ssl_vision_detection.proto:3-18,39-57; schemas/ssl-game-controller/proto/vision/ssl_vision_wrapper.proto:15-18 | サービス公開storeで件数・入力時刻・検出内容を保持。実受信結線の代用にはしない | 1 |
| V02 | 別内容の検出2件、時刻差20ms | 最新のframe/座標/受信時刻を同じsnapshotに保持 | designs/aspire-simulation-test-environment.md:158,231; designs/tracker-comparison-debug-design.md:249 | 検出内容と入力時刻の対応を記録する限定store試験 | 1 |
| V03 | 公開RecordDecodeErrorを1回呼出 | success packet0/error1/失敗診断非空 | designs/aspire-simulation-test-environment.md:158,183,230 | 明示的な失敗記録を成功受信に数えない。parser異常入力結線は未検証 | 1 |
| V04 | 公式geometry-only wrapper | 正常wrapper count1/error0/Geometryあり/Detectionなし | designs/aspire-simulation-test-environment.md:230-231; schemas/ssl-game-controller/proto/vision/ssl_vision_wrapper.proto:15-18; schemas/ssl-game-controller/proto/vision/ssl_vision_geometry.proto:47-82,135-139 | 正常wrapperと新しい検出データを区別する限定fixture。readiness値は試験しない | 1 |

## 追加範囲

比較resolverは11メソッド29ケース、vision storeは4メソッド4ケース。計15メソッド33ケース相当。
C04の各ケースは三つのsource nameすべてを確認する。source名・UUID・endpoint・SourceRoleの独立fixtureを使用。
IdentityKeyの内部表現・接頭辞・理由文字列を期待値にしない。比較は同一性/相違性のみ。
公式vision wrapperは公開protoのfield番号/wire型から手動符号化し、既存fixture・生成物・製品コードは読んでいない。

## 依然未検証の境界

- 純粋な単一source resolverにはsnapshot時刻・role集合・ready結果がない。三role一意性・競合時not-ready・最新代表選択・同じ有限窓内のcomparison health集約は本版で検証済みとしない。
- VisionPacketStoreの公開StoreDatagram/RecordDecodeErrorの試験は、実VisionReceiverServiceのUDP受信→ServiceReceiveDiagnostics結線やHTTP公開への結線を代用しない。
- 追加依存型宣言を読めるようになったが、実受信投入/health helperの非公開という別の技術境界は残る。BackgroundService/ホスト/実機を起動せずに構成できる純粋fixtureへ限定した。
- StoreDatagramの不正wireに対する例外/失敗記録契約はAPI形だけでは決めない。V03は明示的な公開失敗記録の限定試験。
- RuntimeHost実受信datagram投入、Debug HTTP実到達・mapping、tracker診断InputIdの公開宣言不足は未解決。
- delta読取拒否だけがHTTP全体を不可にしたという説明はしない。

## 実行計画と禁止事項

追加版は未ビルド・未実行。manifest照合と親の明示指示後、既存キャッシュ/既存CI経路のみで実行する。
新規依存install・環境/auth/ネットワーク設定変更・外部変更・merge・実装閲覧・追加agentは禁止。
実行harness/正式CIの選択は親所有。想定targetはAdditionalSpecificationTests.csprojまたは同一byte追加sourceを既存Tracker.Testsへ統合したexact-HEAD CI。
失敗時は既存実装担当が原因調査し、同じ凍結追加テストで再実行する。緩和・skip化・元v1変更をしない。
テスト誤り訂正には承認設計根拠と新旧hashを記録する。
後続DCP配置・所有ID取得・Windows実機・外部tracker・受入全体は今回の追加合格条件にしない。
