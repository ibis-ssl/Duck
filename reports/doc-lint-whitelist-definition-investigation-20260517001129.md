# Sub-agent実行レポート

## タスク

既存の `tools/lint/markdown-whitelist.yaml` 定義自体の修正案を調査する。

## sub-agentを使う理由

ユーザー指定により、lint 修正作業と並行してサブエージェントで調査するため。

## 対象範囲

- `tools/lint/markdown-whitelist.yaml`
- `tools/lint/README.md`
- `.agents/skills/review-enforcer/scripts/check-markdown-whitelist.js`
- `.agents/skills/review-enforcer/scripts/run-cspell-markdown.js`
- 担当 worker report の whitelist 候補記録

## 対象外

- `tools/lint/markdown-whitelist.yaml` の編集
- 対象 Markdown 本文の編集
- reports 以外のファイル編集

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,240p' reports/doc-lint-whitelist-definition-investigation-20260517001129.md`
- `sed -n '1,260p' .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js`
- `sed -n '1,220p' .agents/skills/review-enforcer/scripts/run-cspell-markdown.js`
- `sed -n '1,260p' tools/lint/README.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files __no_markdown_input__.md --list-unknown`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files __no_markdown_input__.md`
- `nl -ba tools/lint/markdown-whitelist.yaml | sed -n '1,260p'`
- `sed -n '1,260p' reports/doc-lint-root-docs-worker-20260516235249.md`
- `sed -n '1,260p' reports/doc-lint-tracker-readmes-worker-20260516235249.md`
- `sed -n '1,280p' reports/doc-lint-tracker-design-core-worker-20260516235249.md`
- `sed -n '1,300p' reports/doc-lint-tracker-design-hosts-worker-20260516235249.md`
- `nl -ba .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js | sed -n '1,230p'`
- `nl -ba .agents/skills/review-enforcer/scripts/run-cspell-markdown.js | sed -n '1,160p'`
- `rg -n "CandidateMissing|NoCandidateSnapshot|ReplayTimelineIndex|same-source|saved-session-alignment|WorldFrameCommitted|Tracker\\.DebugHost|Tracker\\.RuntimeHost|\\bTracked\\b|\\bsource\\b" --glob '*.md' --glob '*.yaml' --glob '!reports/**'`
- `git status --short -- reports/doc-lint-whitelist-definition-investigation-20260517001129.md tools/lint/markdown-whitelist.yaml`

## 対象ファイル

- 確認のみ:
  - `tools/lint/markdown-whitelist.yaml`
  - `tools/lint/README.md`
  - `.agents/skills/review-enforcer/scripts/check-markdown-whitelist.js`
  - `.agents/skills/review-enforcer/scripts/run-cspell-markdown.js`
  - `reports/doc-lint-root-docs-worker-20260516235249.md`
  - `reports/doc-lint-tracker-readmes-worker-20260516235249.md`
  - `reports/doc-lint-tracker-design-core-worker-20260516235249.md`
  - `reports/doc-lint-tracker-design-hosts-worker-20260516235249.md`
- 変更:
  - `reports/doc-lint-whitelist-definition-investigation-20260517001129.md`
- 未変更:
  - `tools/lint/markdown-whitelist.yaml`
  - 対象 Markdown 本文
  - 他の report

## 指摘事項

- `check-markdown-whitelist.js` は `--stdin` 以外の実行では、Markdown 入力に加えて `readWhitelistDescriptionInputs(whitelist.entries)` を連結するため、`tools/lint/markdown-whitelist.yaml` の各 `description` も検査対象になる。`--files` で対象 Markdown を絞っても description 検査は残る。
- `term` と `aliases` は `entryValues` から `whitelist.terms` と `valuePattern` に入る。つまり許可値そのものであり、現在の script では `term` / `aliases` の内部語を別途 lint する処理はない。一方で、description 内に書いた英単語とカタカナ語は通常本文と同じ正規表現で検査され、許可値に無ければ失敗する。
- `stripMarkdownNoise` はフェンス付きコード、インラインコード、脚注定義行、URL、メールアドレス、コメント、Markdown link target / reference link address を除外する。続いて `maskWhitelistValues` が whitelist の `term` / `aliases` と一致する値を空白化する。したがって description に通常語として英語やカタカナ語を書くと、既存許可値にない限り検査対象に残る。
- `run-cspell-markdown.js` は `term` / `aliases` から一時辞書と無視パターンを作る。`description` を辞書化せず、description 自体も直接 cspell へ渡さない。whitelist description 由来の失敗は主に `check-markdown-whitelist.js` の責務である。
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files __no_markdown_input__.md --list-unknown` は終了コード 1。存在しない Markdown を指定しているため本文入力は空になり、既存 whitelist description 由来の未登録語だけを切り分けられる。主な未登録語は `snapshot` 6 件、`tracker` 6 件、`Vision` 5 件、`layer` 5 件、`replay` 5 件、`diagnostics` 3 件、`ibis` 3 件、`missing` 3 件、`overlay` 3 件、`raw` 3 件、`tick` 4 件、`タイミング` 1 件。
- entry 別の未登録語:
  - `CandidateMissing`: `Field`, `snapshot`, `missing`, `ready`, `layer`, `legend`, `details`, `reason`
  - `NoCandidateSnapshot`: `comparison`, `snapshot`, `missing`, `UI`, `future`, `later`
  - `ReplayTimelineIndex`: `diagnostics`, `replay`, `selected`, `timeline`, `tick`, `index`, `record`
  - `same-source`: `Layer`, `Vision`, `overlay`, `layer`
  - `saved-session-alignment`: `CaptureOn`, `session`, `replay`, `timeline`, `tick`, `tracker`, `snapshot`, `record`, `diagnostics`, `party`, `selected`
  - `source`: `balls`, `robots`, `geometry`, `Vision`, `split`, `overlay`, `Layer`
  - `Tracked`: `ibis`, `tracker`, `Vision`, `DTO`, `raw`, `detection`, `Layer`, `overlay`
  - `Tracker.DebugHost`: `rename`, `debug`, `host`, `Web`, `UI`, `raw`, `vision`, `viewer`, `diagnostics`, `capture`, `replay`
  - `Tracker.RuntimeHost`: `tracker`, `operation`, `AutoRef`, `mode`, `process`, `headless`
  - `WorldFrameCommitted`: `ibis`, `tracker`, `world`, `frame`, `commit`, `dispatch`, `result`, `render`, `snapshot`, `callback`, `raw`, `Vision`, `cadence`, `タイミング`
