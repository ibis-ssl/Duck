# Issue #10 field描画部整合 設計追補

## 対象

RAW-VISION-016 の追加要望として、Vision overlay の drag 時に片方の layer だけ表示が変わる問題を修正する。あわせて、overlay だけでなく split についても、Vision live と diagnostics の field 描画部を揃える。

## 設計判断

- overlay と split は相互に共通化しない。overlay は 1 つの field に Layer A/B を重ねる表示、split は左右に field を並べる表示として、mode ごとの画面構造を保つ。
- split 用 field コンポーネントと overlay 用 field コンポーネントは別物として切り出す。両者を 1 つの共通 component に統合する意味ではない。
- Vision live と diagnostics は、split では同じ split 用 field コンポーネントを使い、overlay では同じ overlay 用 field コンポーネントを使う。
- field / boundary / geometry / marker の描画責務は split 用 / overlay 用の field コンポーネントへ置く。source selector、timestamp metadata、missing reason、legend、layout wrapper は field コンポーネントへ混ぜず、各画面の page / wrapper / 付加 component 側に残す。
- overlay mode では、layer ごとに独立した `VisionFieldCanvas` を重ねる方針を不採用とする。1 つの overlay 用 field コンポーネントで field / geometry を 1 回だけ描き、Layer A/B の balls / robots を同じ viewport state 配下の layer group として描く。
- split mode では、左右 field の pan / zoom 同期は要件にしない。左右は比較対象を並べる別 field なので、独立 viewport のままにする。ただし Vision live split と diagnostics split は、同じ split 用 field コンポーネントと同じ marker / geometry 描画方針を使う。

## 反映先

- `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
- `Tracker/Tracker.Server/Design/tasks-status.md`
- `Tracker/Tracker.Server/Design/phases-status.md`

## TDD 方針

- overlay mode が layer ごとに独立した field canvas を重ねず、Vision live overlay と diagnostics overlay が同じ overlay 用 field コンポーネントの単一 viewport state 配下で Layer A/B を描くことを先に固定する。
- split mode は左右 viewport の独立を維持しつつ、Vision live split と diagnostics split が同じ split 用 field コンポーネントと同じ marker / geometry 描画方針を通ることを先に固定する。
- same-source collapse、missing layer、visibility、Layer A/B accent color の既存挙動を退行させない。

## 未完了

- この設計追補に基づく TDD / 実装 / build / test 検証は `reports/issue-10-field-render-alignment-implementation-20260514111723.md` に記録済み。
- gpt-5.5 high review は `reports/issue-10-field-render-alignment-review-20260514114210.md` に記録済みで、blocking findings はない。
- tracking は同期済み。PR #15 本文同期と push は後続の git / PR 更新で扱う。
