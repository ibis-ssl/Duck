# PR #29 ASPIRE-004A 3回目再レビュー

## 対象

- 親設計: PR #28
- 実装 PR: #29
- 再レビュー開始時の current HEAD: `9ca4953f35def07cc8af43835f2eb30c24387c6a`
- 前回再レビュー報告 commit: `a3e5bab1246350e80b6d5eff8e12d0438daefd2c`
- 前回残件: `I29-004A-RR-001`
- 表記レビュー対象: `Tracker/Design/Testing/aspire-004a-workflow.md` と `Tracker/Design/tasks-status.md` の PR #29 追加部分
- `reports/` は表記レビュー対象外

重点確認項目は、行内コードや引用符による lint 回避、一般的でない日本語、一般語の過度な英語表記、技術的意味の保持である。

## 結論

阻害指摘なし。

前回残件 `I29-004A-RR-001` は解消を確認した。新しい阻害指摘はない。

## 前回残件の確認

### `Docker image` の行内コード回避

解消。

`Tracker/Design/Testing/aspire-004a-workflow.md` の通常文から `Docker image` の表現が削除されている。

現在は次のように記述されている。

- `game-controller` を `Aspire AppHost` から Docker で起動する資源として追加する。
- 検査対象は `ContainerImageAnnotation.Image` と `ContainerImageAnnotation.Tag` の固定値として明示する。

`ContainerImageAnnotation.Image` / `Tag` は実装上のプロパティ名であり、行内コードの利用目的が明確である。一般語を lint から逃がす形ではない。

### bare `sidecar` の行内コード回避

解消。

`Tracker/Design/tasks-status.md` の PR #29 追加行は、bare `sidecar` を使わず、許可一覧に既存登録済みの alias `tracker snapshot alignment sidecar` を通常文として使用している。

許可一覧では `alignment sidecar` の alias として `tracker snapshot alignment sidecar` が登録されているため、コード表記による除外に依存していない。

### 「専用の操作接続」

解消。

現在は次の具体的な作業分離条件になっている。

- 専用 Git `worktree` を使う。
- `RemoteDesktopMCP` の `session_id` をこの作業専用に1つ確保する。
- 並行する `ASPIRE-003A/B` と `worktree` / `session_id` を共有しない。

`RemoteDesktopMCP` は実際のツール名、`session_id` は実際のフィールド名であり、一般語をコード表記で逃がしたものではない。作業分離の条件も前回より具体的になった。

## 表記レビュー

今回変更された設計本文を全文確認した。

- 不要な日本語引用符は確認できない。
- 一般語をバッククォートで囲って lint 対象外にする新しい箇所は確認できない。
- 以前のような `固定版`、`有効な命令`、`通信接続層` といった過剰な一般語化は解消されている。
- `ContainerImageAnnotation.Image` / `Tag`、`IRefereePacketReceiver`、`IGameControllerWebSocketTransport`、`UdpRefereeCommandSource` などは実装上の識別子として使用されている。
- `NORMAL_START` / `FORCE_START` と試合進行状態の関係が明示されており、active command の意味は保持されている。
- Git `worktree`、`HEAD SHA`、`head_sha`、`git merge` の運用条件は具体性を保っている。

## 固定版 Markdown lint

PR #30 HEAD `eeac918f2601f6e8c985c388e2915ec903bbd11f` と、その CI が固定する CodexSkill commit `583a9594d8157fc101dff3c6b3338238809d9779` を隔離作業ツリーへ展開して独立再現した。

`Tracker/Design/Testing/aspire-004a-workflow.md`:

- textlint: 成功
- CSpell: 成功、指摘 0
- 厳格許可一覧検査: 成功

`Tracker/Design/tasks-status.md` の PR #29 追加5行:

- textlint: 成功
- CSpell: 成功、指摘 0
- 厳格許可一覧検査: 成功

許可一覧は前回再レビュー後に変更されていない。

## 差分検査

次はいずれも成功した。

- PR 全差分の `git diff --check`
- 前回再レビュー後差分の `git diff --check`

## テスト

current HEAD で次を再実行した。

- `AppHostApplicationModelTests`
- `RefereeDriverFixtureTests`
- `RefereeDriverNetworkAdapterTests`

結果: 14 / 14 成功。

前回再レビュー後の製品実装変更はなく、今回の実変更は設計文書と報告のみである。

## CI

再レビュー開始時の current HEAD は `9ca4953f35def07cc8af43835f2eb30c24387c6a`。

この SHA と完全一致する `.NET tests` run `37133922230` は success。別 SHA の run は判定に使用していない。

この報告を push すると current HEAD が更新されるため、最終 CI は更新後 HEAD と完全一致する run だけを使用する。

## 判定

`I29-004A-RR-001`: 解消。

新規 findings: なし。

merge は行わない。

