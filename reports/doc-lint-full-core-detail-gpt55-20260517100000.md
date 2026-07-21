# Sub-agent実行レポート

## タスク

`Tracker/Design/Core/tracker-core-engine-detail-design.md` と `Tracker/Design/Core/tracker-test-maintainability-detail-design.md` の Markdown lint 指摘だけを対象に修正案を作る。

## sub-agentを使う理由

関連する Core 詳細設計を狭い範囲にまとめ、`gpt-5.5 high` の worker に任せるため。

## 対象範囲

`Tracker/Design/Core/tracker-core-engine-detail-design.md`
`Tracker/Design/Core/tracker-test-maintainability-detail-design.md`

## 対象外

ホワイトリストの直接編集、lint に出ていない箇所の言い換え、機械的な全文置換。

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `sed -n '1,220p' reports/doc-lint-full-core-detail-gpt55-20260517100000.md`
- `git status --short`
- `node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js Tracker/Design/Core/tracker-core-engine-detail-design.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/Core/tracker-core-engine-detail-design.md --list-unknown`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/Design/Core/tracker-core-engine-detail-design.md --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules`
- `node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/Core/tracker-test-maintainability-detail-design.md --list-unknown`
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/Design/Core/tracker-test-maintainability-detail-design.md --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules`
- `git diff -- Tracker/Design/Core/tracker-core-engine-detail-design.md Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
- `git status --short -- Tracker/Design/Core/tracker-core-engine-detail-design.md Tracker/Design/Core/tracker-test-maintainability-detail-design.md reports/doc-lint-full-core-detail-gpt55-20260517100000.md tools/lint/markdown-whitelist.yaml`

## 対象ファイル

- 変更: `Tracker/Design/Core/tracker-core-engine-detail-design.md`
- 変更: `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
- 変更: `reports/doc-lint-full-core-detail-gpt55-20260517100000.md`
- 非編集確認: `tools/lint/markdown-whitelist.yaml`

## 指摘事項

- `tracker-core-engine-detail-design.md`
  - 初回 `cspell`: `Core`, `engine`, `class`, `property`, `method`, `contract`, `proto`, `profile switch`, `geometry`, `detection buffer`, `event-time reorder`, `merge window`, `ball tracking`, `robot tracking`, `orientation unwrap`, `measurement noise`, `settings` など、通常文の英語混じり表現が多数指摘された。
  - 対応: 通常文は日本語へ直し、識別子、ファイルパス、コマンド、単位はコード表記または既存表記を維持した。
  - 残り: `cspell` と `textlint` は通過。専用 whitelist 検査だけが未登録語を残している。
- `tracker-test-maintainability-detail-design.md`
  - 初回 `cspell`: `test`, `file`, `Core engine`, `production code`, `event-time buffer`, `geometry reset`, `profile switch`, `fixture`, `assertion`, `capture`, `replay`, `runtime toggle`, `focused test`, `full test` など、通常文の英語混じり表現が多数指摘された。
  - 対応: 通常文は日本語へ直し、テスト名、型名、コマンドは意味を変えず維持した。
  - 残り: `cspell` と `textlint` は通過。専用 whitelist 検査だけが未登録語を残している。
