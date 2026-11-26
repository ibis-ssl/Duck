# 課題 #151 独立最終レビュー指摘 FR-06・FR-07 対応報告

## 対象

- 課題 #151、既存 Draft PR #154、作業番号 `I151-MCAP-01`。
- ブランチ: `task/issue151-mcap-diagnostics`。
- 指摘対象・作業開始 HEAD: `20f5d0067de278553e6d183a728001f8e1ac3997`。
- 取得済み Duck の `origin/main`: `68ff65fbfedd5e933b553d6ce4f5d3ea535f33fa`。CodexSkill の `origin/main`: `61fa07b80a9ecd551fe9f573c76f3eb5a987b721`。
- 指定 HEAD から分離した作業木で修正。開始時は清浄。両リポジトリの元の作業木は切り替えていない。
- 本書は通常レビューに渡す実装・検証記録であり、独立最終レビュー報告ではない。指摘の重要度は依頼に記載がなく、変更・推定しない。

## 指摘への対応

- FR-06: `Diagnostics.razor` と `Diagnostics.razor.cs` で MCAP の読取成功時に表示件数、読み飛ばし件数、省略件数を表示する。既存の `Entries`、`SkippedLineCount`、`OmittedEntryCount` を使い、省略が正なら元の MCAP に記録が残る旨を付記する。選択中のスナップショットから算出するので別ファイルを選んだ際に件数が残らない。10,000件の上限、Reader、ファイル書き込みは変更しない。
- FR-06 の近接試験: `TrackerDiagnosticsMcapReaderTests.DiagnosticsPage_McapEntrySummary_FollowsSelectedLogAndReportsLimit`。正常10,002件と不正1件を含む MCAP で表示10,000・読み飛ばし1・省略2、元ファイルから正常10,002件の再読取を確認。通常 MCAP の1・0・0、旧形式への選択変更で件数非表示、再選択で件数復帰を確認。ページ状態と Razor の表示結線を検査する試験であり、ブラウザー実画面の確認ではない。
- FR-07: DebugHost README で MCAP 閲覧と CaptureOn の再生・比較を分け、再生位置の変更と比較が CaptureOn 用であると明記。`Logging:LogLevel` が MCAP にも適用され、DebugHost の既定 `Warning` と検証例の `Information` の違いを明記した。設定値は未変更。新規 CaptureOn の `DiagnosticsLogPath` は既存診断標本表示との対応付け用で、旧形式のログや起動単位 MCAP を生成する設定ではない旨を README と設計書に追記した。
- 変更対象: 上記の画面2ファイル、試験1ファイル、README、設計書、進捗2文書、本報告のみ。

## 検証

以下は修正後の作業木で実施。SDK は既存 Cloud 環境の .NET 10、既存 NuGet と文書道具を使用。Proto は元の清浄なサブモジュールと同じ固定コミットの内容を作業木に展開した。

| コマンド | 結果 |
| --- | --- |
| `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter 'FullyQualifiedName~TrackerDiagnosticsMcapReaderTests&FullyQualifiedName!~RegisteredHost_NormalStop' --verbosity minimal` | Debug ビルド成功、10件成功、失敗0、省略0 |
| `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-build --filter 'FullyQualifiedName~TrackerDiagnosticsLogReaderTests\|FullyQualifiedName~DiagnosticsPlaybackStateTests\|FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests\|FullyQualifiedName~TrackerDiagnosticsReplayTimelineIndexTests' --verbosity minimal` | 81件成功、失敗0、省略0 |
| `dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -c Release --no-restore --verbosity minimal` | 成功、警告0、エラー0 |
| `git diff --check` | 成功 |

Debug ビルドには既存の NuGet 監査警告、Aspire 構成の警告、他の試験コードの警告が出た。依存や対象外コードは変更しない。

変更した README・設計書に `npm run lint:md:text`、`npm run lint:md:spell`、`npm run lint:md:whitelist` を `-- --files` で適用し、すべて成功。進捗2文書も加えた4文書では textlint 成功、綴り検査は終了値1・116件、許可一覧検査は終了値1・1,468件。開始 HEAD の別作業先でも同一コマンドで116件・1,468件。綴りの全診断と許可一覧の先頭200件はファイル名・診断内容の集合が同一、許可一覧の残数1,268件も同一。進捗2文書の追加行単独の許可一覧検査はともに終了値0で、綴り検査の追加行指摘も0。既存の文書全文指摘は残し、語彙一覧は変更しない。報告書は標準 Markdown 検査の対象外。

## 残件と次の工程

- 同じ試合再生、Docker、手動 CI、実ホスト起動、実機、ブラウザー画面は今回実施していない。既存の実動作確認と区別する。
- 変更後の正確な HEAD に一致する CI は本書の作成時点で未確認。本書の対象はテストした差分であり、公開コミット SHA は親への返却時に記録する。
- 通常レビュー担当による FR-06・FR-07 の修正確認が次の工程。指定の独立最終レビュー報告は作成していない。PR の新規作成・マージはしない。
