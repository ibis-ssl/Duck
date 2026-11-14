# PR #63 独立最終レビュー（2026-10-06）

## レビュー識別と対象

- レビュー種別: independent final review
- レビュー担当: `/root/pr63_independent_final`（実装・通常レビュー修正の担当とは別に、このレビューのため起動した担当）
- 対象リポジトリ: `ibis-ssl/Duck`
- PR: [#63](https://github.com/ibis-ssl/Duck/pull/63)、branch `task/pr61-crane-probe-20261005` → `task/pr28-aspire-005`（Draft、レビュー時OPEN）
- レビュー対象実装HEAD: `9f64981de6339bf009657f45e24cf8f1c2156faa`
- 対象範囲: Issue [#37](https://github.com/ibis-ssl/Duck/issues/37) / ASPIRE-005、親Issue [#18](https://github.com/ibis-ssl/Duck/issues/18)
- PRの変更ファイル23件と、基準点から対象HEADまでの差分、要件、設計、workflow、テスト、報告、現HEADのCI・受入証拠を確認した。
- PR全体の基準点SHAは `2094d035dabd11c58ec2175af8cc3a0548d86cad`。実装受入の比較に重要なSHAは `236eb38eb5f8f6ba5399060d52ed4c344140aeeb`。
- レビューは読み取り専用で実施し、テスト再実行、編集、commit、push、PR作成、mergeは行っていない。

## 独立性と継続性

このレビュー担当はPRの実装者でも、通常レビューやその修正を行った担当でもない。実装担当から独立した別担当として割り当てた。N7/N8/N9はレビュー開始時点で親側が解消確認済みと伝達していた。今回の担当は現HEADの該当文言を確認し、復元された「シミュレータ」の表現による具体的な回帰を認めなかった。このレビューは元の指摘の識別子や重大度を変更しない。

## カバレッジ

| 観点 | 判定 |
|---|---|
| Issue要件・設計との適合 | `checked_no_finding` |
| 準備判定、retry、境界条件 | `checked_no_finding` |
| stack所有権、cleanup範囲、失敗処理 | `checked_no_finding` |
| 診断の直接保存経路と秘密情報の秘匿 | `checked_no_finding` |
| workflowとテストの確認範囲 | `checked_no_finding` |
| 現HEADのCI・実行証拠の評価 | `checked_no_finding`（現HEADでfull-stackを実行したとは判定していない） |
| 文書・報告の整合性 | `checked_no_finding` |
| 変更行の許可一覧検査 | `held` |

担当はwrapperの準備経路、Craneのsetup markerとnode名の一致条件、診断ファイルへの直接保存とcleanup時のsanitizer、所有containerの検証、active referee/motion/tracker acceptance harness、startup failure時のworkflow診断とcleanup順を独立に確認した。実装・設計上の新規findingはなかった。通常レビューで記録されたN5の診断輸送上の不足は、直接ファイルへ保存する経路で解消している。secret-field sanitizerのmultiline処理も双方で確認した。

## Findings、保留、未探索

- 新規の実装・設計finding: なし。
- **保留**: 現設計書の該当行（`Tracker/Design/Testing/aspire-simulation-test-environment.md:179`）にある「シミュレータ」2箇所は、現許可一覧に未登録。対象行のcspellは0件だが、SudachiPy許可一覧検査はこの2箇所を報告している。辞書・文言はこのレビューで変更しない。この文書lint残件をIssue #64と調整する。
- **現HEADで未実行**: full-stack Linux acceptanceとASPIRE-NET-002。current-head runでは両jobがskipである。これは未実行として記録し、成功扱いしない。
- Issue #64の未公開作業は確認・取り込みしていない。
- 実行環境を使った独立再受入は行っていない。

## 検証証拠と適用範囲

1. [run 37450191388](https://github.com/ibis-ssl/Duck/actions/runs/37450191388) はレビュー対象HEAD `9f64981de6339bf009657f45e24cf8f1c2156faa` と一致する。.NET testsは成功、ASPIRE-NET-002とASPIRE full-stack Linux acceptanceはskip。
2. [run 37299169992](https://github.com/ibis-ssl/Duck/actions/runs/37299169992) は `236eb38eb5f8f6ba5399060d52ed4c344140aeeb` を対象に、.NET tests、ASPIRE-NET-002、ASPIRE full-stack Linux acceptanceがすべて成功した。このrunでは準備待ち、active-motion/tracker acceptance、cleanup、evidence uploadの各stepも成功している。
3. `git diff 236eb38eb5f8f6ba5399060d52ed4c344140aeeb 9f64981de6339bf009657f45e24cf8f1c2156faa` は次の5ファイルだけで、42 insertions / 7 deletions。
   - `Tracker/Design/Testing/aspire-simulation-test-environment.md`
   - `Tracker/Design/tasks-status.md`
   - `cspell.config.jsonc`
   - `reports/pr63-doc-lint-followup-20261005.md`
   - `tools/lint/markdown-whitelist.yaml`
4. 同範囲にproduct code、test、workflow、固定image指定、起動引数の変更はない。この差分比較からrun 37299169992を現HEADのランタイム実装と同等な証拠として参照できるが、**現HEADでfull-stackが実行・成功した証拠ではない**。
5. [run 37244425991](https://github.com/ibis-ssl/Duck/actions/runs/37244425991) と `06794bf5e4a6a51242e9dcb74c418489113d2d89` の実通信受入は補助的な過去証拠であり、今回の対象HEAD実行とは区別する。

## 判定と残るリスク

**Verdict: `pass_with_held`**。レビュー対象HEADに新しい実装findingはない。許可一覧違反を文書lint残件として明示的に保留する。別SHAのfull-stack成功は差分で確認したランタイム同等性を支えるが、現HEADのfull-stack実行としては主張しない。

次の判断は、Issue #64の公開・検査成果とこの2語の許可一覧残件をどう扱うかである。レビュー報告が記録されても、PRはDraftのままでありmerge承認を意味しない。

## 報告コミット（attestation）

- 予約済み報告パス: `reports/pr63-independent-final-review-20261006.md`
- 本報告の技術判定が適用されるのは `reviewed_implementation_head: 9f64981de6339bf009657f45e24cf8f1c2156faa` のみ。
- 本報告はその実装HEADの後に作る単一の管理用report-attestation commitとして保存する。attestation commitはこの予約パスだけを変更し、first parentはレビュー対象HEADとする。
- attestation SHAはcommit後にPRコメントで記録し、報告内へ事前記入しない。
- attestation後に別commitが追加された場合、この判定は新しい実装内容へ自動適用されず、通常の差分検証と新しい独立最終レビューが必要。
- 本報告を作成する変更では、製品、テスト、設計、workflow、設定、task tracking、handoffを変更しない。
- mergeは行わない。
