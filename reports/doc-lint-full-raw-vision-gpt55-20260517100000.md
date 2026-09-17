# Sub-agent実行レポート

## タスク

`Tracker/Design/DebugHost/raw-vision-viewer-plan.md` の Markdown lint 指摘だけを対象に修正案を作る。

## sub-agentを使う理由

対象が大きいため、`gpt-5.5 high` の worker に狭い範囲で任せるため。

## 対象範囲

`Tracker/Design/DebugHost/raw-vision-viewer-plan.md`

## 対象外

ホワイトリストの直接編集、lint に出ていない箇所の言い換え、機械的な全文置換。

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/DebugHost/raw-vision-viewer-plan.md --list-unknown`
- `npx textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/DebugHost/raw-vision-viewer-plan.md`

## 対象ファイル

- 変更: `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- 変更: `reports/doc-lint-full-raw-vision-gpt55-20260517100000.md`
- 未変更: `tools/lint/markdown-whitelist.yaml`

## 指摘事項

- 初回 `cspell`: `datagram`、`detection`、`geometry`、`project`、`vision endpoint`、`hosted service`、`multicast join`、`packet count`、`field SVG` など、本文の英語混在を指摘。
- 初回 whitelist checker: `--list-unknown` で多数の未登録語を検出。本文中の一般語、設計語、脚注ラベル、カタカナ技術語が混在。
- `textlint`: 初回、再実行とも通過。
- 対応方針: lint に出た本文箇所のうち、意味を崩さず日本語化できる一般説明語だけを行単位で修正。UI 表示名、脚注で定義済みの複合設計語、`source` のように意味を固定している語は無理に訳さず、ホワイトリスト候補に回した。
- 残った主な指摘: `source` / `source-term`、`live-state`、`mutable-state`、`immutable-snapshot-store`、`packet-timestamp`、`receive-callback`、`field-rendering-part`、`layer-group`、`viewport-state`、`diagnostics-sample-tick`、`diagnostics-sample-sidecar`、`unsupported / degraded legacy session`、`Vision`、`ibis`、`tick`、`diagnostics`、`contract`、`SVG`、およびカタカナ技術語。
- 残数: whitelist checker の `--list-unknown` は 176 語。通常出力は先頭 200 件の後に `... 376 more violations` を表示。

## 結果

- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md` の本文で、受信、保持、描画、診断再生、テスト方針の英語混在を自然な日本語へ寄せた。
- 見出し内の `Store`、`Proto`、`Field`、コンポーネントファイル名見出しの `cspell` 指摘を処理した。
- `textlint` は対象ファイルで通過。
- `cspell` と whitelist checker は未通過。残りはホワイトリスト編集禁止のため、下記候補として記録する。

ホワイトリスト候補:

- term: `source`
  aliases: `source-term`, `source key`, `source label`, `source snapshot`, `Field source`
  description: Vision の比較表示で、Layer A/B に描画する情報の由来を表す設計語。`Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` などを選ぶ単位。
  理由: 脚注で意味を定義している中心概念であり、「原典」などへ訳すと設計上の意味が崩れるため。
- term: `live state`
  aliases: `live-state`, `mutable state`, `mutable-state`
  description: 通常表示中に外部トラッカーから最後に受けた状態と、更新で内容が変わる管理器内状態。
  理由: UI が直接保持してはいけない状態境界を説明する複合語で、脚注参照が lint に残るため。
- term: `immutable snapshot store`
  aliases: `immutable-snapshot-store`, `immutable snapshot`, `immutable-snapshot`
  description: 描画中に変化しない表示用スナップショットを保持する境界。
  理由: ライブ表示の固定時点契約を表す複合語で、単語別登録より意味範囲が狭い。
- term: `packet timestamp`
  aliases: `packet-timestamp`, `receive callback`, `receive-callback`
  description: 受信データ側の時刻と、受信処理呼び出しを表す比較条件用語。
  理由: 厳密同時性を要求しない設計判断の対象語で、脚注で説明しているため。
- term: `field rendering part`
  aliases: `field-rendering-part`, `split-field-component`, `overlay-field-component`
  description: 競技場背景、境界、形状線、ボール、ロボットを描く部品境界と、分割/重ね表示用の競技場コンポーネント。
  理由: UI 部品境界の設計語であり、本文では日本語化しても脚注ラベル参照が残るため。
- term: `layer group`
  aliases: `layer-group`, `viewport state`, `viewport-state`, `split-independent-viewport`
  description: Layer A/B の描画要素をまとめるグループと、表示位置・倍率を保持する状態。
  理由: overlay/split の描画契約を表す複合語で、単語単位の登録は広すぎるため。
- term: `diagnostics sample tick`
  aliases: `diagnostics-sample-tick`, `diagnostics sample timeline`, `diagnostics-sample-timeline`, `diagnostics sample sidecar`, `diagnostics-sample-sidecar`
  description: 診断ログ記録と再生で、未加工入力とトラッカー出力を同じ保存単位として固定する tick と、その保存補助ファイル。
  理由: RuntimeHost/DebugHost 分離後の診断保存契約を表す複合語で、本文から除去できないため。
- term: `unsupported / degraded legacy session`
  aliases: `degraded-legacy-session`, `unsupported legacy session`, `degraded legacy session`
  description: 旧描画スナップショット補助ファイルしか持たず、新しい診断サンプル経路の性能保証を受けない記録セッション。
  理由: 旧形式の扱いを示す状態名で、脚注と本文の両方に残るため。
- term: `ibis`
  aliases: `ibis tracker`, `ibis トラッカー`
  description: 本リポジトリのトラッカー実装を外部トラッカーと区別して呼ぶ名称。
  理由: 固有名として使っており、一般語への置換が不自然なため。
- term: `diagnostics`
  aliases: `diagnostics overlay`, `diagnostics split`, `diagnostics time-sync regression`, `diagnostics missing regression`
  description: DebugHost の診断、再生、比較画面に関する設計語。
  理由: 既存画面名・契約名に含まれる語で、対象文書内に複数の複合語として残るため。
- term: `contract`
  aliases: `TDD contract`, `Vision split / overlay contract`, `Vision overlay color contract`, `Vision live comparison contract`
  description: テストで固定する設計上の契約。
  理由: テスト方針の見出し語として使われており、単なる一般英語ではなく設計語として残るため。
- term: `SVG`
  aliases: `field SVG`
  description: 競技場描画に使う SVG 表現。
  理由: 普通の技術名であり、lint 回避目的で不自然に言い換えない方がよいため。

## リスク

- `tools/lint/markdown-whitelist.yaml` を編集していないため、`cspell` と whitelist checker はまだ失敗する。
- 残った候補には UI 表示名、脚注ラベル、複合設計語、カタカナ技術語が混在する。ユーザー確認なしで登録すると許容語彙が広がり過ぎる可能性がある。
- `raw vision information` など一部の英語表現は脚注または既存設計語に近いため、本文側では無理に言い換えていない。
