# Duck #130 通常レビュー指摘の修正・検証記録

## 対象

- 課題: Duck #128、追跡項目 `I128-LAUNCH-01`
- PR: #130（下書きのまま）
- 修正対象のレビュー基準: `38e6cb88ad50dd8d01b7e4d68b47a0c2edd84c23`
- 専用作業木: `H-pr130-review`
- レビュー指摘を先に記録したコミット: `abfbab195b42847bb167300746b442067b0baafc`
- この記録は修正候補の実装者側検証であり、通常レビューの再判定ではない。

## 指摘ごとの対応と証拠

| 指摘 | 対応と確認 |
| --- | --- |
| NR130-001 | Session 起動では AppHost の終了値 0、`stdout and stderr drained` の完了記録、外側の `dotnet.exe` 終了値 0、所有コンテナ・ネットワークの消失を同じ実行記録で確認した（run `local-20261118T230515Z-21e6427683854f1cb5f45e613476cfbf`）。以前の別実行で観測された外側の終了値 1 の発生元は、保存記録だけからは特定できない。今回の Persistent 実験では Ctrl+C / Ctrl+Break を送ってもランチャーが終了せず、記録完了マーカーも出なかったため、試験用プロセスツリーを強制終了してから明示停止した。この Persistent の Ctrl+C 不成立は未解決であり、Session の終了値 0 の証拠とは区別する。 |
| NR130-002 | 引数テストで、省略した既定 ID と明示した既定 ID `duck-local-development`、任意 ID、停止 ID の一致、ID 省略拒否、末尾 LF 拒否を確認。実機では未使用を事前確認したうえで、明示 ID `duck-local-development` の停止が exit 0 で完了し、対象コンテナ数 0 を維持した。専用 ID `duck130-review-persist-20261118-a` では稼働中の停止が排他ロックで exit 1 となり、7 資源すべてが残ること、AppHost 終了後の明示停止が exit 0 となり、7 コンテナが消えることを確認した。ロック保持と owner / stack / run ラベル不一致時に削除を行わない単体テストも通過した。 |
| NR130-003 | ログ確保前に子を起動しないテストを維持。ログ書き込み障害テストを拡張し、識別した AppHost 子とその子孫の双方が終了し、出力回収処理が例外で完了することを確認した。対象テスト 12 件すべて合格。 |
| NR130-004 | 起動ファイルの `CallerFilePath` から作業木を解決するテストを維持。別の Duck 作業木のカレントディレクトリから H の起動ファイルを実行した Session 証拠に加え、Duck 外の `review-fix` ディレクトリをカレントにして Persistent 起動・再起動・停止した。後者で H の生成物と記録先を使用した。 |
| NR130-005 | 変更 README の文面検査は合格。変更行にあった Markdown whitelist の指摘（`コンパイラー`、`コード`）を既存辞書を広げず言い換えた。ファイル全体の cspell / whitelist は引き続き失敗するが、診断は差分外の既存行に限られる（下記参照）。 |
| NR130-006 | ランチャー、`RunAsync`、停止・出力回収境界、テストクラスとテスト目的に日本語 XML コメントを追加・維持。変更 C# は厳密 UTF-8 BOM 付き、CRLF を確認した。 |

## 実行した検証

