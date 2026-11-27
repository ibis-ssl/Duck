# PR #28 Aspire 設計 fix verification 報告

## メタデータ

- リポジトリ: `ibis-ssl/Duck`
- 対象 PR: #28 `docs: design Aspire simulation test environment`
- レビュー種別: fix verification / normal reviewer
- reviewer continuity: 前回 I28-DR-001〜004 を報告した同一チャット
- 初回 reviewed design HEAD: `7a58472d527d52efceccd21b55bfde0b9d0b82db`
- 前回 review-report HEAD: `e39ee1ebb3c930d0f67105b98ced70ce6e71d45b`
- 今回 reviewed implementation HEAD: `f5950cbd5f830d2b87c8eb77fde2e9f7aef66d33`
- fix commit range: `e39ee1ebb3c930d0f67105b98ced70ce6e71d45b..f5950cbd5f830d2b87c8eb77fde2e9f7aef66d33`
- 実行環境: RemoteDesktopMCP / `FA780` / Windows
- review worktree: `C:\Users\donabe\Project\Duck-pr28-rereview-20260927`
- source state: detached HEAD / clean
- 判定: **fail**

## 対象

修正差分は次の4ファイル。

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`
- `Tracker/Design/Testing/tracker-comparison-debug-design.md`
- `reports/pr28-aspire-design-review-fixes-20260927.md`
- `handoffs/pr28-aspire-design-review-fixes-20260927.yaml`

前回 finding I28-DR-001〜004 の required action、既存 DebugHost / RuntimeHost 契約、今回変更箇所の sibling case を確認した。

## Finding completeness matrix

| Finding | Severity | Disposition | Required action | 確認結果 |
| --- | --- | --- | --- | --- |
| I28-DR-001 | Medium | **not closed** | source identity collision 時の Ready / source option 契約を既存 DebugHost と矛盾なく一意化する | UUID 集約方針自体は既存 live 契約へ寄せられたが、Duck / TIGERs / ER-Force の logical role を source identity から解決する規則が無く、comparison mode では既存 `SourceRole` が三者とも `external` になる。また live は UUID 集約、diagnostics replay は既存実装・設計上 source label 集約であり、source option 契約が一意でない |
| I28-DR-002 | Medium | **closed** | 共有 multicast port と排他 port を分離し、second-stack 判定を固定する | `10020` / `11010` を共有 multicast port とし、stack ownership lock と占有制御 port 検出へ分離。focused test 契約も追加済み |
| I28-DR-003 | Medium | **closed** | RuntimeHost 自身の受信 packet count を観測できる production path を固定する | `VisionPacketsReceivedTotal`、診断ログ、NET-002/003 の前後差分、DebugHost 既存 packet count の利用まで固定済み |
| I28-DR-004 | Low | **closed** | 起動順序を Aspire の開始依存として固定し、モデル検査へ落とす | `WaitForStart` の依存グラフと `cm4-sim` 無効構成のモデル検査を固定済み。Aspire の現行 `WaitAnnotation` / `WaitForStart` 契約とも整合 |

## I28-DR-001 / Medium / not closed

### 根拠

`tracker-comparison-debug-design.md:87-89,176,208-211` は、同一 UUID / 複数 endpoint を一つの source に集約し、comparison mode の `Ready` を Duck / TIGERs / ER-Force の三 logical role が別 source identity へ解決できること、と定義した。

しかし既存の `TrackerConnectionLib/src/MultiTrackerManager.cs:78-93` の `SourceRole` は次の分類だけを行う。

- DebugHost 自身と一致: `own`
- UUID と source name の両方が空: `unknown`
- それ以外: `external`

comparison mode では DebugHost 自身を `debug-host-observer` とするため、Duck、TIGERs、ER-Force はすべて `external` になる。新設計は packet の UUID / source name / AppHost resource のどれを使って logical role `Duck` / `TIGERs` / `ER-Force` へ割り当てるかを定義していない。TIGERs は現行 upstream で source name `TIGERs`、ER-Force は `ER-FORCE`、Duck は現在 `ibis` だが、その値を role 解決契約として固定していない。

さらに live と replay の既存契約が異なる。

- live: `VisionLiveComparisonViewState` は `TrackerSourceIdentity.CreateUuidPreferredKey` を使い UUID 優先で集約する。
- diagnostics replay: `TrackerDiagnosticsComparisonViewStateReader` は source option / Field source を `SourceLabel` で group 化する。
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md:96` も保存上は endpoint-sensitive key、UI は source label aggregate を正本としている。

