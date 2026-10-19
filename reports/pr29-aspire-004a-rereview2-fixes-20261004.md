# PR #29 ASPIRE-004A 2回目再レビュー指摘対応報告

## 対象

PR #29 の2回目再レビューで残った次の指摘へ対応した。

- 一般語 `image` を行内コードで囲い、Markdown lint の検査対象外にしていた箇所
- `tasks-status.md` の bare `sidecar` を行内コードで囲い、同様に検査対象外にしていた箇所
- `aspire-004a-workflow.md` の「専用の操作接続」が、実際の RemoteDesktopMCP セッション分離条件を表していなかった箇所

対応開始時の PR #29 current HEAD は `a3e5bab1246350e80b6d5eff8e12d0438daefd2c`。
修正コミットは `ea944b8`。

## 修正内容

### Docker image 表記

一般語 `image` を行内コードで囲う記述を削除した。

- 資源の説明は「`Aspire AppHost` から Docker で起動する資源」とした。
- 検査対象は実装上の属性 `ContainerImageAnnotation.Image` と `ContainerImageAnnotation.Tag` として明示した。

これにより、一般語をコード表記で検査対象外にせず、実装契約を具体的に記述している。

### sidecar 表記

`tracker-snapshot-alignment.jsonl` の説明は、既存の許可済み設計語 `tracker snapshot alignment sidecar` を通常文として使用した。
bare `sidecar` を行内コードで囲う記述は削除した。

### RemoteDesktopMCP の作業分離

「専用の操作接続」を次の具体要件へ変更した。

- 専用の Git `worktree` を使う。
- `RemoteDesktopMCP` の `session_id` をこの作業専用に1つ確保する。
- 並行する `ASPIRE-003A/B` と `worktree` / `session_id` を共有しない。

これにより、単なるフォルダーや接続ではなく、実際の Git worktree と RDMCP セッション分離条件を明示した。

### 差分検査

2回目再レビュー報告 `reports/pr29-aspire-004a-rereview2-20261004.md` の末尾にも余分な空行があったため削除した。

次は終了値 0。

```text
git diff --check
git diff design/issue18-aspire-test-orchestration --check
```

## Markdown lint

PR #30 の Markdown lint ラッパーと、その CI が固定する CodexSkill commit `583a9594d8157fc101dff3c6b3338238809d9779` を隔離 worktree で使用した。
許可一覧 `tools/lint/markdown-whitelist.yaml` は変更していない。

`Tracker/Design/Testing/aspire-004a-workflow.md`:

- textlint: 成功
- CSpell: 指摘 0
- 厳格許可一覧検査: 違反 0

`Tracker/Design/tasks-status.md` は PR #28 ベース側の既存違反と分離するため、PR #29 が追加した5行を UTF-8 Markdown として差分抽出し、同じ3検査を実行した。

- textlint: 成功
- CSpell: 指摘 0
- 厳格許可一覧検査: 違反 0

## 対象テスト

.NET SDK 10.0.401 で個別実行した。

- `AppHostApplicationModelTests`: 5 / 5 成功
- `RefereeDriverFixtureTests`: 5 / 5 成功
- `RefereeDriverNetworkAdapterTests`: 4 / 4 成功

合計 14 / 14 成功。

## CI

この報告を push 後、PR #29 の current HEAD と `headSha` が完全一致する `.NET tests` run だけを最終 CI 判定対象とする。
別 SHA の run は代用しない。

merge は行わない。
