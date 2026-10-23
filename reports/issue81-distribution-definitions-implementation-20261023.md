# Sub-agent実行レポート

## タスク

Issue81 / PR84 / I81-04B2。8つの最上位固定参照と通常用3管理プログラムの配布定義を、実際の発行物へ接続して検査する。

## sub-agentを使う理由

ユーザーの親は管理を担当する方針と、既存実装担当の継続。先行試験、製品定義、設計同期、検証を同じ限定単位で実行する。

## 対象範囲

基準b37555e27ba82f9b78b1e6a4ccb754531419786c。RuntimeHostはruntime基底、DebugHostとRefereeDriver.Hostは既存ASP.NET依存に合わせaspnet基底。調査済みindex digestを固定し、既存publish出力とDockerfile入力・実行体を照合。外部参照の供給表と既存AppHostの宣言を対応させるが接続は変更しない。

## 対象外

image/layer取得・build・起動、実コンテナ通信、実機。デバッガー専用定義と供給は04B3、AppHost配線は05。外部ソース、依存導入、認証/環境/network設定変更、mergeなし。

## Dispatch profile

- selection_source: user_override / continuity_reuse
- task_kind: implementation
- signals: work_class=bounded_technical; uncertainty=medium; change_radius=cross_module; criticality=high; repetition=single; decomposability=sequential_dependencies; decomposition_policy=forbidden; context_need=bounded_history
- automatic_floor: Sol/high（配布・互換契約）。現行ユーザーの実装Luna/high固定を優先する。
- original_requested: {model:gpt-6-luna,reasoning_effort:high,fork_turns:none}
- executor: /root/issue81_luna_implementation
- role_plan: existing default role; profile_effect unknown
- planned_runtime_profile: unknown
- applied: null
- profile_observability: final_profile_hidden
- application_status: reused_existing_agent_profile
- report_persistence_mode: normal_persistence
- verification_capability: Pythonの定義検査はlocal_execution_available、標準.NET/実publishはremote_ci_only。コンテナbuild/runを成功へ含めない。
- policy: 比較設計のTDD要求。まず固定先行試験と有効なRedを実行し、その後に配布定義を実装する。新規依存なし。子は実装/設計の実Skillを読み、入れ子起動なし。

## 実行コマンド

- python3 -m py_compile scripts/tests/verify-linux-distribution-definitions.py scripts/tests/test-linux-distribution-definitions.py — exit 0。
- python3 scripts/tests/test-linux-distribution-definitions.py — 16 tests pass, exit 0。
- bash -n scripts/tests/verify-linux-publish-outputs.sh — exit 0。
- 既存CI接続: 3つの実publish後・portable PDB検査後に単体試験を実行し、続けて verify-linux-distribution-definitions.py --repo-root "$repo_root" --publish-root "$publish_root" を呼ぶ。Docker buildは行わない。
- 追跡行のfocused lintは tasks-status.md のI81-04B/I81-04B2/I81-04B3の3行だけを1ファイル入力として実施。textlint/CSpell/Sudachiはいずれもexit 0、CSpellは1 file/0 issue。原コマンドと出力はevidence bundle内に保存。

## 対象ファイル

- 新規 scripts/tests/verify-linux-distribution-definitions.py。固定digest/mediaType/platform descriptor、現AppHost default image refs、3つの発行出力を一括照合する検査器。
- 新規 scripts/tests/test-linux-distribution-definitions.py。16件の標準ライブラリ単体試験。
- scripts/tests/verify-linux-publish-outputs.sh の末尾へ単体試験と実publish検査を追加。04Aの既存発行/assertionおよび04B1のPDB試験は差分なし。
- 製品Dockerfile、image lock、AppHost binding、設計文書は未変更。

## 指摘事項

- 固定基底: RuntimeHost → mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3; DebugHost/RefereeDriver.Host → mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca。RefereeDriver.Hostの既存Microsoft.AspNetCore.App framework referenceと04A検査契約に合わせた。
- 8参照ロック期待値はv4公開metadata証拠のroot index digest、media type、CPU descriptorを含む。unknown/unknown attestationは別欄に置きplatform扱いしない。GHCR3件は既知の公開source commitも保持する。検査はLinux/amd64を新しい要件にしない。
- Dockerfile期待値はdigest固定FROM、WORKDIR /app、COPY . /app、対象DLLへのJSON-array ENTRYPOINT。CIは同一runで生成した各projectのpublish directoryを引数にし、その実出力のDLL/deps/runtimeconfig、設定・静的assetとruntimeconfigの共有framework (10.0.x) を照合する。
- 既存AppHostの6 scenario image refs/default tagを検査し、lockのregistry/repo/tagと対応づける。AppHost自身のimage選択や実行時挙動は変えない。

## 結果

- 固定テスト期待値をソースhashに記録した後の最終実行: Python構文検査0、16 unit tests pass、Bash構文検査0。製品の定義がまだ存在しないため、ローカル実publish Redは実行せず、機能Redとも報告しない。親によるtest-stage公開後のexact PR CIで、実publishを終えた同じworkflowが不足するlock/Dockerfileを検出することを確認する段階。
- 固定SHA-256: verifier 5137277985d3c42b4a3dcb35de5b5a7ddc3ef9d28596072522f2ef0c837cc642、test script 337fb357e67c831b9c6b0b0acde5d0d6490fa292b52864b5330d6ee56b5bfc32; publish shellのhashはevidence/final-v2/source-hashes.json参照。
- 検査内容にはvalid fixture、欠落Dockerfile、floating base/tag、誤ったRefereeDriver.Host base、COPY context不一致、ENTRYPOINT不一致、実publish asset欠落、runtimeconfig framework不一致、digest drift、unknown descriptor分離、実AppHost既定値がある。
- 生コマンド、stdout/stderr/exit、config/input hashは reports/evidence/issue81-04b2-test-stage-20261023/ に保存。v2 inventoryは47 entries / 13,867 bytes、SHA-256 29076597656d5aa816e4e871385b703c78bfdc88ba75e251e11a9df39ecbdc55。8つのlock期待値とv4保存metadataの再照合もexit 0。実publish Redと.NET結果は未回収。

## リスク

- 16 unit testsはfixture上の検査器確認であり、実CIのpublish output検査成功証拠ではない。定義本体も未作成なので、有効な仕様Redはまだ得ていない。
- Python validatorが許可するDockerfile subsetはFROM/WORKDIR/COPY/ENTRYPOINT。最終Dockerfileはこの小さな文法に合わせる。COPY contextは既存publish rootの対象project directoryであり、実buildは受入条件に含めない。
- manifest index digestの確認は公開metadataの根拠であり、image/layer供給、build、startの証拠ではない。GHCRの現platformはamd64 childのみで、CPU契約は未指定のまま。
- 実発行CIが構文/依存問題で定義検査へ到達しない場合は機能Redと数えず、原因を区別する。