- `dotnet run --file <H の絶対パス> -- --help` を Duck 外のディレクトリから実行: exit 0、使用法を表示。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --nologo --filter FullyQualifiedName~LauncherProcessContractTests`: 12 件合格、失敗 0、スキップ 0。
- `npm.cmd run lint:md:text -- --files README.md Testing/README.md`: 合格。
- `npm.cmd run lint:md:spell -- --files README.md Testing/README.md`: ファイル全体で 17 件。README 21 / 126 行、Testing README 11 / 32 / 55 / 101 行のみで、今回追加・変更した行ではない。
- `npm.cmd run lint:md:whitelist -- --files README.md Testing/README.md`: ファイル全体で差分外の既存語彙指摘が残る。変更行 84 と 97 の指摘は修正後に消えた。辞書・許可語ファイルは変更していない。
- Docker Desktop `desktop-linux` 上で、試験前のコンテナ・ネットワーク・イメージ一覧を保存。対象固定イメージはすべてローカルに存在したため pull は不要だった。
- Persistent 初回は固有 ID で 7 コンテナを起動。Crane の coordinator 診断は `Ready=true`、`coordinator_match`。2 回目の起動でも 7 コンテナが稼働し、Crane 診断が同じく ready。初回に対して Crane・Simulator・CM4・Referee Driver の 4 ID が再利用され、Duck・DebugHost・Game Controller の 3 ID が再作成された。Aspire は三つの proxyless endpoint の公開ポートを保存できない旨をログに出しており、同一実装の再起動はソース変更反映の証拠ではない。
- 稼働中の Persistent ID への明示停止はロックで拒否され、資源は変更されなかった。試験プロセスの強制終了後、明示停止を実行し 7 コンテナが消えたことを確認した。試験で追加された空の Persistent network と、試験前一覧に無く、残存コンテナ参照も無かった 5 つの build image tag を削除した。
- 終了時のコンテナとネットワーク一覧は試験前と一致。ただし Docker の `bridge` network ID のみ試験前と異なる値を観測した。bridge には変更コマンドを発行しておらず、差の発生原因は確認できていない。既存 stopped container は一件も変更していない。

## 未解決・未検証

- 最初の古い試験で外側の終了値 1 となった原因は特定できていない。別実行では AppHost と外側の終了値 0、記録完了、Session 資源消失を確認済み。
- Persistent 実行の Ctrl+C / Ctrl+Break が AppHost 終了へつながらなかった。試験資源を残さないため、試験プロセスツリーを強制終了した後、公式の明示停止を実行した。Persistent の通常終了を成功と扱わない。
- Persistent 再起動時に一部資源が再利用されたが、コード変更の反映は試していない。
- この実装修正後の CI は未確認。過去の CI 結果は修正前の HEAD に対するもの。
- 通常レビューは未再実施。修正候補をレビュー担当へ渡す段階である。
## 2026-11-19 候補 HEAD の通常再レビュー・全体ゲート確認

- 対象候補は公開 PR #130 の HEAD `a9a3a8f27394fd3d515e22535719298e9d6afa57`。PR は Draft のまま。GitHub の通常再レビュー結果は [コメント 6997772871](https://github.com/ibis-ssl/Duck/pull/130#issuecomment-6997772871) に記録され、NR130-001〜006 は pass と報告された。これは通常再レビューの結果であり、独立最終レビューは未実施。独立レビュー用の予約・freeze も行っていない。
- exact-head CI [run 44443697638](https://github.com/ibis-ssl/Duck/actions/runs/44443697638) は 2026-11-19 02:24:54 UTC 時点で completed/success。Linux job で CM4 native helper、AutoRef Java agent、`dotnet test`、desktop acceptance policy、match-recording preservation、Linux publish outputs が成功。Tracker test は 1,457 passed、4 skipped、0 failed。ASPIRE-NET-002 Linux packet-flow と ASPIRE full-stack Linux acceptance は skip。
- Duck に全体ローカル等価ゲート一つの定義はない。公式 pull_request 経路は `.github/workflows/dotnet-test.yml`。この run はその限定 CI ルートを満たすが、skip された packet-flow/full-stack の受入れまで証明しない。README / Testing README のローカル .NET コマンドは `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore`。
- FA780 の Windows ローカル実行記録は HEAD `6a14add67847fc35e17cefdacfeac367bfb6d472` で exit 1（1,451 passed、8 failed、2 skipped）。失敗は Linux native verifier を要する `MatchLinuxBindPermissionsTests`。現在の候補とその SHA の間でランチャー、AppHost、Tracker.Tests の対象 blob は同一であり、測定対象は変わっていない。Windows ではこの Linux 固有検証を実施できていない。Docker 実機は再実行していない。
- 対象の tracked Markdown は README.md、Testing/README.md、Tracker/Design/phases-status.md、Tracker/Design/tasks-status.md。merge-base と候補 HEAD の同一設定比較で、変更範囲に textlint の新規指摘は 0。cspell / whitelist は既存差分外の指摘が残るが、比較で新規指摘は 0。
- `npm run lint:md` の全体実行は候補 HEAD で失敗。textlint は成功し、cspell が既存語彙指摘を報告した。走査対象には Git 追跡外かつ .gitignore 対象の `.venv/` も含まれ、そこからの語彙診断は PR 差分ではない。辞書・許可語を緩和せず、`.venv` も削除していない。
- CodexSkill `8ee6e9f80a0362a4d066c4ea39fd3e87002fc829` の development-orchestrator、review-enforcer、report-output-manager、report-writer を確認。レビュー前の証跡と追跡を通常レポートに保存し、レビュー予約・freeze は review-enforcer の担当として未実施。
