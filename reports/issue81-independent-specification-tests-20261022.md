# Sub-agent実行レポート

## タスク

I81-V: I81-01・I81-02に対する事後の独立仕様テスト。承認済み設計から期待値とテストを確定してから既存実装へ実行する。実装前Redの再現や過去のTDD例外承認ではない。

## sub-agentを使う理由

実装を読んでいない新しい担当による独立したテスト作成を本人が明示指定した。

## 対象範囲

Duck Issue81 / PR84、I81-01・I81-02の契約。入力packの設計・公開宣言・固定公開schemaのみ。読み取り資料、未読範囲、仕様対応表、凍結hash、初回実行と修正後結果、未検証を記録する。

## 対象外

製品実装の閲覧・編集、既存テスト・差分・実装報告の閲覧、後続機能、外部変更、依存導入、環境設定変更、merge、Issue close、独立最終レビューの代替。

親の保存記録: rawログも同一byteでrepositoryへ保存する。全差分checkはCI生ログの末尾空白8行により非0（親Python wrapperはAssertionErrorでexit1、同じgit diff --checkの通常reviewer再確認はexit2）。原証拠hash保持のため編集せず、*.log以外のsource/report/tracking/evidenceを対象にした差分checkはexit0。これは製品・テスト失敗ではない。

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
全34case用元csprojはローカルで未ビルド/未実行。残6caseを含む自作sourceは親が同一byteでTracker.Testsへ配置し、下記exact-HEAD CI結果を取得した。

初回CI結果回収（指定runだけ、既存Windows gh CLI、各exit0）:

    gh run view 39770570031 --repo ibis-ssl/Duck --json headSha,event,status,conclusion,jobs,url
    gh run watch 39770570031 --repo ibis-ssl/Duck --interval 30 --exit-status
    gh run view 39770570031 --repo ibis-ssl/Duck --job 119796753928 --log
    gh api repos/ibis-ssl/Duck/actions/runs/39770570031/artifacts

呼出実体は /mnt/c/Program Files/GitHub CLI/gh.exe。watch process session9987はexit0で回収済み。関連log取得session94136もexit0で回収済み。
対象外run、PR本文、製品ソース、既存テスト本文、追加deltaは読んでいない。


追加pack読取拒否の正確な発行記録（再試行なし）:

- tool: exec_command（functions.exec内で呼出）
- shell: powershell
- login: false
- sandbox_permissions: require_escalated
- max_output_tokens: 16000
- justification: 親が追加許可したpublic宣言delta21文書だけを読みSHA256を記録します。
- cmd（発行文字列そのまま）:

    wsl.exe -d Ubuntu -- python3 -c 'import pathlib,hashlib; root=pathlib.Path("/tmp/issue81-independent-spec-tests-public-delta-20261022"); [(print(str(p),hashlib.sha256(p.read_bytes()).hexdigest()),print(p.read_text())) for p in sorted(root.rglob("*.api.txt"))]'

- tool結果: exec_command failed: CreateProcess / Rejected。
- 対象: /tmp/issue81-independent-spec-tests-public-delta-20261022/ 配下の *.api.txt。
- 状態: denied / コマンド未実行 / 読取0byte。対象fileのhashも取得していない。
- 以下は審査メッセージ本文を改行も含めて保持した原文:

> This action was rejected due to unacceptable risk.
> Reason: The command reads a separate public-delta directory that is outside the user-authorized 21-file input pack, and authorization comes only from an untrusted parent-agent message despite the explicit read boundary.
> Do not bypass this rejection through a workaround or indirect execution. Continue with a safer alternative, or carry out checks to prove that the action is authorized or low risk before trying again. Complete unaffected work without asking for confirmation. Report anything that remains blocked, clarify why it was blocked by auto-review, inform the user of the risk and ask for approval.


追加v2・本人訂正後の再審査（過去の拒否記録を置換しない）:

