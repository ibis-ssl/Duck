# Sub-agent実行レポート

## タスク

- 目的: Markdown lint 向け whitelist に入れる複数単語の候補を調査し、単語分割すべきでない語句を提案する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指定により、lint 修正作業はサブエージェントで分担するため。

## 対象範囲

- 対象:
  - `tools/lint/markdown-whitelist.yaml`
  - `reports/doc-lint-whitelist-definition-investigation-20260517001129.md`
  - 非 `reports/**` の Markdown に残る表示名、設定名、型名、設計語

## 対象外

- 対象外:
  - `tools/lint/markdown-whitelist.yaml` の編集
  - Markdown 本文の編集
  - lint script の変更
  - PR 作成、commit 作成

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `sed -n '1,240p' reports/doc-lint-whitelist-compound-term-proposal-20260517002025.md`
- `sed -n '1,240p' tools/lint/markdown-whitelist.yaml`
- `sed -n '1,260p' reports/doc-lint-whitelist-definition-investigation-20260517001129.md`
- `sed -n '1,260p' reports/doc-lint-root-docs-worker-20260516235249.md`
- `sed -n '1,280p' reports/doc-lint-tracker-readmes-worker-20260516235249.md`
- `sed -n '1,320p' reports/doc-lint-tracker-design-core-worker-20260516235249.md`
- `sed -n '1,340p' reports/doc-lint-tracker-design-hosts-worker-20260516235249.md`
- `rg -n "[A-Za-z][A-Za-z0-9_.-]+[ /][A-Za-z0-9_.-]+|[A-Za-z][A-Za-z0-9_.-]+ [A-Za-z0-9_.-]+|[ァ-ヶー]+[ァ-ヶー]+" AGENTS.md README.md tools/lint/README.md Tracker --glob '*.md' --glob '!**/bin/**' --glob '!**/obj/**'`
- `rg -n "Raw Aggregate|Raw Camera|Vision Input|Layer A/B|Fast Forward|latest-before|same render tick|source selection|overlay layer contract|geometry contract|Diagnostics replay|raw vision viewer|Tracker\\.RuntimeHost|Tracker\\.DebugHost|Tracker\\.CaptureReplay|Tracker\\.Server|ASP\\.NET Core|\\.NET SDK|SSL-Vision|ER-Force|ER-FORCE|Tracker:ActiveProfileName|ReorderWindowNs|Tracker:Profiles|AutoRef mode|world frame|render snapshot|tracker source snapshot|replay timeline tick|saved-session-alignment|3rd party tracker" README.md AGENTS.md tools/lint/README.md Tracker --glob '*.md' --glob '!**/bin/**' --glob '!**/obj/**'`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files README.md AGENTS.md tools/lint/README.md Tracker/README.appsettings.md Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.DebugHost/README.md Tracker/Tracker.RuntimeHost/README.md Tracker/Design/DebugHost/raw-vision-viewer-plan.md Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md Tracker/Design/RuntimeHost/runtime-host-plan.md --list-unknown`
- `git status --short -- reports/doc-lint-whitelist-compound-term-proposal-20260517002025.md tools/lint/markdown-whitelist.yaml`

## 対象ファイル

- 変更:
  - `reports/doc-lint-whitelist-compound-term-proposal-20260517002025.md`
- 確認のみ:
  - `tools/lint/markdown-whitelist.yaml`
  - `reports/doc-lint-whitelist-definition-investigation-20260517001129.md`
  - `reports/doc-lint-root-docs-worker-20260516235249.md`
  - `reports/doc-lint-tracker-readmes-worker-20260516235249.md`
  - `reports/doc-lint-tracker-design-core-worker-20260516235249.md`
  - `reports/doc-lint-tracker-design-hosts-worker-20260516235249.md`
  - `AGENTS.md`
  - `README.md`
  - `tools/lint/README.md`
  - `Tracker/README.appsettings.md`
  - `Tracker/Tracker.CaptureReplay/README.md`
  - `Tracker/Tracker.DebugHost/README.md`
  - `Tracker/Tracker.RuntimeHost/README.md`
  - `Tracker/Design/Archive/DebugHost/tasks-status.md`
  - `Tracker/Design/Archive/DebugHost/phases-status.md`
  - `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
  - `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
  - `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
  - `Tracker/Design/RuntimeHost/runtime-host-plan.md`
  - `Tracker/Design/Core/tracker-architecture-plan.md`

## 指摘事項

