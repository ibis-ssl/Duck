# Issue #52 現在の受入記録

## 対象

- Issue: [#52](https://github.com/ibis-ssl/Duck/issues/52)
- PR: [#119](https://github.com/ibis-ssl/Duck/pull/119)
- base: `task/issue50-aspire-006g`
- 記録時点の HEAD（当初記録）: `129a752040b22e7204d19e54d94f130fe9163410`
- 実行主体: `FA780\donabe`
- Docker/Linux 実受入: GitHub Actions `42434455957`（最新イメージ、Linux runner）

## 受入結果

run `42434455957` は全体として失敗し、PR は Draft のままです。CI、Linux publish、AppHost 起動、7 participant の readiness は成功しました。readiness 検査が `cm4-sim` を除外対象と誤認し、Vision capture と後続の独立 acceptance step を実行せず失敗しました。取得 artifact の match manifest では `cm4-sim` は必要な transport container として起動していました。

同 run では MatchController 自体が実パケットを処理しており、取得済み recorder archive は gzip stream 終端まで読み取り可能でした。既存 .NET verifier の結果は `RecordStreamComplete=true`、71,199 records、10 active referee windows です。各 window で Crane と TIGERs の移動台数はともに11台でした。この証跡は Issue #52 の中心条件を満たした実測ですが、workflow が全体成功になったことを意味しません。

11003 の referee source capture は完全で、4,915 datagrams、観測 source tuple は `172.18.0.2:45228` の1つでした。これは当該 run 内で観測した送信元の一意性を示します。

MatchController は `MaximumTimeReached` で失敗し、POST_GAME に達していません。終了時の Game Controller は正常後半の残り17秒でした。AppHost の旧既定期限2分が試合完了より先に尽きたことが、取得ログと結果 JSON から確認できます。

CM4 log に configuration datagram `WrongSize` が1件あります。一方で両チームの実移動は同 run 内で観測されています。この設定 packet の拒否が試合終了未達に影響したかは、現在の証跡だけでは判断できません。

## 今回の差分 `129a752`

- match 専用 MatchController の既定実行期限を2分から5分へ延長しました。実測で後半開始後、Game Controller の残り時間17秒の時点に2分期限へ到達したためです。Readiness timeout は変更していません。
- match acceptance checker の excluded-resource set から `cm4-sim` を除きました。現在の match 構成は CM4 を必要とし、同 run ではその実 container が起動していたためです。
- 2行の変更後、AppHost Release build は成功し0 errorsでした。ASPIRE010、NU1900、NU1902 の既存 warning は残りました。`git diff --check` も成功しました。
- exact-head CI run `42435642644` は `129a752` に対して成功しました。PR イベントのため Linux packet-flow と full-stack jobs は skip されました。修正後の full-stack run はまだ起動していません。

## 次の実受入 run `42436181286`

この run は exact source HEAD `749709a69171e410e1263157e8356daf46688749` で実行し、CM4 を除外する checker 誤りは再発しませんでした。CM4、Simulator、Crane、Duck を含む構成が起動し、7 participant readiness は完了しました。

開始 timeout 時に取得した MatchController continuation state は `NORMAL_FIRST_HALF_PRE` / `KICKOFF(YELLOW)` で、`NORMAL_START` は `BLOCKED` でした。初期の referee 状態は STOP です。Game Controller は `Ball is 0.3 m away from center circle` と返しました。開始 fixture は中心の停止ボールと22台の own-half robot teleport を一度送信しましたが、UDP send は適用確認ではありません。match-result は `stageReached=start`、`finalReadiness=null` で、POST_GAME には到達していません。recorder archive は完全終端、74,373 records、motion verifier は `Passed=false` / active windows 0 でした。11003 capture は完了し、4,664 datagrams の観測 source tuple は1個でしたが、この run は kickoff を開始できていません。

readiness checker は `finalReadiness=null` を処理できず、二次 `AttributeError` を出しました。今回の null-safe 修正は、これを明示的な readiness 不成立として失敗させます。欠落した readiness は成功扱いになりません。

初期 setup 後のボール再送案はレビューで却下しました。受け入れ済み設計 `Tracker/Design/Testing/aspire-simulation-test-environment.md` は初期配置1回・固定待機なしを指定し、既存の100ms readiness budget test も250msの固定待機で壊れるためです。再送案は戻しており、配置ずれの原因や再送の有効性は未確認です。根拠ある runtime 修正がないため、同じ全体受入は再起動していません。

`749709a` の exact-head CI `42435895494` と match workflow の .NET tests、ASPIRE-NET-002、Linux publish は成功しました。full-stack run `42436181286` は上記の初期 kickoff blocker で失敗しました。

期限延長と checker の CM4 修正は通常レビュー、独立レビューとも focused pass でした。初期 ball retry 案は独立レビューが P2 として design conflict / 100ms test regression を指摘したため、未コミットのまま全て戻しました。null-safe verifier 修正は独立レビューで fail-closed と確認されました。

## Planner とレビュー指摘

Match では `rvo2` の launch argument を省略し、対象 Crane の既定値を使います。明示的な non-default 設定は引き続き尊重します。CM4 の packet 経路選択は match mode に基づき、Planner 設定から独立しています。今回の差分は Planner に触れていません。run `42434455957` のログから Planner が POST_GAME 未達の原因だとする証拠も得られていません。

通常レビューの I52-NR-010 は、protobuf の同一 wrapper 内に複数の singular `detection` フィールドがある場合に、merge された内容から誤った移動合格を作り得るという指摘でした。再現 probe では修正前に false pass、`da493b8` 後に duplicate detection の rejection を確認しました。Focused test は16/16成功です。発生頻度を示す運用データはなく、「利用者1,000人あたり月1回以上」と言える根拠はありません。ただし #52 の合否判定そのものを誤認させ得るため、受入要件に直接関係する P2 として維持しました。

## 未完了

- HEAD `129a752` の exact-head CI 完了
- 5分期限と修正済み checker を含む最新 HEAD で、post-game/full-match outcome までの実受入
- CM4 の `WrongSize` configuration datagram の影響確認
- current HEAD のレビュー再確認と完了時の日本語 PR コメント
- PR #119 は Draft のまま。Issue #50 の親ブランチへの merge は行っていません。

## 補足

この turn では Python を実行していません。checker の変更は既存 Python verifier の除外 resource 集合から `cm4-sim` を削除する1行で、runtime・Crane の実装は変更していません。以前に誤って `CodexSandboxOffline` で実行した読み取りとテストは、実行主体制約に反するため有効な証拠として扱っていません。

## 最新の配置切り分け（HEAD `571c8de3c4fa6bcae8e5abf5c22c29ef0b0dd98f`）

既存の `42436181286` recorder archive を既存 .NET `SslLogRecordReader` で読み、配置指示後の Vision 座標を確認しました。新しい全体 run、再送、Crane の動作変更は行っていません。

- STOP 中の初期 Vision frame ではボール座標がほぼ `(0, 0)` mm でした。従って少なくとも初期観測では、中心へのボール配置が反映された状態を確認できます。
- 同じ初期 frame のロボット座標は fixture が意図した位置と一致しませんでした。設定 `blue_team_on_positive_half=false` の fixture は Blue #3 を `(-1.2, 0)` m、Yellow #3 を `(+1.2, 0)` m に置く想定でしたが、観測値は Blue #3 `(3400.64, -2799.32)` mm、Yellow #3 `(-3399.36, -2799.32)` mm でした。UDP の送信成功だけでは配置適用を証明できず、この記録だけでも「teleport が未適用」なのか「観測前後に移動した」のかは区別できません。
- `PrepareKickoffYellow` 中、Blue #3 は record 7331 でボールから約 `111.2` mm まで近づきました。record 7382 ではボールが中心から約 `505.0` mm、最終観測では約 `808.8` mm 離れていました。これは Blue #3 がボールに触れて動かした可能性を示しますが、衝突の直接記録ではなく原因の断定には使えません。
- Game Controller が開始を拒否した理由は `Ball is 0.3 m away from center circle` です。ボールが円外へ移動した後の拒否と整合します。初期ボール配置は観測上反映されていますが、ロボット配置の適用とボールが動いた物理的原因は未確定です。

一時的な .NET 診断分岐は記録を確認した後に除去済みです。診断コードの commit/push はありません。現在の差分はこの記録の追記だけで、アプリケーションソースの変更はありません。

PR #119 の現状は Draft、base `task/issue50-aspire-006g`、HEAD `571c8de` です。exact-head CI `42437862408` は成功（.NET tests、Linux publish を含む）ですが、PR event のため実 packet/full-stack acceptance は skip です。実受入 run `42436181286` は開始段階で失敗し、`POST_GAME`、active referee window での両チーム移動、full match outcome は未確認です。merge は行っていません。
