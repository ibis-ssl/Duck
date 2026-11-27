## 実行コマンド
### 通信・JSON境界：6件runtime Red後の最小候補（2026-10-22）

- exactbecd47ca / CI39827360572 は666中660成功/6実assertion失敗、timeout/error0。transport13件は6失敗/7成功、旧653/固定67/raceは個別Passed。親が採用したRedを起点に固定testを変えず最小読取behaviorを修正した。
- Roslyn構文解析exit0、git diff --check exit0。新build/test/CIは実行せず、formalはremote_ci_onlyを維持。候補Greenは未観測。


## 対象ファイル
### 通信・JSON境界：6件runtime Red後の最小候補（2026-10-22）

- GameControllerStateClient.cs のみ製品変更。SHA256 4f49bceab83d1a7361d410a48843e35a461784adf6a42a5490d8e1532df9a87b。各読取のローカルMemoryStreamでraw UTF8を組立て、完成Outputごとに解析する。初期Outputを読み飛ばす際にbufferを空へ戻す。
- transport13case SHA256 bfaa0544e3b705c3f57ed29d4d3900948ab28d5d34ae0d734b8f0115248d4764と初回固定test SHA256 f8fa3dbd928e4a695b288a604cca7ba73e4bf97347e2db6dc47713ebc79133d7を保持。他26sourceと全既存tracked testも不変。親tracking/通常review report/保存済Red証拠は編集していない。


## 指摘事項
### 通信・JSON境界：6件runtime Red後の最小候補（2026-10-22）

- message.Lengthと次fragmentのCountの合計をコピー前に上限と照合し、EndOfMessageまでJSON化しない。完成した非matchState Outputの後は次messageとして読み直す。API文字列HALTの明示対応を保ち、UDP整数castや他状態を追加していない。
- catch対象はJsonExceptionとWebSocketExceptionだけで、呼出tokenが未取消ならnull観測へ戻す。caller取消済みならThrowIfCancellationRequestedで取消を優先し、OperationCanceledException/OOM/無関係な例外を広いcatchで通常成功へ変換しない。usingにより受領socketと各読取bufferは成功/失敗/取消で破棄され、Sendは呼ばない。
- 注入TimeProviderの単調経過を接続前の読取開始から採取し、Receive前後と成功直前に期限到達（elapsed >= Timeout）/時計逆行を拒否する。期限後の正常frameはnullにする。無入力時に自動timerで受信を終端するbehaviorは未実装として明示保持し、今の応答後チェックをその証明にしない。readonlyのURI/options/clockと読取ローカル資源だけを使い、新lockや共有read状態は追加しない。


## 結果
### 通信・JSON境界：6件runtime Red後の最小候補（2026-10-22）

- 最小候補を保存しsource/hash/syntax/diffと他26guardを確認。証拠は /tmp/issue81-game-controller-state-transport-green-candidate-20261022/。source-manifest.json、completeness-matrix.json、production.diff、before/after source、生command/stdout/stderrを保存した。
- 固定13case/初回test/旧653の候補Greenは未観測。親commit/push後のexact-SHA PR CIで実測を回収する。親prefix/Dispatchを保持し子5欄へ追記・読戻し。全process終端、未回収exitなし。子commit/push/CI操作なし。


## リスク
### 通信・JSON境界：6件runtime Red後の最小候補（2026-10-22）

- 自動timerによる無入力時の期限終端、STOP/RUNNING map、epoch/sequence/旧応答、API-UDP freshness照合、再設定/実hostname接続、AutoDrive/03Cは後続未実装。今回の有限窓は返った入力を期限で拒否する範囲までであり、完全な読取timeout実装や03B readiness完成としない。
- runtimeGreen/全666回帰は正式CI待ち。新依存/環境/auth/network/外部repo/実service/実機/新agentなし。既存全文文書gate不合格を保持、原Solhigh継続/applied:null、自己review判定なし。
