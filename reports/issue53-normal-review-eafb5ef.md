# Issue #53 通常レビュー

## 対象と判定

- レビュー種別：初回通常レビュー。対象リポジトリ：ibis-ssl/Duck。
- 対象 HEAD：eafb5ef9b2ce3a2aaff5af2eb289866f7b4d4ea4、branch task/issue53-aspire-006g3。
- PR #121、base task/issue50-aspire-006g。レビュー時の remote PR head は対象 HEAD と一致。
- 対象は Issue #53 の受入条件と設計、全19変更ファイル、直接依存、検証と成果物。
- #54 の POST_GAME は対象外。Issue #53 に明記されていない raw packet capture を必須条件にしていない。

## 実行・profile evidence

- reviewer: /root/issue53_normal_review_eafb5ef。新規の通常 reviewer で、実装参加なし。独立最終レビューではない。
- 実行者 fa780\donabe。PowerShell、git / gh 読取に成功。作業ディレクトリ C:\Users\donabe\Documents\Codex\2026-11-07\task\Duck-issue53-aspire-006g3。
- 起動時に tool 引数として model=gpt-6.1-sol、reasoning_effort=high、fork_turns=none を指定。runtime が返す最終 profile は観測できず、applied profile は未確認（spawn_succeeded_profile_unverified）。指定と実適用を同一視しない。
- reviewer はテスト、Docker、commit、push、PR編集・コメント、mergeを実行していない。対象 CI と既存成果物を読取確認した。

## Dispatch profile（親所有）

- 判定：初回レビュー、判断を要する cross-system の重要なレビュー。observed decomposability は independent_workstreams。
- 実行方針：review lifecycle により decomposition を禁止し、reviewer 1名で実行。
- requested: gpt-6.1-sol high、fork_turns=none。ユーザーが通常・独立レビューについて指定済み。
- runtime default role を使用。tool に明示 role 引数なし。利用可能なユーザー config に role mapping は見つからず、default-role の profile 影響は不明。
- requested profile は tool 引数で指定済み。final applied profile は null、状態は spawn_succeeded_profile_unverified / final_profile_hidden。

## 確認した要件と変更範囲

Issue #53 / ASPIRE-MATCH-004 の条件は、Sumatra の UDP 11010 出力、AutoRef の UDP 10020 / 11003 入力と 11010 出力、Duck の 11010 非送信を、対象 run に紐づく件数・記録で確かめること。現行 task / source audit は AutoRef の 11010 を出力として扱う。古い history table に入力とする表記もあるため、方向差を記録した。追加の 11010 受信数や raw packet capture は完了条件に加えていない。
レビューでは base 9756f9dd35ce05cf18d6ad91eaf277ad917fcfe9 から対象 HEAD までの全19変更ファイルを読み、AppHost、受信診断、container/resource identity、native log recorder、workflow / artifact、テスト、task/phase tracking、報告を確認した。Crane、planner、packet protocol は変更差分にない。

## Findings

### I53-NR-EAFB-001 — medium — Duck 自身の送信元判定に割当済み ResourceId を使う

- 発生箇所：Testing/Duck.Testing.AppHost/MatchParticipantStatusProjection.cs の ExecuteAsync caller と identity predicate、Tracker/Tracker.Tests/MatchParticipantStatusProjectionTests.cs の fixture。
- 問題：caller が expectedResourceId に model name duck を渡す一方、診断 identity は割当済み duck-wdmzzrcw を保持する。完全一致条件のため、Duck の IP から届いた正当な packet も派生 self-source list から除外される。
- 影響：空の duckSelfDecodedTrackerSources が「Duck から該当 packet なし」と「identity mismatch」を区別できず、正の自己送信例を検出できない。この欠陥は検出能力の欠落を示すが、Duck が実際に送信した証拠ではない。独立して保存される full duckTrackerReceiveSources と AutoRef / Sumatra の計数は残る。raw capture も要求しない。
- 根拠：run 42481939519 の artifact は model resourceId=duck、割当済み currentResourceId=duck-wdmzzrcw、診断 identity.resourceId=duck-wdmzzrcw を記録し、派生 self list は空。network address は 172.18.0.8。同じ production file の正の publisher 判定は duckTarget.ResourceId を使う。
- 頻度：観測した DCP resource naming では派生欄への影響は決定的。ただし1000人利用時の月間発生頻度や、実際の誤送信件数を示す母集団・利用統計はない。その頻度は断定しない。要求された非送信検出の肯定ケースを取りこぼすため、検出能力の修正を必須と判断。
- 必要対応：診断 source と同時に検証済みの割当済み Duck ResourceId を保持して判定に渡す。identity 未取得は、有効な空結果と区別する。model name と割当済み ID が異なる実 caller 構成、Duck address の decoded source、誤 identity 拒否を確認する回帰 fixture を追加する。修正後の HEAD に検証を結び付ける。#54 と raw capture は対象外。