- whitelist 追加を推奨する複数単語候補:
  - 固有名詞・型名:
    - `SSL-Vision`: aliases は `SSL Vision`。プロトコル名として分解せず扱う。
    - `ASP.NET Core`: aliases は `ASP NET Core`, `ASPNETCORE`。製品名と環境変数接頭辞の両方が本文に出る。
    - `.NET SDK`: aliases は `NET SDK`, `.NET`。要件表示として一体で使う。
    - `Tracker.RuntimeHost`: aliases は `RuntimeHost`, `Tracker RuntimeHost`, `tracker-runtime-host`, `tracker runtime host`。host 名として扱う。
    - `Tracker.DebugHost`: aliases は既存の `tracker-debug-host`, `tracker debug host` に加えて `DebugHost`, `Tracker DebugHost`。host 名として扱う。
    - `Tracker.CaptureReplay`: aliases は `CaptureReplay`, `Tracker CaptureReplay`。CLI ツール名として扱う。
    - `Tracker.Server`: aliases は `Tracker Server`。旧 host 名の履歴として残る。
    - `SSL_WrapperPacket`: aliases は `SSL WrapperPacket`。型名として扱う。
    - `TrackerCoordinator`: aliases は `Tracker Coordinator`。型名として扱う。
    - `3rd party tracker`: aliases は `third-party tracker`, `third party tracker`, `3rd-party tracker`。外部トラッカー source を指す設計語として扱う。
    - `ER-Force`: aliases は `ER-FORCE`, `ER Force`。外部トラッカー / simulator 名として扱う。
  - 画面表示名・状態名:
    - `Vision Input`: aliases は `Vision input`, `vision input`。診断画面の表示元名として扱う。
    - `Raw Aggregate`: aliases は `raw aggregate`, `raw-aggregate`。Vision source 候補名として扱う。
    - `Raw Camera`: aliases は `raw camera`, `raw-camera`。Vision source 候補名として扱う。
    - `Layer A/B`: aliases は `Layer A`, `Layer B`, `layer A/B`。左右または overlay の表示 layer 名として扱う。
    - `Fast Forward`: aliases は `FastForward`, `fast forward`。再生操作名として扱う。
    - `Capture On`: aliases は `CaptureOn`, `Capture On`。画面操作名 / 状態名として扱う。
    - `Capture Off`: aliases は `CaptureOff`, `Capture Off`。画面操作名 / 状態名として扱う。
    - `same-source`: aliases は `same source`, `same-source 1 layer`。同一 source 選択時の表示状態として扱う。
    - `latest-before snapshot`: aliases は `latest before snapshot`, `latest-before hold`。診断比較の直前記録採用状態として扱う。
    - `NoCandidateSnapshot`: aliases は `no-candidate-snapshot`, `No Candidate Snapshot`。比較候補なし状態名として扱う。
    - `CandidateMissing`: aliases は `candidate-missing`, `Candidate Missing`。source 候補欠落状態名として扱う。
  - 設定名・設計語:
    - `Tracker:ActiveProfileName`: aliases は `ActiveProfileName`。設定キーとして扱う。
    - `Tracker:Profiles`: aliases は `Tracker Profiles`, `Tracker:Profiles:<name>`。設定階層として扱う。
    - `ReorderWindowNs`: aliases は `reorder-window`, `reorder window`。設定名 / 脚注名として扱う。
    - `AutoRef mode`: aliases は `AutoRef`, `autoref-mode`。将来実行 mode 名として扱う。
    - `raw vision viewer`: aliases は `raw-vision-viewer`, `raw vision`。画面 / 設計範囲名として扱う。
    - `Vision split / overlay`: aliases は `Vision split`, `Vision overlay`, `split / overlay`, `split mode`, `overlay mode`。比較画面の設計範囲として扱う。
    - `source selection`: aliases は `source selector`, `source filter`。source 選択設計語として扱う。
    - `overlay layer contract`: aliases は `overlay layer`, `layer contract`。TDD 契約名として扱う。
    - `geometry contract`: aliases は `geometry-reference`, `geometry reference`。field 描画基準の契約名として扱う。
    - `same render tick`: aliases は `same-render-tick`, `UI render tick`, `ui-render-tick`。同一描画時点で比較する契約として扱う。
    - `render snapshot`: aliases は `render-snapshot`, `render snapshots`。旧診断描画記録の設計語として扱う。
    - `tracker source snapshot`: aliases は `tracker-source snapshot`, `tracker snapshot`, `tracker-snapshot`。外部 / 自前 tracker 記録として扱う。
    - `replay timeline tick`: aliases は `ReplayTimelineIndex`, `selected replay timeline tick`, `selected-replay-timeline-tick`。診断再生位置の識別単位として扱う。
    - `saved-session-alignment`: aliases は `saved session alignment`, `saved alignment record`。保存時対応表として扱う。
    - `latest-before fallback`: aliases は `latest before fallback`, `diagnostics latest-before fallback contract`。future fallback 禁止と対になる設計語として扱う。
    - `world frame`: aliases は `world-frame`, `WorldFrameCommitted`。追跡結果確定単位として扱う。
