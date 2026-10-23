# Issue81 I81-04B 配布資材の調査報告

## Dispatch profile（親所有）

- selection_source: continuity_reuse
- task_kind: investigation
- signals: {work_class: judgment_heavy, uncertainty: high, change_radius: cross_module, criticality: medium, repetition: single, decomposability: independent_workstreams, decomposition_policy: forbidden, context_need: bounded_history}
- decomposition_disposition: 既存担当で逐次実行。新規担当の追加なし。
- automatic_floor: Sol/high（高い不確実性と判断を伴う調査）。ユーザーの現行モデル固定指示を優先してLuna/highを継続し、無断で変更しない。
- original_requested: {model: gpt-6-luna, reasoning_effort: high, fork_turns: none}
- executor: /root/issue81_luna_implementation
- applied: null
- application_status: reused_existing_agent_profile
- profile_observability: final_profile_hidden
- role_plan: original default role reused; effect unknown
- scope: Issue81/PR84、I81-04B固定配布資材・デバッグ構成。既存worktree/branch、基準5f280e33da9f0e93d7396373c9573b0f21424582。04Aは通常レビュー条件付き合格、全体未完了。
- boundary: 読取調査と本報告の子5欄のみ。外部変更・取得・導入・コンテナ作成起動・配置切替・mergeなし。
- parent_disposition: 読取結果を回収。既存AppHostの実image選択への接続はI81-05の範囲とし、04Bの資材定義と混同しない。8platform参照と3GHCRの公開由来は候補証拠、まだ採用・実供給・起動の成立ではない。Debug供給は不足を保持。
- evidence_validation: 親がSHA256SUMS.txtの68データentryを照合。manifest自身を含む69ファイル。証拠束はworker保存の未追跡資料として保持、未commit・未公開。最初の親読取はファイル名と列形式の仮定違いで失敗し、実名と3列形式を確認して回復。
- prior_publication_blocker: 04A記録commit/pushと既存PR報告は初回実行前拒否。後続の具体的ユーザー原文を根拠とする各同一操作1回の再審査で許可され、28f227dと既存PR報告更新が成功。04Bの本調査資料はそのcommit対象外。

## 実行コマンド

- I81-04Bの公開メタデータ照会では `docker manifest inspect --verbose <既存タグ>` のみ実行。8参照すべてexit 0。pull/build/runなし。
- GHCR package versions APIは3参照とも403、gh exit 1。stderrは `You need at least read:packages scope to get a package's versions. (HTTP 403)`。認証変更・API再試行なし。
- vsdbg配布先へのHTTP HEADのみ。応答200。installer scriptの最終URL等を記録し、本体は取得せず。
- 04AのPR #84、head `28f227dbcd0e7b1643203231942ce91718dfe72c` に対する既存 `.NET tests` pull_request run `39875364344` を読み取り確認。run completed/success。standard job `120159553408` のログでテストと発行出力を照合した。rerun/dispatchなし。
- 保存証拠: `reports/evidence/issue81-04a-final-ci-28f227-20261023/`。9ファイル（8 data + SHA256SUMS）、合計9218 bytes、manifest SHA256 `3732a8e467b7085ad1ecbc70740ba76b9895687779ab5837ca58ee9e71af7c41`。run JSON、選択ログ行、空stderr、exit code、run identity、コマンド記録を収納。前回の69ファイル04B束は変更していない。

## 対象ファイル

- 既存設計: `Tracker/Design/Testing/aspire-simulation-test-environment.md` L35-L45 は実発行した3管理プログラムの配布物を指定。L224-L231 は同じ版のDebugHost実行物・移植可能なデバッグ情報、事前配置する `/vsdbg/vsdbg`、`/src` からWindowsソースへの対応を指定。
- 既存発行検査: `scripts/tests/verify-linux-publish-outputs.sh`。Releaseで3プロジェクトを実際に発行し、DLL/deps/runtimeconfig、設定・静的資産、共有実行基盤を検査。DebugHostのPDBとそのソースパスは検査しない。
- 設定・CI: `Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj` は `net10.0` Web SDKで、明示的な `DebugType`/`DebugSymbols`/`PathMap` 設定なし。`.github/workflows/dotnet-test.yml` は標準テストの後に上記実発行検査を行う。I81-05のAppHost image選択は今回の変更・提案範囲外。
- 前回の公開メタデータ証拠: `reports/evidence/issue81-04b-readonly-20261022/`（69 files / 220209 bytes、manifest SHA256 `6a8db74566edd185c05c519e3dd7d0e789cf05e82aba428566e10c1f45ec1b31`）。原本不変。

## 指摘事項

