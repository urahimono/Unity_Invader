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

1P専用・クレジットなしです。Issue #61のPlayer被弾、Issue #63のLives・復帰・GameOverに加え、Issue #69でShield4基の弾による画素破壊とWave復元、Issue #71でInvader Colliderによる画素侵食、Issue #79でUFO、Issue #81でInvader侵攻によるGameOverを追加しています。待機デモ・開始/リトライ・追加残機/Lives HUD・HighScore保存、弾の相殺は未実装です。検証状況は [Lives・復帰](LIVES.md) と [Shield](SHIELDS.md)・[UFO](UFO.md)・[侵攻GameOver](GAME_OVER.md) を参照してください。

## Playingの責務

```mermaid
flowchart TD
    Input[Input System] --> Reader[PlayerInputReader]
    Reader --> Driver
    Driver -->|PlayerInputStateの値| Player[Player: 移動・射撃受付]
    Driver[GameplayDriver: State / Score / Wave / Lives] -->|SetPlaying / Respawn| Player
    Driver -->|SetPlaying / BeginWave / PreparePlayerRespawn| Formation[InvaderFormation: 集団移動・集計]
    Player -->|TryLaunch / SetPlaying / ResetFlight| Shot[PlayerShot: Rigidbody2D / Trigger / 消費]
    Physics[Unity Physics2D] -->|OnTriggerEnter2D| Shot
    Shot -->|IHitReceiver.TryReceiveHit(this)| Invader[Invader: 個体の被弾・撃破・表示]
    Shot -->|IHitReceiver.TryReceiveHit(this)| Ufo[Ufo: 移動・被弾・解除]
    Shot -.->|FlightEnded: 命中種別| Player
    Player -.->|ShotResolved: 履歴更新済みの命中結果| Driver
    Bomb[DebugCarpetBomb] -->|TryLaunchのみ| Shot
    Ufo -.->|Defeated(source)を購読| Formation
    Invader -.->|Defeated個体・命中元を購読| Formation
    Formation -->|B弾の暫定得点通知・侵攻通知 / IsCleared| Driver
    Formation -->|配置・アニメ・再開| Invader
    Formation -->|種類別phase / TryLaunch / AdvanceStep / SetPlaying| EnemyShot[3種類のEnemyShot: 下降・画面外消滅・再利用]
    Formation -->|Squiggly共有phase / TryLaunch / AdvanceStep / SetPlaying| Ufo
    EnemyShot -->|Trigger / IHitReceiver.TryReceiveHit| Player
    Player -.->|HitAccepted event| Driver
    Formation -->|IPositionProvider.Position / Playerに束縛したFuncで終了回数を取得| Player
    Shot -->|Physics2D領域検索 / 移動区間の画素判定| Shield[Shield: 残存画素・局所破壊・描画]
    EnemyShot -->|Physics2D領域検索 / 移動区間の画素判定| Shield
    Driver -->|SetPlaying / 次WaveのBeginWave| Shield
    Formation -->|各移動ステップの侵食依頼| Invader
    Invader -->|生存個体のColliderと移動量| Shield
```

