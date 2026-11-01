# PR #96 作業報告案（Issue #102 / #103）

## 1. 従来の問題

比較モードの AppHost は Duck、TIGERs、ER-Force の live tracker 比較を開始できても、DebugHost の tracker 受信診断へ、この実行・コンテナの identity を帰属させる handoff が未接続でした。ContainerMode の strict readiness は帰属済みの全入力を要求するため、比較スタック全体の同一 run 受入を確認できませんでした。

## 2. 追加した機能

既存の prepare/challenge/commit Unix-domain-socket identity-control 経路を DebugHost に接続しました。AppHost の owned-container exec allowlist と比較モード登録を追加し、DebugHost が認証済み identity を受けて tracker diagnostics を新しい receive epoch に切り替えます。identity 変更時に Vision receiver の epoch も同期し、古い受信を拒否しつつ後続 Vision データの readiness を維持します。

## 3. 動作の変更

明示的な Desktop identity bridge が有効な比較モードでは、DebugHost が自身の実行 identity を受け取ってから tracker 入力を ready と評価します。Duck / TIGERs / ER-Force の役割・比較結果は既存の strict 条件のままです。Yellow identity の確認、HALT から FORCE_START への遷移、同じ Yellow robot ID の新しい Vision frame による 100 mm 以上の移動、比較完了後の最終 referee 読み取りを同一 acceptance run で確認しました。

## 4. 対象外

Game Controller / AutoRef 本体、host UDP 受信、Docker/OS/auth/network 設定、workflow、比較 acceptance 条件は変更していません。#97〜#101 の実装・証拠・判定もこの変更では更新・再判定していません。PR コメントは投稿していません。

## 5. 検証結果と証拠

- 実装 commit: `963cb2a20776ff3ac59963f891f7e172c718d652`（9 files, +534/-5）。独立レビューは指摘なし。Vision epoch の非干渉問題を修正し、再レビューで閉鎖しました。
- Release DebugHost publish と acceptance-enabled test assembly build は成功。focused tests は 4/4 成功。TRX: `Tracker/Tracker.Tests/TestResults/debughost-identity-handoff-epoch-fix.trx`。
- Windows AppHost + Docker Desktop の同一 run `issue102-dcp-20261101-44269e5d5fa8` で、filtered acceptance は 1/1 成功。branch head は上記 commit、Docker context は `desktop-linux`、engine ID は `5bc5ab8a-7340-47ba-8b7c-9f645375a54c`。
- 同一 run の観測: Yellow team `crane`; HALT counter 0; FORCE_START による active counter 1; Yellow robot 0 が 149.85703 mm 移動; source roles Duck / ER-Force / TIGERs; 3 比較 pair の status はすべて `Compared` (0); 最終 referee は ForceStart counter 1。
- 結果 JSON: `%TEMP%\issue102-dcp-20261101-44269e5d5fa8\acceptance-result.json`。受入 TRX: `%TEMP%\issue102-dcp-20261101-44269e5d5fa8\test-results\issue102-acceptance.trx`。同ディレクトリに AppHost log、acceptance console log、launch record、fixture manifest がある。
- #97〜#101: 既存の各 report と既存判定を維持。今回の commit/run はこれらを変更または再検証せず、完了扱いも変更しない。
- #102: DebugHost identity 接続と、active referee 維持・Yellow 100 mm・3 tracker 入力の同一 run acceptance を確認。
- #103: 同じ run の停止後、stack-owned container 0、global container 0、lock が `FileShare.None` で取得可能、AppHost / run DCP process 0、dashboard と mapped endpoint の listener 0 を確認。既存 issue #103 cleanup 条件に必要な同一 run の所有 stack は残っていません。

## 6. 決定・操作・影響

commit `963cb2a20776ff3ac59963f891f7e172c718d652` は `task/issue78-referee-state` へ通常 push 済みです。shutdown は Ctrl+C で実施し、stack/container cleanup を読み取りで確認しました。9 件残った MSBuild node-reuse process は同タスクの build が開始した `/nodemode:1 /nodeReuse:true` worker で、親 process は終了済みです。これらは AppHost、DCP、stack/container ではなく、既存 #103 cleanup 条件の追加 gate にはしませんでした。2 件の既存 untracked report は未変更です。GitHub `ls-remote` は後続の読み取り時に接続できませんでしたが、push 自体は成功応答を返し、ローカル branch は upstream と同期しています。
