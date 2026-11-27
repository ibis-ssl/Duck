# Issue #80 独立最終レビュー報告

## 判定

独立最終レビューは `pass_with_held`。必須指摘 I80-IFR-001 は修正確認により閉じた。技術判定の対象は設計・許可語の変更を含む実装 HEAD `66f0e30330a144ce195fd757f0349790de9062f3`。この報告のコミットは管理用の attestation であり、レビュー対象 HEAD を変更しない。

## 対象と予約

- リポジトリ: ibis-ssl/Duck
- Issue / PR: #80 / #83
- ブランチ: `task/aspire-docker-desktop-496`
- base: `design/issue18-aspire-test-orchestration`、`c72939ec076ec3261357ba16e61fc5140de2b3d4`
- タスク差分の基点: `074be4a8aa0ece890759fa49095ffc2a2ad0dec4`
- 初回独立レビュー HEAD: `f58ab156138f20c9255b233f274117e390894e08`
- 限定 closure HEAD / `reviewed_implementation_head`: `66f0e30330a144ce195fd757f0349790de9062f3`
- 技術変更 HEAD: `87ffca2a577f0a53b8856a9dd81ec5d56983695b`
- 予約パス: `reports/issue-80-design-independent-final-review-20261022083551.md`
- 予約所有者: review-enforcer
- 予約ID: `c4c21282-7c99-459c-a829-199cae73d9ef`
- 予約状態: `metadata_only`。レビュー合格後に初めてこのファイルを作成する。
- 保存方法: `report_attestation_commit`。コミットの first parent はレビュー対象 HEAD とし、変更ファイルはこの予約パスだけにする。attestation SHA は保存後に PR の外部記録へ追記する。レビュー対象ではない attestation commit の後に Git commit を追加すると完了状態は無効になり、通常修正確認と同一独立レビュアーによる限定 closure が必要になる。

## 範囲・根拠

確認した差分 `074be4a8aa0ece890759fa49095ffc2a2ad0dec4..66f0e30330a144ce195fd757f0349790de9062f3` は設計文書、許可語リスト、既存レビュー報告と引用整理報告の4ファイル。今回の独立指摘後の追加差分は既存通常レビュー報告のprofile状態1行だけ。配置・通信・起動図、製品名表記、設計要件の本文は変更されていない。

ユーザー承認範囲は Docker Desktop と Windows Aspire/DCP による設計整理、図の追加、製品名の最小許可語登録、既存Issue/PR内の文書・報告更新である。製品実装、image 操作、OS/Docker/network 設定、外部repo変更、merge は含まれない。

文書 SHA-256 は `75ff76e288e030354821f8c785a41bec4dcf8b1dd1cd61832a442361e28fac46`、許可語リスト SHA-256 は `67e5e51172a7b93f0b9d2c7513ba9de16cd78c56361c99fe16cf1fa1e4d11c19`。作業ツリーと Git blob の内容は一致する。最初に供給された `f6ffd69c...` fingerprint は直接証拠と一致せず、ローカル報告にも出所が見つからなかったため採用していない。

## 独立性とレビュアー

- 初回・closure とも同一レビュアー `/root/issue80_independent_design_review` が担当。実装・文書修正・通常レビューには参加していない。
- 初回 exhaustive review は HEAD `f58ab156138f20c9255b233f274117e390894e08` に対して実施。I80-IFR-001 により一旦 `fail`。
- 初回指摘後、通常レビュアー `/root/issue80_design_normal_review` が同じレビュアーIDで1行修正を検証し `pass`。次に独立レビュアーが同じ予約・レビュアーIDでその指摘だけを closure し `pass_with_held`。
- 要求profile: `gpt-6.1-sol/high`。spawn API は最終適用model/reasoningを返さないため、実profileは `null / unverified`。requested と applied を同一視しない。
- 分解可能性: 初回は独立した確認領域を含む (`independent_workstreams`) が、レビューenforcer lifecycleにより `decomposition_policy: forbidden`、`parallelism_mode: single_agent`、`decomposition_disposition: prohibited_by_review_lifecycle`。

## 独立レビューの対象と判定

文書・図・ホワイトリストを要件と既存コード契約に照らして確認した。

- 配置図は Windows AppHost/DCP、Docker Desktop、専用 bridge、DCP の単独ライフサイクル管理、foreign container 保護を区別している。
- ユーザー管理 Ubuntu 上の別 Docker Engine を製品実行経路から除外し、Docker Desktop 内部 WSL 2 backend とは区別する。設定変更・削除を設計条件にしない。
- 通信図は方向、未確定の審判受信先、実通信と注記線、未割当てポートの不確実性を保つ。UDP multicast は未検証である。
- 起動シーケンスは process start と通信 readiness を区別し、Compare受信は任意分岐、RefereeDriver→AppHost の readiness証跡とその未決方式を示す。循環待ちを前提にしない。
- 名前解決、数値IP/localhost 固定、資源再作成、multicast受信、Windows DCP/runtime の未解決条件は完了済みと表現していない。
- 製品名と実コード識別子の表記を確認。WSL、simulator、RefereeDriver は通常の Sudachi checker で必要性を確認。orion-cm4-sim は親から与えられた CSpell 10指摘の要約に基づくが、元 stdout が保存されておらず直接再現証拠ではない。

