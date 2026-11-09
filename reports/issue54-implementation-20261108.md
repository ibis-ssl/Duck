# Issue #54 実装レポート（作業中）

## 対象

- Issue #54 / ASPIRE-006G4。base `task/issue50-aspire-006g`。
- 実装開始 HEAD: `c9f693e5d32da89e8a986609016bdd088f110dbd`。
- 受入条件と既存 artifact の確認: `reports/issue54-intake-20261108.md`。

## Dispatch profile

<!-- Parent-owned. Implementer must leave this section unchanged. -->

- selection inputs: task_kind=implementation; work_class=bounded_technical; uncertainty=medium; change_radius=cross_module; criticality=ordinary; repetition=single; context_need=bounded_history.
- selection source: `codex-delegation-executor` and `sub-agent-task-manager`.
- observed decomposability: sequential_dependencies; decomposition policy=allowed; no parallel split.
- requested profile: `gpt-6-luna` / `high` (existing task-family instruction).
- approval status/evidence: no gated profile proposed.
- agent role/default-role plan: default implementation role; explicit role override unavailable in spawn interface.
- role config evidence / profile effect: spawn tool exposes requested model and reasoning; final runtime application remains unobservable unless returned by runtime.
- planned runtime profile: `gpt-6-luna` / `high`.
- applied profile: pending; record exact application only when observable.
- application status: pending.
- runtime profile observability: pending.
- fork policy: full history.
- reasons / constraints: preserve Crane/planner/protocol behavior; no new Python implementation or local Python execution; no Docker run for implementation worker; do not mark MaximumTimeReached as passed; implementer inspects own complete diff and reports all findings/limitations.

## 実装目的

ASPIRE-MATCH-005 の試合終了・結果・SSL記録・Crane recording data、および全起動対象の標準出力／標準エラーを、失敗時にも同一 run artifact に保存する。既存 resource が teardown で消える前に証拠を回収する。

## 実装者の報告

<!-- Implementer fills the following sections only; preserve this heading and parent-owned profile. -->

### 変更したファイルと理由

- `.github/workflows/dotnet-test.yml`: AppHost/DCP がコンテナーを削除する前に、run 所有のリソースログ、試合結果ファイル、SSL recorder の最終化結果、Crane の rosbag を acceptance artifact に保存する。リソースごとに取得・エラー・欠落の状態を記録する。
- 手動 `workflow_dispatch` の match 実行では、`fullMatchPassed=true`、`stageReached=post_game`、`terminationKind=PostGame` に加えて、9 リソースすべてのライブログ取得、SSL archive の正常な最終化、Crane bag の最終化と archive 化、artifact の sanitizer 成功を要求する。sanitizer が失敗した場合は gate も失敗し、artifact upload は既存条件でスキップする。その他の失敗証拠を残すため、この判定は `always()` 条件の artifact upload より前に行う。
- 手動 `workflow_dispatch` の `mode=match` に限り、MatchController の最大時間を15分、workflow verifier の待機時間を20分とした。run 42488259296 は従来の5分上限に達した時点ですでに `NORMAL_SECOND_HALF` だったため、両ハーフの進行と結果保存の余裕を取った。既存の PR readiness 経路は従来どおりMatchController 5分、verifier 15分とする。最大時間到達は成功として扱わない。

### 実装上の判断

