# 初期履歴・保守性設計の文脈確認

対象は `tracker-history-000-038.md` と `debug-host-maintainability-design.md`。親コミットは `762a9513e395ce55f3119d8eb0c8cb146da8f7cc`。公開コミット番号はコミット作成後の記録で扱う。

初期履歴の39作業・10区分と、保守性設計の全変更箇所を、変更前後の本文・関連実装と照合した。全19文書の確認完了を意味しない。元の出現箇所の一覧と最終的な個別対応表は別途整備中である。

## 修正

形状変更で初期化されるのは、フィールド形状そのものではなく未確定入力と追跡状態である。`TrackerEngine.cs:69–84` の処理に合わせて説明した。観測フレームと追跡フレーム、イベントの発行と通知先への配送、継続観測後のボールと欠測時の保持を区別した。Kalman filter、capture metadata、source detections、XML documentation comment、timeline scrubber などの名称を保持し、造語・助詞の重複を修正した。

## 検証

対象textlint・cspell・`git diff --check` は成功した。対象の許可語検査は「単体テスト」で失敗した。全19文書の `npm run lint:md` も、「単体テスト」と原文引用内の「サブエージェント」で失敗した。自然な表現をlintだけのために崩さず、許可語追加と引用の扱いを利用者確認待ちとしている。設定は変更していない。

`validation-history-design.json` に各コマンド・終了値・検査対象のハッシュを、`history-design-validation-logs.tar.gz` に標準出力・標準エラー・終了値を保存した。失敗ログも含む。

`history-preservation-final.json` で数値、作業ID、レポート参照の出現数が維持され、直接引用を含む開始時点の引用範囲が改変されていないことを確認した。feedback文書は開始時点とバイト単位で一致する。基準本文のすべての括弧句を引用とみなした初回検査は、前任が変更済みの著者自身の説明句を検出して失敗した。初回の判定結果と再現実行のログを保存し、直接引用と説明上の括弧句を区別した。これを検査成功として隠していない。

個別の変更前後・理由は `edits-history*.json` と `edits-maintainability.json`、区分ごとの判断は `decisions-doc-09.json` / `decisions-doc-12.json`、履歴の49行の対照は `table-review-doc-09.json` に保存した。未承認の変更案は `pending-approval-proposals.json` に記録した。

Archiveの縮約された本文、残る設計書、変更前の全出現箇所との最終対応表は未完了。今回の成果を独立最終レビューの合格や全件確認済みとは扱わない。
