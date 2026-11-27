# DOC-LINT-003 進捗文書の原文照合

作業担当として作業状況の全265行と工程状況の全25行を原文と照合し、40件と6件の採否・理由・段落対応を記録した。独立最終レビューではない。

過剰な漢字連結、外部自前追跡という矛盾、raw visionを映像とする誤訳、プロジェクトを計画や根とする誤訳、構築済み済み等の重複を修正した。diagnostics sample tick、render snapshot、設定名、クラス名、実UI名、実際に記録されたレビュー設定、コミットを原文の意味に沿って示した。

RUNTIME-HOST-002/003の各3失敗・0成功、004の実装前3失敗・0成功と実装後3成功、008の調整対象7成功と広範囲23成功・1失敗、009の3成功/修正後5成功/広範囲26成功・1失敗/調整後26成功、010の10/10/15成功と既知1失敗、011の指摘後3/11成功を各時点とともに保持した。未実行の全体テストを成功とせず、当時の保留理由・後続作業・初回指摘から再レビューまでの経過・報告参照を残した。

旧見出しと後追加のDOC-LINT本文を誤対応させないようにした。D16-B004は旧CAPTURE-REPLAY本文から現在38〜49行、D16-B011は旧完了済み見出しから現在126行、D16-B012は旧RUNTIME-HOST-001本文から現在128〜129行へ対応する。DOC-LINT-001〜003の後追加本文も別途読み、現在の承認状態と過去の検証結果を区別した。

証拠は `diagnostics/wording-occurrence-audit-20260916/decisions-doc-14.json`、`decisions-doc-16.json`、`progress-doc-14-block-correspondence-20260917.json`、`progress-doc-16-block-correspondence-20260917.json`、各 `edits-progress-*.json` に保存した。

`validation-progress-final-20260917-0726.json` では対象textlint/cspell/whitelistと差分検査が終了値0。全対象のnpm run lint:mdは引用中execによる終了値123。初回の不成功を含め、コマンド、対象内容ハッシュ、stdout/stderr/終了値をそれぞれ別名のJSONとログtarへ保存した。

今回の6文書543差分の本文と採否を記録したが、既存13文書272差分との最終照合、4,336全出現箇所の対照台帳、履歴4文書の最終確認は継続中。全件確認済みでもDOC-LINT-003完了でもない。上流の引用例外は方針承認済みだが使用中の固定検査器には未実装であり、取り込みを別件として記録した。用語一覧・検査設定・製品コード・全体完了条件は変更していない。

## 対象の内容ハッシュ

- `Tracker/Design/phases-status.md`: `a165e3c5d1b505c77f2d26c3793c0436c0a68067f2e31ef84f732043d9fa408d`（差分6件）
- `Tracker/Design/tasks-status.md`: `54c3658423601c8aeacf9b8a6004bfd1ff12669a17fb7686bec85b0af1b4e178`（差分40件）
