# Issue81 I81-04 実装報告

## Dispatch profile（親所有）

- selection_source: user_override
- task_kind: implementation
- requested: {model: gpt-6-luna, reasoning_effort: high, fork_turns: none}
- role_plan: {explicit_agent_type: null, effective_role: default, role_config_evidence: null, profile_effect: unknown}
- planned_runtime_profile: {model: null, reasoning_effort: null}
- applied: null
- application_status: spawn_succeeded_profile_unverified
- profile_observability: final_profile_hidden
- constraints: ユーザー明示の後継起動。既存Sol担当は終端。default roleの影響は公開APIで未観測という事前例外を引継ぐ。
- signals: {work_class: implementation, uncertainty: high, change_radius: cross_module, criticality: medium, repetition: single, decomposability: sequential, decomposition_policy: forbidden, context_need: fresh}
- observed_runtime_evidence: {spawn_succeeded: true, task_identity: /root/issue81_luna_implementation}
- scope: Issue81 / PR84 / I81-04。基準4e3099e75c4afab8ba1f11c67500ebe29f17fed3。
- active_unit: I81-04A。I81-04Bの固定外部成果物・Debug用供給物は未確定、全体未完了。
- target: ibis-ssl/Duck / Issue81 / PR84 / task/issue81-aspire-desktop-bridge / base task/aspire-docker-desktop-496。
- execution_environment: FA780、fa780\donabe、PowerShell7 login:falseから承認付きWSL Ubuntu donabe、/home/donabe/Duck-issue81-aspire-desktop-bridge。
- verification_capability: remote_ci_only。標準.NET試験はローカルの生成済みSslProto不足を補修せず、既存遠隔CIで検証する。
- governing_policy: 比較設計のTDD要求を適用。実行時Red前の本実装をしない。過去のPD-I81-02-TDD未充足を今回の証拠で遡及充足しない。
- publication_state: 先行版aaa903d公開・CI39865903670構成Red。候補fdff6ec8e5851a4a7d1d20d0dad4f69503b926a9公開・CI39867225445初回775成功/1失敗/発行skip、同HEADの2回目は776成功と3アプリ実発行・出力検査成功。初回原因未確定。通常レビュー対象の記録同期はcommit_pending。
- authorization_boundary: 同課題の配布定義・試験・必要設計同期。外部ソース・設定変更、依存導入、配布物取得、コンテナ作成起動、mergeなし。
- continuity: 旧実装担当は終端のまま保持。通常レビューは既存担当を継続しSol/high、独立最終レビューは未開始。
- evidence_storage: reports/evidence/issue81-linux-distribution-validation-20261022。テキスト211件収納、重複展開・ソース原本9件は接続先に保持。parent-storage-map.json SHA256 4a042739e9f0e989344a820d4beb77136100e0abb4aefc0f03ad0cf7cf56a95e。旧38件は03C履歴参照のみ。
- preflight: 公開対象の追跡状態、検査入力と対象数、指定済みghのlogin:false/require_escalated経路を実行前にまとめて確認し、記録更新と証拠収納を一まとまりにする。

- evidence_erratum: 通常レビューで、最終trackingのSudachi引数が後指定で上書きされphase断片のみを検査していたと判明。従来のtasks側成功は未裏付け、変更4行のうち未検査行もあった。既存証拠を保持し、4行を対象に補完済み。通常レビューでEV-I81-04A-01はclosed、残る必須修正0。製品と配布出力のCI成功とは分ける。
- normal_review: 対象5f280e33da9f0e93d7396373c9573b0f21424582と限定文書追補、pass_with_held、必須製品0・証拠所見closed。記録のcommit/pushはcommit_pending。
- supplemental_evidence: reports/evidence/issue81-linux-distribution-tracking-lint-20261022 と issue81-linux-distribution-review-target-ci-20261022。原本manifest3492d0a6と63d5565aを照合収納済み。
- publication_blocker: 一括記録のcommit/pushは自動承認のCreateProcess拒否でコマンド全体未実行。理由は初期禁止に対する後続承認が見えないとの判定。PR84本文と既存自担当comment6336270248の状態更新も別途実行前拒否、理由は未公開結果の外部共有承認が確認できないとの判定。両操作とも再試行・別経路なし。local/remote HEADは5f280e3、index空、本人が実際に許可していないとの断定はしない。

## 実行コマンド

