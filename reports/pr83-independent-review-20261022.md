# Duck PR #83 独立レビュー（2026-10-22）

対象 HEAD: `ba2b1910b878a0afb493bab8f969257b21e4590d`
base: `c72939ec076ec3261357ba16e61fc5140de2b3d4`
判定: **要修正（P2 2件）、実機受入保留**。Draft PR、merge は行わない。

## 指摘

**P2-1 配置・起動図の不整合**
`Tracker/Design/Testing/aspire-simulation-test-environment.md:55,95,175`。
本文では RefereeDriver を Windows 側の `ProjectResource` とするが、配置図では Docker Desktop のブリッジ内にある。また起動図では Desktop が Duck を起動するが、Duck はホスト側 `ProjectResource`。配置と起動主体を一貫させること。

**P2-2 PR本文の対象範囲説明**
PR 本文末尾は「今回は設計文書だけを変更」「コード変更なし」とするが、PR全体には `DockerContainerWrapper.cs` と `DockerContainerWrapperLifecycleTests.cs` の変更が存在する。最終コミット限定の説明であればその旨を明示し、PR全体のコード変更範囲も明示すること。

**P3（注意） skills リンクの移植性**
`.agents/skills` は `../../CodexSkill/skills` へのシンボリックリンク（Git mode 120000）で、Duck単独checkoutでは参照先がない。CodexSkillを隣接配置する前提を明記すること。既知の専用workspaceでは意図的と判断し、低優先度とする。

## 実装の確認

Docker wrapper は発見した短縮IDを inspect で完全64桁IDに展開し、ID・コンテナ名・画像・各所有ラベル・ネットワークモードを検証する。停止・削除前にも完全IDと所有条件を再検証する。短縮ID、所有者不一致、inspect失敗、停止後の所有者変化に対応するテストを確認。静的確認の範囲で新たなコード欠陥は確認できなかった。

## 検証

- HEAD一致CI: [run 39712001396](https://github.com/ibis-ssl/Duck/actions/runs/39712001396) の .NET tests **476/476成功、失敗0**。Linux packet-flow、full-stack acceptance は skipped。
- workflowには失敗時TRX・stdout/stderr・vstest診断・binlog・環境情報のartifactアップロードがある。成功した今回のrunでは失敗artifactなし。
- `git diff --check` 成功。レビュー用worktreeはHEAD固定のまま。
- 現行設計書へYomiyasuを再実行し **100/100、is_clean=true、指摘0、exit 0**。textlint・CSpell・Sudachiは今回の独立再実行対象外。
- Windows Aspire/DCP・Docker DesktopのUDP/マルチキャスト・Web UI/API・localhost限定待受・数値IP問題・停止の実機試験は未実施。CI成功を実機受入とみなさない。

P2修正後に再レビューし、未検証の受入条件は保留を維持する。
