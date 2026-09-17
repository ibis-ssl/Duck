# 承認済み用語の追加と本文表記の修正報告

## 対象と実施結果

- 対象: `ibis-ssl/Duck`、PR #20。
- ブランチ: `docs/runtimehost-readme-appsettings`。基点: `main`。
- 開始時のPR HEAD: `f1fd7a52ed95c3f5e80ca529243974ff917d1f67`。
- 本文・設定変更の公開HEAD: `c8f256f3e215f125756ed1051a91b6b401971833`。
- 作業日: 2026-09-15、日本時間。
- 編集・検証: RDC端末 `ibis-ThinkBook-14-G7-IML`、`/home/ibis/ssl/IbisDuck`。
- 本文変更の公開時にはGitHub connectorを使用した履歴がある。利用者からRDC利用時はgitコマンドを使うよう指示を受けた後は、RDC上で `git fetch`、ローカルHEAD同期、報告コミット、`git push` を行う。マージは行わない。

承認された具体的な許可語と語句を追加し、現行設計書7文書とDebugHostの利用手順を修正した。ホワイトリストは170項目から301項目になった。製品コードや検査器の修正、設計書全体の用語整理は実施していない。全体のMarkdown検査とローカル.NETテストには失敗が残っている。

この報告と検証記録・引き継ぎは上記の本文変更後に保存する。記録用コミット自身のSHAと、そのHEADに一致するCI結果はPRコメントへ記載する。本報告の保存を独立レビューの証明とは扱わない。

## 利用者の承認と変更範囲

利用者は、先に提示した英語2項目、カタカナ104項目、意味を限定する28分類、今回登録しない8項目の方針を承認した。その後の指示に従い、データグラムは登録せず、通常の説明は「UDPパケット」にした。Datagramを含む本物の識別子は改名していない。

| 追加区分 | 新規項目数 | 内容 |
| --- | ---: | --- |
| カタカナ | 103 | 提示済み104項目からデータグラムを除いたもの。 |
| 通信単位 | 1 | UDPパケット。空白付きの表記も同じ項目で許可。 |
| 技術表記 | 2 | Base64、Unix 時刻。Base、Unixの単独許可には広げない。 |
| 意味を限定した語句 | 25 | 提示した具体例のうち、20分類に対応する語句。 |
| 合計 | 131 | 別表記6件を含む新規許可表記は137件。 |

既存170項目の内容と順序は維持した。追加項目内の別表記は、カウンター、コンストラクタ、サーバ、パラメーター、ブラウザー、UDP パケットの6件である。対応する英単語を一緒に許可する変更はしていない。

意味を限定して追加した語句:

通信アドレス、通信ポート、URLクエリ、型のメンバー、追跡エンジン、コマンドラインオプション、マルチキャストグループ、描画グループ、テストケース、サンプルコード、サンプルデータ、表示スタイル、画面ヘッダー、マウスホイール、設定値のバインド、ソケットのバインド、バックグラウンド処理、ファイルパス、ローカルファイル、ローカル実行、競技フィールド、観測フレーム、追跡フレーム、ライブ表示、競技ルール。

28分類の元の単独語は登録していない。このうち、インターフェース、コミット、サービス、ノイズ、フィルター、モジュール、モデル、リセットの8分類は、追加する具体的な語句を今回確定していない。説明に意味を書くだけで単独語を広く許可することは避けた。28分類の全使用箇所を修正済みという意味ではない。

アクセス、コスト、コード、タイミング、タスク、バランス、ラッパー、リングの単独登録も見送った。fixture、assertion、metadataや、それらを不明瞭なカタカナへ置き換える方針は採用していない。

## 本文の修正

修正対象は次の8文書である。