| Component | 所有する責務・状態 |
| --- | --- |
| GameplayDriver | 入力Readerと全体進行・State・Score・Wave・Livesを所有。PlayerのHitAcceptedとShotResolvedを購読し、更新済みの終了履歴からUFO得点を導出。得点表を所有し、加点後とUpdateでFormation.IsClearedを確認。WaveClearの暫定1秒待機とPlayerDeadの暫定0.5秒待機を管理。PlayerShot参照・終了履歴は持たない |
| Shield | 各基の画素占有を所有し、描画・穴通過・方向別局所破壊を担当。弾の移動区間とInvader Colliderのswept boundsを既存Triggerで検索し、残存画素を判定・消去。死亡・停止時は保持、次Waveで初期画像から復元 |
| DebugCarpetBomb | Editor/Development Build限定のB長押し入力、専用PlayerShot 96発の遅延生成・再利用、23レーンの一巡シャッフル。DriverのState/Waveを読み、非Playing・Driver無効・Wave変更・自身の無効化で専用弾を回収。弾イベントは購読せず、通常終了履歴にも加算しない。命中・Score・全滅・Wave進行は既存Componentへ任せる。製品ビルドにも型とScene参照は残すが入力・生成処理は含めない |
| DebugFormationSpeed | Formationと同じGameObjectでF長押しを読み、Editor/Development Build限定の内部操作で移動経過時間の倍率を10/1へ設定。FixedUpdate実行順-100でFormationより先に反映し、自身またはFormation無効時は1へ戻す。製品ビルドにも型は残すが入力・倍率処理は含めない |
| DebugStatusOverlay | ArcadeのDriverと同じGameObjectで、State・Lives・Wave・Scoreの読み取り専用enum/intを描画時に取得。状態の複製や進行操作を行わず、非Playingでも表示。OnGUIによる開発用表示はEditor/Development Buildのみ有効で、Componentの型は製品ビルドにも残す |
| Player | 入力値・移動・同時1発の射撃枠とrelease/再押下ゲート、通常弾の終了履歴を所有。FlightEndedで枠解放→履歴加算→ShotResolvedを通知。NormalShotEndCountは値のみ公開し、OwnsShotで所有関係を問い合わせ可能。次Waveで履歴リセット、死亡復帰では保持。HitAcceptedで被弾を通知。直接操作するゲーム内ComponentはPlayerShotだけ |
| PlayerShot | 構成済みRigidbody2D/RendererとTrigger。GameObject.activeSelfが飛行状態、MonoBehaviour.enabledが進行可否。IHitReceiver.TryReceiveHit(this)成立後にTargetTypeを取得して消費し、FlightEnded(HitTargetType)を一度通知。Shield吸収・上端はNone、ResetFlightは無通知。得点表・通常弾/B弾の識別・Driver参照は持たない |
| Invader | GameObjectの有効状態による生死、種類別Sprite配列・現在フレームindex・アニメ進行、種類から命中種別への対応付け、被弾反応。Inactive化後にDefeated(個体, 命中元)を一度だけ通知し、Formation参照は持たない |
| InvaderFormation | Kinematic Rigidbody2D、55個のInvader参照、行からの種類選択・配置、集団移動の方向/周期・アニメ進行の共通タイミング、生存数の集計。敵弾/UFOの60Hz時計・種類別phase、3種の発射元/Reload制御。UFOの下降条件・出現カウンタ・開始待ち・Squiggly共有枠・発進時の最新通常弾終了回数による方向判定。既存_aliveCountからIsClearedを公開。B弾の得点だけ暫定的にDriverへ通知 |
| EnemyShot | Kinematic Rigidbody2D、移動step countと速度区分。Formationのphaseから1stepを受け、既存Shield判定と移動を実行。TriggerでPlayerへ被弾受付を依頼し、成立時または画面下端で非アクティブ化して再利用 |
| Ufo | Kinematic Rigidbody2D、飛行方向、Sprite/Collider。共有phaseから2px移動を受け、退出・被弾で解除。IHitReceiverで命中を受理し、Defeated(source)を所属Formationへ通知。飛行中はGameObject有効状態を正とし、独立時計やScoreを所有しない |
| ArcadeViewport | 224:256の表示領域と論理px→Unity単位の倍率 |

`GameplaySimulation`、`PlayerUnit`、`PlayerBody`、`InvaderSlot`、`InvaderFormationSimulation`、旧表示Controller2種、表示/検証スナップショットは削除しました。互換モデルや旧AABB判定は残しません。管理Componentや配列を返すGetterもありません。

## 状態の正とPrefab

位置はPlayerのTransform、Shot・Ufo・FormationのRigidbody2Dにあります。InvaderはFormation配下のTransformとGameObject有効状態を正とし、別のX/Y・Aliveモデルを持ちません。Formationの `_aliveCount` は個体からの通知で更新する集計値です。Editor検証の観測にはSerializedObjectを使い、検証用のランタイムGetterは追加しません。開発用オーバーレイにはDriverの`CurrentState`・`Lives`・`WaveNumber`・`Score`の読み取り専用propertyを使い、管理Componentは公開しません。

