## 実行コマンド
### I81-NR-002（P2）runtime Red後の最小修正候補（2026-10-22）

- 親が採用した exact cd7760fd / CI39820168338 のfunctional Redを受け、同じ実装/TDD/report Skillの適用を継続。先行固定testの89行 Assert.False(subscription.IsCompleted) が実行時に失敗し、652件中旧651件は個別Passed、timeout/compile failureではないというTRX/source照合を保持した。
- Roslyn構文解析（候補2file）exit0。/usr/lib/dotnet/dotnet build Testing/Duck.Testing.Diagnostics/Duck.Testing.Diagnostics.csproj --no-restore -m:1 /nr:false -p:EnableSourceControlManagerQueries=false はexit0、0警告/0エラー。SCM照会抑止はcommand-localのみ。git diff --check exit0。新runtime test実行は0件で、実parser/serviceの正式Greenは親のexact-SHA CI待ち。


## 対象ファイル
### I81-NR-002（P2）runtime Red後の最小修正候補（2026-10-22）

- ServiceReceiveDiagnostics.cs: 既存bool RecordSuccessのsignature/defaultを保持し、out acceptedCount overloadへ委譲。同じ診断lock内で受理したpacketの成功件数を返す。SHA256 2f38e7ef3c0a74a1e04860a728882fb9c303be5e5c5de6de51096e9d34cab7b2。
- RefereeReceiveService.cs: そのacceptedCountをcommand cacheへ保持し、購読時にsource/epochに加えて現在診断の成功件数一致を要求。SHA256 c571b48745f7748235527e1a588cba46099a673487bd154bb44a836740af74a4。既定nullの内部観測seamは変更していない。
- 固定race testのSHA256 4f13e3cfbc84b01c485fa409086e541851726b2298af515070ddcffad6a64a02を保持。全既存tracked test差分0、他22sourceもhash不変。既存通常review report/親tracking/保存済CI証拠は編集していない。新候補証拠: /tmp/issue81-referee-receive-cache-race-green-candidate-20261022/。


## 指摘事項
### I81-NR-002（P2）runtime Red後の最小修正候補（2026-10-22）

- 成功件数はinput/sourceごとに正常packet受理で必ず増え、同counterでも増える。source・epoch・成功件数の組でcacheと同じpacketの診断を照合する。Aのcache件数2とBの新診断件数3が異なる間はAを返さず、B公開後にcache3と診断3が対応して初めて既存のstatus/known age/期限条件を評価する。受理後に別途GetSnapshotsを呼んでtokenを推測せず、RecordSuccessの同じlock内で件数を採取する。
- lock順を変更していない。購読は従来どおりcommandGate→diagnostics gate。受信はdiagnostics gateを解放してからcommandGateへ進むため、逆向きのnested lockを追加しない。時計の追加採取もない。失敗/未知age/時計逆行は従来条件で拒否し、ClearCommand/epoch/取消/再開/Dispose/defaultは変更していない。新規関数説明/XMLコメントは日本語。
- I81-NR-002のrequired action / production path / actual composition fixture / focused evidenceを finding-completeness-matrix.json に記録した。候補は静的・BCL build確認までであり、finding closedや通常review合格とは判断していない。


## 結果
### I81-NR-002（P2）runtime Red後の最小修正候補（2026-10-22）

- 最小製品候補2fileを保存し、構文/Diagnostics単独build/diff-checkと固定hash guardを回収した。source-manifest.json、production.diff、finding-completeness-matrix.json、before/after source、生command/stdout/stderrを保存した。候補Greenは未観測で、先行testも期待値も変更していない。
- 親がcommit/pushして同じ固定race testと旧651件のexact-SHA PR CIを実行し、Greenと全回帰を確認してから同じ通常reviewerへ戻す。実装担当はcommit/push/CI操作を行っていない。親prefix/Dispatchを保護し子5欄に履歴追記・読戻しした。全process終端、未回収exitなし。


## リスク
### I81-NR-002（P2）runtime Red後の最小修正候補（2026-10-22）

- BCL単独build成功は実service/parser・競合fixtureのGreenではない。I81-NR-002は候補修正済みだが未検証、正式CI/同reviewer確認待ち。固定36/67および全標準回帰も次の同SHA CIで確認する。
- 既存full Markdown gate不合格、実Desktop/Windows/DCP/外部診断・配置未検証、03B/C未着手の制約は保持。外部/依存/環境変更なし、原要求Solhigh継続/applied:null、新agentなし。
