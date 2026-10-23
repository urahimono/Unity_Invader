# コード規約

本書は、このプロジェクトで確定したコード規約を共有するための文書です。規約は今後、合意した項目から段階的に追加します。設計原則・構造・設計判断は既存の PROJECT_PRINCIPLES・ARCHITECTURE・ADR で扱います。

## private / protected field の命名

private / protected の field は、先頭に `_` を付けた lowerCamelCase とします。

```csharp
private int _score;
protected float _speed;
private readonly InputAction _left;
[SerializeField] private GameStateController _gameState;
```

private / protected の field は、`readonly`、`static`、`[SerializeField]` の有無にかかわらず対象です。

`const`、property、method、parameter、local variable は対象外です。`const` には今回新しい命名規約を追加せず、既存の扱いを変更しません。

## EditorConfig と既存コードへの適用

ルートの [.editorconfig](../.editorconfig) に、Roslyn の命名規則で表現できる範囲を設定します。`const` は field の規則に含まれないよう、専用の除外ルールで診断を無効にしています。利用するエディターの Roslyn 対応により診断の表示は異なります。

今回の規約追加では既存の違反 field を変更しません。既存コードへの適用は後続PRで別途行います。特に `[SerializeField]` の field rename は Unity の serialized data に影響する可能性があるため、その影響を確認する別作業として扱います。
