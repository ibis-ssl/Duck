# 独立仕様対応表

対象は ibis-ssl/Duck Issue81 / PR84、I81-Vとして既存I81-01/I81-02を事後検証する。
過去の実装前Redは再現しない。TDD例外承認でも独立最終レビューでもない。
API宣言・HTTP metadataはコンパイル形を決める資料であり、期待動作の根拠ではない。
「導出」は設計要件を最小のfixtureへ具体化したもの。理由文字列や実装の内部値から補完しない。

| ID | fixture | 期待値 | 設計行・節 | 独立根拠 |
| --- | --- | --- | --- | --- |
| R01 | 開始のみ | not-ready | designs/aspire-simulation-test-environment.md:181,183 | 通信開始とサービス自身の証跡を分ける |
| R02 | 同一対象の新鮮な成功復号10件 | ready | designs/aspire-simulation-test-environment.md:158,181,230; designs/tracker-comparison-debug-design.md:249 | 10件を十分な正の進捗fixtureとして使用。一般状態の最小件数は規定しない |
| R03 | run/resource/container/generationの各不一致 | not-ready | designs/aspire-simulation-test-environment.md:19,158,183; designs/tracker-comparison-debug-design.md:40 | 他対象の証跡は対象自身の証跡ではない |
| R04 | 別入力だけの復号 | not-ready | designs/aspire-simulation-test-environment.md:80,158,230 | 必要入力を別用途入力で代用できない |
| R05 | auxiliary/unknown evidence | not-ready | designs/aspire-simulation-test-environment.md:31,139,230; designs/tracker-comparison-debug-design.md:256 | 独立観測器だけでは代用できない |
| R06 | failed/unknown decode | not-ready | designs/aspire-simulation-test-environment.md:183,230 | 成功未確認は成功復号の証明にならない |
| R07 | ready後のdecode failure | not-ready | designs/aspire-simulation-test-environment.md:183 | 復号失敗で準備未完了へ戻す |
| R08 | 窓5秒に対しage6秒 | not-ready | designs/aspire-simulation-test-environment.md:181,183; designs/tracker-comparison-debug-design.md:118 | 等値境界は規定せず十分古い証跡を排除 |
| R09 | fresh ready後6秒無更新 | not-ready | designs/aspire-simulation-test-environment.md:183; designs/tracker-comparison-debug-design.md:243 | 期限切れで準備未完了へ戻す |
| R10 | age不明/負値 | not-ready | designs/aspire-simulation-test-environment.md:181,183; designs/tracker-comparison-debug-design.md:118 | 新しさを証明できない無効・不明の時間値を認定しない |
| R11 | 新コンテナ/世代へ更新、旧証跡、次に新証跡 | not-ready / not-ready / ready | designs/aspire-simulation-test-environment.md:150,183; designs/tracker-comparison-debug-design.md:253 | 対象変更で旧証跡を無効にし新対象の新しさで復帰 |
| R12 | 成功件数-1/0のみ | not-ready | designs/aspire-simulation-test-environment.md:158,230 | 成功復号がない件数は成功証拠にならない |
| S01 | 受信開始だけ | not-ready | designs/aspire-simulation-test-environment.md:181,230 | ソケット開始を成功復号とみなさない。snapshot未生成も許容 |
| S02 | 10回の公開成功復号記録 | count10/failure0/ID/input/time/status/ready | designs/aspire-simulation-test-environment.md:158,230 | 診断が対象と件数・受信時刻・状態を結び付ける |
| S03 | 10回失敗だけ | success0/failure10/not-ready | designs/aspire-simulation-test-environment.md:158,183,230 | 成功件数と失敗件数を混同しない |
| S04 | ready後の失敗1回 | success10/failure1/failed/not-ready | designs/aspire-simulation-test-environment.md:158,183 | 復号失敗を診断と準備解除へ反映 |
| S05 | 公開された有限鮮度窓より1秒長く停止 | not-ready | designs/aspire-simulation-test-environment.md:181,183; designs/tracker-comparison-debug-design.md:243 | 閾値の数値を期待値へ取り込まない。任意の有限窓外を検査 |
| S06 | identityなしの成功10件 | not-ready | designs/aspire-simulation-test-environment.md:158; designs/tracker-comparison-debug-design.md:40 | 対象自身へ結び付けられない件数だけでは証明できない |
| S07 | 三送信元が各10件 | key/name/uuid/endpoint/count保持 | designs/tracker-comparison-debug-design.md:136,140-150,249-250 | 送信元識別情報を保持。role resolverの代用ではない |
| S08 | 古い受信の遅延復号 | 受信時刻保持/not-ready | designs/aspire-simulation-test-environment.md:158,181,183; designs/tracker-comparison-debug-design.md:118 | 受信時刻と新しさは復号完了で若返らない |
| S09 | 期限切れ後、一方の送信元だけ更新 | 更新元ready/無更新元not-ready | designs/tracker-comparison-debug-design.md:118,243,276 | 一送信元で他送信元の新しさを代用しない |
| P01 | 公式protoのUUID/名称/TrackedFrame | TryDeserialize=true / packet非null | designs/tracker-comparison-debug-design.md:21,35-38,249-250; schemas/ssl-game-controller/proto/tracker/ssl_vision_wrapper_tracked.proto:7-14; schemas/ssl-game-controller/proto/tracker/ssl_vision_detection_tracked.proto:69-88 | 固定protoから独立した手動wire符号化 |
| P02 | 途中で切れたlength/varint/fixed64 | TryDeserialize=false | designs/aspire-simulation-test-environment.md:183; schemas/ssl-game-controller/proto/tracker/ssl_vision_wrapper_tracked.proto:10-14 | 不完全wire値は正常復号ではない |

