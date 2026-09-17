# PR #20 実装修正の分岐分離報告

- Repository: ibis-ssl/Duck
- PR: #20 `docs: add RuntimeHost README and shared appsettings guide`
- 利用者指示: PR #20 は設計修正に限定し、C# の製品コード・テストコード変更を別分岐へ退避する。
- 分離前 current HEAD: `6d19ff2bf0d136f8c2b89d2f72127b53424ea101`
- 設計側の基点: `868fe673093679639b0cc072b7c670b8b48cddda`
- 実装退避先: `fix/pr20-diagnostics-sample-runtime`
- 実装退避先 HEAD: `6d19ff2bf0d136f8c2b89d2f72127b53424ea101`

## 分離対象

`868fe67` 以降の5コミットは、独立確認から派生した実装修正とその検証・再確認記録だけであることを確認した。

- `bd0b08c` `fix(diagnostics): prefer sample sidecar for default fields`
  - `TrackerDiagnosticsComparisonViewStateReader.cs`
  - `VisionPacketCaptureTests.cs`
- `f5902f9` 実装修正の検証・進捗・詳細report
- `07c49ee` 実装経路に対するclosure再確認report
- `f159232` `fix(diagnostics): keep sample replay when tracker sidecar fails`
  - `TrackerDiagnosticsComparisonViewStateReader.cs`
  - `VisionPacketCaptureTests.cs`
- `6d19ff2` 2回目の実装修正の検証・進捗・詳細report

設計書5文書、README 3文書、原出現2,670件の台帳そのものはこの5コミットで変更されていない。

## 実施内容

1. 分離前 current HEAD `6d19ff2...` を `fix/pr20-diagnostics-sample-runtime` としてremoteへ保存した。
2. PR #20 側は実装修正開始前の `868fe67...` を基点に戻す。
3. PR #20 の作業状況・工程状況へ、実装変更を別分岐へ分離した事実を追記する。
4. PR #20 の最終差分に製品コード・テストコードを含めない。

## 境界

実装側で確認された不整合を「存在しなかった」「解消済み」とは扱わない。別分岐に保存し、必要なら設計PRとは独立した実装作業として扱う。

PR #20 では設計書・README・台帳・文書検査の修正と、その独立確認だけを扱う。mergeは利用者が行うため実施しない。
