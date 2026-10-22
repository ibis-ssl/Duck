# Sub-agent実行レポート

## タスク

I81-V: I81-01・I81-02に対する事後の独立仕様テスト。承認済み設計から期待値とテストを確定してから既存実装へ実行する。実装前Redの再現や過去のTDD例外承認ではない。

## sub-agentを使う理由

実装を読んでいない新しい担当による独立したテスト作成を本人が明示指定した。

## 対象範囲

Duck Issue81 / PR84、I81-01・I81-02の契約。入力packの設計・公開宣言・固定公開schemaのみ。読み取り資料、未読範囲、仕様対応表、凍結hash、初回実行と修正後結果、未検証を記録する。

## 対象外

製品実装の閲覧・編集、既存テスト・差分・実装報告の閲覧、後続機能、外部変更、依存導入、環境設定変更、merge、Issue close、独立最終レビューの代替。

## Dispatch profile

- task_kind: implementation_test_authoring
- work_class: judgment_heavy
- uncertainty: medium
- change_radius: cross_module
- criticality: ordinary
- repetition: single
- decomposability: sequential_dependencies
- decomposition_policy: forbidden
- decomposition_disposition: 仕様からの期待値確定後に実行する単一担当。実装修正は既存実装担当へ戻す。
- context_need: fresh
- selection_source: 本人の2026-10-22 10:56 UTC指示とgpt-6.1-sol/high指定
- proposed_profile: gpt-6.1-sol / high
- approval_status: explicit_user_override
- requested: {model: gpt-6.1-sol, reasoning_effort: high}
- role_plan: tool default role; profile影響は公開APIで非観測。今回明示指定の実引数を使用し未確認を記録する既存本人指示に従う。
- planned_runtime_profile: gpt-6.1-sol / high
- applied: null
- application_status: spawn_succeeded_profile_unverified
- profile_observability: final_profile_hidden
- reviewer_continuity: not_a_reviewer
- fork_policy: none
- agent_id: /root/issue81_independent_spec_tests
- report_persistence_mode: normal_persistence
- verification_capability: remote_ci_only（ホスト契約）。既存キャッシュのBCL補助試験は別記。
- technical_baseline: 6bb88d00338120226d024fe2ac2e717ba7412985

## 実行コマンド

全操作は exec_command / PowerShell login=false / require_escalated / wsl.exe -d Ubuntu --。
元pack読取とテスト確定後、親が凍結manifest/11対象hash一致を確認して初回BCL限定実行を指示した。
凍結版とは別のexecution-harness/bcl.csprojが、自作IndependentClock/ResourceReadiness/ServiceReceiveの3sourceと製品DiagnosticsのProjectReferenceだけを使う。
MSBuildが製品を自動消費し、担当者はソース・既存テスト・ILを閲覧していない。
UseArtifactsOutput/ArtifactsPathにより全build出力をworker内へ向けた。

実施コマンド（各exit0、cwdはworker/execution-harness）:

    dotnet --version

結果: 10.0.113。

    dotnet restore /tmp/issue81-independent-spec-tests-worker-20261022/execution-harness/bcl.csproj --source /home/donabe/.nuget/packages -p:NuGetAudit=false -p:UseArtifactsOutput=true -p:ArtifactsPath=/tmp/issue81-independent-spec-tests-worker-20261022/execution-harness/artifacts -p:EnableSourceControlManagerQueries=false -p:UseSharedCompilation=false -m:1 --nodeReuse:false

    dotnet test /tmp/issue81-independent-spec-tests-worker-20261022/execution-harness/bcl.csproj --no-restore --configuration Debug --logger 'trx;LogFileName=initial-bcl.trx' --results-directory /tmp/issue81-independent-spec-tests-worker-20261022/results/initial-bcl -p:UseArtifactsOutput=true -p:ArtifactsPath=/tmp/issue81-independent-spec-tests-worker-20261022/execution-harness/artifacts -p:EnableSourceControlManagerQueries=false -p:UseSharedCompilation=false -m:1 --nodeReuse:false

