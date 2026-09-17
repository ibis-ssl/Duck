# 作業状況

規則: この文書は `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在の作業

- ID: `DOC-LINT-003`
- 題名: 承認済み表記を本文へ反映し、文書検査の再開状況を整理する
- 段階: 文書検査整備
- 状態: レビュー指摘への対応中。診断設計、構成設計、表示画面設計、raw vision 表示設計、RuntimeHost 設計、利用手順、進捗2文書の本文と原文を照合し、採否を記録した。人が読む設計書の中央台帳は2,142 / 2,142件完了。4,336出現箇所の最終台帳、履歴本文との最終照合、全体の文書検査、独立最終レビューは未完了。
- 規模: 中
- 依存関係: `DOC-LINT-002`、承認済み許可語、共有文書検査器の修正。
- 完了条件:
  - ソースコードに現れる名称、実際の表示名、設定名など英語で保持すべき表記と、通常の説明文を区別し、説明文の不要な英語を読みやすい日本語へ直す。
  - 長文入力、脚注、許可語の境界、日本語に隣接する英語、検査対象列挙の不整合を修正し、文書検査器が対象全文を途中停止や見逃しなく検査できるようにする。
  - 一般的な漢字語を許可一覧へ大量登録して通す方法、未承認語の一括許可、本文の検査除外への退避は行わない。
  - `npm run lint:md` を全対象文書に対して実行し、終了値 0 を確認する。
  - 最終 PR の `current HEAD` と同じ `head_sha` の CI を確認し、成功結果と検証証跡を記録する。
- 証跡: `reports/doc-lint-resume-20260915.md`
- 完了前証跡: `reports/markdown-lint-completion-20260916.md`
- 残課題: 許可語の全出現箇所の最終照合、履歴本文の再確認、承認済みの直接引用の例外機能の取り込み、修正後の独立最終レビュー。本文の確認は、共有検査器の修正を待たずに進める。
- 追加承認: 利用者が `ChikkarPy` と提示済み説明文の登録を承認した。用語登録専用担当が登録と対象検査を行い、別担当が確認する。
- 追加証跡: `reports/doc-lint-chikkarpy-registration-20260915.md`
- 2026-09-15 時点の検証: `ChikkarPy` の許可判定と文書検査手順の綴り検査は成功したが、全範囲の検査は未登録語で失敗していた。
- レビュー前の `42e0637` に対する 2026-09-16 の全範囲検証: `npm run lint:md` は 19 文書を対象に終了値 0。`cspell` は 0 件、許可一覧違反は 0 件、`textlint` も成功した。
- 使用中の共有検査器: 長文分割、脚注、語境界、日本語に隣接する英語、対象列挙の修正を含む `279b8da9ffe954f78fd8461555be429f1bdcc100` の保存版を使用する。上流の最新状態とは区別する。

- 2026-09-15 の追加依頼: ソースコードに現れる名称や実際の表示名を残し、それ以外の設計上の説明語を日本語へ寄せる。読みやすさを優先し、分かりにくい片仮名への一括置換は行わない。
- 2026-09-15 当時の本文修正範囲: 基本設計、追跡処理詳細、テスト設計、診断画面詳細、診断画面の保守設計、検出情報の表示設計、通常実行用の設計の 7 文書。この時点では旧履歴、取り込み済み外部資料、製品のソースコードは変更していなかった。
- 2026-09-15 当時の本文修正の公開先: `0f52e9e024c101cb46f9bb4d88fd4e066a477ec2`。文書単位で 7 回に分けて反映した。
- 2026-09-15 当時の本文修正の検証: 7 文書の `textlint` と差分検査は成功。`cspell` の残り 109 件は脚注参照名 106 件と `Unix` / `Base64` に由来する 3 件。許可一覧や検査除外設定を緩和していない。
- 2026-09-15 当時の本文修正の証跡: `reports/design-prose-japanese-20260915142353.md`、`reports/design-prose-validation-20260915142353.json`。全範囲検査の未登録語、脚注参照名の扱い、共有検査器の長文制限は、その時点の残課題として記録した。

- 2026-09-17 の引き継ぎ確認: 今回の6文書の差分543件について、本文と原文を読み、採否と段落の対応を記録した。既存の13文書の記録272件との統合、4,336出現箇所の最終照合、履歴本文の再確認は継続中。全件確認済みや作業完了とはしない。
- 2026-09-17 の検証: 今回の6文書は、それぞれ対象検査3種と差分検査が成功。全体の `npm run lint:md` は終了値123。`feedback-points/feedback-points.md` の2つの利用者発言の引用内の違反は、承認済みの引用例外を共有検査器へ実装する別作業として残る。
- 2026-09-17 の証跡: `reports/task-doc-lint-003-diagnostic-handoff-20260917064359.md`。診断設計の追加照合で、比較領域のボタン位置、テスト範囲、100 ms 時点の参照対象を修正し、`99929bc` で公開した。相対位置の参照範囲もキャプチャー単位へ戻した。追加照合は `reports/task-doc-lint-003-diagnostic-scope-followup-20260917082311.md` に記録する。作業担当の自己点検であり、独立最終レビューは未実施。

- 2026-09-17 の raw vision 表示設計照合: `Tracker/Design/DebugHost/raw-vision-viewer-plan.md` の原文524出現を全件中央台帳へ統合した。変更箇所504件と原文同一20件を確認し、未解決0。本文では不自然な日本語とネットワーク受信の説明を整理し、型名・設定名・実UI名は維持した。
- 2026-09-17 の raw vision 検証: 対象文書の `textlint`、`cspell`、許可一覧検査、`git diff --check` は終了値0。共有集計は1,946 / 2,142件完了、残り196件。証跡は `reports/task-doc-lint-003-raw-vision-correspondence-20260917.md` と `reports/diagnostics/wording-raw-vision-ledger-20260917/`。独立最終レビューの合格には扱わない。

- 2026-09-17 の RuntimeHost 設計照合: `Tracker/Design/RuntimeHost/runtime-host-plan.md` の原文196出現を全件中央台帳へ統合した。変更箇所186件と原文同一10件を確認し、未解決0。本文では AutoRef の意味、`InterfaceAddress` の用途、CaptureOn 中のキャプチャー、追跡スナップショット、公式形式の `TrackerWrapperPacket` の説明を原文と実装に合わせて整理した。
- 2026-09-17 の RuntimeHost 検証: 対象文書の `textlint`、`cspell`、許可一覧検査、`git diff --check` は終了値0。人が読む設計書の中央台帳は2,142 / 2,142件完了、残り0。証跡は `reports/task-doc-lint-003-runtime-host-correspondence-20260917.md` と `reports/diagnostics/wording-runtime-host-ledger-20260917/`。4,336出現箇所の最終台帳、履歴本文との最終照合、全体の文書検査、独立最終レビューは未完了。

- ID: `CAPTURE-REPLAY-001`
- 題名: `Tracker.CaptureReplay` に raw vision と `ibis tracker` の遅延分析出力を追加する
- 段階: PR
- 状態: 当時は PR #19 公開中
- 規模: 中
- 依存関係: `RUNTIME-HOST-011` の完了。
- 完了条件:
  - `Tracker.CaptureReplay` がキャプチャーの保存先を入力として、raw vision のパケットの受信周期と、`ibis tracker` の出力周期および時刻差を同じ出力で比較できる。
  - 出力は今回のキャプチャー固有ではなく、次回以降の遅延、古い状態が残る問題、周期の調査に再利用できる操作名と概要 / 詳細の形式にする。
  - 指定キャプチャー `/home/ibis/ssl/IbisDuck/Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T132706328Z-4a19e61b92c443929f91eccdff3512c9` で、`ibis tracker` が raw vision より遅れて見える原因を報告書に記録する。
  - 対象を絞ったテスト、`Tracker.CaptureReplay` のビルド、専用のレビューを通し、進捗文書を同期する。
  - 実装証跡:
    - `reports/capture-replay-001-latency-investigation-20260516185833.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~CaptureReplayTests -m:1 /nr:false` は 11 件成功。
    - `dotnet build Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -m:1 /nr:false` は警告 0 件 / エラー 0 件。
    - 指定キャプチャーでは通常設定 `ReorderWindowNs=100ms` で `avgCommitLagMs=111.813` / `maxCommitLagMs=117.302`。`--reorder-window-ns 0` の対照実行では `avgCommitLagMs=15.812` / `maxCommitLagMs=20.842` まで下がることを確認した。
  - レビュー証跡:
    - `reports/pr19-review-capturereplay-20260516200807.md`
    - `reports/pr19-review-docs-tracking-20260516200807.md`
    - `Tracker.CaptureReplay` の範囲には、完了を妨げる指摘はなかった。`ReceivedAt` を基準とする遅延の説明には、完了を妨げない懸念があり、READMEと調査報告の文言を修正した。

- ID: `RUNTIME-HOST-012`
- 題名: `Tracker.RuntimeHost` 起動時に CLI 引数で適用する設定プロファイルを指定できるようにする
- 段階: PR
- 状態: 当時は PR #19 公開中
- 規模: 小
- 依存関係: `RUNTIME-HOST-011` の完了。
- 完了条件:
  - `Tracker.RuntimeHost` が `--profile <name>` と `--profile=<name>` を受け取り、`Tracker:ActiveProfileName` より優先して設定プロファイルを選択できる。
  - `--profile` 引数が未指定の場合は、既存の `appsettings.json` の `Tracker:ActiveProfileName` による動作を維持する。
  - 不正な `--profile` 指定は起動時に明示失敗する。
  - 対象を絞ったテスト、`Tracker.RuntimeHost` のビルド、専用のレビューを通し、進捗文書を同期する。
  - 実装証跡:
    - `reports/runtime-host-012-cli-profile-20260516195943.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostOperationLoopTests|FullyQualifiedName~RuntimeHostScaffoldContractTests" -m:1 /nr:false` はレビュー指摘の修正後に 17 件成功。
    - `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false` は警告 0 件 / エラー 0 件。
    - コマンドラインの解析は、`Microsoft.Extensions.Configuration.CommandLine` の設定読込機能と、引数名から設定キーへの対応表で実装した。
    - `--profile` / `--profile=` に値がない場合は、リポジトリ内の `appsettings.json` を読み込む実際の起動経路でも `ArgumentException` で即終了することを確認した。
  - レビュー証跡:
    - `reports/pr19-review-runtimehost-profile-20260516200807.md`
    - `reports/pr19-review-runtimehost-profile-r2-20260516201757.md`
    - 初回レビューの重大指摘は修正済み。2回目のレビューで指摘がなく、完了としてよいことを確認した。

