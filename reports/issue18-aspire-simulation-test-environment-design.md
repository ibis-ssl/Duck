# Issue #18 Aspire シミュレーション試験環境 設計報告

## メタデータ

- リポジトリ: `ibis-ssl/Duck`
- Issue: `#18 Aspire対応`
- 関連 Issue: `#14 dockerでシミュレーターのケースを追加`
- PR: `#28 docs: design Aspire simulation test environment`
- ブランチ: `design/issue18-aspire-test-orchestration`
- ベース: `main`
- ベース SHA: `449296725fc69dc004818ede2e8ad59a52ef2d27`
- 設計変更 SHA: `63e3647667dc4bda06b2eb38e5abecad88042dc8`
- 実行環境: RDC 接続先 `FA780`
- 作成日時: `2026-09-21T17:45:53+09:00`

## 目的

テスト時に ER-Force シミュレータ、AI、Duck を個別に起動する手順をなくし、Aspire から一括で起動、停止、状態確認、ログ確認できる構成を設計する。

今回の作業は設計のみとし、AppHost、Dockerfile、AI 接続、製品コードは実装しない。
## 確定した構成

- `simulator`: ER-Force `simulator-cli` を実行する Docker コンテナ。
- `ai`: ロボット制御 AI を実行する Docker コンテナ。
- `duck`: 既存の `Tracker.RuntimeHost` をホスト上で動かす .NET プロジェクト資源。
- AppHost: `Testing/Duck.Testing.AppHost` に置く C# のプロジェクト型 AppHost。
- Duck の登録: `ProjectReference` と `AddProject<Projects.Tracker_RuntimeHost>` を使う。
- コンテナのネットワーク: 初期実装は Docker host network。
- Duck の入力: 既存の `sim` 設定と SSL-Vision `224.5.23.2:10020` を維持する。
- シミュレータ制御: UDP 10300。
- 青チーム制御: UDP 10301。
- 黄チーム制御: UDP 10302。

## ネットワーク設計の根拠

現行の ER-Force `simulator-cli` は通常時に SSL-Vision を `224.5.23.2:10020` へ送り、`--localhost` 指定時は `127.0.0.1:10020` を使う。任意の送信先アドレスを指定する起動引数は現行実装にない。

通常の Docker bridge network で `--localhost` を使うと送信先はコンテナ自身になる。bridge network とホスト間のマルチキャスト透過にも依存しないため、初期構成では simulator と AI を host network で実行する。

host network では固定 UDP ポートをホスト全体で共有するため、同一ホストでの同時起動は一組を基本とし、10020、10300、10301、10302 の競合を事前に検出する。
## Aspire の選定

現在の Duck は .NET 10 のソリューションであるため、C# のプロジェクト型 AppHost を選ぶ。

現行 Aspire の `AddDotnetProject` は試験的 API である。既存ソリューションに ProjectReference を追加して `AddProject<T>` を使う経路は公式のプロジェクト資源として提供されているため、初期実装はこちらを採用する。

コンテナへ Docker の高度な実行引数を渡す `WithContainerRuntimeArgs` が提供されているため、host network の指定は AppHost の資源定義へ置ける。

## 起動契約

開発者向けの代表コマンドは次とする。

```sh
aspire run --apphost Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj
```

Aspire ダッシュボードを三資源の起動状態、終了状態、標準出力、標準エラーの確認先とする。

起動順は simulator、duck、ai とする。ただし UDP に正常性確認先を追加していない段階では、単なるプロセス起動と通信可能状態を混同しない。
## AI に関する未確定入力

今回の依頼では AI の具体的なリポジトリ、イメージ名、起動コマンドは指定されていない。

Issue #18 には `crane(ros2)` の記載があるため最初の候補にはできるが、設計では `ai` 資源として抽象化し、イメージまたは Dockerfile、チーム色、シミュレータ接続先、追加引数を差し替え可能にした。

この情報は `ASPIRE-004` の実装開始前に確定が必要である。`ASPIRE-002` と `ASPIRE-003` の実装開始は阻害しない。

## 実装分割

- `ASPIRE-002`: AppHost の骨格。最初にアプリケーションモデルの失敗テストを追加する。
- `ASPIRE-003`: ER-Force simulator の Docker 化と host network 接続。
- `ASPIRE-004`: AI コンテナの具体化と simulator 接続。
- `ASPIRE-005`: 三資源の一括起動試験と正常経路の証跡。

