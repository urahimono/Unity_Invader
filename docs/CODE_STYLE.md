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
- PlayerShotは自身のOnTriggerEnter2Dで接触を検出し、IHitReceiverへ命中元Componentを渡し、受理されたら自身を消費します。受理後に対象種別を読み取りますが、得点は計算しません。
- Invaderは自身の被弾・撃破・表示を担当し、Formationへ一度だけ通知します。
- Formationは個体の配置・集団移動・生存数を管理し、Driverへ全滅状態の値を提供します。弾の得点通知は所有者が担当し、Formationへデバッグ用の中継を持ち込みません。Driver/Formationへ独自AABB判定を持ち込みません。

今後の依存関係は、[設計原則の「責務境界に沿った依存関係」](PROJECT_PRINCIPLES.md#責務境界に沿った依存関係)に基づいて判断します。C#での適用では次を守ります。

- 管理者が責務上必要なComponentを直接参照する場合、SerializeFieldや初期化引数で参照を渡せます。ただし、参照を設定できることだけを理由に管理対象の内部Componentへ依存しません。
- 下位Componentが、通知のために上位管理者を直接保持・検索したり、初期化引数等で注入されたりすることは禁止します。
- 下位から上位への結果通知はC#のevent/delegateを基本とし、原則として直接管理する側が購読と解除を担当します。通知には必要最小限の事実を渡します。
- 具体的な命中対象へのTryReceiveHit等の操作は、上位管理者への結果通知と区別します。他者の内部状態の直接書き換えや、管理対象をGetterで辿る操作は認めません。

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

購読は生成・接続時に行い、破棄時に同じdelegateで解除します。一時停止を購読解除の理由にせず、通知途中で状態が変わる場合も必要な結果を取りこぼさないようにします。

```csharp
// PlayerShot内。強制中断はこの通知を発生させない。
public event Action<HitTargetType> FlightEnded;

// 直接管理するPlayerが購読し、終了履歴を更新して自身の結果を通知する。
_shot.FlightEnded += ResolveShot;
```

現在はPlayerが通常弾のFlightEndedを購読し、射撃枠解放と終了履歴更新後にShotResolvedを発行します。DriverはPlayerのHitAcceptedとShotResolvedを購読し、PlayerShotを直接参照しません。PlayerはPlayerInputStateの値を受け取り、直接操作するゲーム内ComponentはPlayerShotだけです。変更前の判断はADR-0011、現在の責務分担は [ADR-0014](adr/0014-player-shot-results-and-scoring.md) に記録します。

IHitReceiver.TryReceiveHit(Component inSource)の命中受付と、TargetTypeの読み取りを分けます。PlayerShotは受理後に種別を取得し、自然終了時に通知します。通常弾のUFO得点はPlayerの更新済み履歴からDriverが計算し、+1補正は行いません。B弾は所有者DebugCarpetBombがFlightEndedを生成時に一度購読し、Driverの共通NotifyShotResolvedへ結果を渡します。破棄時に解除し、再利用・回収では購読を増やしません。Player履歴は進めず、Driverや弾にB弾専用API・識別を持ち込みません。友軍接触の制限は既存Physics2Dレイヤー設定を維持します。

同時1発・入力の再押下条件はPlayer側へ置きます。弾は自身の飛行中の再発射を拒否しますが、複数インスタンスの利用は制限しません。Issue #83ではPlayerShot周辺に適用し、Issue #85ではPlayerの入力・被弾通知とEnemyShot/Formationからの最小Interface接続へ適用しました。現在はIssue #110でFormationのDriver直接依存を撤去し、ScoreはDriverが所有する既存propertyを読むFunc<int>で発射試行時に取得します。値の同期用コピーや専用Interfaceは追加しません。

### Editor検証用API

- 検証の都合だけでランタイム向け公開APIを増やしません。Editor処理は `Assets/Editor/` 等へ置き、必要な最小限の入口だけを `#if UNITY_EDITOR` に分離します。
- 観測は値またはEditor側のInspector確認を優先し、可変モデルの実体を返すGetterを追加しません。
- `UNITY_EDITOR` はEditorのPlay Modeでも有効です。通常のランタイム処理から検証APIへ依存しません。
- 衝突や遅延Destroyの通知順序に頼らず、同じ弾・個体の重複処理を防ぎます。

### レビュー時の確認

クラス図と対応するコードを照合し、既存の確認事項に加えて責務・管理関係に沿った依存かを確認します。

- 各クラスの依存先は、そのクラスの責務に必要な対象か。
- 管理対象の内部Componentを、管理者を飛び越えて参照・操作・購読していないか。
- 上位クラスが下位Componentの実装詳細・通知順序を知る必要が生じていないか。必要な順序には、確定タイミングと保証する担当の契約があるか。
- 所有者による操作API・結果通知で自然な依存関係にできないか。
- 抽象化やイベント中継の追加で、逆に実装や処理の追跡が複雑になっていないか。
- 例外的な直接依存に、責務上の理由があるか。
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

Unity イベント関数（MonoBehaviour Message）はアクセス修飾子に関係なく通常の関数より上にまとめ、以下のカテゴリ順・関数順で宣言します。同カテゴリ内でも、この固定順をアクセス修飾子より優先します。これはC#ソース中の宣言順序であり、Unityが関数を実行する順序を表すものではありません。

| カテゴリ順 | カテゴリ | カテゴリ内の関数順（左から右） |
| --- | --- | --- |
| 1 | 基本ライフサイクル | `Awake` → `OnEnable` → `OnDisable` → `OnDestroy` → `Start` → `Update` → `LateUpdate` → `FixedUpdate` |
| 2 | 物理・Trigger | `OnCollisionEnter2D` → `OnCollisionEnter` → `OnCollisionStay2D` → `OnCollisionStay` → `OnCollisionExit2D` → `OnCollisionExit` → `OnTriggerEnter2D` → `OnTriggerEnter` → `OnTriggerStay2D` → `OnTriggerStay` → `OnTriggerExit2D` → `OnTriggerExit` → `OnJointBreak2D` → `OnJointBreak` → `OnControllerColliderHit` |
| 3 | アプリケーション | `OnApplicationFocus` → `OnApplicationPause` → `OnApplicationQuit` |
| 4 | 描画・カメラ | `OnBecameVisible` → `OnBecameInvisible` → `OnWillRenderObject` → `OnPreCull` → `OnPreRender` → `OnRenderObject` → `OnPostRender` → `OnRenderImage` → `OnGUI` |
| 5 | Animator | `OnAnimatorMove` → `OnAnimatorIK` |
| 6 | マウス | `OnMouseEnter` → `OnMouseOver` → `OnMouseExit` → `OnMouseDown` → `OnMouseDrag` → `OnMouseUp` → `OnMouseUpAsButton` |
| 7 | Editor・Inspector・Gizmo | `Reset` → `OnValidate` → `OnDrawGizmos` → `OnDrawGizmosSelected` |
| 8 | その他（Transform → UI → Audio → Particle） | `OnTransformParentChanged` → `OnTransformChildrenChanged` → `OnChildRectTransformDimensionsChange` → `OnAudioFilterRead` → `OnParticleCollision` → `OnParticleTrigger` → `OnParticleSystemStopped` → `OnParticleUpdateJobScheduled` |

存在しない関数は飛ばして相対順を保ちます。特に `OnDisable` / `OnDestroy` は `Start` より上、`FixedUpdate` は `LateUpdate` より下に置きます。今後追加するUnityイベント関数も、必ずこの固定順の該当位置へ挿入します。順序を揃えるためだけに関数を新設・削除しません。

一覧にないUnity Messageは、該当カテゴリの類似機能の末尾に配置します。順序が不明な場合は規約決定の対象として報告します。UnityEventクラスのdelegate、通常のイベントハンドラー、Unity Messageではない同名関数、StateMachineBehaviour固有のメッセージは本規約の対象外です。

宣言位置を変更するときは、関数に付随する属性・コメント・プリプロセッサガードも一緒に移し、`#if UNITY_EDITOR` / `#if UNITY_EDITOR || DEVELOPMENT_BUILD` 等の適用範囲を維持します。関数本体・アクセス修飾子・名前・属性・動作・購読内容は変更せず、通常関数同士の順序も維持します。

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

## ログ・Assert・例外と異常時の継続可否

ゲーム本体では、異常の検出・診断と、初期化の中断・ゲーム進行の停止を分けて設計します。ログを出しただけで修復・停止したと見なさず、異常の重大さと安全に続行できる範囲を明確にします。

### Assert・ログの使い分け

ゲーム本体のアサートは `UnityEngine.Debug.Assert(condition, message, context)` を標準とします。以下の `Debug` は `UnityEngine.Debug` を指します。例外を投げる挙動がある `UnityEngine.Assertions.Assert.IsTrue` / `IsNotNull` 等は、ランタイムの標準方式にしません。

| 状況 | 標準 | 失敗後の動作 |
| --- | --- | --- |
| プログラマーの想定では必ず成立する不変条件・内部矛盾を開発時に検出したい | `Debug.Assert(condition, message, context)` | 条件違反をUnity Consoleに示す。これだけで処理を止めたつもりにならない |
| 必須参照・設定不足など、ゲーム進行が成立しない不備 | 開発時の前提違反を `Debug.Assert` で検出してよい。製品ビルドでも記録が必要なら `Debug.LogError(message, context)` | 安全な初期化失敗・進行停止を必ず明示し、不完全な状態で続行しない |
| 機能欠落・不正値など、続行はできるが本来の挙動から外れる問題 | `Debug.LogError(message, context)` | 影響する処理をスキップするか安全な代替へ切り替え、ゲーム全体は必要に応じて継続する |
| 想定済み・許容できる不備や、安全なフォールバックの通知 | `Debug.LogWarning(message, context)` | 安全に続行する |
| 通常情報・一時的な調査 | `Debug.Log(message, context)` | 過剰な毎フレームログを避ける |

`Debug.Assert` は `UNITY_ASSERTIONS` に依存し、通常の製品ビルドでは呼び出しが省略され得ます。条件式や引数に、状態更新・初期化など実行が必要な副作用を含めません。`Debug.Assert(condition);` の後でそのままnull参照を使用するなど、アサートを例外や実行時ガードの代替にしてはいけません。

安全に続行できない失敗では、アサートの有無にかかわらず、`return`、`enabled = false`、安全なState遷移、呼び出し元への失敗返却等で実際に中断・停止します。停止方法は担当クラスの責務とUnityライフサイクルに合わせます。`enabled = false` だけで他オブジェクトからの通常メソッド呼び出しまで止まると考えず、初期化APIの成否・失敗後の状態・呼び出し元が続行しないことも確認します。

製品ビルドで原因を追う必要のあるエラーは `Debug.LogError` 等の実行時ログを残します。同じ失敗で `Debug.Assert` と `Debug.LogError` を機械的に二重出力せず、出力条件・担当・目的を明確にします。

ログには原因・対象・必要な前提が分かるメッセージを付け、可能なら `UnityEngine.Object` の `context` を添付します。状態に依存する同じ異常を毎フレーム繰り返し出力しません。

### throw の扱い

- `Assets/Scripts/` 等のゲーム本体の通常実行経路では、設定不備・引数不正・ゲーム状態異常を通知するための明示的な `throw new ...` を原則禁止します。想定外状態の検出には上記のAssert・ログと明示的な失敗処理を使います。
- 外部ライブラリやUnity内部から伝播する例外を隠蔽する規約ではありません。例外をすべて握りつぶす空の `catch` は設けません。
- Editor拡張、データ変換ツール、開発用CLI、自動検証・テスト等のゲームプレイ外の処理では、明確な失敗通知のための例外を許容します。Editor検証・テストの失敗を `Debug.Assert` のログだけへ置き換えず、バッチ検証で失敗を確実に検出できる方式を維持します。
- 既存の `throw` を機械的にAssert・ログへ置き換えません。適用時は初期化APIの成否と失敗後の状態、呼び出し元が続行しないことを個別にレビューします。

### コード例

以下は使い分けを示す小さな例です。実際の停止範囲は、そのクラスの責務に合わせて決めます。

不変条件の検証では `Debug.Assert` を使います。この例では、アサートが省略されても範囲外アクセスを行わないよう、処理の中断を別途記述しています。

```csharp
private void AdvanceInvader(int inIndex)
{
    bool validIndex = inIndex >= 0 && inIndex < _invaders.Count;
    Debug.Assert(validIndex, "Invader index must be within the formation.", this);
    if (validIndex == false)
    {
        return;
    }
    _invaders[inIndex].AdvanceAnimation();
}
```

必須のScene/Prefab参照がない場合は、製品ビルドでも原因を記録し、初期化を中断します。この例の `false` を受け取る呼び出し元も初期化失敗として扱い、ゲーム進行を開始しません。

```csharp
private bool TryInitializeGameplay()
{
    if (_formation == null)
    {
        Debug.LogError("Gameplay requires a Formation reference in the Scene/Prefab.", this);
        enabled = false;
        return false;
    }
    return true;
}
```

本来あるべき演出機能が欠落していてもゲーム進行を継続できる場合は、`Debug.LogError` を出し、当該処理だけを中断します。この例は演出要求時に呼ぶもので、毎フレーム呼び出して同じエラーを連射しません。欠落が仕様上想定済み・許容済みなら、通知の必要性を判断したうえで `Debug.LogWarning` を使います。

```csharp
private void PlayDefeatEffect()
{
    if (_defeatEffect == null)
    {
        Debug.LogError("Defeat effect is missing; skip the effect and continue gameplay.", this);
        return;
    }
    _defeatEffect.Play();
}
```

本節は今後の実装・レビューの基準です。今回の規約追加では既存ランタイム実装・Editor検証・テストを変更しません。既存の `throw` の整理は、呼び出し元や状態遷移も含めて後続Issue・別PRで行います。標準のEditorConfigで正確に表現できないため、本書を正本とし、`.editorconfig` は変更しません。

## EditorConfig と既存コードへの適用

ルートの [.editorconfig](../.editorconfig) に、Roslyn の命名規則で表現できる範囲を設定します。`const` は field の規則に含まれないよう、専用の除外ルールで診断を無効にしています。利用するエディターの Roslyn 対応により診断の表示は異なります。

今回の規約追加では既存の違反 field を変更しません。既存コードへの適用は後続PRで別途行います。特に `[SerializeField]` の field rename は Unity の serialized data に影響する可能性があるため、その影響を確認する別作業として扱います。