- ID: `DOC-LINT-001`
- 題名: 文書検査と綴り検査を導入し、英単語と片仮名語を意味付きの許可一覧で管理する
- 段階: 文書検査整備
- 状態: PR #20 公開中、追加整備は `DOC-LINT-003` で追跡
- 規模: 小
- 依存関係: なし
- 完了条件:
  - リポジトリの最上位ディレクトリから文書検査を実行できる `npm` の実行コマンドを追加する。
  - `textlint` が利用者編集対象の `*.md` 全般に実行され、文書の表記揺れや文章規則を検出できる。
  - `cspell` が利用者の編集対象である `*.md` 全般を検査し、専用の許可一覧にない英単語がある場合は失敗する。
  - 独自の許可一覧検査が、利用者の編集対象である `*.md` 全般と許可一覧の説明文に含まれる英単語と片仮名語を検査し、未登録の場合は失敗する。
  - 許可一覧は、既存文書の脚注から初版を収集した `tools/lint/markdown-whitelist.yaml` の1ファイルを正本とし、単語名と説明の対を残す。
  - 生成物、外部からの取り込み資料、ビルド出力など、利用者の編集対象ではない文書は明示的に除外する。
  - 文書検査の導入意図、環境構築、実行方法、許可一覧の更新手順、対象と除外を、リポジトリ内の手順書に残す。
  - 委任した担当者による実装、検証、専用のレビューを通し、進捗文書を同期する。
  - 実装証跡:
    - `reports/doc-lint-001-implementation-20260516221628.md`
    - `reports/doc-lint-001-prh-integration-20260517104319.md`
    - `textlint-rule-prh` を `textlint` に追加し、表記揺れ辞書 `tools/lint/prh.yml` を文書検査に組み込んだ。初期辞書では具体的な表記統一規則を登録しない。
  - レビュー証跡:
    - `reports/doc-lint-001-review-20260516233048.md`
    - 当時の全範囲の文書検査は、既存文書の未登録語を大量に許可して勝手に通さないため、意図的に失敗のままとした。専用の許可一覧の内容変更には、利用者の明示的な確認が必要。