- `Tracker/Design/Core/tracker-architecture-plan.md`
- `Tracker/Design/Core/tracker-core-engine-detail-design.md`
- `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `Tracker/Tracker.DebugHost/README.md`

これらの文書にあった「データグラム」5箇所と、通常の説明のdatagram 1箇所を修正した。通信単位は「UDPパケット」、保存する中身は「受信データ」「バイト列」として説明した。8文書内で、これらの旧表記が残っていないことを確認した。

そのほか、実行時点、公開範囲、処理負荷、別の処理を呼び出すメソッド、円形の目印など、文脈に合う表現へ直した。作業管理上のタスクは作業へ、ソースコードを指すコードはソースコードへ直した。画面ヘッダー、URLクエリ、マウスホイールは対象を含む表現にした。

変更前後の文章を現在のChat自身が読み、通信データの保存範囲、互換処理を追加しない方針、処理順序、表示の意味、過去の作業状態を変えていないか点検した。行内の識別子とコード例の並びは機械的にも照合し、8文書すべてで変更なしだった。通常の説明を逆引用符で囲って検査対象外へ逃がす変更はしていない。

## ローカル検証

| 検証 | 結果 |
| --- | --- |
| YAML構文・既存項目維持・追加表記の重複 | 成功。既存170項目を維持し、追加137表記の重複なし。 |
| 追加137表記を許可一覧検査へ1表記ずつ入力 | 137件すべて終了コード0。 |
| 同じ137表記をcspellへ1表記ずつ入力 | 137件すべて終了コード0。 |
| 単独許可しない50表記を許可一覧検査へ入力 | 50件すべて終了コード1。意図した拒否。 |
| 未登録英語13表記をcspellへ入力 | 13件すべて終了コード1。意図した拒否。 |
| 上記の有効な許可・拒否検証 | 337件中337件が期待どおり。 |
| 本文8文書のtextlint | 終了コード0。 |
| 対象ファイルのgit diff --check | 終了コード0。 |
| 行内識別子・コード例の維持 | 8文書すべて一致。 |
| 全体npm run lint:md | 変更前後とも終了コード123。未通過。 |
| 全体の許可一覧検査を単独実行 | 終了コード1。SudachiPyの入力長上限で停止。 |
| .NETテスト全体 | 328件中323件成功、5件失敗、0件省略。終了コード1。 |

個別の表記検査では次を使い、各回の標準入力を1表記に限定した。

```bash
.venv/bin/python .agents/skills/review-enforcer/scripts/check-markdown-whitelist-sudachi.py --stdin tools/lint/README.md --list-unknown
node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js --no-progress stdin://tools/lint/whitelist-probe.md
```

変更前の確認も保存した。Base64、Unix 時刻、バッファ、UDPパケットの順に、許可一覧検査の終了コードは0、1、1、1、cspellは1、1、0、0だった。変更前に全検査で拒否されたわけではない。これは設定変更の前後比較であり、製品実装のTDD証拠ではない。

cspellへ日本語の拒否候補37表記を入力した結果は参考情報として保存したが、cspellはこの日本語の拒否判定を担わないため、上記337件には含めていない。

全体Markdown検査では通常の英語説明や脚注参照などの未対応が残る。別実行の許可一覧検査は、49,149バイト上限に対して80,104バイトの入力となり停止した。共有検査器、一般漢字語の検査契約、対象除外は変更していない。追加説明文全体を含む検査が通ったとも扱わない。

### .NETテストの失敗

| テスト | 観測した失敗 |
| --- | --- |
| Repository_UsesDebugHostProjectNameAndLeavesOldServerPathRemoved | Assert.Falseに対してTrue。 |
| TrackerOptions_DefaultReceiveSettingsAreOptIn | 受信設定の既定値でFalseを期待し、True。 |
| RuntimeHostedService_ComesFromServerTrackingNamespace | 対象assembly内で期待する登録型が見つからない。 |
| ReplayReader_ExplicitFutureTimestamps_ReturnsNoCandidateSnapshot | Assert.Singleの対象が空。 |
| CaptureOnSession_WritesTrackerSnapshotAndKeepsExistingSidecars | Assert.Trueに対してFalse。 |

製品のC#、プロジェクトファイル、実行設定JSONは今回変更していない。ただし、開始時点へ戻した比較実行はしていないため、5件すべての原因を今回の作業だけで確定したとは扱わない。製品コードを直してこの文書変更へ混ぜることはせず、テスト名、結果、標準出力、標準エラー、TRX、VSTest診断、MSBuildバイナリログを保存した。CI結果とこのローカル失敗は別々に報告する。

## 保存した診断記録

元文書、追加項目、1表記ごとの入力、全実行のコマンド・標準出力・標準エラー・終了コードは次に保存した。

```text
/home/ibis/.local/share/duck-doc-lint/approved-whitelist-20260915T180655
```

.NETのTRXと追加診断は同じ場所の `dotnet-results/`、実行出力は `dotnet-tests/` にある。許可・拒否結果と対象ファイルのSHAは、同じ報告フォルダの `markdown-whitelist-approved-validation-20260915.json` にも保存する。

作業開始時にmainの `.github/workflows/dotnet-test.yml` を確認した。テスト失敗時にTRX、標準出力、標準エラー、VSTest診断、blame出力、MSBuildバイナリログ、終了コード、実行環境、ソース一式を保存する定義がある。今回のDuck文書変更ではworkflowを変更していない。PR側の開始時点のツリーにworkflowがないことと、main側の定義の存在を区別する。

## 公開履歴と内容照合

| コミット | 内容 |
| --- | --- |
| d5bbdc5491ee726a2791cc0d582b444685598c80 | ホワイトリストへの承認語の追加。 |
| ed3fceb3e9dc34cd903bbfa405b746bcef44e304 | DebugHost利用手順のUDPパケット表記。 |
| 67937f8a6965f74430b2c695cc665133716c03db | Core設計3文書の表記修正。 |
| c8f256f3e215f125756ed1051a91b6b401971833 | DebugHost・RuntimeHost設計4文書の表記修正。 |

RDC側の開始HEADは `90140c05f2f7536275df4cb6d307a0e049058044` で、GitHub側の開始HEADとは異なる。開始時のTracker以下のツリーは双方とも `283a97232be1f6bb3803322bba2b0a155e387ba5` で一致し、ホワイトリストの元blobも一致した。変更後は、RDCで検証した各ファイルの `git hash-object` と公開済みblobのSHAを照合した。その後RDC上で `git fetch origin docs/runtimehost-readme-appsettings` を実行し、承認対象9ファイルが `origin/docs/runtimehost-readme-appsettings` と一致することを確認したうえで、ローカルHEADを `c8f256f3e215f125756ed1051a91b6b401971833` へ同期した。`Tracker/Design/tasks-status.md` と既存の調査用差分・未追跡ファイルは保持している。

本文変更を公開した段階では、直前に作成済みのtreeを基に変更後ファイルを組み立てた履歴がある。公開前のコミット比較で、現在の親からの差分が予定した設計4文書だけであることを確認し、既存変更を消していない。強制更新は使っていない。今回の報告保存以降のgit操作はRDC上のgitコマンドで行う。

## 意図的に変更していないもの

開始前から変更されていた `Tracker/Design/tasks-status.md` の草案は、開始時の内容と同一のまま保持した。既存の未追跡の調査ファイルも混ぜていない。履歴文書、過去の候補表、製品コード、実行テスト、実行設定、共有Skill、検査器、prh、依存関係、文書対象の除外設定は変更していない。

アップロードされたChat実装・文脈整理・実装・報告・引き継ぎの手順を使用した。共有Skillは `106ea5dcf12c4805756351fb9381df220b94f044` でGitHub mainと一致し、未変更だった。Serenaの接続は利用できず、利用可能なプラグイン検索でも見つからなかったため未使用。別エージェントは起動していない。

## 残る事項

今回の具体的な承認語の追加と対象本文の言い換えは公開済み。全体の未登録語整理、条件付き分類の未確定語句、検査器の長文・脚注等の修正、ローカル.NETテスト失敗の調査、通常・独立レビューは未完了である。DOC-LINT-003全体を完了にはしていない。

最終CIは、記録用コミットを含むPR current HEADを再取得し、同じhead_shaのpull_request runだけを確認する。一致するrunがなければCI未実施、実行中なら結果未確定として記録し、別SHAの成功を代用しない。
