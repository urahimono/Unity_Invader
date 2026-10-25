# Architecture

現在のコード構造の正本です。判断基準は [PROJECT_PRINCIPLES](PROJECT_PRINCIPLES.md)、変更理由は [ADR-0008](adr/0008-component-gameplay.md) を参照します。

## 移行状態と起動

Issue #57でPlayingのコードをComponent/Physics2Dへ移行しました。**作業環境ではUnityライセンスが利用できず、追跡中のArcade Sceneへ移行は未適用、Prefabは未生成、Play Modeは未検証です。** Scene YAMLは編集していません。旧SceneをそのままPlayしても新構造の接続は完成していません。

ローカルUnityで `Unity Invader > Migrate Arcade to Components` を実行してArcadeとPrefabを保存し、接続確認後にBootstrapから起動します。詳細は [README](../README.md) を参照してください。

| Scene | 役割 |
| --- | --- |
| Bootstrap | StartでArcadeをSingleロード。今回変更なし |
| Arcade | Driver、Player、単一Shot、Formation、カメラ。Editorメニューで新構造へ移行する |
| SampleScene | 既存サンプル。今回変更なし |

1P専用・クレジットなしです。待機デモ・開始/リトライ・敵弾・Player被弾/残機・Shield・UFO・HighScore保存は未実装です。

## Playingの責務

```mermaid
flowchart TD
    Input[Input System] --> Reader[PlayerInputReader]
    Reader --> Player[Player: 入力・移動・射撃受付]
    Driver[GameplayDriver: State / Score / Wave] -->|SetPlaying| Player
    Driver -->|SetPlaying / BeginWave| Formation[InvaderFormation: 集団移動・集計]
    Player -->|TryLaunch / SetPlaying| Shot[PlayerShot: Rigidbody2D / Trigger / 消費]
    Physics[Unity Physics2D] -->|OnTriggerEnter2D| Shot
    Shot -->|TryReceiveHit| Invader[Invader: 個体の被弾・撃破・表示]
    Invader -->|NotifyDefeated| Formation
    Formation -->|得点・全滅通知| Driver
    Formation -->|配置・アニメ・再開| Invader
```

| Component | 所有する責務・状態 |
| --- | --- |
| GameplayDriver | 現在State、Score、Wave番号、WaveClearの暫定1秒待機。全滅通知から遷移し、次Waveを開始する |
| Player | InputReader、入力値と発射受付、release/再押下ゲート。Transformを移動し、Scene内の単一Shotへ発射を依頼 |
| PlayerShot | 動的Rigidbody2DとTrigger。自身の接触をInvaderへ渡し、命中成立後に非アクティブ化して消費。画面上端でも消費 |
| Invader | GameObjectの有効状態による生死、Spriteの2コマ、被弾反応、所属Formationへの一度だけの通知 |
| InvaderFormation | Kinematic Rigidbody2D、55個のInvader参照、集団移動の方向/周期/アニメ位相、生存数の集計。通知からDriverへ得点・全滅を伝える |
| ArcadeViewport | 224:256の表示領域と論理px→Unity単位の倍率 |

`GameplaySimulation`、`PlayerUnit`、`PlayerBody`、`InvaderSlot`、`InvaderFormationSimulation`、旧表示Controller2種、表示/検証スナップショットは削除しました。互換モデルや旧AABB判定は残しません。管理Componentや配列を返すGetterもありません。

## 状態の正とPrefab

位置はPlayerのTransform、ShotとFormationのRigidbody2Dにあります。InvaderはFormation配下のTransformとGameObject有効状態を正とし、別のX/Y・Aliveモデルを持ちません。Formationの `_aliveCount` は個体からの通知で更新する集計値です。Score・Wave番号・生存数のEditor観測にはSerializedObjectを使い、検証用のランタイムGetterは追加しません。

Player、Shot、InvaderのPrefabを `Assets/Prefabs/Gameplay/` にEditor APIで生成します。PlayerのShot参照はArcadeのPrefab instance overrideです。ShotはPlayerの子にせず、発射後のXがPlayerの移動へ追従しないようにします。