- `bash -n scripts/tests/verify-linux-publish-outputs.sh` は exit 0。スクリプトは mode 100755、SHA-256 `adfe7cd88a943a93bf59afc515b71f178b05d4255761280548d07ecd1cc2704f`。
- `gh run view 39867225445 -R ibis-ssl/Duck --json ...` を実行前確認と完了確認に使用。初回の通常権限実行は api.github.com:443 の socket access error。親の承認済み経路 `login:false + require_escalated` で読み直し、attempt 1 が fdff6ec の終端failureであることを確認した。
- 承認された再実行は `gh run rerun 39867225445 --failed -R ibis-ssl/Duck` の一度だけ。exit 0。実行後のstatus readと `gh run view ... --log --job 120136505328` も exit 0、attempt 2 の終端結果を保存した。
- attempt 2 の標準試験は 776 passed / 0 failed / 0 skipped。後続 `scripts/tests/verify-linux-publish-outputs.sh` は RuntimeHost、DebugHost、RefereeDriver.Host を各 `LinuxContainer` profile で発行し、`Linux publish output contract passed for all three managed applications.` を出した。
- CIのsource archiveから得た対象4ファイルと、Python `subprocess.run(["git","show",ref+":"+path], check=True)` で baseline `4e3099e75c4afab8ba1f11c67500ebe29f17fed3` / candidate `fdff6ec8e5851a4a7d1d20d0dad4f69503b926a9` を比較。exit 0、test fixture / wrapper / launch options / test csproj は全てbyte-equal。過去のprocess substitution比較ログは空blobを含むため失敗証拠として保持し、比較根拠には使用しない。
- 現04A設計docのsource/config/runtime/dictionary同一性とbaseline・追加12行の比較結果、及びcurrent-source lint rawは `C:\Users\donabe\AppData\Local\Temp\issue81-04a-evidence-20261022\04a-lint-source-context.json`、`04a-lint-runtime-context.txt`、`04a-lint-results-summary.txt` と同rootの `text-current.*` / `spell-*.{command.txt,stdout.log,stderr.log,exit-code}` / `yomiyasu-04a-*.{command.txt,stdout.log,stderr.log,exit-code}` / `sudachi-04a-*-captured.*` に保存。実行は既存Windows Node v24.20.0 / npm 11.19.0 と既存cache junctionで、依存導入なし。全体文書gateは新たに実行していない。
- 以前の `/tmp/issue81-referee-driver-host-design-sync-20261022/final/` 等を指す38件の `prior-lint-reference.sha256` は03Cの歴史的参照のみで、04Aの結果根拠には使わない。Sudachiの最初のstdinなしcaptureはraw保持の上invalid扱いとし、stdinを渡した `*-captured.*` のみを有効結果に使う。
- 4e3099e75c4afab8ba1f11c67500ebe29f17fed3→5f280e33da9f0e93d7396373c9573b0f21424582の4行差分（tasks 31〜33 / phases 19）を保存し、当初5f版のtextlint 0・CSpell 1・Sudachi 1を個別記録。親がtasks 33を修正したworking treeでもbaselineとの差分を再抽出し、final fragmentは2ファイル/4行。最終textlint、CSpell、Sudachiは全てexit 0。Sudachiは単一の `--files` と2ファイルパスで実行。最終source hash、行/fragment hash、config/checker hash、Sudachi辞書hash、全raw command/stdout/stderr/exitは追補bundle `C:\Users\donabe\AppData\Local\Temp\issue81-tracking-lint-5f280e3-20261022\final` に保存。
- 全ログ・command・exit・source hashは `C:\Users\donabe\AppData\Local\Temp\issue81-04a-evidence-20261022` に保存。218ファイル/39,625,450 bytes。inventory manifest `evidence-files.sha256` SHA-256 `8a1e01df1f6715b1e7b9bca4d67662dad3c6b8026ea55278365b5409953b134e`。ZIP拡張子・PK magicは0件、gzip source archiveはTemp原本のみ、展開コピーとbinlog重複は除外。

## 対象ファイル

- `Tracker/Tracker.RuntimeHost/Properties/PublishProfiles/LinuxContainer.pubxml`
- `Tracker/Tracker.DebugHost/Properties/PublishProfiles/LinuxContainer.pubxml`
- `Testing/Duck.Testing.RefereeDriver.Host/Properties/PublishProfiles/LinuxContainer.pubxml`
- `scripts/tests/verify-linux-publish-outputs.sh`
- `Tracker/Design/Testing/aspire-simulation-test-environment.md`
- 親所有の `Tracker/Design/tasks-status.md` と `Tracker/Design/phases-status.md` は読み取り・changed-line lintのみ。今回このworkerからは編集していない。

## 指摘事項