- ID: `DOC-LINT-002`
- 題名: SudachiPy で文書語彙を抽出し、許可一覧検査で日本語を形態素単位に解析する
- 段階: 文書検査整備
- 状態: PR #20 公開中、追加整備は `DOC-LINT-003` で追跡
- 規模: 中
- 依存関係: `DOC-LINT-001` の文書検査対象列挙と許可一覧設定。
- 完了条件:
  - SudachiPy と辞書の Python 依存物を、リポジトリ内の環境構築手順に残す。
  - 利用者編集対象の文書から、英字語、片仮名語、漢字を含む日本語語彙を抽出し、頻度と出現元を集計できる。
  - SudachiPy の正規形、読み、品詞を語彙再構築の候補情報として出力できる。
  - 許可一覧検査が、日本語語彙を文字種正規表現だけではなく SudachiPy の形態素単位で扱える。
  - 対象文書の直接指定と変更分指定を維持する。
  - `namespace` / `ネームスペース` / `名前空間` のような意味対応は自動確定せず、候補として提示し、最終的な許可一覧更新は利用者の明示確認に委ねる。
- 実装証跡:
  - `reports/doc-lint-002-sudachipy-vocabulary-20260517104319.md`
  - `lint:md:vocab` は `tools/lint/README.md` 直接指定で頻度降順の語彙一覧を出力した。
  - `lint:md:whitelist` は SudachiPy 版へ切り替えた。当時は漢字語の検出により、許可一覧の内容では意図的に失敗していた。
- レビュー証跡:
  - `reports/doc-lint-002-review-20260517104319.md`
  - `reports/doc-lint-002-review-r2-20260517104319.md`
  - 初回レビューでは、`--stdin` で対象を絞った検査の互換性に、完了を妨げる指摘があった。修正後の2回目のレビューでは指摘なし。

## 完了済み作業

- `RUNTIME-HOST-001`: `Tracker.RuntimeHost` / `Tracker.DebugHost` の分離方針と設計資料の統合を完了した。設計資料と進行中の作業を管理する文書を `Tracker/Design/` へ統合し、両者の命名、責務境界、将来の自動レフェリーの組み込み、処理周期の分離、旧ログとの互換性を必須としないこと、`BreakingChanges` が不要であることを設計に固定した。`gpt-5.5 high` による初回レビューの阻害指摘2件を修正し、2回目には指摘なしを確認した。下書き PR #17 を作成した。
  - レビュー証跡:
    - `reports/runtime-host-001-design-review-20260514155548.md`
    - `reports/runtime-host-001-design-fix-20260514160144.md`
    - `reports/runtime-host-001-design-review-r2-20260514160734.md`
