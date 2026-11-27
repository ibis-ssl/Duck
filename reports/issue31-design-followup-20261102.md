# Issue #31 設計追補・再確認

## 対象

- リポジトリ: `ibis-ssl/Duck`
- 既存ブランチ / PR: `design/issue18-aspire-test-orchestration` / PR #28
- 変更前HEAD: `7f735af97ec9d3ad3ea75bfda20ebae7eb1ccc4d`
- 目的: 2026-09-29のTIGERs対Crane設計追加を、試合開始までの到達点と既存Game Controller契約に合わせて明確にする。

## 変更

- `Tracker/Design/Testing/aspire-simulation-test-environment.md` に対戦開始確認を追記。`HALT`ではGame Controller APIの `RESUME_FROM_HALT` 後に新しい `STOP` パケットを確認し、`STOP`から `NEXT_COMMAND` の後に新しいチーム別 `PREPARE_KICKOFF_BLUE` / `PREPARE_KICKOFF_YELLOW` を確認して `NORMAL_START` を送る。送信成功、固定待機、`FORCE_START`だけでは開始成立としない。
- 制限時間は実行上限として扱い、それだけで成功にしない。最終結果時に必要なGC API、審判情報、SSL-Vision、参加資源の新しい証拠が欠ければ入力/実行時失敗とし、部分証跡を保持する。
- 同文書のRefereeDriver説明と `Tracker/Design/tasks-status.md` の `ASPIRE-006F6` を同じ遷移・証拠契約に合わせた。

## 要件との照合

- 元の `ASPIRE-MATCH-003` / #52 条件（同じ有効な審判期間にTIGERsとCraneの双方で位置が変わる）を保持。AI指令への帰属や数値移動閾値は追加していない。発生頻度の根拠は不明で、前回レビューのこの追加期待は採用条件にしない。
- 制限時間の成功誤判定は設計上の結果分類で対応。match-controller実装、試合完了、実機/実通信の証明は未実施で、#43/#49/#50と後続受入に残る。
- 2026-09-29の履歴記録は書き換えていない。

## レビュー

- 同じ独立レビュー担当が変更した2文書を再確認。
- 最初の確認のMedium指摘: HALT中は `NEXT_COMMAND` が無効で、`RESUME_FROM_HALT` がSTOPへ遷移する。設計と台帳を修正し、同担当の再確認で解消。今回の設計差分は合格。
- 依頼プロファイル: `gpt-6.1-sol high`。実runtime profileは観測できていない。
- レビュアーは最初の読取で一時的なper-command `safe.directory`指定を使ったと申告。永続Git設定・ファイル変更は報告されず、その後の修正確認ではGit設定変更や再度のoverrideはなかった。

## 検証

変更後の2文書に対する以下の各コマンドは終了コード0。

- `npm run lint:md:text -- --files Tracker/Design/Testing/aspire-simulation-test-environment.md Tracker/Design/tasks-status.md`
- `npm run lint:md:spell -- --files Tracker/Design/Testing/aspire-simulation-test-environment.md Tracker/Design/tasks-status.md`
- `npm run lint:md:whitelist -- --files Tracker/Design/Testing/aspire-simulation-test-environment.md Tracker/Design/tasks-status.md`
- `git diff --check`

文書変更のため製品ビルド、ランタイム試験、実通信受入、最新差分に対するCIは未実施。成功扱いしない。

## 状態

- 変更前HEADは上記のとおり。報告作成時点で設計差分のcommit/pushは未実施。
- PR #28のmain向けmerge、Issue #31/#75/#18のclose、後続Issue実装は未実施。
- 次: 既存設計ブランチへ通常commit/pushし、PR #28の公開HEADを確認する。原完了条件を満たした分だけ完了を記録し、その後 #41 を準備する。