# PR #28 独立最終レビュー指摘対応報告

## メタデータ

- リポジトリ: `ibis-ssl/Duck`
- 対象 PR: #28 `docs: design Aspire simulation test environment`
- 作業種別: review follow-up
- 対象レビュー: `reports/pr28-independent-final-review-20260927.md`
- 独立レビュー対象 HEAD: `17a58778058cfd9d995475b15fa346d7544d0121`
- 修正開始 HEAD: `39318c3fe117f952b4db89edb642aab9c8617986`
- 技術 HEAD: `54354a97abfbc720c9849444e6920396dd3e18a3`
- 実行環境: RemoteDesktopMCP / Windows / `C:\Users\donabe\Project\Duck-issue18-aspire-design`

## 対応範囲

独立最終レビューの I28-IFR-001〜003 の required action のみを対象とした。製品コード、AppHost 実装、`Tracker/Design/tasks-status.md` は変更していない。task status は文書内で updater が限定されているため、この worker では触れていない。

## I28-IFR-001 / High

- 通常 mode / comparison mode の両方で standalone `game-controller` を `224.5.23.1:11003` の唯一の authoritative producer とした。
- `game-controller` は host network、固定 tag または digest、制御 API `127.0.0.1:8082` を契約化した。
- `referee-driver` は 11003 を publish せず API client としてだけ動作する。11003 で `HALT` を確認後、`NEXT_COMMAND`、必要に応じて `FORCE_START` または `NORMAL_START` を送り、active command を確認してから Crane の位置変化を検査する。
- comparison mode の Sumatra は `source=NETWORK` / `port=11003` / `gameController=false` / `publishRefereeMessages=false` に固定し、内蔵 Game Controller を禁止した。ER-Force 側の `--gc-port 11003` も consumer とした。
- AppHost model test、起動依存、ASPIRE-NET-007、診断ログの契約を同じ設計へ追加した。
- commit: `dd00080dcd1409d1773fe0499f4fd53d308eb1a8` `docs: define referee ownership for Aspire tests`

## I28-IFR-002 / Low

- 比較 resource 名を `tracker-tigers` / `tracker-erforce` に統一した。
- 主設計と比較詳細設計、AppHost resource、dashboard、application-model test の契約で同じ名称を使う。
- commit: `54354a97abfbc720c9849444e6920396dd3e18a3` `docs: align comparison resource names`

## I28-IFR-003 / Low

技術 HEAD `54354a97abfbc720c9849444e6920396dd3e18a3` と一致する `.NET tests` run `36329849299` は completed / success。PR 本文の Final HEAD / Exact-HEAD CI は、報告書・handoff の管理用コミットを push した後、その最終 HEAD と一致する run を確認してから更新する。別 SHA の run は代用しない。

## 検証

- `git diff --check`: success。
- CSpell: 対象2設計書、2 files / 0 issues。
- `npm run lint:md`: Windows 環境に `xargs` がなく `lint:md:text` 開始時点で blocked。成功扱いにはしていない。
- local `dotnet`: 実行環境に存在しないため未実施。
- exact-HEAD CI: technical HEAD `54354a97...` / run `36329849299` / `.NET tests` / success。
- `.github/workflows/dotnet-test.yml` は TRX、stdout、stderr、診断ログ、exit code 等を `artifacts/test-results` へ保存して upload-artifact する既存契約を持つため、診断 artifact workflow の追加変更は不要。

## 変更ファイル

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`
- `Tracker/Design/Testing/tracker-comparison-debug-design.md`
- 本報告書と handoff。

## 次の作業

本報告書と handoff を管理用コミットとして push し、その最終 HEAD と一致する CI を確認する。その後 PR 本文の Final HEAD / Exact-HEAD CI を更新し、簡易報告を PR コメントへ投稿する。I28-IFR-001〜003 の技術的 closure 判定は、同じ independent final reviewer の bounded closure verification に委ねる。merge は利用者が行う。
