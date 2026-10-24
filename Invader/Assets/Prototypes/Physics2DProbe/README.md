# Physics2D衝突・撃破の独立試作（Issue #55）

本編を変更せず、GameObject・Prefab・Component・Physics2Dを学ぶための小規模試作です。namespace は `UnityInvader.Prototypes.Physics2DProbe`、Editor処理はその `.Editor` に限定します。本編のState・Score・Wave・独自判定・表示Push構造には接続しません。Physics2Dへの正式移行を決定するものではありません。

## 作成状況

この変更にはComponent、Editor生成メニュー、接触検証メニューを含めます。作業環境ではUnity Editorライセンスが利用できず、**Scene・Prefab・画像は未生成、実Physics2D・Play Mode・画面表示は未検証**です。以下はローカルのライセンス有効なUnityで実行する手順と期待結果です。

## Sceneの生成・開き方

1. プロジェクト `Invader/` をUnity **6000.6.4f1**で開き、スクリプトのコンパイル完了を待ちます。
2. Play Modeを停止し、メニュー **Unity Invader > Prototypes > Physics2D > Create or Open Probe Scene** を実行します。現在の未保存SceneにはUnityの保存確認が表示されます。
3. `Assets/Prototypes/Physics2DProbe/Generated/` に次をEditor APIで生成して開きます。`.unity` / `.prefab` のYAMLは手書きしていません。
   - `Physics2DProbe.unity`
   - `Physics2DProbeInvader.prefab` / `Physics2DProbeShot.prefab`
   - 単色正方形の `ProbeSquare.png`（生成画像。元ゲームの素材は不使用）
4. 生成後は同じメニュー、またはProjectウィンドウの `Physics2DProbe.unity` のダブルクリックで開けます。既存Sceneがあれば開くだけなので、Inspectorでの調整を上書きしません。

Build Settingsには追加せず、本編Scene・ProjectSettings・Packageは変更しません。生成物を共有する場合は、それぞれUnityが生成した `.meta` も一緒に管理してください。

## 操作と期待結果

横長のGameビュー（例：16:9、960×540以上）でPlayを開始します。緑がInvader、黄色が弾です。敵はFormationがPrefabから生成します。上部のUIはIMGUIで、入力方式・Packageの変更は不要です。

| 操作 | 初期状態 | 期待結果 |
| --- | --- | --- |
| `Fire LEFT` を1回押す | 左は1体、生存数1、通知0 | 下から弾が進み、1体が消え、生存数0・通知1。弾も消える |
| `Fire LEFT` を再度押す | 左は撃破済み | 生存数0・通知1のまま。弾は寿命で消える |
| `Reset both cases` → `Fire RIGHT` を1回押す | 右は隣接2体、生存数2、通知0 | 境界に向けた1発が両方へ接触し得る配置で、1体だけが消え、生存数1・通知1 |
| `Reset both cases` | 弾が飛行中でも可 | 弾を即時無効化し、個体を作り直して生存数1/2・通知0に戻す |

右側は中心間隔1、各Collider幅1、弾幅0.35、弾は中央から上向きなので、初期配置では左右へ等しく重なります。通知の順番・倒れる側は保証しません。同じ側ばかり倒れても優先順位が保証されたとは扱いません。複数回発射すれば残りの個体も倒せますが、**1発あたり最大1体**です。

Consoleには `PHYSICS2D_PROBE_DEFEAT` と生存数・通知回数が出ます。Play停止・再開でも初期化できます。

### Inspectorで確認する点

- Invader Prefab：`Physics2DProbeInvader`、`BoxCollider2D`（サイズ1×1、Triggerオフ）、`SpriteRenderer`。
- Shot Prefab：`Physics2DProbeShot`（Speed 4、Lifetime 3秒）、`Rigidbody2D`（Dynamic、Gravity Scale 0、回転固定、Discrete）、`BoxCollider2D`（0.35×0.6、Triggerオン）、`SpriteRenderer`。
- Formation：Invader Prefabと生成位置配列。左は1点、右はx=-0.5/+0.5の2点。Reset後に反映されます。
- Arena：両Formation、Shot Prefab、発射位置の参照。Prefabの値は次の生成・発射へ反映されます。Play中の変更は通常どおり停止時に失われます。
- 位置はUnityワールド座標、速度は単位/秒で、Transform/Rigidbody2Dが位置の正です。本編の224×256論理ピクセルとは対応付けていません。

