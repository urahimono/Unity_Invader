# コード規約

本書は、このプロジェクトで確定したコード規約を共有するための文書です。規約は今後、合意した項目から段階的に追加します。設計原則・構造・設計判断は既存の PROJECT_PRINCIPLES・ARCHITECTURE・ADR で扱います。

## カプセル化：内部管理オブジェクトの参照公開を禁止する

**管理・ゲームロジック・表示の責務を守るため、所有している可変オブジェクトの参照をGetter・property・メソッド戻り値で外部へ公開しないこと。** `get;` や `private set;` は、返された参照先の不変性を保証しません。

### 禁止する実装

- `GameplayDriver`などの管理クラスが、内部で所有する `GameplaySimulation` や他の可変管理オブジェクトを外部へそのまま返すこと。
- 他クラスが `_driver.Simulation.Formation.Invaders` のように、複数の内部オブジェクトを辿って状態を取得・変更したり、そのメソッドを呼んだりすること。チェーンをローカル変数や独自Getterに分割しても禁止です。
- `PlayerController` / `InvaderFormationController` のような表示クラスが、`GameplayDriver`またはゲームモデルを`[SerializeField]`・引数・Interface・検索などを通じて保持し、内部状態を取りに行くこと。**Interfaceに隠しても実体がGameplayDriverなら禁止**します。
- `IReadOnlyList<T>`や `IReadOnlyCollection<T>`で包んだだけの**可変参照型要素**を、「不変データ」と見なして外部公開すること。

NG（**現状コードに存在する、後続の修正対象となる構造**）:

```csharp
public GameplaySimulation Simulation { get; } = new GameplaySimulation();
public PlayerBody Player => _player.Player;
public PlayerShot Shot => _player.Shot;

// 別のComponent側から内部オブジェクトを辿る
var formation = _gameplayDriver.Simulation.Formation;
formation.TryKill(0);
```

### 原則とする実装

- 何かを**実行**させるときは、所有者が意味のある操作を公開し、その内部で処理を完結させます（Tell, Don't Ask）。
- 何かを**表示**するときは、所有者・進行担当が必要最小限の値、または可変オブジェクトの参照を含まない不変の表示用データを渡します。表示側は受け取った値をRenderer等へ反映し、ゲーム進行には関与しません。
- `GameplayDriver`をゲーム進行の唯一の実行入口とし、表示ControllerからDriverやSimulationへの依存を作りません。`GameplayDriver`が表示Controllerを保持し、必要な表示情報をPushします。画面更新の具体的な手段は既存のUnityライフサイクルと同期順序を守り、必要最小限に設計します。
- `int`、`bool`、`enum`などの値の取得や、不変の値データの公開は、責務・依存方向を壊さない範囲で可能です。**Getter一般やC# propertyそのものの禁止ではありません。**

OK（データを表示側へ渡す方向を示す概念例）:

```csharp
public readonly struct PlayerDisplayState
{
    public int  X       { get; }
    public int  Y       { get; }
    public bool Visible { get; }

    public PlayerDisplayState(int inX, int inY, bool inVisible)
    {
        X       = inX;
        Y       = inY;
        Visible = inVisible;
    }
}

// GameplayDriverが表示状態を渡す。PlayerControllerはDriverやSimulationを参照しない。
_playerController.ApplyDisplay(inPlayerDisplayState);
```

上記は**設計の例示**であり、`PlayerDisplayState`や `ApplyDisplay` は現時点の実装APIではありません。既存コードの実装をそのまま表すものではありません。

### GameplayDriverの公開APIと表示データの受け渡し

- `GameplayDriver`のランタイム向け公開Getter・状態遷移用publicメソッドは原則不要です。`CurrentState`・`Simulation`・`ChangeState`のような現行の公開APIは、後続の実装修正で必要性を見直し、内部処理に閉じます。
- 表示ControllerはDriver、Simulation、PlayerBody、Shot、Formation等の可変ゲームモデルの参照を**保持せず、取得もしません**。`GetComponent<GameplayDriver>()`や、Driverを実装元とするInterfaceの注入でも回避できません。
- `GameplayDriver`側が`PlayerController`と`InvaderFormationController`を保持し、進行後に表示専用の値データを`Apply`等のメソッドへPushする構造を基本とします。Controllerは描画処理と表示用Unityオブジェクトを管理するだけで、更新の主導権やモデルの操作権を持ちません。
- 変化するデータを継続して扱う必要がある場合も、値・不変スナップショットを優先します。Interface方式を採用するなら、参照先がDriverや可変モデルの実体になっていないか確認します。必要のないInterfaceや共通Managerは作りません。
- Unityの更新順序、初期表示、非表示・再有効化、Wave切替時の表示と55体のRenderer再利用を壊さない具体的な実装方法は、実装PRで検証します。ここで新しいランタイムAPIやクラスを作成済みとは扱いません。

