## 実行コマンド
### 通信・JSON境界unitの先行試験準備（2026-10-22）

- HEAD38bb4890f988d5d42155750452cc9d23d4f86881で現client・既存固定契約を確認し、TDD/implementation/report Skillの適用を継続。通信境界13caseとcompile用宣言だけを保存した。Roslyn構文解析exit0、git diff --check exit0。新runtime実行0件、compile/functional Redは未観測。親がexact-SHA PR CIで正式Redを回収する。


## 対象ファイル
### 通信・JSON境界unitの先行試験準備（2026-10-22）

- 新規 GameControllerStateTransportTests.cs：13case、BCL WebSocket fakeと手動TimeProvider/単発ITimer。SHA256 bfaa0544e3b705c3f57ed29d4d3900948ab28d5d34ae0d734b8f0115248d4764。
- GameControllerStateClient.cs：optional options/TimeProvider引数、未使用のdiscard、GameControllerStateReadOptions宣言だけ。SHA256 1cd10b71401c41ce6bdca37cb6dc20736c49f79d885734731ee98179ba0ee9a6。追加宣言/引数/discardを除くと旧source（SHA256 2fd3e0c02618754a9dfcaf17594b382005c89235da800dd37ddc84d5cf99921c）とbyte同一。読取body・成功判定・通信behaviorは未修正。
- 初回固定test SHA256 f8fa3dbd928e4a695b288a604cca7ba73e4bf97347e2db6dc47713ebc79133d7、他25source、全既存tracked testを保持。親tasks/phasesの進捗更新は対象外として保持。証拠: /tmp/issue81-game-controller-state-transport-red-preparation-20261022/。


## 指摘事項
### 通信・JSON境界unitの先行試験準備（2026-10-22）

- 期待Red6case：UTF8途中分割matchState、分割初期gcStateからの継続、不正JSON、受信切断、正常JSONの合計byte上限1超過、期限後解放の正常frame拒否。実不足をNotNull/Assert.Null(error)/Assert.Null(observed)で確認する。既存regression見込み7case：不正構造2、Close/Binary2、上限ちょうど1、fragment合計超過拒否1、呼出取消1。全部を人工的にRedにしない。合計超過fragmentが現在nullでも、現在の合計上限実装の証明ではなく未対応fragmentの早期拒否だと区別する。
- 読取窓は既定3秒、上限は既定4096byteの有限値を内部optionsとして宣言。現在buffer4096と同じ規模の論理message上限で、既定値の外部性能保証を主張しない。試験は期限2秒/手動時計と正常JSONの正確なbyte数を明示注入し、期限到達後の正常応答を成功にしない。optionsはまだbehaviorへ適用していない。
- 通常の不正JSON/切断/期限等はnull観測、呼出元の明示取消はOperationCanceledExceptionという独立期待値を固定。無関係な例外/OOMを成功へ変換する契約ではない。全socketをusingで保持し、待機caseはfinallyで合図解放/取消/Task回収する。5秒watchdogのtimeout自体をRedとは認定しない。無入力のまま期限で終端することは今回の遅延frame試験だけでは証明せず、Green後の追加確認へ残す。


## 結果
### 通信・JSON境界unitの先行試験準備（2026-10-22）

- first transport unitのtest/scaffold保存・構文/diff/hash確認を完了。expectations-and-matrix.jsonに13case/各期待値/既存挙動との差、source-manifest.jsonに指紋、scaffold-only-identity-proof.jsonにbehavior未修正のbyte証明を保存。見込み全666件は未実測で、runtime Red/Green未観測。
- 親prefix/Dispatchを保護し子5欄へ追記・読戻し。全process終端、未回収exitなし。製品behavior実装・新build/test・commit/push/CI操作は行っていない。親がこの候補をCIへ渡し、実assertion不足を採用してから最小実装する。


## リスク
### 通信・JSON境界unitの先行試験準備（2026-10-22）

- 期待6失敗/7regressionは静的見込みで、実測ではない。期限は明示注入した読取窓の契約であり、実GC/外部networkの所要時間を仮定していない。STOP/RUNNING/API-UDP明示map・snapshot age/epoch/sequence・遅い旧応答/再設定/hostname再解決・AutoDrive・03Cは未実装の後続として保持。
- 新依存・環境/auth/network・外部repo・実service/実機・新agentなし。既存全文文書gate不合格を保持。原Solhigh継続/applied:null、自己review合格判定なし。
