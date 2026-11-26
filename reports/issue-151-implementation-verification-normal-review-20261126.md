# Duck #151 実装・検証・通常レビュー報告

## 対象

- 課題: [Duck #151](https://github.com/ibis-ssl/Duck/issues/151)
- Draft PR: [#154](https://github.com/ibis-ssl/Duck/pull/154)
- 作業ブランチ: `task/issue151-mcap-diagnostics`
- 対象コード HEAD: `f22e040e58ef8a8d905ba806cc736a8a0651f815`
- 対象時点の base `main`: `68ff65fbfedd5e933b553d6ce4f5d3ea535f33fa`
- 技術的な変更範囲: base から対象 HEAD までの Duck 診断 MCAP 記録・読取、両ホストへの登録、診断表示、関連試験・設計・README・依存定義。報告書と進捗追跡の追加はこの技術対象 HEAD の後に行う。

## 目的と対象範囲

Duck 単体の診断ログを起動ごとに MCAP 1本へ記録し、終了後に標準 `foxglove.Log` として読み戻せること、重要度・発生元・時刻を保持すること、診断ログ表示を CaptureOn の試合記録表示から分離することを扱う。旧診断形式の新規書込み、二重出力、互換出力、変換機能は追加しない。過去ログの読取、試合再生、外部製品ログの取込みは本報告の対象に含めない。

## 実装の要約

対象差分は base `68ff65fbfedd5e933b553d6ce4f5d3ea535f33fa` から HEAD `f22e040e58ef8a8d905ba806cc736a8a0651f815` まで。主な実装領域は次のとおり。

- `Tracker/Tracker.Diagnostics.Mcap/`: MCAP 記録 provider、ホスト終了と記録確定の制御、Foxglove ログスキーマ。
- `Tracker/Tracker.RuntimeHost/` と `Tracker/Tracker.DebugHost/`: 記録経路の登録、設定解決、診断ログの読取・表示。
- `Tracker/Tracker.Tests/`: 記録のライフサイクル、失敗、読取、一覧分離の焦点試験。
- `Tracker/Design/DebugHost/`、`Tracker/Tracker.DebugHost/README.md`: 設計・利用説明。

HEAD `f22e040e58ef8a8d905ba806cc736a8a0651f815` は読取一覧の一部サブフォルダーで発生する列挙障害を分離し、後続の健全な兄弟フォルダーにある MCAP を一覧へ残す修正を含む。旧版 Windows 5件の実測対象を RuntimeHost と図の両表現で揃えた。Linux 限定の実ホスト試験は実行条件の検出時スキップと実ビルド構成に応じたホスト解決を使う。

## 検証結果

### Duck 単体の Linux 実記録

[受入記録（PR コメント 7178476752）](https://github.com/ibis-ssl/Duck/pull/154#issuecomment-7178476752) は、この対象 HEAD での Linux RuntimeHost 単体実行を記録している。Aspire を介さず、SSL-Vision の受信と送信を無効にし、テストハーネスで SIGINT 後のホスト終了コード 0 を確認した。完成 MCAP は1本、サイズ4,266 bytes、SHA-256 `7b877a38bdc9744c105bed2cb81628a60ee89d105104deea54ec1846b077bc52`。公式 MCAP CLI 0.4.0 の `doctor`、`info`、`cat` はそれぞれ終了コード0。`/diagnostics` の標準 `foxglove.Log` 5件、発生元2種類、全件 INFO、単調増加する記録時刻を読み戻し、途中ファイルと旧 `.log` は0本だった。

この受入記録にあるコマンドの完全な起動引数列はコメントに記載されていないため、本報告では補っていない。CLI 版・各終了値・MCAP のハッシュと観測結果は同コメントを一次参照とする。現版の Windows、実機、受信・送信を有効にした試合、異常終了後の回収は未検証。旧 Windows 実測は別 HEAD `d6147e43b81f8ceb659192e39769350c91fdd062` であり、この HEAD の確認に流用しない。

### 焦点試験と自動 CI

[通常再レビュー記録（PR コメント 7177677694）](https://github.com/ibis-ssl/Duck/pull/154#issuecomment-7177677694) によれば、変更領域の Linux 焦点試験は Debug と Release で各16件成功した。テストコマンドそのものは同コメントに記載がないため、本報告では推定していない。

[自動 CI run 45721216934](https://github.com/ibis-ssl/Duck/actions/runs/45721216934) は対象 HEAD と一致し、`.NET tests` ワークフローの `test` ジョブが成功した。`ASPIRE-NET-002 Linux packet flow` と `ASPIRE full-stack Linux acceptance` はスキップされた。これは当該 CI run の結果であり、Aspire または全体受入の成功を示さない。CI は本報告作成時に再実行していない。

## 通常レビューと修正確認

[通常再レビュー（PR コメント 7177677694）](https://github.com/ibis-ssl/Duck/pull/154#issuecomment-7177677694) は HEAD `f22e040e58ef8a8d905ba806cc736a8a0651f815` で、残っていた3点を解消と判定した。内容は、読取不可のサブフォルダーを分離して健全な兄弟の MCAP を維持すること、旧 Windows 実測のホスト名を図と埋め込み図面で揃えること、Linux 限定のホスト試験を Debug／Release の実構成に合わせること。今回の報告はレビュー所見と担当者が提示した検証結果を整理したもので、独自の試験再実行ではない。

## 独立技術判定との区別

[独立担当の技術判定（PR コメント 7178533922）](https://github.com/ibis-ssl/Duck/pull/154#issuecomment-7178533922) は、HEAD `f22e040e58ef8a8d905ba806cc736a8a0651f815` における Linux 単体の対象範囲を合格とし、通常レビュー後に PR 本文の旧 SHA／CI 表記を直したことを確認している。同コメントは、現版 Windows・実機・負荷が未検証であること、および正式な報告証跡の確定、本人レビュー、マージが未実施であることも明記している。

この技術判定コメントは独立最終レビュー報告書ではない。本報告は通常の実装・検証・通常レビュー記録をまとめるもので、独立最終報告の予約や作成を行わない。独立レビューの最終的な報告証跡・本人確認・マージは未完了であり、PR は Draft のままとする。

## この報告作成時の作業と未検証事項

- 技術対象 HEAD の既存証拠と指定コメント、CI run を読み取り、報告と進捗追跡を作成した。追加の製品試験、ビルド、CI 起動、Windows／実機操作はしていない。
- 変更した `tasks-status.md` と `phases-status.md` に対し `npm run lint:md:text -- --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md` を実行したが、終了値1で `node_modules/textlint/bin/textlint.js` が存在せず未実施。`npm run lint:md:spell -- --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md` も終了値1で、既存 `yaml` モジュールがなく未実施。`python3 tools/lint/scripts/check-markdown-whitelist-sudachi.py --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md reports/issue-151-implementation-verification-normal-review-20261126.md` は終了値2、`sudachipy` 不在で未実施。依存導入はしていない。
- リポジトリの `tools/lint/markdown-targets.json` は `reports/` を Markdown lint 対象から除外しているため、標準 textlint／CSpell の対象は2件の進捗文書。報告書も含めた上記直接ホワイトリスト検査は、SudachiPy 不在で判定できなかった。`git diff --check` は終了値0。変更した3ファイルはすべて UTF-8、BOMなし、LF、末尾改行あり。
- 現版 Windows／実機、異常終了中の回収、受信・送信を有効にした試合、実運用負荷、Foxglove の実画面、Aspire の生ログ統合、試合再生データとの同居は未検証または対象外。
- 技術的な合格判定は `f22e040e58ef8a8d905ba806cc736a8a0651f815` に結び付く。後続の報告・追跡コミットは管理文書の追加であり、技術対象 HEAD やその検証範囲を変更しない。

## 参照

- [通常再レビュー／修正確認](https://github.com/ibis-ssl/Duck/pull/154#issuecomment-7177677694)
- [Linux 単体 MCAP 受入](https://github.com/ibis-ssl/Duck/pull/154#issuecomment-7178476752)
- [対象 HEAD の自動 CI](https://github.com/ibis-ssl/Duck/actions/runs/45721216934)
- [独立担当の技術判定（正式な独立報告とは別）](https://github.com/ibis-ssl/Duck/pull/154#issuecomment-7178533922)
- [課題 #151](https://github.com/ibis-ssl/Duck/issues/151)
- [Draft PR #154](https://github.com/ibis-ssl/Duck/pull/154)
