# Issue #53 finding 修正記録

## 対象

- finding: `I53-NR-EAFB-001`（レビュー対象 `eafb5ef9b2ce3a2aaff5af2eb289866f7b4d4ea4`）。
- Issue / PR: #53 / #121。
- 変更対象 HEAD: この修正と報告を含む同一コミット（報告に自己参照 SHA は記載しない）。
- scope: Duck 自身の送信元検出に割当済み ResourceId を使い、未取得時は有効な空集合として扱わない。

## 修正内容

- `TigersTrackerOutputResult` に検証済み Duck ResourceId を診断スナップショットと同時に保持。
- 自己送信元の抽出は、その同一証跡に含まれる ResourceId、network address、container ID、tracker receive sources から実施。
- resource name `duck` と allocated ResourceId `duck-wdmzzrcw` が異なる構成の回帰 fixture を追加。Duck address の decoded source を選択し、ResourceId が欠ける場合は `null` を返すことを確認。
- Crane、planner、port、protocol、AutoRef/Duck の通信動作は変更していない。

## 検証

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~MatchParticipantStatusProjectionTests --logger "console;verbosity=minimal"` — 16 passed、0 failed、0 skipped。
- `git diff --check` — clean。
- 初回テスト試行では fixture の `SuccessfullyDecodedCount` 期待値を誤って `2` とし失敗。型定義を確認して期待値を `8` に修正後、クラス全体が通過した。
- build 中の既存 warning: NU1902 (MessagePack advisory), ASPIRE010 (Aspire CLI bundle disabled), nullable / obsolete / xUnit analyzer warnings。

## 未確認・残件

- これはローカルのコード・回帰テスト確認であり、Linux/Docker MATCH の実測ではない。
- 旧 run `42481939519` は旧 HEAD `a881af3` の証拠であり、今回の修正後 HEAD の実受入証拠に転用しない。
- ここでは実際の Duck 送信を主張しない。#54 POST_GAME と raw packet capture はこの修正の条件外。
- 次工程: 同じ通常 reviewer に finding と関連 composition の fix verification を依頼する。

## 永続化状態

- persistence mode: normal persistence。
- commit state: 実装・テスト・報告を含む同一コミットに記録済み。
- push / CI wait: 未実施。
