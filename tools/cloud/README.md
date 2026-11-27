# Duck 開発環境の準備

関連: #155、PR #156

## SDK と検査設定

`toolchain.json` は Duck が管理する共通導入処理用の設定です。SDK の版と SHA-512 は Duck の設定値を使い、取得先 URL は共有側の規則から導きます。受理される項目は `schema_version`、`platform`、`dotnet_sdk.version`、`dotnet_sdk.sha512` です。

共有導入処理は `ssaattww/CodexSkill` の固定版 ID `124c52054c65058e1f240f4ae28ef6a45514e77f` を使います。`scripts/install-cloud-tools.sh` の内容 ID は `5c1b491567e8a45ea8f119682e5eb94198c74e18` です。PR #94 は独立確認と固定版 CI を経て既定の開発枝に取り込まれています。

## 初回準備の設定案

初回準備の前に、Duck の `package-lock.json` を扱える `npm` と `Node.js` を用意してください。ロックファイル内の `cspell` 9.8.0 は `Node.js` 20.18 以上を要求します。共通導入処理には `python3` が必要です。文書検査環境の準備では Python 3 の `venv` と `pip` も使います。これらが既存環境にない場合は必要な準備が未完了として報告し、ここでは自動導入しません。

`Codex Cloud` の初回準備では、上記の固定版を `/workspace/CodexSkill` に配置し、共通導入処理を先に呼びます。その後、Duck の準備処理を呼びます。準備処理には関連先の取得、`.NET` の復元と構築、文書検査環境の準備を置きます。

```bash
#!/usr/bin/env bash
set -euo pipefail

export CODEX_CLOUD_TOOLS_HOME=/workspace/.duck-env
cd /workspace/Duck

bash /workspace/CodexSkill/scripts/install-cloud-tools.sh \
  --repo-root /workspace/Duck \
  --config tools/cloud/toolchain.json

bash tools/cloud/prepare.sh
```

`tools/cloud/prepare.sh` は `Tracker.Tests` を個別に復元してから構築します。文書検査環境は Duck の `package-lock.json`、`tools/lint/requirements.txt`、`npm run lint:md:setup` を使います。`Node.js` 本体と `yomiyasu` は追加しません。全体テストは初回準備に含めません。

## 開始時の案内

この作業では子担当が実装を進め、親担当が指示とレビューを担います。

固定の案内先は `/workspace/Duck/tools/cloud/README.md` です。最初にここを読みます。`AGENTS.md` は存在する場合だけ読み、存在しないことを理由に停止しません。通常の作業開始では導入、復元、構築を繰り返しません。

```bash
cd /workspace/Duck
export CODEX_CLOUD_TOOLS_HOME=/workspace/.duck-env
export DOTNET_ROOT="$CODEX_CLOUD_TOOLS_HOME/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="$CODEX_CLOUD_TOOLS_HOME/dotnet-home"
export NUGET_PACKAGES="$CODEX_CLOUD_TOOLS_HOME/nuget-packages"
export NUGET_HTTP_CACHE_PATH="$CODEX_CLOUD_TOOLS_HOME/nuget-http-cache"
command -v dotnet
dotnet --version
```

通常シェルで見つかる実行ファイルは `/workspace/.duck-env/dotnet/dotnet`、版は `toolchain.json` の `dotnet_sdk.version` と一致する必要があります。一致しない場合は SDK 選択確認を失敗として報告し、通常開始時に共有導入処理を呼び直しません。開始時の設定は各シェルで行い、初回準備の値が後続のシェルに残るとは仮定しません。

文書検査は SDK 確認と分けます。検査には既存の `node`、`npm`、Duck 直下の `.venv` と指定済み Python 依存が必要です。どれかが欠ける、または検査が実行できない場合は文書検査を未確認と報告し、成功扱いにしません。通常開始時に依存導入を自動実行しません。

## 必要時の DebugHost 起動

必要な作業でのみ、次を手動で実行します。準備処理や開始時の案内からは起動しません。

```bash
bash tools/cloud/start-debughost.sh
```

この入口は旧環境と同じく HTTP を `127.0.0.1:5289` に限定し、映像の受信先を `127.0.0.1:12020` にします。UDP 再送信を無効にし、出力先を `/workspace/.duck-env/packet-captures` にします。

## 新しい作業シェルでの確認

新しい環境の復元が未実施なら、通常シェルでの SDK 選択確認は未実施のまま残します。確認する場合は上の開始時の案内を使い、Duck の最上位から `command -v dotnet` と `dotnet --version` の結果だけを期待値と比べます。文書依存の準備・検査は別に記録し、追加の全体構築は行いません。
