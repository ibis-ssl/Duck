## 実行コマンド
### CSpell 表示上限と旧行の判定訂正（2026-10-22）

- 全gateを再実行せず、保存済stdoutと既存CSpell cache sourceを読取確認した。上限未観測だった今回の親変更行だけを、同じconfig/辞書/相対pathの別一時mirrorへ抜粋し、既存 run-markdown-targets.js spell --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md で限定検査した。対象2断片文書、exit0、指摘0。元の行位置を空行で保持し、全文検査とは区別。実commandと出典行/全文hash/断片hashを edited-lines-proof に保存した。


## 対象ファイル
### CSpell 表示上限と旧行の判定訂正（2026-10-22）

- tasks:25（I81-02）はbaseline/currentとも SHA256 154a2d35fc70714e760d323a49c4580a56797808582066bb89eb23844ca1a58d、tasks:26（I81-V）はともに f805b8398383e7bcaa9a86ad0bde5ab4e7711f1d22c784abdd6c2ea770b18846。いずれも改行を除く行byte完全同一。全文の保存hash・raw旧結果を保持し、製品/試験/trackingは編集していない。


## 指摘事項
### CSpell 表示上限と旧行の判定訂正（2026-10-22）

- 先の newTokenStrings 差分を新本文の追加と解釈するのは誤りのため訂正する。既存CSpellの DefaultSettings.js:77 は maxNumberOfProblems:100、docValidator.js:322-326 は重複語制限と1文書上限到達で出力を打ち切る。baseline/finalのrawはともにphases19/tasks100。前方の指摘減少により後方の旧語が表示範囲へ入り、同一語の重複制限でも表示位置が変化した。difflibの同一行対応自体はtasks25/26を正しく対応していた。これらは追加本文ではなく既存記録の表示差であり、旧I81-02/I81-V行の翻訳提案を撤回する。
- 新規本文の未観測解消は限定断片検査で確認した。tasks13/19/27-30とphases14のCSpell指摘は0。Sudachi保存JSONでもこれらの新規語は0で、実指摘はtasks19のTCPのみ（baseline19/29にも存在する実protocol名）。CSpell全文の後半が未観測だったことを、全文無指摘と読み替えていない。


## 結果
### CSpell 表示上限と旧行の判定訂正（2026-10-22）

- 訂正の一次証拠は cspell-display-limit-correction.json。行同一hash、cache sourceの該当行/hash、raw上限件数、断片2文書の0指摘、現在編集行ごとのSudachi指摘を記録した。最終full24のCSpell232件/Sudachi890件と既存不合格判定は変えていない。旧JSON/rawは履歴として残す。
- 限定検査session84074はexit0で終端回収済み。一時mirror/junction削除、既存cache保持。CIは再実行していない。report親prefix/normalreview prefixは保持した。


## リスク
### CSpell 表示上限と旧行の判定訂正（2026-10-22）

- 上限付き診断の件数や語集合は全本文の未知語一覧ではない。旧行byte同一の直接証拠を優先し、表示差だけで新規本文や改稿必要性を断定しない。今回変更行の断片検査は全文gate不合格を解消するものではない。
