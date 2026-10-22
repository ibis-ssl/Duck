# Sub-agent実行レポート

## タスク

I81-01: Duck内の純粋な診断スナップショット・所有ID/世代・受信鮮度判定契約を先行実装する。

## sub-agentを使う理由

利用者指定の同一実行担当が実装し、親は範囲・統合・レビューを所有する。

## 対象範囲

BCLのみの小libraryと既存テストプロジェクト。完全ID/世代一致、同世代count増分、古い応答・不明な受信age・補助観測の拒否。外部型を持ち込まない。

## 対象外

HTTP/JSON/DCP接続、外部adapter、実サービスの計測値取得・所有証明・時刻正規化、外部repo編集、依存追加/導入、環境/認証/network変更、image取得/build/container起動、実機、commit/push、PR/Issue操作、nested agentは禁止。

## Dispatch profile

Parent-owned. task_kind=implementation; work_class=judgment_heavy; uncertainty=medium; change_radius=cross_module; criticality=high; repetition=single; decomposability=sequential_dependencies; decomposition_policy=forbidden; context_need=bounded_history. selection_source=user explicit gpt-6.1-sol/high; requested=gpt-6.1-sol/high from original actual spawn. role/default profile effect hidden; planned_runtime_profile=requested, exact applied=null. application_status=reused_existing_agent_profile; profile_observability=final_profile_hidden. continuity=/root/multicast_investigation_fresh; no new spawn/fork; parent requested gpt-6.1-sol/low. Sol xhigh/max/Astra not requested.

## 実行コマンド

実行場所は `/home/donabe/Duck-issue81-aspire-desktop-bridge`、HEAD は `6902b736d7934a65147624b2581c3a82e86a2e8a`。PowerShell の `login=false` / `require_escalated` から WSL Ubuntu の既存 .NET SDK 10.0.113 / MSBuild 18.0.36 を使用した。

必須の work-context-manager、implementation-executor、implementation-worker、report-output-manager、report-writer を `/home/donabe/CodexSkill/skills/<name>/SKILL.md` で実読し、I81-01 の純粋契約と子所有報告欄の境界に適用した。root/対象下の AGENTS.md は存在しない。新しい担当・モデル選択・外部調査は行っていない。

全コマンド引数・終了コードは `/tmp/issue81-readiness-validation-20261022/commands.json`。同ディレクトリに各段階の stdout/stderr、exit-code、binlog、TRX を保持した。

| 段階 | 終了コード | 実結果 |
| --- | ---: | --- |
| 初回標準 focused test、no-restore | 0 | 空出力、TRX なし、実行 0 件。成功として扱わない |
| 標準 Tracker.Tests の offline restore | 0 | 現在の worktree に必要な assets を生成。既存ローカル cache のみ |
| 標準 focused test、restore 後 | 1 | Microsoft.Build.Tasks.Git が relativeworktrees extension を未対応として停止。テスト未実行 |
| 同標準 test、SCM queries をコマンド限定で無効化 | 1 | SslProto の未展開 submodule により CS0246。テスト未実行 |
| 初回純粋 library build | 0 | 警告 0、エラー 0 |
| 一時 harness offline restore | 0 | 既存 cache の xUnit/Test SDK のみ |
| 修正前 harness 全件 | 0 | 38 executed / 38 passed / 0 failed / 0 skipped |
| 追加した時刻逆行境界 test | 1 | 1 executed / 1 failed。Assert.False が Actual True |
| 最終 library build | 0 | 警告 0、エラー 0 |
| 最終 harness 全件 | 0 | 39 executed / 39 passed / 0 failed / 0 skipped |
| 最終 harness list-tests | 0 | 39 件を列挙。TRX の実行 39 件とも一致 |
| git diff --check | 0 | 診断なし |

標準および harness の restore は以下と同じ制約で行った。NuGet 設定は変更していない。

```text
dotnet restore Tracker/Tracker.Tests/Tracker.Tests.csproj --source /home/donabe/.nuget/packages --disable-parallel -p:NuGetAudit=false -p:RestoreAdditionalProjectSources= -m:1 /nr:false
dotnet restore /tmp/issue81-readiness-validation-20261022/focused-harness/Focus.Tests.csproj --source /home/donabe/.nuget/packages --disable-parallel -p:NuGetAudit=false -p:RestoreAdditionalProjectSources= -m:1 /nr:false
```

