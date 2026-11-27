# Sub-agent実行レポート

## タスク

- 目的: `tools/lint/markdown-whitelist.yaml` の alias から、親項目の意味を広げる怪しい登録を調査する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指定により、怪しい alias の調査を専用サブエージェントへ分担するため。

## 対象範囲

- 対象:
  - `tools/lint/markdown-whitelist.yaml`
  - 非 `reports/**` Markdown 内の alias 実使用箇所

## 対象外

- 対象外:
  - `tools/lint/markdown-whitelist.yaml` の編集
  - Markdown 本文の編集
  - lint script の変更

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - `sed -n '1,260p' reports/doc-lint-whitelist-suspicious-alias-audit-20260517090027.md`
  - `sed -n '1,260p' tools/lint/markdown-whitelist.yaml`
  - `nl -ba tools/lint/markdown-whitelist.yaml | sed -n '1,220p'`
  - `nl -ba tools/lint/markdown-whitelist.yaml | sed -n '220,280p'`
  - `rg --files -g '*.md' -g '!reports/**'`
  - `rg -n --fixed-strings --glob '*.md' --glob '!reports/**' '<alias候補>'`
  - `node -e "const fs=require('fs'); const yaml=require('yaml'); const doc=yaml.parse(fs.readFileSync('tools/lint/markdown-whitelist.yaml','utf8')); for (const e of doc.entries) if (e.aliases) console.log(e.term+'\\t'+e.aliases.join(' | '));"`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/doc-lint-whitelist-suspicious-alias-audit-20260517090027.md`
  - 確認: `tools/lint/markdown-whitelist.yaml`
  - 確認: `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - 確認: 非 `reports/**` Markdown。主に `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`、`Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`、`Tracker/Design/Core/tracker-architecture-plan.md`、`Tracker/README.appsettings.md`、`Tracker/Tracker.DebugHost/README.md`、`Tracker/Tracker.RuntimeHost/README.md`。

## 指摘事項

