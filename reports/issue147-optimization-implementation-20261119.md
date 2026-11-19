# 課題147 近接重複判定の実装報告

## タスク

- 目的: PR #149 の境界・割り当て回帰試験を先行し、PR #148 の計測基盤を取り込んだ後に最小の最適化を検証する。
- タスク種別: 実装・局所検証。ブランチ perf/issue147-near-duplicate、開始HEAD cfd684787960581a7eeb87944b626b7b12310de5。
- 進め方の更新: 当初は PR #148 の取り込みを待つ予定だった。対象判定は計測ツールに依存せず、実装前の失敗を確認した試験で局所検証できるため、親の指示で判定の変更と同じ試験での確認を先行した。正式な再生比較は PR #148 取り込み後に行う。

## sub-agentを使う理由

- 理由: 先行ツールの実装とは別作業木で独立する境界試験を先に用意し、検証実行を親から分離する。

## 対象範囲

- 対象: 近接重複判定の境界と割り当ての回帰試験、親が続行を指示した後の同判定の最適化、当該報告。

## 対象外

- 対象外: 先行ツールの変更、Git操作、Issue/PR/進捗更新、しきい値と設定、通信構成、外部資源、生ログの複製。

## Dispatch profile

- selection inputs: implementation / bounded_technical / uncertainty medium / local / ordinary / single。
- selection source: codex-delegation-executor、sub-agent-task-manager、本人指定6.2 Sol High。
- observed decomposability: independent_workstreams（先行ツールと、既存Coreに対する試験の作成は別作業木で分離）。
- decomposition policy / disposition: allowed / 試験作成だけ先行し、最適化と実測は先行PRの取り込み後に直列化する。
- requested profile: gpt-6.2-sol / high / fork none。
- agent role / default-role plan: runtime default role。確認した構成行にagentsによるprofile変更なし。
- planned runtime profile: gpt-6.2-sol / high。
- applied profile: null。
- application status: spawn_succeeded_profile_unverified。実装担当 /root/issue147_optimization_impl が完了し、最終profileの値は観測できていない。
- runtime profile observability: final_profile_hidden。
- fork policy: none。
- reasons / constraints: 所有ファイルが分かれ、テスト作成の待ち時間を減らす。A/B負荷測定は同時実行しない。MCP/RDMCP/WSL禁止、FA780の既知承認経路のみ。

## 実行コマンド

FA780 のコマンド単位の承認経路で `whoami` を確認し、専用作業木 `Duck-opt` の `perf/issue147-near-duplicate` と開始 HEAD `cfd684787960581a7eeb87944b626b7b12310de5` を照合した。PowerShell 7.6.10、.NET SDK 10.0.404 のローカル実行。プロトコル生成に必要な既存の2サブモジュールを `git submodule update --init --` で初期化し、固定コミット `9b0765fe7a07a55124f6ee957d4559348462d227` と `1e719a8d410cc84847ec69104da44fed13738291` を確認した。

`$validation` は実装前の作業木外 `task-23/validation/issue147-red`、`$feed` は `task-23/empty-feed`。既存の NuGet キャッシュと `task-23/dotnet-home` を使用し、`DOTNET_GENERATE_ASPNET_CERTIFICATE=false` を実行プロセスに設定した。変更後も同じ実行主体、SDK、試験条件を使い、`$validation` を `task-23/validation/issue147-green`、TRX 名を `issue147-robot-contracts-green.trx` に置き換えた。

```powershell
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -c Release `
  --filter 'FullyQualifiedName~TrackerEngineCloseRobotDuplicateContractTests|FullyQualifiedName~TrackerEngineRobotTrackingContractTests' `
  --results-directory $validation --logger 'trx;LogFileName=issue147-robot-contracts-red.trx' `
  --logger 'console;verbosity=normal' --diag (Join-Path $validation 'vstest-diagnostics.log') --blame `
  -m:1 /nr:false '-p:UseSharedCompilation=false' "-p:RestoreSources=$feed" "-bl:$validation/dotnet-test.binlog"
```

## 対象ファイル

