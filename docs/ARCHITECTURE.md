# Architecture

現在のコード構造の正本です。判断基準は [PROJECT_PRINCIPLES](PROJECT_PRINCIPLES.md)、変更理由は [ADR-0008](adr/0008-component-gameplay.md) を参照します。

## 移行状態と起動

Issue #57でPlayingのコードをComponent/Physics2Dへ移行し、現在はArcade SceneとGameplay Prefabを保存済みです。Issue #59ではRolling敵弾をEditor APIで追加し、Unity 6000.6.4f1の非表示バッチで限定Play確認を実施しました。Scene/PrefabのYAMLは直接編集していません。

通常は保存済みBootstrapから起動します。Scene再生成が必要な場合の移行メニューは [README](../README.md) を参照してください。

| Scene | 役割 |
| --- | --- |
| Bootstrap | StartでArcadeをSingleロード。今回変更なし |
| Arcade | Driver、Player、単一PlayerShot、Formation、Rolling/Plunger/Squiggly各1個のEnemyShot、単一Ufo、Shield4基、カメラ |
| SampleScene | 既存サンプル。今回変更なし |

1P専用・クレジットなしです。Issue #61のPlayer被弾、Issue #63のLives・復帰・GameOverに加え、Issue #69でShield4基の弾による画素破壊とWave復元、Issue #71でInvader Colliderによる画素侵食、Issue #79でUFOを追加しています。待機デモ・開始/リトライ・追加残機/Lives HUD・HighScore保存、弾の相殺、Invader侵攻による敗北条件は未実装です。検証状況は [Lives・復帰](LIVES.md) と [Shield](SHIELDS.md)・[UFO](UFO.md) を参照してください。

## Playingの責務

```mermaid
flowchart TD
    Input[Input System] --> Reader[PlayerInputReader]
    Reader --> Player[Player: 入力・移動・射撃受付]
    Driver[GameplayDriver: State / Score / Wave / Lives] -->|SetPlaying / Respawn| Player
    Driver -->|SetPlaying / BeginWave / PreparePlayerRespawn| Formation[InvaderFormation: 集団移動・集計]
    Player -->|TryLaunch / SetPlaying| Shot[PlayerShot: Rigidbody2D / Trigger / 消費]
    Physics[Unity Physics2D] -->|OnTriggerEnter2D| Shot
    Shot -->|TryReceiveHit| Invader[Invader: 個体の被弾・撃破・表示]
    Shot -->|TryReceiveHit| Ufo[Ufo: 移動・被弾・解除]
    Shot -->|自然終了通知 / UFO得点通知| Driver
    Invader -->|NotifyDefeated| Formation
    Formation -->|得点・全滅通知| Driver
    Formation -->|配置・アニメ・再開| Invader
    Formation -->|種類別phase / TryLaunch / AdvanceStep / SetPlaying| EnemyShot[3種類のEnemyShot: 下降・画面外消滅・再利用]
    Formation -->|Squiggly共有phase / TryLaunch / AdvanceStep / SetPlaying| Ufo
    EnemyShot -->|Trigger / TryReceiveHit| Player
    Player -->|NotifyPlayerHit| Driver
    Shot -->|Physics2D領域検索 / 移動区間の画素判定| Shield[Shield: 残存画素・局所破壊・描画]
    EnemyShot -->|Physics2D領域検索 / 移動区間の画素判定| Shield
    Driver -->|SetPlaying / 次WaveのBeginWave| Shield
    Formation -->|各移動ステップの侵食依頼| Invader
    Invader -->|生存個体のColliderと移動量| Shield
```