- コンテナー選択には既存の `duck.aspire.owner=duck-apphost`、現在の stack ID、実行前スナップショット、match run label による所有確認を使う。手動 match acceptance では、Crane/AppHost の停止前に、検証済みの新規所有コンテナーすべてで `timeout 300s docker logs --follow --tail=all --timestamps` を開始する。停止後に出力を既存 sanitizer で処理し、終了コード0のストリームだけを完全取得として gate に通す。Docker が返す標準出力と標準エラーの結合ログを `*.stdout-stderr.log` に保存する。300秒はハング時の上限であり、AppHost の停止待ちとコンテナーの終了処理を上回る余裕を設けた。タイムアウトは成功にせず gate を失敗させる。
- AppHost 停止後も残る所有コンテナーはログを再取得し、停止時の末尾を含む完全な sanitized log で事前スナップショットを置き換える。事前に捕捉した ID 一覧は維持し、cleanup で新たに見つかった所有 ID は重複を除いて追加する。各 ID について `post-shutdown-capture-status.tsv` に再取得済み、停止前に削除、残存したが再取得できず、または inventory error の状態を記録する。
- 発見した各コンテナーと、コンテナー未捕捉の各期待リソースに状態行を記録する。match の9リソースは simulator、Game Controller、`cm4-sim`、Crane、Duck、TIGERs、AutoRef、SSL recorder、MatchController である。参加リソースの readiness 集合は既存の7件のままとする。ログ名にはコンテナー ID を加えて衝突を避ける。participant status、match result、referee source capture は AppHost 停止の前後でコピーする。
- Crane へのシグナル送信は、実行前スナップショットが完全であり、候補 ID がそのスナップショットに含まれず、owner/stack/run label が現在の実行と一致する場合だけ許可する。cleanup では `ros2 bag record` に SIGINT を送り、最大30秒待って recorder process の終了を確認してから archive 化する。`/root` 以下、深さ8までの `metadata.yaml` から既存の出力先を探し、各 bag directory をコピーして `.tar.gz` にし、gzip CRC と SHA-256 を確認する。安全な inventory がない場合、停止・探索・コピー・archive 化に失敗した場合や container/bag がない場合は、その状態を明記する。
- Crane の起動コマンド、planner、packet protocol、Python 実装は変更していない。既存 artifact sanitizer を維持し、rosbag のバイナリデータは sanitizer がテキストとして書き換えないよう `.tar.gz` にまとめる。

### 自己 diff 点検


- workflow の完全な差分を確認した。過去の実行では DCP teardown 後にコンテナーが残らなかったため、停止前スナップショットは必要である。cleanup は事前 ID 一覧を切り詰めず、後から発見した ID を重複なく追加する。各 ID は再取得済み、すでに削除済み、残存するが所有確認や取得エラーで未再取得、または inventory error として報告される。full-match gate は手動 `mode=match` のみを対象とし、sanitizer 成功を含む全条件を確認してから成功を書き込む。gate が失敗しても artifact upload の `always()` 条件は維持され、sanitizer 失敗時のみ upload がスキップされる。
- Docker logs は標準出力と標準エラーの結合ストリームである。cleanup 前にリソースが削除済みの場合、inventory が不完全な場合、またはログ読み取りに失敗した場合は、その制限を status file に記録し、取得成功とは扱わない。
- Docker を実行していないため、`docker logs --follow` の実際の終了動作や Crane の出力先は未確認である。follower が異常終了または300秒でタイムアウトした場合は手動 gate が失敗する。Crane は `/root` 以下・深さ8までの `metadata.yaml` 探索を使うため、次の full-stack run で実際の path と archive 化を確認する必要がある。
- 独立レビューの verdict は出していない。

### 実行したコマンドと結果

