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
- MatchController の保存済み Game Controller continuation state は `NORMAL_START=BLOCKED`、理由 `Ball is 1.6 m away from center circle`。同時刻の simulator log は Crane のロボットコマンドを `vision_global_pos` と simulator 上のロボット位置の差が閾値を超えたとして破棄した記録を複数含む。よって失敗箇所は DCP の起動待ちではなく、試合開始前にシミュレーターと Vision/Crane の状態が一致せず、Game Controller が開始指令を許可しなかった段階と特定できる。
- ただし、このログだけでは中央配置の setup datagram がシミュレーターに届かなかったのか、届いたが適用されなかったのか、Vision/Crane が別状態を保持したのかは判別できない。setup 再送や Crane/planner/protocol の挙動変更は行っていない。
- 次の最小作業は、同一の通常1回 setup 送信について既存の受信応答・simulator ログ・最初の Vision 状態を時刻と run/container ID で対応づけること。受信/適用が証明できるまで実受入を再実行しても同じ兆候を反復するだけなので、新しい全体 run は起動していない。