- 指摘要約または「指摘なし」:
  - 怪しい alias 一覧:
    - `same-source`: `same-source 1 layer`
      - 理由: `same-source` は Layer A/B が同じ source を選んだ状態名だが、alias は挙動説明の複合句であり同一表示名ではない。
      - 推奨対応: alias から削除。本文では日本語で「1 layer 表示にまとめる」と説明するか、必要なら別概念として登録する。
    - `saved-session-alignment`: `saved alignment record`
      - 理由: 親 term は保存済み対応表全体、alias は 1 件の record を指す表現で、粒度が異なる。
      - 推奨対応: alias から削除。必要なら `alignment record` を独立 entry として登録する。
    - `source`: `source selection`, `source selector`, `source filter`
      - 理由: `source` という一般語を親にして UI 操作・部品・絞り込み条件をまとめて許可しており、意味が広すぎる。
      - 推奨対応: alias から削除。本文側は「表示元選択」「表示元 selector」「source filter」など用途ごとに日本語化または独立 entry 化する。
    - `WorldFrameCommitted`: `world frame`, `world-frame`
      - 理由: 親 term は通知名だが、alias は追跡結果の frame 概念そのものを指す。構成要素の単独許可に近い。
      - 推奨対応: alias から削除。`world frame` が必要なら別 entry とするか、本文日本語化を優先する。
    - `.NET SDK`: `.NET`
      - 理由: 親 term は開発環境名だが、alias は製品・実行基盤全体を指し得る。
      - 推奨対応: alias から削除。`.NET` は必要なら独立 entry として登録する。
    - `Layer A/B`: `Layer A`, `Layer B`
      - 理由: 親 term は左右対象をまとめた表示名だが、alias は片側だけの構成要素を単独許可する。
      - 推奨対応: alias から削除。必要なら `Layer A` と `Layer B` を別 entry にする。
    - `latest-before snapshot`: `latest-before hold`
      - 理由: `snapshot` と `hold` は同じ概念ではなく、直前標本を保持する挙動説明へ意味が広がる。
      - 推奨対応: alias から削除。本文は「直前 sample の hold」など説明文として扱う。
    - `Tracker:ActiveProfileName`: `ActiveProfileName`
      - 理由: 実使用では設定 JSON のプロパティ名として `ActiveProfileName` 単体も出るため同一設定を指す場合は alias 可。ただし一般プロパティ名としても成立するため、設定文脈外の許可を広げるリスクがある。
      - 推奨対応: 維持可だが、より厳密にするなら `ActiveProfileName` を「設定プロパティ名」として独立 entry 化する。
    - `Tracker:Profiles`: `Tracker Profiles`
      - 理由: colon から空白への表記ゆれではなく、設定階層名を一般名詞句へ広げる。
      - 推奨対応: alias から削除。必要なら本文を `Tracker:Profiles` に寄せる。
    - `Tracker:Profiles`: `Tracker:Profiles:<name>`
      - 理由: 同じ設定階層内の placeholder 付き表記であり、同一設定を指す表記ゆれとして許容可能。
      - 推奨対応: 問題なし寄り。必要なら独立 entry 化せず維持でよい。
    - `AutoRef mode`: `AutoRef`
      - 理由: 親 term は実行形態だが、alias は自動判定機能または製品的な短縮名として使われる。
      - 推奨対応: alias から削除。`AutoRef` は必要なら独立 entry として登録する。
    - `raw vision viewer`: `raw vision`
      - 理由: 親 term は画面の設計語だが、alias は未加工映像入力の一般概念で、本文でも `raw vision packet`、`raw vision 入力`、`raw vision receiver` など広く使われている。
      - 推奨対応: alias から削除。`raw vision` は独立 entry 化するか、本文を「未加工入力」へ日本語化する。
    - `overlay layer contract`: `overlay layer`, `layer contract`
      - 理由: どちらも契約名そのものではなく構成要素を単独許可している。
      - 推奨対応: alias から削除。`overlay layer` は必要なら独立 entry 化、`layer contract` は日本語化を優先する。
    - `same render tick`: `UI render tick`, `ui-render-tick`
      - 理由: `same render tick` は同一描画時点の比較契約、`UI render tick` は Blazor UI の表示更新単位であり、同一概念ではなく関連概念。
      - 推奨対応: alias から削除。`UI render tick` は独立 entry として登録する。
    - `tracker source snapshot`: `tracker snapshot`, `tracker-snapshot`
      - 理由: 親 term は source 付きの追跡器由来記録だが、alias は source 制約を落としている。
      - 推奨対応: alias から削除。`tracker snapshot` が必要なら独立 entry とする。
    - `latest-before fallback`: `diagnostics latest-before fallback contract`
      - 理由: 親 term へ diagnostics と contract を足した長い契約名で、単純な表記ゆれではない。
      - 推奨対応: alias から削除。必要なら独立 entry とする。
    - `JSON`: `JSONL`
      - 理由: 別形式の略語であり、同じ識別子・同じ表示名ではない。
      - 推奨対応: alias から削除。`JSONL` を独立 entry として登録する。
    - `UDP`: `HTTP`, `HTTPS`
      - 理由: 同カテゴリの通信方式を 1 つの親 term に束ねているだけで、同じ概念ではない。
      - 推奨対応: alias から削除。`HTTP`、`HTTPS` を独立 entry とする。
    - `UUID`: `ID`, `NIC`, `OS`, `TDD`, `PR`
      - 理由: 略語というカテゴリ共通性だけで束ねており、親 term の意味を大きく広げている。特に `ID` は一般語としても広い。
      - 推奨対応: alias から削除。それぞれ独立 entry 化し、`ID` は本文日本語化または用途限定の複合語登録を優先する。
    - `ms`: `ns`, `mm`, `rad`, `Hz`
      - 理由: 単位というカテゴリ共通性だけで束ねており、同一概念ではない。
      - 推奨対応: alias から削除。それぞれ独立 entry 化する。
  - 削除推奨 alias:
    - `same-source 1 layer`
    - `saved alignment record`
    - `source selection`
    - `source selector`
    - `source filter`
    - `world frame`
    - `world-frame`
    - `.NET`
    - `Layer A`
    - `Layer B`
    - `latest-before hold`
    - `Tracker Profiles`
    - `AutoRef`
    - `raw vision`
    - `overlay layer`
    - `layer contract`
    - `UI render tick`
    - `ui-render-tick`
    - `tracker snapshot`
    - `tracker-snapshot`
    - `diagnostics latest-before fallback contract`
    - `JSONL`
    - `HTTP`
    - `HTTPS`
    - `ID`
    - `NIC`
    - `OS`
    - `TDD`
    - `PR`
    - `ns`
    - `mm`
    - `rad`
    - `Hz`
  - 別 entry として独立登録すべき候補:
    - `.NET`
    - `JSONL`
    - `HTTP`
    - `HTTPS`
    - `TDD`
    - `PR`
    - `UI render tick`
    - `raw vision`
    - `world frame`
    - `alignment record`
    - `tracker snapshot`
    - `Layer A`
    - `Layer B`
    - `ns`
    - `mm`
    - `rad`
    - `Hz`
  - 本文日本語化すべき候補:
    - `source selection`、`source selector`、`source filter`: 表示元選択、表示元 selector、表示元絞り込みなどへ寄せる。
    - `same-source 1 layer`: 「同一表示元の場合は 1 layer 表示にまとめる」など説明文へ寄せる。
    - `latest-before hold`: 「直前標本の保持」「直前 sample の hold」など、挙動説明として書く。
    - `layer contract`: 「layer 契約」または具体的な契約名へ寄せる。
    - `ID`: 用途に応じて「識別子」「設定 ID」「camera ID」など複合語または日本語へ寄せる。
  - 問題なしと判断した代表例:
    - `Tracker.DebugHost`: `tracker-debug-host`、`tracker debug host`、`Tracker DebugHost` はドット、ハイフン、空白、大小文字差で同じ実行体名を指す。
    - `Tracker.RuntimeHost`: `Tracker RuntimeHost`、`tracker-runtime-host`、`tracker runtime host` は同じ実行体名を指す。
    - `SSL-Vision`: `SSL Vision` はハイフン有無の表記ゆれ。
    - `ASP.NET Core`: `ASP NET Core`、`ASPNETCORE` は同じ製品名の表記ゆれ。
    - `SSL_WrapperPacket`: `SSL WrapperPacket` は同じ型名の区切り違い。
    - `3rd party tracker`: `third-party tracker`、`third party tracker`、`3rd-party tracker` は同じ設計語の表記ゆれ。
    - `ER-Force`: `ER-FORCE`、`ER Force` は同じ名称の大小文字・ハイフン違い。
    - `Fast Forward`: `FastForward`、`fast forward` は同じ操作名の表記ゆれ。
    - `Capture On` / `Capture Off`: `CaptureOn` / `CaptureOff` は同じ操作名または状態名の表記ゆれ。

## 結果

- 結果:
  - `tools/lint/markdown-whitelist.yaml` の alias を確認し、親 term の意味を広げる疑いが強い alias を 33 件抽出した。
  - 本文確認では、`raw vision`、`UI render tick`、`ActiveProfileName`、`TDD`、`HTTP`、`ID`、`Hz`、`Tracker:Profiles:<name>` などが非 `reports/**` Markdown に実使用されていることを確認した。
  - whitelist、Markdown 本文、lint script は編集していない。

## リスク

- 未解決のリスクまたは後続対応:
  - このレポートは提案のみであり、whitelist の削除・独立 entry 化・本文日本語化は未実施。
  - `tools/lint/markdown-whitelist.yaml` は調査開始時点で既に変更済みだったため、既存差分の作成者・意図は未確認。
  - `reports/**` は対象外指定に従い本文使用確認から除外した。reports 内だけで使われる alias は今回の実使用確認に含めていない。
