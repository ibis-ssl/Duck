# Issue81 I81-04B1 デバッグ情報の実装報告

## Dispatch profile（親所有）

- selection_source: continuity_reuse
- task_kind: implementation
- signals: work_class=implementation; uncertainty=medium; change_radius=cross_module; criticality=medium; context_need=bounded_history; decomposition_policy=forbidden。試験と実発行への接続は同じ入出力を共有するため既存1担当で逐次実行。
- automatic_floor: Terra/high。ユーザーの実装モデル固定指示を優先してLuna/highを継続、無断の変更なし。
- original_requested: {model: gpt-6-luna, reasoning_effort: high, fork_turns: none}
- executor: /root/issue81_luna_implementation
- applied: null
- application_status: reused_existing_agent_profile
- profile_observability: final_profile_hidden
- role_plan: original default role reused; effect unknown
- scope: Issue81/PR84 / task/issue81-aspire-desktop-bridge / I81-04B1。基準28f227dbcd0e7b1643203231942ce91718dfe72c。既存DebugHostの実発行と移植可能なデバッグ情報・実行体・ソースコードの対応を検査する。
- verification_capability: remote_ci_only。TDD要求を保持、先行する実行時失敗の前に発行本体を変更しない。
- boundary: 許可済みDuck内の本単位。外部資材取得、依存追加、イメージ作成起動、AppHost資源配線、mergeなし。

- parent_evidence_note: Redのみ31ファイル時点のmanifestは最終統合版へ更新され、旧manifest現物は保存されていない。旧90fe14ffの現物再照合は不可。CI生ファイルは最終55項目のmanifest（33c42136）で親が全件再照合し、reports/evidence/issue81-04b1-red-and-fix-validation-20261023へ保存した。過去manifestの保存不足と実CI Redの生証拠を区別する。

## 実行コマンド

- 固定試験はCI対象HEAD 1e8f624424c80f8015179c7b8437918f250d657aでhash一致を確認：verify-debughost-portable-pdb.py 58e0beb075e195fcb9985a109fafe08580d1f83dbbf993945a5cc33d02465a24、test-debughost-portable-pdb.py a0dc746080a192c162fa5db7d894bb2a3d86906e1133eff9e652af032c7641c8、verify-linux-publish-outputs.sh 7c430e8daaa1ce5aa45e7490cbc7a3e6a25ab04a3144c08de48e120904635500。
- exact PR CI run 39878834053 / attempt 1 / event pull_request / HEAD 1e8f624424c80f8015179c7b8437918f250d657a を通常監視のみで回収。gh run watch はCI failureを示すexit 1、run metadata/log/artifact downloadは各exit 0。workflow_dispatch・rerunなし。
- Run dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj はPassed 776、Failed 0、Skipped 0、Total 776。LinuxContainer profileを明示した3 publishはすべてexit 0。RuntimeHost/DebugHost/RefereeDriver.Hostの3基本出力検査もすべてexit 0。
- publish script内のPDB単体試験は7件すべてpass。実際に発行されたDebugHost PE/PDBを使う検証はexit 1。CIログは実PDBに固定source document /src/Tracker/Tracker.DebugHost/Program.cs が無く、例として /home/runner/work/Duck/Duck/Tracker/Tracker.DebugHost/Components/App.razor、Components/Layout/MainLayout.razor、Components/Layout/NavMenu.razor が記録されていると報告。コンパイル失敗ではなく、実発行物をparserで読んだsource path契約のRed。
- Artifact linux-publish-failure-39878834053-1（id 12469792160、4853 bytes、API digest c00ea75878073ed8879515a514544c9a895155e90b0f4190455b94d2069a879b）を取得。環境、3 publish/checkのstdout/stderr/exitを含む。全CI・focused lint evidence bundleは C:/Users/donabe/AppData/Local/Temp/issue81-04b1-ci-1e8f624-20261023、56 files / 105174 bytes（inventoryが列挙する55 filesとinventory.sha256）、inventory.sha256 SHA-256 33c42136aa5a342ac2d5fa466959384e009e91cbd5179739739b0dfc9be335f6。
- Environment metadataはrun 39878834053 attempt1、refs/pull/84/merge SHA bfee7e4022c96251399dc329f3cdc427e8ee7879。PR84はDraft/Openで、PR mergeCommitはnull。workflow上のmerge ref SHAをPRのmerge commitと呼ばない。
- .NET publishをlocal実行していない。変更後はprofile XML parse、git diff --check、固定試験3hash確認を実行しexit 0。最終的なPathMap効果は後続exact CIで実発行PDBを検査する。
- 基準1e8f624比の親所有tracking変更はtasks-status.mdとphases-status.mdの2ファイル・各1本文行のみを抽出し、Windows Node mirrorでselector 2path、textlint exit 0、CSpell 2 files/0 issue/exit 0、既存WSL venv PythonによるSudachi exit 0/指摘0を確認。19設定・script・辞書fileはsource/mirror byte一致。raw・command・exit・input SHAを同CI evidence bundleのfocused-tracking-lint-summary.txt、lint-config-inputs.json、対応するstdout/stderr/exitファイルへ保存。Windows Node wrapperが既定Pythonのyaml不足でexit 2となった失敗も別保存し、依存追加なしで既存venvを使った成功結果と区別。