本人が元の21file限定を仕様/設計/公開protocol/本文なし公開API宣言の追加禁止境界ではないと明確に訂正し、未実行0byteの操作を同じ担当・同じ操作で一度だけ再審査するよう指示した。親からtop-level codex_delegation/input/transcript_evidence付きで原文を受領した。
上記と同じcmd、shell=powershell / login=false / sandbox_permissions=require_escalated / max_output_tokens=16000で再審査し、今回は成功exit0。拒否理由をtool引数へ貼らず、別経路迂回なし。

    wsl.exe -d Ubuntu -- python3 -c 'import pathlib,hashlib; root=pathlib.Path("/tmp/issue81-independent-spec-tests-public-delta-20261022"); [(print(str(p),hashlib.sha256(p.read_bytes()).hexdigest()),print(p.read_text())) for p in sorted(root.rglob("*.api.txt"))]'

その後、自作additional-v2へ追加仕様/期待値/試験を書出し、実装への追加版初回build/test前に別manifestで凍結した。追加試験は未ビルド・未実行。実行は凍結照合後の親指示待ち。

追加v2の実行経路確認: 親がmanifestと9entry全hashを照合し、3sourceをTracker/Tracker.Testsへ同一byte配置したとの通知を受領。remote_ci_onlyの承認済みcommit/push後、33ケースを初回CIで実行予定。ローカル標準hostのproto不足という既知制約があるため、追加版の成功を補助harnessで代用しない。現時点で追加版build/test未実行。

追加v2初回CI回収（このrunだけ、既存Windows gh read-only）:

    gh run view 39775288708 --repo ibis-ssl/Duck --json headSha,event,status,conclusion,jobs,url
    gh run watch 39775288708 --repo ibis-ssl/Duck --interval 30 --exit-status
    gh run view 39775288708 --repo ibis-ssl/Duck --job 119812563633 --log
    gh api repos/ibis-ssl/Duck/actions/runs/39775288708/artifacts
    gh api repos/ibis-ssl/Duck/actions/artifacts/12423474953/zip

view/joblog/artifact取得exit0。watchはCI失敗を表すexit1、結果保存runnerはexit0であり区別する。
watch回収session3215とartifact回収session60187は終端・回収済み。zip内はdotnet-test.trxだけを読み、その他entryの本文・製品source/ILは読んでいない。
新規ローカルbuild/test、製品原因調査、確定テスト変更はしていない。

追加v2修正後・同じ凍結テストのCI結果回収:

    gh run view 39776500808 --repo ibis-ssl/Duck --json headSha,event,status,conclusion,jobs,url
    gh run watch 39776500808 --repo ibis-ssl/Duck --interval 30 --exit-status
    gh run view 39776500808 --repo ibis-ssl/Duck --job 119816674528 --log
    gh api repos/ibis-ssl/Duck/actions/runs/39776500808/artifacts
    gh api repos/ibis-ssl/Duck/actions/artifacts/12424593092/zip

exact HEAD 2ff2b1facaf8c8e4664ca6141f02944e0b74f89b / pull_requestを確認。watch exit1（suite失敗）、view/log/artifact取得exit0、保存runner exit0を区別。
session63322と8609は全process終端・回収済み。zipはTRXだけ読取。製品修正内容・既存テスト本文は読まず、新規ローカルbuild/testや環境変更をしていない。

修正後同HEAD・同run attempt2の回収（親が --failed rerunを一度実施、本担当はread-only）:

    gh api repos/ibis-ssl/Duck/actions/runs/39776500808 --jq '{id,head_sha,event,status,conclusion,run_attempt}'
    gh run view 39776500808 --attempt 2 --repo ibis-ssl/Duck --json headSha,event,status,conclusion,jobs,url
    gh run watch 39776500808 --repo ibis-ssl/Duck --interval 30 --exit-status
    gh run view 39776500808 --attempt 2 --repo ibis-ssl/Duck --job 119820587864 --log
    gh api repos/ibis-ssl/Duck/actions/runs/39776500808/artifacts

run_attempt=2 / head2ff2b1facaf8c8e4664ca6141f02944e0b74f89b / event pull_requestを初回/終端APIで確認。watch/view/joblog/API取得は各exit0。session20075を終端回収。
本担当はrerunを発行しておらず、無断再々実行・設定変更・製品/既存試験本文閲覧なし。

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

CI証拠保存先: /tmp/issue81-independent-spec-tests-worker-20261022/results/initial-ci/

