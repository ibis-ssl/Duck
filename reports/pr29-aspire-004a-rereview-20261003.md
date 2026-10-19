# PR #29 ASPIRE-004A 再レビュー

## 対象

- 親設計: PR #28
- 実装 PR: #29
- 再レビュー開始時の PR #29 current HEAD: `0ba2f46c80e4d1bbe3664f20e4c50c28039598e5`
- 前回レビュー報告 commit: `4142e0fe038d0436b059aaaf0c6f0c609eec6189`
- 前回指摘: `I29-004A-REV-001`〜`003`
- レポート本文は表記レビュー対象外とし、設計文書と検証証跡を確認した。

重点確認項目は、過剰な引用符、一般的でない日本語、一般語の過度な英語表記、修正による技術的意味の変化である。

## 結論

受入れは引き続き保留する。

前回指摘のうち、不要な引用符、`git diff --check`、Markdown lint 成功証跡は解消した。一方、表記修正で技術用語を一般語へ置き換えすぎたため、設計文書の読みやすさと技術的意味に新しい阻害指摘が1件ある。

## 前回指摘の確認

### I29-004A-REV-001

一部解消、一部未解消。

不要な日本語引用符は削除されており、引用符による lint 回避候補は確認できなかった。

一般語の英語表記も大幅に整理されている。ただし、修正の一部は技術用語まで一般語へ置き換えており、意味が曖昧または不自然になっている。詳細は `I29-004A-RR-001` とする。

### I29-004A-REV-002

解消。

次を再実行し、いずれも終了値 0 を確認した。

- PR 全差分の `git diff --check`
- 前回レビュー後差分の `git diff --check`

`Tracker/Tracker.Tests/RefereeDriverFixtureTests.cs` の末尾余分空行は削除されている。

### I29-004A-REV-003

解消。

PR #30 HEAD `eeac918f2601f6e8c985c388e2915ec903bbd11f` と、その Markdown lint CI が固定する CodexSkill commit `583a9594d8157fc101dff3c6b3338238809d9779` を隔離作業ツリーへ展開して独立再現した。

`Tracker/Design/Testing/aspire-004a-workflow.md`:

- textlint: 成功
- CSpell: 成功、指摘 0
- 厳格許可一覧検査: 成功

`Tracker/Design/tasks-status.md` の PR #29 追加5行:

- textlint: 成功
- CSpell: 成功、指摘 0
- 厳格許可一覧検査: 成功

許可一覧の変更は確認されていない。

## 新規指摘

### I29-004A-RR-001: 技術用語の過剰な日本語化で意味と読みやすさが低下している

重要度: 阻害

前回の一般語英語表記を直す方向は正しいが、現在の本文には、一般的な技術用語まで曖昧な一般語へ置き換えた箇所が残る。lint が成功しても、人が読む設計書としては受入れできない。

代表例:

1. `Tracker/Design/Testing/aspire-004a-workflow.md` 11行目
   - 現在: `Docker 実行画像資源`
   - 問題: 「実行画像」は Docker 文脈で一般的な表現ではなく、`Docker image` を無理に直訳した表現に見える。
   - 例: 「Docker イメージ資源」のように、一般的な技術用語を使う方が自然である。

2. 同 12、40、50行目
   - 現在: `固定版`
   - 問題: この契約は単なる版固定ではなく、`ContainerImageAnnotation.Tag` と実装値 `3.20.3` を固定タグとして検査している。正本の設計も `fixed tag or digest`、`image tag` と区別している。
   - 「固定版」では tag / digest / version の区別が失われる。
   - 「固定タグ」など、実際に検査する属性を明示する必要がある。

3. 同 14、60、61行目、および `Tracker/Design/tasks-status.md` 45行目
   - 現在: `有効な命令`
   - 問題: 実装の `IsActive` は `NORMAL_START` / `FORCE_START` を「試合が進行する active command」として判定する。`有効な命令` は「妥当な命令」とも読め、active 状態の意味を保持していない。
   - 「試合進行中を示すコマンド」など、状態の意味が分かる日本語にするか、`NORMAL_START` / `FORCE_START` を明示する必要がある。

4. 同 20行目
   - 現在: `専用作業領域`
   - 元の契約: 専用 `worktree`
   - 問題: Git worktree を使うという具体的な分離要件が、単なるフォルダーや作業場所にも読める一般語へ変わっている。
   - 「専用の Git worktree」など、Git の機能名を保持する必要がある。

5. 同 24、35行目
   - 現在: `既存の自動処理`、`現在の HEAD のコミット識別子`
   - 問題: 対象は具体的に GitHub Actions workflow と `HEAD SHA` であり、一般語にすると運用条件が読みにくい。
   - 「既存ワークフロー」「現在の HEAD SHA」の方が自然かつ正確である。

6. 同 66行目
   - 現在: `通信接続層`
   - 元の契約: network adapter 境界
   - 問題: 「層」はアーキテクチャ上の layer を示す表現で、adapter 境界とは異なる意味を持ち得る。実装は `IRefereePacketReceiver`、`UdpRefereeCommandSource`、`IGameControllerWebSocketTransport` などの adapter / transport 境界を分離している。
   - 「通信アダプター境界」など、実装構造と対応する表現が適切である。

7. 同 75行目
   - 現在: `取り込みは行わない`
   - 問題: Git では merge、rebase、cherry-pick など複数の取り込み操作がある。利用者指示は merge を行わないことであり、一般語化により範囲が曖昧になっている。
   - 「マージは行わない」と書く方が正確である。

同様に、`tasks-status.md` 46行目の `補助記録` は、このプロジェクトで使っている sidecar という具体的な記録形式を一般語へ置き換えており、技術的な参照関係が弱くなっている。

対応条件:

- 一般語の不要な英語表記を戻すのではなく、正式名称・一般的な技術用語・実装上の概念は自然なカタカナまたは技術用語として保持する。
- `tag`、`worktree`、`HEAD SHA`、adapter、merge など、一般語化すると意味が変わる用語は具体性を保つ。
- `active command` は状態の意味を保持する日本語へ直す。
- 修正後に同じ固定版 lint を再実行する。

## 実装・テスト確認

前回レビュー後の C# 変更は `RefereeDriverFixtureTests.cs` 末尾空行の削除だけで、実装ロジックの変更はない。

current HEAD で対象テストを再実行した。

- `AppHostApplicationModelTests`
- `RefereeDriverFixtureTests`
- `RefereeDriverNetworkAdapterTests`

結果: 14 / 14 成功。

## CI

再レビュー開始時の current HEAD は `0ba2f46c80e4d1bbe3664f20e4c50c28039598e5`。

この SHA と完全一致する `.NET tests` run `37100645149` は success。別 SHA の run は判定に使用していない。

この再レビュー報告を push すると HEAD が更新されるため、最終確認では新しい current HEAD と完全一致する run だけを使用する。

## 受入れ条件

- `I29-004A-RR-001` の過剰な日本語化を修正し、読みやすさと技術的意味を両立させる。
- 修正後の設計文書に対して固定版 lint を再実行する。
- `git diff --check` を再確認する。
- 対象14テストを再確認する。
- 更新後 current HEAD と完全一致する CI run だけを最終判定に使う。

merge は行わない。