- 修正案分類:
  - description を日本語へ直す: `missing`, `ready`, `legend`, `details`, `reason`, `comparison`, `future`, `later`, `selected`, `index`, `record`, `balls`, `robots`, `rename`, `debug`, `host`, `viewer`, `operation`, `mode`, `process`, `headless`, `world`, `frame`, `commit`, `dispatch`, `result`, `render`, `callback`, `タイミング` は、description の説明語として出ているだけなら日本語へ寄せる。例: `missing` は「欠落」、`ready` は「準備済み」、`legend / details` は「凡例と詳細欄」、`future / later snapshot` は「選択時点より後の記録」、`タイミング` は「時点」と書く。
  - term / alias として許可する: 本文や worker report でも繰り返し必要な固有名詞、略語、設計語は whitelist entry へ追加する。重複候補は `AutoRef`, `CaptureOn`, `diagnostics`, `replay`, `snapshot`, `overlay`, `tick`, `timeline`, `tracker`, `raw`, `Vision`, `UI`, `DTO`, `Field`, `Layer`, `geometry`, `cadence`。また `Tracker.DebugHost` には `DebugHost`, `debug-host`、`Tracker.RuntimeHost` には `RuntimeHost`, `runtime-host`, `tracker-runtime-host`, `tracker runtime host` の alias 追加を検討する。
  - entry 自体を削除 / 統合する: 今回の 10 entry は `rg` で対象 Markdown 内の実使用が確認できるため、lint を通す目的だけで削除するのは推奨しない。将来、脚注参照や設計用語を本文側から除去する場合に限り、`CandidateMissing` / `NoCandidateSnapshot` など状態名 entry の削除や統合を再検討する。
