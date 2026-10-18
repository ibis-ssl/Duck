# Duck PR #62 独立レビュー報告（2026-10-18）

## 対象・結論

- 対象: https://github.com/ibis-ssl/Duck/pull/62
- PR HEAD: `70ece5452cf36a16b772e879f2b679f4e3d7746c`
- base: `design/issue18-aspire-test-orchestration`、HEAD `d766432e75c75976937b80eefd887392a23fcd45`
- 関連: Issue #38、依存PR #61（確認時点でMERGED）
- 状態: OPEN / Draft / CONFLICTING、差分58ファイル（+7,458/-620行）
- 判定: **モデル定義単体に新たな確定不具合は見つからないが、PR全体はマージ保留。** 以下の競合および実行時受入未完了が残る。

## 指摘A: 親ブランチ更新との競合（必須対応）

`git merge-tree --write-tree --name-only origin/design/issue18-aspire-test-orchestration review-pr62` で次のcontent conflictを再現した。

1. `Testing/Duck.Testing.AppHost/Program.cs`
2. `Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj`
3. `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs`

PR #61は親ブランチへ `d766432` として統合済み。一方、PR #62は以前のPR #61履歴（`b106d6d`）を取り込んだ `70ece54` に留まるため、現在の親との統合時に競合する。競合解消では親のDocker wrapper、準備完了判定、NET-009検証と、comparison modeの資源・fixture・テストの双方を保持する。変更後の新HEADで改めてCIを検証する必要がある。

## 指摘B: 比較モードの実行時readiness（設計上の残件）

`Testing/Duck.Testing.AppHost/Program.cs` の `tracker-tigers` / `tracker-erforce` は `AddContainer` + `WaitForStart`、`debug-host`も `WaitForStart` の構成である。一方 `Tracker/Design/Testing/tracker-comparison-debug-design.md` は、各trackerのowned Docker wrapper、owner label / container IDの検証、コンテナ内process、期待source identityを持つ新しいtracker packetを検証してからreadyにすることを要求する。単なるprocess startでは条件が成立しない。

現PR本文は「比較資源モデルのみ、runtime acceptanceは別途」と明記する。従ってこの未達は既知の後続スコープとして区別し、モデル実装に存在する確定不具合とは断定しない。ただしPR全体・Issue #38を受入完了やマージ可能とは扱えない。

## 検証済みの実装内容

- 比較モードだけでTIGERs、ER-Force、DebugHostの3資源が宣言され、通常モードでは追加されない。
- `simulation_protocol_comparison.xml` は読取専用マウントかつビルド出力にコピーされる。referee input `NETWORK:11003`、内蔵Game Controller `false`、referee再配信 `false`、tracker send `224.5.23.2:11010` が静的テストで確認されている。
- DebugHostは自身のtracker UDP送信を無効化し、外部tracker `224.5.23.2:11010` を受信する環境変数を登録する。
- Docker Hub APIで `roboticserlangen/autoref:2025.1.0` の有効なtag JSONを確認。digestは `sha256:de4f06a9bcfad524b3bae2015f92bb3fc3a209295eb668a34d5a0b329616bf68`。`autoref-cli`の `--vision-port`、`--tracker-port`、`--gc-port` は上流実装で宣言されている。タグ不在・未知CLI引数という指摘は不適切。
- PR #62の既存レビューでも固定版ソース照合により外部CLI契約が確認済みのため、重複指摘しない。

## テスト・CI・診断artifact

- run `39119140269` の `headSha` は対象 `70ece5452cf36a16b772e879f2b679f4e3d7746c` と完全一致。
- `.NET tests/test` は **success**。`ASPIRE-NET-002 Linux packet flow` と `ASPIRE full-stack Linux acceptance` は **skipped**。後二者を成功とは見なさない。
- `.github/workflows/dotnet-test.yml` に失敗診断artifact処理が存在。TRX、stdout、stderr、vstest診断log、binlog、終了コード、環境情報を失敗時に保存する。NET-002とfull-stackにもartifact処理がある。
- 実施したレビュー検証: `gh`、`git`、`rg`、上流ソース照合、Docker Hub API tag照会、非破壊`git merge-tree`。
- ローカルのdotnet test、Docker image pull/run、Linux multicast packet実証は未実施。モデルテストの合格を実行時動作の保証へ拡大しない。

## 対応の優先順位

1. PR #62を現在のbase `d766432e...` に統合し、3ファイルの競合を解消する。PR #61の後続修正を失わない。
2. 統合後HEADに対するビルド、モデルテスト、既存のNET-009/ownershipテスト、`git diff --check`および新HEAD一致CIを確認する。
3. 設計どおりの外部tracker wrapperとhealth/identity/packet readinessを実装したうえで、Linux環境でDuck・TIGERs・ER-Forceの3系統の独立source packet受信を証跡化する。
4. PR本文の依存先#61の状態をMERGEDへ更新する。マージは利用者が判断する。

## レビュー操作の区別

本報告は元PRのHEADとCIを変更しないため、PR #62のブランチではなく独立のreview branchに置く。レビュー自体で実装・設定は変更していない。