### Editor検証用APIの分離

- Editor検証でState変更やモデルの観測が必要でも、**検証用という理由だけでランタイム向けの公開APIを追加・維持しません**。
- Editor専用の検証処理は`Assets/Editor/`などのEditor専用アセンブリに置きます。ランタイムアセンブリ側に検証専用の入口が必要なときだけ、`#if UNITY_EDITOR`で囲んだ最小限のAPIを検討します。製品ビルドに含めないことが条件です。
- Editor専用APIでも、`GameplaySimulation`など所有する可変オブジェクトの実体を返すGetterを安易に作りません。確認は可能な限り値・不変スナップショット、操作は用途を限定した検証メソッドで行います。
- `#if UNITY_EDITOR`はUnity EditorのPlay Mode中にも有効です。**Editor環境でのアクセス制御を保証する仕組みではありません**。通常のランタイム処理は検証用APIへ依存せず、表示ControllerへDriver参照を渡さないという原則を維持します。

概念例（既存APIではない）:

```csharp
#if UNITY_EDITOR
public void EditorSetState(GameState inNextState)
{
    ChangeState(inNextState);
}
#endif
```

Editor検証のために公開範囲を広げるのではなく、まず既存のVerificationが必要とする観測・操作を特定し、必要最小限の入口だけを設けます。

### レビュー時の確認

- 公開APIから可変な内部管理オブジェクトを取得できないか。
- メソッド内部のローカル変数経由を含め、他クラスの内部構造を辿るアクセスがないか。
- 表示専用クラスがゲームロジックの保持先・実装を知る必要がない構造になっているか。
- 受け渡すデータが可変の参照（配列・リストの可変要素を含む）を漏らしていないか。
- 既存テストの都合だけで実装側に可変オブジェクト公開を追加していないか。
- 表示ControllerがDriverを直接またはInterface越しに参照していないか。情報がDriver側から表示側へPushされているか。
- GameplayDriverのpublic APIは本当に通常のランタイムから必要とされるか。Editor検証用APIは製品ビルドから分離されているか。

現在の `GameplayDriver` / `GameplaySimulation` / 表示Controllerなどには、この規約に合わない参照公開が残っています。**この規約PRではルールのみを追加し、実装修正は後続PRとして別途実施します。** 実装済みと誤認しないようにしてください。

## private / protected field の命名

private / protected の field は、先頭に `_` を付けた lowerCamelCase とします。

```csharp
private int _score;
protected float _speed;
private readonly InputAction _left;
[SerializeField]
private SpriteRenderer _playerRenderer;
```

private / protected の field は、`readonly`、`static`、`[SerializeField]` の有無にかかわらず対象です。

`const`、property、method、parameter、local variable は対象外です。`const` には今回新しい命名規約を追加せず、既存の扱いを変更しません。

## parameter の役割を示す名前の prefix

parameter 名には、その役割を示す prefix を付けます。名前だけを見ても parameter・local variable・member field を判別できるようにするためです。

| parameter の種類 | 名前の形式 | C# の modifier の扱い |
| --- | --- | --- |
| 通常の入力 parameter | `inLowerCamelCase` | 命名のために modifier を追加しない |
| C# の `in` parameter | `inLowerCamelCase` | 既存の `in` を維持する |
| C# の `out` parameter | `outLowerCamelCase` | 既存の `out` を維持する |
| C# の `ref` parameter | `inOutLowerCamelCase` | 既存の `ref` を維持する |

通常の入力 parameter の例:

```csharp
public InvaderSlot(int inRow, int inColumn)
{
    Row    = inRow;
    Column = inColumn;
}
```

`out` parameter の例:

```csharp
private bool TryGetPosition(
    int inId,
    out int outX,
    out int outY)
{
    outX = 0;
    outY = 0;
    return true;
}
```

`ref` parameter は入力・出力の両方を担うため、`inOut` prefix を使います。

```csharp
private void UpdateValue(ref int inOutValue)
{
    inOutValue++;
}
```

ここでの `in` / `out` / `inOut` は parameter **名の prefix** です。入力 parameter を識別する目的だけで C# の `in` modifier を新たに追加してはいけません。既存の parameter modifier は意味を変更せず維持します。

### 他の変数との区別

- input parameter: `inLowerCamelCase`
- out parameter: `outLowerCamelCase`
- ref parameter: `inOutLowerCamelCase`
- local variable: `lowerCamelCase`
- private / protected field: `_lowerCamelCase`
- public property: `UpperCamelCase`

### 既存コードへの適用時の確認事項

parameter 名の変更は、named argument を使う呼び出し側に影響する可能性があります。既存コードへの適用時は named argument、constructor 呼び出し、method 呼び出し、override、interface implementation、public API を確認し、名前変更の影響と整合性を確認します。