Player、Shot、InvaderのPrefabを `Assets/Prefabs/Gameplay/` にEditor APIで生成します。PlayerのShot参照はArcadeのPrefab instance overrideです。ShotはPlayerの子にせず、発射後のXがPlayerの移動へ追従しないようにします。

Issue #87でPlayerShot PrefabとArcadeの通常弾はInactiveへ変更しました。Rigidbody2DとRenderer参照はEditor保存時に設定済みで、実行時のGetComponent/CacheComponents/Initializeはありません。GameObject.activeSelfが飛行状態、MonoBehaviour.enabledが進行可否の正です。OnEnable/OnDisableで表示・物理・速度を導出します。PauseはActiveのままenabled=falseにして占有・位置を保持し、Resumeで継続します。自然終了・ResetFlightはInactiveへ戻します。Inactiveな親階層への発射は拒否します。設定更新はConfigure PlayerShot Prefabで行えます。

```csharp
var shot = Instantiate(shotPrefab);
shot.TryLaunch(position);
```

この利用例に購読者・Driverや準備呼出順は不要です。通常ゲームのPlayerは進行に応じてSetPlayingで明示的に停止/再開します。B弾はTryLaunchで発射しResetFlightで回収するだけです。PlayerはAwakeでFlightEndedを一度購読しOnDestroyで解除します。入力時点の射撃枠を判断し、飛行中の押下は予約しません。RespawnのResetFlightは無通知なので射撃枠も明示的に解放します。B弾はTryLaunchの成否でプールを探し、発射成功でのみレーンを消費します。初回のプール生成時にレーンをシャッフルし、以後は一巡の最後の発射成功時に次巡を準備します。Playerは所有期間中FlightEnded購読を、DriverはPlayerのShotResolved購読を保持するため、一時停止やWaveClearへ遷移した命中処理の途中でも自然終了通知を取りこぼしません。通知の判断履歴は [ADR-0010](adr/0010-player-shot-lifecycle-and-events.md)、現在の状態表現は [ADR-0012](adr/0012-player-shot-unity-state.md) を参照してください。

Issue #97では通常弾の結果をPlayerShot→Player→Driverへ変更しました。PlayerはAwakeでFlightEndedを一度購読しOnDestroyで解除、DriverはStartでPlayer.HitAcceptedとShotResolvedを購読しOnDestroyで解除します。Pause/Disableでは購読を保持します。PlayerInputReaderはDriverのOnEnableで生成、OnDisableでDisposeし、Updateで取得したPlayerInputStateだけをPlayer.SetInputへ渡します。PlayerはFixedUpdateまで短い発射入力を保持します。停止・無効化では未処理の入力値・発射予約だけを破棄し、射撃受付状態（_fireReady）は保持します。WaveClear・Pauseからの再開時に新たなreleaseを一律には要求しません。Respawn時は_fireReady=falseとし、Playing中のrelease→再押下を必要とします。過去の直接購読判断はADR-0011、現在の判断は [ADR-0014](adr/0014-player-shot-results-and-scoring.md) を参照してください。

Player自身のRenderer/ColliderはPrefab保存時、ShotはArcadeのinstance overrideで設定し、Awakeは構成検証だけを行います。SetPlayingやOnEnableから参照を再取得せず、Awakeで進行状態を上書きしないため、DriverとのAwake順に依存しません。FormationにはDriverのStartからPlayerをIPositionProviderとして一度渡し、Rolling照準に現在位置の値だけを使います。EnemyShotは接触先のIHitReceiverに被弾受付を依頼します。FormationのDriver依存は今回維持します。判断は [ADR-0011](adr/0011-player-dependencies.md) を参照してください。

Invaderは5×11個を一度生成し、次Waveでも同じ個体・Rendererを再利用します。Formationは行からInvaderType（Top/Middle/Bottom）を選んでInitializeへ渡します。Invaderは種類をHitTargetTypeへ対応付け、単一Prefabに保存した種類別の任意長Sprite配列から画像を選択します。現行は各2枚のA/Bで、画像全体の比率を維持します。表示用の子Visualだけを縮小し、親Colliderの寸法を変えません。ShotもBorder不要のSimple表示です。