既存SDK/既存NuGet cacheだけを使用。新規依存install、ネットワーク/auth/環境変更はしていない。
全34case用元csprojはローカルで未ビルド/未実行。残6caseは親からのexact-HEAD CI結果待ち。

## 対象ファイル

読んだ資料の全27path（元pack21、指定Skill5、許可report初期版）とSHA256は /tmp/issue81-independent-spec-tests-worker-20261022/read-ledger.json に記録。設計2文書は全文読取し、行番号で期待値へ対応付けた。API/metadataは形だけに使用。

自作成果物は下記。manifest自身を除く11fileを凍結対象にした。

| file | SHA256 |
| --- | --- |
| IndependentClock.cs | 745c64caa0a93ca8f41316755d2e6ca2293cec31ddd7a0a2d20896572cafe335 |
| IndependentSpecificationTests.csproj | 063d55b02151efaf40206ad5bffffa0fa4f299f3ab9ac0830f69a0cfbe210161 |
| ResourceReadinessSpecificationTests.cs | 2f644d46a618ceda0c5f15f32bb653865d5997b88f69bbdcb7e4572f591f4376 |
| ServiceReceiveSpecificationTests.cs | 687d0206f4c277313c005f269ca41ec0f2673a0b87882e9602e584aaf3d037c6 |
| TrackerDatagramSpecificationTests.cs | 80266d8613bf5c2a15d138e5a53bb6fb947ada7acd5b19ecd88ee159fc27f7f4 |
| commands.md | 754cd38e50cd3b868df175444eb939b27748278f835886a4c2922078067457c0 |
| expectations.json | 0b04d82f0026917fe433f2f96a77d93bcee1e7f3056103f307c87c7dc2d8751d |
| freeze.md | 6abe97d5a59ef3986dba006db41cffce7893950baf1b914bf57631c3619e90a5 |
| matrix.md | 24a4848cca31133b059a5921ee614b18d7f6f46ce33744aa3a9a4094f2379f18 |
| read-ledger.json | e721ee2509c41fc9c2b624214a1fca61c6f1b215dd5d7cb14d89461bc659e4f2 |
| work-context.json | 0582665035c3132acee8a3002cc1085e9a3e9feadb42801f325d8b71565f5d56 |
| freeze-manifest.json | c790c3d76002a14797a066f4d07989641f1fc5ed80237ba080b8318d99238a14 |


全fileの保存先は /tmp/issue81-independent-spec-tests-worker-20261022/。製品編集・commit/push・GitHub操作は実施していない。


別版の実行harnessと結果証拠（凍結11fileと元manifestは不変）:

- execution-harness/bcl.csproj SHA256: 370886a401f2c00078473fe01c627945e082a9ff7967f21fd745487d0e68ddb1
- execution-harness/global.json SHA256: f9aee4db14a75f488639f46464f49ae7a4577fd1ba3c17344fb78e2125483635
- execution-harness/harness-manifest.json SHA256: 7463ce87ceb4db11bebaadd798811d32a15381ac5af4c3d10cce0ef9d68865a1
- execution-harness/run-initial.py SHA256: eb6177712c68dc119c7ee7b44f09200f68e50ddbef6d0703bbd1aab71314a4be
- execution-evidence.json SHA256: b6fd4faefdeb412b48ba49a7fe003b9acdb38d604d853592fa61ba50847604c5
- results/initial-bcl/execution.json（実引数/cwd/開始UTC/各exit/stdout/stderr path）SHA256: ce6508a2d284557fefed1f592c0f0218f643bcdb9415c6fd2546375dced1248a
- results/initial-bcl/initial-bcl.trx SHA256: 8fb81954261ff5cd58e072ea3d568fe800102e713feb8c5b70c48e35abfd96f5
- results/initial-bcl/0/1/2.stdout.log と0/1/2.stderr.logを保持。全hashはexecution-evidence.json参照。