最終の検証コマンド:

```text
dotnet build /home/donabe/Duck-issue81-aspire-desktop-bridge/Testing/Duck.Testing.Diagnostics/Duck.Testing.Diagnostics.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:EnableSourceControlManagerQueries=false -bl:/tmp/issue81-readiness-validation-20261022/library-build-final.binlog
dotnet test /tmp/issue81-readiness-validation-20261022/focused-harness/Focus.Tests.csproj --no-restore -m:1 /nr:false -p:UseSharedCompilation=false -p:EnableSourceControlManagerQueries=false --logger 'trx;LogFileName=focused-final.trx' --results-directory /tmp/issue81-readiness-validation-20261022/final-results -bl:/tmp/issue81-readiness-validation-20261022/harness-test-final.binlog
dotnet test /tmp/issue81-readiness-validation-20261022/focused-harness/Focus.Tests.csproj --no-restore --no-build --list-tests -m:1 /nr:false -p:EnableSourceControlManagerQueries=false
```

`-p:EnableSourceControlManagerQueries=false` は SDK の Git reader 制約を避ける検証コマンド限定の指定。製品設定には保存しておらず、この実行で SourceLink/Git metadata の成立を検証したとはしない。`--no-restore`、単一 MSBuild worker、node reuse 無効、shared compilation 無効で実行した。

新規 5 file に対する `git diff --no-index --check -- /dev/null <file>` は各 exit 1、stdout/stderr とも空。これは新規内容との差分を表し、空白診断はない。tracked 差分の `git diff --check` は exit 0。

## 対象ファイル

子が編集した製品関連 7 file:

- `Testing/Duck.Testing.Diagnostics/Duck.Testing.Diagnostics.csproj`: net10.0 の BCL のみの library。PackageReference なし。
- `Testing/Duck.Testing.Diagnostics/ResourceDiagnosticIdentity.cs`: run/resource/完全 container ID/世代の値契約。
- `Testing/Duck.Testing.Diagnostics/ResourceDiagnosticSnapshot.cs`: 入力 ID、取得開始前に割り当てる観測 sequence、サービス自身の decode と補助観測の区別、正常 decode count、根拠のある age 上限。
- `Testing/Duck.Testing.Diagnostics/ResourceReadinessState.cs`: 完全一致、世代切替、進捗と有限鮮度、古応答と不確かな age の拒否。
- `Tracker/Tracker.Tests/ResourceReadinessStateTests.cs`: 39 ケースの純粋契約 test。
- `Tracker/Tracker.Tests/Tracker.Tests.csproj`: library の ProjectReference 1 件追加。
- `Duck.slnx`: Testing folder 内の library 登録 1 件追加。

報告更新は本 file の子所有 5 欄のみ。親の `Tracker/Design/tasks-status.md` と既存の調査 report は変更していない。

検証用一時 harness は `/tmp/issue81-readiness-validation-20261022/focused-harness/Focus.Tests.csproj`。上記の実 test source を絶対パスの Compile Link、実 library project を絶対パスの ProjectReference で直接参照する。ソースの複製や別 worktree の assets の流用はない。Microsoft.NET.Test.Sdk 17.14.1、xunit 2.9.3、xunit.runner.visualstudio 3.1.4 は既存の標準 test と同版で、既存 cache だけを使った。一時 harness は標準 test project を代替する受入 gate ではない。

最終 SHA256 は `/tmp/issue81-readiness-validation-20261022/final-inputs.json` に全 7 file と harness project を保存した。

| file | SHA256 |
| --- | --- |
| `Testing/Duck.Testing.Diagnostics/ResourceDiagnosticIdentity.cs` | `bd81e0e125852c80511d29b71541466973a7a67e574ea8c28394f969f37bbab2` |
| `Testing/Duck.Testing.Diagnostics/ResourceDiagnosticSnapshot.cs` | `0c34d0436647dc466a2ee1017f8e3b3dd61379ee4f92f055ffdad2e50dfdbbf0` |
| `Testing/Duck.Testing.Diagnostics/ResourceReadinessState.cs` | `0d6792f188fbaa91c56ee5a1da9e739d0b068d7931f4526f8dc366a639641da7` |
| `Tracker/Tracker.Tests/ResourceReadinessStateTests.cs` | `a1875bc5418277790b1b9f28569e95c78775e5b3a0e665628b6fa19c4fb89b7c` |
| `harnessProject` | `a73932c060ef6490004fca88d84ad8e5e19eea96a23b767d35dab433504f370f` |

