# コード規約

本書はプロジェクトのコード規約の正本です。設計原則・現在構造・判断理由は PROJECT_PRINCIPLES・ARCHITECTURE・ADR で扱います。

## カプセル化とComponent間の操作・通知

**所有する可変な管理オブジェクト・Component・ゲームモデル・コレクションを、Getter・property・メソッド戻り値で外部へ横流ししません。** `get;` や `private set;` は参照先の不変性を保証しません。可変要素を含む配列や `IReadOnlyList<T>` も同様です。

禁止例:

```csharp
public InvaderFormation Formation => _formation;
public PlayerShot Shot => _shot;

// 所有者の内部構造を辿って個体を操作する
_driver.Formation.Invaders[0].Defeat();
```

禁止される連鎖アクセスをローカル変数や独自Getter、Interfaceに分割しても回避にはなりません。必要な操作は所有者に用途の明確なメソッドで依頼します。

### Playingの責務

Issue #57 / ADR-0008に基づき、PlayingはComponent・Physics2D中心に構成します。

- GameplayDriverはState・Score・Wave進行を管理し、Player/Formationへ進行可否を伝えます。
- Playerは入力・移動・単一Shotへの発射依頼を担当します。
- PlayerShotは自身のOnTriggerEnter2Dで接触を検出し、IHitReceiverへ命中元・中心の移動区間・寸法・方向をHitInfoで渡し、受理されたら自身を消費します。具体的な対象型・得点は判定しません。移動区間の精密判定はProjectileHitDetectorとISweptHitReceiverで扱い、通常Triggerからは除外します（[ADR-0014](adr/0014-shield-hit-receiver.md)）。
- Invaderは自身の被弾・撃破・表示を担当し、Formationへ一度だけ通知します。
- Formationは個体の配置・集団移動・生存数を集計し、Driverへ得点・全滅を通知します。Driver/Formationへ独自AABB判定を持ち込みません。

Component間の参照保持は一律に禁止せず、次の依存方向に従います。

- 上位の管理者は、自分の管理する下位Componentを参照し、用途の明確な操作を依頼できます。SerializeFieldや初期化引数も、この方向で必要な参照を渡すために使えます。
- 下位Componentが、通知のために上位管理者を直接保持・検索したり、初期化引数等で注入されたりすることは禁止します。
- 下位から上位への結果通知はC#のevent/delegateを基本とし、所有・管理側が購読と解除を担当します。通知には必要最小限の事実を渡します。
- 具体的な命中対象へのTryReceiveHit等の操作は、上位管理者への結果通知と区別します。他者の内部状態の直接書き換えや、管理対象をGetterで辿る操作は認めません。
- 依存方向を守るためだけに、不必要なInterfaceやManagerを追加しません。

上記のPlayingの責務は現在構造の説明です。Formation等に残る上位への直接依存を推奨する規約ではありません。Issue #83/#85の適用範囲と現在構造は [ARCHITECTURE.md](ARCHITECTURE.md)・[ADR-0010](adr/0010-player-shot-lifecycle-and-events.md) を参照してください。

位置はTransform/Rigidbody2D、Invaderの生死はGameObject有効状態を正として、旧モデルや表示スナップショットに二重保持しません。PlayerShotの飛行状態はGameObject.activeSelf、進行可否はMonoBehaviour.enabledを正とし、待機・終了時はInactiveにします。物理・表示・速度はこの状態から導出し、独自boolで複製しません。Playerは入力受付時の同時1発制限に必要な射撃枠を所有し、発射成功・FlightEnded・Respawnで更新します。Formationの生存数など、責務に必要な集計値は保持できます。描画は個体Component自身で反映でき、Driver→表示専用ControllerへのPushを必須としません。表示を分離する場合は、不変の値をPushする方式も使えます。

`int`・`bool`・enum等の値の取得は、責務に必要な範囲で可能です。公開Getter一般を禁止するものではありません。ゲーム進行を外部へ任意に変更させるpublic APIや、将来用途だけのManager・Interface・イベントバスは追加しません。