### 初回指摘 I80-IFR-001 — low — closed

- 発生元: 独立最終レビュー。
- 初回場所: `reports/aspire-007d-issue80-design-normal-review-87ffca2-20261021.md:42`。
- 問題: 通常レビューが完了しているのに、親所有profile metadataの `application status` が `pending spawn` のままだった。
- 影響: post-spawn状態の報告が不正確で、freeze前のprofile証拠が整合しなかった。
- 必須修正: `spawn_succeeded_profile_unverified` に訂正し、要求profile、`applied: null`、最終profile非観測を保つ。
- 修正証拠: 同行は `spawn_succeeded_profile_unverified`。requested `gpt-6.1-sol/high`、applied `null`、runtime profile observability `final profile metadata unavailable to caller` を保持。変更はHEAD `f58ab15..66f0e30` の当該レポート1行のみ。`git diff --check` exit 0。
- 通常レビューのfix verificationは元の通常レビュアーが行い `pass`。独立レビュアーは同じIDでこの finding のみを closure し、`pass_with_held`。

### Coverage

| 観点 | 状態 |
|---|---|
| 要件・設計整合、製品名/識別子表記 | checked_no_finding |
| 配置図、DCP所有、foreign保護 | checked_no_finding |
| Ubuntu Engine と Desktop内部WSL2の区別 | checked_no_finding |
| 通信方向、審判先の未確定、注記線と実通信の区別 | checked_no_finding |
| readiness、Compare任意分岐、RefereeDriver証跡、循環防止 | checked_no_finding |
| IP/DNS、localhost固定、multicast未検証の明示 | checked_no_finding |
| 変更ファイル、履歴参照、可読性、範囲 | checked_no_finding |
| whitelist最小性 | checked_no_finding（orion-cm4-simのみ提供証拠に制約） |
| 正確なレビュー状態・tracking報告 | checked_finding、I80-IFR-001 closed |
| API/設定/互換性の設計契約 | checked_no_finding |
| 実行コード・workflow・新規error/secret処理 | not_applicable |
| validation adequacy | held（直接確認と提供証拠を区別） |
| Mermaid parser/render、Windows runtime、UDP/multicast、接続契約 | held |
| exact-HEAD CI | held（成功証拠なし） |

## 検証証拠と保留

**直接実施:** PowerShell 7.4.21 login:false / require_escalated から WSL Ubuntu user `donabe` に接続し、branch/HEAD/status、対象blob hash、差分、normal report修正、レビュー報告差分を読取確認。commit `66f0e30` はレポートのみ。通常・独立レビュー担当も文書本文と3図を手作業で確認した。Mermaid parserまたはrendererは実行できていない。

**親から供給された検証結果（独立レビュー担当は再実行していない）:** 対象文書1件の textlint exit 0、CSpell 0 findings exit 0、Sudachi exit 0、Yomiyasu 100/100 exit 0、AppHost build exit 0（3 warnings: NU1902、ASPIRE010）、`git diff --check` exit 0。ビルド警告は解消済みと扱わない。初回build wrapperの終了コード捕捉失敗と、後続の正確なexit 0確認は区別されている。

**未確認 / 保留:** Mermaid描画、現在HEADのCI、Docker Desktop上のWindows DCP、bridge UDP/multicast、接続契約変更とend-to-end受入は実施していない。設計文書のレビューを実機受入、Issue完了、merge許可と同一視しない。

**保持した不確実性:** ignored `tools/lint/scripts/__pycache__/check-markdown-whitelist-sudachi.cpython-314.pyc` がレビュー時に作成された可能性があるが、事前snapshotがなく既存状態か不明。削除・変更していない。別作業の未追跡reportsとIssue82一時ファイルを保持した。元のCSpell 10指摘stdoutは保存されていない。

## 次の操作と境界

レビュー判定 `pass_with_held` は実装・設計の限定レビューだけに適用する。Docker runtime・受入・通信の未解決条件は残り、mergeは許可しない。

この報告は現在、予約パスへの1回の管理用attestation commit待ち。コミット後に許可される操作は、Git HEADを変えない既存PRコメント更新、PR表示によるMermaid描画確認、レビュー依頼、外部Issue操作のみ。追加のrepository commitは行わない。PRコメントにattestation SHAを外部記録する場合も、既存報告コメントだけを更新し、第三者コメントを編集しない。

## Persistence

- mode: `report_attestation_commit`
- technical/reviewed HEAD: `66f0e30330a144ce195fd757f0349790de9062f3`
- administrative parent: `66f0e30330a144ce195fd757f0349790de9062f3`
- report path: `reports/issue-80-design-independent-final-review-20261022083551.md`
- path reservation identity: `c4c21282-7c99-459c-a829-199cae73d9ef`
- resulting report-attestation HEAD: null until the single allowed report-only commit is created and validated; record the resulting SHA outside this file afterward.