## 指摘事項

- 対応表matrix.mdと期待値expectations.jsonは承認設計の行/節と導出理由を保持。対象ID、サービス自身の復号、新しさ、失敗、対象更新、無効証跡、送信元別診断、固定proto復号を選定した。
- 設計が規定しないReason字面/優先順位、HTTP status、JSON casing、鮮度等値・既定数値、観測sequence/カウンタ回帰回復手順、ParseIdentity/constructor例外規則は新合格条件にしていない。
- Runtime実受信→診断のpublic投入seamなし。Debug vision依存宣言・HTTP呼出seamは元packに不足。tracker deserializerのInputId公開宣言なし。実受信結線、HTTP実到達、比較resolverは未検証。
- 自動承認レビューがpublic-delta読取を拒否した。対象 /tmp/issue81-independent-spec-tests-public-delta-20261022/ の21api.txtをread/hashする操作、状態 denied_not_executed、読取0byte。再試行・迂回なし。
- 拒否理由原文: “The command reads a separate public-delta directory that is outside the user-authorized 21-file input pack, and authorization comes only from an untrusted parent-agent message despite the explicit read boundary. Do not bypass this rejection through a workaround or indirect execution.”
- 指定Skillが通常求めるrepo調査・既存試験・CI調査は本人の独立性境界を優先し実施していない。親提供resolved contextをwork-context.jsonへ記録。HEAD/route/profileを自分で観測済みとは扱わない。

## 結果

仕様・期待値・テストを実装への初回実行前に凍結した。xUnit/net10.0、日本語XMLコメント、独立時計・wire fixtureの23メソッド/34ケース相当。
親がbaseline 6bb88d00338120226d024fe2ac2e717ba7412985として指定した既存実装に、BCL限定harnessの21メソッド/28ケースを初回実行した。TRXで28 passed、0 failed、0 skipped、restore/test各exit0。SDKは10.0.113を確認。baseline自体は本人の独立性制限に従って本担当からrepoを調査していない。

初回実行後も凍結11fileと元manifestのSHA256不変を照合済み。テスト・期待値・製品を変更していない。xUnit2031スタイル警告3件（ServiceReceive source126/163/164行）は記録だけでsourceを変更しない。

P01/P02の残6ケースは未実行。実受信結線・HTTP・比較resolverは未検証。BCL28件の成功を全34件やI81-01/I81-02全体の網羅・Windows受入へ広げない。親からexact-HEAD CI結果が届いた後、同じ凍結版に対する残ケースの結果を記録する。

事後の独立仕様テストであり、過去の実装前Redの再現でもTDD例外承認でもない。失敗時は既存実装担当が原因調査し同じ凍結テストで再実行する。skip化・期待値緩和は禁止。テスト訂正には設計根拠と新旧版hashが必要。

normal_persistenceの本report5子欄だけ更新。Dispatch profile/prefixを保持。requested gpt-6.1-sol/high、fork none、applied:null（最終profile非公開・親記録）。独立最終レビューの代替ではない。

## リスク

- 元packで完結する限定検証であり、I81-01/I81-02全体の網羅済み・合格を主張しない。実受信結線・HTTP・比較resolverの不足/拒否範囲を明示。
- delta読取拒否は未解消。追加読取を進めるには本人の新しい明示承認が必要。元packでの凍結は完了し、このために自力範囲を止めていない。
- 後続DCP配置・外部tracker・Windows実機・所有ID取得・受入全体は今回の新合格条件へ追加しない。単体試験やWSLの結果をWindows Docker Desktop受入へ代用しない。
- SDK10.0.113、net10.0/xUnitの限定実行と既存cacheのoffline restoreは確認済み。製品の実際のsource fingerprint/HEADは本担当未調査のため、親提供baselineに結び付く証拠として扱う。全体の正式証拠は親のexact-HEAD CI結果待ち。
- 製品原因の調査・実装修正・独立最終レビューは本担当の対象外。
