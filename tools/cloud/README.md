# Duck クラウド環境の準備

関連: #155、PR #156

## ツール設定

`toolchain.json` は Duck が管理する共通インストーラー用の設定です。SDK の版と SHA-512 は従来の Duck 設定から引き継ぎ、取得先 URL は共有側の固定規則で組み立てます。`Node.js`、`yomiyasu`、文書検査依存の導入処理はここに含めません。

共有側 PR #94 の候補では、設定の形式は `schema_version`、`platform`、`dotnet_sdk.version`、`dotnet_sdk.sha512` に限られます。共有側の変更は未マージです。この文書の初回準備例は候補契約の確認用で、固定版と最終の呼び出し先がレビュー・公開されるまではクラウド設定に適用しません。

## 初回準備スクリプト案

共有側の固定版が利用可能になった後、`Codex Cloud` の初回準備スクリプトに設定する内容です。共有 SDK 導入の後に行う Git の `submodule` 取得、依存復元、`build`、文書検査環境の準備は Duck の手順として残します。

```bash
#!/usr/bin/env bash
set -euo pipefail

repo_root=/workspace/Duck
export CODEX_CLOUD_TOOLS_HOME=/workspace/.duck-env
cd "$repo_root"

bash /workspace/CodexSkill/scripts/install-cloud-tools.sh \
  --repo-root "$repo_root" \
  --config tools/cloud/toolchain.json

export DOTNET_ROOT="$CODEX_CLOUD_TOOLS_HOME/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="$CODEX_CLOUD_TOOLS_HOME/dotnet-home"
export NUGET_PACKAGES="$CODEX_CLOUD_TOOLS_HOME/nuget-packages"
export NUGET_HTTP_CACHE_PATH="$CODEX_CLOUD_TOOLS_HOME/nuget-http-cache"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
export npm_config_cache="$CODEX_CLOUD_TOOLS_HOME/npm-cache"
export PIP_CACHE_DIR="$CODEX_CLOUD_TOOLS_HOME/pip-cache"

git submodule update --init --recursive
dotnet restore Duck.slnx --disable-parallel
dotnet build Duck.slnx --no-restore -m:1 -p:BuildInParallel=false /nr:false
dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 -p:BuildInParallel=false /nr:false
npm run lint:md:setup
```

`npm run lint:md:setup` は Duck の `package-lock.json` と `tools/lint/requirements.txt` を使い、npm 依存とルート `.venv` を準備します。`Node.js` 自体は導入しません。全体テストと `DebugHost` の起動は行いません。

## 開始用スキル案

通常のタスク開始ではインストーラー、`restore`、`build`、依存導入を繰り返しません。開始用スキルには、環境変数の設定と Duck の共通指示を読む入口だけを置きます。

```bash
cd /workspace/Duck
export CODEX_CLOUD_TOOLS_HOME=/workspace/.duck-env
export DOTNET_ROOT="$CODEX_CLOUD_TOOLS_HOME/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="$CODEX_CLOUD_TOOLS_HOME/dotnet-home"
export NUGET_PACKAGES="$CODEX_CLOUD_TOOLS_HOME/nuget-packages"
export NUGET_HTTP_CACHE_PATH="$CODEX_CLOUD_TOOLS_HOME/nuget-http-cache"
source .venv/bin/activate
```

続けて `AGENTS.md` と選択済み CodexSkill の開発入口に従います。`DebugHost` は必要な作業でのみ起動します。

## 新規タスク開始後の確認

初回準備時の `export` が後続の通常タスクシェルへ引き継がれるとは仮定しません。新規タスクの復元後、Duck のルートから次を実行し、実際に見つかった `dotnet` の絶対パスが `$CODEX_CLOUD_TOOLS_HOME/dotnet/dotnet`、版が `toolchain.json` の `dotnet_sdk.version` と一致するかだけ確認します。

```bash
cd /workspace/Duck
command -v dotnet
dotnet --version
```

この確認は SDK の選択状態だけを検証します。文書依存の準備・検査状態とは別に記録します。新規タスクの復元がまだなら未検証として残し、追加の全体 `build` は行いません。