- exact CI run `39875364344`: `.NET tests` job success。standard suiteは **776 passed / 0 failed / 0 skipped**。同じjobの `Verify Linux application publish outputs` は成功し、RuntimeHost・DebugHost・RefereeDriver.Hostの3つすべてで「Linux publish output contract passed」。テストとpublish検査を別結果として記録する。
- `ASPIRE-NET-002 Linux packet flow` と `ASPIRE full-stack Linux acceptance` は両方skipped（pull_request event）。失敗診断収集/アップロードstepもsuccess jobの条件によりskipped。artifactを取得・生成したとは扱わない。
- I81-04Bの次の自然な最小単位は、**DebugHostの実発行物にportable PDBとソースパス対応を定義し検査する契約**。これならI81-05のAppHost image選択、外部vsdbg、image buildを待たずに進められ、設計のデバッグ対応条件へ直接つながる。
  - 仕様→固定期待値: 実際の `dotnet publish Tracker/Tracker.DebugHost` 出力に `Tracker.DebugHost.dll` と隣接 `Tracker.DebugHost.pdb` を含める。PDBのCodeView識別子と対象DLLの識別子が一致し、portable PDB documentに `/src/Tracker/Tracker.DebugHost/Program.cs` を含む（および他の同一repo source pathを `/src/` 以下に収める）。Windows側のsourceFileMapで `/src` を同じrevisionのcheckoutへ対応させる。
  - 先行test/scaffold候補: 既存 `scripts/tests/verify-linux-publish-outputs.sh` が出したDebugHostの実publish directoryを対象にする。manifestや新しいファイル一覧の自己検査にせず、実行物/PDBの対応とsource document pathを固定値検査する。追加NuGet packageなしで使えるSDK/.NET metadata APIの有無は実装前に確認し、別依存導入をしない。
  - 予想されるRed: 現行projectには `/src` へのPathMap/source mapping設定が見当たらず、runner上のcheckout絶対パスがPDBへ入る可能性が高い。ただし今回のCIはPDB内部を保存・検査していないため、Redの実測とは主張しない。先行testで既存実publishの値を採取し、固定期待値との差を確認してから実装する。
- プラットフォーム契約: 設計はLinux向け配布を指定するがCPU architecture/RIDは指定していない。現profilesはRIDなしのframework-dependent publish、検査scriptは明示的にRIDを付けない。`ubuntu-latest` runnerだけでは利用者向けコンテナplatform選択の根拠にならない。したがって前回記録のlinux/amd64 manifest digestsは照会時のamd64 child descriptorにすぎず、正式な採用platformではない。image base/Dockerfileでplatform別digestを固定する単位は、対象platformが設計決定されるまで保留する。
- 外部取得なしで完了できる上記PDB/source mapping契約とは別に、Debug専用実イメージへ含めるvsdbg本体のversion/digest、取得元固定、再配布条件は未供給のまま。installer URLへのHEAD応答は本体や許諾の根拠ではない。現状、追跡Dockerfile/.pdb/vsdbgはない。通常ビルドのbin/obj PDBは配布済み/debug image資材とは扱わない。

## 結果

- 04A exact PR CIはsuccess: 776件の標準suiteと3管理プログラムの実Linux publish/file checkが成功。packet/full-stack acceptance jobはskippedで、実行成功ではない。
- 04Bの推奨次単位はプラットフォームや外部供給に依存しないDebugHost portable PDB/source path contract。変更候補は既存publish validator、DebugHostの発行設定、同じ実発行物を読む小さなmetadata test helper。親のcanonical分割前にコード・設計は変更していない。
- 前回のamd64 digestは採用決定ではなく読取結果として保持。外部3 Docker Hub refsのsource provenance、vsdbgの固定資材・配布条件は未解決。
- 今回、CI rerun、コード/設計/配置/設定変更、image取得/build/start、外部repo改変、依存導入はなし。報告の子5欄とCI証拠束のみ追記。

## リスク

- portable PDBとソースpathの整合は現行CIのpublish successからは証明されない。先行検査で実publish出力を読み、CodeView/PDB識別子とdocument pathを見てRedを確認する必要がある。
- `/src` のPDB document pathとWindows側sourceFileMapは対応する同一revisionを必要とする。SourceLink/PathMapの正確な実装方法は未検証で、依存追加なしの実装可能性を調査してから選定する。
- コンテナ基底/外部manifestを固定する前に、製品のCPU architecture/platformを設計で決める必要がある。CI runnerのplatformから利用者契約を推測しない。
- GHCR API 403、Docker Hub由来の未確定、vsdbg実体/再配布条件の不足は継続。digest確認を実供給・起動成功と表現しない。
