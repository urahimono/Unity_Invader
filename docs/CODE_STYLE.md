# コード規約

本書は、このプロジェクトで確定したコード規約を共有するための文書です。規約は今後、合意した項目から段階的に追加します。設計原則・構造・設計判断は既存の PROJECT_PRINCIPLES・ARCHITECTURE・ADR で扱います。

## private / protected field の命名

private / protected の field は、先頭に `_` を付けた lowerCamelCase とします。

```csharp
private int _score;
protected float _speed;
private readonly InputAction _left;
[SerializeField]
private GameStateController _gameState;
```

private / protected の field は、`readonly`、`static`、`[SerializeField]` の有無にかかわらず対象です。

`const`、property、method、parameter、local variable は対象外です。`const` には今回新しい命名規約を追加せず、既存の扱いを変更しません。

## field に付与する属性の配置

field に付与する属性は、field 宣言と同じ行に記述しません。属性は1属性につき1行で記述し、すべての属性を書いた後の次の行に field 宣言を書きます。

NG:

```csharp
[SerializeField] private bool _developmentStartPlaying;

[SerializeField] [Tooltip("Temporary development start path; real game-start transitions are not implemented.")]
private bool _developmentStartPlaying;
```

OK:

```csharp
[SerializeField]
private bool _developmentStartPlaying;

[SerializeField]
[Tooltip("Temporary development start path; real game-start transitions are not implemented.")]
private bool _developmentStartPlaying;
```

属性が複数ある場合も1属性につき1行とし、field 宣言は属性と同じ行に置きません。属性の並び順について新しい規約は追加せず、既存の属性順序を維持します。

この規約は field に付与する属性の配置だけを対象とします。method / class / property など、field 以外の属性配置については今回新しい規約を追加しません。

標準の EditorConfig 設定だけではこの配置規約を正確に表現できないため、本書を正本とし、今回 `.editorconfig` は変更しません。

今回の規約追加では既存C#コードを修正しません。既存コードへの適用は、この規約を `develop` へマージした後、別のCodexセッション・後続PRで行います。

## 文の波括弧と本文の改行

`if`、`else`、`for`、`foreach`、`while`、`do`、`using` 文、`lock` は、本文が1文だけの場合でも波括弧を省略しません。本文は波括弧内へ改行して記述し、`if (condition) { DoA(); }` のような1行形式は使用しません。

NG:

```csharp
if (!FireReady) return;

if (condition)
{
    DoA();
}
else DoB();

for (int i = 0; i < count; i++) DoSomething(i);
foreach (var item in items) DoSomething(item);
while (condition) Advance();
do Advance(); while (condition);
using (var stream = Open()) Read(stream);
lock (_sync) UpdateState();
```

OK:

```csharp
if (!FireReady)
{
    return;
}

if (condition)
{
    DoA();
}
else
{
    DoB();
}

for (int i = 0; i < count; i++)
{
    DoSomething(i);
}

foreach (var item in items)
{
    DoSomething(item);
}

while (condition)
{
    Advance();
}

do
{
    Advance();
}
while (condition);

using (var stream = Open())
{
    Read(stream);
}

lock (_sync)
{
    UpdateState();
}
```

`else if` は次の一般的な形式を許可します。

```csharp
if (conditionA)
{
    DoA();
}
else if (conditionB)
{
    DoB();
}
else
{
    DoC();
}
```

`using System;` などの using directive は対象外です。using declaration も今回変更しません。

### EditorConfig の適用範囲と既存コード

`.editorconfig` の `[*.cs]` に `csharp_prefer_braces = true:warning` を設定し、Roslyn 標準の波括弧省略に関する診断（IDE0011）を利用します。`else if` の連鎖は許可されます。この設定は using directive や using declaration の変更を要求しません。

この設定だけでは本文の改行や1行形式の禁止を含む今回の規約全体を正確に表現できないため、本書を正本とします。今回決めていないスタイルまで変更する設定は追加しません。診断の表示は利用環境の Roslyn 対応に依存します。

今回の波括弧規約追加では既存C#コードを修正しません。既存コードへの適用は、この規約を `develop` へマージした後、別のCodexセッション・後続PRで行います。

## EditorConfig と既存コードへの適用

ルートの [.editorconfig](../.editorconfig) に、Roslyn の命名規則で表現できる範囲を設定します。`const` は field の規則に含まれないよう、専用の除外ルールで診断を無効にしています。利用するエディターの Roslyn 対応により診断の表示は異なります。

今回の規約追加では既存の違反 field を変更しません。既存コードへの適用は後続PRで別途行います。特に `[SerializeField]` の field rename は Unity の serialized data に影響する可能性があるため、その影響を確認する別作業として扱います。