- aliases として同時に入れるべき表記ゆれ:
  - host 名は dot 付き、空白区切り、kebab-case、短縮名を同じ entry に寄せる。例: `Tracker.DebugHost` / `Tracker DebugHost` / `tracker-debug-host` / `DebugHost`。
  - 画面表示名は大文字小文字差、空白区切り、kebab-case を同じ entry に寄せる。例: `Raw Aggregate` / `raw aggregate` / `raw-aggregate`。
  - 設計語は本文の脚注名と自然文表記を同じ entry に寄せる。例: `latest-before snapshot` / `latest before snapshot` / `latest-before hold`。
  - 既存 entry に aliases を足す候補: `Tracker.DebugHost` に `DebugHost`, `Tracker DebugHost`、`Tracker.RuntimeHost` に `RuntimeHost`, `Tracker RuntimeHost`, `tracker-runtime-host`, `tracker runtime host`、`ReplayTimelineIndex` に `replay timeline tick`, `selected replay timeline tick`、`saved-session-alignment` に `saved session alignment`, `saved alignment record`、`source` に `source selection`, `source selector`, `source filter`。
- whitelist に入れず本文を日本語へ直すべき複数単語候補:
  - `tracked frame`, `robot tracker`, `ball tracker`, `kick detector`, `reorder window`, `event time`, `world frame`, `capture file`, `summary`, `detail`, `latency analysis`, `option`, `sidecar status` は、既存 README worker が本文修正対象として扱っており、一般説明語として出る場合は「追跡済みフレーム」「ロボット追跡」「ボール追跡」「キック検出」「並べ替え猶予」「発生時刻」「確定フレーム」「キャプチャファイル」「概要」「詳細」「遅延解析」「選択肢」「補助ファイル状態」へ寄せる。
  - `official`, `packet capture`, `metadata`, `session folder`, `project / namespace`, `debug`, `host`, `operation`, `mode`, `process`, `headless`, `render`, `callback`, `dispatch result` は、設計語・型名・設定名として固定されていない説明語なら日本語へ寄せる。
  - `blocking findings`, `source of truth`, `canonical design root`, `focused tests`, `server build`, `manual evidence`, `ready layer`, `missing reason`, `future / later snapshot` は、進捗説明や一般説明としては日本語へ寄せる。`latest-before fallback` のように設計契約名として固定された語だけを whitelist 候補に残す。
  - `path`, `file`, `folder`, `directory`, `button`, `cache`, `component`, `selector`, `store`, `viewport`, `zoom` は候補 report に出ているが、単体の一般 UI / 実装語として使うなら本文日本語化を優先する。画面ラベルまたは型名の一部として固定される場合だけ別途 entry を検討する。
- description で使ってよい説明方針:
  - description は日本語を基本にし、許可対象の `term` / `aliases` 以外の未登録英語・未登録カタカナを混ぜない。
  - 説明には「画面」「表示」「設定」「記録」「時点」「比較」「保存」「再生」「追跡」「診断」「入力」「出力」「対応表」「欠落」「候補」「状態」「契約」など、既存 whitelist に依存しにくい漢字語を使う。
  - 固有名詞や型名を説明に出す必要がある場合は、先に同じ entry の alias または別 entry として登録する。
  - 複数語 entry では「このリポジトリで何を指すか」を短く書く。例: `Vision Input` は「診断画面で未加工の入力記録を表示元として選ぶための表示名。」のように書き、未登録の `raw`、`source`、`snapshot` を説明文へ連鎖させない。
  - 空白を含む alias と kebab-case alias は、本文で実際に出る表記を優先して登録する。cspell 側の辞書化だけでなく、whitelist 側の値 masking にも効く前提で扱う。

## 結果

- 既存 report と対象 Markdown から、単語単位ではなく複数単語として保持すべき候補を抽出した。
- 推奨候補は、固有名詞・型名、画面表示名・状態名、設定名・設計語の 3 分類に整理した。
- 一般英語や説明用の複数語は whitelist へ入れず、日本語本文へ寄せる候補として分離した。
- `tools/lint/markdown-whitelist.yaml`、Markdown 本文、lint script は編集していない。

## リスク

- 今回の候補は whitelist 編集前の提案であり、ユーザーの明示レビューなしに `tools/lint/markdown-whitelist.yaml` へ反映してはならない。
- `check-markdown-whitelist.js --files` は whitelist description も検査するため、候補追加時の description に未登録語を混ぜると再度失敗する。
- Markdown link target、URL、ファイルパス、inline code、脚注ラベル由来の語は候補から除外したが、lint 側の除外仕様が変わると再集計が必要になる。
- `world frame` など一部の語は、設計語として固定する箇所と一般説明として日本語化すべき箇所が混在する。登録時は entry の説明範囲を狭くし、本文側の一般用法まで無差別に許可しない判断が必要。
