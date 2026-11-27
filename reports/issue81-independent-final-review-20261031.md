# 課題 #81 / PR #84 独立最終レビュー

## 1. 対象と判定

判定は `pass_with_held`。必須指摘は0件で、今回の原要件と最新の受入方針に対する必須実装残件はない。保留4件は第5節に記録する。これは技術判定であり、本人の最終確認、GitHubの正式承認、統合、課題の終了を含まない。

```yaml
review_mode: independent_final_review
reviewed_implementation_head: 9ca6a64ecbc6737d04820d14c6dbfe63f33ce0c5
initial_independent_reviewed_head: 9ca6a64ecbc6737d04820d14c6dbfe63f33ce0c5
closure_reviewed_heads: []
tree: 8403337f85ac9a3ffe7ea6bf241e12d2face62d2
base: d67d2a4d3389445ea387b1935d0c79b411f3adc4
branch: task/issue81-aspire-desktop-bridge
reviewer_id: I81-FINAL-REVIEWER-01
requested_model: gpt-6.1-sol
requested_reasoning_effort: high
applied_profile: null
profile_observability: final_profile_hidden
fork_policy: none
exhaustive_pass_count: 1
decomposability: independent_workstreams
decomposition_policy: forbidden
parallelism_mode: single_agent
verification_capability: local_execution_available
required_findings: []
finding_completeness_matrix: []
severity_reclassification_records: []
verdict: pass_with_held
```

独立確認担当は実装と通常レビューに参加していない新しい1名で、他の担当へ分担していない。指定設定と実際の適用設定は区別する。実行識別子との対応と詳細な原本は非公開の検証記録に保持する。

