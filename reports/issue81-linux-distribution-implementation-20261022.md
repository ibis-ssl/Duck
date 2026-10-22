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
- publication_state: commit_pending / push_pending / ci_wait_pending。
- authorization_boundary: 同課題の配布定義・試験・必要設計同期。外部ソース・設定変更、依存導入、配布物取得、コンテナ作成起動、mergeなし。
- continuity: 旧実装担当は終端のまま保持。通常レビューは既存担当を継続しSol/high、独立最終レビューは未開始。


## 実行コマンド

- `bash -n scripts/tests/verify-linux-publish-outputs.sh` — exit 0（shell 構文確認のみ）。
- `git diff --check` — exit 0。
- `stat -c "%a %n" scripts/tests/verify-linux-publish-outputs.sh` — mode 755。
- `sha256sum scripts/tests/verify-linux-publish-outputs.sh` — `adfe7cd88a943a93bf59afc515b71f178b05d4255761280548d07ecd1cc2704f`。固定出力期待値と検査実装はこのsource hashに含む。
- 対象CIコマンド: `.github/workflows/dotnet-test.yml` の既存.NET jobが標準 `dotnet test` の後に `scripts/tests/verify-linux-publish-outputs.sh` を実行する。scriptは3プロジェクトを `dotnet publish --configuration Release --no-restore --output <temp> -p:PublishProfile=LinuxContainer` で発行し、期待ファイル、.NET 10 framework、共有ランタイム依存、native apphost不在を確認する。
- 想定Red（未実行）: `Required Linux publish profile is missing: <project>/Properties/PublishProfiles/LinuxContainer.pubxml`。これは発行設定未定義の構成Redである。CI時は確認用stderrとexit-codeを `artifacts/linux-publish/` に保存し、失敗時に環境情報と併せてartifact uploadする。profile追加後の実publish出力検査がGreen証拠となる。
- 設計文書のfocused lintは実行不能。WSL内にNode.js/npmがなく、PowerShell経由の `npm run lint:md:*` はWindows NodeがUNC作業ディレクトリを扱えず `C:\Windows\tools\lint\scripts\...` のmodule-not-foundで終了。依存導入はしていない。既存full文書lintの保留状態も引き継ぐ。
- `dotnet publish`、.NET標準試験、CIは未実行。検証経路はremote_ci_onlyで、CI Redは親のcommit/push後に回収する。

## 対象ファイル

- `Tracker/Design/Testing/aspire-simulation-test-environment.md` — I81-04Aの配布出力・固定期待値を既存設計へ追加。
- `scripts/tests/verify-linux-publish-outputs.sh` — 実際のpublish出力を検査する先行試験。
- `.github/workflows/dotnet-test.yml` — 既存.NET jobに検査stepと失敗時のログ・環境artifact収集を追加。既存BOM保持。
- `reports/issue81-linux-distribution-implementation-20261022.md` — 本報告の子所有5欄を更新。Dispatch欄は変更していない。

## 指摘事項

独立レビュー未実施。ここに記したRedは設計上の予測で、実行結果ではない。scriptが参照する `LinuxContainer.pubxml` はまだ作成していない。

## 結果

先行試験・CI配線と04A設計同期を用意した。固定期待出力:

- RuntimeHost: `Tracker.RuntimeHost.dll`、`.deps.json`、`.runtimeconfig.json`、`appsettings.json`、`Microsoft.NETCore.App 10.0.x`。
- DebugHost: 同名出力一式、`wwwroot/app.css`、`wwwroot/favicon.png`、`Microsoft.AspNetCore.App 10.0.x`。
- RefereeDriver.Host: 同名出力一式、`Microsoft.AspNetCore.App 10.0.x`。環境変数設定のみのためappsettingsを要求しない。

出力は共有.NET 10ランタイムを使うmanaged DLL形式とし、RID固有ランタイムやnative apphostを同梱しない。profileは既存CI restoreの結果を再利用するためRIDを持たせず、検査コマンドも `--no-restore` とする。

## リスク

profile追加前のCI Red、profile追加後の実publish出力、失敗artifactの取得はいずれも未確認。固定base image、外部digest、Debug専用vsdbg/PDB供給はI81-04B待機。画像取得・作成・起動、外部repo変更、依存追加、実機実行は未実施。変更した設計文書のfocused lintは実行環境不足のためunsupportedであり、成功扱いしない。
