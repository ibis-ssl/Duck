# ASPIRE-004B 実装報告

## 対象

- リポジトリ: `ibis-ssl/Duck`
- GitHub Issue: #36
- Draft PR: #59（PR #28 の作業ブランチをbaseに指定）
- 実装HEAD: `a2a045f6e558dde8e0a90ed4cc428f7c65efc649`
- 関連: ASPIRE-003 R3 の `ExecutionConfigurationBuilder` API移行もテストファイルに含む。

## 実装

- `Testing/Duck.Testing.AppHost/Program.cs` に固定tagのCraneと `cm4-sim` 資源を追加し、両方をhost networkで実行する。
- `visibility_graph` plannerではCrane mode 4のUDP 12345を `cm4-sim` へ渡し、mode 3からDuckへ12346で返す。`cm4-sim` は `simulator` 起動後に開始し、CraneはDuck / Game Controller / cm4-simを待つ。
- `rvo2` plannerでは `cm4-sim` を作らず、CraneからDuckへ直接12346で送り、`FEEDBACK_SIM_MODE=true` を設定する。
- Crane tagは確認した `develop` commit `a544db92b72b137c8974285b36940d0d4b5e7e69` 固定のscenario tag、cm4-sim tagはCrane scenario既定値 `d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a`。
- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs` に固定tag、network mode、UDP引数、planner別資源、環境変数、`WaitForStart`依存を確認する試験を追加した。

## 検証

- テスト先行checkpoint `431fa354df558b5d5596835c7f7fac0fcd2bb678` のCI run `37174657246`: build成功、346件成功、追加した004B契約試験3件が期待どおり失敗。
- 実装HEAD `a2a045f6e558dde8e0a90ed4cc428f7c65efc649` のCI run `37175134903`: `.NET tests` 成功。Linux packet flow jobはskip。
- `git diff --check`: 成功。
- ローカル実行環境に `dotnet` SDK がなく、ローカル試験は未実施。
- 公開はローカルGit pushではなく、許可されたGitHub経路を使用した。認証設定は変更していない。

## 残作業

- PR #59 最新HEADに対するCIを確認し、独立通常レビューを完了する。
- 通常レビュー結果をPRとIssue #36へ記録する。
- PR #28へのmergeは行っていない。最終レビューと統合承認は親側で行う。