- `RUNTIME-HOST-002`: 両プロジェクトの依存境界を検査する契約テストを追加した。`Tracker.RuntimeHost` が `Tracker.DebugHost` / `Tracker.Server` / Web UI / 診断再生 UI のプロジェクトを参照しないこと、そのソースコードが診断ログ / 再生 / Blazor UI の名前空間を直接参照しないこと、`Tracker.DebugHost` が読み取りを担当することを、実装に先行する失敗テストで固定した。
  - 実装証跡:
    - `reports/runtime-host-002-implementation-20260514163841.md`
    - `reports/runtime-host-002-boundary-context-20260514164124.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests -m:1 /nr:false` は3件失敗 / 0件成功。当時は `Tracker.RuntimeHost` のプロジェクトとソースコードのディレクトリ、および `Tracker.DebugHost` のディレクトリが存在せず、意図した契約テストの検証失敗になっていた。
  - レビュー証跡:
    - `reports/runtime-host-002-review-20260514164528.md`
    - `reports/runtime-host-002-review-fix-20260514164850.md`
    - `reports/runtime-host-002-review-r2-20260514165133.md`
    - 2回目のレビューで阻害指摘なし。`Tracker.DebugHost` が追跡処理の周期を担当するかの検査に使う目印については、将来の誤検出の可能性を保留事項として記録した。
- `RUNTIME-HOST-003`: diagnostics sample tick の境界と、旧形式では機能が制限される契約を追加した。diagnostics sample tick が追跡フレームの確定周期や `WorldFrameCommitted` に依存しないこと、診断画面の `Vision Input` が diagnostics sample sidecar から復元されること、旧形式の render snapshot を保存する補助ファイルは非対応または機能制限付きで扱うことを、実装に先行する失敗テストで固定した。設計文書の作業参照も固定一覧へ同期した。
  - 実装証跡:
    - `reports/runtime-host-003-implementation-20260514165750.md`
    - `reports/runtime-host-003-boundary-context-20260514165750.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDiagnosticsSampleBoundaryContractTests -m:1 /nr:false` は3件失敗 / 0件成功。`RuntimeHostDiagnosticsSampleBoundaryContractTests` はビルド済みで、diagnostics sample sidecar と旧形式の機能制限表示が未実装であることを、検証の失敗として固定している。
  - レビュー証跡:
    - `reports/runtime-host-003-review-20260514170652.md`
    - レビューで阻害指摘なし。diagnostics sample sidecar の保存形式と、raw vision の入力データを表す DTO の詳細は、`RUNTIME-HOST-007` で実装を成功させる際に確認する保留事項として記録した。
- `RUNTIME-HOST-004`: `Tracker.Server` を `Tracker.DebugHost` というプロジェクト名、名前空間、起動経路へ変更した。使用中のプロジェクト、名前空間、起動経路、README、`Duck.slnx` とプロジェクトの参照、`Tracker.CaptureReplay` とテストからの参照を `Tracker.DebugHost` に揃え、既存の診断用機能の正常動作を維持した。
  - 実装証跡:
    - `reports/runtime-host-004-implementation-20260514171550.md`
    - `reports/runtime-host-004-rename-impact-20260514171550.md`
    - `reports/runtime-host-004-verification-20260514172634.md`
    - 実装前: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDebugHostRenameContractTests -m:1 /nr:false` は3件失敗 / 0件成功。`Tracker.DebugHost` のフォルダとプロジェクトが存在せず、使用中の参照も未更新であることを、検証の失敗として確認した。
    - 実装後: 同じ対象のテストは 3 件成功。`dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false`、`dotnet build Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -m:1 /nr:false`、`dotnet build Duck.slnx -m:1 /nr:false` は成功した。
  - レビュー証跡:
    - `reports/runtime-host-004-review-20260514172921.md`
    - レビューで阻害指摘なし。`Tracker.Tests` 全体は、`RUNTIME-HOST-002` / `RUNTIME-HOST-003` の既存の失敗テストがあるため、当時は未実行とした。
- `RUNTIME-HOST-005`: トラッカーの周期処理で共有する実行時の責務を `Tracker.Core/Runtime` へ抽出した。`TrackerCoordinator`、`ITrackerPacketPublisher`、`TrackerPublisherOptions`、`TrackedSnapshot`、`TrackedSnapshotStore`、`UdpTrackerPacketPublisher` を UI に依存しない `Tracker.Core` の実行時処理へ移し、`Tracker.DebugHost` は UDP のデコード、未加工入力の保存、キャプチャーの後に `TrackerCoordinator` を呼ぶ接続部分とした。旧診断ログと render snapshot を保存する補助ファイルの生成は共通の周期処理から外し、性能を優先して `Tracker.RuntimeHost` から再利用できる責務境界を固定した。
  - 実装証跡:
    - `reports/runtime-host-005-implementation-20260514180031.md`
    - `reports/runtime-host-005-verification-20260514180308.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostSharedOperationLoopBoundaryTests|FullyQualifiedName~TrackerCoordinatorFrameFlowTests|FullyQualifiedName~TrackerCoordinatorResetAndProfileTests|FullyQualifiedName~TrackerProfileRequestServiceTests|FullyQualifiedName~TrackerCoordinatorDiagnosticsCaptureTests" -m:1 /nr:false` は 15 件成功。
    - `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false` は成功し、`git diff --check` も成功した。
  - レビュー証跡:
    - `reports/runtime-host-005-review-20260514180308.md`
    - レビューで阻害指摘なし。`Tracker.DebugHost` の UI を読み取り側へ分離すること、diagnostics sample sidecar、`Tracker.RuntimeHost` の起動に必要な最小構成は、`RUNTIME-HOST-006` 以降へ残す。
