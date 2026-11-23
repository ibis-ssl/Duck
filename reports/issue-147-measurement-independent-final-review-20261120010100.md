# 課題147 計測基盤の独立最終レビュー

## 対象と判定

- PR: [148](https://github.com/ibis-ssl/Duck/pull/148)、Issue: [147](https://github.com/ibis-ssl/Duck/issues/147)の非閉鎖参照。
- branch: `perf/issue147-measurement`、base: `main`、固定基準 `d215ec0c3b9daef978bf889c15addbe97623da59`。
- initial_independent_reviewed_head: `9c9cc3fe89393cec3ac702bc2f04769f1b9b4732`。
- closure_reviewed_heads / reviewed_implementation_head: `8715197699be27f2ba1d195fc8fd4da4ad309b17`。
- reviewer: `/root/issue147_measurement_independent`。実装担当・通常担当と異なり、実装や修正をしていない。
- verdict: **`pass_with_held`**。初回は必須1件で `fail`、同じ担当の指摘・CI差分に限る閉鎖確認で解消した。未解決の必須指摘、必須観点の未探索はない。

技術判定は上記の実装HEADに適用する。この文書は合格後に初めて保存する管理上の証跡であり、この報告だけのコミット自体を実装レビューしたとは主張しない。

## 独立性と実行プロファイル

初回は要求とコードを独立に読み、全25変更ファイル（ツール15、テスト2、進捗2、報告6）と直接依存を確認後に既存報告を照合した。網羅レビューは1回のみ。修正後は同じ独立担当がM147-IFR-001とCI差分だけを確認し、基準追加や再分解はしていない。

`decomposability: independent_workstreams`、`decomposition_policy: forbidden`、`parallelism_mode: single_agent`、`decomposition_disposition: prohibited_by_review_lifecycle`。review-enforcerとsub-agent-task-managerによる計画で、本人指定を優先し `gpt-6.2-sol/high`、`fork none`、runtime default roleを要求した。確認した設定にroleによる上書きはない。`planned_runtime_profile: gpt-6.2-sol/high`、`applied: null`、`application_status: spawn_succeeded_profile_unverified`、`profile_observability: final_profile_hidden`。継続時は `reused_existing_agent_profile` とし、元の未確認状態を維持した。Astra使用・承認は非該当。

## 所見と閉鎖確認

**M147-IFR-001 / P2 / medium / 独立網羅レビューで検出、resolved。重大度変更なし。** `ReplayRunner.cs:51` のnsから100ns刻みへの切捨てで異なる受信時刻が同値になる。製品 `RuntimeVisionPacketBuffer.cs:47` は検出を先、形状専用を後に列挙して受信時刻で安定整列するため、記録の形状専用→検出の順が反転し得た。形状変更による破棄、確定・配信、仮想遅延が変わり、同じ誤順序の反復でも品質照合は真になり得る。

再現条件は周期16ms、並べ替え5ms、統合0ms、形状変更しきい値500mm。受信1,000,000,000nsで形状9000mmと検出A（1.000秒）、1,010,000,000nsで形状12000mmだけ、1,010,000,001nsで同じカメラの検出B（1.010秒）、1,032,000,000nsで検出C（1.032秒）。記録順ならA破棄・B確定、旧実装ではA確定・B破棄となり、双方1配信でも内容と遅延が異なる。

| 必須条件 | 製品経路・実際の構成 | 証拠と処置 |
| --- | --- | --- |
| 計測側で受信順を保持し同一時刻も固定 | 周期ごとに `max(ns/100, 直前tick+1)` を製品バッファへ渡す。Core、RuntimeHost、既定設定は不変。 | 修正 `611fb67c2b9483102baf3ecd0f75d9d177edf8d4`。同じ通常担当が完全性を確認、独立担当が閉鎖した。 |
| 元nsで周期と仮想遅延を算出 | 入力の割付・受信集計は元のns。順序用tick計算は個別の入力/周期計測の外で、再生全体の経過時間/CPU内。 | RunnerとREADME、通常修正確認を照合。 |
| 実製品経路の回帰 | 合成gzip→読取→実バッファ→周期→Core→メモリー配信。同一ns/1ns差と形状専用/別カメラの4条件。 | 同一試験ソースでRed4件すべて旧検出の16msで失敗、Green24件すべて成功。後続検出の22ms又は21.999999msと置換/破棄/確定/配信/末尾件数を照合。 |
| 既存入力への影響と説明 | READMEに順序調整を記載。30秒、1カメラの既存実記録を1起動のみ確認。 | Releaseビルド0警告/0エラー。1876入力、908確定・配信、品質2回とも既知SHA一致。ソース明細SHA `8E55E5334A0644091F0724F74CE5294EE8A1EF9B9699CF8E560E353A9215AB8A` と固定blobを照合。 |
| 新しい同一HEADの正式全体ゲート | 既存Linux pull_request CI。 | `8715197` のrun44565159801成功、1537成功/0失敗/4skip。修正前CIを新HEADの証拠に転用していない。 |

Red/Green、明細、実記録の照合は作業木外 `task-23/issue147-measurement-order-fix-evidence` に保持。未コミット実行時の埋込版は9c9cc3fであり、修正後の固定blobとの対応を指紋明細で確認した。出力SHAは `330BA050FA6C52A393F68A9FD8DFD079AEF9D489304108C7987CF97D2A4DE420`。正式A/Bは当時の計測コード・SHAの証拠として保持し、新HEADでの性能再計測とは読み替えない。

## 網羅レビューのカバレッジ

| 観点 | 判定 |
| --- | --- |
| #147の要求、分割設計、PR範囲、API/既定/依存/workflow | checked_no_finding。先行は課題を閉じず、最適化は後続PR149。 |
| gzip/版/長さ/半開窓/type4/別種別/EOF/引数/失敗診断 | checked_no_finding。 |
| バッファへの受信順、形状変更・遅着・確定・遅延 | 初回checked_finding、M147-IFR-001を閉鎖。 |
| 設定解決、測定境界/分位点/CPU/割り当て/GC/実暖機 | checked_no_finding。 |
| 置換/保留/重複/配信失敗、品質hash分離、0出力拒否、3DLLの版照合 | checked_no_finding。順序への波及は上記所見で閉鎖。 |
| テストと証拠 | 初回の受信順構成不足を回帰4件で解消、局所24件と実経路を確認。 |
| 全変更と直接依存の配置、日本語XML/試験目的、UTF-8/LF、既存csprojのBOM、文書/報告/進捗 | checked_no_finding。修正差分7ファイルもUTF-8/LF、diff --check成功。 |
| 生ログ、入力パスと本文の扱い | checked_no_finding。公開差分に記録本体なし、計測JSONに入力パス/本文なし。 |
| 認証/秘密/配備/通信構成変更 | not_applicable。 |
| 同一HEADの正式CI | checked_no_finding。 |
| CPU関数別標本、UDP/OS待機/実負荷/多カメラ精度/外部比較 | held。 |
| 未探索の必須観点 | なし。 |

## 実行環境と検証経路

FA780の承認経路で `whoami=fa780\donabe` を確認した。独立担当はGitの範囲/clean状態/diff、ghのIssue/PR/CI、TRX・実行明細を読み取り照合し、追加ビルド・試験・再生を実行していない。実装と通常確認の詳細は既存の実装・修正確認報告に保持した。

全体ゲート工程は `remote_ci_only`、局所検証は利用可能なFA780実行。初回9c9の [CI44560277503](https://github.com/ibis-ssl/Duck/actions/runs/44560277503) は1533成功/0失敗/4skip、修正後8715の [CI44565159801](https://github.com/ibis-ssl/Duck/actions/runs/44565159801) は1537成功/0失敗/4skip。標準.NET、ネイティブ検査、Linux公開成果物検査が成功。ASPIREパケットフローと全体受入は条件skipであり、実機受入成功ではない。

以前のローカル全体試験はWindows1527成功/8失敗/2skip、隔離Linux1523成功/10失敗/4skipで終了値1のまま保持。Docker/OS/通信環境の制約を全体ゲート経路報告に記録し、環境を変えた再試験、socket共有、権限拡張、ネットワーク変更、試験除外は行っていない。

READMEの標準文書検査は全て0。進捗2節は静的照合と標準入力Sudachiで確認し、一式の再実行はheld。通常担当が `validation/issue147-order-normal-fix-review` にarchive・展開先・一時junction・ログを生成しjunctionを削除する補助コマンドは、読み取り範囲を越えるとして自動承認に拒否された。未実行で再試行なし。既存全文文書診断は#144へ保持する。UDP、実時間、過負荷、多カメラ、正解データ精度、外部2実装との同条件比較、CPU標本の関数別解析は未検証。16msは周期設定で達成保証ではない。

## 予約と管理上の証明

`reservation_owner: review-enforcer`、`reservation_identity: issue147-pr148-ifr-20261120010100`、`pre_reserved_report_path: reports/issue-147-measurement-independent-final-review-20261120010100.md`。予約は2026-11-19T16:01:00.1810815Z、初回凍結は同16:01:00.1934279Z。再凍結は同16:25:47.2866830Z。予約は一度だけmetadata_onlyで作り、初回fail時も再予約せず、閉鎖合格まで本ファイルは存在しなかった。外部 `reservation-pr148.json` と初回証拠を親が保持した。

report_typeはindependent_final_review_report、persistenceはreport_attestation_commit。technical_headとadministrative_parentは `8715197699be27f2ba1d195fc8fd4da4ad309b17`。commit_stateはcommit_pending、push_stateはpush_pending、ci_wait_stateはci_wait_pending。親はこの予約パスだけを変更する1コミットを作り、第一親・変更パス・追加1コミットを検証し、結果SHAをPR本文等の外部へ記録する。

以後のGitコミットは完了状態を無効とし、通常修正確認と同じ独立担当の限定閉鎖が必要となる。報告後の追跡/設計/実装/設定/Skill等の書き込みは要求しない。最終公開HEADのpull_request CIを一度確認し、mainへ統合しない。#147全体の完了と#128は親の別工程に残す。