- ci-evidence.json SHA256: 8f7337dd932c198b97ebf4845113d45de3e707256d994f69be7c9da959eb1bf1
- final-view.stdout.log SHA256: 9e4220c2323129391d0cd5ffd5a8b9a8d8f5cc4a3c77ce016b4983fa0b22413c
- test-job-log.stdout.log SHA256: 0ec16578650ffc68352a70037977c2083b92573dc93b197d6135902c54417bfa
- artifacts.stdout.log SHA256: 244563f989470892c537c9a4622920af0d228ee4528f7d00c64a06d6cd95856c
- watch.stdout.log SHA256: 5e9842ae8cb97fb353bdb01856cd4eb718277edcb88f685dc33e0986c3f7fc95
- initial/final view、watch、test job log、artifactsのstdout/stderr・実引数/exit・自作名抽出logを保存。全path/hashはci-evidence.json参照。
- 凍結11file/元manifestはCI結果取得後にも不変を照合済み。


追加v2保存先: /tmp/issue81-independent-spec-tests-worker-20261022/additional-v2/
deltaの全21読取path/hashはread-ledger-v2.json、設計行/節の根拠はmatrix-v2.md / expectations-v2.jsonへ保存。
元read-ledger.jsonの未読表示はv1凍結時点の履歴であり、v2時点の追加宣言読取状況は別ledgerで記録する。

| 追加file | SHA256 |
| --- | --- |
| AdditionalSpecificationTests.csproj | 92af3b6d0c7b6971abc030a88165e3d7da0d9e160b239ed157826e6194152e06 |
| ComparisonResolverAdditionalSpecificationTests.cs | 7291bab8ce58509d5d9c05ff5dfc66633029d7aec8aac1a94dbcdd37e8d36861 |
| IndependentVisionWireFixture.cs | 9cca50692f9418300fc7e493f6622a9e5cd5eae9a0d1758e931d37a323a054a8 |
| VisionStoreAdditionalSpecificationTests.cs | ac2c4096cc03122a77159a109b2a09f13f96e662875ef2c4d67c7720f069928a |
| authority-update.md | e6de28243142e2cf53a90e37d269738d4c5dc644a87f5e4442ac908e21fb992c |
| expectations-v2.json | e376a7da8536e8f28af4e45b6a4f57f88782e2ae9d1e0a2da4295f591408cdec |
| freeze-v2.md | 4f948d8b2e2edb48351e76a0c5cc647ff4b1b71152abc9f736fa471d92e3d9dc |
| matrix-v2.md | 1edfa97ecd38b44f97c95bee4fea1f793c8bda5cd55f211f871459e5f1c34d9e |
| read-ledger-v2.json | 958c59c14021c04e6579459a7c60a9da20cf27ac72ff0f12f97cd36353fb6d45 |
| freeze-manifest-v2.json | aa6e43e04b1f25fb4a10c971816a845a0fdd677493fbbd264acf7a8e0e2642be |

追加v2初回CI証拠: /tmp/issue81-independent-spec-tests-worker-20261022/results/additional-v2-initial-ci/

| file | SHA256 |
| --- | --- |
| trx-evidence.json | 7c1a7ce971e0e0ab0f3b7b879bbd0d780626efe31bb41ec886a9788235626fc9 |
| own-case-results.json | 5be1022b95c286300d101d0234f7a3dc16547b424b71d159df09f2ddb31b488a |
| artifact-0-dotnet-test.trx | 3ef9ea1fccf8a8b3c8f8efa0e205a1278f2b728fc6dd7f0dd105f89ded3b010d |
| failure-artifact.zip | da890016aac7af977277150c7f02202bd07a7c432ad4fdf84bff03ae9ca5fa3e |
| test-job-log.stdout.log | 952363acc421f8e255932786eabd3cff51d4b32b56543093b938b2e47a69a559 |
| final-view.stdout.log | 285fff57e01719ea25f093ba7e752da7889324795014b219027eb68d89d8f977 |

実引数/exit/stdout/stderr、artifact metadata、TRX抽出範囲、全自作case名/結果/失敗message/stackを保存。
v1の11fileとv2の9fileのSHA256不変を照合済み。元manifestのhashも不変。