## Coverage

| 項目 | 判定 | 根拠 |
| --- | --- | --- |
| 要件・設計適合 | checked_finding | 正の出力経路は確認。self-source 検出の ID 不一致を発見。 |
| 正しさ・境界 | checked_finding | model name と allocated ID が異なる。正の readiness 経路は target ID を使用。 |
| scope / unrelated changes | checked_no_finding | MATCH 限定。Crane、planner、protocol の変更なし。 |
| 全変更ファイル・依存 | checked_finding | 全19ファイルと直接依存を確認。指摘は projection と fixture の caller 組成にある。 |
| API / data / config / workflow | checked_finding | optional JSON 項目、recorder、mount、upload を確認。派生 ID 判定は欠陥。 |
| failure handling | checked_no_finding | Java 起動失敗、archive summary、sanitization の経路を確認。 |
| security / secret / cleanup | checked_no_finding | runごとの resource ownership と sanitizer-before-upload を確認。secret 値は報告に含めていない。 |
| テスト・validation | checked_finding | 既存 helper test が実 caller の異なる ID を組成していない。reviewer はテストを実行していない。 |
| exact-HEAD CI | checked_no_finding | run 42483036726 は対象 HEAD で成功。Docker jobs は skip。 |
| live evidence の HEAD | checked_no_finding | MATCH run 42481939519 は a881af3。対象 HEAD で実行したとは主張しない。 |
| reports / tracking | held | 古い intake / phase / PR 記述に現況と異なる文や古い状態が残る。時系列の記録は保持し、現況文を同期する。 |
| regression / maintainability | checked_finding | 上記 identity composition defect。 |
| independent review / merge | not_applicable | この通常レビューでは実行なし。 |

## 検証・成果物

- exact-HEAD CI run 42483036726: eafb5ef、PR event、success。dotnet 1,359 passed / 4 skipped、Linux publish success。Docker / full-stack job は skip。これは build evidence であり、実対戦の非送信証明ではない。
- Linux MATCH run 42481939519: 実行 HEAD は a881af369678be9f86d8c5fa4cf9a384bfb22c4f。対象 eafb5ef とは report-only difference だったが、HEAD が違うため完全一致の証拠には転用しない。
- artifact は AutoRef native log 5,250,922 compressed bytes、referee 16,427、vision detection 26,346、tracker 0、EOF 到達・読取失敗なし。これは AutoRef が記録した decoded input records で全raw datagram数ではない。
- Duck observer は Sumatra IP 172.18.0.5:54467、UUID 288679d8-e7eb-4ee5-a838-e28979c9e8f7、33,427 decoded、AutoRef 172.18.0.6:55649、UUID 56917106-fc43-4d2e-a4da-455709e0bf1e、33,061 decoded、decode failure 0 を保存。後刻取得した全source snapshot で Sumatra は33,445。別時刻の snapshot count で矛盾ではない。送信先 224.5.23.2:11010 も log に記録。
- Duck IP 172.18.0.8 と一致する endpoint は全source listにない。MATCH config は Tracker__PublishUdp=false。これは observer と設定に基づく範囲限定の推論であり、任意の UDP egress 不在を測るraw captureではない。壊れていた派生 self list は追加の根拠にならない。
- archive gate は stop_exit_code=0、container_running=false、candidate_count=1。AutoRef log capture / summary と sanitization / upload は成功。match-acceptance.json が pass とする範囲は MATCH-001/002。fullMatchPassed=false / MaximumTimeReached は #54 側であり、本レビューで #53 条件に追加していない。
- 既存 artifact は読み取ったが、この reviewer は archive を再取得・hashせず、別の decode tool も実行していない。実際の Duck 誤送信は観測しておらず、その頻度も不明。

## 保留・残件・永続化

- verdict: fail。medium finding 1件を要修正。判定は eafb5ef9b2ce3a2aaff5af2eb289866f7b4d4ea4 のみに結び付く。
- H53-EAFB-001: 現況記述を phases / PR と同期する。履歴の intake/native log audit は消さず、日付付き証跡として残す。
- H53-EAFB-002: exact-HEAD live MATCH は eafb5ef で未実施。前回 a881af3 の結果を付け替えない。実装変更後は影響範囲に応じた新しい検証が必要。
- 未実施として保持：reviewer自身の test/build/Docker、fresh artifact download/hash、binary-source reproducibility、raw egress capture、runtime recovery experiment。未実施を成功扱いにしていない。
- 次の処理：I53-NR-EAFB-001 の修正、その finding に限定した同じ reviewer の fix verification。独立最終レビューは未開始。
- 保存先 reports/issue53-normal-review-eafb5ef.md。通常レビュー報告は修正と修正確認の記録と共に保存済み。独立レビュー用 attestation / merge 権限は付与しない。
