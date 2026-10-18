# ASPIRE-006C2 接続・性能検証計画

## 実装範囲

- PR #69 の source resolver が解決する `Duck` / `TIGERs` / `ER-Force` の各追跡 snapshot を取得し、PR #70 の `TrackerDifferenceCalculator` へ渡す製品経路を追加する。
- 比較結果に source identity、対象 frame、Robot / Ball 差分、missing / unmatched を結び付け、計算結果がどの二つの snapshot に由来するか追跡できるようにする。
- Split / Overlay の UI render tick から差分モデルを呼ぶ配線や、描画と差分計算の snapshot pair 共有は含めない（Issue #41）。

## focused integration tests

- 3つの logical role がそれぞれ一意な source identity に解決される入力を使い、resolved snapshot から差分計算までの経路を通す。
- 一致した Robot / Ball 差分と、欠落・未対応オブジェクトの status/index が計算結果まで保たれることを確認する。
- missing role、role の重複・identity conflict、無効な frame など、経路が成立しない場合の振る舞いを確認する。
- 既存 resolver と計算モデルを再実装せず、それらの公開 API を通して検証する。

## 性能測定

- 接続経路を含む専用の再現可能な測定 harness を追加する。新しい依存 package は導入しない。
- representative packet / frame から Robot 数・Ball 数を採り、測定した入力件数を記録する。差分評価の呼出頻度は実行中の実測 source update cadence と測定時間から算出して併記する。
- warm-up 後に同じ snapshot pair を繰り返し評価し、処理時間（中央値・p95）と `GC.GetAllocatedBytesForCurrentThread` による bytes/call を記録する。
- .NET / OS / CPU、入力件数、snapshot 選択・resolver・計算のどこを含むか、warm-up と反復回数を記録する。未測定の性能目標や比較対象を達成した扱いにしない。

## 検証の順序

- 接続 seam の focused test を先に追加し、実装前の失敗を確認する。
- 接続経路、失敗時の振る舞い、測定 harness を実装し、focused tests と build を実行する。
- full-stack 検証は既存 Aspire/Linux 受入経路を再利用する。新しい環境設定が必要になった場合はそこで停止し、親へ報告する。
- 結果は実際に検証した HEAD とともに記録する。
