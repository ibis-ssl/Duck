# Duck PR #83 独立レビュー（2026-10-22、対象 a650fab）

## 対象と判定

- PR: https://github.com/ibis-ssl/Duck/pull/83 （Draft）
- レビュー対象の元HEAD: `a650fab9539e3675ba11c7a7aa62c647e10b50d7`
- 比較先: `design/issue18-aspire-test-orchestration`、`c72939ec076ec3261357ba16e61fc5140de2b3d4`
- 変更範囲: 18ファイル（追加1846行、削除583行）。設計本文・履歴・台帳・報告・許可語・Skillリンクに加え、Docker wrapperとライフサイクルテストを含む。
- **判定: 要修正（P2 1件、P3 1件）。** 新方式の実装・実機受入は未完了。mergeは行わない。

## 指摘事項

### P2: ProjectResourceの起動主体の図がAspire/DCPの実行モデルと不一致

対象: `Tracker/Design/Testing/aspire-simulation-test-environment.md:45-48,170-176`

配置図は `app -. ProjectResource 起動 .-> duck/debug/ref`、起動図は `AppHost-->>Duck/Ref: プロセス開始` として、AppHostがWindows上のProjectResourceプロセスを直接起動するように表現している。一方、同設計書11行付近はDCPが資源の起動と監視を担当すると説明している。

Aspireの正式資料では、AppHostがアプリケーションモデルをDCPへ渡し、DCPがProjectResourceに対応する実行プロセス（Executable）を起動・監視する。コンテナはDocker Engine上で実行されるが、Windows上のProjectResourceもDCPの実行制御下にある。

- https://learn.microsoft.com/en-us/dotnet/aspire/architecture/overview
- https://github.com/microsoft/aspire/blob/main/docs/specs/IDE-execution.md

**修正提案:** AppHost→DCPを「ProjectResource定義/登録」、DCP→WindowsのDuck/DebugHost/RefereeDriverを「プロセス起動/監視」に変更し、コンテナについてはDCP→Docker Engine→コンテナの実行経路と区別する。通信図でも`refereeDriver`はWindowsホストのsubgraph外にあるため、ホスト上のProjectResourceである旨を配置で明確化すると一貫する。

前回指摘されていた「Docker DesktopがDuck/RefereeDriverを起動する」という説明は修正されたが、起動主体の訂正がまだ実際のDCP境界と一致していない。これは図の記載不整合であり、新方式の実装障害を実証したものではない。

### P3: PR本文の「最新コミット」とレビュー状態が古い

PR本文は `b4bb26ea275d86a25c576cafbbb268e4937a81b9` を「最新コミット」と呼び、「今回の修正後レビューは未実施」としている。しかし対象HEADは`a650fab9539e3675ba11c7a7aa62c647e10b50d7`であり、既にb4bb26e対象の再レビュー報告とPRコメントが存在する。

**修正提案:** 技術変更コミット`b4bb26e`と報告追加コミット`a650fab`を区別し、レビュー済み範囲と未検証範囲を更新する。

## 前回指摘への対応確認

- RefereeDriverをWindows上のProjectResourceに置き、Docker Engineによるコンテナ起動図から除いたことは確認済み。ただしP2のDCP起動主体を要訂正。
- PR本文にDockerContainerWrapperとLifecycleTestsのコード変更が含まれる説明あり。「PR全体が文書のみ」という誤記は解消済み。
- `.agents/skills` はGit mode `120000`、参照先 `../../CodexSkill/skills`。PR本文に隣接CodexSkillが必要なことを記載。Windowsのレビュー用cloneではシンボリックリンクとして存在し、`core.symlinks=true`を確認した。

## コードとテストの静的確認

- `DockerContainerWrapper.cs`: cleanup候補をinspectして完全64桁IDに展開し、ID・名前・イメージ・所有ラベル・hostネットワークモードを照合。stop/rm前にも同じ完全IDの所有条件を再確認する構造を確認。
- `DockerContainerWrapperLifecycleTests.cs`: 短縮IDからの検証、作成中キャンセルの所有者不一致/inspect失敗、stop後の所有者変化に対する回帰試験を確認。今回の独立静的確認では、新たな必須コード欠陥は発見できなかった。
- 既存workflow `.github/workflows/dotnet-test.yml` は失敗時のTRX、stdout/stderr、vstest診断、binlog、環境・ソース情報をartifact化する定義を含む。変更不要。

## 実行した検証

- PR現行HEAD`a650fab9539e3675ba11c7a7aa62c647e10b50d7`と一致するGitHub Actions [run 39721931488](https://github.com/ibis-ssl/Duck/actions/runs/39721931488)を確認。 .NET tests: **476成功、0失敗、0スキップ**。Linux packet-flowとfull-stack acceptanceの別jobはskip。HEAD不一致のrunを代用していない。
- `git diff --check origin/design/issue18-aspire-test-orchestration...HEAD`: exit 0。
- 独立Yomiyasu: 現行設計書 **100/100**、`is_clean=true`、findings 0、exit 0。
- 独立Yomiyasu: 再編前の設計履歴は68/100（excess_listなど警告）、台帳`tasks-status.md`は0/100（箇条書きが大半でtrailing_colon等の警告）。これらは履歴保存文書・台帳の構造由来の評価を含み、今回新設計本文の違反とは判定しない。
- 今回はtextlint/CSpell/SudachiとWindowsでのfull-stack実機試験を独立再実行していない。文書lintの成功報告はPR作成者の証跡として区別する。

## 受入保留・次の確認

Windows AppHost/DCPの実動作、Docker Desktop内ブリッジUDP/マルチキャスト、ホストProjectResourceとの到達性、数値IP/localhostの外部契約、Game ControllerのUI/API、所有者限定の停止・削除は未実装または未検証。提案構成を受入済みとして扱わない。

本報告の追加コミットによりPR HEADが更新される場合、上記のCI結果はレビュー対象元HEADに対する記録である。新しいHEADに紐付くrunを別途確認し、存在しなければCI未実施と記載する。