- `Tracker/Tracker.Tests/Contracts/TrackerEngineCloseRobotDuplicateContractTests.cs` に近接重複判定の回帰試験を追加した。既存の C# ファイルに合わせて BOM なしの UTF-8 と LF を確認した。
- 新規試験ファイルの SHA-256 は `8def14819e03ec0f03eacb485f7a5189242b6cd2642809d000fba1a3432f4b0f`。`git diff --no-color --no-ext-diff --full-index --no-index -- /dev/null` が生成した追加差分を LF で `test-only.diff` に保存し、SHA-256 `ae37b65395294ffa22409f8e5f9fa92ae428c240949d91231a992fc3790b4126` を記録した。
- 実装前に既存の `TrackerEngineRobotTrackingContractTests.cs` と製品の `RobotTracking.cs` が開始 HEAD と一致することを `git diff --exit-code HEAD --` で確認した。既存試験の SHA-256 は `4c7e9da0ad6acefdcf22ea161e3ec9a6a47a990cf2c9cd341460c7c49ed06c85`、製品ファイルは `846752e37c6c9b44f4e0c15fe15f2515a7598cee126eddd0b8bf17741e91cbc8`。変更後に両試験ファイルの指紋と TRX の試験名 31 件の一致を確認した。
- `Tracker/Tracker.Core/Engine/TrackerEngine/RobotTracking.cs` の `HasCloseRobotObservationWithDifferentId` は、呼び出し元の `Dictionary` を `foreach` で順に確認し、同一カメラ・同一チームの別 ID が 135 mm 未満にあれば最初の一致で返す。変更範囲は private メソッドと日本語 XML 説明。変更後のファイルは BOM なしの UTF-8 と LF で、SHA-256 は `c9860a2438c971b86f84c8e4ef3446964c1415f51e1320c90c4c1457c2eb0840`。対象メソッドと説明だけをメモリ上で元に戻すと実装前の指紋と一致した。

## 指摘事項

- 実装前の失敗は `HasCloseRobotObservationWithDifferentId_DoesNotAllocatePerCall` の割り当て上限だけで確認した。上限は 4,096 バイト。近接と遠方を各 10,000 回呼び、実装前・変更後とも判定が真になる件数は期待する 10,000 件と一致した。
- 観測の作成・登録、式からデリゲートを生成する処理、同じ入力での暖機、アサーション、結果出力は計測区間の外に置いた。計測では現在のスレッドが判定ループ中に割り当てたバイト数を採取する。
- ビルドは `AspireUseCliBundle=false` に対する `ASPIRE010` を出力し、対象試験まで実行した。

## 結果

| Release 局所試験 | 終了値 | 成功 | 失敗 | 未実施 | 近接重複判定 20,000 回の割り当て |
| --- | ---: | ---: | ---: | ---: | ---: |
| 実装前 | 1 | 30 | 1 | 0 | 3,040,000 バイト（152 バイト/回） |
| 変更後 | 0 | 31 | 0 | 0 | 0 バイト |

- 新規試験は 135 mm の直前・等号・直後、負方向と斜めの等号、カメラ・チーム・ID の違い、辞書への観測挿入順を確認する。公開の `TrackerEngine.Update` を通して、信頼度降順、同率時の ID 昇順、同じ ID の後勝ちも確認した。
- 作業木外の `task-23/validation/issue147-red` に実装前の失敗を保持し、`task-23/validation/issue147-green` に変更後の成功を保存した。それぞれの TRX は `issue147-robot-contracts-red.trx` と `issue147-robot-contracts-green.trx`。標準出力と標準エラー、VSTest 診断、MSBuild バイナリログ、終了値、実行元の指紋も保存した。割り当て値は両 TRX の該当試験の個別出力で確認した。

## リスク

- 割り当ての数値は固定した観測辞書に対する近接判定単体の結果。周期全体の追跡・配信にかかる時間や割り当ては後続の同一入力再生で確認する。
- 全体検証、再生での出力ハッシュ・出力件数・性能値の比較、レビューと CI は未実施。

## 親による正式比較の整理

上記の局所実装時の未実施事項のうち、先行候補の取り込みと正式A/Bは完了した。同じ通常担当が追加確認し、必須指摘なしとした。測定対象の完全SHA、入力・出力と設定の照合、数値とばらつきは通常レビュー報告の追記と「issue-147-optimization-readiness-20261120011655.md」に記録する。先行PRの独立レビューで見つかった受信順修正は別工程として取り込み、正式測定を新HEADでの再測定とは読み替えない。全体CIと独立最終レビューは以後の工程である。
