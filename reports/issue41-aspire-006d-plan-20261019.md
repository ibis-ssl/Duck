# Issue #41 / ASPIRE-006D 作業計画

基点: `design/issue18-aspire-test-orchestration` の `a5105173bb2527f0d7147eb44eeca562a8bd3033`。依存Issue #40 は完了済み。親Issue #18 / PR #28 の範囲に従い、変更はこの専用ブランチから親統合ブランチへ提案する。

## 実装方針

1. `Tracker.DebugHost` の live Compare で描画と数値比較が別々に snapshot を取得している箇所を追い、1 UI render tickで固定した同一のsnapshot pairを共有する境界を特定する。
2. Split / Overlay と robot / ball の数値差分を、同じsnapshot pairから生成する。比較計算へ渡す間に受信側の最新状態が更新されても、一回の描画内の表示は変わらないようにする。
3. tracker timestamp、受信時刻、snapshot age と source 間のtimestamp deltaを表示し、時刻差を物体差から独立して確認できるようにする。既存の汎用CompareとDiagnostics表示契約は保つ。
4. 対象を絞ったテストで「描画・比較が別の時点を読める」現状を先に失敗として再現する。小さなモデル境界の変更ごとにbuildし、その後でfocused test、Markdown lint、yomiyasuを実行する。
5. テストの前提を示す失敗確認と実装を別commitにする。最終HEADを固定して、実行結果・警告の採否とともにレビュー用に報告する。mergeは行わない。

## 受入確認

- 一回のUI更新で固定した同じsnapshot pairがSplit / Overlayと数値差分の双方へ使われる。
- 各sourceの時刻情報およびsource間のtimestamp deltaが物体差とは別に表示される。
- 対象テスト、build、正式Markdown lint、yomiyasuの結果を最終HEADに対応付ける。

## 初期Draft PR時点

この計画だけを含み、実装・検証は未実施。Issueの `Closes #41` を付けるが、PRのbaseは既定ブランチではないため、GitHubによる自動closeはbaseが既定ブランチへ統合されるまで発生しない。
