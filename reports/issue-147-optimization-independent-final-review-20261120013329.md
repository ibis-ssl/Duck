# 課題147 最適化の独立最終レビュー

## 対象と判定

- PR: [149](https://github.com/ibis-ssl/Duck/pull/149)、Issue: [147](https://github.com/ibis-ssl/Duck/issues/147)を参照し、先行[PR #148](https://github.com/ibis-ssl/Duck/pull/148)に積み重ねる。
- branch: `perf/issue147-near-duplicate`、base: `perf/issue147-measurement`。
- reviewed implementation HEAD: `6a5bbfcb08249104f1ac50295a639c1d0c1f03f7`。base HEAD: `afb97fcda784018644917bd19f632b7208d13d40`。
- reviewer: 親が手配した可視作業用チャットの独立担当（[レビュー作業](https://chatgpt.com/c/6aff290e-091c-8191-965c-a37141e480da)）、要求profile GPT-6.2 Sol High。
- verdict: **`pass_with_held`、必須指摘0**。実装コード・レビュー対象の変更はない。

この技術判定は実装HEAD `6a5bbf…` に適用する。本文は合格後に初めて保存した管理用の報告であり、この報告だけのコミットは実装レビュー対象に含めない。

## 差分とレビュー範囲

baseからの最適化差分は6ファイル。製品変更は `RobotTracking.cs` の近接重複判定で、捕捉ラムダの `Any` を同じ辞書順の `foreach` に置き換えた。しきい値135 mmの厳密な `<`、距離式、同じカメラ・チーム・別IDの条件、初回一致での短絡を保つ。設定既定値、公開API、通信構成、依存、workflowは変更しない。

独立担当は最適化の積み差分と直接依存、通常指摘 O147-NR-001 の解消、CIログ/TRX、A/B JSON、3 DLLのSHA/MVID、最終候補の出力照合を読み取り確認した。境界は135 mmの等号と近傍、ID・チーム・カメラ、信頼度、観測順を含む。近接重複の新規15件と既存ロボット16件、計31件はいずれもCIのTRXで成功。計測側24件も成功。

通常指摘 **O147-NR-001（P3、low）** は修正済み。順序用時刻の計算は個別の入力・周期計測外だが、再生全体の経過時間とプロセスCPU計測には含まれると報告の記述を限定した。同じ通常担当が最終報告差分と先行報告のみのmergeを確認し、必須指摘0とした。

## 正式な前後比較

正式A/Bは対象コミットA=`9c9cc3fe89393cec3ac702bc2f04769f1b9b4732`、B=`d5fdc7aad952b311d33fee7927b8e070686a9ab9`。同一計測ソース、Release、3 DLLの完全SHA/MVID、固定入力・設定を確認し、A/Bそれぞれ28パスで実行した。最終候補の計測ツールは先行PRで受信順を修正した後に出力だけを再照合した。これは性能の再測定ではなく、新HEADへ正式A/Bの数値を帰属させない。

| 指標 | A | B |
| --- | ---: | ---: |
| 周期内割り当て中央値 | 88,223,496 B | 82,528,680 B（6.45%減） |
| 周期p50中央値 | 0.90 µs | 0.90 µs |
| 周期p95中央値 | 44.75 µs | 36.95 µs |
| 周期p99中央値 | 49.25 µs | 41.10 µs |
| 周期p99観測範囲の上端 | 55.3 µs | 68.0 µs |
| プロセスCPU時間中央値 | 46.875 ms | 46.875 ms（不変） |

比較では908配信と連結出力SHA `330BA050FA6C52A393F68A9FD8DFD079AEF9D489304108C7987CF97D2A4DE420` が一致した。入力、設定、件数、その他の測定値と条件は通常レビューの[A/B報告](issue-147-optimization-normal-review-20261119235930.md)および[最適化の検証整理](issue-147-optimization-readiness-20261120011655.md)に記録した。p99の最大観測値は増加しており、CPU時間中央値の改善は観測されていない。

## CIと検証の状態

同一の実装HEAD `6a5bbfcb08249104f1ac50295a639c1d0c1f03f7` に対する[既存pull_request CI run 44567172477](https://github.com/ibis-ssl/Duck/actions/runs/44567172477)は、親が失敗jobだけを1回再実行した。

| 試行 | test job | 結果 |
| --- | --- | --- |
| 1回目 | `135704779945` | 1,551成功、1失敗、4スキップ。既存 `TimeoutKillsAndWaitsForTheOwnedCliProcess` で期待した `TimeoutException` に対して `DockerCliProcessRecoveryException` が返った。 |
| 親が再実行した2回目 | `135721004355` | 1,552成功、失敗0、4スキップ。 |

第1試行で失敗した試験とランナーはbaseと同じblobで、最適化差分に含まれない。#53に同型の先例があるとの親報告を記録するが、今回の失敗原因は未確定とする。2回目の試行に基づき先行失敗を削除せず、その現象を成功だったことにも変更しない。ASPIREパケットフローと全体受入は両試行でskipであり、実機受入を意味しない。追加再実行はない。

最終候補ではReleaseビルド、3 DLLの完全SHA照合、5秒以上の暖機後の実記録1起動による出力再照合が成功済み。1,876入力、908配信、1,005,937出力バイト、実効設定、出力SHAが先行修正版と一致した。正式A/Bを再度実行していない。

## 所見、保留、未検証

| 観点 | 判定 |
| --- | --- |
| 近接判定の135 mm厳密境界、ID・チーム・カメラ、信頼度と観測順 | checked_no_finding |
| 通常レビューO147-NR-001の解消と最終文書 | resolved |
| 新規15件＋既存16件のTRX、計測24件、3 DLL識別、A/B JSON、最終出力 | checked_no_finding |
| 同一HEAD CI | 第1試行に既存試験1件の失敗、第2試行は1,552成功/0失敗/4skip。両試行を記録し、原因未確定。 |
| private メソッドの名前とシグネチャに対する割り当て試験の依存 | held。公開 `Update` 経由の回帰試験もあるが、private境界試験自体は実装名とシグネチャに依存する。 |
| PR本文の停止表示の更新 | held、親管理の外部PR本文作業。Git差分外。 |
| 実機UDP・実時間過負荷・多カメラ、正解データを用いた追跡精度、TIGERs Mannheim／ER-Forceとの同条件性能比較 | 未検証。 |
| CPUサンプリング関数別集計 | 未検証。予備の生トレースは存在するが、関数別標本比率は算出していない。 |

## レビュー担当と確認範囲

親の連絡により、別の可視作業用チャットの独立担当がレビューを行い、親が判定 `pass_with_held`、必須指摘0を確認した。担当はGPT-6.2 Sol Highと指定された。最終適用状態を示す別のランタイム証拠はこの報告の対象外であり、推測で補わない。

このレビューが扱ったのは積み差分6ファイル、最適化と回帰の境界、通常所見の解消、CI二試行、A/B根拠、3 DLLと最終出力の照合である。レビュー中にビルド・テスト・再生の追加実行はなく、親が依頼した失敗jobだけの1回のCI再実行を超える実行はない。

## 証明コミットと公開

- reservation_owner: `review-enforcer`
- reservation_identity: `issue147-pr149-ifr-20261120013329`
- pre_reserved_report_path: `reports/issue-147-optimization-independent-final-review-20261120013329.md`
- reviewed_implementation_head / technical_head: `6a5bbfcb08249104f1ac50295a639c1d0c1f03f7`
- persistence mode: `report_attestation_commit`
- administrative parent: `6a5bbfcb08249104f1ac50295a639c1d0c1f03f7`
- report-attestation head: この本文を含むコミットの作成後、外部で記録する。
- commit state: `commit_pending`
- push state: `push_pending`
- CI wait state: `not_requested`。最終公開HEADのCI確認は親が実施する。

この報告は一度限りの管理上の証明コミットを目的とし、変更パスは予約済み報告だけとする。後続Gitコミットはこの完了証明を無効にし、通常の修正確認と同じ独立担当による限定閉鎖を要する。mainへの統合は行わない。