Issue #14 の Tigers Tracker と ER-Force Tracker の比較起動は今回の初期構成へ混ぜず、後続拡張とした。

## テスト方針

実装時は TDD を使う。AppHost のアプリケーションモデルを検査するテストを先に失敗させ、その後 AppHost を実装する。
モデル検査では三資源の存在、資源種別、Tracker.RuntimeHost 参照、host network 設定、Duck の sim 設定、simulator の geometry と realism の明示を固定する。

Docker を必要とする一括起動試験はモデル検査から分離し、資源が Running になっただけでは成功としない。SSL-Vision を Duck が受信し、TrackerWrapperPacket を出力するところまでを正常経路とする。

## 診断 workflow の確認

`.github/workflows/dotnet-test.yml` は既に失敗時の診断 artifact を保存する。

確認できた内容は TRX テスト結果、標準出力、標準エラー、VSTest diagnostics、blame、MSBuild binary log、終了コード、dotnet 情報、git 状態、submodule 状態、ソースアーカイブである。

このため今回の設計作業では workflow 変更を行っていない。

将来 Aspire の Docker 一括起動試験を CI へ追加するときは、simulator と ai のコンテナログも資源別に artifact へ含める必要がある。

## 検証結果

- `git diff --cached --check`: 成功。
- 新規設計書に対する CSpell: 成功。1 ファイル、指摘 0。
- リポジトリ全体の `npm run lint:md`: 実行完了できず。

`npm run lint:md` は Windows の通常シェルでは `xargs` が存在せず停止した。Git for Windows の Bash で再実行すると `xargs` は利用できたが、現在の `main` に存在しない `.agents/skills/review-enforcer/scripts/list-markdown-targets.js` を package script が参照しているため停止した。

これは今回追加した文書の lint 指摘ではなく、現在の `main` の文書検査実行経路がこの checkout だけでは成立しないことによる阻害である。
RDC 接続先 `FA780` では `dotnet`、Docker、Aspire CLI が PATH 上に存在しなかったため、AppHost のビルドや Docker 起動試験は実施していない。今回の変更は設計文書だけであり、実装の動作確認を成功扱いにはしていない。

## 変更ファイル

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`: 新規の正本設計。
- `Tracker/Design/tasks-status.md`: `ASPIRE-001` の進捗と後続実装単位を追加。
- `reports/issue18-aspire-simulation-test-environment-design.md`: 本報告。
- `handoffs/task-aspire-001-design-20260921174553.yaml`: 次作業用の引継ぎ。

## 意図的に変更していない領域

- `Tracker.RuntimeHost` の製品コード。
- `Tracker.RuntimeHost/appsettings.json`。
- `Duck.slnx`。
- `.github/workflows/*`。
- Dockerfile と AppHost。
- AI のリポジトリやイメージ。

## CI

本報告を含む最終コミットを push した後、その PR の current HEAD SHA と workflow run の head SHA が一致する run だけを確認する。

本報告作成時点では最終コミット前のため、最終 HEAD の CI 結果は未確定である。最終 exact-HEAD CI 結果は PR コメントとチャット報告へ記録する。

## 残件とリスク

- `ASPIRE-004` の前に、実際に使う AI の Docker image または Dockerfile と起動コマンドを確定する必要がある。
- host network は固定 UDP ポートを共有するため、複数環境の並列起動には向かない。
- Docker Desktop を使う場合は host networking の有効化が必要であり、初期受入環境とは分けて検証する。
## 次作業

設計承認後、`ASPIRE-002` として AppHost のアプリケーションモデルを検査する失敗テストから実装を開始する。

## Merge 境界

本 worker は merge を行わない。PR #28 は利用者が確認して merge する。

## 外部参照

- Aspire project resources: https://aspire.dev/integrations/dotnet/project-resources/
- Aspire SDK: https://aspire.dev/get-started/aspire-sdk/
- Aspire container runtime args: https://aspire.dev/reference/api/csharp/aspire.hosting/containerresourcebuilderextensions/methods/
- Aspire run command: https://aspire.dev/reference/cli/commands/aspire-run/
- Docker host network driver: https://docs.docker.com/engine/network/drivers/host/
- ER-Force framework: https://github.com/robotics-erlangen/framework
- SSL simulation protocol: https://github.com/RoboCup-SSL/ssl-simulation-protocol