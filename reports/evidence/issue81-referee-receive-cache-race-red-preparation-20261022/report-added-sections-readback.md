## 実行コマンド
### I81-NR-002（P2）競合の先行テスト準備（2026-10-22）

- 実ファイルの work-context-manager / implementation-executor / implementation-worker / tdd-executor / report-output-manager / report-writer を再読して適用した。比較設計 tracker-comparison-debug-design.md:260 のTDDと親の指示に従い、HEAD95ce7801224f6d2a77fc2ab6487675fe44fcb433で先行testと最小観測seamのみを保存した。
- 既存SDKの Roslyn構文解析ツール Declarations.dll で候補2ファイルを解析してexit0。git diff --checkもexit0。標準build/testは実行しておらず、実parser/hostは引き続きremote_ci_only。構文成功をcompile成功やfunctional Redとは扱わない。


## 対象ファイル
### I81-NR-002（P2）競合の先行テスト準備（2026-10-22）

- Tracker/Tracker.Tests/RefereeReceiveCacheRaceTests.cs を新規追加。SHA256 4f13e3cfbc84b01c485fa409086e541851726b2298af515070ddcffad6a64a02。
- Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs は既定nullの内部 BoundaryObserver、3観測点、内部enumだけを追加。SHA256 0c045bf368621f7e3341cd4276cf401b4bb857e0666fd406fa6e7cbf6514ba5f。constructor/public API/default経路は保持。seam宣言と呼出だけを除去すると元sourceとbyte同一（SHA256 084d6e25a21a90a25c3cf71fbca31c64622b69df3b88a1fb61d19c7cf4c6aca0）。
- 既存testの差分0、source manifestの他22ファイルもhash不変。固定blind67・既存36caseは編集していない。通常review reportと親trackingは子所有外として保持。証拠は /tmp/issue81-referee-receive-cache-race-red-preparation-20261022/ に保存した。


## 指摘事項
### I81-NR-002（P2）競合の先行テスト準備（2026-10-22）

- I81-NR-002 medium(P2)は未解消。現在のcacheがSourceKey/ReceiverEpochだけで最新診断のageを照合し、期限切れAを同endpointのBのage0で返せるというfindingを確認した。診断GetSnapshots内の時計停止では診断lockも保持してBの受理を止めるため、それをfixtureに採用していない。
- 最小観測seamはcommandGate保持中の診断読取直前／正常診断受理後のcommand公開直前／購読の待機直前の3点。既定nullであり、時計・診断・cache・判定・公開の順序を修復するものではない。test設定時のみ既存の順序で可能な一時停止を固定する。日本語の説明/XMLコメントを追加した。
- 実service・既定の実Referee.Parser・実ServiceReceiveDiagnostics・手動単調時計を使用する。A(counter7/HALT)2packetをt0に受理してcount2/ready→t6へ進め期限切れ→購読がgate保持→B(counter8/STOP)を同endpointで受理してcount3/age0/ready、公開だけ停止→購読の診断読取を解放する。test:89の Assert.False(subscription.IsCompleted) は、B公開前にAを返さず待機へ進むことを直接確認する。B公開解放後は同じ購読がcounter8/STOP/yellowを返すことと単一pumpを確認する。


## 結果
### I81-NR-002（P2）競合の先行テスト準備（2026-10-22）

- test/scaffold候補の保存・構文確認・hash/読戻しを完了。新testのruntime実行は0件、functional Red未観測。将来のCIでcompileerrorや5秒watchdogのtimeoutをRedと認定しない。旧A返却による具体的なAssert.False失敗を親が実測確認してからbehavior修正へ進む。
- expectations-and-matrix.jsonにrequired action / production path / actual composition fixture / focused evidenceを記録。source-manifest.json、seam-only-identity-proof.json、unchanged22-source-checks.json、syntax.command.json/result、seam.diff、生stdout/stderrを保持する。先の651Greenはこの新競合を検証した証拠ではない。
- finallyは両barrierを解放し、runとsubscriberを取り消して両Taskを回収し、その後serviceをawait Disposeする。現在の作成・解析・保存processは全て終了コードを回収済み。commit/push/CI操作は行っていない。


## リスク
### I81-NR-002（P2）競合の先行テスト準備（2026-10-22）

- I81-NR-002はopenのまま。次は親がこの先行test/scaffoldを非main commit/pushし、exact-SHA PR CIの実runtime Redを取得する。最小behavior修正・同一test Greenはその後であり、現段階では未実施。
- 既存全文Markdown gate不合格、成功CI個別TRX不在、Desktop/Windows/DCP/実機、03B/Cの未検証は保持。外部ソース/配置/依存/環境変更は行っていない。元要求gpt-6.1-sol/high継続、applied:null、nested agentなし。自分でreview合格判定を行わない。
