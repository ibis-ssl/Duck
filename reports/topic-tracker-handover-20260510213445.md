# Tracker handover 2026-05-10 21:34:45

## 次チャットへ貼る依頼

`/home/ibis/ssl/IbisDuck` で `development-orchestrator` に従って続けてください。

現在の作業は PR #5 `Tracker capture replay CLI を追加` の続きです。

- Repository: `https://github.com/ibis-ssl/Duck`
- Branch: `feat/tracker-capture-replay-tool`
- PR: `https://github.com/ibis-ssl/Duck/pull/5`
- Latest pushed commit: `c4b32bd fix(tracker): diagnostics詳細欄の余白を詰める`
- Working tree: `SslProto/src/external/ssl-game-controller` submodule が dirty 表示だが、今回の UI/diagnostics 作業では触っていない。勝手に stage/revert しないこと。

次にやること:

1. ユーザーが `/diagnostics` 画面を確認して追加の UI 指摘を出す可能性が高いので、指摘内容に合わせて `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor` と `.razor.css` を小さく調整する。
2. 追加調整後は `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore -m:1 /nr:false` を実行する。
3. CSS/UI の小修正ならレビューは不要。ユーザーが「もう一回レビュー実施は無駄」と明示済み。
4. 対象ファイルだけを commit し、`git push origin feat/tracker-capture-replay-tool` する。

## 目的

今回のチャットの主目的は、Tracker の「ボールが複数に分裂する」「存在しない黄色 11 番ロボットが出る」問題を調査できるように、packet capture、replay、diagnostics viewer を整備し、保存済み capture から再現・比較・表示できる状態へ持っていくことだった。

途中でユーザー要望により、`/diagnostics` の UI は実調査画面として何度も調整した。現在は field 2 面、timeline、scrubber、profile settings modal、Vision Input / Tracker Output の詳細表示を持つ状態。

## 重要な前提とルール

- 日本語で応答する。
- `AGENTS.md` に従い、開発作業は `development-orchestrator` を入口にする。
- Tracker 作業では `Tracker/Tracker.Core/Design/tasks-status.md` と `phases-status.md` が正。現在は TRACKER-027 まで done。
- ユーザーは UI を画面で確認しながら細かく直す進め方をしている。短い UI 修正は、build、commit、push まで自走してよい。
- ユーザーは今回の UI polish について「レビューをもう一回やるのは無駄」と明示している。追加 review / sub-agent review は勝手に実施しない。
- ただし本格的な仕様変更、設計変更、テスト影響が出る変更では通常 workflow に戻す。
- 未関係の `SslProto/src/external/ssl-game-controller` submodule dirty は触らない。

## 現在の repo 状態

- CWD: `/home/ibis/ssl/IbisDuck`
- Branch: `feat/tracker-capture-replay-tool`
- Upstream: `origin/feat/tracker-capture-replay-tool`
- Remote: `https://github.com/ibis-ssl/Duck`
- PR: #5 `Tracker capture replay CLI を追加`
- PR state: open
- Base: `main`
- Head: `feat/tracker-capture-replay-tool`
- Latest pushed commit: `c4b32bd`
- Dirty state: `m SslProto/src/external/ssl-game-controller` のみ。これは今回の作業外。

## 現在の tracker tracking 状態

`Tracker/Tracker.Core/Design/tasks-status.md`:

- 現在のタスク: `TRACKER-027`
- Title: Tigers 由来の近接重複 robot / 短命 ball 抑制を追加する
- Status: done
- 主要 report:
  - `reports/tracker-026-reproduction-analysis-20260510160509.md`
  - `reports/tracker-027-evidence-20260510161437.md`
  - `reports/tracker-027-review-20260510161549.md`

`Tracker/Tracker.Core/Design/phases-status.md`:

- 全体状況: done
- 残りフェーズ: none

この後の capture/replay/diagnostics viewer 作業は、TRACKER-027 完了後の調査支援 tooling / UI work として PR #5 に積んでいる。

## 主要な完了済み内容

### Capture replay

- `Tracker/Tracker.CaptureReplay` を追加。
- 保存済み `ssl-vision-packets-*.jsonl.gz` を `TrackerEngine` へ再投入できる CLI を実装。
- `--settings` で appsettings 形式または capture metadata 形式の外部設定を読み込める。
- `--profile`、`--expect`、`--detail-filter`、`--max-details` で検証・抽出できる。
- `tmp/.../Program.cs` のような別 exe 作成案はユーザーが拒否。既存の `Tracker/Tracker.CaptureReplay` に集約する方針。

### Packet capture / sidecar

- packet capture は `VisionReceiver:PacketCapture` 配下で制御。
- 起動時設定として `Enabled` は残す。起動直後から回したい場合があるため。
- UI から Capture On/Off 可能。
- capture 開始ごとに新しい capture session を作る。
- capture 出力は `packet-captures` ディレクトリに寄せる。
- `FlushEachPacket=true` の設定例を README に記載済み。起動時 capture は off でも flush 設定だけ true にできる。
- packet capture と同時に以下を同じ capture basename で sidecar 保存する:
  - `.jsonl.gz` packet
  - `.metadata.json`
  - `.tracker-diagnostics.log`
  - render snapshot
