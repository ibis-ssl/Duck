# Issue #54: シミュレーションでのカード後ロボット許容数

## 対象

- Repository: `ibis-ssl/Duck`
- Issue / PR: #54 / #122
- Branch / base: `task/issue54-aspire-006g4` / `task/issue50-aspire-006g`
- 実装コミット: `ab54d140b81f37f75b866f83a84e004a41be2f34`
- 実装前 HEAD: `876b2eecb2cd75135dc82438a1ea19a6655ef4a5`

## 要件と変更範囲

シミュレーションでは退場したロボットをVisionから取り除くのが難しいため、Game Controllerがカード後に出場可能台数を減らしても、既に場に残る11台で試合を続行できるようにする。Crane、Tigers、planner、通信プロトコルの挙動は変更しない。

`Testing/Duck.Testing.AppHost/Fixtures/match/state-store-initial.json.stream` のseed stateで、`maxBotsPerTeam` とBlue/Yellow双方の `maxAllowedBots` を11から16に揃えた。MatchModeはこのstate-store seedからGame Controller状態を復元し、YAMLの初期state生成を通らないため、YAMLに既に設定されていた `DIV_A: 16` がseedの11を上書きしていなかった。16は既存YAMLおよびコメントのCI用カード余裕の意図に合わせた値である。

## 原因の証拠

修正前の手元MATCH `local-issue54-876b2ee-20261109` では、referee counter 0でYellow/ibisの `max_allowed_bots=11`、counter 29でYellowにイエローカード1枚が記録され、以降は `max_allowed_bots=10` になった。VisionにはYellowの11台が残り、Game Controllerは後半の再開を `Yellow team has too many robots` でブロックした。MatchControllerは `MaximumTimeReached` で終了し、`POST_GAME` 未達だった。

同runの最終Vision 15フレームにはYellow robot ID 0〜10の11台があり、ID重複はなかった。これは単純な余分な検出ではなく、カード後の許容数とシミュレーター上に残る台数の不一致と整合する。Game Controllerの初期state復元経路とカード時の許容数再計算をレビューで確認した。

## 検証

- AppHost build: 成功、0 errors。既存のASPIRE010とNU1902警告あり。
- ローカルDocker Desktop MATCH: `local-issue54-876b2ee-cardheadroom-20261109-retry1`、stack `565ba91db5344099918a8d80e4658b78`。全参加資源がready。
- カード後: Yellow/ibisのイエローカード1枚、`maxBotsPerTeam=16`、`maxAllowedBots=15`。Vision上の11台で試合を続行。
- 試合結果: `status=passed`、`stageReached=post_game`、`terminationKind=PostGame`、final command counter 50、final readiness complete。
- Referee capture: 5,716 datagrams、1 source、capture complete、truncated=false。
- SSL archive: 14,205,323 bytes、SHA-256 `CB2C3D6146F3779B8DDE78E33A7B923F2AFB473A4B697A78F7DB930ED32B6E81`。
- .NET motion/archive verifier: passed、record stream complete、74,327 records (referee 11,228 / vision 17,734)、両チームの移動があるactive window 11件。decompressed EOFとgzip footerを確認。
- `git diff --check`: 成功。
- 試合後、当該runが所有する9コンテナは全て停止。無関係なコンテナは操作していない。

記録と検証JSONはローカル `%TEMP%\duck-aspire-match-results\565ba91db5344099918a8d80e4658b78\` に保持している。AppHost起動ではDocker Desktop CLIの既存パスと `desktop-linux` のEngine identityを当該プロセスにだけ設定した。永続的な環境設定変更や依存追加は行っていない。

## レビューと未確認事項

- 通常レビュー: pass、必須指摘なし。実装差分と同一のfixture blobを対象とした。要求プロファイルは `gpt-6.1-sol / high`。実適用プロファイルのメタデータは観測できない。
- 独立最終レビュー: 未実施。
- 当該実装コミットに対応するPR CI: 未確認。CIはローカル実動作確認に代えるものではなく、push後のガードとして確認する。
- 1枚のカードによる上限低下を実測した。複数カード時の全パターンはこのrunでは検証していない。
- #54の試合終了と記録最終化は今回のrunで確認済み。親PR #118のmainマージは対象外。

## Skillと作業手順

- CodexSkill `main` (`8ee6e9f80a0362a4d066c4ea39fd3e87002fc829`) はcleanで `origin/main` と一致。`development-orchestrator` と必要な関連Skillを参照した。
- Duckリポジトリ直下に `AGENTS.md` はない。`.agents/skills` の確認済み内容はCodexSkill側と一致していた。
- この変更について新しいSkill gapは認められないため、Skillの変更は行わない。既存の開発・レビュー手順を継続する。
- 役割: 親はコンテキスト、権限、ライフサイクルと最終統合を管理。今回の指定実装担当は設定調査・実装・ローカル実行を担当し完了。通常レビュー担当は読み取り専用レビューを完了。独立最終レビューは未実施。

## 次の作業

独立最終レビューを実施し、問題がなければPR #122を更新して、実装・検証・未確認を日本語でコメントする。PRの最終状態と必要なCIを確認するまで、マージ完了とは扱わない。
