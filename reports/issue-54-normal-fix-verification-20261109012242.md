# Issue #54 通常レビュー修正確認

## 対象

- Issue: #54 / ASPIRE-006G4
- PR: #122（Draft）
- ブランチ: `task/issue54-aspire-006g4`
- base: `task/issue50-aspire-006g` / `c9f693e5d32da89e8a986609016bdd088f110dbd`
- 実装 HEAD: `7c4d9dc66e946a106f677c3fc396647198acd7d6`
- レビュー: 同一の通常レビュー担当による I54-IFR-001 / I54-IFR-002 の限定 closure
- 判定: `pass_with_held`。両 finding は closed、必須指摘は残っていない。

## 修正と証拠

| Finding | 対応 | 根拠・状態 |
|---|---|---|
| I54-IFR-001 | Crane の記録コピーを sanitizer の対象外に保持し、通常 archive または .NET 回復 archive が成功した後だけ削除。両方失敗した場合と部分的な docker cp 失敗でも raw bytes と失敗状態を別 artifact に残す。 | 実処理と同じ `capture_crane_recordings` を fixture から呼び、圧縮失敗時のバイナリ内容保持を照合。closed。 |
| I54-IFR-002 | SSL `.log.gz` を finalizer より先にコピーし、finalizer 失敗時は raw copy を残す。 | `preserve_ssl_before_finalization` と失敗する shell finalizer stub を fixture で組み合わせ、失敗戻り値と byte 一致を確認。closed。 |

発生頻度は実測されていないため unknown。月1回/利用者1000人の閾値を満たすという推定はしていない。両指摘は頻度評価ではなく Issue #54 の既存失敗時ログ保存要件との整合に基づき修正した。

## 検証

- `bash -n scripts/match-recording-preservation.sh scripts/tests/test-match-recording-preservation.sh`: 成功。
- `bash scripts/tests/test-match-recording-preservation.sh`: 成功。Docker は起動せず、Python interpreter も実行していない。
- `git diff --check`: 成功。
- exact-HEAD CI [42546504888](https://github.com/ibis-ssl/Duck/actions/runs/42546504888): 成功。`.NET tests`、新しい保存経路 composition fixture、desktop policy、Linux publish が成功。ASPIRE-NET-002 と full-stack acceptance は skip。
- 先行の .NET helper focused tests: 2 passed / 0 failed / 0 skipped（HEAD `190ba6a`。helper は後続差分で変更なし）。

## 保留事項

実 Docker/MATCH、実 artifact upload、POST_GAME を含む #54 の受入は未実施。CI の skip を受入成功として扱わない。通常レビュー担当の指定 profile は `gpt-6.1-sol / high`、実際に適用された profile は観測不能（`null`）。
