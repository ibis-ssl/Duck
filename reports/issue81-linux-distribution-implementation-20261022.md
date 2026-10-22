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
- publication_state: 先行試験aaa903db2b4b6da8645df4c6e321e3f117cbdd6e commit/push完了、CI39865903670終端失敗。標準776件成功、配布定義欠落による構成Redを確認。実装候補はcommit_pending。
- authorization_boundary: 同課題の配布定義・試験・必要設計同期。外部ソース・設定変更、依存導入、配布物取得、コンテナ作成起動、mergeなし。
- continuity: 旧実装担当は終端のまま保持。通常レビューは既存担当を継続しSol/high、独立最終レビューは未開始。


## 実行コマンド

- bash -n scripts/tests/verify-linux-publish-outputs.sh — exit 0（shell 構文確認）。
- git diff --check — exit 0。
- scripts/tests/verify-linux-publish-outputs.sh の mode は 755。
- sha256sum scripts/tests/verify-linux-publish-outputs.sh — adfe7cd88a943a93bf59afc515b71f178b05d4255761280548d07ecd1cc2704f。先行検査の固定期待値とスクリプトは変更していない。
- PR #84 CI run 39865903670、head aaa903db2b4b6da8645df4c6e321e3f117cbdd6e、event pull_request — 終端 failure。Run dotnet test は 776 passed / 0 failed / 0 skipped。別step Verify Linux application publish outputs は RuntimeHost profile 欠落で exit 1。linux-publish-failure-39865903670-1 artifact ID 12463912638（7ファイル、2,286 bytes、https://github.com/ibis-ssl/Duck/actions/runs/39865903670/artifacts/12463912638）から stderr・check exit 1・publish exit not-run・環境情報を読取済み。ログは Tracker/Tracker.RuntimeHost/Properties/PublishProfiles/LinuxContainer.pubxml が存在しないと報告。実際に測れた構成Redはこの最初のprofile欠落だけで、後続二つのprofileや発行出力はまだ測定していない。
- CI artifact の環境: Ubuntu 24.04、.NET SDK 10.0.402、linux-x64。packet-flow と full-stack job は skipped。
- Windows Node focused textlint: exit 0。既存cache C:\Users\donabe\AppData\Local\Temp\issue80-mdlint-20261022-a1f4c7\node_modules へのjunctionを使い、新しい一時mirror issue81-04a-lint-20261022で同じリポジトリ設定を実行した。npm install は行っていない。
- 同じfocused cspell: exit 1、既存行296の Tailscale のみ。追加した説明文の appsettings / DLL などは一般語を日本語にし、実ファイル名はコード表記のまま保持。
- Yomiyasu focused strict: exit 0、score 100、is_clean true、findings 0。
- Sudachi whitelist focused: exit 1。既存の10件（TCP 9件、Tailscale 1件）だけで、追加した節の新規指摘はない。語彙辞書や全体設定は変更していない。
- 全文gateは再実行せず、既知のcspell 232件 / 5文書、Sudachi 890件 / 5文書の不合格状態を維持。
- ローカル dotnet publish と.NET標準試験は実行していない。remote_ci_onlyを維持し、profile実装後の実発行Greenは親のcommit/push後にCIで確認する。

## 対象ファイル

- Tracker/Tracker.RuntimeHost/Properties/PublishProfiles/LinuxContainer.pubxml — Release/net10.0/FileSystem、SelfContained=false、UseAppHost=false、単一ファイル/R2R無効、RIDなし。
- Tracker/Tracker.DebugHost/Properties/PublishProfiles/LinuxContainer.pubxml — 同じ明示profile。
- Testing/Duck.Testing.RefereeDriver.Host/Properties/PublishProfiles/LinuxContainer.pubxml — 同じ明示profile。各profile SHA-256は 54179c597446fe7382c2c80c38b1c005c71fa8014a12f5d0541bf605081dd1ab。
- Tracker/Design/Testing/aspire-simulation-test-environment.md — 04A配布契約の説明文を自然な日本語にし、固定期待値を変えずに用語を修正。
- scripts/tests/verify-linux-publish-outputs.sh と .github/workflows/dotnet-test.yml — 親の公開済み先行試験・CI配線を保持、変更なし。
- 3つの.pubxmlは .gitignore:195:*.pubxml でignoreされる。.gitignoreは変えず、commit時に正確な3パスをforce-addする必要がある。

## 指摘事項

- observed 04A Red は最初のprofile不在だけ。Run 39865903670は通常テスト成功後に検査stepが止まった。DebugHost / RefereeDriver.Host のprofile不在や各出力の不足を、このrunで測定したとは扱わない。
- 3 profile はいずれもRIDなし・framework-dependentで、CIの既存restoreと --no-restore に合わせている。外部image・runtime・debug資材の版とdigestは今回確定しない。
- CSpell と Sudachi のfocused実行は既存語彙項目のため不合格。該当行は既存TCP/Tailscale記述で、辞書追加・製品名を引用符で隠す回避はしていない。
- 自分の通常レビューは未実施。別担当の通常レビューplaceholderは親が作成済みで、レビュー未開始。

## 結果

先行検査の観測Redを受け、3個の明示Linux publish profileと04A設計の用語修正を追加した。固定期待値は維持し、DebugHostの wwwroot/app.css、wwwroot/favicon.png を含む実際のpublish出力検査を緩和していない。標準テスト結果（776 pass）と構成Red（RuntimeHost profile欠落）を区別して記録した。設計文書のfocused textlint/Yomiyasuは通過し、既存のspell/whitelist gate指摘を保持した。

## リスク

- profileがGit ignore対象のためforce-addされなければCIに含まれず、同じ構成Redが継続する。
- profile追加後の3プロジェクト全ての実publish出力、.runtimeconfig.json framework、静的資産同梱はCI Greenで未確認。
- profileファイルの出力定義SHA-256は各ファイル同一の 54179c597446fe7382c2c80c38b1c005c71fa8014a12f5d0541bf605081dd1ab。固定検査スクリプトは adfe7cd88a943a93bf59afc515b71f178b05d4255761280548d07ecd1cc2704f のまま。
- fixed base image/digest、外部成果物とdebugger供給物はI81-04B待機。取得、コンテナ作成起動、外部repo変更、依存追加、実機起動は行っていない。
