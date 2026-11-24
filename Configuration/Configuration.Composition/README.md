# 設定合成ライブラリ

> **状態:** Duck #150 で確認を依頼する設計案です。ライブラリは未実装で、以下の呼出し例は実行できません。

共通設定の部品を合成し、本番の Duck 単体起動と開発用 AppHost が同じ設定を使うための独立ライブラリ案です。

## 最短の利用例

設計上は `IConfigurationComposer` を呼び、構成または診断を含む結果を受け取ります。

```csharp
CompositionResult result =
    await composer.ComposeAsync(request, cancellationToken);
```

詳しい契約と Duck／Aspire の利用例は[設定合成の設計書](../Design/configuration-composition-design.md)を参照してください。
