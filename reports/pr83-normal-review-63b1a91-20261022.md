# Duck PR #83 通常レビュー報告

## 対象と判定

- Issue / PR: Issue #80 / Draft PR #83
- branch / base: task/aspire-docker-desktop-496 / design/issue18-aspire-test-orchestration
- reviewed implementation HEAD: 63b1a918564436c60e571bfd560c753ab042621f
- review mode: normal review
- reviewer identity: cloud thread 01a1621d-7326-7159-8ea3-ca0079bd2001
- verdict: fail。設計段階で対応が必要な P2 が1件。
- requested profile: gpt-6.1-sol / high
- applied: null。最終profileはAPIから観測できない。application_status: spawn_succeeded_profile_unverified。
- independent reservation / HEAD freeze: なし。attestation対象外。

## Finding I80-NR-001 — P2

- 対象: Tracker/Design/Testing/aspire-simulation-test-environment.md の 29、78、118–124、147、217行。
- 内容: Windowsホスト側のDuck／DebugHostと、Docker Desktop bridge内のsimulator／比較trackerの間で必須となるマルチキャスト受信経路が定まっていない。
- 影響: 現行設計の標準bridgeだけでは必須受信経路を満たせない。UDP公開、中継、host networkを使わない制約下で成立する具体方式が示されるまで、設計の受入を保留する。
- 根拠: Docker Desktop既知制限 https://docs.docker.com/desktop/features/networking/networking-how-tos/#known-limitations
- 必要対応: 現行制約を維持して成立する方式の根拠を示すか、必要な制約変更と影響を特定して利用者判断を得る。#81実装や#82実機試験はこの指摘の要求ではない。

## 既解消事項

- 旧P2（AppHostとDCPの起動・監視責務の図の不一致）: 解消済み。
- 旧P3（PR本文の技術HEADとレビュー状況の陳腐化）: 解消済み。
- これらは今回の新しいP2を解消したことを意味しない。

## 検証とレビュー範囲

- 対象HEADとPRの終了時HEADは一致していたとの確認。
- GitHub上の配置・通信・起動停止の3図を表示確認。
- Yomiyasu: 100/100、findings 0、strict 0。Sudachi許可語検査とgit diff --check: 指摘0。
- CI run 39728310400: .NET tests 476成功。packet-flow / full-stack acceptanceはskipped。
- 既存wrapper / lifecycle testの静的確認では、新たな必須コード指摘なしとの報告。
- textlint、CSpell、ローカルbuildはreviewerが再実行していない。
- Windows Desktop/DCP実動作、UDP/マルチキャスト受入、Game Controller操作、所有資源限定cleanupはheld / 未検証。
- 設計レビュー不合格と実機試験未実施を混同しない。

## 報告状態

- persistence mode: normal_persistence。
- 独立最終レビューの予約: なし。
- この報告はレビュー証拠を記録するもので、設計修正、実機受入、Issue完了、mergeを認定しない。
## I80-NR-001 修正後の追確認（2026-10-22）

- reviewer: 既存の通常レビュアー `01a1621d-7326-7159-8ea3-ca0079bd2001`。turn `01a16275-d4ef-71a6-a880-5e6c98b13c4c`。reviewed base HEAD `63b1a918564436c60e571bfd560c753ab042621f` と、未コミット3ファイル差分 SHA-256 `8ca795dfd166fd73d5084b1145e46a2344b22794fdfe95ebe3239842e477015c` を対象とした。実装側・独立最終レビューではない。
- 判定: `pass_with_held`。I80-NR-001 P2 は閉鎖、必須未解消P2なし。任意P3 I80-NR-002 は主設計11行の「資源資源定義」の重複。独立最終レビューの代わりにはならない。
- 解消確認: Duck／DebugHost server／RefereeDriver UDP observer／readiness observerをDesktopの同一DCP bridgeへ配置し、Windows AppHost/DCPとブラウザーを残す。Game ControllerとDebugHostの画面操作はlocalhost TCP。3 multicast group、Game Controller唯一producer、TIGERs/ER-Force、送信元識別・新鮮度、debug attach/capture、Crane/cm4-simのIP・loopback再作成契約、DCP localhost bindの確認事項が本文に反映され、主設計・比較設計・図の説明に明白な矛盾なしとの確認。
- 検証の扱い: textlint exit 0、`git diff --check` exit 0。主設計Yomiyasu 100/100 strict 0、比較設計91/100 strict exit 1（既存 `negative_parallelism` と `excess_list`）。CSpellとSudachiはいずれもbaselineを同じパス／設定／辞書で比較し、現行はexit 1。CSpell baseline 100件、現行101件（96件は既存行、4件は既存UI名 `Split` / `Overlay`、1件は製品名 `Tailscale`）。Sudachiでは製品・プロトコル名 `TCP` と `Tailscale` が追加指摘として記録されている。いずれも合格扱いにしない。許可一覧を変更していない。
- 図は利用者が最後に確認するため、描画・表示は未確認。ソース上の意味確認では明白な本文／図の矛盾を検出していない。Mermaid外部送信の拒否は再試行していない。
- 実機・新CIは未実施。Windows AppHost/DCP、UDP/マルチキャスト、Game Controller操作、所有者限定後始末は引き続き未検証。`report_attestation_allowed: false`。独立最終レビュー予約・freeze・attestationは本報告で実施していない。
- Dispatch profile: ユーザー指定 `gpt-6.1-sol/high`。`applied: null`、`spawn_succeeded_profile_unverified`（最終model/reasoningを示す証拠は親から観測できない）。