### 自然な操作API・初期化・結果通知

必須参照は構成済みPrefabへ保存するか、実行が保証されるライフサイクルで一度だけ取得します。複数の操作からCacheComponents等を保険で呼びません。非アクティブPrefabのAwake遅延を利用者へ転嫁せず、Prefabの有効状態と待機・飛行の寿命を揃えます。

NG（利用者が内部の準備順を知る必要があり、弾が全体管理者を要求する）:

```csharp
var shot = Instantiate(shotPrefab);
shot.Initialize(driver, true);
shot.SetPlaying(true);
shot.ResetFlight();
shot.TryLaunch(position);
```

OK（Prefabの参照設定が完了していれば、通知先なしでも発射できる）:

```csharp
var shot = Instantiate(shotPrefab);
shot.TryLaunch(position);
```

停止・再開・強制中断は意味のある個別操作として残せますが、初回発射や再利用の準備として必須にしません。PlayerShotのSetPlaying(false)はゲーム進行による明示的な停止で、ResetFlightは通知なしの中断です。

下位Componentは管理者を保持・検索・引数注入しません。終了・命中結果は必要な事実だけをeventで通知し、所有側が生成・接続時に購読して破棄時に解除します。一時停止を購読解除の理由にせず、通知途中で状態が変わる場合の順序も維持します。

```csharp
// PlayerShot内。強制中断はこの通知を発生させない。
public event Action FlightEnded;

// 上位の所有・管理側で購読し、破棄時に同じdelegateを -= する。
shot.FlightEnded += driver.NotifyNormalPlayerShotEnded;
```

この例は結果イベントの購読を示すもので、PlayerがDriverを参照して接続する配置を推奨するものではありません。Issue #85で暫定接続を解消し、Driverが通常弾を明示的に参照して直接購読します。PlayerはPlayerInputStateの値だけを受け取り、被弾をHitAcceptedで通知します。Playerが直接操作するゲーム内ComponentはPlayerShotだけです。

IHitReceiver.TryReceiveHit(in HitInfo inHit)は命中元・矩形中心の移動区間・寸法・進行方向を読み取り専用値として渡します。UFOのDefeated通知は所属Formationが購読してDriverへ転送し、弾にUFO固有イベントを置きません。通知がFlightEndedより先に届くため、Driverは通常弾なら得点添字だけ終了予定分+1を反映します。実履歴はFlightEndedだけで更新し、B弾では進めません。友軍誤射の対象制限は保存済みPhysics2Dレイヤー設定で行い、弾同士の具体型依存を追加しません。

同時1発・入力の再押下条件はPlayer側へ置きます。弾は自身の飛行中の再発射を拒否しますが、複数インスタンスの利用は制限しません。Issue #83ではPlayerShot周辺に適用し、Issue #85ではPlayerの入力・被弾通知とEnemyShot/Formationからの最小Interface接続へ適用しました。FormationのDriver通知は今回変更しません。

### Editor検証用API

- 検証の都合だけでランタイム向け公開APIを増やしません。Editor処理は `Assets/Editor/` 等へ置き、必要な最小限の入口だけを `#if UNITY_EDITOR` に分離します。
- 観測は値またはEditor側のInspector確認を優先し、可変モデルの実体を返すGetterを追加しません。
- `UNITY_EDITOR` はEditorのPlay Modeでも有効です。通常のランタイム処理から検証APIへ依存しません。
- 衝突や遅延Destroyの通知順序に頼らず、同じ弾・個体の重複処理を防ぎます。

### レビュー時の確認

- 内部の可変オブジェクトやコレクションを返していないか。
- 他クラスの内部構造を辿る操作がないか。
- 検出、個体の反応、集団の集計、全体進行が担当ごとに分かれているか。
- 同じ位置・生死を独立モデルとComponentに二重保持していないか。
- 停止StateやDisable時にも入力・物理接触が進まないか。
- Editor用の操作・観測を通常ランタイムへ公開していないか。

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