| Component | 所有する責務・状態 |
| --- | --- |
| GameplayDriver | 現在State、Score、Wave番号、Lives、通常PlayerShot終了履歴。履歴からUFO方向と15周期の得点表位置を導出。WaveClearの暫定1秒待機とPlayerDeadの暫定0.5秒待機。次Wave・復帰/GameOverを管理 |
| Shield | 各基の画素占有を所有し、描画・穴通過・方向別局所破壊を担当。弾の移動区間とInvader Colliderのswept boundsを既存Triggerで検索し、残存画素を判定・消去。死亡・停止時は保持、次Waveで初期画像から復元 |
| DebugCarpetBomb | Editor/Development Build限定のB長押し入力、専用PlayerShot 96発の遅延生成・再利用、23レーンの一巡シャッフル。DriverのState/Waveを読み、非Playing・Driver無効・Wave変更・自身の無効化で専用弾を回収。生成時に通常終了履歴の対象外とする。命中・Score・全滅・Wave進行は既存Componentへ任せる。製品ビルドにも型とScene参照は残すが入力・生成処理は含めない |
| DebugFormationSpeed | Formationと同じGameObjectでF長押しを読み、Editor/Development Build限定の内部操作で移動経過時間の倍率を10/1へ設定。FixedUpdate実行順-100でFormationより先に反映し、自身またはFormation無効時は1へ戻す。製品ビルドにも型は残すが入力・倍率処理は含めない |
| DebugStatusOverlay | ArcadeのDriverと同じGameObjectで、State・Lives・Wave・Scoreの読み取り専用enum/intを描画時に取得。状態の複製や進行操作を行わず、非Playingでも表示。OnGUIによる開発用表示はEditor/Development Buildのみ有効で、Componentの型は製品ビルドにも残す |
| Player | InputReader、入力値と発射受付、release/再押下ゲート。Transformで移動し被弾をDriverへ通知。復帰時に自身の位置・受理/発射ゲートと自機弾を初期化 |
| PlayerShot | 動的Rigidbody2DとTrigger。Invader/Ufoへ命中を依頼し、成立後に消費。Shield吸収・上端も自然終了とし、通常弾だけDriverへ一度通知。強制ResetFlightは履歴対象外。UFO命中は弾終了後にDriverへ加点を依頼 |
| Invader | GameObjectの有効状態による生死、Spriteの2コマ、被弾反応、所属Formationへの一度だけの通知 |
| InvaderFormation | Kinematic Rigidbody2D、55個のInvader参照、集団移動の方向/周期/アニメ位相、生存数の集計。敵弾/UFOの60Hz時計・種類別phase、3種の発射元/Reload制御。UFOの下降条件・出現カウンタ・開始待ち・Squiggly共有枠。通知からDriverへ得点・全滅を伝える |
| EnemyShot | Kinematic Rigidbody2D、移動step countと速度区分。Formationのphaseから1stepを受け、既存Shield判定と移動を実行。TriggerでPlayerへ被弾受付を依頼し、成立時または画面下端で非アクティブ化して再利用 |
| Ufo | Kinematic Rigidbody2D、飛行方向、Sprite/Collider。共有phaseから2px移動を受け、退出・被弾で解除。飛行中はGameObject有効状態を正とし、独立時計やScoreを所有しない |
| ArcadeViewport | 224:256の表示領域と論理px→Unity単位の倍率 |

`GameplaySimulation`、`PlayerUnit`、`PlayerBody`、`InvaderSlot`、`InvaderFormationSimulation`、旧表示Controller2種、表示/検証スナップショットは削除しました。互換モデルや旧AABB判定は残しません。管理Componentや配列を返すGetterもありません。

## 状態の正とPrefab

位置はPlayerのTransform、Shot・Ufo・FormationのRigidbody2Dにあります。InvaderはFormation配下のTransformとGameObject有効状態を正とし、別のX/Y・Aliveモデルを持ちません。Formationの `_aliveCount` は個体からの通知で更新する集計値です。Editor検証の観測にはSerializedObjectを使い、検証用のランタイムGetterは追加しません。開発用オーバーレイにはDriverの`CurrentState`・`Lives`・`WaveNumber`・`Score`の読み取り専用propertyを使い、管理Componentは公開しません。

Player、Shot、InvaderのPrefabを `Assets/Prefabs/Gameplay/` にEditor APIで生成します。PlayerのShot参照はArcadeのPrefab instance overrideです。ShotはPlayerの子にせず、発射後のXがPlayerの移動へ追従しないようにします。

Invaderは5×11個を一度生成し、次Waveでも同じ個体・Rendererを再利用します。行ごとのSpriteは既存のTop/Middle/Bottom A/Bを渡し、画像全体の比率を維持します。表示用の子Visualだけを縮小し、親Colliderの寸法を変えません。ShotもBorder不要のSimple表示です。

## 入力・更新・停止

- Player.Update：左右矢印・Spaceを読む。押しっぱなしでは再発射せず、飛行中の押下も消費する。短い押下は次のFixedUpdateまで保持する。
- Player.FixedUpdate：60px/秒相当で左右移動し、端でclamp。受付済みの発射を行う。左右同時押しは従来どおり右優先。
- Formation.FixedUpdate：通常20/60秒ごとに2px横移動。端では8px下降・方向反転し、全個体のA/Bを交換する。開発用F長押しは移動用の経過時間加算だけを10倍にし、既存の各移動・侵食ステップを繰り返す。敵弾専用時計とTime.timeScale / fixedDeltaTimeは変更しない。離した後も処理済みステップを除いた端数だけを引き継ぐ。
- Shot：Rigidbody2Dが240px/秒相当で上昇。FixedUpdateで画面上端を確認し、OnTriggerEnter2Dで接触対象を処理する。
- Driver.Update：WaveClear待機とPlayerDead待機。次WaveではLives・Score・Player位置・Shot占有・releaseゲートを保持し、編隊を初期化する。死亡からの復帰では編隊を初期化しない。