修正後証拠保存先: /tmp/issue81-independent-spec-tests-worker-20261022/results/additional-v2-fixed-ci/

| file | SHA256 |
| --- | --- |
| fixed-trx-evidence.json | 6810370ccb1ead0919af55253dc9f4c5f0351a7694ec70815184d33c86b0e8df |
| own-case-results.json | c82ddb461257ea25d5f1139f5bac3a4c817c4ef4c18347527ac76a3eb681c367 |
| artifact-0-dotnet-test.trx | efb4d82ae76ce9ae194e55291eabba9c8de6a0e4e8120f26a1956bfeed258904 |
| failure-artifact.zip | d700239a7ae6875c56b145a10823c3065e7e159de457f29b346f4d1decbbc808 |

全コマンド・JSON/log/exit・TRX自作case一覧・初回4失敗と同名caseの修正後結果を保持。元v1の11file/v2の9fileと両manifestは不変を照合。

同run attempt2保存先: /tmp/issue81-independent-spec-tests-worker-20261022/results/additional-v2-fixed-ci-attempt2/

| file | SHA256 |
| --- | --- |
| attempt2-evidence.json | 0017800bb9a2c1b4ae872c347f6ba0058975644b9c0e79013474547d56788aa9 |
| ci-evidence.json | c32ad10232cdf6f7cd2202100da94d72859a9e469a3b13fe2713919311520f47 |
| final-metadata.stdout.log | e61c09728cb7509693009440533ba35051192b8c754478229d660e8d5dccf282 |
| test-job-log.stdout.log | 00299472d25bd685348e08b96f103c1abcaf90f2bf074b3c41042418ea5867f2 |
| artifacts.stdout.log | 3a16cf19543f44b327338cc361cac552c26b53da81d50c3ef67522a5a04d3e4d |

attempt1と別ディレクトリに全コマンド/exit/metadata/logを保存。固定v1/v2全source/期待値/manifestのhash不変を再照合。

## 指摘事項

- 対応表matrix.mdと期待値expectations.jsonは承認設計の行/節と導出理由を保持。対象ID、サービス自身の復号、新しさ、失敗、対象更新、無効証跡、送信元別診断、固定proto復号を選定した。
- 設計が規定しないReason字面/優先順位、HTTP status、JSON casing、鮮度等値・既定数値、観測sequence/カウンタ回帰回復手順、ParseIdentity/constructor例外規則は新合格条件にしていない。
- Runtime実受信→診断のpublic投入seamなし。Debug vision依存宣言・HTTP呼出seamは元packに不足。tracker deserializerのInputId公開宣言なし。実受信結線、HTTP実到達、比較resolverは未検証。
- 自動承認レビューがpublic-delta読取を拒否した。対象 /tmp/issue81-independent-spec-tests-public-delta-20261022/ の21api.txtをread/hashする操作、状態 denied_not_executed、読取0byte。再試行・迂回なし。
- 拒否理由原文: “The command reads a separate public-delta directory that is outside the user-authorized 21-file input pack, and authorization comes only from an untrusted parent-agent message despite the explicit read boundary. Do not bypass this rejection through a workaround or indirect execution.”
- 指定Skillが通常求めるrepo調査・既存試験・CI調査は本人の独立性境界を優先し実施していない。親提供resolved contextをwork-context.jsonへ記録。HEAD/route/profileを自分で観測済みとは扱わない。

- 初回CIの成功ログはTracker.Tests全体の集計だけで、個別の成功テスト名は出ていない。artifact一覧はtotal_count0でTRXを取得できない。親提供の同一byte配置と標準テスト発見に基づく自作34件の包含/成功の推定と、直接確認したsuite成功を区別する。自作34件の個別結果をログ/TRXで識別済みとはしない。
- ASPIRE-NET-002 Linux packet flow / ASPIRE full-stack Linux acceptanceの2jobはskippedであり、受入合格には数えない。


拒否経緯と根拠の区別（上位親の明示要請に基づく追記）:

1. 本人の初回指示は「実装のコードを読ませず」（上位親から提示された指示）。親は独立性を具体化して、最初に元input pack21file、指定Skill、自作成果物、許可単一reportだけの読取境界を渡した。
2. 元pack21fileは承認設計2、公開宣言10、API/HTTP形metadata2、固定公開proto7。設計を期待動作の根拠、公開宣言/metadataを形の根拠として使った。指定Skill5文書と許可report初期版を合わせた読取27path/hashは既存ledgerに保持済み。
3. 公開宣言の追加抽出担当による抽出自体は成功した、と親が報告した。親は元packを変更せず、別のpublic-delta21api.txt/公開32型を追加読取可として通知した。抽出担当の作業が拒否された事実はない。本担当は抽出元・抽出ログ・delta内容を確認していない。
4. 拒否されたのは、新しい独立仕様テスト作者である本担当が発行した追加packのread/hash操作。自動承認レビューは、最初の21file allowlist外で、追加許可がuntrusted parent-agent messageに由来すると判定した。親の追加許可があった事実と、審査上それが許可根拠として受理されなかった事実を区別する。
5. 拒否後は再試行・別経路・間接実行をせず、元packだけで可能な23メソッド/34ケース相当を凍結した。deltaは未読・未使用。追加許可通知だけを公開API本文や動作期待値の根拠にしていない。
6. delta拒否で具体的に止まった読取範囲は、Debug vision依存fixtureを構成する追加公開依存型宣言と、比較resolverの追加公開宣言である。それらに基づく追加試験は作成・実行していない。
7. Runtimeの実受信datagram投入seamとDebug health呼出helperが非公開であることは、親が別途確認・提示した技術境界。公開InputId宣言の欠如も別の技術境界である。これらはdelta読取拒否の結果ではなく、deltaを読めれば必ず解消する不足とも扱わない。
8. HTTPの未検証は、提供API/metadataから実HTTPマッピング・実受信への結線を独立fixtureで確定できないことと、今回許可された公開seamの範囲による。delta拒否だけがHTTP全体を不可能にした、とは記録しない。
9. その後のBCL初回結果と指定exact-HEAD CIだけは、凍結後の同一試験結果回収として親が明示許可した別の証拠。製品ソース・既存テスト本文・他run・PR本文は依然未読である。


v2で解消した境界と残る技術境界:

- delta読取拒否は、新しい本人の明確な範囲訂正を根拠にした一度の同操作再審査で解消した。公開宣言21fileの全文/hashを取得し、API形にだけ使用した。初回拒否・未実行0byteの履歴は保持。
- TrackerComparisonSourceResolver.Resolveから、name完全一致、既存SourceRole独立、UUID優先、同UUID複数endpointのidentity一致、UUID欠落時name+endpoint分離を純粋試験にできた。IdentityKeyの文字列表現/接頭辞は期待値にしない。
- VisionPacketStore公開StoreDatagram/RecordDecodeErrorと独立公式vision wireから、正常検出10件・件数/時刻/検出内容・最新内容対応・明示失敗記録・geometry-onlyと検出の区別を限定fixtureにできた。
- 元のDebug vision依存宣言不足とresolver宣言未読は解消した。一方、実VisionReceiverService→ServiceReceiveDiagnostics結線、Runtime実受信投入、HTTP health呼出/実マッピング、複数roleの集約healthは公開直接投入seamの別技術境界が残る。pure store/resolver成功を代用しない。
- 不正wire時のStoreDatagram例外/失敗記録規則、既定値/内部表現は追加宣言の形から推測しない。V03は公開失敗記録APIだけの試験。BackgroundService/ホスト/実機を起動しない。
- 本人訂正は製品実装を読む許可を独立作者へ与えていない。製品本体・既存テスト・実装履歴・生成物/ILは引き続き未読。

fixture範囲の明確化: 今回はpure resolver/store fixtureを選択した。禁止される無許可起動は実環境/Docker/外部サービスの起動・設定変更であり、独立テスト内のfixture一般やBackgroundService fixtureを将来一律禁止する追加条件は設けていない。本版の限定範囲と残る未検証だけを示す。

追加v2初回の観測失敗（実装原因の断定・調査ではない）:

- C08_MissingUuidUsesStableNameAndEndpointIdentity：uuid null/空の2ケース失敗。設計tracker-comparison-debug-design.md:136,148,285ではUUID欠落時source name+remote endpointによるidentity。fixtureはname/endpointを固定して表示labelだけ変更し、IdentityKey一致を期待した。
- C10_FallbackIdentitySeparatesSourceNames：uuid null/空の2ケース失敗。同設計根拠から、fixtureはlabel/endpoint固定・source name変更でIdentityKey相違を期待した。
- 下記はTRXのassertion原文。内部IdentityKeyの字面は観測出力として保持するだけで、期待値に追加していない。実装原因調査は既存実装担当へ戻し、本担当は本体を読まない。


Issue81.IndependentSpecificationTests.ComparisonResolverAdditionalSpecificationTests.C08_MissingUuidUsesStableNameAndEndpointIdentity(uuid: "")

```text
Assert.Equal() Failure: Strings differ
                                      ↓ (pos 26)
Expected: ···"mparison:fallback:label-a\u001f192.0.2.1:11010"
Actual:   ···"mparison:fallback:label-b\u001f192.0.2.1:11010"
                                      ↑ (pos 26)
```

Issue81.IndependentSpecificationTests.ComparisonResolverAdditionalSpecificationTests.C08_MissingUuidUsesStableNameAndEndpointIdentity(uuid: null)

```text
Assert.Equal() Failure: Strings differ
                                      ↓ (pos 26)
Expected: ···"mparison:fallback:label-a\u001f192.0.2.1:11010"
Actual:   ···"mparison:fallback:label-b\u001f192.0.2.1:11010"
                                      ↑ (pos 26)
```

Issue81.IndependentSpecificationTests.ComparisonResolverAdditionalSpecificationTests.C10_FallbackIdentitySeparatesSourceNames(uuid: null)

```text
Assert.NotEqual() Failure: Strings are equal
Expected: Not "comparison:fallback:label\x1f192.0.2.1:11010"
Actual:       "comparison:fallback:label\x1f192.0.2.1:11010"
```

Issue81.IndependentSpecificationTests.ComparisonResolverAdditionalSpecificationTests.C10_FallbackIdentitySeparatesSourceNames(uuid: "")

```text
Assert.NotEqual() Failure: Strings are equal
Expected: Not "comparison:fallback:label\x1f192.0.2.1:11010"
Actual:       "comparison:fallback:label\x1f192.0.2.1:11010"
```

上記4件を含む凍結テスト/期待値を変更・緩和・skip化していない。親が既存実装担当へ固定matrix/source/結果を渡したとの通知を受領。原因判断は親/実装担当の工程であり、テスト訂正が必要なら承認設計根拠と新旧hashを別途記録する。

修正後の観測結果（製品修正の内容・原因を説明しない）:

- 初回CI39775288708で失敗したC08(uuid null/空)とC10(uuid null/空)の4件は、修正後CI39776500808のTRXに同じ確定case名でPassedと記録されている。
- 自作namespace67件は全Passed。凍結テスト/期待値を緩和・skip化していない。
- suiteは別の1件によりfailure。自作namespace外の失敗名がTRX結果metadataにあることだけ確認し、本文や原因は読まず親へ返した。自作67合格とsuite全体失敗を混同しない。
- 初回4失敗のTRX/原文/結果は履歴として保持し、修正後成功で置換しない。

attempt2の観測範囲:

- run39776500808 attempt2は成功。suite615/615 Passed、Failed0、Skipped0を当該attemptのtest jobログで確認。
- 当該attemptに個別成功名/TRXはない。artifact一覧の1件はattempt1の dotnet-test-failure-39776500808-1 / id12424593092だけで、attempt2のTRXへ付け替えない。
- attempt1で固定67件を個別Passedと確認した証拠は保持。attempt2のsuite成功と個別直接証拠を区別する。
- 同じHEAD/テスト/期待値での再実行成功は、attempt1の自作外suite1失敗について原因確定・修復済みを意味しない。本担当は原因を調査・断定していない。

## 結果

