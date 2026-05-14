# Sub-agent実行レポート

## タスク

Issue #10 設計書の用語説明を、脚注側に十分な説明を置き、用語集との重複や食い違いを避ける形に再整理する。

## sub-agentを使う理由

同じ設計用語修正の小修正であり、直前の用語修正担当 sub-agent の文脈を再利用できるため。

## 対象範囲

`Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md` の用語集と脚注、およびこのレポート。脚注には「何の説明か」が分かるように用語名を含め、結合語としての意味を説明する。

## 対象外

製品コード、テストコード、PR 本文、`Tracker/Tracker.Server/appsettings.json`、Issue #10 の挙動変更。

## 実行コマンド

`sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
`sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
`sed -n '1,220p' reports/issue-10-design-terminology-audit-r3-20260514102338.md`
`nl -ba Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md | sed -n '180,430p'`
`git diff --check`
`rg -n "^## 用語|\[\^" Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`

## 対象ファイル

変更:
`Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
`reports/issue-10-design-terminology-audit-r3-20260514102338.md`

確認のみ:
`/home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
`/home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`

## 指摘事項

独立した `## 用語` セクションが残っていると脚注と説明が二重管理になり、用語説明が食い違う可能性がある。`source`、`Raw Aggregate`、`latest-before snapshot` などの UI ラベル、識別子、既存の英語概念語は本文で維持し、説明は脚注へ集約した。脚注本文は `source: ...` の形に揃え、各脚注の先頭で説明対象の用語名が分かるようにした。

## 結果

`Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md` から独立した `## 用語` セクションを削除した。用語集にあった説明は脚注へ統合し、`Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker`、`live state`、`immutable snapshot store`、`selected replay timeline tick`、`latest-before snapshot` などを結合語単位で説明した。Issue #10 の仕様挙動は変更していない。

`git diff --check` は出力なし。`rg -n "^## 用語|\[\^" Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md` では `## 用語` が出ず、脚注参照と脚注定義だけが残ることを確認した。

## リスク

脚注数は多いが、本文へ長い括弧説明を戻さない方針を優先した。`Tracker/Tracker.Server/appsettings.json` には既存の無関係変更があるため触れていない。
