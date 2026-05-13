# TRACKER-055-057 diagnostics UI / performance plan

## 目的

- `/diagnostics` で 100MB を超える tracker snapshot sidecar を含む session を扱っても、timeline scrubber / playback が実用的に追従するようにする。
- `Tracker Comparison` panel を主役にせず、Field 表示で ibis / 3rd party tracker の差を確認できる UI に戻す。

## 固定タスク

- `TRACKER-055`: diagnostics playback / scrubber の低速問題を解消する。
- `TRACKER-056`: `Tracker Comparison` を折り畳み可能にし、左右 Field の source を任意に切り替える。
- `TRACKER-057`: Field 重ね合わせ表示を追加する。want 扱いだが、`TRACKER-056` の model を再利用できる範囲で実装する。
- `TRACKER-053`: PR #9 ready 化。`TRACKER-057` までの完了後、または overlay を明示 defer した後に実行する。

## 設計方針

- 低速問題は `TRACKER-055` で先に閉じる。現在の遅さは playback interval だけではなく、selected entry の変更ごとに tracker snapshot sidecar を読み直す経路があることが主因なので、scrub / playback tick では sidecar size に比例した I/O / parse を発生させない。
- 100MB は上限ではなく、1-2分程度の capture で到達しうる通常サイズとして扱う。巨大 sidecar では全ロード前提に寄せすぎず、file path / last write time / length を cache key にした lightweight index、background preload、per-source/time lookup、cancellation、bounded memory policy を検討する。ただし今回は正常系優先のため、DB 化や大規模 storage 導入は避け、既存 JSONL sidecar を活かす。
- 通常 Play は実 timestamp delta を残す一方、調査用に速度倍率または fast playback を明示的に選べるようにする。既存 Play / Fast Forward の停止・末尾リセット・stale tick guard は維持する。
- `TRACKER-056` では `Tracker Comparison` panel を初期 collapsed またはユーザー操作で collapsed にし、左右 Field の selector を Field header 側へ置く。既定は現状維持の `Vision Input` vs `ibis tracker` とし、3rd party / unknown / source label を左右どちらにも選べるようにする。
- `TRACKER-057` は want として、左右比較とは別に overlay mode を追加する。Field source model が整った後に、色分け / legend / visibility を最小限で足す。

## 受け入れ条件

- `TRACKER-055` 完了時点で、140MB / 29k records 級およびそれ以上の tracker snapshot sidecar を scrub / playback tick ごとに再読込しない。
- `TRACKER-055` 完了時点で、通常再生と調査用高速再生をユーザーが区別して使える。
- `TRACKER-056` 完了時点で、数値中心の `Tracker Comparison` panel を閉じたまま、Field で 3rd party tracker snapshot を見られる。
- `TRACKER-057` 完了時点で、選択した tracker sources を同じ Field に重ねて表示できる。重ね合わせが過剰に複雑になる場合は、review 前に defer 判断を report に明記する。

## 実行ルール

- 実装・追加調査・レビューは gpt-5.5 high sub-agent を使う。
- 各 task は focused tests、実装 report、gpt-5.5 high review report、progress sync、commit / push を単位に閉じる。
