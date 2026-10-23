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

## 実行コマンド

- python3 scripts/tests/test-debughost-portable-pdb.py — 標準ライブラリ単体試験7件、exit 0。欠落PDB、破損PE/PDB、PDB/DLL識別子不一致、期待source欠落、/src/外sourceとDocument.Name先頭空要素を検査。stdout/stderr/command/exitを証拠bundleに保存。
- bash -n scripts/tests/verify-linux-publish-outputs.sh — exit 0。Python ast.parseで2ファイル、git diff --checkもexit 0。raw保存済み。
- 追跡差分28f227d比は2ファイル・追加本文5行。抽出した5行だけをテスト入力としてNode 24.20.0のtextlint、CSpell、Sudachiを実行。textlint exit 0、CSpell 2ファイル/0 issue/exit 0、Sudachi exit 0。
- 初回の5行Sudachiは「パス」2件でexit 1。親が意味を保って「場所」へ修正し、同じ5行・同じ辞書・同じmirrorで最終3検査を再実行、すべてexit 0。初回rawは最終結果で上書きしていない。
- 20個のlint設定・スクリプト・辞書ファイルのmirror/source SHAを照合し全てbyte一致。Windows NodeはC:/Program Files/nodejs/node.exe、既存cache junctionはissue80-mdlint-20261022-a1f4c7/node_modules、textlint 15.7.0、CSpell 9.8.0、yaml 2.9.0。依存取得・設定・辞書変更なし。
- 実行証拠：/tmp/issue81-04b1-prepublish-20261023。最終inventoryは76 files、313394 bytes、SHA-256 fefcf25ad6b00ba835ca23690638ff56716eb69dab863e71bdafede8489400c9。単体試験/構文のraw、最初のSudachi指摘、wrapper組み立て失敗、全ファイル探索時の出力は別ファイルで保持。2文書全体を通した探索検査はCSpell 119件とSudachi指摘が出たが、baseline比較はしておらず5追加行の結果には帰属させない。rawは全体検査用として分けて保存し、5追加行のみの最終検査を採用する。
- ローカルの.NET publish/testは未実行。CI検査経路は既存workflowから呼ばれる bash scripts/tests/verify-linux-publish-outputs.sh。親によるtest-stage公開後のPR CIで実発行物を検査し、実行時Redを回収する。

## 対象ファイル

- scripts/tests/verify-debughost-portable-pdb.py — 発行済みTracker.DebugHost.dllのCodeView GUID/ageとTracker.DebugHost.pdbのportable PDB IDを照合し、固定Documentと全Documentの/src/配置を検査。
- scripts/tests/test-debughost-portable-pdb.py — 欠落・破損・不一致・source path境界の診断試験。標準ライブラリのみ。
- scripts/tests/verify-linux-publish-outputs.sh — 既存3プログラムのpublish出力検査末尾に単体試験と実発行物検査を追加。既存publish/assertion本文は変更していない。

## 指摘事項

- 主設計226/229行の「同じ版sourceの実行体と対応portable PDB」「sourceFileMapの/src対応」を、DebugHostの実発行物でDLL/PDB識別子一致とsource document pathとして測定可能な契約にした。固定値は /src/Tracker/Tracker.DebugHost/Program.cs。コンテナへsource本文を複製する契約は追加していない。CPU/RIDも指定していない。
- Portable PDB仕様はDocument.NameをseparatorとBlob heap part列として定義し、part index 0は空文字列。絶対pathの先頭空要素を拒否すると/src/...を誤拒否するため、index 0を空partとして復号し単体試験で固定した。[Portable PDB metadata specification](https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md)
- 初回追跡文言のSudachi新規指摘は「パス」2行で、親が「場所」に修正。最終の追加5行のみの検査では新規指摘0件。変更行のsource lineはtasks-status.md:33-36、phases-status.md:19。mirror内容もこの5行とbyte一致。
- 不一致・path負例はparser出力をmockし、契約診断分岐を検査する。破損PDB負例は実parserへ不正metadata bytesを渡して検査する。実publishとの対応・実portable PDB読み取りは未測定で、CI実Red/Greenの対象。

## 結果

先行test/scaffoldのみ。shell差分は既存3つのpublish呼出とassertionの後ろに2つのPython検査を追加しただけで、既存assertionは保持。ローカル単体7件は成功したが、実publish契約のCI検査はまだ実行していない。

固定SHA-256（CI Red前）：

- verify-debughost-portable-pdb.py: 58e0beb075e195fcb9985a109fafe08580d1f83dbbf993945a5cc33d02465a24
- test-debughost-portable-pdb.py: a0dc746080a192c162fa5db7d894bb2a3d86906e1133eff9e652af032c7641c8
- verify-linux-publish-outputs.sh: 7c430e8daaa1ce5aa45e7490cbc7a3e6a25ab04a3144c08de48e120904635500

28f227d基準の追跡入力hash：tasks-status.md 9aacc82347f5735cfe1136532dc5e6e45faf81dbede524f692506c967f0277d5、phases-status.md 50506e042aa62c33a9a3d896c8032a1648c71fc8433fb4e3bb88bc045930d833。固定試験3hashは追跡文言修正後も不変。

想定する機能Redは、CIが既存3つの通常publish/assertionを通過した後、実DebugHost出力がportable PDBではない、DLLとPDBのIDが異なる、固定source documentがない、またはPDB内documentに/src/外pathがある場合の検査失敗。コンパイルや環境準備の失敗は機能Redに数えない。CI実測前なのでRed結果は未確定。

## リスク

実publishのportable PDB parser動作と固定pathはCI未検証。先頭空要素の復号は公式仕様と単体fixtureで確認済み。実際のデバッグ接続と事前デバッガー供給、コンテナイメージの作成起動は対象外。
