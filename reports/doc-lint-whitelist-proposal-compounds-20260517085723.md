# Sub-agent実行レポート

## タスク

- 目的: 旧 `temporary-doc-lint-terms` 一覧から、単語単体ではなく複合語として whitelist 登録すべき候補を分類する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指定により、旧一時許可一覧を複数カテゴリに分けてサブエージェントで分担するため。

## 対象範囲

- 対象:
  - `git show HEAD:tools/lint/markdown-whitelist.yaml` 内の旧 `temporary-doc-lint-terms`
  - 非 `reports/**` Markdown 内の実使用箇所

## 対象外

- 対象外:
  - `tools/lint/markdown-whitelist.yaml` の編集
  - Markdown 本文の編集
  - lint script の変更

## 実行コマンド

- 実行コマンド:
  - `git show HEAD:tools/lint/markdown-whitelist.yaml | sed -n '/term: temporary-doc-lint-terms/,/description: 一時許可語/p'`
  - `rg --files -g '*.md' -g '!reports/**'`
  - `rg -n -g '*.md' -g '!reports/**' -i "event[- ]time|pending detection|field geometry|raw snapshot|render tick|latest before|receive timestamp|capture time|packet timestamp|frame timestamp|wall clock|time sync|loop isolation|tracker operation|world model|live store|immutable snapshot|diagnostics line|replay timeline|selected tick|multi camera|third party tracker|field first|same process|same render|thread safe|user visible|headless host|read side"`
  - `rg -n -g '*.md' -g '!reports/**' -i "event time|event-time|pending detection buffer|pending buffer|field geometry|receive timestamp|packet timestamp|frame timestamp|capture-time|capture time|wall-clock|render tick|raw snapshot cadence|diagnostics sample tick|diagnostics sample sidecar|diagnostics sample timeline|loop isolation|tracker operation|server live display|diagnostics logging / replay|web server live display|immutable snapshot|live store|alignment sidecar|alignment record|nearest timestamp|future / later snapshot|timeline cursor|raw snapshot|tracked snapshot|world model|same process|headless host|read-side snapshot|composite read-side|field source|source label|source role|matching rule|replay timeline index|latest-before frame|nearest-after fallback|fastest available source cadence|transport button"`
  - `rg -n -g '*.md' -g '!reports/**' -i "\\[[^\\]]*\\]\\([^)]*(event|pending|field|timestamp|snapshot|timeline|alignment|source|render|diagnostics|loop)[^)]*\\)|\`[^\`]*(event|pending|field|timestamp|snapshot|timeline|alignment|source|render|diagnostics|loop)[^\`]*\`"` は shell の解釈で失敗したため、結果判定には使っていない。

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/doc-lint-whitelist-proposal-compounds-20260517085723.md`
  - 確認: `tools/lint/markdown-whitelist.yaml` の HEAD 版
  - 確認: `Tracker/Design/RuntimeHost/runtime-host-plan.md`
  - 確認: `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
  - 確認: `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
  - 確認: `Tracker/Design/Core/tracker-architecture-plan.md`
  - 確認: `Tracker/Design/Core/tracker-core-engine-detail-design.md`
  - 確認: `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
  - 確認: `Tracker/Design/Core/tracker-history-000-038.md`
  - 確認: `SslProto/src/external/ssl-game-controller/CLAUDE.md`

## 指摘事項