- aaa903d / CI 39865903670 で最初に測定された構成RedはRuntimeHost Linux publish profileの欠落だけ。標準776件成功、publish check exit 1。
- fdff6ec / CI 39867225445 attempt 1 は標準試験775 passed / 1 failed / 0 skipped。`DockerContainerWrapperLifecycleTests.LogFollowerReapTimeoutReturnsFailureAndStillCleansContainer` が期待1・実際0で失敗し、publish stepは未到達。artifact `12463689590` を保持。
- attempt 2 は同じheadで標準776件成功後、3アプリの実publish出力契約も成功。初回の終了処理試験失敗は再現しなかった。原因は未確定であり、タイミング依存は推測に留める。
- packet-flowとfull-stack CI jobは両attemptでskip。04Bの外部成果物digest/Debug専用構成は未着手。
- 現04A設計docは基準版と現行版の差分を比較し、現行mirrorと `fdff6ec...` の同文書がbyte一致することを確認（36,178 bytes / SHA-256 `98424b1d...`）。基準版との差分は追加12行（1,632 bytes / `b0f4978b...`）。textlint currentはexit 0。CSpell baseline/currentは同じ `Tailscale` 1件だけ（line 284/296）でexit 1、追加12行はexit 0。Yomiyasu strict baseline/current/追加行は全てexit 0、score 100、clean、findings 0。Sudachiのstdinあり正規captureではbaseline/currentが各10件（TCP 9、Tailscale 1）でexit 1、追加行は0件/exit 0。既存source/configとSudachiPy 0.6.11 / SudachiDict-core 20260428 / system.dic SHAを `04a-lint-results-summary.txt` に記録。全文gateは既知のCSpell 232件/5文書、Sudachi 890件/5文書の不合格状態を維持。
- 以前のSudachi最終コマンドは `--files tasks --files phases` とnargs+引数を重ねていた。後段指定が先行指定を上書きするため、exit 0で確認できたのはphases断片のみ。tasksも成功したという過去の主張は未裏付けとして訂正し、正しい引数契約と元command/exitを追補bundleに保存。
- 親修正後の4行で最終再検査を実施。tasks-status.md 31〜33行、phases-status.md 19行をそれぞれ含む2ファイルを入力し、textlint exit 0、CSpell exit 0（2 files / 0 issues）、Sudachi exit 0（findings 0）。先行版5fの失敗rawと修正途中の `シンボル` / `コード` 指摘は履歴として残し、最終結果と区別。

## 結果

- 04Aの3つのLinux配布profileは実際の遠隔 `dotnet publish` と固定期待値の出力検査に結び付き、CI attempt 2で3アプリ全てpassした。
- 固定先行試験scriptは編集前後同一hash・実行modeで、期待値は緩和していない。標準 `dotnet test` も776件成功。
- exact CI source archive hashは `candidate-source-hashes.txt` と `source.tar.gz` に記録。展開したソース重複はhash記録後に削除、archive原本はTempに保持。
- 現04Aのsource/config/runtime/dictionaryとlint結果のcommand/stdout/stderr/exitをTemp evidence root内に保存し、baseline・追加12行との差分結果と照合した。prior-lint-reference.sha256の38件は旧03C歴史参照であり、04Aの検証根拠として扱わない。歴史的な最初の直接UNC失敗は保存rawから発見できておらず、その詳細を再構成せず欠落として記録。
- 追補bundle `C:\Users\donabe\AppData\Local\Temp\issue81-tracking-lint-5f280e3-20261022` に5f版失敗raw、親修正diff/source hash、最終4行入力、3検査のraw command/stdout/stderr/exitを保存。current inventoryは77ファイル/61,655 bytes、manifest SHA-256 `3492d0a6284c624ab5a47e0d85b92627d53abc0f975e15abc69e6d2c44cd3efa`（不一致/未登録0）。初回56-file manifest SHA-256 `2762e36dbba12106646edbe3044bcc105744e086a350265e4f0b811c8b21d0b8` とそのinventoryを `initial-56/` に原本hashのまま保存。04Aの既存218-file bundleも不変。

## リスク

- attempt 1 のlifecycle failureはattempt 2で再現しなかったが、原因解明には至っていない。成功した再実行を原因修正の証拠にしない。
- passは標準776件と3アプリのmanaged publish output contractに限る。packet-flow、full-stack、実機、通常review、独立最終reviewは別途未完了。container imageの取得・build・起動はしていない。
- 全文文書lintは既知の未解消baseline findingで失敗を保つ。図の最終視認も未完了。
