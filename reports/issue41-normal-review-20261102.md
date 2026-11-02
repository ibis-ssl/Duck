# Issue #41 通常レビュー報告

## 対象と範囲

- Issue: https://github.com/ibis-ssl/Duck/issues/41
- PR: https://github.com/ibis-ssl/Duck/pull/104（Draft、base `design/issue18-aspire-test-orchestration`）
- レビュー対象HEAD: `14bfc271d5b8ce9cca094ad353ccda1afa3cbc1e`
- 要件: 1 UI render tickで固定した同じsnapshot pairをSplit / Overlayと数値差分が共有し、時刻差を別表示する。
- 対象範囲: `Home.razor`, `VisionLiveComparisonViewState`, `VisionLiveDisplaySnapshotProvider`, `TrackerDifferenceSnapshotEvaluator`、`VisionLiveComparisonViewStateTests`、`RuntimeHostDebugHostReadSideSnapshotBoundaryTests`とこのPRの報告文書。
- 製品コードのPR差分はない。PR差分はIssue #41の計画・検証・レビュー記録に限る。

## レビュー結果

初回レビューではR1（P2、報告出力条件）を検出した。`reports/issue41-resumption-plan-20261102.md` にUTF-8 BOMがなく、報告ファイルの指定形式を満たさなかった。BOMを追加し、最初の非昇格実行がテスト開始前のビルド中に停止したという記録へ直した。通常レビュー担当が修正後HEADを再確認し、R1の解消と修正後の記録を確認した。続く軽微な表現指摘も「検証時点のPR HEAD」へ直し、同じ担当の再確認で残指摘なし・passとなった。

製品経路では実装不足を確認しなかった。`Home.razor`は一度採取した`liveDisplaySnapshot`を表示レイヤーと比較view stateに渡し、Split / Overlayのlayerと数値差分を同じsnapshotから構成する。時刻差は物体差分表と別欄にある。providerはraw/tracked/external各storeを一度ずつ読み、同じrender snapshotからview stateを作る。view stateはfrozen snapshot内でsourceを解決する。`TrackerDifferenceSnapshotEvaluator`はHomeの選択ペア経路では使われないが、Home側で同じ固定pairから数値差分を計算しており、このIssue条件の欠落ではない。

## 対象経路とテスト範囲

| 対象 | 確認した内容 |
| --- | --- |
| `Home.razor` 73-80, 250-267, 341-357, 696-716 | 同じlive display snapshotから比較状態とlayerを構成。時刻差を別表示。モード・選択変更でもcapture済みsnapshotを再利用。 |
| `VisionLiveDisplaySnapshotProvider.cs` 36-87 | 一回のcapture、render tick識別、同じsnapshotからview state作成。 |
| `VisionLiveComparisonViewState.cs` 262-346, 441-568 | 同じfrozen snapshotでA/Bを解決し、物体差分・時刻差・Split / Overlay layerを構成。 |
| `VisionLiveComparisonViewStateTests.cs` 1030-1182 | frame/timestamps/difference/timestamp delta、render tick、Split / Overlayでの同じpair、store更新後もfrozen pairを保持。 |
| `RuntimeHostDebugHostReadSideSnapshotBoundaryTests.cs` 47-83, 153-216 | providerからview stateへのsnapshot境界と、capture後の各store更新後も捕捉済み値を保つこと。 |

## 検証

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~VisionLiveComparisonViewStateTests|FullyQualifiedName~RuntimeHostDebugHostReadSideSnapshotBoundaryTests" -m:1 /nr:false` はレビュー対象HEAD `14bfc271d5b8ce9cca094ad353ccda1afa3cbc1e` で21/21成功。
- 同じfocused testはコード同一の`08c65a923626ff42a9c1ca1eb1bbb866a973dd4d`で通常build付きでも21/21成功。初回sandbox実行はMSB3491でテスト開始前のbuild中に止まり、許可済みWindows PowerShell経路で再実行成功した。stdout/stderrはローカル `reports/evidence/issue41-focused-test-20261102/` に保存。
- 報告Markdownのtextlintとspell lint、`git diff --check`は成功。
- GitHub run [41532892502](https://github.com/ibis-ssl/Duck/actions/runs/41532892502) はレビュー対象HEADで確認時点`in_progress`。`ASPIRE-NET-002 Linux packet flow`はskip。成功済みとは扱わない。
- 統合試験・実入力でのUI確認は未実施。統合試験失敗はなく、Issue記載のAspire/コンテナ診断ログ保存条件は発生していない。

## 指摘と処置

| ID | 重大度 | 内容 | 処置 |
| --- | --- | --- | --- |
| R1 | P2 | 日本語報告ファイルのUTF-8 BOMが欠落。 | BOMを追加して再レビューpass。 |
| R1に伴う記録修正 | 軽微 | buildの失敗位置をテスト開始前のビルド中と明記し、SHA表現を検証時点のHEADに限定。 | 修正済み、再レビューpass。 |

## プロファイルと判定

- task_kind: review; mode: normal review / same-reviewer fix verification
- requested profile: `gpt-6.1-sol`, reasoning `high`
- applied: `null`; application_status: `spawn_succeeded_profile_unverified`（最終runtime metadataは親から不可視）
- decomposability: `independent_workstreams`; decomposition_policy: `forbidden`; parallelism_mode: `single_agent`
- final verdict at `14bfc271d5b8ce9cca094ad353ccda1afa3cbc1e`: **pass**, 必須の残指摘なし。
- 独立レビューは別担当による後続工程。マージ・Approveは未実施。

## 残件

Issueの指定条件に対するコード上の不足はなし。GitHub CIは未完了のため成功扱いにしない。実入力の画面確認と統合試験は未実施のまま明示する。新しい受入条件やSub-issueは追加しない。