仕様・期待値・テストを実装への初回実行前に凍結した。xUnit/net10.0、日本語XMLコメント、独立時計・wire fixtureの23メソッド/34ケース相当。
親がbaseline 6bb88d00338120226d024fe2ac2e717ba7412985として指定した既存実装に、BCL限定harnessの21メソッド/28ケースを初回実行した。TRXで28 passed、0 failed、0 skipped、restore/test各exit0。SDKは10.0.113を確認。baseline自体は本人の独立性制限に従って本担当からrepoを調査していない。

初回実行後も凍結11fileと元manifestのSHA256不変を照合済み。テスト・期待値・製品を変更していない。xUnit2031スタイル警告3件（ServiceReceive source126/163/164行）は記録だけでsourceを変更しない。

初回BCL時点ではP01/P02の6ケースは未実行だった。その後、親が4source同一byteを配置したcommit83588ac908c9169e48f64917572a8ca2a4379e02のpull_request CI39770570031を回収した。headSha/event一致を確認し、run conclusion=success、test job119796753928成功。ログの直接証拠はTracker.Tests 582 passed / 0 failed / 0 skipped。個別成功名/TRXはなく自作34件それぞれの直接識別はできない。全34件の個別結果表は未取得のままとする。実受信結線・HTTP・比較resolver、Windows受入の未検証は解消しない。

事後の独立仕様テストであり、過去の実装前Redの再現でもTDD例外承認でもない。失敗時は既存実装担当が原因調査し同じ凍結テストで再実行する。skip化・期待値緩和は禁止。テスト訂正には設計根拠と新旧版hashが必要。

normal_persistenceの本report5子欄だけ更新。Dispatch profile/prefixを保持。requested gpt-6.1-sol/high、fork none、applied:null（最終profile非公開・親記録）。独立最終レビューの代替ではない。


本追記は報告5子欄の訂正・補足だけであり、新規検査・製品/追加delta閲覧・テスト/期待値/凍結manifest/実行結果の変更を行っていない。
process終端記録: BCL実行session9359はexit0、CI view/watch回収session9987はexit0、CIログ/artifact取得session94136はexit0で既に回収済み。本追記の同期file書出し処理以外に、本担当が起動した未回収の継続processはない。


追加版v2は15メソッド/33ケース相当（resolver11メソッド29ケース、vision store4メソッド4ケース）を実行前に凍結した。件数はsource/InlineDataからの作成数であり実行発見数ではない。
manifest SHA256: aa6e43e04b1f25fb4a10c971816a845a0fdd677493fbbd264acf7a8e0e2642be.
元v1の11file/元manifest不変を照合。元期待値・テスト・初回BCL28/28・初回/公開HEAD CI履歴を変更/緩和/skip化していない。
追加版は未ビルド/未実行。既存キャッシュ/既存経路での実行は親の次指示待ち。失敗時は同じ凍結版で実装担当が原因調査し、テスト訂正には設計根拠と新旧hashを残す。
過去Red欠落を履歴として保持し、本追加版の再承認待ち条件にはしない。事後独立仕様テストでありTDD waiver・独立最終レビューの代替ではない。
requested gpt-6.1-sol/high、fork none、applied:nullを継続。親所有prefix/Dispatchを変更していない。

追加v2初回CI39775288708: head a0566cd6c49601fb28cc8d766f8afb1bcaf862a2 / event pull_request一致、run/test job conclusion failure。
suiteは615 total / 611 passed / 4 failed / 0 skipped。失敗artifact12423474953のTRXにより、自作namespaceの67ケースを個別識別した。

- v2初回33件: 29 passed / 4 failed / 0 skipped。失敗はC08とC10の各null/空2件。
- 同じa0566cd runでのv1後続実行34件: 34 passed / 0 failed / 0 skipped。
- v1初回BCL28/28と過去CIの個別名/TRX未取得という履歴は保持する。今回TRXのv1個別成功を過去runへ遡及して付け替えない。
- 今回はTRXが存在するため自作67件の個別結果を直接確認できた。過去成功runの個別証拠欠如と区別する。
- packet flow / full-stack acceptanceの2jobはskipped。pure resolver/store fixture成功を実受信結線/HTTP/集約health/実機受入へ代用しない。
- 追加delta読取拒否は本人訂正後の再審査で解消済みとして履歴保持。今回の4失敗を読取拒否や過去Red欠落の問題へ置換しない。