これは parameter 名の規約であり、型、modifier、戻り値、method 名、overload 構成、処理内容を変更する規約ではありません。

今回の役割別 prefix を EditorConfig で正確かつ安全に表現できるか不明なため、今回 `.editorconfig` は変更しません。

今回の規約追加では既存C#コードを修正しません。既存コードへの適用は、この規約を `develop` へマージした後、別のCodexセッション・後続PRで行います。

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

## field / const 宣言のグループ内の列揃え

空行で区切られず連続している field / const 宣言をひとまとまりのグループとして扱い、宣言の各要素を縦に揃えます。揃える対象は、access modifier、`const` / `static` / `readonly` などの modifier、型、field 名、initializer がある場合の `=` です。要素がない宣言では、その欄を空けて後続の要素を揃えます。

```csharp
public  const int Rows            = 5;
public  const int Columns         = 11;
public  const int Spacing         = 16;
public  const int CellSize        = 16;
public  const int HorizontalStep  = 2;
public  const int Descent         = 8;
public  const int TicksPerStep    = 20;
public  const int MinimumX        = CellSize / 2;
public  const int MaximumX        = PlayerBody.ScreenWidth - CellSize / 2 - (Columns - 1) * Spacing;
private       int _ticksUntilStep = TicksPerStep;
```

### グループの境界

空行を境に別のグループとして扱います。列揃えはグループ内で行い、空行を越えて揃える必要はありません。file / class 全体で同じ列位置へ揃えることや、固定の列位置は要求しません。

```csharp
public const int Rows           = 5;
public const int Columns        = 11;
public const int HorizontalStep = 2;

private int _ticksUntilStep = TicksPerStep;
private int _direction      = 1;
```

この例では空行の前後は別グループなので、互いの列位置を合わせる必要はありません。

### 属性付き field

属性は既存の「field に付与する属性の配置」に従います。属性行そのものは列揃えの対象にせず、field 宣言行を同じグループ内の他の field 宣言と揃えます。属性行が間にあっても、空行で区切られていなければ同じグループです。

```csharp
[SerializeField]
private int _playerSpeed = 2;
[SerializeField]
private int _shotSpeed   = 4;
```

この規約を理由に、属性の順序・内容・配置規約を変更しません。

標準の EditorConfig 設定だけではこの列揃えを正確に表現するのは難しいため、本書を正本とし、今回 `.editorconfig` は変更しません。

今回の規約追加では既存C#コードを修正しません。既存コードへの適用は、この規約を `develop` へマージした後、別のCodexセッション・後続PRで行います。

## ローカル変数宣言・単純代入文の `=` の列揃え

同じブロック内で、空行を挟まず連続しているローカル変数宣言・単純代入文はひとまとまりのグループとして扱い、単純代入演算子 `=` の位置を縦に揃えます。

```csharp
public InvaderSlot(int inRow, int inColumn)
{
    Row    = inRow;
    Column = inColumn;
}
```

ローカル変数宣言も同様です。この規約の列揃え対象は `=` だけです。

```csharp
int row    = 0;
int column = 0;
```

### グループの境界

空行を境に別グループとして扱います。空行を越えて `=` の位置を揃える必要はありません。また、別のブロックや対象外の文をまたいで揃えることも要求しません。

```csharp
int row    = 0;
int column = 0;

int score = 100;
```

この例では `score` は別グループです。

### 対象外と既存規約との関係

今回の対象は単純代入演算子 `=` だけです。`==`、`!=`、`>=`、`<=`、`+=`、`-=`、`*=`、`/=`、その他の複合代入、ラムダ式、named argument、比較式など代入以外の用途は対象外です。この規約を理由に、式の意味や演算子そのものを変更しません。

field / const 宣言には既存の「field / const 宣言のグループ内の列揃え」を適用します。今回の規約は、その対象や内容を変更するものではありません。

標準の EditorConfig 設定だけではこの列揃えを正確に表現するのは難しいため、本書を正本とし、今回 `.editorconfig` は変更しません。

今回の規約追加では既存C#コードを修正しません。既存コードへの適用は、この規約を `develop` へマージした後、別のCodexセッション・後続PRで行います。

## property のグループ化と列揃え

property は、同じ property section 内で「同じ型 × 同じ宣言形式」をひとまとまりのグループとして扱います。同じグループに属する property は連続して配置します。

グループは property の型と宣言形式を組み合わせて分けます。たとえば、`int` + accessor block、`int` + expression-bodied、`IReadOnlyList<int>` + accessor block は、それぞれ別グループです。

同じ property section 内で、異なる property グループ同士は空行1行で区切ります。グループ判定は既存規約どおり「同じ型 × 同じ宣言形式」とし、同じグループ内には空行を入れません。