[課題 #81](https://github.com/ibis-ssl/Duck/issues/81)の原条件、方式BとB′、本人の最新の正常系優先方針を照合した。PR #76の統合 `c72939ec` が基準版の祖先であることも確認した。#82全体や任意の固定デバッガー調査を追加条件にはしていない。

## 2. 変更前の問題と実装の要点

Craneが待つ準備状態の更新に必要な通知監視が、資源起動を待つサービスより後に開始されていた。既存通知監視を `StartingAsync` で先に開始し、後の `StartAsync` では二重起動を防ぐ。所有確認を省略する変更ではない。

その後の初回宛先適用は、CM4の検査照合で送信前に拒否されていた。既存ログへ適用結果の番号、名前、段階、対象IDと世代、設定世代、具体的理由、時刻を接続した。番号は `Unknown=0`、`Applied=1`、`Rejected=2`、`OutcomeUnknown=3` の固定値である。

検査テンプレートとDocker操作命令による終端改行を表す再現試験で、修正前の拒否を再現した。読取処理は末尾の空行だけを除去する。内部6項目、完全ID、実行と役割、単一ネットワーク、アドレスの厳格な照合は維持する。

検証器の接続先、プロセス限定探索先、有限観測、反射取得、終了値記録の修正は製品差分と区別した。失敗時の生出力と旧検証器原文がないため、改善の寄与を製品読取処理だけに断定しない。

## 3. 独立確認の範囲

固定実装版の全6573ファイルを分類した。報告群の6424ファイルと、それ以外の149ファイルを区別した。証拠内の実装の複製と実行する実装を区別した。生ログは、実装と主張の裏付けに必要な範囲を確認した。

| 基準 | 判定 | 確認内容 |
| --- | --- | --- |
| 原要件と採用設計 | `checked_no_finding` | 専用作業場所、基準版、設定変更の境界、所有対象だけの回収、原因への対処、文書検査。 |
| 全変更集合と直接依存 | `checked_no_finding` | `Testing` 36、`Tracker`製品24、試験62、設計台帳4、Cの部品 15、実行定義等8。 |
| 正常系 | `checked_no_finding` | 通常5資源、起動順、所有確認、初回適用、DuckとCraneの受信、停止回収。 |
| 受付から取消・終了 | `checked_no_finding` | 通知と世代、確認応答、遠隔無効化、再起動順、標準入出力の回収、`UDS`受付継続。 |
| APIと設定・互換性 | `held` | 既定経路に必須指摘なし。非既定選択はH001とH002。 |
| 異常処理と診断 | `checked_no_finding` | 既存の取消、期限、旧通知、所有不一致、不正入力、出力上限、番号付き適用結果。 |
| 安全性と所有境界 | `checked_no_finding` | 構造化引数、完全IDとラベル、読み取り専用配置、限定`UDS`、`127.0.0.1`の動的`TCP`。 |
| 試験と検証 | `checked_no_finding` | 順序再現試験と追加改行再現試験、世代・取消・所有・復号・配布の正負例。 |
| 対象実装版のCI | `checked_no_finding` | CI 41160878957が対象実装版と一致して成功。 |
| 報告と台帳・文書 | `checked_no_finding` | 実測とCIの時系列、設定世代と資源世代の区別、最終8検査と入力一致。 |
| 回帰と保守性 | `checked_no_finding` | 既存機構を再利用し、新しい管理・監視・所有台帳を必須化していない。 |
| 判定を妨げる未探索 | `not_applicable` | 必須確認範囲に未探索の残りはない。 |

## 4. 検証結果

確認担当は既存証拠を読んだ。追加の試験、ビルド、コンテナ起動、UDP実行、設定変更は行っていない。

```yaml
normal_run: normal-run-20261031-inspect-newline-01
started_utc: 2026-10-31T02:12:21.2742336Z
ended_utc: 2026-10-31T02:12:45.1009964Z
process_id: 20304
exit_code: 0
elapsed_seconds: 23.827
resources: [simulator, duck, game-controller, cm4-sim, crane]
initial_apply: Applied
configuration_epoch: 1
cm4_generation: 5
simulator_generation: 6
duck_successfully_decoded: 99
duck_decode_failures: 0
duck_age_ms: 5.1286
crane_max_valid_packet_count: 321
crane_invalid_packet_count: 0
crane_age_ms: 189.2002
owner_scan_exit: 0
remaining_ids: []
host_udp_receiver_created: false
```

Craneの321はロボット間の最大値であり、合計ではない。現在の所有IDと宛先を照合し、120秒以内に停止した。配置した補助実行物と起動補助は回収済みである。

実測は製品コミット `6b7d04fb` の内容に対応する。後続は設計書と台帳だけで、指定の実装・試験4ファイルの指紋は一致した。AppHost `DLL`もビルド、試験出力、検証器の読込先で一致した。

```text
AppHost DLL SHA256
A3200B37CD7839C53F2794A279262A2497C06838869788435831F9C6F369608E
normal run stdout SHA256
774DED9488C9A2C99A6236DCCBE38B836686C44064876A1134BA30C31450B5A9
normal run stderr SHA256
E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855
```

順序と改行の再現試験は修正前失敗と修正後成功を記録した。関連34試験、`Release` ビルド、設計書の4検査、最終台帳の8検査が成功した。台帳だけの差分には、既存CIと不変指紋を再利用した。追加の手元全試験を成功したとは扱わない。

[対象実装版のCI 41160878957](https://github.com/ibis-ssl/Duck/actions/runs/41160878957)は成功した。標準試験、Cの部品と起動補助、発行検査、配布資材保存が成功し、`packet-flow`と`full-stack`の処理は省略だった。Windowsでの今回の正常系実測は、CIの省略と区別する。

Simulatorの短い実行末尾の表示は、復号増分を待つ基準点である。登録情報の世代 7と識別情報の受渡しの世代 1は別の管理系統であり、検証器の表示だけを製品の永続故障と解釈しない。

## 5. 保留事項

| ID | 場所・条件 | 影響と扱い |
| --- | --- | --- |
| I81-IFR-H001 | `BaseSimulationResourceRegistration.cs:174`、`transport_server_posix.c:33`、`transport_core.c` の転送先選択。標準構成の `Testing:Simulator:IbisPort` を12346以外へ変更する場合。 | 設定値と12346固定補助実行物の口が一致しない。上書き試験は比較構成を対象とする。採用B′の固定契約と既定の成功経路を区別する。利用頻度は未測定で、閾値以上・以下とは認定しない。この選択を受入する際に、設定拒否か対応範囲を決める残件である。 |
| I81-IFR-H002 | `CraneTargetAddressSettings.cs:26`。標準構成の自動起動補助選択は `visibility_graph` だけ。 | 他経路計画方式で未指定なら元の同一コンテナ内を指す宛先が残る。今回の成功経路には該当せず、その選択の通信成功は主張しない。新しい実測や検査は要求しない。 |
| I81-IFR-H003 | 正常系1回の実測範囲。 | 再作成後の実通信、比較構成、他の起動選択、長時間安定性、CM4サービス準備完了は未確認。一回の成功から拡張せず、新しい停止条件にも加えない。 |
| I81-IFR-H004 | 原本と由来の限界。 | 失敗検査の生出力、失敗実行 `normal-run-20261031-initial-apply-log-01`の終了値、実行別の旧検証器原文と入力はない。`DLL`差の寄与を厳密に分離できず、読取処理だけを唯一原因とは認定しない。過去のTDDと原本不足も回復済みにはしない。 |

上記は独立確認から得た保留事項で、今回の必須修正はない。本人の基準は1000人全体で月1回程度以上の不具合を修正対象とするもの。頻度不明を独断で分類せず、新たな負荷試験や故障注入は追加していない。発見した不具合の表示は、処理、対象、具体的理由を後から追跡できることを要する。

## 6. 報告の固定と公開境界

```yaml
reservation_owner: review-enforcer
reservation_identity: duck-issue81-pr84-independent-final-20261031-01
reserved_at_utc: 2026-10-31 02:40:33 UTC
frozen_at_utc: 2026-10-31 02:41:04 UTC
reserved_report_path: reports/issue81-independent-final-review-20261031.md
reservation_before_review: metadata_only
report_created_by_reviewer: false
report_persistence_mode: report_attestation_commit
administrative_parent: 9ca6a64ecbc6737d04820d14c6dbfe63f33ce0c5
report_attestation_head: commit_pending
report_attestation_allowed: true
remaining_required_code_actions: []
unexplored_verdict_blockers: []
```

この文書は、上記実装実装版の技術判定を記録する管理上の追記である。報告コミット自身を実装としてレビューしたことを意味しない。

親は実装実装版の直後に、予約したこの報告だけを変更するコミットを一つ作る。最初の親コミット、変更ファイルの許可範囲、後続コミットがないことを確認し、公開後の正確なコミットのCIを確認する。報告コミットのIDと最終CI結果は、コミット作成後の引継ぎで記録する。

実行実装、開発手順、設計、設定、台帳等の後続変更は、この完了判定を無効化する。その場合は通常確認と同じ独立確認担当の限定確認へ戻る。PRコメントの再投稿、GitHubの正式承認、統合、課題の終了は実施しない。
