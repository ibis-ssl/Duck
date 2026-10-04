# PR #28 Aspire 設計 bounded fix verification 報告（第2回）

## メタデータ

- リポジトリ: `ibis-ssl/Duck`
- 対象 PR: #28 `docs: design Aspire simulation test environment`
- レビュー種別: normal review / bounded fix verification
- 初回 reviewed design HEAD: `7a58472d527d52efceccd21b55bfde0b9d0b82db`
- 前回 fix verification reviewed HEAD: `f5950cbd5f830d2b87c8eb77fde2e9f7aef66d33`
- 前回 review-report HEAD: `3c5b90ee6858558f9cfb37bdff55adff13a46a14`
- 今回 reviewed implementation HEAD: `78959b5675aa52c310ec1776c2db5688b9cd5252`
- fix range: `3c5b90ee6858558f9cfb37bdff55adff13a46a14..78959b5675aa52c310ec1776c2db5688b9cd5252`
- 実行環境: RemoteDesktopMCP / `FA780` / Windows
- review worktree: `C:\Users\donabe\Project\Duck-issue18-aspire-design`
- source state: clean
- 判定: **pass**
- このレビューは独立最終レビューではない。

## 対象

前回未解決だった次の finding を bounded scope で再確認した。

- I28-DR-001 / Medium: logical role / source identity / live-replay source option 契約
- I28-DR-005 / Medium: 複数 ball の deterministic one-to-one assignment

I28-DR-002 / 003 / 004 は前回 closed 済みで、今回の差分が再度影響していないことだけ確認した。

## Finding completeness matrix

| Finding | Severity | Disposition | Required action | Production/design path | Focused fixture contract | 確認結果 |
| --- | --- | --- | --- | --- | --- | --- |
| I28-DR-001 | Medium | **closed** | comparison logical role を既存 `SourceRole` と分離し、role 解決入力、UUID collision、live/replay 集約契約を固定する | `tracker-comparison-debug-design.md:89-103,192,224-230` | source name 3種、UUID別、同一 role+UUID 複数 endpoint、同一 UUID 複数 role、同一 role 複数 UUID、live/replay 一致 | 必須 source name、UUID優先 identity、Ready 条件、通常 replay 契約を維持した comparison replay 解決が一意になった |
| I28-DR-005 | Medium | **closed** | gate、assignment objective、deterministic tie-break、unmatched、境界 fixture を固定する | `tracker-comparison-debug-design.md:148-156` | 2対1、1対2、greedy で pair 数が減る2対2、同率2対2、gate境界 | pair 数最大化 → 総XY距離最小化 → source-local index辞書順の順序と unmatched 条件が固定された |

## I28-DR-001 / Medium / closed

comparison mode の logical role を既存 `SourceRole` (`own` / `external` / `unknown`) から分離した。

logical role は source name から次のように解決する。

- Duck: `ibis`
- TIGERs: `TIGERs`
- ER-Force: `ER-FORCE`

名称は `StringComparison.Ordinal` の完全一致で、未定義名称を推測して role に割り当てない。source identity はその後に既存の UUID 優先規則を適用し、UUID が無い場合だけ source name + remote endpoint を代用する。

外部根拠も確認した。

- TIGERs Sumatra の `VisionTrackerSender` は `source-name` の既定値を `TIGERs` として `TrackerPacketGenerator` へ渡す。
- ER-Force framework の tracker sender は `SOURCE_NAME = "ER-FORCE"` を `TrackerWrapperPacket.source_name` に設定する。
- Duck RuntimeHost の現行 `appsettings.json` は `SourceName = "ibis"`。

`Ready` は三 logical role が互いに別の comparison source identity へ一意に解決できる場合だけ true とする。同一 UUID に複数 logical role が対応する場合と、一つの logical role に複数 UUID が同時に対応する場合は Ready にしない。同一 logical role + UUID が複数 endpoint から届く場合だけ一つへ集約し、最新 `ReceivedAt` を代表にする。

live と comparison replay は同じ logical role resolver と UUID 優先 identity 規則を使用する。一方、CaptureOn の endpoint-sensitive source key と通常 diagnostics replay の `External` / `Unknown` / source label aggregate は変更しない。前回問題だった「live の UUID 集約と replay の source label 集約をどのように両立するか」が comparison mode と汎用 replay の責務分離として固定された。