この空行規約は section 内のグループ間の区切りを定めるものであり、section 全体の前後に余計な空行を追加する規約ではありません。

accessor block を持つ property は、property 名の後ろへ空白を入れて、同じグループ内の `{` の開始位置を縦に揃えます。型の異なる property を無理に同じ列へ揃える必要はありません。

```csharp
public int X          { get; private set; } = 32;
public int Y          { get; private set; } = 208;
public int Direction  { get; private set; } = 1;
public int StepCount  { get; private set; }
public int AliveCount { get; private set; } = Rows * Columns;

public int AnimationFrame => StepCount % 2;

public IReadOnlyList<int> Invaders { get; }
```

### property section と並び替えの範囲

property section は、method、field、nested type、region 等の構造的な境界で区切られた property のまとまりです。同じ section 内で同じグループの property が離れている場合は、グループ化のために並び替えを許可します。section を越えた移動は要求せず、以下を守ります。

- method を越えて property を移動しません。
- field を越えて property を移動しません。
- nested type を越えて property を移動しません。
- region 等の構造的な境界を越えて移動しません。
- 属性やコメントと property の結び付きを壊しません。
- 同じグループ内の property の元の相対順序を維持します。
- property の型、名前、visibility、accessor、initializer、expression を変更しません。

### expression-bodied property と既存規約との関係

expression-bodied property は、同じ型でも accessor block を持つ property とは別グループとして扱います。たとえば `public int AnimationFrame => StepCount % 2;` は上の accessor block のグループへ混ぜません。expression そのものは変更しません。

field / const 宣言、ローカル変数宣言、単純代入文には、それぞれ既存の列揃え規約をそのまま使用します。今回の規約はそれらの対象や内容を変更するものではありません。

標準の EditorConfig 設定だけでは property のグルーピングや列揃えを正確に表現するのは難しいため、本書を正本とし、今回 `.editorconfig` は変更しません。

今回の規約追加では既存C#コードを修正しません。既存コードへの適用は、この規約を `develop` へマージした後、別のCodexセッション・後続PRで行います。

## 文の波括弧と本文の改行

`if`、`else`、`for`、`foreach`、`while`、`do`、`using` 文、`lock` は、本文が1文だけの場合でも波括弧を省略しません。本文は波括弧内へ改行して記述し、`if (condition) { DoA(); }` のような1行形式は使用しません。

NG:

```csharp
if (FireReady == false) return;

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
if (FireReady == false)
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

## if 条件式での bool 値の否定

`if` の条件式では、bool 値を単項論理否定演算子 `!` で反転する書き方を使用せず、`== false` で明示します。先頭の `!` を視覚的に見落としやすいためです。`else if` の条件式も対象です。

bool 変数の `!value`、bool を返す property の `!Object.Property`、method の `!Method()`、その他の bool 式に対する単項論理否定が対象です。複合条件式内の否定も同様に扱います。

NG:

```csharp
if (!playing)
{
    return;
}

if (!Object.Property)
{
    return;
}

if (!IsReady())
{
    return;
}

if (!_input.IsPressed())
{
    return;
}
```

OK:

```csharp
if (playing == false)
{
    return;
}

if (Object.Property == false)
{
    return;
}

if (IsReady() == false)
{
    return;
}

if (_input.IsPressed() == false)
{
    return;
}
```

対象は `if` の条件式だけです。`while` / `for` / `foreach` など他の構文や、if 条件式以外で使われる `!` へこの規約を拡張しません。null-forgiving operator の `!` も対象外です。

`!=` は禁止しません。`== null` と `!= null` も通常どおり使用できます。この規約を理由に null 判定の書き方を変更しません。

以下もOKです。

```csharp
if (_controller != null)
{
    _controller.Apply();
}

if (score != 0)
{
    UpdateScore();
}

if (_controller == null)
{
    return;
}
```

標準の EditorConfig 設定だけではこの規約を正確に表現するのは難しいため、本書を正本とし、今回 `.editorconfig` は変更しません。

今回の規約追加では既存C#コードを修正しません。既存コードへの適用は、この規約を `develop` へマージした後、別のCodexセッション・後続PRで行います。

## EditorConfig と既存コードへの適用

ルートの [.editorconfig](../.editorconfig) に、Roslyn の命名規則で表現できる範囲を設定します。`const` は field の規則に含まれないよう、専用の除外ルールで診断を無効にしています。利用するエディターの Roslyn 対応により診断の表示は異なります。

今回の規約追加では既存の違反 field を変更しません。既存コードへの適用は後続PRで別途行います。特に `[SerializeField]` の field rename は Unity の serialized data に影響する可能性があるため、その影響を確認する別作業として扱います。
