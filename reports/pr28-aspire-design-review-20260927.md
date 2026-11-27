# PR #28 Aspire シミュレーション試験環境 設計レビュー報告

## メタデータ

- リポジトリ: `ibis-ssl/Duck`
- 対象 PR: #28 `docs: design Aspire simulation test environment`
- 関連 Issue: #18、#14
- レビュー種別: initial review / normal reviewer
- 対象ブランチ: `design/issue18-aspire-test-orchestration`
- ベース SHA: `449296725fc69dc004818ede2e8ad59a52ef2d27`
- reviewed implementation HEAD: `7a58472d527d52efceccd21b55bfde0b9d0b82db`
- commit range: `449296725fc69dc004818ede2e8ad59a52ef2d27..7a58472d527d52efceccd21b55bfde0b9d0b82db`
- 実行環境: RemoteDesktopMCP / Windows / `C:\Users\donabe\Project\Duck-pr28-design-review-20260927`
- ソース状態: detached HEAD、clean
- 判定: **fail**

## 目的と範囲

Issue #18 / #14、PR #28 の変更 11 ファイル、既存 RuntimeHost / DebugHost / TrackerConnectionLib 契約、Crane / TIGERs / ER-Force の外部契約を突き合わせて設計を確認した。レビューでは設計修正や製品実装は行っていない。

主要確認対象:
- `Tracker/Design/Testing/aspire-simulation-test-environment.md`
- `Tracker/Design/Testing/tracker-comparison-debug-design.md`
- `Tracker/Design/tasks-status.md`
- PR 内の report 4 件、handoff 4 件
- `Tracker.RuntimeHost` の SSL-Vision receiver / packet buffer
- `Tracker.DebugHost` の external tracker / live comparison
- `TrackerConnectionLib` の multicast receiver / source identity

## 外部契約の確認

- `roboticserlangen/autoref:2025.1.0` は Docker Hub 上で active。amd64 / arm64 が存在する。
- 同タグの entrypoint は `vnc` / `gui` 指定がない場合に `autoref-cli` を headless 起動する。設計の `--vision-port` / `--tracker-port` / `--gc-port` は CLI に存在する。
- TIGERs Sumatra の `simulation_protocol` は raw vision `224.5.23.2:10020` を受信し tracker data を `224.5.23.2:11010` へ送る。設計で使う CLI option も存在する。
- Sumatra と ER-Force はいずれも tracker packet に起動時生成 UUID と source name を設定する。
- Crane の現行 scenario compose は `crane -> UDP 12345 -> cm4-sim -> UDP 12346 -> simulator-cli`。PR #28 の経路と整合する。
- Docker Desktop host networking を Linux と同一視せず、OS ごとの実 packet 受入を要求する方針は妥当。

## Findings

### I28-DR-001 / Medium / introduced_by_change