- `RUNTIME-HOST-006`: `Tracker.DebugHost` のライブ表示を、スナップショットを読む側の責務へ分離した。`VisionLiveDisplaySnapshotProvider` が1回の描画更新で raw vision / 自前トラッカー / 外部トラッカーのスナップショットを固定する。`Home.razor` は未加工入力と追跡結果の保存先を直接受け取らず、同じ合成スナップショットから `Raw` / `Tracked` / `Compare` を作る。`ExternalTrackerSnapshotStore` は `MultiTrackerManager` の更新通知からパケットと付随情報を複製した DTO を保持し、描画処理が管理処理の変更可能な状態を直接読まない構造にした。
  - 実装証跡:
    - `reports/runtime-host-006-boundary-context-20260514181333.md`
    - `reports/runtime-host-006-implementation-20260514182342.md`
    - `reports/runtime-host-006-verification-20260514182549.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDebugHostReadSideSnapshotBoundaryTests|FullyQualifiedName~VisionLiveComparisonViewStateTests|FullyQualifiedName~TrackedVisionViewStateTests" -m:1 /nr:false` は 18 件成功。
    - `dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false` と `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false` は成功し、`git diff --check` も成功した。
  - レビュー証跡:
    - `reports/runtime-host-006-review-20260514182549.md`
    - レビューで阻害指摘なし。diagnostics sample sidecar と `Tracker.RuntimeHost` の起動に必要な最小構成は、`RUNTIME-HOST-007` 以降へ残す。
- `RUNTIME-HOST-007`: `Tracker.DebugHost` に diagnostics sample sidecar を高速に読み書きする経路を実装した。UI に依存しない `DiagnosticsSampleHostedService` が `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` に従って、最新の未加工入力と追跡結果のスナップショットを `diagnostics-samples.jsonl` へ保存する。診断再生と `Field` は、diagnostics sample sidecar の参照量を制限した検索と、パケットの内容を要約した情報を主に使う。旧形式の render snapshot を保存する補助ファイルしか持たないキャプチャーは非対応または機能制限付きで扱い、高負荷な互換経路は復活させない。
  - 実装証跡:
    - `reports/runtime-host-007-implementation-20260514184219.md`
    - `reports/runtime-host-007-review-fix-20260514185807.md`
    - `reports/runtime-host-007-configurable-sample-interval-20260514191628.md`
    - `reports/runtime-host-007-verification-20260514184527.md`
    - 対象と影響範囲を絞ったテスト、`Tracker.DebugHost` と `Tracker.Tests` のビルド、`git diff --check` は、委任した担当者の報告で成功を確認した。
  - レビュー証跡:
    - `reports/runtime-host-007-review-20260514184501.md`
    - `reports/runtime-host-007-review-r2-20260514190459.md`
    - `reports/runtime-host-007-review-r3-20260514191820.md`
    - `reports/runtime-host-007-review-r4-20260514192425.md`
    - 初回レビューの阻害指摘2件を修正し、2回目・3回目・4回目のレビューでは指摘なしを確認した。