- worker report 4 件との重複:
  - root docs: `Codex`, `Serena`, `.NET`, `CLI`, `NuGet`, `Duck`, `SSL-Vision`, `Tracker`, `CaptureOn`, `ASP.NET Core`, `npm`, `Git` と、`スキル`, `ユーザー`, `サブエージェント`, `レビュー`, `レポート`, `リポジトリ`, `マークダウン` などのカタカナ語候補。
  - Tracker README: `SSL-Vision`, `Vision`, `ibis`, `CaptureOn`, `ER-Force`, `Docker`, `ASP.NET Core`, `.NET SDK`, `protobuf`, `UDP`, `HTTP`, `HTTPS`, `API`, `UI`, `CLI`, `JSON`, `JSONL`, `UUID`, `ID`, `NIC`, `OS`, `ns`, `ms`, `mm`, `rad`, `Hz` と、`トラッカー`, `パケット`, `キャプチャ`, `フィールド`, `ロボット`, `ボール`, `タイムライン`, `モード` など。
  - Tracker Core design: `Tracker`, `RuntimeHost`, `DebugHost`, `AutoRef`, `CaptureOn`, `CaptureReplay`, `Tigers`, `Ibis`, `Duck`, `ER-Force`, `Vision`, `Field`, `diagnostics`, `raw`, `packet`, `frame`, `snapshot`, `sidecar`, `replay`, `capture`, `geometry`, `detection`, `timestamp`, `cadence`, `alignment`, `profile`, `Kalman`, `review`, `passed`, `blocking`, `findings`, `done`, `PR`, `validation`, `draft`, `ready`。
  - Tracker DebugHost / RuntimeHost design: `AutoRef`, `Blazor`, `CaptureOn`, `CaptureOff`, `CaptureReplay`, `DebugHost`, `Diagnostics`, `ER-FORCE`, `RuntimeHost`, `SSL-Vision`, `SSL_WrapperPacket`, `Tracker.Server`, `TrackerCoordinator`, `UI`, `CLI`, `JSON`, `JSONL`, `UDP`, `TDD`, `PR`, `README`, `alignment`, `best-effort`, `cadence`, `comparison`, `diagnostics`, `field`, `geometry`, `overlay`, `packet`, `profile`, `raw`, `replay`, `sidecar`, `snapshot`, `source`, `timeline`, `tracker`。
- description 記述ルール案:
  - description は日本語説明を基本にし、許可したい語そのもの以外の英単語を入れない。
  - 許可語の説明で別の未登録カタカナ語を使わない。一般語は「時点」「記録」「表示」「画面」「入力」「出力」「比較」「追跡」「診断」「保存」「再生」のような漢字語に寄せる。
  - 固有名詞、画面ラベル、設定名、型名、コマンド名を説明に出す必要がある場合は、先に同じ entry の `term` / `aliases` または別 entry として許可する。単なる説明語なら日本語へ置換する。
  - ハイフン付き、空白区切り、ドット付きの表記が本文に出る場合は `aliases` に実表記を明示する。空白を含む alias は cspell の辞書語ではなく無視パターンになるため、表示語として必要な単語形は別途確認する。
  - description の例文は短くし、worker report から候補を追加するときも「このリポジトリで何を指すか」を日本語で書く。例: `snapshot` の説明は「保存時点の状態記録。」のようにし、`capture / replay / timeline` など未登録語を連鎖させない。

## 結果

- 既存 whitelist definition 由来の失敗を、対象 Markdown 本文なしの `--files __no_markdown_input__.md` で切り分けた。
- `tools/lint/markdown-whitelist.yaml` の全 10 entry で description 内の未登録語が残っていることを確認した。
- 既存 entry は対象 Markdown 内で実使用があるため、削除よりも description 日本語化と必要語の whitelist 追加 / alias 追加で通す方針が妥当。
- `tools/lint/markdown-whitelist.yaml`、対象 Markdown 本文、他 report は編集していない。

## リスク

- description 日本語化だけでは、worker report 4 件に出ている本文側の `Tracker`, `RuntimeHost`, `DebugHost`, `SSL-Vision`, `UI`, `JSON`, `diagnostics`, `snapshot`, `replay`, `overlay`, `タイムライン` などは解消しない。本文で必要な語はユーザーレビュー後に whitelist entry / alias として追加する必要がある。
- 逆に、本文側候補を大量追加する場合でも、各 entry の description に未登録語を混ぜると `check-markdown-whitelist.js` が再度失敗する。追加時は description を先に lint 可能な日本語へ制約する必要がある。
- `--files` 指定時も whitelist description は常に検査されるため、個別担当ファイルの lint 結果に definition 由来の未登録語が混ざる。担当本文だけを見たい場合は `--stdin` 個別実行、または今回のような存在しない `--files` 指定で definition 側を別集計する必要がある。
