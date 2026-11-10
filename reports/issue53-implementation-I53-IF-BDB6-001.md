# Issue #53 実装修正 follow-up: I53-IF-BDB6-001

## タスク

- 目的：独立レビュー finding I53-IF-BDB6-001 を最小限の変更で解消する。
- 開始 HEAD：bdb6be1e11ba8cd5224268b9e244c95ec4dae377。
- 対象：ibis-ssl/Duck #53 / PR #121、branch task/issue53-aspire-006g3、base task/issue50-aspire-006g。
- finding：AutoRef game-log JAR の存在検査が AppHost model 構築時に実行され、JAR 未生成の通常 checkout で MATCH model tests を止める。
## Dispatch profile（親管理）

- task_kind: review finding follow-up implementation, bounded technical, uncertainty medium, change radius local, criticality high, single, decomposability single.
- requested: gpt-6-luna, reasoning high, fork_turns=none。これはユーザー指定。
- decomposition: forbidden。nested agent を起動しない。
- role plan: runtime default、explicit role 指定なし。role effect unknown。
- planned/applied profile: runtime role effect unknown。最終適用 profile は観測不能なため null のまま記録する。
- 実装範囲: まず Program.cs、AppHost csproj、該当 test と workflow の JAR build step を読む。変更は必要最小限。新依存追加・環境設定変更なし。
## 範囲・目標

- 実 MATCH 起動時の JAR 不在エラーを維持しつつ、JAR がない状態で AppHost model を構成できるようにする。
- README の dotnet test 経路、workflow の JAR build 順、resource start 前の検査境界を確認した。
- ダミー JAR、新しい Java/JDK、Docker、push、PR本文・コメント変更、merge は実施しない。
- #54 POST_GAME、raw packet capture、Crane、planner、protocol の変更は対象外。

## 実装・変更・検証

- `Testing/Duck.Testing.AppHost/Program.cs`：model composition 中の `File.Exists` 失敗を除去。AutoRef container の environment callback に `AutoRefGameLogAgent.RequireAvailable` を登録し、resource environment を構成する起動前段階で明瞭な `FileNotFoundException` を維持。
- `Testing/Duck.Testing.AppHost/AutoRefGameLogAgent.cs`：JAR 必須チェックを小さな内部 helper に集約。
- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs`：JAR 不在の状態で MATCH model composition が成功すること、AutoRef resource の実 environment callback が起動前に明確な `FileNotFoundException` を返すこと、不在パスの例外情報を回帰テストで確認。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~MatchModelCanComposeWithoutAgentButMissingAgentFailsItsPreflight`：成功、1 passed / 0 failed / 0 skipped。Windows、.NET 10、HEAD は開始 HEAD。JAR は AppHost output に存在しなかった。Docker/JDK は使用せず。
- `git diff --check`：成功。
- ビルド時に既存の NU1902、ASPIRE010、警告が出たが、対象テストは成功。
- README 全体 gate は再実行していない（指示により禁止）。Docker、CI、実 MATCH launch は未実施。

## 残件・リスク

- callback 自体は Aspire の resource environment callback として登録済み。実際の Aspire/DCP 起動順・Docker 起動を使った動作確認は未実施だが、JAR check は resource start より前の environment 構成フェーズに置いた。回帰テストは不在状態で実際の AutoRef environment callback を直接呼び出して失敗境界を確認した。
- JAR がない実 MATCH 起動に対する helper の例外型、メッセージ、FileName は focused test で確認済み。
- 今回の変更で新依存は不要。Java/JDK がない環境では JAR 生成経路自体は検証していない。
- 最終 HEAD：未コミットの作業ツリー。commit、push、CI wait は未実施。
- 既存作業による `Tracker/Design/phases-status.md` の変更は本 follow-up で編集していない。
- 次の作業：通常レビューによる bounded fix verification、その後独立レビュー finding closure。