## メンバ変数・property の宣言順序

クラスを開いたときに構成を把握できるよう、次の順序で宣言します。

1. `[SerializeField]` を持つ field
2. 通常のメンバ変数（`protected` → `private`）
3. property
4. 内部で使用する固定値（`readonly`、`const`）

分類が重複する場合はシリアライズ指定を優先します。`static` field も意味を変えず、該当する分類へ配置します。既存の他のアクセス修飾子をこの整理のために変更しません。

デバッグ・開発用 field は通常処理の field と混在させず、上記の通常メンバの後に独立したグループとして配置します。例えば `GameplayDriver._developmentStartPlaying` は開発用グループに置きますが、ランタイムでも使用するため削除や `#if UNITY_EDITOR` の追加はしません。

属性・Tooltip・コメントは対象 field と一緒に移動します。属性を1行ずつ宣言前に配置する規約と、field / property のグループ化・列揃え規約を維持し、グループ間には空行を設けます。今回の宣言順序の適用では、以前の property section の移動範囲に関する制限に優先して field や method を越えた配置整理を行えます。型定義・region 等の境界は機械的に越えません。

名前、アクセス修飾子、属性、initializer は変更しません。field や自動propertyの初期化に順序依存がある場合は、動作の維持を優先して必要な宣言の相対順序を保ちます。

## 関数の宣言順序と Unity イベント関数の簡潔化

関数は次の順序で配置します。

1. Unity イベント関数
2. 通常の関数（`public` → `protected` → `private`）
3. デバッグ・開発用関数
4. Editor 専用関数

既存の `internal` 関数はアクセス修飾子を変更せず、通常の関数の `public` の後へ配置します。同じアクセスレベルでは関連する処理をまとめます。

Unity イベント関数（`Awake`、`OnEnable`、`Start`、`Update`、`LateUpdate`、`OnDisable`、`OnDestroy`、`OnGUI` などの Unity Message）はアクセス修飾子に関係なく通常の関数より上にまとめ、初期化・更新・終了の流れが分かる順序にします。

Unity イベント関数はエンジンから呼ばれる入口として簡潔に保ちます。数行の単純な処理はそのままで構いません。複数の条件分岐、ループ、異なる責務の処理が混在する場合は、処理内容を表す名前の意味のある private メソッドへ切り出します。

```csharp
private void Update()
{
    UpdateGameplay();
}

private void LateUpdate()
{
    PublishDisplay();
}
```

イベント関数から「いつ、何をするクラスか」が分かることを目的とし、1行ごとの抽出や、行数だけを理由にした機械的な分割はしません。イベント間の処理順序、条件判定、return 位置、副作用、例外発生タイミングを維持します。

デバッグ・開発用関数は通常処理から分離し、Editor 専用関数は最後にまとめ、既存の `#if UNITY_EDITOR` を維持します。`Assets/Editor/` 内などクラス全体が Editor 専用の場合は、すべてを Editor 専用関数へ分類せず、そのクラス内で通常のアクセス修飾子順を適用します。コンストラクタや型定義には新しい宣言順序を追加しません。

今回追加した宣言順序とイベント簡潔化の規約は、既存C#コードにも適用します。標準の EditorConfig で正確に表現できないため、本書を正本とし、`.editorconfig` は変更しません。

## EditorConfig と既存コードへの適用

ルートの [.editorconfig](../.editorconfig) に、Roslyn の命名規則で表現できる範囲を設定します。`const` は field の規則に含まれないよう、専用の除外ルールで診断を無効にしています。利用するエディターの Roslyn 対応により診断の表示は異なります。

今回の規約追加では既存の違反 field を変更しません。既存コードへの適用は後続PRで別途行います。特に `[SerializeField]` の field rename は Unity の serialized data に影響する可能性があるため、その影響を確認する別作業として扱います。