**場所**
- `Tracker/Design/Testing/tracker-comparison-debug-design.md:85-87`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md:218`
- `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs:263`

**内容**

新しい比較設計は source identity が衝突して複数 tracker を一意に分離できない場合を「比較準備未完了」とする。一方、既存 DebugHost の正本設計と regression test は、同じ UUID が複数 endpoint から届いた場合に 1 source へ集約し、最新 `ReceivedAt` の snapshot を代表描画する契約を持つ。

**影響**

ASPIRE-006B 実装時に、既存 UUID 集約を維持するのか comparison mode だけ collision として Ready を落とすのかが一意に決まらず、どちらを選んでも片方の設計または既存 test と衝突する。

**必須対応**

comparison mode の同一 UUID / 複数 endpoint の扱いを正本設計で一つに決める。既存契約を変更する場合は `raw-vision-viewer-plan.md` の契約変更も明示し、ASPIRE-006B の TDD 項目で collision 時の Ready 状態と source option を固定する。既存集約を維持する場合は、新設計の「比較準備未完了」条件を具体化する。

### I28-DR-002 / Medium / introduced_by_change

**場所**
- `Tracker/Design/Testing/aspire-simulation-test-environment.md:65`
- `Tracker/Design/Testing/aspire-simulation-test-environment.md:234-241`
- `Tracker/Tracker.RuntimeHost/RuntimeVisionReceiverService.cs:113-115`
- `TrackerConnectionLib/src/UdpTrackerReceiver.cs:40-46`

**内容**

設計は二つ目 stack を「利用中 port の起動前検出」で失敗させる一方、`10020` / `11010` multicast は複数 receiver の同時利用を必須とする。Duck の receiver も `SO_REUSEADDR` を有効化して同一 UDP port の共有を意図している。

**影響**

単純な bind 可否や port 使用中判定では、共有可能 multicast port を競合と誤判定するか、`ReuseAddress` により second stack の検出に失敗する。OS ごとの UDP bind semantics によって ASPIRE-NET-009 の合否が変わる。

**必須対応**

排他的 port と共有 multicast port を設計上分類し、ASPIRE-NET-009 の second-stack 判定方法を固定する。必要なら stack ownership 用 lock / sentinel を使い、`10020` / `11010` の単純な in-use 判定を second-stack 検出契約にしない。

### I28-DR-003 / Medium / coverage_miss

**場所**
- `Tracker/Design/Testing/aspire-simulation-test-environment.md:234-235,243,256`
- `Tracker/Tracker.RuntimeHost/RuntimeVisionReceiverService.cs:41-58`
- `Tracker/Tracker.RuntimeHost/RuntimeVisionPacketBuffer.cs:9-51`

**内容**

ASPIRE-NET-002/003 は RuntimeHost の受信 packet count 増加を合格条件とし count を証跡保存する。しかし現行 RuntimeHost receiver は受信 packet を latest-packet buffer へ保存するだけで累積受信 count を保持・出力せず、buffer も camera ごとの最新 packet に上書きして operation loop で clear する。

**影響**

独立 packet sniffer の count では「RuntimeHost が受信した」ことを証明できず、現行 RuntimeHost の可観測面から設計要求の count を取得できない。OS 受入証跡の取得方法が実装時に未確定となる。

**必須対応**

ASPIRE-NET-002/003 の packet count を取得する production path を設計で固定する。RuntimeHost receiver 自身の受信を証明できる counter / diagnostic log / integration-test seam のいずれかを定義し、独立 sniffer の count を RuntimeHost 受信数として代用しない。

### I28-DR-004 / Low / coverage_miss

**場所**
- `Tracker/Design/Testing/aspire-simulation-test-environment.md:134-146`
- `Tracker/Design/Testing/aspire-simulation-test-environment.md:204-211`

**内容**

設計は `simulator` → `cm4-sim` → `duck` → `crane` の起動順序を要求するが、AppHost のアプリケーションモデル検査項目には待機依存の契約がない。Aspire の `WithReference` は接続情報の参照を構成するもので起動順序を制御せず、起動済み状態だけを待つ場合は `WaitForStart`、正常性まで待つ場合は `WaitFor` を使う必要がある。

**影響**

列挙済みのモデル検査をすべて満たしても各資源を並行起動する AppHost が成立し、設計が要求する起動順序を回帰テストで固定できない。UDP の正常性確認を後段へ保留することと、プロセス開始順序を固定することも区別されていない。

**必須対応**

この順序が必要なら `WaitForStart` などでどの資源間に開始依存を置くかを正本設計へ明記し、AppHost のモデル検査でその待機依存を固定する。順序が不要なら、順序を成功条件として読める記述を削除し、並行起動を許容する契約へ変更する。`WaitFor` を使う正常性確認は、現行方針どおり観測可能な正常性条件を定義してから別途導入する。

## Coverage

| 観点 | disposition | 根拠 |
| --- | --- | --- |
| requirement / design conformance | checked_finding | I28-DR-001、002、003、004 |
| correctness / edge cases | checked_finding | source collision、UDP port reuse、受信証跡、起動順序 |
| scope discipline | checked_no_finding | 変更は設計・台帳・report・handoff に限定 |
| changed files / direct dependencies | checked_no_finding | 変更11ファイルと直接依存を確認 |
| API / config / compatibility | checked_finding | I28-DR-001、002、004 |
| failure diagnostics | checked_no_finding | `.github/workflows/dotnet-test.yml` は失敗時 artifact を保存 |
| security / secrets | not_applicable | 設計文書のみ |
| tests / validation adequacy | checked_finding | I28-DR-003、004 |
| current-HEAD CI | checked_no_finding | reviewed HEAD と一致する run #141 success |
| report / tracking accuracy | checked_no_finding | ASPIRE-001 台帳と段階別 report / handoff を確認 |
| regression / maintainability | checked_finding | I28-DR-001、002、004 |

## 検証

reviewed HEAD `7a58472d527d52efceccd21b55bfde0b9d0b82db` に対する `.NET tests` run #141（run id `36291598405`）は completed / success。別 SHA の run は根拠に使用していない。

検証出力は reviewed source 外の `C:\Users\donabe\Project\_review-evidence\Duck-pr28-20260927` に保存した。

- `git diff --check 449296...7a58472...`: exit 0。
- 対象2設計書の CSpell: exit 0、2 files / 0 issues。
- `npm run lint:md`: exit 255。Windows 実行環境に `xargs` が無く `lint:md:text` で停止したため、全体 Markdown lint は成功扱いにしていない。
- Docker CLI は接続先に存在せず、container 起動試験はローカル未実施。OS 別実動作は設計上も後続実装時の受入対象。

## Held / unexplored

- Linux / Windows / macOS の ASPIRE-NET 実 packet 試験は AppHost 未実装のため現時点では未実施。設計自身が未実施 OS を対応済みと扱わないため、この設計レビューの追加 finding にはしていない。
- unexplored: なし。

## 判定

**fail**

Medium finding 3 件、Low finding 1 件。設計上の解釈分岐、port 競合判定の未定義、受入証跡の不可観測性、起動順序の契約不足を設計段階で解消してから ASPIRE-002 以降へ進む必要がある。blocking / high finding はない。

## 次の作業

I28-DR-001 から I28-DR-004 を設計へ反映する。修正後は同じ normal review chat で fix verification を行い、各 finding の required action、正本設計変更、対応 test 契約、focused evidence を確認する。

## Merge 境界

本レビューでは merge を行わない。merge は利用者が行う。