## 未規定のため合格条件へ追加しない項目

- ResourceReadinessReason / ServiceReceiveSnapshot.Reasonの字面と優先順位。
- sequenceの順序・同値・並べ替え、count regression後の回復手順、constructor/ParseIdentityの例外型や文字列検証。
- generationの開始値・増分・無効値の受理規則。異なる世代が対象を代用しないことだけ扱う。
- 鮮度の等値境界とサービス既定窓の数値。一般状態の最小復号件数。
- HTTP status、JSON casing、既定モード・既定待受prefixなど設計に明記のない表現。

## 宣言不足・未検証

- RuntimeHost実受信datagramから診断への接続は公開投入seamがない。
- DebugHost vision受信経路のfixtureに必要な依存公開宣言が元packにない。
- DebugHost /health/vision と /health/comparisonの実HTTP到達・マッピング。metadataだけでは振る舞いを判断しない。
- tracker deserializerが使うInputIdの公開宣言なし。GetSnapshotsとの接続を推測しない。P01/P02はboolだけの限定試験。
- 比較resolver公開宣言はdeltaにあるとの親連絡のみ。delta読取は自動承認レビュー拒否で未実行・未読。

## 今回対象外

後続DCP配置、AddContainer、所有ID取得、外部tracker実行、Windows/Docker Desktop実機、受入全体、他OS、UI差分、記録/再生、debug接続、Crane/cm4経路。
これらを新合格条件へ追加せず、単体試験の成功をWindows受入へ代用しない。

## 実行計画

この段階はテスト確定まで。製品のビルド/実行、dotnet/tool/cacheの調査は未実施。
凍結manifestを親が照合し、明示指示後にのみ初回実行する。
既存依存キャッシュだけでのoffline assets作成方法を親が確定した後、初回試験予定は次の通り。

    dotnet test /tmp/issue81-independent-spec-tests-worker-20261022/IndependentSpecificationTests.csproj --no-restore --configuration Debug --logger 'trx;LogFileName=initial.trx' --results-directory /tmp/issue81-independent-spec-tests-worker-20261022/results/initial -p:ProductRoot=/home/donabe/Duck-issue81-aspire-desktop-bridge

依存導入・ネットワーク/auth/環境変更・実ホスト起動・製品編集・commit/push/GitHub操作をしない。
依存不足・実行制約を明示し、失敗のskip化/期待値緩和はしない。製品原因の調査は既存実装担当へ戻す。