Issue #101では画像参照・現在フレームindexをInvaderが所有します。BeginWaveで先頭へ戻し、Formationの既存MoveFormationから55体すべてへAdvanceAnimationを呼びます。個体ごとの時計は追加せず、死亡個体のフレーム進行も従来どおりです。停止/再開・死亡復帰でフレームをリセットしません。保存済み設定の限定更新はConfigure Invader Animationを使い、Arcadeの再生成は行いません。生成用Editorコードも既存Prefabの未設定配列を補完し、設定済みの任意長配列を維持します。既存の個体表示責務への適合であり、新規ADRは追加しません。

Issue #99ではFormationがCreateInvadersで各個体のDefeatedを一度だけ購読し、OnDestroyで解除します。BeginWaveや一時停止では購読を変更しません。通知は同期的に既存NotifyDefeatedへ届き、生存数・8体以下の敵弾速度・命中元・通常弾/B弾の得点分岐・WaveClearの順序を維持します。既存のイベント通知原則の適用であり、新しいADRは追加しません。

Issue #89では衝突対象をレイヤーで制限します。Player=8、Invader/UFO=9、PlayerShot=10、EnemyShot=11をPrefabへ保存し、Arcadeは継承します。Physics2DではPlayerShot↔PlayerとEnemyShot↔Invader/UFOだけを除外し、他の組合せは維持します。新規生成も同じレイヤーを使い、既存設定更新はConfigure Gameplay Collisionsです。ShieldのDefaultレイヤー・移動区間の画素判定・上下別マスクには変更しません。レイヤー設定の判断は [ADR-0013](adr/0013-hit-receivers-and-ufo-scoring.md)、現在の結果通知・加点順は [ADR-0014](adr/0014-player-shot-results-and-scoring.md) を参照します。

## 入力・更新・停止

- Driver.Update → Player.SetInput：左右矢印・SpaceをReaderで取得し、PlayerInputStateの値を渡す。Playerがrelease/再押下と占有を判定し、短い発射入力を次のFixedUpdateまで保持する。Player.Updateは持たない。
- Player.FixedUpdate：60px/秒相当で左右移動し、端でclamp。受付済みの発射を行う。左右同時押しは従来どおり右優先。
- Formation.FixedUpdate：通常20/60秒ごとに2px横移動。端では8px下降・方向反転し、全個体のA/Bを交換する。開発用F長押しは移動用の経過時間加算だけを10倍にし、既存の各移動・侵食ステップを繰り返す。敵弾専用時計とTime.timeScale / fixedDeltaTimeは変更しない。離した後も処理済みステップを除いた端数だけを引き継ぐ。
- Shot：Rigidbody2Dが240px/秒相当で上昇。FixedUpdateで画面上端を確認し、OnTriggerEnter2Dで接触対象を処理する。
- Driver.Update：WaveClear待機とPlayerDead待機。次WaveではLives・Score・Player位置・Shot占有を保持し、編隊を初期化する。停止中の入力は受理せず、次Waveでも停止前の射撃受付状態を保持する。死亡からの復帰では編隊を初期化しない。

Unityの既存Fixed Timestep（0.02秒）を使います。Issue #89のProjectSettings変更はレイヤー名・接触組合せで、物理stepや速度等の設定値は変更しません。全体の独自60Hz時計や「Player→Shot→Formation→Collision」の旧tick順序は廃止しました。Issue #77では敵弾に限定した60Hz種類別phaseをFormationへ追加し、Physics2Dは0.02秒刻みのままです。速度・周期は従来相当に換算しますが、接触タイミング・境界・端の到達frameは厳密一致を保証せず、描画FPS別の実測も未確認です。

非Playing・Driver無効時はPlayer移動/射撃とFormation更新を止め、表示とCollider/物理シミュレーションを停止します。既存Shotの位置・占有と個体の生死は保持します。停止時は未実行入力とFormationの経過時間を破棄し、再開時に停止時間を取り戻しません。Player単体のDisableでも自身とShotを、Formation単体のDisableでも配下の表示・物理を停止します。