- 登録推奨:
  - alias は同じ意味の表記ゆれだけに限定する。構成要素、上位語、下位語、関連語は alias に入れない。
  - `event time`: 到着順ではなく入力検出の時刻で処理順を決める設計語として複数箇所で使われている。`event-time reorder`、`event-time buffer`、`event time ordering` は同義ではないため、必要なら別 entry または本文日本語化に分ける。
  - `event-time reorder`: 入力検出の時刻で並べ替える契約名として使われている。
  - `event-time buffer`: 入力検出の時刻順に保持する領域名として使われている。
  - `pending detection buffer`: 確定前の検出を保持する領域として使われている。`pending buffer` は同じ領域の省略表現として扱えるが、単体の `pending` や `buffer` は登録しない。
  - `field geometry`: 競技場描画や入力側形状の基準として使われている。`Vision field geometry` や `raw Vision field geometry` は下位文脈なので alias にせず、必要なら別 entry か本文日本語化に分ける。
  - `packet timestamp` / `receive timestamp` / `frame timestamp` / `tracked frame timestamp` / `capture-time` / `wall-clock`: 診断再生や比較の時刻軸を区別するために複合語として使われている。単体の `timestamp`、`packet`、`frame`、`time` は登録しない。
  - `diagnostics sample tick` / `diagnostics sample sidecar` / `diagnostics sample timeline`: 新規保存経路の単位と補助記録を表す設計語として使われている。
  - `raw snapshot cadence` / `latest raw snapshot` / `immutable snapshot` / `read-side snapshot` / `composite read-side snapshot`: 表示、保存、読み取り境界の区別を示す複合語として使われている。`composite read-side snapshot` は `read-side snapshot` の同義ではないため別 entry とする。
  - `alignment sidecar` / `alignment record`: 診断再生の対応表と対応表内の記録を指す設計語として使われている。
  - `selected tick` / `timeline cursor` / `future / later snapshot` / `nearest timestamp`: Issue #10 系の時刻対応規則を説明する用語として脚注と本文で使われている。`future snapshot` と `later snapshot` の単独許可は避ける。
  - `loop isolation`: 周期と責務を切り離す方針名として設計上の意味を持つ。
  - `same process` / `headless host`: 実行形態を説明する複合語として脚注で意味定義されている。
  - `source role` / `source label` / `source key` / `Field source` / `matching rule`: 外部追跡器の保存、選択、比較規則を説明する語としてまとまって使われている。`Field source label` や `Field source selector` は同義ではないため alias にしない。
- 本文日本語化優先:
  - `transport button`、`compact tabs`、`comparison panel`、`source option`、`display label` は画面部品の一般説明であり、本文を日本語へ寄せた方が whitelist を広げずに済む。
  - `fastest available source cadence`、`web server live display processing`、`diagnostics logging / replay processing` は長い説明句として使われている。登録するより、本文側で短い日本語説明に分ける方が読みやすい。
  - `tracker operation loop`、`server live display` は `loop isolation` の説明文脈に出るが、現時点では方針名としての `loop isolation` と保存境界語を優先すれば足りる。
  - `Vision field geometry`、`raw Vision field geometry` は `field geometry` の下位文脈であり、本文を日本語へ寄せるか、別 entry として必要性を判断する。
  - `capture-time ordering`、`target capture-time`、`sample tick`、`receive time` はそれぞれ `capture-time`、`diagnostics sample tick`、`receive timestamp` と同義ではないため、alias にせず本文日本語化を優先する。
  - `Field source label`、`Field source selector` は `Field source` の構成要素または操作対象であり、必要なら別 entry として登録する。
- 不要:
  - 単体語のみの `buffer`、`field`、`geometry`、`timestamp`、`snapshot`、`source`、`render`、`diagnostics`、`loop`、`button`、`tab` などは一般語なので候補外。
  - `Tracker.RuntimeHost`、`Tracker.DebugHost`、`ReplayTimelineIndex`、`CandidateMissing`、`NoCandidateSnapshot`、`ReorderWindowNs`、`WorldFrameCommitted` などは識別子、型名、状態名の候補であり、今回の複合語候補からは外す。
  - `Home.razor`、`tracker-snapshot-alignment.jsonl`、`diagnostics-samples.jsonl`、`reports/...` など、パス、ファイル名、命令、または inline code のみで現れるものは候補外。
  - `3rd party tracker`、`latest-before snapshot`、`same render tick`、`raw vision viewer`、`Vision split / overlay` は現行 whitelist 側ですでに複合語として扱われているため、この report の追加最小候補には含めない。
  - 現行 whitelist の `Vision split / overlay` に対する `Vision split`、`Vision overlay`、`split mode`、`overlay mode` のような構成要素 alias は、今回の方針では同義表記ゆれではない。必要なら別 entry として審査し、不要なら本文日本語化する。

## 結果

- whitelist 更新案へ含めるべき最小候補:

```yaml
  - term: event time
    aliases:
      - event-time
    description: 入力検出の時刻を基準に処理順を決める設計語。
  - term: event-time reorder
    aliases:
      - event time reorder
    description: 入力検出の時刻で並べ替える契約名。
  - term: event-time buffer
    aliases:
      - event time buffer
    description: 入力検出の時刻順に保持する領域。
  - term: pending detection buffer
    aliases:
      - pending buffer
    description: 確定前の検出を時刻順に保持する領域。
  - term: field geometry
    aliases:
      - field-geometry
    description: 競技場描画と入力形状の基準を指す設計語。
  - term: packet timestamp
    aliases:
      - packet-timestamp
    description: 受信した記録に含まれる時刻。
  - term: receive timestamp
    aliases:
      - receive-timestamp
    description: 記録を受け取った時刻。
  - term: frame timestamp
    aliases:
      - frame-timestamp
    description: 表示または追跡結果の時刻。
  - term: tracked frame timestamp
    aliases:
      - tracked-frame-timestamp
    description: 追跡結果の時刻。
  - term: capture-time
    aliases:
      - capture time
    description: 記録時点を基準にした時刻軸。
  - term: wall-clock
    aliases:
      - wall clock
    description: 実時間の経過を基準にした時刻。
  - term: diagnostics sample tick
    aliases:
      - diagnostics-sample-tick
    description: 診断保存で入力と追跡結果を固定する単位。
  - term: diagnostics sample sidecar
    aliases:
      - diagnostics-sample-sidecar
    description: 診断保存の補助記録。
  - term: diagnostics sample timeline
    aliases:
      - diagnostics-sample-timeline
    description: 診断保存単位を時系列に並べた再生軸。
  - term: raw snapshot cadence
    aliases:
      - raw-snapshot-cadence
    description: 未加工入力の記録が更新される周期。
  - term: latest raw snapshot
    aliases:
      - latest-raw-snapshot
    description: その時点で最新の未加工入力記録。
  - term: immutable snapshot
    aliases:
      - immutable-snapshot
    description: 描画中に内容が変わらない読み取り用記録。
  - term: read-side snapshot
    aliases:
      - read-side-snapshot
    description: 読み取り側へ渡す固定済み記録。
  - term: composite read-side snapshot
    aliases:
      - composite read-side-snapshot
    description: 複数の固定済み記録をまとめた読み取り境界。
  - term: alignment sidecar
    aliases:
      - alignment-sidecar
    description: 再生位置と追跡記録の対応表。
  - term: alignment record
    aliases:
      - alignment-record
    description: 対応表に含まれる一件の記録。
  - term: selected tick
    aliases:
      - selected-tick
    description: 診断再生で現在選んでいる位置。
  - term: timeline cursor
    aliases:
      - timeline-cursor
    description: 診断再生で現在位置を示す値。
  - term: future / later snapshot
    aliases:
      - future-later-snapshot
    description: 選択位置より後にある記録。
  - term: nearest timestamp
    aliases:
      - nearest-timestamp
    description: 選択位置に近い時刻を探す規則。
  - term: loop isolation
    aliases:
      - loop-isolation
    description: 処理周期と責務を分ける設計方針。
  - term: same process
    aliases:
      - same-process
    description: 同じ処理内で動かす実行形態。
  - term: headless host
    aliases:
      - headless-host
    description: 画面を持たず入出力と処理を担う実行体。
  - term: source role
    aliases:
      - source-role
    description: 表示元の役割を示す分類。
  - term: source label
    aliases:
      - source-label
    description: 表示元を選ぶための名前。
  - term: source key
    aliases:
      - source-key
    description: 表示元を一意に扱うための値。
  - term: Field source
    aliases:
      - field source
    description: 競技場表示で使う表示元。
  - term: matching rule
    aliases:
      - matching-rule
    description: 比較に使う記録を選ぶ規則。
```

## リスク

- 未解決のリスクまたは後続対応:
  - 現行 `tools/lint/markdown-whitelist.yaml` はこの調査中に既に変更済みだったため、この report では HEAD 版の旧一覧と非 `reports/**` Markdown の実使用だけを根拠にした。
  - `description` は短い日本語案にしたが、最終登録前にユーザーの明示レビューが必要。
  - 本文日本語化優先に分類した語を本文側で直すか、登録対象に昇格するかは別 task で判断する必要がある。