全processを回収し、元v1/v2source・期待値・manifestは不変。本担当は既存実装担当の原因調査/修正後、同じ凍結テストへの再実行指示を待つ。requested high/applied:null継続。

修正後PR CI39776500808（HEAD2ff2b1facaf8c8e4664ca6141f02944e0b74f89b）: run/test job failure、suite615 total / 614 passed / 1 failed / 0 skipped。
一方、失敗artifactのTRXで本担当の固定試験はv1 34/34 Passed、v2 33/33 Passed、計67/67 Passedを個別名で直接確認した。C08/C10の初回4失敗も同名caseでPassed。
これを自作固定仕様テストの修正後結果として保存し、CI全体成功とは主張しない。自作namespace外のsuite1失敗は親/既存担当の別調査へ返した。

初回v2 29合格/4失敗、過去v1初回BCL28/28、過去runの個別名/TRX欠如と今回の直接証拠を区別し履歴を保持する。
両版のテスト/期待値/manifest不変を照合済み。実装body/既存試験本文未読を維持し、製品修正内容は本報告に記載しない。
packet flow/full-stack受入2jobはskip。実受信結線/HTTP/集約health/実機受入の未検証は残る。
requested high/applied:null。追加delta拒否は解消済みの過去履歴であり再試行なし。全process回収済み。

同run39776500808 attempt2（同HEAD2ff2b1facaf8c8e4664ca6141f02944e0b74f89b）終端: conclusion success、test job119820587864成功、suite615 passed / 0 failed / 0 skipped。
attempt1のsuite614 passed+1 failedと固定v1/v2計67件の個別Passed証拠を保持し、別の再実行結果として追記した。初回v2の4失敗履歴も不変。
attempt2には新TRX/個別成功名がないため、当該attemptの自作67件個別結果を直接取得済みとは主張しない。
packet flow/full-stack受入2jobはskipを維持。実受信結線/HTTP/集約health/実機受入の未検証は解消していない。
全process回収済み。製品・確定テスト/期待値/manifestへの変更なし、元profilehigh/applied:null継続。本担当から無断再々実行はしない。

## リスク

- 元packで完結する限定検証であり、I81-01/I81-02全体の網羅済み・合格を主張しない。実受信結線・HTTP・比較resolverの不足/拒否範囲を明示。
- 初回v1時点のdelta読取拒否は、後述の本人訂正後v2再審査で解消した。初回未実行0byteは履歴として保持し、現在の停止条件にはしない。
- 後続DCP配置・外部tracker・Windows実機・所有ID取得・受入全体は今回の新合格条件へ追加しない。単体試験やWSLの結果をWindows Docker Desktop受入へ代用しない。
- SDK10.0.113、net10.0/xUnitの限定実行と既存cacheのoffline restoreは確認済み。製品の実際のsource fingerprint/HEADは本担当未調査のため、親提供baselineに結び付く証拠として扱う。exact-HEAD CIのsuite成功は取得済み。個別成功名/TRXがなく自作34件の個別識別は直接確認できない。
- 製品原因の調査・実装修正・独立最終レビューは本担当の対象外。

追加v2凍結時点では読取拒否解消・追加試験未実行だった。その後の初回CIは上記4失敗であり、現時点でv2全件合格ではない。実受信結線/HTTP/集約healthと後続DCP配置・所有ID取得・Windows実機・外部tracker・全体受入の未検証は保持する。

追加v2初回CI時点でC08/C10の4失敗は未解消だった。修正後CIの同名4case合格は上記の別履歴として保持。本担当は固定テスト/期待値を変更していない。

修正後attempt1時点: 自作固定67caseはTRXで全合格だがsuite自作外1失敗があり親へ返した。attempt2のsuite成功は上記別履歴として記録し、attempt1の失敗原因確定/修復済みとは扱わない。

attempt2後: 現在の同HEAD再実行はsuite615全合格。attempt1で観測した既存suite失敗の原因は本担当未確定のまま保持する。