Invaderは5×11個を一度生成し、次Waveでも同じ個体・Rendererを再利用します。行ごとのSpriteは既存のTop/Middle/Bottom A/Bを渡し、画像全体の比率を維持します。表示用の子Visualだけを縮小し、親Colliderの寸法を変えません。ShotもBorder不要のSimple表示です。

## 入力・更新・停止

- Player.Update：左右矢印・Spaceを読む。押しっぱなしでは再発射せず、飛行中の押下も消費する。短い押下は次のFixedUpdateまで保持する。
- Player.FixedUpdate：60px/秒相当で左右移動し、端でclamp。受付済みの発射を行う。左右同時押しは従来どおり右優先。
- Formation.FixedUpdate：20/60秒ごとに2px横移動。端では8px下降・方向反転し、全個体のA/Bを交換する。
- Shot：Rigidbody2Dが240px/秒相当で上昇。FixedUpdateで画面上端を確認し、OnTriggerEnter2Dで接触対象を処理する。
- Driver.Update：WaveClear待機のみ。次Wave開始時にScore、Player位置、Shotの占有状態、releaseゲートを保持し、編隊の位置・方向・生存数・アニメを初期化する。

Unityの既存Fixed Timestep（0.02秒）を使い、ProjectSettingsは変更しません。独自60Hz時計や「Player→Shot→Formation→Collision」の旧tick順序は廃止しました。速度・周期は従来相当に換算しますが、接触タイミング・境界・端の到達frameは厳密一致を保証せず、描画FPS別の実測も未確認です。

非Playing・Driver無効時はPlayer移動/射撃とFormation更新を止め、表示とCollider/物理シミュレーションを停止します。既存Shotの位置・占有と個体の生死は保持します。停止時は未実行入力とFormationの経過時間を破棄し、再開時に停止時間を取り戻しません。Player単体のDisableでも自身とShotを、Formation単体のDisableでも配下の表示・物理を停止します。

## 座標・命中・進行

論理画面は従来どおり224×256です。**1論理px＝0.01 Unity単位**、画面中心がUnity原点です。論理座標との対応は `((x - 112) * 0.01, (y - 128) * 0.01)`。物理速度を小さなUnity単位で扱い、ProjectSettingsの速度制限を変更しません。カメラのOrthographic Sizeは1.28です。

- Player初期位置：(112,24)、移動範囲x=8〜216、Sprite幅16px。
- Shot：幅1×高さ4px。位置は下端中央、発射位置はPlayerの8px上、Collider中心は下端から2px上。
- Formation左上初期位置：(32,208)、間隔16px、Colliderは16×8px。Spriteは既存480×241/PPU30の約16×8.03pxの比率を維持。
- Score：Top=30、Middle=20、Bottom=10。55体の合計は990。Driverが通知の受理時に加算する。HUDは未実装。

Shotが処理中ガードを立ててInvaderに命中を依頼し、Invaderは受理時に直ちにGameObjectを無効化して通知します。Shotも命中成立後に無効化するため、同じ弾の後続通知や同じ個体への別弾は再撃破・再加点しません。最終撃破の通知でDriverは即座に全Componentを停止します。

隣接2体への同時接触は最大1体だけを撃破しますが、どちらが選ばれるかは保証しません。旧独自判定の下段→上段・左→右の優先順位は再現しません。Discrete Triggerと固定物理stepを使うため、高速化時のすり抜け・接触境界の差はローカルで確認が必要です。移動速度・初期位置・WaveClearの1秒等は既存の暫定値を継承しています。

## 検証の範囲

旧モデル・旧Push専用のEditor検証と旧設定ツールは削除しました。代わりに接続・物理設定・Missing Script・Play中の55体と生存集計を確認する `Verify Component Gameplay Setup`、Editor限定の一時停止/再開メニュー、READMEの最小限の操作確認を用意しています。接続チェックは衝突やWave進行の成功を意味しません。

Scene/Prefab移行・Unityコンパイル・Play動作の実行状況はPR本文を参照してください。独立PoC [Physics2DProbe](../Invader/Assets/Prototypes/Physics2DProbe/README.md) は今回変更せず、本編から参照しません。