- ユーザーは一度「圧縮ファイル一つ」を希望したが、最終的には「一つのフォルダに 3 つ入る」方向でよいと変更した。

### Diagnostics viewer

`/diagnostics` で以下を表示できる:

- `packet-captures` を既定探索場所にする。
- diagnostics log file selector と Reload。
- timeline list。
- 下部 scrubber。
- `Vision Input Field` と `Tracker Output Field` の 2 面 field。
- 既存 `VisionFieldCanvas` を使った ball / robot 描画。
- Vision Input / Tracker Output の詳細文字列。
- capture metadata から profile settings を読める。
- profile settings は現在 `Profile` header 内の `Settings` ボタンから modal 表示。
- scrubber は `@oninput` で選択 entry を切り替える。さらに range 上の wheel で移動できる:
  - 通常: 1 entry
  - Shift: 10 entries
  - Ctrl: 100 entries

### UI polish の最新状態

直近の UI polish commits:

- `7b91088 fix(tracker): diagnosticsのstatus表示を統合する`
- `4ecade3 fix(tracker): diagnosticsの表示密度を上げる`
- `7830412 fix(tracker): diagnosticsのprofile設定をモーダル化する`
- `ff6ff13 fix(tracker): diagnosticsのrobot詳細枠を広げる`
- `c4b32bd fix(tracker): diagnostics詳細欄の余白を詰める`

最新の `c4b32bd` では `pre` 要素の標準 margin を潰し、Yellow / Blue / Robots 周辺の余計な隙間を詰めた。

## 検証済みコマンド

直近 UI 修正では毎回以下の Server build が成功している:

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore -m:1 /nr:false
```

最新実行結果:

- Build succeeded
- 0 Warning(s)
- 0 Error(s)

過去の大きい単位では full test / focused test / review report も取得済みだが、直近 UI polish はユーザー指示により追加レビュー不要。

## 重要な判断・決定

- diagnostics はボール専用 viewer ではなく、汎用の raw/tracked capture diagnostics viewer として扱う。
- Yellow / Blue / Robots などの詳細欄は、調査で読む文字情報を優先して大きくする。
- field と detail の間の空きは最小化する。
- field の中央黒丸は、`VisionFieldLines` の SVG fill 未指定が原因候補だったため、`line` / `path` / fallback `circle` / fallback `line` に `fill="none"` を明示した。
- Profile settings は通常レイアウト内に置くと縦領域を圧迫するため modal へ移した。
- `Profile settings` の中身は profile 名だけではなく、metadata 内の `TrackerOptions.Profiles` と `ResolvedTrackerOptions` を展開して確認できる必要がある。
- console log 抑制は ASP.NET Core logging の `Logging:LogLevel` で調整。packet capture 出力とは別で、log level を Warning にしても packet capture 自体は消えない。

## 未解決・次に注意すること

- ユーザーが `/diagnostics` 画面をさらに見て追加の余白・表示量・操作感の指摘を出す可能性が高い。
- UI は実ブラウザ画面での見た目が主なので、CSS 変更後は可能ならユーザーのスクリーンショット確認を優先する。
- 現状こちらでは Playwright screenshot verification は実施していない。直近は Server build のみ。
- `gh pr view` では PR #5 の `mergeStateStatus` が `UNKNOWN`。CI 状態は今回確認していない。
- `SslProto/src/external/ssl-game-controller` submodule dirty は作業外。触らない。
- PR #5 は open のまま。必要なら最後に PR description を更新するが、UI polish のたびに必須ではない。

## 参考 report

- `reports/topic-tracker-capture-replay-tool-20260510180956.md`
- `reports/topic-tracker-capture-replay-tool-review-20260510185110.md`
- `reports/topic-tracker-capture-replay-tool-review-r2-20260510191728.md`
- `reports/topic-tracker-capture-replay-tool-review-r3-20260510192129.md`
- `reports/topic-tracker-diagnostics-render-snapshot-review-20260510193226.md`
- `reports/topic-tracker-diagnostics-render-snapshot-review-r2-20260510193628.md`
- `reports/topic-tracker-capture-toggle-diagnostics-review-20260510200504.md`
- `reports/topic-tracker-diagnostics-scrubber-layout-review-20260510203435.md`
- `reports/topic-tracker-diagnostics-scrubber-layout-evidence-20260510204253.md`

## 再開時の推奨確認

```bash
git status --short --branch
git log --oneline -8
gh pr view --json number,title,url,state,headRefName,baseRefName,mergeStateStatus
```

UI 修正後の最小検証:

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore -m:1 /nr:false
```

commit 時は対象ファイルだけ stage すること。`SslProto/src/external/ssl-game-controller` は stage しない。