修正後設計の「source option も通常は UUID ごとの集約 source を一つだけ表示する」を live のみとするのか replay にも適用して既存契約を変更するのかが定義されていない。

### 影響

ASPIRE-006B / 006E の実装時に、

- 三 logical role の割当方法
- `Ready` の判定方法
- live と replay で同じ source identity をどう表現するか
- UUID 同一 / source name 相違時の source option

が実装者判断になる。前回 finding の「collision 時の契約を一意化する」という required action は完了していない。

### 必須対応

次を正本設計で固定する。

1. comparison logical role (`Duck`, `TIGERs`, `ER-Force`) と既存 `SourceRole` (`own`, `external`, `unknown`) を別概念として定義する。
2. logical role を packet / AppHost resource から解決する規則を固定する。source name を使う場合は期待値・変更時の扱いを固定する。
3. live と diagnostics replay の source identity / source option の集約キーを明示する。
4. 既存 DebugHost の source-label replay 契約を変更する場合は `debug-host-cli-ui-detail-design.md` と既存回帰テストの更新を後続 TDD 契約へ含める。
5. 同一 UUID が複数 logical role に割り当たる fixture を、実際の logical-role resolver を通して `Ready=false` になるテストとして定義する。

## I28-DR-002 / Medium / closed

`aspire-simulation-test-environment.md:65-67,221,249` で以下が固定された。

- `10020` / `11010` は `SO_REUSEADDR` 前提の共有 multicast port。
- 二重 stack は AppHost の host-local ownership lock で resource 起動前に拒否。
- lock file の存在ではなく排他 handle の取得可否で判定。
- 外部競合検出は占有制御 port に限定。
- focused test で二つ目の失敗、最初の終了後の再取得、共有 multicast port を判定に使わないことを固定。

前回 required action を満たす。

## I28-DR-003 / Medium / closed

`aspire-simulation-test-environment.md:223,242-243,251,266` で以下が固定された。

- RuntimeHost receiver が正常 decode 後に buffer へ渡した packet の累積値 `VisionPacketsReceivedTotal`。
- endpoint / interface / 累積値の production diagnostics。
- NET-002/003 は RuntimeHost の counter 前後差分を使用。
- 独立 sniffer count は代用不可。
- DebugHost は既存 `VisionPacketSnapshot.PacketCount` を使用。

既存 `VisionPacketStore` は累積 `packetCount` を保持しているため、DebugHost 側の証跡経路も存在する。前回 required action を満たす。

## I28-DR-004 / Low / closed

`aspire-simulation-test-environment.md:136-148,214-215` で開始依存と正常性確認を分離した。

既定 `visibility_graph`:

- `simulator`: 先行
- `cm4-sim` / `duck`: `simulator` へ `WaitForStart`
- `crane`: `cm4-sim` と `duck` へ `WaitForStart`

`cm4-sim` 無効時:

- `crane`: `duck` のみへ `WaitForStart`

Aspire の現行仕様でも `WaitForStart` は resource が開始したことを待つ `WaitAnnotation` であり、健康状態を待つ `WaitFor` と区別される。モデル検査項目にも依存を追加しているため、前回 required action を満たす。

## I28-DR-005 / Medium / introduced_by_change

### 場所

- `Tracker/Design/Testing/tracker-comparison-debug-design.md:132-140`
- `Tracker/Design/Testing/tracker-comparison-debug-design.md:212-216`

### 内容

