# 設定合成ライブラリ

> **状態:** Duck #150 で確認を依頼する設計案です。ライブラリは未実装で、以下の呼出し例は実行できません。

共通設定の部品を合成し、本番の Duck 単体起動と開発用 AppHost が同じ設定を使うための独立ライブラリ案です。

## 最短の利用例

共通の設定を読み込み、入口の部分設定で必要な値だけを変更する例です。

`parts/base.yaml`（共通設定）

```yaml
version: 1
instances:
  - name: worker
    kind: service
    settings:
      port: 8000
      enabled: true
```

`entry.yaml`（入口）

```yaml
version: 1
includes:
  - file: parts/base.yaml
instances:
  - name: worker
    kind: service
    settings:
      port: 8001
```

合成結果の `worker.settings` は次のとおりです。

```yaml
port: 8001
enabled: true
```

## 利用順

1. 共通設定と入口を用意する。
2. 入口の絶対パス、引数、上限、許可ディレクトリを合成要求に指定する。
3. 合成結果を取得し、呼出側の型と規則で検証する。

```csharp
CompositionResult result =
    await composer.ComposeAsync(request, cancellationToken);
```

成功時は `Configuration`、失敗時は `Diagnostics` を確認します。
詳しい[処理の流れ](../Design/configuration-composition-design.md#処理の流れ)と[構造・合成規則の例](../Design/configuration-composition-design.md#最小限の構造と合成規則)も参照してください。

詳しい契約は[設定合成の設計書](../Design/configuration-composition-design.md)、Duck／Aspire の利用例は[利用設計案](../Design/duck-configuration-composition-usage.md)を参照してください。