この試作は小さな速度・十分な厚みのTriggerを使う離散検出です。速度・Collider・固定時間刻みを変更したときのすり抜けは別途確認が必要です。Continuousを選ぶだけでTriggerの高速移動を含むすべてを保証する試作ではありません。ProjectSettingsやグローバルPhysics2D設定は変更しません。

## 責務と重複防止

- **検出**：Invaderの `OnTriggerEnter2D` は弾Componentを識別してprivateの被弾処理へ渡します。
- **弾**：`TryConsume` が最初の呼び出しだけ成功し、フラグ設定→Rigidbody2Dのシミュレーション停止→GameObject非アクティブ化→遅延Destroyを行います。Destroyの完了やコールバック順序に頼りません。
- **個体**：撃破済みなら何もしません。弾の消費成功後、自身を撃破済みにし、Collider・表示をGameObjectごと無効化してFormationへ1回通知します。試作では撃破済み個体を再利用せず、Resetで作り直します。
- **Formation**：Prefab生成と個体の保持、生存集合からの通知元削除、通知回数更新を担当します。未登録・重複通知は集合削除の失敗として無視します。命中した個体の内部状態を操作しません。
- **Arena**：比較ケースの発射・リセットと数値のUI表示のみ。個体の選別や命中判定は行いません。内部管理オブジェクトを返すGetterはありません。

Unityは無効化されたMonoBehaviourにもTrigger通知を送る場合があるため、明示的な状態ガードを残しています。仕様参照：[OnTriggerEnter2D](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/ontriggerenter2d)、[Rigidbody2D.linearVelocity](https://docs.unity3d.com/ja/6000.0/ScriptReference/Rigidbody2D-linearVelocity.html)。

## 小さな接触検証

Scene生成後、Play Mode中に **Unity Invader > Prototypes > Physics2D > Verify Contacts (Play Mode)** を実行できます。別のローカルPhysics2D Sceneを作り、手動シミュレーションで次の3ケースを確認後に破棄します。本編Sceneやグローバル物理設定は操作しません。

- 1体へ下から移動して通常命中する。
- 1発を2体へ等しく重ねる。
- 2体の生成順序を逆にして同じ接触を行う。

各ケースで生存数が1だけ減ること、通知が1回であること、消費済み弾を再消費できないこと、追加stepでも結果が変わらないことを確認します。全成功時は `PHYSICS2D_PROBE_CONTACTS_PASS`、失敗時は例外が出ます。これは**用意した検証手段であり、この作業環境で実行済みではありません**。対象個体の優先順位は検証・保証しません。GUIからの発射・非表示・再発射・Resetは上記の目視手順で確認してください。

検証メニューは生成時のPrefab設定（速度・Collider寸法など）を前提とします。Inspectorで変更した場合は、その設定が検証の配置・step数にも適するか確認してください。

## 既存方式との比較

| 観点 | Physics2D試作 | 既存の独自矩形判定 |
| --- | --- | --- |
| 配置・学習 | GameObject/Prefab/ColliderをScene・Inspectorで調整・可視化できる | 論理座標とコード上の寸法から把握する必要がある |
| 責務 | 個体が被弾・撃破、Formationが集計。接触検出はエンジンへ委ねる | Simulation内の明示したtickと走査で検出・処理する |
| 同時命中 | 消費ガードで最大1体。選ばれる個体の優先順位は不定 | 下段→上段・左→右という走査順が明示されている |
| 再現・検証 | 固定物理step、形状、速度、通知順序の影響をEditorで確認する必要がある | Unity非依存の論理tickを使い、順序と境界を検証しやすい |
| 負荷 | 個体ごとのComponentや物理処理が増える。未計測 | 専用の配列・矩形処理。こちらも比較計測は未実施 |

長所・短所の整理であり、性能優劣や本編への採用は結論付けていません。本編の責務境界とADRは維持し、このPoCの結果を今後の判断材料にします。
