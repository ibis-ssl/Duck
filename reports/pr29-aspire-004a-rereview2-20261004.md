# PR #29 ASPIRE-004A 2回目再レビュー

## 対象

- 親設計: PR #28
- 実装 PR: #29
- 再レビュー開始時の current HEAD: `85c4711f36882729c622a5fe2057995f1366156e`
- 前回再レビュー報告 commit: `3f3b0af40e965e04534684f01e4a8c64b9abec17`
- 前回阻害: `I29-004A-RR-001`
- 表記レビュー対象: 設計文書と台帳の PR #29 追加部分
- `reports/` は表記レビュー対象外

重点確認項目は、一般語の過度な英語表記、コード表記や引用符による lint 回避、一般的でない日本語、技術的意味の保持である。

## 結論

受入れは引き続き保留する。

前回指摘で挙げた `fixed tag`、Git `worktree`、`HEAD SHA`、active command、通信境界、merge の具体性は改善している。一方、一般語の英語表記を行内コードへ入れて lint 対象外にする箇所が残っており、リポジトリ自身の lint 方針に反することを独立再現で確認した。

`I29-004A-RR-001` は完全解消とはしない。

## 指摘

### I29-004A-RR-001: 未解消 - 行内コードによる lint 回避が残る

重要度: 阻害

`tools/lint/README.md` は、行内コードの除外を本物の識別子、コマンド、ファイルパス、画面表示名、明示的な項目名に限定し、通常文中の英単語や片仮名語を検査から逃がすためにコード表記や引用符で囲むことを明示的に禁止している。

現在の文書には、その禁止事項に該当する箇所が残っている。

#### 1. `Docker image`

`Tracker/Design/Testing/aspire-004a-workflow.md`:

- 11行目: `Aspire AppHost` の `Docker image` 資源
- 40行目: `Docker image` 名

`Docker image` は型名、CLI 引数、ファイル名、画面表示名ではなく、通常文中の技術用語である。行内コードにする理由はない。

PR #30 HEAD `eeac918f2601f6e8c985c388e2915ec903bbd11f` と CodexSkill `583a9594d8157fc101dff3c6b3338238809d9779` の固定 lint 環境を隔離作業ツリーへ展開して確認した。

current 文書のままでは textlint / CSpell / 厳格許可一覧検査が成功する。

隔離コピーで `Docker image` のバッククォートだけを外すと、本文は一切変えずに次が検出された。

- CSpell:
  - 11行目 `Unknown word (image)`
  - 40行目 `Unknown word (image)`
- 厳格許可一覧:
  - 11行目 `image` 未許可
  - 40行目 `image` 未許可

したがって current の成功は、`image` を行内コードに入れて検査対象外にしたことに依存している。

読みやすさを優先するなら「Docker イメージ」のような一般的な日本語技術表記が自然である。厳格許可一覧への追加が必要なら、利用者の明示承認を得て許可語として扱うべきであり、コード表記で回避してはならない。

#### 2. bare `sidecar`

`Tracker/Design/tasks-status.md` の PR #29 追加行では、`tracker-snapshot-alignment.jsonl` の後に bare `sidecar` を行内コードとして記載している。

`sidecar` はここでは識別子ではなく記録形式を説明する通常文中の用語である。

同じ固定 lint 環境の隔離コピーで `sidecar` のバッククォートだけを外すと、次が検出された。

- CSpell: `Unknown word (sidecar)`
- 厳格許可一覧: bare `sidecar` 未許可

許可一覧には `snapshot sidecar`、`alignment sidecar`、`diagnostics sample sidecar` などの具体的な設計語はあるが、bare `sidecar` は登録されていない。

ここも current の lint 成功が行内コード除外に依存している。

既存の許可済み設計語を正確に使うか、`tracker-snapshot-alignment.jsonl` が何の補助ファイルか日本語で明示するべきである。bare `sidecar` をコード表記で通すべきではない。

#### 3. 「専用の操作接続」

`Tracker/Design/Testing/aspire-004a-workflow.md` 20行目の「専用の操作接続」は日本語として不自然で、何を専用化する契約なのか分かりにくい。

元の作業条件は、専用 Git worktree と専用のリモートデスクトップ作業セッションを分離することである。Git `worktree` は復元されたが、接続側は「操作接続」と一般化されている。

「専用のリモートデスクトップセッション」または実際に使う仕組みに合わせた明確な名称とし、作業分離の条件を読者が判断できる表現にする必要がある。

## 改善を確認した点

前回指摘のうち次は改善を確認した。

- `ContainerImageAnnotation.Tag` を明示し、tag / version / digest の意味を混同しなくなった。
- active command を `NORMAL_START` / `FORCE_START` と試合進行状態の関係で説明している。
- Git `worktree` を具体的に記載している。
- current HEAD と `head_sha` の一致条件を明示している。
- 通信境界を `IRefereePacketReceiver` / `IGameControllerWebSocketTransport` という実装上の識別子へ対応させている。
- merge 禁止を `git merge` と具体化している。
- `UdpRefereeCommandSource` を台帳へ具体的に記録している。
- 不要な日本語引用符は確認できない。

## 機械検査

固定版 lint を current 文書に対して独立再実行した。

`Tracker/Design/Testing/aspire-004a-workflow.md`:

- textlint: 成功
- CSpell: 成功、指摘 0
- 厳格許可一覧: 成功

`Tracker/Design/tasks-status.md` の PR #29 追加5行:

- textlint: 成功
- CSpell: 成功、指摘 0
- 厳格許可一覧: 成功

ただし、上記の `Docker image` と bare `sidecar` は行内コード除外によって成功しているため、人レビュー上の合格根拠にはしない。

許可一覧は前回再レビュー後に変更されていない。

## 差分検査

次はいずれも成功した。

- PR 全差分の `git diff --check`
- 前回再レビュー後差分の `git diff --check`

## テスト

current HEAD で次の対象テストを再実行した。

- `AppHostApplicationModelTests`
- `RefereeDriverFixtureTests`
- `RefereeDriverNetworkAdapterTests`

結果: 14 / 14 成功。

前回再レビュー後の製品実装変更はなく、今回の変更は設計文書と報告だけである。

## CI

再レビュー開始時の current HEAD は `85c4711f36882729c622a5fe2057995f1366156e`。

この SHA と完全一致する `.NET tests` run `37131520737` は success。別 SHA の run は判定に使用していない。

この報告を push すると current HEAD が更新されるため、最終 CI は更新後 HEAD と完全一致する run だけを使用する。

## 受入れ条件

- `Docker image` を通常文中の自然な表記へ直し、コード表記による lint 回避をなくす。
- bare `sidecar` をコード表記で回避せず、既存の具体的な設計語または意味が明確な日本語へ直す。
- 「専用の操作接続」を、実際のセッション分離条件が分かる自然な表現へ直す。
- 必要な許可一覧変更がある場合は、利用者の明示承認を得る。レビュー担当・実装担当が無断で追加しない。
- 修正後に固定版 lint、`git diff --check`、対象14テストを再確認する。
- 更新後 current HEAD と完全一致する CI run だけを最終判定に使う。

merge は行わない。