レビュー中に、起動失敗節へ旧語「三つの source role」が1箇所残っていることを検出した。実装モードへ戻して `logical role` / `comparison source identity` に揃え、commit `78959b5` として push 後、この reviewed HEAD で再確認した。

## I28-DR-005 / Medium / closed

複数 ball の一対一対応は XY 平面距離で決める。

候補 edge は、両 ball の XY が有限値で、距離が設定 gate 以下の場合だけ作る。gate と等しい距離は候補に含め、gate を越える距離は候補にしない。

assignment の優先順位は次で固定された。

1. 対応 pair 数を最大化する。
2. 同じ pair 数なら全 pair の XY 距離合計を最小化する。
3. それも同じなら `(reference source-local index, comparison source-local index)` の pair 列を辞書順比較し、最小の列を採用する。

track id は使わない。候補へ入れない ball と採用 pair に含まれない ball は unmatched とする。

focused test 契約には、2対1、1対2、局所 greedy では pair 数が減る2対2、pair 数と距離合計が同じ2対2、gate境界が明記されている。前回の「iteration order により結果が変わる」問題は設計契約上解消した。

## Validation

reviewed HEAD `78959b5675aa52c310ec1776c2db5688b9cd5252` に対する証跡:

- `git diff --check 3c5b90e..78959b5`: exit 0。
- CSpell: `tracker-comparison-debug-design.md` 1 file / 0 issues / exit 0。
- GitHub Actions `.NET tests`: run `36321258365`, head SHA `78959b5675aa52c310ec1776c2db5688b9cd5252`, completed / success。
- full `npm run lint:md`: Windows 実行環境に `xargs` が無く実行不能。成功扱いにはしていない。
- local `.NET` validation: 接続PCに `dotnet` が無いため unavailable。
- GitHub確認・コメント操作は `gh` CLI を使用し、GitHub connector は使用していない。

## Coverage

| 観点 | disposition | 根拠 |
| --- | --- | --- |
| previous finding closure | checked_no_finding | I28-DR-001 / 005 closed、I28-DR-002〜004 closed 維持 |
| requirement / design conformance | checked_no_finding | logical role / identity と deterministic ball assignment を正本設計で固定 |
| correctness / edge cases | checked_no_finding | UUID collision、role ambiguity、複数 endpoint、2対1 / 1対2 / 2対2 / gate boundary |
| changed files / direct dependencies | checked_no_finding | design diff、既存 MultiTrackerManager、live comparison、diagnostics replay を照合 |
| API / config / compatibility | checked_no_finding | 既存 SourceRole と通常 diagnostics replay 契約を変更しない |
| error handling / failure diagnostics | checked_no_finding | Ready=false 条件と既存診断情報を維持 |
| tests / validation adequacy | checked_no_finding | planned focused fixtures が required action を覆う |
| current-HEAD CI | checked_no_finding | run 36321258365 exact-head success |
| report / documentation accuracy | checked_no_finding | implementation report の当時の未認証記録は当時の事実。今回 report が現状を上書きせず追記する |
| scope discipline | checked_no_finding | review finding の設計契約と用語整合だけ |
| security / secrets | not_applicable | 設計文書のみ |
| unexplored | checked_no_finding | なし |

## 判定

**pass**

- I28-DR-001 / Medium: closed
- I28-DR-002 / Medium: closed
- I28-DR-003 / Medium: closed
- I28-DR-004 / Low: closed
- I28-DR-005 / Medium: closed
- 新規 finding: なし

## 残リスク

- AppHost / comparison mode はまだ設計段階であり、実装時にはここで固定した focused test を TDD で具体化する必要がある。
- Linux / Windows / macOS の multicast 実 packet 検証は実装段階の ASPIRE-NET 受入試験として残る。
- full Markdown lint はこの接続Windows環境では `xargs` 不在のため未完了。

## 次の作業

normal review は完了した。独立最終レビューが必要な運用では、この変更を実装していない別 reviewer / 別 chat で frozen HEAD を確認する。

merge は利用者が行う。