Unityの既存Fixed Timestep（0.02秒）を使い、ProjectSettingsは変更しません。全体の独自60Hz時計や「Player→Shot→Formation→Collision」の旧tick順序は廃止しました。Issue #77では敵弾に限定した60Hz種類別phaseをFormationへ追加し、Physics2Dは0.02秒刻みのままです。速度・周期は従来相当に換算しますが、接触タイミング・境界・端の到達frameは厳密一致を保証せず、描画FPS別の実測も未確認です。

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

### Player被弾（Issue #61）

EnemyShotは自身のOnTriggerEnter2Dで接触を検出し、PlayerがPlaying・Collider有効・未受理を確認して被弾を成立させます。DriverはPlaying中の通知だけでLivesを1機減らしてPlayerDeadへ遷移します。Playerの受理ガードは復帰時だけ解除します。EnemyShotは処理中ガードと非アクティブ化で重複接触を抑止します。

PlayerDeadでは既存SetPlaying(false)で入力・射撃・編隊・弾・UFOの物理と表示を止め、Player Colliderも無効化します。暫定0.5秒後、Livesが残ればPlayer自身のRespawnで初期位置(112,24)へ戻し、旧自機弾・入力・受理ガードを初期化してPlayingへ直接戻ります。Formationは3種類の旧敵弾・step count・敵弾時計と残留UFO・開始待ちを解除し、位置・方向・生存個体・アニメとScore/Wave・通常終了履歴は保持します。0機ならGameOverで停止し、Attractへは戻りません。詳細は [Lives・復帰](LIVES.md) と [UFO](UFO.md) を参照してください。

### 3種類のEnemyShot（Issue #77）

FormationがRollingのPlayer対応列、Plunger16列、Squiggly15列を選びます。単一の敵弾専用60Hz蓄積時計でRolling=0 / Plunger=1 / Squiggly=2のphaseを0→2→1の順に処理し、各種は3tickに1回だけ発射試行または移動します。旧2秒タイマーは廃止しました。他2種のstep countが0またはScore依存Reload値を厳密に超えている場合だけ新規発射できます。ScoreはDriverの読み取り専用値から取得します。

EnemyShotは3つの独立したインスタンスで、発射時count=1・移動ごとに加算し、既存のShield画素判定・Player Trigger・下端消滅を共用します。独立FixedUpdate時計はなく、Formationのphaseから移動を依頼します。各物理stepの先頭で前stepのMovePosition速度を保持位置へ戻してから、選択された種類の移動先を設定します。Rigidbody2D/GameObject有効状態を正とする点は維持しています。

次Waveで3弾と両表ポインタを初期化、死亡復帰で弾枠だけを整理してポインタと編隊は保持します。初期phase・Wave/死亡時の持越し、爆発待ち省略、ビーム別ISRと0.02秒Physics2Dとの量子化差等は原作との未解決差分です。詳細・Reloadの実境界は [3種類のEnemyShot](ENEMY_SHOTS.md)、限定した時計の設計判断は [ADR-0009](adr/0009-enemy-shot-phases.md) を参照してください。Rolling/Plunger導入当時の記録は [Rolling](ROLLING_ENEMY_SHOT.md)・[Plunger](PLUNGER_ENEMY_SHOT.md) に残します。

### UFO（Issue #79）

Formationの既存phase 2をSquigglyと共有し、下降後の出現カウンタ・8体以上の開始条件を追加しました。Ufo自身は移動と被弾・解除、PlayerShotは既存Physics2D接触と自然終了通知、Driverは唯一の履歴と加点を担当します。通常弾の命中終了を先に履歴へ反映してからUFO得点を選ぶ順序です。B弾は同じPrefabの初期化フラグで履歴対象外にします。新しいSchedulerや管理ComponentのGetterはありません。既存のComponent/通知・phase方式を拡張するため新規ADRは追加せず、具体的な規則・近似・検証は [UFO](UFO.md) に記録します。

旧モデル・旧Push専用のEditor検証と旧設定ツールは削除しました。代わりに接続・物理設定・Missing Script・Play中の55体と生存集計を確認する `Verify Component Gameplay Setup`、Editor限定の一時停止/再開メニュー、READMEの最小限の操作確認を用意しています。接続チェックは衝突やWave進行の成功を意味しません。

Scene/Prefab移行・Unityコンパイル・Play動作の実行状況はPR本文を参照してください。独立PoC [Physics2DProbe](../Invader/Assets/Prototypes/Physics2DProbe/README.md) は今回変更せず、本編から参照しません。
