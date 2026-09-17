# DOC-LINT-003 人が読む設計書の照合範囲

## 目的

PR #20 の用語整理について、利用者の今回の指示に基づき、全4,336出現を完了条件にせず、**人が読む設計書**に対象を限定する。`*-plan.md` も内容が設計書であるものは含め、保守案件・進捗管理・README・履歴/保存版・lint手順・feedbackは対象外とする。

この記録は作業範囲と分担境界を固定するためのものであり、独立レビューの合格やPR全体の完了を示すものではない。

## 基準

- PR: `ibis-ssl/Duck#20`
- 範囲決定時の公開HEAD: `f927ac7bf3105d47aca3eff5c4bc98881a8f09ab`
- 用語整理前の比較元: `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`
- 原出現一覧: `reports/diagnostics/wording-occurrence-audit-20260916/occurrences-start.json`
- 今回の機械可読対象一覧: `reports/diagnostics/wording-human-design-scope-20260917-1011/scope-occurrences.json`
## 今回の対象

| 作業単位 | 文書 | 原出現 | 対応済み | 残り |
| --- | --- | ---: | ---: | ---: |
| ENGINE-COMPLETE | `Tracker/Design/Core/tracker-core-engine-detail-design.md` | 133 | 133 | 0 |
| CLI-UI | `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md` | 724 | 724 | 0 |
| ARCHITECTURE | `Tracker/Design/Core/tracker-architecture-plan.md` | 565 | 565 | 0 |
| RAW-VISION | `Tracker/Design/DebugHost/raw-vision-viewer-plan.md` | 524 | 524 | 0 |
| RUNTIME-HOST | `Tracker/Design/RuntimeHost/runtime-host-plan.md` | 196 | 0 | 196 |
| **合計** |  | **2,142** | **1,946** | **196** |

`tracker-core-engine-detail-design.md` は133/133出現の対応表が公開済みなので、新規担当の作業対象ではなく回帰確認用の参照とする。RAW-VISION 524件は全件対応済みで、残り196件は RUNTIME-HOST へ割り当てる。同じ原出現IDを複数担当で処理しない。CLI-UI 724件、ARCHITECTURE 565件、RAW-VISION 524件は、それぞれの現行本文に対する対応表へ統合済みである。

## 対象外

- 保守案件: `tracker-test-maintainability-detail-design.md`、`debug-host-maintainability-design.md`
- 進捗管理: `Tracker/Design/tasks-status.md`、`Tracker/Design/phases-status.md`
- README類
- `tracker-history-000-038.md` と `reports/history/`、archive台帳
- `tools/lint/README.md`、`feedback-points/feedback-points.md`
## 並行作業の分担方法

各担当は **CLI-UI / ARCHITECTURE / RAW-VISION / RUNTIME-HOST のうち1作業単位だけ**を所有する。開始時にRDCのsession/worktreeとPR current HEADを再確認し、同じ文書を別担当が作業中なら編集しない。

担当文書について `scope-occurrences.json` の同一 `file` を抽出し、全原出現IDを1件ずつ最終本文へ対応付ける。各行には少なくとも、原出現ID、元の表記・行・文脈、最終表現・位置・文脈、判断、理由を残す。単なる位置推定やlint通過を意味保持の根拠にしない。

本文を修正する場合は、自然な日本語と技術的意味の維持を優先する。実UI名、型名、設定名、プロトコル名など明示的な技術名は無理に日本語化しない。既存のユーザー承認語と直接引用方針を維持し、新しい許可語・prh・検査除外が必要になった場合だけ利用者判断事項として分離する。

各担当は自分の文書に対する対応表と詳細reportを新規パスへ保存し、小さな論理単位でcommit/pushする。別担当の未コミット変更をstash/reset/cleanせず、他作業単位の本文を同じcommitへ混ぜない。
## 各担当の検証と完了条件

担当文書の自己点検では、対象文書のtextlint、cspell、許可一覧検査、`git diff --check` を実行し、失敗時はstdout・stderr・終了値・原因調査ログを保存する。PR全体の既知の引用内lint残件を、担当文書の失敗と混同しない。

作業単位の完了条件は次のすべてを満たすこととする。

1. `scope-occurrences.json` で割り当てられた原出現IDが全件対応済みで、欠落・重複がない。
2. 原文と最終本文を人が読み、意味変化・不自然な日本語を確認済みである。
3. 必要な本文修正と、その理由・実装/設計上の根拠を記録している。
4. 担当文書のfocused lintと`git diff --check`の結果を保存している。
5. 詳細reportと次担当向けhandoffを保存し、PRへ簡易報告する。

全4作業単位の完了後、統合担当が2,142件のID集合を再計算し、ENGINE-COMPLETE 133件と新規2,009件を合わせて対象範囲2,142/2,142件になっていることを確認する。その後に最新本文との整合、全体検査、独立最終レビューを行う。別SHAのCIをcurrent HEADの証拠として代用しない。

## 現時点の状態

範囲定義時点では対象2,142件中133件が完了していた。その後、CLI-UI 724件、ARCHITECTURE 565件、RAW-VISION 524件を全件対応表へ統合したため、現在は **1,946 / 2,142件完了、残り196件**。ARCHITECTURE の対応表は `reports/diagnostics/wording-architecture-ledger-20260917/architecture-occurrence-correspondence.json`、RAW-VISION の対応表は `reports/diagnostics/wording-raw-vision-ledger-20260917/raw-vision-occurrence-correspondence.json` を参照する。4,336件全体の未処理数はDOC-LINT-003の今回完了条件には用いない。