- `RUNTIME-HOST-008`: `Tracker.RuntimeHost` が画面なしで起動するための最小構成と設定を追加した。プロジェクト、`Program`、設定と DI の初期化処理、`Duck.slnx` への登録を追加し、Web UI / 診断再生 / キャプチャー確認画面を持たずに起動できる構成を作った。`RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開し、0以下では起動時の設定検証が失敗する契約を追加した。
  - 実装証跡:
    - `reports/runtime-host-008-implementation-20260514192917.md`
    - `RUNTIME-HOST-008` に対象を調整したテストは7件成功。広い範囲のテストは23件成功 / 1件失敗で、失敗した1件は、この作業の範囲外にある既存の `Tracker.DebugHost` の周期処理の責務に関する検証だと、レビューで確認した。
    - `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`、`dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`、`git diff --check` は、委任した担当者の報告で成功を確認した。
  - レビュー証跡:
    - `reports/runtime-host-008-review-20260514193633.md`
    - `reports/runtime-host-008-review-fix-20260514194021.md`
    - `reports/runtime-host-008-review-r2-20260514194042.md`
    - 初回レビューで完了を妨げていた XML の概要コメントを修正し、2回目のレビューで指摘なしを確認した。
- `RUNTIME-HOST-009`: `Tracker.RuntimeHost` の周期処理と公式パケット送信の正常経路を実装した。画面なしで動く SSL-Vision 受信処理、最新パケットを保持するバッファ、`RuntimeHost:OperationLoopIntervalMilliseconds` に従う周期処理、`Tracker.Core` の `TrackerCoordinator` / `TrackedSnapshotStore` / `UdpTrackerPacketPublisher` を DI で組み合わせた。テスト用の SSL-Vision 入力が調停処理、送信処理、最新スナップショットの保存先に届く正常経路を固定した。指定された設定プロファイルが存在しない場合は、`Tracker.DebugHost` と同じく明示的に失敗する。
  - 実装証跡:
    - `reports/runtime-host-009-implementation-20260514194405.md`
    - `reports/runtime-host-009-review-fix-20260514200105.md`
    - `RUNTIME-HOST-009` の対象テストは3件成功。レビュー指摘の修正後、`RuntimeHostOperationLoopTests` は5件成功。広い範囲では26件成功 / 1件失敗で、失敗した1件は、この作業の範囲外にある既存の `Tracker.DebugHost` の責務に関する検証だと、レビューで確認した。対象を調整したテストでは26件成功。
    - `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`、`dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`、`git diff --check` は、委任した担当者の報告で成功を確認した。
  - レビュー証跡:
    - `reports/runtime-host-009-review-20260514195653.md`
    - `reports/runtime-host-009-review-r2-20260514200945.md`
    - 初回レビューで、指定された設定プロファイルが存在しない場合の代用処理に阻害指摘があり、修正後の2回目には指摘なしを確認した。最新パケットを保持するバッファは最新優先のままとし、`RUNTIME-HOST-010` の手動証跡を得てから判断する保留事項とした。
- `RUNTIME-HOST-010`: `Tracker.RuntimeHost` / `Tracker.DebugHost` の分離について、対象を絞った検証と手動証跡を揃えた。両者のテストとビルド、diagnostics sample sidecar の証跡、旧形式での機能制限の証跡、`Tracker.DebugHost` の UI の正常動作、`Tracker.RuntimeHost` の画面なしの正常動作を、委任した担当者の報告に残した。`.gitignore` に実行時と診断用キャプチャーの生成物を追加し、手元の生成物が通常の差分へ混入しないことを確認した。
  - 検証証跡:
    - `reports/runtime-host-010-validation-20260514201701.md`
    - `Tracker.RuntimeHost` の対象テストは10件成功。境界の確認対象を調整したテストは10件成功。診断機能の対象テストは15件成功。
    - `Tracker.RuntimeHost` / `Tracker.DebugHost` / `Tracker.Tests` のビルド、画面なしでの短時間起動、`Tracker.DebugHost` の HTTP 200 応答、`git diff --check`、`.gitignore` の生成物除外は、委任した担当者の報告で成功を確認した。
    - 広い範囲のテストでは、既知の `RuntimeHostDependencyBoundaryContractTests.DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop` の1件だけが失敗した。当時の設計は `Tracker.DebugHost` から `Tracker.Core` の周期処理への接続部分を残すことを許容していたため、この作業の完了を妨げるものではなく、`RUNTIME-HOST-011` の最終レビューで扱う保留事項とした。
  - レビュー証跡:
    - `reports/runtime-host-010-review-20260514202428.md`
    - レビューで阻害指摘なし。既知の `Tracker.DebugHost` の責務に関する検証失敗は、保留を続けることが妥当と確認した。
- `RUNTIME-HOST-011`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離の最終レビュー、進捗同期、PR の提出準備を完了した。最終レビューで、リポジトリに残る失敗した契約テストが完了を妨げると判定されたため、`RuntimeHostDependencyBoundaryContractTests` を当時の設計へ合わせた。`Tracker.DebugHost` 全体から `Tracker.Core` の周期処理への接続を禁止する契約ではなく、UI / 診断再生 / 描画元のソースコードが周期処理を直接駆動しない契約に限定した。2回目のレビューで阻害指摘なしとなり、PR を提出できると確認した。
  - レビュー証跡:
    - `reports/runtime-host-011-final-review-20260514203109.md`
    - `reports/runtime-host-011-review-fix-20260514203809.md`
    - `reports/runtime-host-011-final-review-r2-20260514204526.md`
    - 初回の最終レビューでは、リポジトリに残る失敗した契約テストが完了を妨げると判定された。修正後、`RuntimeHostDependencyBoundaryContractTests` は3件成功、分離と境界の対象テストは11件成功、`Tracker.Tests` のビルドと `git diff --check` は成功。2回目のレビューで指摘がなく、PR を提出できると確認した。

## 固定した作業範囲（当時の計画）

- 固定一覧は `RUNTIME-HOST-001` から `RUNTIME-HOST-011` とする。両プロジェクトの分離の範囲では、`RAW-VISION-*` や `TRACKER-*` を追加しない。
- `RUNTIME-HOST-001`: 分離方針と設計資料の統合を完了する。設計資料を `Tracker/Design/` へ移し、進行中の作業の管理文書を統合する。両プロジェクトの責務境界、将来の自動レフェリーの組み込み、処理周期の分離、旧ログとの互換性を必須としない方針を設計へ反映する。
- `RUNTIME-HOST-002`: 両プロジェクトの依存境界を検査する契約テストを追加する。`Tracker.RuntimeHost` が `Tracker.DebugHost` / Web UI / 診断再生 UI に依存しないこと、`Tracker.DebugHost` がトラッカーの周期処理の主責務を持たず、読み取りを担当することを、実装に先行する失敗テストで固定する。
- `RUNTIME-HOST-003`: diagnostics sample tick の境界と、旧形式では機能が制限される契約を追加する。diagnostics sample tick が追跡フレームの確定周期に依存せず、診断画面の `Vision Input` が diagnostics sample sidecar から復元され、旧形式の render snapshot を保存する補助ファイルは非対応または機能制限付きで扱うことを、実装に先行する失敗テストで固定する。
- `RUNTIME-HOST-004`: `Tracker.Server` を `Tracker.DebugHost` というプロジェクト名、名前空間、起動経路へ変更する。Web UI / 診断 / 再生 / キャプチャー確認画面の責務を明確にし、既存の診断用機能の正常動作を壊さない。
- `RUNTIME-HOST-005`: トラッカーの周期処理で共有する実行時の責務を抽出する。SSL-Vision 入力、追跡状態の更新、公式トラッカーパケットの送信、最新の追跡スナップショットの公開を UI と診断保存から分離し、`Tracker.RuntimeHost` から再利用できる形にする。
- `RUNTIME-HOST-006`: `Tracker.DebugHost` のライブ表示を、スナップショットを読む側の責務へ分離する。UI の描画更新ごとに raw vision / 自前トラッカー / 外部トラッカーの変更されない最新スナップショットを固定し、Web UI の描画更新がトラッカーの周期処理を駆動しない構造にする。
- `RUNTIME-HOST-007`: `Tracker.DebugHost` に diagnostics sample sidecar を高速に読み書きする経路を実装する。diagnostics sample tick で最新の未加工入力と追跡結果のスナップショットを固定して保存する。新規キャプチャーでは、保存された診断ログを参照量を制限して検索する経路を主に使う。
- `RUNTIME-HOST-008`: `Tracker.RuntimeHost` が画面なしで起動するための最小構成と設定を追加する。Web UI / 診断再生 / キャプチャー確認画面を持たないプロジェクト、`Program`、設定と DI の初期化処理、`Duck.slnx` への登録を追加する。`RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開する。
- `RUNTIME-HOST-009`: `Tracker.RuntimeHost` の周期処理と公式パケット送信の正常経路を実装する。SSL-Vision 入力、追跡状態の更新、公式トラッカーパケットの送信、`Tracker.DebugHost` が読める最新の追跡スナップショットの公開を、画面なしで成立させる。実行周期は `RuntimeHost:OperationLoopIntervalMilliseconds` で制御する。
- `RUNTIME-HOST-010`: 両プロジェクトの分離について、対象を絞った検証と手動証跡を揃える。ビルド、対象テスト、diagnostics sample sidecar の証跡、旧形式での機能制限、`Tracker.DebugHost` の UI の正常動作、`Tracker.RuntimeHost` の画面なしの正常動作を報告書に残す。
- `RUNTIME-HOST-011`: 分離の最終レビュー、進捗同期、PR の提出準備を完了する。`gpt-5.5 high` によるレビュー、必要な修正と再レビュー、進捗同期、報告書の参照、検証証跡、下書き PR #17 を提出できる状態への更新を完了する。

