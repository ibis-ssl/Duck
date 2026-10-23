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

- parent_recovery_note: CI証拠収納時のmkdir失敗はnested bash引数の引用不備で変数が消え、実対象が/artifactになったOS拒否だった。repo配下の拒否ではなく、uid1000/owner1000/mode755と書込可否の読取で訂正。誤対象を再試行せず、既存here-string→WSL Python経路へ戻し、権限/owner設定変更なしで承認済みrepo内作業を続行した。Tempの原errorと後続訂正を保持。

## 実行コマンド

- `python3 -m py_compile scripts/tests/verify-linux-distribution-definitions.py scripts/tests/test-linux-distribution-definitions.py` — exit 0。
- `python3 scripts/tests/test-linux-distribution-definitions.py` — 16 tests passed, exit 0。期待値は固定し、missing/floating/mismatched base, copy context, entrypoint, asset, framework, digest, descriptor分類を確認。
- `bash -n scripts/tests/verify-linux-publish-outputs.sh` — exit 0。既存publish/PDB assertionの後に16 unit testsと実publish出力検査を接続。
- 合成publish directoryを使った統合確認 — exit 0。repo内lock、既存AppHost参照、3 Dockerfileと各対象発行出力のDLL/runtimeconfig/static assets/shared frameworkの関係を検査した。これは.NETの実publishではない。
- Exact PR CI `39891317074` (attempt 1, HEAD `56294aa48090ed90774ee919e6893bd033de2bc4`, merge `d77f8f18204254e0d40ca954f954612440b8303f`) — 標準テスト776 passed/0 failed/0 skipped。3つの実publish成功、04B1 PDB 10/10、112 document paths under `/src` とDLL/PDB identity、追加unit 16/16の後、実publish outputに結合した配布定義検査がexit 1。lockと3 Dockerfileの未作成を検出した有効な実行時Red。再実行なし。
- CIの `ASPIRE-NET-002 Linux packet flow` と `ASPIRE full-stack Linux acceptance` はskip。診断artifactの保存/uploadは成功。
- 追跡文書のfocused比較は、基準 `56294aa` と現在の3ファイル（主設計、比較設計、`tasks-status.md` の親所有04B2行）を既存Windows Node/cache mirrorと既存Sudachi venvで検査。`textlint` は基準/currentとも exit 0。CSpellとSudachiは両方の側で既存指摘によりexit 1だが、差分行に指摘はない。Yomiyasu strictも同じ3ファイルを基準/currentで比較した（詳細は指摘事項と証拠bundle）。

## 対象ファイル

- `Tracker/Tracker.RuntimeHost/Dockerfile` — SHA-256 `70846d6a485e77470931e3bce193378b6b577140e9ba0b5233a168b557bf7661`。
- `Tracker/Tracker.DebugHost/Dockerfile` — SHA-256 `4c185c0a0f636c7c444de64bac0b884d2f8c795a75ff674d00611d89f4743517`。
- `Testing/Duck.Testing.RefereeDriver.Host/Dockerfile` — SHA-256 `9cc4b6eab1a891431fa71110445345bc0210c3b333328d821baab5793cf916e3`。
- `Testing/Duck.Testing.AppHost/linux-image-lock.json` — SHA-256 `91492b44d44cd95a67b50d5de2498f464acf87519213d61df000d6b9c41c51d8`。
- 先行固定試験 `scripts/tests/verify-linux-distribution-definitions.py` (`5137277985d3c42b4a3dcb35de5b5a7ddc3ef9d28596072522f2ef0c837cc642`)、`scripts/tests/test-linux-distribution-definitions.py` (`337fb357e67c831b9c6b0b0acde5d0d6490fa292b52864b5330d6ee56b5bfc32`)、`scripts/tests/verify-linux-publish-outputs.sh` (`2701743512d538a437d77de87f14bf9e083c40a316c51389cfaf5d0e65d83dd7`) は実装後も不変。
- 設計同期: `Tracker/Design/Testing/aspire-simulation-test-environment.md` SHA-256 `15205d3b20221336368ea5b2f3375f88e6cc08b22e62371e1f4edb9e80d413d9`、`Tracker/Design/Testing/tracker-comparison-debug-design.md` SHA-256 `c1bfa61d4a5267bf4ac429791dbf19ad5c71e247052a461105eafdd69036cc85`。

