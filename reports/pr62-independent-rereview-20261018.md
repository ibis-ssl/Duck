# Duck PR #62 独立再レビュー（2026-10-18 JST）

## 対象と結論
- PR: https://github.com/ibis-ssl/Duck/pull/62
- 今回のレビュー対象HEAD: `2ce737c10870cfa54063435c63a146799dfb6689`
- 前回の対象HEAD: `70ece5452cf36a16b772e879f2b679f4e3d7746c`
- base: `design/issue18-aspire-test-orchestration` / `d766432e75c75976937b80eefd887392a23fcd45`
- 状態: OPEN / Draft / MERGEABLE、20ファイル +1,017 / -33
- 判定: **再レビュー指摘2件（P2 1件、P3 1件）。現HEADの静的な資源定義とLinux受入CIは確認できたが、起動完了判定に偽陽性の実装経路があるため修正を推奨。PRのマージは実施しない。**

## 前回指摘の解消確認
1. 親ブランチ競合: 解消。今回のHEADはbase `d766432e...` を祖先として含み、GitHubのmergeable判定もtrue。変更されたProgram.cs・csproj・AppHostApplicationModelTests.csにはbaseのDocker wrapper/NET-009と比較モード側の資源・fixture/テストが共存する。
2. 比較モード実行時受入: TIGERs/ER-Forceがowned Docker wrapperになり、各wrapperに所有権・container processとVision/tracker packetのreadiness検査が追加。DebugHostには3つの論理source role（Duck/TIGERs/ER-Force）をfreshnessおよびUUIDで判定するhealthが追加された。RuntimeHost/DebugHostのVision countとLinux比較モードfull-stack acceptanceが現HEADのCIで成功。以前の「ランタイム未実施」は解消。
3. 比較表示・再生の拡張はIssue #39（ASPIRE-006B以降）の未完了範囲を含む。現PR説明にもUUIDなしendpoint fallbackとlive/replay共通resolverの直接試験不足が明記されているので、Issue #39をこのPRで完了扱いしない。

## P2: 5秒Vision確認後にwrapper/DebugHost状態を再確認せずreadyを返す
対象: `scripts/wait_aspire_stack_readiness.py` 140–194行。特に162–177、179–194行。

実際のフローは、(a) AppHost生存・全wrapper/比較health endpointがHTTP 200、(b) 一度だけstack所有コンテナを照合、(c) Vision countのbefore採取、(d) `sleep(5)`、(e) RuntimeHost/DebugHostのVision count増加のみ検査、(f) 無条件に`ReadinessResult("ready",...)` を返す、となる。(d)～(f)でAppHost生存、全endpoint、所有コンテナを再確認しない。

例えばt=0で全endpoint=200、t=1でtracker-erforceが停止し、RuntimeHostとDebugHostだけはVision受信を継続してt=5で両countが増加する場合、本関数はtracker-erforceのreadinessが失われても`ready`を返せる。これは全資源が同時にreadyの時点にだけ起動成功を宣言する契約に対する偽陽性経路となる。またcountが最初の5秒だけ増えない場合、タイムアウトの残時間があっても`comparison_vision_count_stalled`ですぐ失敗するため、一時的な入力停止によるCI失敗を引き起こし得る。

修正案: count確認後に`is_alive`、wrapper/比較health endpoint、`verify_owned_stack`を再照合し、失敗時は設定されたdeadlineに従って再試行／正しい理由で終了する。モック時計と時刻で変わるprobeを使い「5秒の間に外部trackerが停止したがcountは増加した」ケースを追加テストする。

根拠: https://github.com/ibis-ssl/Duck/blob/2ce737c10870cfa54063435c63a146799dfb6689/scripts/wait_aspire_stack_readiness.py#L140-L194

## P3: comparison healthのVisionPacketsReceivedはVision受信数ではない
対象: `Tracker/Tracker.DebugHost/Program.cs` 115–140行。

`/health/comparison` は `ExternalTrackerSnapshotStore snapshots` に対して `VisionPacketsReceived = snapshots.GetSnapshot().Count` を返す。これはtrackerの現在のsource snapshotの件数であって、SSL-Vision packet受信数ではない。一方 `/health/vision` では `VisionPacketStore.GetSnapshot().PacketCount` を返している。前者の名称は診断時の数値を誤認させる。項目をtracker snapshot countと正しく命名するか、実際のVision counterを使うことを推奨。

根拠: https://github.com/ibis-ssl/Duck/blob/2ce737c10870cfa54063435c63a146799dfb6689/Tracker/Tracker.DebugHost/Program.cs#L115-L140

## 現HEAD一致CIとダウンロード済みartifactによる確認
- push run `39128661044` とPR run `39128663692` の`headSha`はいずれも`2ce737c10870cfa54063435c63a146799dfb6689`。
- 両runの`.NET tests/test`成功、`ASPIRE full-stack Linux acceptance`成功。`ASPIRE-NET-002`はworkflow_dispatchガードによりSKIPPED。CI結果の代用・別SHAからの流用はしていない。
- PR run artifact `aspire-full-stack-39128663692-1` をダウンロードし、`acceptance.stdout.log` で2/2テスト成功、`acceptance-result.json` でstatus=passed、Vision 43、tracker 18、referee 52 datagrams consumed、robot movement 140.72011 mmを確認。
- `wrapper-readiness.json` にはstatus=ready、owned_stack_valid=true、comparison mode、RuntimeHostのVision受信数7087→7393とDebugHost側の増加が記録されている。
- PRイベントのartifact `acceptance-result.json` の`sha=8940f78e...` はGitHubが作成したPR merge commit SHAであり、PRメタデータの`merge_commit_sha`と一致。runの`headSha`が上記current HEADと一致していることと矛盾しない。push runはHEAD本体で成功している。
- `.github/workflows/dotnet-test.yml` で失敗時TRX、stdout、stderr、vstest診断ログ、binlog、環境情報のartifactアップロード処理がある。NET-002/full-stackも専用artifact手順あり。

## 調査方法・限界
- 使用: Windows作業ワークスペースの`gh`/`git`/`rg`、現在のHEAD固定worktree、GitHub API、CI artifact直接確認、ソースコードとテストの静的監査。
- RDMCP接続が作業途中で切れ、RDCもオフラインになったため、独立のローカル回帰テストは未実行。P2/P3は現HEADのコード制御フロー／値の定義から導かれる指摘であり、動的再現済みとはしない。
- 元PRブランチにはコード変更、merge、PR状態変更を行っていない。