## 座標・命中・進行

論理画面は従来どおり224×256です。**1論理px＝0.01 Unity単位**、画面中心がUnity原点です。論理座標との対応は `((x - 112) * 0.01, (y - 128) * 0.01)`。物理速度を小さなUnity単位で扱い、ProjectSettingsの速度制限を変更しません。カメラのOrthographic Sizeは1.28です。

- Player初期位置：(112,24)、移動範囲x=8〜216、Sprite幅16px。
- Shot：幅1×高さ4px。位置は下端中央、発射位置はPlayerの8px上、Collider中心は下端から2px上。
- Formation左上初期位置：(32,208)、間隔16px、Colliderは16×8px。Spriteは既存480×241/PPU30の約16×8.03pxの比率を維持。
- Score：Top=30、Middle=20、Bottom=10。55体の合計は990。Driverが通知の受理時に加算する。HUDは未実装。

ShotはInvaderへ同期的に命中を依頼し、Invaderは自身をInactive化してFormationの_aliveCountを更新します。通常弾の通知は暫定得点経路では無視します。Shotは成立後にTargetTypeを取得してInactive化し、FlightEndedでPlayerが射撃枠解放→終了履歴加算→ShotResolvedを発行します。Driverは更新済み履歴で加点した後、TryEnterWaveClearでPlayingかつFormation.IsClearedなら一度だけWaveClearへ移行します。Updateでも同じ判定を補完的に呼び、予約フラグ・別の生存カウンタは持ちません。UFOも同じ通常弾の順序で加点し、終了予定分+1補正はありません。

隣接2体への同時接触は最大1体だけを撃破しますが、どちらが選ばれるかは保証しません。旧独自判定の下段→上段・左→右の優先順位は再現しません。Discrete Triggerと固定物理stepを使うため、高速化時のすり抜け・接触境界の差はローカルで確認が必要です。移動速度・初期位置・WaveClearの1秒等は既存の暫定値を継承しています。

通常弾の最終撃破では、従来の受信側内でのWaveClearから、同じ同期呼び出し内の弾終了・履歴更新・加点後へ遷移位置が移ります。Physics2Dの次のTriggerコールバックまでには停止するため、先に処理されたPlayer被弾・侵攻を優先する既存のStateガードは維持します。B弾は従来どおり受信側の同期通知中に遷移します。Update補完は通知経路外で全滅した場合に限る補完で、同一物理更新でのコールバック順自体は保証しません。

## 検証の範囲

### Player被弾（Issue #61）

EnemyShotは自身のOnTriggerEnter2Dで接触を検出し、PlayerがPlaying・Collider有効・未受理を確認して被弾を成立させます。DriverはPlaying中の通知だけでLivesを1機減らしてPlayerDeadへ遷移します。Playerの受理ガードは復帰時だけ解除します。EnemyShotは処理中ガードと非アクティブ化で重複接触を抑止します。

PlayerDeadでは既存SetPlaying(false)で入力・射撃・編隊・弾・UFOの物理と表示を止め、Player Colliderも無効化します。暫定0.5秒後、Livesが残ればPlayer自身のRespawnで初期位置(112,24)へ戻し、旧自機弾・入力・受理ガードを初期化してPlayingへ直接戻ります。Formationは3種類の旧敵弾・step count・敵弾時計と残留UFO・開始待ちを解除し、位置・方向・生存個体・アニメとScore/Wave・通常終了履歴は保持します。0機ならGameOverで停止し、Attractへは戻りません。詳細は [Lives・復帰](LIVES.md) と [UFO](UFO.md) を参照してください。

### Invader侵攻（Issue #81）

Formationは通常の移動ステップ後、移動先Rigidbody2D座標と各生存Invaderのローカル位置から最下位置を判定します。論理y=40未満でDriverへ侵攻通知し、Driverは有効・Playing中だけLivesを0にして直ちにGameOverへ遷移します。PlayerDead・復帰経路は使いません。既存ApplyPlayingStateでPlayer・自機弾・編隊・敵弾3種類・UFO・Shieldを停止し、同じFixedUpdateの残りの編隊移動も中断します。Score/Wave・Shield画素・個体の生死は保持します。