## 指摘事項

標準 gate の阻害は解消していない。具体的には SDK の Git reader が `relativeworktrees` を読めず、コマンド限定の回避後も未展開 SslProto submodule に由来する `SSL_WrapperPacket`、`TrackerWrapperPacket`、`Referee` 等の CS0246 が Tracker.Core/TrackerConnectionLib/RefereeDriver で発生する。代表位置は `Tracker/Tracker.Core/Proto/TrackerPacketGenerator.cs:39` と `Tracker/Tracker.Core/Engine/ITrackerEngine.cs:13`。`red-local.stdout.log` に全文がある。submodule 取得・標準 gate の修復には進んでいない。

追加境界 `BackwardClockAfterUnchangedPollInvalidatesReadiness` は、count が増えない新しい poll 後の時計逆行で古い進捗が Ready のままになることを 1 件の実失敗として確認した。`clock-red-results/clock-red.trx` と `clock-regression-red.stdout.log` に保持した。最新観測時刻との比較と InvalidClock 時の進捗無効化を追加し、時計が追いついただけでは復活せず新しい count 増分を必要とするよう修正した。最終 39 件はその修正後の実行である。

初期の test source を実装より先に記述したが、標準 project の最初の失敗は環境/参照不足であり、機能上の Red とは扱わない。機能上の失敗と成功を回収できたのは上記の局所的な時刻境界である。子による独立レビュー合格判定は出していない。

## 結果

純粋なメモリ内契約を実装した。サービス自身の正常 decode count が同じ identity/input/世代内で増加し、根拠ある age 上限とローカル経過時間が window 内のときだけ診断上の候補 Ready を返す。最初の count は基準値であり増分とはしない。非増加 count の poll は lease を延命しない。unknown/negative/stale age、補助観測、失敗 decode、負 count、巻戻り count は不成立。古い sequence/異なる ID は新状態を上書きしない。所有者による明示的な世代更新で基準値を消去する。

最終補助検証は discovery 39 / TRX total 39 / executed 39 / passed 39、failed/notExecuted 0。最終 TRX は `/tmp/issue81-readiness-validation-20261022/final-results/focused-final.trx`、SHA256 `2461799fd6f00232db69d60b4407a175de06954cecc4bb8ca4edf4f1b6898d6f`。修正前 38 件の成功は別ファイル `focused-results/focused.trx` に残し、修正後の証拠に転記していない。最終 library build も exit 0 / 警告 0 / エラー 0。

HTTP/JSON/DCP 型や外部 schema、adapter、実サービスの時刻変換・証明取得を追加していない。完全 ID の形式検査と値比較は所有証明ではなく、age の真実性もこの library 単独では証明しない。G1/G3 の外部契約は継続保留で、実際の Windows 配置、受信、サービス Ready、全体受入の成功を示す結果ではない。

全発行コマンドの終了コードを回収済み。確認時に当該 dotnet build/test/restore、testhost、MSBuild の実行中 process は 0 件。承認待ち・結果不明の操作はない。commit/push/PR/Issue 操作はしていない。

## リスク

- 標準 Tracker.Tests/solution gate は CS0246 で未完了。harness の成功から標準 gate 成功を推論しない。
- 呼出側が信頼できる所有者から expected identity と新世代を取得し、入力ごとの sequence と正常 count、取得遅延を含む保守的な age を供給する必要がある。その外部接続は本 unit の対象外。
- 不明な外部時刻を poll 時刻で置き換えることは禁止する契約であり、正規化が確定するまでは age=null / not-ready。外部サービスの telemetry と取得 API の成立は未検証。
- source metadata、DCP/Docker/Windows、実機、全体受入は未検証。独立レビューと親側統合の判断が残る。
