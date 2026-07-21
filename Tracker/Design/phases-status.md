# 段階状況

規則: この文書は `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在の段階: PR
- 現在の作業: `CAPTURE-REPLAY-001` / `RUNTIME-HOST-012` / `DOC-LINT-001` / `DOC-LINT-002`
- 残り段階: 文書検査 PR / PR 確認 / 統合

## 段階一覧

| 段階 | 状態 | 完了条件 |
| --- | --- | --- |
| 準備 | 完了 | 旧 `Tracker.Core/Design` と `Tracker.Server/Design` の設計資料を確認し、`Tracker/Design/Archive/` に旧進捗管理文書を保存した。 |
| 設計 | 完了、下書き PR #17 | `Tracker/Design/` を正本の設計根とし、`Core` / `Tracker.DebugHost` / `Tracker.RuntimeHost` の設計範囲を保管場所で分ける。`Tracker.RuntimeHost` を自前追跡と将来 `AutoRef mode` の本番寄り画面なし実行体、`Tracker.DebugHost` を `Web UI` / 診断 / 再生 / 記録確認画面用の診断実行体として設計し、実行周回分離と旧記録互換非要件を固定した。`reports/runtime-host-001-design-review-r2-20260514160734.md` で阻害指摘なしを確認済み。 |
| 検証 | 完了、下書き PR #17 | `RUNTIME-HOST-002` と `RUNTIME-HOST-003` で `Tracker.RuntimeHost` / `Tracker.DebugHost` の依存境界、読み取り側責務、診断標本境界、旧形式の縮退契約の失敗先行試験を追加し、作業ごとの確認で阻害指摘なしを確認した。`RUNTIME-HOST-002` は二回目確認、`RUNTIME-HOST-003` は `reports/runtime-host-003-review-20260514170652.md` で完了した。 |
| 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-004` から `RUNTIME-HOST-009` で `Tracker.DebugHost` 改名、共有実行周回境界、`Tracker.DebugHost` 読み取り側化、診断標本補助記録の高速経路、`Tracker.RuntimeHost` 骨組み、`Tracker.RuntimeHost` 正常系を対象試験 / 構築 / 作業確認付きで通した。`RUNTIME-HOST-004` は `reports/runtime-host-004-review-20260514172921.md`、`RUNTIME-HOST-005` は `reports/runtime-host-005-review-20260514180308.md`、`RUNTIME-HOST-006` は `reports/runtime-host-006-review-20260514182549.md`、`RUNTIME-HOST-007` は `reports/runtime-host-007-review-r4-20260514192425.md`、`RUNTIME-HOST-008` は `reports/runtime-host-008-review-r2-20260514194042.md`、`RUNTIME-HOST-009` は `reports/runtime-host-009-review-r2-20260514200945.md` で指摘なしを確認済み。 |
| 確認 | 完了、PR #17 提出可能 | `RUNTIME-HOST-010` で `Tracker.RuntimeHost` / `Tracker.DebugHost` 構築、診断標本証跡、旧形式の縮退証跡、`Tracker.DebugHost` 画面正常系、`Tracker.RuntimeHost` 画面なし正常系の検証証跡と作業確認を完了した。`RUNTIME-HOST-011` では最終確認の阻害指摘を受けて取り込み済み失敗契約を現設計へ修正し、`reports/runtime-host-011-final-review-r2-20260514204526.md` で阻害指摘なし / PR 提出可能を確認した。 |
| 記録再生調査 | PR #19 公開中 | `CAPTURE-REPLAY-001` で `Tracker.CaptureReplay` に未加工映像 / `ibis` 自前追跡の周期と `ReceivedAt` 基準遅延を比較する汎用出力を追加し、指定記録の遅延原因を `reports/capture-replay-001-latency-investigation-20260516185833.md` に記録した。対象試験は 11 件成功、`Tracker.CaptureReplay` 構築は成功。専用確認は `reports/pr19-review-capturereplay-20260516200807.md` で阻害指摘なし、文書 / 進捗管理確認は `reports/pr19-review-docs-tracking-20260516200807.md` で阻害指摘なし。 |
| 実行体設定選択 | PR #19 公開中 | `RUNTIME-HOST-012` で `Tracker.RuntimeHost` 起動時に `--profile <name>` / `--profile=<name>` から有効設定を指定できるようにした。命令行解析は `Microsoft.Extensions.Configuration.CommandLine` 提供機能と切り替え対応表を使う。確認指摘修正後の対象試験は 17 件成功、`Tracker.RuntimeHost` 構築は成功。初回確認の重大指摘は修正し、`reports/pr19-review-runtimehost-profile-r2-20260516201757.md` で指摘なしを確認した。 |
| 文書検査整備 | 確認完了、PR 待ち | `DOC-LINT-001` で保存庫根に文書向け `textlint` / `cspell` を導入し、利用者編集対象の `*.md` 全般を品質門に載せる。英単語と片仮名語の許可一覧は既存文書脚注から初版を収集した専用 `YAML` 1 文書を正本とし、単語名と説明の対を保持する。追加で `textlint-rule-prh` と表記揺れ辞書 `tools/lint/prh.yml` を文書検査に組み込んだ。`DOC-LINT-002` で SudachiPy による語彙抽出と、許可一覧検査の日本語形態素化を追加し、`reports/doc-lint-002-review-r2-20260517104319.md` で指摘なしを確認した。全範囲文書検査は未登録語を大量許可一覧で自動許可しないため、意図的に失敗状態。 |