## 指摘事項

- RuntimeHostの基底は固定した `mcr.microsoft.com/dotnet/runtime` index digest、DebugHostとRefereeDriver.Hostは `mcr.microsoft.com/dotnet/aspnet` index digest。RefereeDriver.Hostは既存の `Microsoft.AspNetCore.App` framework referenceと04A検査契約に合わせた。CPU/RIDを追加していない。
- 8件の固定index参照は既存AppHostの6参照と2つの.NET基底を結び、media type/platform descriptorsと判別不能なdescriptorを別記する。3件のGHCR source commitは公開手順で確認した値だが、暗号学的provenanceやimage供給/build/startを証明しない。
- 各Dockerfileのbuild contextは同じworkflow runで作成された各projectの実publish出力。`COPY . /app`、DLL entrypoint、runtimeconfig/static assetsと共有frameworkの実出力をvalidatorが検査する。AppHost bindingはI81-05、DebugHost専用debug資材はI81-04B3。
- 文書比較はbaseline `56294aa`→currentで、主設計に12行追加（current 46–57）、比較設計に1行追加（current 254）、`tasks-status.md` の親所有I81-04B2行1行を更新（current 35）。baseline/current SHA-256はそれぞれ主設計 `98424b1de762e707f0ea20e9815d29dafc45f11e58f51594229bfae132aa3284`→`15205d3b20221336368ea5b2f3375f88e6cc08b22e62371e1f4edb9e80d413d9`、比較設計 `16074dca9c837b8c33cc0ec2ba6a0c4ac45de5efa3a781c4ab7c5c3077d7ca29`→`c1bfa61d4a5267bf4ac429791dbf19ad5c71e247052a461105eafdd69036cc85`、tasks `b48818fb1eef9a402b8f4df5d2824add02ffc13267019a11015ca6178071aa6e`→`bfdcd6ca56e1a2324a313986a692795c69736407f36e6397da04394e81af0cf6`。
- 3ファイル全体に対するCSpell/Sudachi exit 1を合格扱いしていない。基準/current双方の生出力は保存済みで、改行差分3箇所（主設計12行、比較設計1行、status 1行）に該当指摘はない。Yomiyasu strictのbaseline/current findings (rule,line)は全件一致: 主設計は両方score 100/exit 0、比較設計は両方score 91/exit 1で `excess_list` line 1 と `negative_parallelism` lines 9,166、tasksは両方score 0/exit 1で40 findings。status変更行35や追加設計行に新findingなし。比較設計の指摘は追加行254より前にある既存指摘。
- 文書検査config/source hashesは `design-lint-final/input-and-config-hashes.json`。変更行とbaseline/current hashの明細は同bundleの `changed-lines.json`。Yomiyasuの完全JSON・textlint/CSpell/Sudachiのstdout/stderr/exit・commandsは同bundle内。

## 結果

- 先行版の有効Red後に固定値どおりの3 Dockerfileとlockを追加。単体16件、Python構文、Bash構文、合成publish連携は成功。固定試験hashは維持した。
- 有効Redのexact CIは実publishに達し、publishと既存テストが通過した後の出力検査で不足3 Dockerfile+lockを検出した。これはコード構成不足の証拠であり、publish build失敗ではない。
- 証拠一式: `reports/evidence/issue81-04b2-implementation-20261023/`。CI run bundleは `ci/run-39891317074/`、新しい3ファイルfocused lint比較は `design-lint-final/`。`inventory-v2.tsv` と `SHA256SUMS-v2.txt` が今回追補世代。
- 04B2製品定義を追加した後のexact CIはまだ未実行。親の公開後CIで実publish連携Greenを確認する必要がある。

## リスク

- 検証は定義と実publish出力の対応まで。Docker image/layers取得、build、起動、通信、実環境での提供を実証していない。
- 8件のindex digest確認とGHCR公開source commitは供給参照の根拠であり、source-to-imageの暗号学的provenanceではない。GHCR REST package API 403は未解決のまま保持し、registry metadata読取結果と混同しない。
- 実行環境のCPU/RID条件は既存設計で未指定のため、そのまま維持。GHCR metadataでamd64 childが見えても新しい利用者制限として採用しない。