複数ボール時の仕様は「位置距離に基づく一対一対応」「距離 gate 超過は unmatched」までしか定義しておらず、候補が競合した場合の assignment objective と tie-break が無い。

official `TrackedFrame` は `repeated TrackedBall balls` を持ち、`CAPABILITY_DETECT_MULTIPLE_BALLS` も定義されているため、複数ボールは protocol 上の実ケースである。

例えば gate 内に一つの比較先 ball があり、基準側の二つの ball がどちらも候補になる場合、処理順に greedy matching すると、どちらを matched / unmatched にするかが iteration order で変わる。複数候補がある場合は位置差の数値結果も変わり得る。

### 影響

同じ snapshot pair でも実装方式や collection order により、

- matched / unmatched
- ball ごとの位置・速度差
- UI 表示順

が変わる。ASPIRE-006C の TDD expected result を一意に定められない。

### 必須対応

複数 ball の一対一対応について少なくとも次を固定する。

- gate の適用方法
- assignment の最適化目的（例: gate 内で match 数を最大化し、その後総位置距離を最小化）
- 同率時の deterministic tie-break（例: packet 内 index の辞書順）
- unmatched の決定規則
- 2対1、1対2、2対2同距離、gate 境界値の focused test

特定アルゴリズム名を必須にする必要はないが、結果が一意になる契約は必要。

## 検証

reviewed HEAD `f5950cbd5f830d2b87c8eb77fde2e9f7aef66d33` に対する evidence:

- GitHub Actions `.NET tests` run #148 / run id `36297572886`: **completed / success**。`head_sha` は reviewed HEAD と一致。
- `git diff --check 449296725fc69dc004818ede2e8ad59a52ef2d27..f5950cbd5f830d2b87c8eb77fde2e9f7aef66d33`: exit 0。
- 対象2設計書 CSpell: 2 files / 0 issues / exit 0。
- full `npm run lint:md`: review worktree に `.agents/skills/review-enforcer/scripts/list-markdown-targets.js` が存在せず実行経路を構成できないため未完了。成功扱いにしない。
- local `.NET` / Docker / Aspire runtime: 接続PCに `dotnet` / Docker / Aspire CLI が無いため unavailable。
- validation evidence: `C:\Users\donabe\Project\_review-evidence\Duck-pr28-fixverify-20260927`

## Coverage

| 観点 | disposition | 根拠 |
| --- | --- | --- |
| previous finding closure | checked_finding | I28-DR-001 open、I28-DR-002〜004 closed |
| requirement / design conformance | checked_finding | I28-DR-001、I28-DR-005 |
| correctness / edge cases | checked_finding | logical-role resolution、live/replay identity、multiple-ball assignment |
| changed files / direct dependencies | checked_finding | fix diff 4 files、MultiTrackerManager、live comparison、diagnostics replay、VisionPacketStore を照合 |
| API / config / compatibility | checked_finding | I28-DR-001 |
| network acceptance / observability | checked_no_finding | I28-DR-002 / 003 closed |
| startup dependency | checked_no_finding | I28-DR-004 closed |
| tests / validation adequacy | checked_finding | I28-DR-001 / 005 の planned TDD contract が不足 |
| failure diagnostics workflow | checked_no_finding | 既存 `.NET tests` workflow は失敗診断 artifact を保持 |
| current-HEAD CI | checked_no_finding | run #148 exact-head success |
| scope discipline | checked_no_finding | fix は設計・report・handoff のみ |
| security / secrets | not_applicable | 設計変更のみ |
| unexplored | checked_no_finding | なし |

## 判定

**fail**

- I28-DR-001 / Medium: not closed
- I28-DR-002 / Medium: closed
- I28-DR-003 / Medium: closed
- I28-DR-004 / Low: closed
- I28-DR-005 / Medium: new finding

High / Blocking finding はない。

## 次の作業

I28-DR-001 と I28-DR-005 を正本設計へ反映する。修正後は同じ normal review chat で bounded fix verification を行う。

## Merge 境界

本レビューでは merge を行わない。merge は利用者が行う。
