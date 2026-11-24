# Issue #158 独立最終レビュー報告

## レビューと証明の対象

- リポジトリ: `ibis-ssl/Duck`
- PR: [#159](https://github.com/ibis-ssl/Duck/pull/159)（レビュー時点で Open / Draft）
- ブランチ: `issue-158-startup-submodule-preflight`
- base: `main` / `f2e8d3e6f3de2edca55013cf0158abdac73193cc`
- **reviewed_implementation_head:** `3832d8527b6ce0ff944392836ae21ab87a757fb3`
- 同じ独立レビュワーによる独立レビューの証拠: [レビュー会話](https://chatgpt.com/c/6b057946-802c-83ee-823a-1778fbfae0be)。このレビューは実装者および通常レビュワーと独立して実施されたものとして、review-enforcer が保持するレビューライフサイクル記録に基づきます。本報告はその判定を記録し、新たなレビュー判定を行うものではありません。
- **判定: PASS。** 技術判定は上記 reviewed implementation HEAD に限ります。

## レビュー履歴

| 対象 | 判定 | 内容 |
| --- | --- | --- |
| `0585b095505a124313bdb6ec43df976293541437` | 技術 FAIL | 初回レビューで D158-IFR-01〜04 を指摘。独立レビュー報告の予約にも不備があり、この結果は attestation として扱いません。 |
| `f4d9ef6d35f58c1a74ad0326c08595544fa685d7` | 未合格 | 同じ独立レビュワーの閉鎖確認で IFR-01 / IFR-03 の解消を確認。IFR-02 / IFR-04 が残りました。 |
| `3832d8527b6ce0ff944392836ae21ab87a757fb3` | **技術・正式 PASS** | 同じ独立レビュワーが IFR-02 / IFR-04 の解消を確認。全必須指摘が閉じました。 |

予約はこの独立レビューライフサイクルの既存予約を再利用しました。

- `reservation_identity`: `duck158-pr159-ifr-attestation-20261124`
- `reservation_owner`: `review-enforcer`
- 元の予約時刻: `2026-11-24T14:56:43.658Z`
- レビュー中の状態: `metadata_only`
- `report_attestation_allowed`: `true`
- 予約済みパス: `reports/issue-158-independent-final-review-20261124.md`

## 指摘と処置

重要度は元の独立レビューに記録された値を維持し、再分類していません。

| 指摘・重要度 | 起点、影響、必要な処置 | 実装、実 fixture、判定 |
| --- | --- | --- |
| **D158-IFR-01 — medium** | 配置済み submodule の追跡ファイルにローカル変更があると、Git の `assume-unchanged` / `skip-worktree` index flag により通常の status 検査から隠れ、準備処理で既存データを損なうおそれがありました。配置前の検査で flag を検出し、診断して停止し、既存変更を保持して他方の未配置 submodule にも進まないことが必要でした。 | `scripts/start-duck-simulation.cs` の `EnsurePreparedProtoCheckoutCleanAsync` は `git ls-files -v -z` の lowercase flag と `S` flag を停止条件にし、共通診断は module、期待 SHA、実際の状態、安全な次の手順を示します。`AssumeUnchangedTrackedFileStopsBeforeEitherPlacement` は README の変更を作り `assume-unchanged` に設定、通常 status が空でも検出されること、変更内容が保持されること、もう一方が未配置であることを確認します。専用 fixture は assume-unchanged を直接扱います。skip-worktree は同じ判定経路に含まれますが、独立した skip-worktree fixture はこの報告で確認していません。**PASS** |
| **D158-IFR-02 — medium** | MSBuild の item 名は大小文字を区別しません。従来の静的検査が大小文字の異なる `protobuf Remove` を見落とすと、レビュー済み固定入力が実効 item から除外されても準備処理がそのまま進む可能性がありました。宣言検査と固定入力列挙の比較を MSBuild の項目名規則に揃え、危険な Remove は両配置前に拒否する必要がありました。 | `IsProtobufItem` の `OrdinalIgnoreCase` 比較を `VerifyReviewedProtoDeclarations` の宣言走査、固定入力列挙、item metadata の親確認で共有します。`CaseInsensitiveProtobufRemoveStopsBeforeEitherPlacement` は実準備入口へ `<protobuf Remove="src/external/ssl-game-controller/proto/**/*.proto" />` を加え、両 submodule が未配置のまま停止することを確認します。`CaseInsensitiveProtobufDeclarationsAreEnumerated` は固定宣言を小文字の `protobuf` に置き換え、通常の準備と両入力の検査が完了する正例です。実装担当は最小 SDK-style project の `MSBuild.dll -getItem:Protobuf` 評価で大小文字違いの Remove 後に `Protobuf` が空になることも確認しました。**PASS** |
| **D158-IFR-03 — medium** | 固定 object store、保存 index、必須 materialized Proto 入力の欠落・読取不能・不一致時に、単なる例外ではなく対象 module、期待 SHA、確認できたローカル状態、安全な次の準備手順を示し、既存ファイルと他方の未配置状態を保つことが必要でした。 | `LocalProtoAssetFailure` が共通の具体的診断を形成します。`UnreadableSavedIndexUsesLocalProtoAssetDiagnosticBeforePlacement` は不正な index を作り診断・index 保持・両未配置を確認し、`UnreadableMaterializedProtoUsesLocalProtoAssetDiagnosticBeforeOtherPlacement` は必須 Proto の読取失敗を作り、他方が未配置であることを確認します。`MissingMaterializedProtoInputIsReportedByReadOnlyCheck` と `MaterializedProtoContentMustMatchPinnedBlobBeforeStatusCheck` も対象入力の欠落・内容不一致を読み取り検査で報告し、fixture の状態を保ちます。**PASS** |
| **D158-IFR-04 — low** | 欠落試験とピン留め blob との内容不一致試験の XML 要約が汎用文で、失敗条件と保護する状態を区別できませんでした。それぞれの試験が何を検出し何を保持するか、日本語の要約で明確にする必要がありました。 | `MissingMaterializedProtoInputIsReportedByReadOnlyCheck` は必須入力の欠落診断と既存 checkout / 他方の未配置を説明します。隣接する `MaterializedProtoContentMustMatchPinnedBlobBeforeStatusCheck` はピン留め内容との不一致診断とローカル変更 / 他方の未配置を説明します。**PASS** |

この実装・fixture は `scripts/start-duck-simulation.cs` と `Tracker/Tracker.Tests/LauncherProcessContractTests.cs` にあります。通常レビュー記録は[既存の指摘対応レポート](https://github.com/ibis-ssl/Duck/blob/3832d8527b6ce0ff944392836ae21ab87a757fb3/reports/issue-158-independent-review-fixes-20261124.md)と[通常レビュー合格コメント](https://github.com/ibis-ssl/Duck/pull/159#issuecomment-7135859864)にあります。

## 検証、未確認範囲

- reviewed implementation HEAD `3832d8527b6ce0ff944392836ae21ab87a757fb3` の [.NET CI run 45425535357](https://github.com/ibis-ssl/Duck/actions/runs/45425535357/job/138556364610#step:7:1) は `Tracker.Tests` **1,595成功・4スキップ・0失敗**。同一 run の [ASPIRE-NET-002 Linux packet flow](https://github.com/ibis-ssl/Duck/actions/runs/45425535357/job/138556367617) と [ASPIRE full-stack Linux acceptance](https://github.com/ibis-ssl/Duck/actions/runs/45425535357/job/138558625623) はスキップされました。
- 実装版で局所焦点試験4件成功。独立レビュワーはその実装・試験差分と検証記録を確認しました。最小 SDK 評価も実装担当の検証証拠です。この報告作成中の再試験はしていません。
- 標準ローカル全体テストは runtime pack `Microsoft.NETCore.App.Runtime.linux-x64` 不足による `NETSDK1112` で実行できていません。これはローカル合格として扱いません。Windows の最新修正範囲、Docker、通信・実機受入も未確認です。
- 過去の UDP bind 失敗は再現しておらず、原因は未確定です。
- CI のスキップ項目、Windows、Docker、通信・実機は本レビューで追加実行していないため未確認のままです。新たな実装変更を要求する未解決 finding はありません。

## 証明と境界

本報告は **reviewed implementation HEAD `3832d8527b6ce0ff944392836ae21ab87a757fb3` に対する技術判定 PASS** を記録します。`reports/issue-158-independent-final-review-20261124.md` だけを変更する、reviewed implementation HEAD を第一親とした**一度限りの事務的 report-attestation commit** を目的とします。attestation commit は実装の一部としてレビューされたものではありません。証明 SHA は commit 後に親がブランチ外で記録します。

attestation commit の許可差分は予約済みの上記 report path だけです。他のファイルを同じ commit に含めません。後続の Git commit が加わった場合、この attestation による完了扱いは無効となり、新しい review lifecycle が必要です。最終 PR CI 状態とマージ判断は未確定で、merge を許可する報告ではありません。
