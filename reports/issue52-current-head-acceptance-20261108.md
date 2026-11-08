# Issue #52 現在の受入記録

## 対象

- Issue: [#52](https://github.com/ibis-ssl/Duck/issues/52)
- PR: [#119](https://github.com/ibis-ssl/Duck/pull/119)
- base: `task/issue50-aspire-006g`
- 記録時点の HEAD: `129a752040b22e7204d19e54d94f130fe9163410`
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