- 先行test-stage時点の仕様→試験対応：実際のDebugHost publish DLL/PDBを検証対象とし、CodeView GUID/ageとportable PDB ID、固定document /src/Tracker/Tracker.DebugHost/Program.cs、全Document /src/ を検査。先行試験はmissing/corrupt/mismatch/path境界を扱う。単体試験は標準ライブラリのみ。Document.Nameの先頭空segment index 0は絶対pathの仕様上の空要素として受理し、portable PDB仕様とfixtureで固定。[Portable PDB metadata specification](https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md)
- 先行公開28f227dからreview-target 5f280e3への親tracking変更はtasks-status.mdの31-33行とphases-status.mdの19行の計5行。先行focused lintはtextlint exit 0、CSpell 2 file/0 issue/exit 0、Sudachiは「パス」2件を検出した後に親の文言修正でexit 0になった。これは現在の1e8f624比2行lintとは別の歴史的検査であり、初回のrawと修正後final bundleは /tmp/issue81-04b1-prepublish-20261023 に保持。旧inventory SHA-256 fefcf25ad6b00ba835ca23690638ff56716eb69dab863e71bdafede8489400c9。
- 旧test-stage bundleは76 files / 313394 bytes。現在のCI bundleとは別に保全され、CI Red前の単体・構文raw、tracking lint、config/source hashを含む。過去5行のSudachi成功を今回2行の成功根拠へ流用していない。
## 対象ファイル

- Tracker/Tracker.DebugHost/Properties/PublishProfiles/LinuxContainer.pubxml — 実Redに対し、このDebugHostの明示的LinuxContainer publish profileだけへrepo-rootから/srcへのPathMapを追加。publish設定のみの変更。
- scripts/tests/verify-debughost-portable-pdb.py、scripts/tests/test-debughost-portable-pdb.py、scripts/tests/verify-linux-publish-outputs.sh — CIで固定されたPDB parser、7件の単体試験、3 publishと実発行物検証。今回は編集せず、上記SHA-256を保持。
- Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj、標準build/run、他2 publish profileは変更なし。AppHost配線・CPU/RIDも対象外。

## 指摘事項

- 主設計のDebugHost手順はsourceFileMapの /src をWindows側の同じ版のソースコードへ対応させる。実Redでは実PDB documentにrunner絶対pathが残り、固定した /src/Tracker/Tracker.DebugHost/Program.cs が欠落していた。期待値・parser・単体試験は緩めていない。
- LinuxContainer.pubxml内のPathMapはMSBuildProjectDirectory（Tracker/Tracker.DebugHost）から /../.. をSystem.IO.Path.GetFullPathで正規化してrepo rootを求め、MSBuild EnsureTrailingSlashでprefix末尾を区切ってから /src に割り当てる。repo root以下の相対部分は /src/Tracker/... となる意図。XMLで値を固定し、global csproj propertyにしていない。
- 既存CI scriptはdotnet publishに明示的に -p:PublishProfile=LinuxContainer を渡す。このためPathMapは当該発行のcompileに使うprofile内設定として加え、通常dotnet test/runや他プロジェクトへ適用する設定は追加しない。PathMapはコンパイラが物理source pathをPDBへ出すsource pathに置換するMSBuild compiler property。公式資料は [C# PathMap](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/compiler-options/advanced)、[MSBuild common properties](https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-properties?view=visualstudio)、[MSBuild property functions](https://learn.microsoft.com/en-us/visualstudio/msbuild/property-functions?view=visualstudio)。Microsoft資料はPathMapをlocal debug buildsへ無条件に適用しないよう注意しているため、明示配布profile内に限定。
- GetFullPathと末尾separatorを含むXML値は構文上確認したが、MSBuild上の評価結果、Razor生成sourceを含むportable PDBへの反映、実PDBが全て/src/になることは未確認。profileのXML構文成功を実mapping成功とは扱わず、後続remote CIで固定testと実PDB verifierの両方を通す必要がある。

## 結果

先行test/scaffoldのCI Redを受け、許可されたDebugHostのLinuxContainer publish profile一箇所だけへPathMapを加えた。実Redのsource pathは検出済みだが、profile変更後のCI未実行のため修正Greenは未確認。固定試験3ファイルは不変。XML parse、git diff --check、固定test hashesを再確認しexit 0。workflow rerun/push/commitは担当外のため行っていない。

## リスク

実発行物で明確なsource path契約違反を確認した。PathMapはCI/配布プロファイルに限定しているが、Razor生成documentを含めすべてのDebugHost PDB pathが期待どおり変換されるかは未証明。次のexact PR CIで3つの既存publish/basic assertions、標準776テスト、7単体試験、実DLL/PDB GUID一致、固定documentと全Document /src/条件を確認してGreenを判定する。失敗時は実際のPathMap評価とPDB document一覧を見て原因を分ける。