接触判定ではなく生存個体アンカーの座標判定です。原作ROMの描画座標とUnityのSprite中心は異なり、40pxは暫定対応です。演出・原作動画との見た目の一致は未確認です。根拠・差分は [侵攻GameOver](GAME_OVER.md) を参照してください。既存の責務と通知方式の拡張に留まり、新規ADRは追加しません。

### 3種類のEnemyShot（Issue #77）

FormationがRollingのPlayer対応列、Plunger16列、Squiggly15列を選びます。単一の敵弾専用60Hz蓄積時計でRolling=0 / Plunger=1 / Squiggly=2のphaseを0→2→1の順に処理し、各種は3tickに1回だけ発射試行または移動します。旧2秒タイマーは廃止しました。他2種のstep countが0またはScore依存Reload値を厳密に超えている場合だけ新規発射できます。ScoreはDriverの読み取り専用値から取得します。

EnemyShotは3つの独立したインスタンスで、発射時count=1・移動ごとに加算し、既存のShield画素判定・Player Trigger・下端消滅を共用します。独立FixedUpdate時計はなく、Formationのphaseから移動を依頼します。各物理stepの先頭で前stepのMovePosition速度を保持位置へ戻してから、選択された種類の移動先を設定します。Rigidbody2D/GameObject有効状態を正とする点は維持しています。

次Waveで3弾と両表ポインタを初期化、死亡復帰で弾枠だけを整理してポインタと編隊は保持します。初期phase・Wave/死亡時の持越し、爆発待ち省略、ビーム別ISRと0.02秒Physics2Dとの量子化差等は原作との未解決差分です。詳細・Reloadの実境界は [3種類のEnemyShot](ENEMY_SHOTS.md)、限定した時計の設計判断は [ADR-0009](adr/0009-enemy-shot-phases.md) を参照してください。Rolling/Plunger導入当時の記録は [Rolling](ROLLING_ENEMY_SHOT.md)・[Plunger](PLUNGER_ENEMY_SHOT.md) に残します。

### UFO（Issue #79）

Formationの既存phase 2・下降条件・8体以上の開始条件は維持します。Ufoは移動・被弾・解除、Playerは通常弾終了履歴、Driverは得点表と加点を所有します。Issue #103では発進方向の判定もFormationへ移しました。Driverは初期化時にPlayer.GetNormalShotEndCountをFunc<long>として渡し、Formationは発進時にその最新値の偶奇を評価します。回数を複製せず、IPositionProviderはPositionのみの契約を維持します。既存責務内の整理に留まるため、新規ADRは追加しません。Ufo.DefeatedはFormationが購読し、通常弾以外のB弾用の暫定通知としてDriverへ伝えます。DriverはPlayer.OwnsShot(source)で通常弾を除外し、B弾は現在のPlayer履歴で加点します。B弾のInvader撃破も同じ暫定経路で加点後に全滅判定し、通常弾履歴は進めません。FormationのDriver参照はB弾加点・侵攻通知・敵弾Reload用Score参照のため維持します。B弾の再設計・新規Managerは導入しません。詳細は [UFO](UFO.md) と [ADR-0014](adr/0014-player-shot-results-and-scoring.md) を参照してください。

旧モデル・旧Push専用のEditor検証と旧設定ツールは削除しました。代わりに接続・物理設定・Missing Script・Play中の55体と生存集計を確認する `Verify Component Gameplay Setup`、Editor限定の一時停止/再開メニュー、READMEの最小限の操作確認を用意しています。接続チェックは衝突やWave進行の成功を意味しません。

Scene/Prefab移行・Unityコンパイル・Play動作の実行状況はPR本文を参照してください。独立PoC [Physics2DProbe](../Invader/Assets/Prototypes/Physics2DProbe/README.md) は今回変更せず、本編から参照しません。
