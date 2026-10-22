# Duck PR #83 再レビュー報告（2026-10-22）

## 対象と判定

- PR: https://github.com/ibis-ssl/Duck/pull/83 （Draft）
- 技術レビュー対象 HEAD: `b4bb26ea275d86a25c576cafbbb268e4937a81b9`
- 基準: 前回レビュー報告コミット `6a442dbfc9b29833e9eea338203ca5b0c1c55007`。
- 結果: **pass_with_held**。前回の P2 2件および P3 1件は解消を確認。今回の限定差分に新たな必須指摘なし。
- 保留: Windows/Docker Desktop/Aspire DCP、ブリッジUDP/マルチキャスト、Game Controller UI/API、資源再作成と所有者限定停止の実機受入は未実施。mergeは行わない。

## 前回指摘の確認

### P2-1（解消）配置と起動主体

`Tracker/Design/Testing/aspire-simulation-test-environment.md` の配置図で `ref[referee-driver / RefereeDriver]` を Windows ホスト側の `ProjectResource` グループへ移動し、AppHostからの起動矢印を追加した。Docker Engineからrefへのコンテナ起動矢印は削除された。起動シーケンスでは `Desktop-->>Duck` と `Desktop-->>Ref` をそれぞれ `AppHost-->>Duck`、`AppHost-->>Ref`（ProjectResource）へ変更。本文の資源境界と整合することを差分で確認した。

### P2-2（解消）PR全体の説明

PR本文に、全体差分には `Testing/Duck.Testing.AppHost/DockerContainerWrapper.cs` および `Tracker/Tracker.Tests/DockerContainerWrapperLifecycleTests.cs` のコード・テスト変更が含まれると明記。今回の図修正コミット `b4bb26e` は文書のみの変更と区別された。全体差分の実態と一致する。

### P3（解消）Skillリンクの前提

PR本文に `.agents/skills` が `../../CodexSkill/skills` へ向く Git シンボリックリンクであり、Duck単独checkoutでは参照先がなく、Skill利用には兄弟ディレクトリとしてのCodexSkill配置が必要と明記。Git上の対象は引き続き mode 120000 で、今回リンク内容の変更なし。構造上の環境依存は残るが、前提説明不足は解消。

## 差分と図の確認

`6a442db..b4bb26e` の変更は設計Markdown 1件・8行（4追加4削除）のみ。Markdownの配置図とシーケンス図をコードとして読解し、Windows側ProjectResourceとコンテナ起動主体の区分が一貫していることを確認した。GitHub画面のレンダラでの図表示成功はPR作成者の報告であり、本レビューでは別途描画試験を実施していない。実装を伴わない設計案であり、通信成立やDCP挙動の証拠ではない。

## 検証証拠

- Yomiyasu（変更設計Markdown）: 既存WSL環境で直接実行、exit 0、100/100、`is_clean=true`、findings 0。
- `git diff --check 6a442db..b4bb26e`: exit 0。レビュー専用worktreeに追加の実装変更なし。
- HEAD一致 GitHub Actions [run 39718658379](https://github.com/ibis-ssl/Duck/actions/runs/39718658379): test成功、476/476、失敗0・skip0。Linux packet-flowとfull-stack acceptanceは skipped。これはWindowsの実機受入成功を示さない。
- CIログには既存警告 `NU1902`（MessagePack 2.5.302）と `ASPIRE010` がある。テスト失敗ではない。
- テスト失敗時の診断artifact workflowあり: TRX、テストstdout/stderr、vstest診断、binlog、環境・ソース情報を失敗時に保存する定義を確認した。
- textlint/CSpell/Sudachiの成功およびGitHub図描画はPR本文での報告。独立レビュアーとして再実行したのはYomiyasuのみ。レビュー専用cloneにnode_modulesがないため、正式文書lintは再実行していない。

## 結論と追跡

P2/P3の指摘はいずれも閉じる。限定再レビューで新規の必須修正事項なし。実機受入条件とネットワーク制約は継続して未検証とする。ここでいうpassは既存指摘への文書上の対応確認であり、製品実装や統合受入の合格・merge許可を意味しない。本報告を追加する管理用コミットはレビュー対象の技術HEADとは区別する。