- whitelist 候補:
  - 複合語候補:
    - `フィールド形状`, `フィールド形状リセット`, `フィールド形状スナップショット`: 競技フィールドの幾何情報を指す固定語であり、単独の `フィールド` や `形状` より受け入れ範囲を狭くできる。
    - `描画スナップショット`, `状態スナップショット`: 単独の `スナップショット` より、診断表示や状態保持の文脈に限定できる。
    - `ロボット追跡`, `ボール追跡`, `ロボット観測`, `ボール観測`: 追跡対象と処理内容が一体の用語であり、単独の `追跡` や `観測` より意味が明確。
    - `ワールドフレーム`, `フレーム番号`, `確定フレーム`: フレーム一般ではなく、トラッカーの出力単位または時系列単位を指す。
    - `プロファイル切り替え`, `実行時上書き`, `設定上書き`: 設定変更の契約を表す固定語であり、単独の `プロファイル` より範囲を狭くできる。
    - `イベント時刻`, `イベント通知`, `メタイベント`, `イベント順序`: トラッカーのイベント契約を表す固定語であり、単独の `イベント` を避けられる箇所が多い。
    - `パケット生成器`, `パケット取得セッション`, `遅延パケット`: 通信または取得処理に関わる固定語であり、単独の `パケット` より用途を限定できる。
    - `テストクラス`, `テストメソッド`, `テストフレームワーク`, `回帰テスト`, `重点テスト`: テスト保守性設計の単位を表す固定語であり、単独の `テスト` より範囲を狭くできる。
    - `レビューゲート`, `レビュー用レポート`, `実行レポート`: 作業手順上の成果物や判定点を表す固定語であり、単独の `レビュー` や `レポート` より範囲を狭くできる。
    - `XML コメント`, `XML 要約`, `XML ドキュメントコメント`: C# コメント形式の固定語であり、単独の `XML` より意図が明確。
    - `ドキュメントコメント`, `通常コメント`: コメント種別を区別する固定語であり、単独の `コメント` より範囲を狭くできる。
    - `データフィールド`, `前提データ`, `診断データ`: テストや診断の文脈に限定でき、単独の `データ` より広がりを抑えられる。
    - `ロボットキー`, `ワールドモデル`, `ロボカップ競技`: ドメイン内で意味が固定された複合語。
  - 単独候補:
    - `ボール`: ロボカップ競技の基礎対象であり、設計全体で追跡対象として頻出するため単独登録の理由が明確。
    - `ロボット`: ロボカップ競技の基礎対象であり、追跡、観測、出力の各文脈で頻出するため単独登録の理由が明確。
    - `カメラ`: SSL-Vision と表示の入力単位であり、カメラ内、複数カメラ、カメラ ID などの文脈で頻出するため単独登録を検討できる。
    - `キック`: 競技イベントの基礎語であり、キック検出、動作中キック状態、浮き球キック分類などで頻出するため単独登録を検討できる。
    - `ゴール`: 競技フィールドの基礎語であり、ゴール線、ゴール開口部の分類に使うため単独登録を検討できる。
    - `コード`: 製品コード、コード表記、既存コードなど作業説明の基礎語として頻出するため単独登録を検討できる。
  - 保留または複合語化推奨:
    - `アクセス`, `アセンブリ`, `アルゴリズム`, `インターフェイス`, `オーバーロード`, `キャッシュ`, `クラス`, `コマンド`, `コンストラクター`, `コンパイルエラー`, `スキーマ`, `ソース`, `チーム`, `ツールチェーン`, `ディレクトリ`, `ドット`, `トップレベル`, `ノイズ`, `パス`, `ビルド`, `ファイル`, `フィルター`, `フォルダ`, `フレームワーク`, `プロトコル`, `プロパティ`, `メソッド`, `メタデータ`, `メンバー`, `モデル`, `ラジアン`, `ラッパー`, `リスク`, `リセット`, `レコード`: 単独では一般語として広すぎるため、実際に許可する場合は `公開プロパティ`, `プロトコル変換`, `コンパイルエラー`, `ラジアン単位`, `メタデータ設定` など、文脈付きの複合語に絞る。
  - 理由: `geometry` のような単独語は広く受け入れすぎるため、原則として処理領域や対象を含む複合語で提案する。単独候補は、ロボカップ競技または画面表示の基礎語として頻出し、単独登録の説明ができるものに限定した。`tools/lint/markdown-whitelist.yaml` は今回の所有外かつ編集禁止のため変更していない。

## 結果

- `Tracker/Design/Core/tracker-core-engine-detail-design.md`
  - `run-cspell-markdown.js`: 通過。`Issues found: 0 in 0 files.`
  - `textlint`: 通過。
  - `check-markdown-whitelist.js --list-unknown`: 未通過。未登録語は `XML`, `イベント`, `カメラ`, `ボール`, `ロボット` など。
- `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
  - `run-cspell-markdown.js`: 通過。`Issues found: 0 in 0 files.`
  - `textlint`: 通過。
  - `check-markdown-whitelist.js --list-unknown`: 未通過。未登録語は `XML`, `テスト`, `クラス`, `コメント`, `パケット` など。
- `tools/lint/markdown-whitelist.yaml` は編集していない。

## リスク

- 専用 whitelist 検査は未登録語が残っているため、この 2 ファイル単体では完全通過していない。
- whitelist 候補は意味レビューが必要。特に単独語を許可すると受け入れ範囲が広がるため、親側で複合語または説明付き許可語へ絞る判断が必要。
- 作業開始時点で `tools/lint/markdown-whitelist.yaml` に既存変更があったが、今回の禁止事項に従い触れていない。
