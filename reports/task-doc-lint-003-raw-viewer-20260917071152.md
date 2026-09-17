# DOC-LINT-003 表示画面設計の原文照合

作業担当として `Tracker/Design/DebugHost/raw-vision-viewer-plan.md` の全394行と原文を照合し、85差分の採否および62脚注の対応を記録した。独立最終レビューではない。

追跡結果のDTOを映像表示用とする誤訳をフィールド描画用へ修正した。raw visionは画像/動画ではなくSSL-Visionの検出情報であるという定義と整合させた。入出力のフレーム時刻、source name、端末側のIPv4、単体テスト、render snapshot、diagnostics sample tick、CaptureOnのキャプチャー単位、Raw/Tracked/Compare等の実UI名を原文の文脈へ戻した。

split/overlayでの共通部品とviewportの責務、1回のUI tickに固定するimmutable snapshot、UUID集約、latest-beforeと未来候補禁止、旧形式の周期制限と新規方式、100ms既定/0以下復帰、20テスト項目の条件を保持した。許可一覧、検査除外、製品コードは変更していない。本文の編集後も読み直し、編集時に混入した助詞の崩れと語の区切りを最終検証前に修正した。

証拠は `diagnostics/wording-occurrence-audit-20260916/decisions-doc-13.json`、`raw-viewer-block-correspondence-20260917.json`、`edits-raw-viewer-20260917.json`、`edits-raw-viewer-final-selfcheck-20260917.json` に保存した。最終本文SHA-256は `80b316ca08b744f2c7567f94840c88469625605f0d5f20120ab76627ae7dae09`。

`validation-raw-viewer-final-20260917-0711.json` で対象textlint/cspell/whitelistと差分検査は終了値0。全対象の `npm run lint:md` は引用中execのため終了値123。成否を問わないstdout/stderr/終了値/対象ハッシュは同名単位のログtarとJSONに保存した。全体完了とはしない。

DebugHost README、作業状況、工程状況、全4,336出現箇所の対照台帳、履歴本文の最終照合を継続する。新たな用語承認が必要な事項はない。独立最終レビューとこの後のHEAD一致CIは別に扱う。
