# PR #28 Aspire 設計レビュー指摘対応報告（第2回）

## メタデータ

- リポジトリ: `ibis-ssl/Duck`
- 対象 PR: #28 `docs: design Aspire simulation test environment`
- 作業種別: review follow-up
- 対象レビュー記録: `reports/pr28-aspire-design-fix-verification-20260927.md`
- 修正開始 HEAD: `3c5b90ee6858558f9cfb37bdff55adff13a46a14`
- 技術 HEAD: `0254b44ae00558f1eebb35e8e22b3315b5a3f5ea`
- 実行環境: RemoteDesktopMCP / Windows / `C:\Users\donabe\Project\Duck-issue18-aspire-design`

## 対応範囲

I28-DR-001 と I28-DR-005 の required action のみを対象とした。製品コード、既存の汎用 DebugHost replay 契約、他タスクの文書は変更していない。

## I28-DR-001 対応

- 既存 `SourceRole` (`own` / `external` / `unknown`) と comparison logical role (`Duck` / `TIGERs` / `ER-Force`) を分離した。
- source name `ibis` / `TIGERs` / `ER-FORCE` を logical role へ `StringComparison.Ordinal` で解決する契約を固定した。
- logical role 解決後に既存の UUID 優先 identity を適用し、同一 identity の複数 role、同一 role の複数 identity を `Ready` にしない。
- live と replay は同じ logical role resolver を使う一方、CaptureOn の endpoint-sensitive key と通常 diagnostics replay の source-label aggregate は維持する。
- UUID collision、role ambiguity、live/replay 一致の focused test 契約を追加した。
- コミット: `8b04217` `docs: define tracker comparison role resolution`

## I28-DR-005 対応

- ball matching は XY 平面距離を使い、gate と等しい距離を候補へ含める。
- assignment は pair 数最大化、総 XY 距離最小化、source-local index の辞書順 tie-break の順で決定する。
- 2 対 1、1 対 2、greedy で pair 数が減る 2 対 2、同率 tie、gate 境界の focused test 契約を追加した。
- コミット: `0254b44` `docs: define deterministic ball matching`

## 検証

- `git diff --check 3c5b90e..0254b44`: exit 0。
- CSpell: `tracker-comparison-debug-design.md` 1 file / 0 issues / exit 0。
- `npm.cmd run lint:md`: exit 255。Windows 環境に `xargs` が無く全体 Markdown lint は実行不能。成功扱いにはしていない。
- `gh auth status`: exit 1。RDMCP 実行環境の `gh` は未認証。現在 HEAD の GitHub Actions run は確認できておらず、別 SHA の run は代用していない。

## 次の作業

同一 normal reviewer が I28-DR-001 と I28-DR-005 の bounded fix verification を行う。merge は利用者が行う。