- `git diff --check` — whitespace error なし。
- AppHost start、readiness、cleanup、手動 full-match の各 shell block を抽出し、`bash.exe -n` を実行 — すべて成功（61、176、622、45行）。shell 構文のみを確認しており、workflow 自体は実行していない。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~Tracker.Tests.MatchControllerWorkerTests` — 出力と test summary がなく、成功したテスト結果とはみなさない。
- `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -v:minimal` — `NETSDK1004` で失敗。`Tracker.Tests/obj/project.assets.json` がないためである。package restore は実施していない。
- Docker/full-stack run とローカル Python コマンドは未実施。この sanitization gate follow-up の作業開始 HEAD は `0b4e398c1664ae72e7daceaf7c8bdf49330fa9db`。本 follow-up は未コミット・未 push。

### 未実施・失敗・取得できなかった証拠

- full Docker acceptance run ができないため、現在の run における Crane rosbag path、Docker log collection の終了動作、SSL archive の最終化、`POST_GAME` gate は実環境で未検証である。復元済み assets がないため、ローカル .NET test も検証できていない。
- 既存 run 42488259296 は失敗の証拠として扱う。5分の harness 上限で `MaximumTimeReached` となり、`POST_GAME` 未達、全リソースの標準出力・標準エラー欠落、Crane rosbag 欠落である。SSL archive と readiness 時点の8個の startup-log tail だけでは full-match acceptance を満たさない。

### リスクと次の操作


- 既存 workflow を手動で `mode=match` 実行し、`POST_GAME`、`fullMatchPassed=true`、SSL archive、全所有リソースのログ、Crane rosbag archive を upload 済み artifact で確認する。取得欠落・失敗は明示し、acceptance 成功とは扱わない。
- 15分の harness 上限は過去の実行結果から設けた余裕であり、試合時間を保証するものではない。`POST_GAME` に到達しなければ gate を失敗させ、観測結果を失敗のまま残す。
- 親による統合・レビュー・commit・push・PR 操作が残っている。本 follow-up では commit、push、issue/PR 編集、merge を行っていない。


### 2026-11-08 手動 Linux full-match run 42528929969

- Workflow: https://github.com/ibis-ssl/Duck/actions/runs/42528929969 、HEAD `7db0d2b27803ef36d5ba09bba64b24092bb7a174`。CI と AppHost/DCP 起動は通過。全9リソースのログは `captured`、sanitizer と artifact upload も成功。match acceptance は `stageReached=start` の失敗で、Vision/motion の受入判定は成立していない。
- MatchController の初期 readiness は完了し、初期 Referee STOP を取得した。通常の配置送信は1回だけ行われ、送信先は simulator `172.18.0.5:10300`、時刻は `22:17:44.294Z`。5秒以内に SimulatorResponse は観測されなかった。これは未受信・未適用の証明ではない。
- MatchController の保存済み Game Controller continuation state は `NORMAL_START=BLOCKED`、理由 `Ball is 1.6 m away from center circle`。同時刻の simulator log は Crane のロボットコマンド11件を、`vision_global_pos` と simulator 内ロボット位置の差が閾値を超えたとして破棄したと記録する。これは試合開始前の状態不一致を示すが、ボール配置阻害の根因とは断定できない。
- ただし、このログだけでは中央配置の setup datagram がシミュレーターに届かなかったのか、届いたが適用されなかったのか、Vision/Crane が別状態を保持したのかは判別できない。setup 再送や Crane/planner/protocol の挙動変更は行っていない。
- 次の最小作業は、同一の通常1回 setup 送信について既存の受信応答・simulator ログ・最初の Vision 状態を時刻と run/container ID で対応づけること。受信/適用が証明できるまで実受入を再実行しても同じ兆候を反復するだけなので、新しい全体 run は起動していない。
#### 既存 run 比較

- 保存済み run [42434455957](https://github.com/ibis-ssl/Duck/actions/runs/42434455957) も同じ simulator image digest `9573c7…5554`、Crane digest `40435e…392b`、同一の `CreateInitialOwnHalfSetupCommand` payload を使用していた。そちらは initial setup 後に試合開始し、複数の Referee window で両チームの移動を記録したが、最後は5分上限で `MaximumTimeReached`、`POST_GAME` 未達だった。
- 旧 run にも同じ11台の Crane command drop が記録されている。したがって、この警告だけでは今回の `NORMAL_START` blocked の原因を説明できない。旧 run の結果は今回の HEAD の受入証拠には流用しない。
- 今回のログから確定できるのは Game Controller が記録したボール位置の阻害と、その後の開始 timeout まで。配置 datagram の到達/適用を直接示す記録はなく、原因の確定には至っていない。

### 2026-11-09 run 42532919016 / HEAD `f7b0724`

- PR CI [42532672465](https://github.com/ibis-ssl/Duck/actions/runs/42532672465) は .NET tests と Linux publish を含め成功。手動 full-stack run [42532919016](https://github.com/ibis-ssl/Duck/actions/runs/42532919016) は失敗し、受入完了ではない。
- 全リソース readiness と direct tracker Vision input capture は成功した。MATCH は `MaximumTimeReached` で終了し、`POST_GAME` に到達せず、`fullMatchPassed=false`。初期 setup の SimulatorResponse は5秒間観測されず、setup は `172.18.0.6:10300` 宛てに1回送信された。`no_response_observed` は未受信・未適用の証明ではない。試合は前回の `NORMAL_START` timeout とは異なり、最大時間まで進行した。最終 Referee counter は43。
- SSL recorder archive は停止後に最終化され、圧縮55,018,640 byte、展開336,929,645 byte。Referee 39,469、Vision detection 62,701、tracker 160,605 records を記録。AutoRef log は1件収集し、container 停止も確認。Crane bag 1件は停止・gzip CRC・SHA-256 検証を通過し、9 resource のログ sanitizer と artifact upload も成功した。
- archive finalize 時の `Duck.Testing.AppHost --verify-motion-archive` は runner 上で `Aborted (core dumped)` となり、`motion-acceptance.json` を生成できず archive verification gate が失敗した。保存された `match-result.json` の `datagramBytes` は SimulatorResponse 未観測時に JSON null であり、CLI の `ReadInt32` が Number 型確認なしに `JsonElement.TryGetInt32` を呼んで `InvalidOperationException` となることを同一 archive のローカル replay で再現した。これは run の abort と一致する原因。
- Number 型だけを整数として読む修正と null 回帰テストを追加。restore 後の AppHost build は成功し、対象 SSL reader tests は18件成功。失敗 run の同一 archive を replay すると `RecordStreamComplete=true`、motion `Passed=true`、272,953 recordsを読み、9つの有効 Referee windowすべてで両チームの移動を確認した。
- その replay で初期 setup は `172.18.0.6:10300` に送信され、SimulatorResponse は未観測だが、送信完了から約12.16ms後の最初の Vision frame #6003 にボールが記録されていた（x=5.14e-9m、y=1.44e-7m、confidence=1）。従ってこの run では初期配置がVisionへ反映され、中心位置のボールを検出したことが archive で確認できる。ACK不在は未適用を意味しない。
- この解析は後処理によるため、GitHub run 42532919016 時点の CI archive-finalization gate は失敗のまま。また match 自体は `MaximumTimeReached` / `POST_GAME` 未達であり、Issue #54 は未完了。
- 調査専任が過去記録を照合し、正常開始した run [42445437138](https://github.com/ibis-ssl/Duck/actions/runs/42445437138) では SimulatorResponse が観測されなくても、送信約51.9ms後に22台が指定位置と最大0.285mm差で一致したことを確認した。送信3.8ms後の最初の frame は旧配置だった。従って ACK 不在や最初の Vision frame だけを未適用原因とは扱わない。別の run 42528929969 では GC が選択した tracker UUID が AutoRef 停止後に切替わった記録がある。今回の原因と共通とは未確定。
- 次は null処理修正のPR CI・reviewを確認し、POST_GAME未達を別の残件として扱う。ローカル後処理では保存済みrunのmotion acceptanceと初期setup/vision相関を検証したが、run時点のarchive gateとmatch完了は失敗のまま。新たな試合run、setup再送、planner/Crane変更は行っていない。

## 2026-11-09 run 42532919016: counter 42 から終了まで

### 確認できた事実

- 保存済み MATCH controller log では、2026-11-08 23:08:16.855 UTC に Referee の `BALL_PLACEMENT_YELLOW`、counter 42、stage `NORMAL_SECOND_HALF`、`NextCommand=FORCE_START`、残り時間 17 秒を観測し、Simulator control port 10300 に ball-only placement を1回送信した。送信呼び出しは返ったが、fixture は `sent_unconfirmed` のままになった。
- 23:08:22.729 UTC に UDP と Game Controller API の両方で counter 43 / `STOP` / `NORMAL_SECOND_HALF` / `NextCommand=FORCE_START` を観測した。API の残り時間は引き続き17秒だった。
- SSL archive の counter 42 以後は Referee 状態が counter 42 と43に変化した後、その状態から `POST_GAME` へ進んでいない。最終 Referee は counter 43 / STOP / NORMAL_SECOND_HALF / NextCommand FORCE_START。run は約12分後に `MaximumTimeReached` で終了した。
- Vision は送信直後の ball を (-2.987, 0.950)m、約10秒後から最後までを (-2.494, 0.876)m と記録した。指定位置 (-2.500, 0.934)m との差は約5.9cmで、後者の観測位置はその後約12分間変わっていない。
- Crane log は 23:08:19 UTC に `ball_sensor_active=1` を記録した。これはボールが robot に保持・近接している可能性を示すログだが、GC が FORCE_START を待った原因と直接結び付ける証拠ではない。

### 原因判断と対応

- **原因判断（コードで確認）:** `RefereeDriverFixture.SelectNormalMatchContinueAction` は STOP 時に `NextCommand=FORCE_START` を処理せず、通常の終了操作 `END_GAME` は stage 残り時間が0以下になるまで選ばない。記録上の stage time は17秒のまま、Game Controller は STOP / FORCE_START に留まっていた。したがって現行 driver は FORCE_START を続行せず待ち続け、終了条件へ到達できない経路がある。
- **別仮説（未確定）:** Crane の ball sensor が placement 後の AutoRef 動作に影響した可能性はある。しかし保存ログだけでは、その関係や AutoRef が STOP を出した理由を証明できない。過去の開始前1.6m問題と同一原因とは扱わない。
- driver は STOP/HALT と API STOP の状態が一致し、Referee の NextCommand が FORCE_START の場合に限り、Game Controller が `FORCE_START=READY_MANUAL` かつ continuation issue なしと返したとき FORCE_START を要求するよう修正した。新しい回帰テストでは、残り時間が17秒でも同条件の action が一度送られ、新しい Referee FORCE_START で完了確認されることを確認した。
- SSL archive verifier は counter 42 以後の Referee 状態遷移と Vision ball の10秒間隔サンプルおよび最終サンプルを出力するようにした。これは既存 archive を解析する診断で、Crane/planner/Simulator の挙動変更ではない。

### 検証・未検証

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~SslLogRecordReaderTests|FullyQualifiedName~RefereeDriverFixtureTests"`: 34 passed。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore`: 1,358 passed、2 skipped、8 failed。失敗した8件は既存 `MatchLinuxBindPermissionsTests` が Windows 上で native Linux verifier 必須ケースを dynamic-skip し、それを xUnit が失敗として報告したもの。今回変更に関する失敗は確認されていない。
- `git diff --check`: passed。
- 保存済み run 42532919016 の完全 archive を変更後の .NET verifier で replay し、record stream complete、272,953 records、motion acceptance passed、counter 42/43 の状態と ball trace を取得した。これは過去runの replay であり、新しい HEAD の実試合受入ではない。
- 修正後の Linux full-stack MATCH、POST_GAME 到達、全完了ゲートは未検証。現時点ではコミット、push、レビュー、CIも未実施。

### 次の作業

- 差分をレビューできる形に確定し、通常レビューを受ける。修正 HEAD での Docker MATCH は実行前に必要なユーザー承認範囲を確認する。成功を確認するまでは #54 を未完了とする。

## 2026年11月9日 通常レビュー所見への対応

### 対応内容

- 通常レビューは HEAD `6487783bad2df5ca493efc1bf40cbde895ed2e70` に `fail / required changes` を返し、P2 の所見4件を記録した。指定プロフィールは `gpt-6.1-sol / high`、実際に適用されたプロフィールは親から観測できていない。
- `I54-NR-001`: STOP/FORCE_START の直前に得た Game Controller continuation read を、直近 API snapshot の `NextCommand` と `NextCommandForTeam` に照合する。UDP が ForceStart、API と直前 read がともに `FORCE_START / UNKNOWN` の場合だけ手動アクションへ進み、不一致・欠落なら待機する。
- `I54-NR-002`: Referee 指定位置は有限値だけを診断 snapshot に保存し、NaN と無限大は null にする。診断 JSON のシリアライズ失敗を防ぐ。
- `I54-NR-003`: counter42 以後に記録された Vision ball だけを counter42 後の trace に使う。以後の ball 観測がない場合、以前の ball を trace に混ぜない。
- `I54-NR-004`: counter42 後の Referee timeline と ball trace は各256件まで保持し、古い項目を落とした場合は `truncated` フラグを出す。archive reader 全体や motion 判定の保存量は変更しない。
- 各指摘の本番発生頻度を示す統計はなく、月1回以上とは断定しない。状態不一致の FORCE_START を送らないことと、今回必要な終了診断の時系列を正確に保つことは対象経路の正しさに関係する。保存量上限は新しい診断配列だけに適用する。Crane・planner・Simulator の動作は変更していない。

### 検証

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~RefereeDriverFixtureTests|FullyQualifiedName~SslLogRecordReaderTests" --verbosity quiet`: 41 passed、0 failed、0 skipped。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --verbosity quiet`: 1,365 passed、2 skipped、8 failed。8件は既存 `MatchLinuxBindPermissionsTests` が Windows で Linux native verifier を要求する環境依存ケース。変更対象テストの失敗はない。
- `git diff --check`: passed。
- 保存済み run `42532919016` は元の HEAD で `MaximumTimeReached`。修正 HEAD での MATCH、POST_GAME、成果物完了は未実施であり、この run を修正後の受入成功とは扱わない。

### 次の工程

- 同じ通常レビュー担当に新 HEAD で4件の修正確認を依頼する。PR と branch の更新後に、レビュー結果および exact-HEAD CI を確認する。
- 実試合の再実行は既存の許可境界を確認してから行う。POST_GAME と全ログ保存が確認できるまで #54 は未完了とする。