## 統合した履歴

- `Tracker.Core` と追跡エンジンの旧進捗文書は、`Tracker/Design/Archive/Core/tasks-status.md` と `Tracker/Design/Archive/Core/phases-status.md` に保存する。
- `Tracker.DebugHost`、raw vision、診断機能の旧進捗文書は、`Tracker/Design/Archive/DebugHost/tasks-status.md` と `Tracker/Design/Archive/DebugHost/phases-status.md` に保存する。
- 旧 `RAW-VISION-013` から `RAW-VISION-016` は、PR #15 `Issue #10 Vision画面に分割表示とオーバーレイを追加する` として `2026-05-14T03:29:25Z` に取り込み済み。
- `RAW-VISION-017` として開始した処理周期の分離設計は、`Tracker.RuntimeHost` / `Tracker.DebugHost` の分離方針へ対象を拡張したため、以後は `RUNTIME-HOST-001` へ統合する。

## 作業一覧（当時の完了記録）

| ID | 作業 | 段階 | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| `RUNTIME-HOST-001` | 分離方針と設計資料を統合する | 設計 | 完了、下書き PR #17 | PR #15 取り込み完了 | 設計資料と進行中の作業の管理文書を `Tracker/Design/` へ統合し、両プロジェクトの命名、責務境界、将来の自動レフェリーの組み込み、処理周期の分離、旧ログとの互換性を必須としない方針、`BreakingChanges` が不要なことを固定した。`gpt-5.5 high` による2回目のレビューで阻害指摘なし。 |
| `RUNTIME-HOST-002` | 両プロジェクトの依存境界を検査する契約テストを追加する | 検証 | 完了、下書き PR #17 | `RUNTIME-HOST-001` | `Tracker.RuntimeHost` が `Tracker.DebugHost` / Web UI / 診断再生 UI に依存せず、`Tracker.DebugHost` がトラッカーの周期処理の主責務を持たず読み取りを担当することを、実装に先行する失敗テストで固定した。2回目のレビューで阻害指摘なし。 |
| `RUNTIME-HOST-003` | diagnostics sample tick の境界と旧形式の機能制限の契約を追加する | 検証 | 完了、下書き PR #17 | `RUNTIME-HOST-002` | diagnostics sample tick は追跡フレームの確定周期や `WorldFrameCommitted` に依存しない。`Vision Input` は diagnostics sample sidecar から復元し、旧形式の render snapshot を保存する補助ファイルは非対応または機能制限付きで扱うことを、実装に先行する失敗テストで固定した。レビューで阻害指摘なし。 |
| `RUNTIME-HOST-004` | Tracker.Server のプロジェクト名、名前空間、起動経路を Tracker.DebugHost へ変更する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-003` | Web UI / 診断 / 再生 / キャプチャー確認画面の責務を `Tracker.DebugHost` として明確にした。既存の診断用機能の正常動作、README、起動設定、`Duck.slnx` とプロジェクトの参照を維持した。レビューで阻害指摘なし。 |
| `RUNTIME-HOST-005` | トラッカーの周期処理で共有する実行時の責務を抽出する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-004` | UI に依存しない共通の周期処理、送信処理、最新スナップショットの保存を `Tracker.Core/Runtime` へ抽出し、`Tracker.DebugHost` は `TrackerCoordinator` を呼ぶ接続部分とした。対象テスト、ビルド、レビューを実施し、阻害指摘なし。 |
| `RUNTIME-HOST-006` | Tracker.DebugHost のライブ表示をスナップショットの読み取り側へ分離する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-005` | `VisionLiveDisplaySnapshotProvider` と `ExternalTrackerSnapshotStore` により、UI の描画更新ごとに変更されない最新スナップショットを固定し、Web UI の描画更新がトラッカーの周期処理を駆動しないことを、対象テスト、ビルド、レビューで確認した。 |
| `RUNTIME-HOST-007` | Tracker.DebugHost に diagnostics sample sidecar を高速に読み書きする経路を実装する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-003`, `RUNTIME-HOST-006` | UI に依存しない `DiagnosticsSampleHostedService` が `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` に従い、最新の未加工入力と追跡結果のスナップショットを `diagnostics-samples.jsonl` へ保存する。診断再生と `Field` は、diagnostics sample sidecar の参照量を制限した検索と、パケットの内容を要約した情報を主に使う。対象と影響範囲を絞ったテスト、ビルド、差分検査は担当者の報告で成功。初回レビューの阻害2件を修正し、2回目は成功。周期を設定可能にした後の3回目、RuntimeHost の周期設定要件を追加した後の4回目には指摘なし。 |
| `RUNTIME-HOST-008` | Tracker.RuntimeHost が画面なしで起動するための最小構成と設定を追加する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-005` | Web UI / 診断再生 / キャプチャー確認画面を持たないプロジェクト、`Program`、設定と DI の初期化、`Duck.slnx` への登録を追加し、トラッカー単独と将来の自動レフェリー組み込みの境界を表現した。`RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開し、0以下では起動時の検証が失敗する契約を、対象テスト、ビルド、レビュー、コミット、下書き PR #17 の更新とともに固定した。 |
| `RUNTIME-HOST-009` | Tracker.RuntimeHost の周期処理と公式パケット送信の正常経路を実装する | 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-007`, `RUNTIME-HOST-008` | SSL-Vision 入力を受け、`RuntimeHost:OperationLoopIntervalMilliseconds` に従って追跡状態を更新し、公式トラッカーパケットを送信し、`Tracker.DebugHost` が読める最新の追跡スナップショットを公開する正常経路を、対象テスト、ビルド、レビュー、コミット、下書き PR #17 の更新とともに成立させた。 |
| `RUNTIME-HOST-010` | 両プロジェクトの分離について対象検証と手動証跡を揃える | 確認 | 完了、下書き PR #17 | `RUNTIME-HOST-009` | 両プロジェクトの対象テストとビルド、diagnostics sample sidecar の証跡、旧形式の機能制限、DebugHost の UI の正常動作、RuntimeHost の画面なしの正常動作を報告書に残した。作業ごとのレビューで指摘なし。 |
| `RUNTIME-HOST-011` | 両プロジェクトの分離の最終レビュー、進捗同期、PR の提出準備を完了する | 確認 | 完了、PR #17 提出可能 | `RUNTIME-HOST-010` | 最終レビュー、阻害指摘の修正、再レビュー、進捗同期、報告書の参照、検証証跡、コミット履歴、下書き PR #17 の説明更新、提出可能であることの判断を完了した。 |

## 文書検査の範囲確認（2026-09-17）

`DOC-LINT-003` の補足として、公開済みのMarkdown差分96文書を検査対象と照合した。本文19文書は比較元の一覧と一致し、報告書77文書は既存設定による対象外だった。検査手順の実行例を、存在する2文書を指定する例へ訂正した。関連する原出現3件の最終位置と理由を記録した。

4,336出現箇所の原文・行・列・表記と、履歴保存版4文書の内容は比較元と一致した。この位置情報と原文保全の確認を、最終表現の全件確認や独立最終レビューの合格には扱わない。全体の文書検査は引用内の違反で未通過のままである。

証跡: `reports/task-doc-lint-003-scope-check-20260917082040.md`。元の完了条件と過去の検証結果は変更していない。

## 追跡エンジンの本文照合（2026-09-17）

`DOC-LINT-003` の補足として、追跡エンジンの詳細設計にある原文133出現を、最終表現の行・列・前後の文脈・判断理由へ対応付けた。86行の文脈を読み、変更のない行の23出現も記録した。曖昧になっていた XML documentation comment の `summary` 要素の指定を復元した。

入力の `SourceFrameNumber` と出力の `FrameNumber`、時刻の選択、初期化の対象、secondary ball の追跡が確立する条件を実装と照合した。この文書の照合結果を、全4,336出現の台帳への統合や独立最終レビューの完了には扱わない。全体の文書検査は、原文を保持した引用内の違反で未通過のままである。

証跡: `reports/task-doc-lint-003-engine-correspondence-20260917084612.md` と `reports/diagnostics/wording-integrity-20260917-0827/engine-occurrence-correspondence.json`。元の完了条件、過去の採否、検証記録は変更していない。
