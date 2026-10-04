# PR #29 ASPIRE-004A 再レビュー指摘対応報告

## 対象

PR #29 の再レビューで追加された `I29-004A-RR-001` に対応した。

修正コミット: `57f7e61`

## 修正内容

`Tracker/Design/Testing/aspire-004a-workflow.md` と `Tracker/Design/tasks-status.md` の ASPIRE-004A 記述を、lint のための過剰な一般語化を避け、実装・Git 運用と対応する表現へ修正した。

主な修正は次のとおり。

- `Docker 実行画像資源` は `Docker image` 資源とし、Docker の技術用語であることを明確にした。
- `固定版` は `ContainerImageAnnotation.Tag` の固定値として、実際に検査している属性を明示した。
- `有効な命令` は、試合進行中を示す `NORMAL_START` / `FORCE_START` への遷移として具体化した。
- `専用作業領域` は専用の Git `worktree` とし、分離要件を復元した。
- `既存の自動処理` と `コミット識別子` は、`GitHub Actions` 定義と `HEAD SHA` / `head_sha` に戻した。
- `通信接続層` は `IRefereePacketReceiver` / `IGameControllerWebSocketTransport` の境界として、実装上の抽象化境界を明示した。
- `取り込みは行わない` は `git merge` を行わないという利用者指定に合わせた。
- `tasks-status.md` の回帰記録は `tracker-snapshot-alignment.jsonl` の `sidecar` と明示した。
- 11003 UDP 受信部分は `UdpRefereeCommandSource` を明示し、一般的な「アダプター」だけに依存しない記述にした。

許可一覧 `tools/lint/markdown-whitelist.yaml` は変更していない。

## Markdown lint

PR #30 の検査器と、その CI が固定する CodexSkill commit `583a9594d8157fc101dff3c6b3338238809d9779` を隔離 worktree で使用した。

`Tracker/Design/Testing/aspire-004a-workflow.md` は次の3検査すべて成功した。

- textlint
- CSpell: 指摘 0
- 厳格許可一覧検査: 違反 0

`Tracker/Design/tasks-status.md` は PR #28 ベース側の既存違反と分離するため、PR #29 が追加した5行を UTF-8 Markdown として差分抽出し、同じ3検査を実行した。追加5行もすべて成功し、CSpell 指摘 0、厳格許可一覧違反 0。

## 差分検査

次の両方が終了値 0。

```text
git diff --check
git diff design/issue18-aspire-test-orchestration --check
```

再レビュー報告 `reports/pr29-aspire-004a-rereview-20261003.md` にも末尾余分空行があったため、PR 全差分の検査を通す目的で削除した。

## 対象テスト

.NET SDK 10.0.401 を使用して個別実行した。

- `AppHostApplicationModelTests`: 5 / 5 成功
- `RefereeDriverFixtureTests`: 5 / 5 成功
- `RefereeDriverNetworkAdapterTests`: 4 / 4 成功

合計 14 / 14 成功。

## CI

この報告を push 後、PR #29 の current HEAD と `headSha` が完全一致する `.NET tests` run だけを最終 CI 判定対象とする。別 SHA の run は代用しない。

merge は行わない。
