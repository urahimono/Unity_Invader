# Architecture

現在のコード構造の正本です。判断基準は [PROJECT_PRINCIPLES](PROJECT_PRINCIPLES.md)、変更理由は [ADR-0008](adr/0008-component-gameplay.md) を参照します。

## 移行状態と起動

Issue #57でPlayingのコードをComponent/Physics2Dへ移行し、現在はArcade SceneとGameplay Prefabを保存済みです。Issue #59ではRolling敵弾をEditor APIで追加し、Unity 6000.6.4f1の非表示バッチで限定Play確認を実施しました。Scene/PrefabのYAMLは直接編集していません。

通常は保存済みBootstrapから起動します。Scene再生成が必要な場合の移行メニューは [README](../README.md) を参照してください。

Issue #124では初期化失敗を例外ではなく成功・失敗の値で扱います。PlayerはAwakeで必須参照を検証し、成功時だけ弾終了を購読して`IsInitialized`を確定します。DriverはStartでPlayerの成功を確認し、Formation→EnemyShotController・各Invaderの`Initialize`がすべて成功した場合だけ進行用イベントを購読し、初期化済みとしてStateを適用します。原因を検出した担当が一度LogErrorを出し、上位は重複ログを出さず停止します。Formationの途中生成失敗では個体を停止・購読解除・破棄し、DriverはPlayer・Formation・敵弾・UFO・Shieldの進行を止めて無効になります。初期化はインスタンスごとに一度だけ試行し、失敗後は参照の修正や再有効化で通常進行へ復帰せず、構成を直したうえでSceneを再ロードします。正常時のWave・Pause・死亡復帰の契約は維持します。

| Scene | 役割 |
| --- | --- |
| Bootstrap | StartでArcadeをLoadSceneAsyncのSingleモードで読み込み、UniTaskで完了を待つ |
| Arcade | Driver、Player、単一PlayerShot、Formation、Rolling/Plunger/Squiggly各1個のEnemyShot、単一Ufo、Shield4基、カメラ |
| SampleScene | 既存サンプル。今回変更なし |

BootstrapはStartから一度だけ非同期読み込みを起動し、開始済みフラグで同じインスタンスからの再起動を防ぎます。`LoadSceneAsync`を`ToUniTask()`で変換してawaitし、例外は`Forget(Debug.LogException)`でUnity Consoleへ記録します。読み込みに失敗しても自動リトライしません。Single遷移によるBootstrap破棄は正常なため、破棄・Disableによる待機キャンセルやCTSは導入しません。CancellationTokenで待機を中断してもUnityのScene読み込みそのものを取り消せるとは扱いません。

待機はScene読み込み要求の完了まで継続する方針で、staticな非同期メソッドは完了後にBootstrapやArcadeの状態を操作しません。UniTask 2.5.11のToUniTaskは既定UpdateでのisDone確認に加えてAsyncOperation.completed通知でも完了するため、特定のPlayerLoop位置やArcadeのAwake/Startとの新しい順序保証は追加しません。読み込みの所要フレームは同期方式と異なり得ます。Bootstrap→Arcade→初期化成功後Attract→EnterでPlayingという契約と2Scene構成は維持します。Play停止時のエンジン側の読み込み終了を独自に制御せず、停止・再実行の実確認状況はPRに記録します。既存の非同期設計原則を局所適用するため新規ADRは追加しません。

1P専用・クレジットなしです。Issue #61のPlayer被弾、Issue #63のLives・復帰・GameOverに加え、Issue #69でShield4基の弾による画素破壊とWave復元、Issue #71でInvader Colliderによる画素侵食、Issue #79でUFO、Issue #81でInvader侵攻によるGameOverを追加しています。待機デモ・HighScore保存は未実装です。弾の相殺と爆発待機は実装済みですが、精密な視覚再現は未実装です。検証状況は [Lives・復帰](LIVES.md) と [Shield](SHIELDS.md)・[UFO](UFO.md)・[侵攻GameOver](GAME_OVER.md) を参照してください。

## 1P開始とGameOver後の新規ゲーム（Issue #131）

保存Bootstrap→Arcadeの初期化成功後は必ずAttractです。保存済みSceneの`_developmentStartPlaying: 1`は旧互換データとして残しますが、Driverは無視して実行時にfalseにします。Scene/Prefabの変更は不要です。初期化失敗時は従来どおり停止し、開始入力で復帰させません。

PlayerInputReaderは左右/SpaceのPlayerInputStateとは別に、Enterのrelease→押下を`ReadStartPressed()`でDriverへ返します。Driverは各Updateで押下を消費し、AttractまたはGameOverのときだけ`BeginNewGame()`へ進みます。Playerへ開始操作を渡しません。Disable/Enableではreaderを作り直し、Enterのreleaseを再要求します。PlayerReadyの待機・演出は追加しません。

新規ゲームは同一Scene再初期化です。Driverは所有者を停止し、`GameStarting`を通知してからScore=0 / Wave=1 / Lives=3と待機時計を設定します。Playerの`Respawn`と`ResetShotHistory`、Formationの`BeginWave`（敵弾3種・列選択・phase/時計・UFO cycle・55体の位置/生死/アニメを含む）、Shield4基の`BeginWave`を呼び、最後にPlayingへ移します。Initializeやイベント購読は再実行しません。既存オブジェクトと購読を再利用でき、Sceneロードのライフサイクルを追加しないためこの方式を選びました。

`GameStarting`は所有者停止後・新規状態復元前の通知です。DebugCarpetBombは自身のOnEnable/OnDisableで購読/解除し、自身の弾を無通知で回収して発射待機とレーンをリセットします。新ゲームではBのreleaseを要求します。DriverはB弾の内部や下位のEnemyShotController/Ufoを直接操作しません。PlayerのRespawn後のSpace releaseゲート、通常の死亡0.5秒・WaveClear1秒・Pause/Resume・得点通知と初期化一度きりの契約は維持します。

## Playingの責務

```mermaid
flowchart TD
    Input[Input System] --> Reader[PlayerInputReader]
    Reader --> Driver
    Driver -->|PlayerInputStateの値| Player[Player: 移動・射撃受付]
    Driver[GameplayDriver: State / Score / Wave / Lives] -->|SetPlaying / Respawn| Player
    Driver -->|SetPlaying / BeginWave / PreparePlayerRespawn / Score読取関数| Formation[InvaderFormation: 集団移動・集計]
    Player -->|TryLaunch / SetPlaying / ResetFlight| Shot[PlayerShot: Rigidbody2D / Trigger / 消費]
    Physics[Unity Physics2D] -->|OnTriggerEnter2D| Shot
    Shot -->|IHitReceiver.TryReceiveHit(this)| Invader[Invader: 個体の被弾・撃破・表示]
    Shot -->|IHitReceiver.TryReceiveHit(this)| Ufo[Ufo: 出現サイクル・移動・被弾・解除]
    Shot -.->|FlightEnded: 即時の命中種別 / BecameReusable: 爆発完了| Player
    Player -.->|ShotResolved: 履歴更新済みの命中結果| Driver
    Bomb[DebugCarpetBomb] -->|TryLaunch / ResetFlight| Shot
    Shot -.->|B弾のFlightEnded| Bomb
    Bomb -->|共通NotifyShotResolved| Driver
    Invader -.->|Defeated個体・命中元を購読| Formation
    Formation -.->|Invadedを購読| Driver
    Driver -->|IsClearedを確認| Formation
    Formation -->|配置・アニメ・再開| Invader
    Formation -->|時計更新 / 一括操作| Controller[EnemyShotController: 60Hz時計・phase・3弾の操作]
    Controller -->|TryLaunch / AdvanceStep / 共通操作| EnemyShot[3種類のEnemyShot: 下降・画面外消滅・再利用]
    Controller -->|列番号から発射位置の値を取得| Formation
    Formation -->|共有phase / TickCountdown / ResetCycle / SetPlaying| Ufo
    EnemyShot -->|Trigger / IHitReceiver.TryReceiveHit| Player
    Shot -->|IHitReceiver.TryReceiveHit / 相殺| EnemyShot
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
| GameplayHud | Arcadeの専用Canvas上で、DriverのScore・Lives・CurrentStateをOnEnable/LateUpdateに読み、uGUI Textへ反映する。状態・得点・残機の管理や入力処理は持たず、通常製品ビルドでも表示する |
| Shield | 各基の画素占有を所有し、描画・穴通過・方向別局所破壊を担当。弾の移動区間とInvader Colliderのswept boundsを既存Triggerで検索し、残存画素を判定・消去。死亡・停止時は保持、次Waveで初期画像から復元 |
| DebugCarpetBomb | Editor/Development Build限定のB長押し入力、専用PlayerShot 96発の遅延生成・再利用、23レーンの一巡シャッフル。DriverのState/Waveを読み、非Playing・Driver無効・Wave変更・自身の無効化で専用弾を回収。生成時にFlightEndedを一度購読し、Driverの共通NotifyShotResolvedへ命中種別を渡す。破棄時に解除し、Playerの通常終了履歴には加算しない。Score・全滅・Wave進行はDriverへ任せる。製品ビルドにも型とScene参照は残すが入力・生成・結果通知処理は含めない |
| DebugFormationSpeed | Formationと同じGameObjectでF長押しを読み、Editor/Development Build限定の内部操作で移動経過時間の倍率を10/1へ設定。FixedUpdate実行順-100でFormationより先に反映し、自身またはFormation無効時は1へ戻す。製品ビルドにも型は残すが入力・倍率処理は含めない |
| DebugStatusOverlay | ArcadeのDriverと同じGameObjectで、State・Lives・Wave・Scoreの読み取り専用enum/intを描画時に取得。状態の複製や進行操作を行わず、非Playingでも表示。OnGUIによる開発用表示はEditor/Development Buildのみ有効で、Componentの型は製品ビルドにも残す |
| Player | 入力値・移動・同時1発の射撃枠とrelease/再押下ゲート、通常弾の終了履歴を所有。FlightEndedで履歴加算→ShotResolvedを即時通知し、BecameReusableで爆発完了後に枠解放。NormalShotEndCountは値のみ公開。次Waveで履歴リセット、死亡復帰では保持。HitAcceptedで被弾を通知。直接操作するゲーム内ComponentはPlayerShotだけ |
| PlayerShot | 構成済みRigidbody2D/RendererとTrigger。GameObject.activeSelfが飛行・爆発中の占有、MonoBehaviour.enabledが進行可否。爆発残量で飛行と待機を区別。IHitReceiver.TryReceiveHit(this)成立後にTargetTypeを取得し、FlightEnded(HitTargetType)を即時に一度通知。爆発完了でInactive化しBecameReusableを通知。Shield吸収・上端はNone、ResetFlightは無通知。得点表・通常弾/B弾の識別・Driver参照は持たない |
| Invader | GameObjectの有効状態による生死、種類別Sprite配列・現在フレームindex・アニメ進行、種類から命中種別への対応付け、被弾反応。Inactive化後にDefeated(個体, 命中元)を一度だけ通知し、Formation参照は持たない |
| InvaderFormation | Kinematic Rigidbody2D、55個のInvader参照、行からの種類選択・配置、集団移動の方向/周期・アニメ進行の共通タイミング、生存数の集計。指定列の最下段生存個体の発射位置取得。UFOへの下降状態・生存数条件・最新通常弾終了回数による発進方向の提供と共有phaseの連携。既存_aliveCountからIsClearedを公開。弾の得点通知は行わない |
| EnemyShotController | Serializableな通常のC#クラス。3つのEnemyShot参照を自身のSerializeFieldとして保存。60Hz時計・phase 0→2→1、一括準備・停止/再開・回収・速度変更、種類別発射条件、Reload計算、Rolling列選択、Plunger/Squiggly列テーブルとポインターを所有。列番号から発射位置を得るFunc<int, Vector2?>と最新ScoreのFunc<int>を受け、FormationやInvaderリスト、UFOは保持しない |
| EnemyShot | Kinematic Rigidbody2D、移動step countと速度区分。Controllerのphaseから1stepを受け、既存Shield判定と移動を実行。TriggerでPlayerへ被弾受付を依頼し、成立時・Shield吸収・画面下端で爆発待機へ移り、自身のphase更新4回後に非アクティブ化して再利用 |
| Ufo | Kinematic Rigidbody2D、出現カウンタ・待機状態・発進・飛行方向、Sprite/Collider。共有phaseから出現判断と2px移動を行い、退出・被弾で解除。IHitReceiverで命中を受理し、自身を非アクティブ化。飛行中はGameObject有効状態を正とし、独立時計やScoreを所有しない |
| ArcadeViewport | 224:256の表示領域と論理px→Unity単位の倍率 |

`GameplaySimulation`、`PlayerUnit`、`PlayerBody`、`InvaderSlot`、`InvaderFormationSimulation`、旧表示Controller2種、表示/検証スナップショットは削除しました。互換モデルや旧AABB判定は残しません。管理Componentや配列を返すGetterもありません。

### Player死亡演出

ArcadeのPlayerDeathEffectは、既存Player.HitAcceptedを購読して被弾時のPosition値と表示開始時刻を記録し、LateUpdateでDriverが有効かつPlayerDeadの間だけ自作Spriteを表示します。Driverの通知購読順は仮定せず、被弾通知中は描画せずに記録だけ行います。被弾なしでPlayerDead/GameOverへStateを設定しても再生せず、Invader侵攻の直接GameOverでも誤再生しません。Player・Driver・GameplayHudのコードと、被弾判定・残機・復帰・追加残機・待機時間は変更しません。

表示期間は既存PlayerDeadの0.5秒待機に従い、独自の終了待機やState変更を持ちません。Time.timeAsDoubleから2フレームの描画indexだけを選ぶため、新しい非同期処理・CTS・完了通知は不要です。復帰・GameOver・その他Stateへの移行は次のLateUpdateで非表示へ戻します。GameStarting、自身のDisableでは即座に表示要求を破棄し、再Enable時もリセットしてから購読するため古い演出を再開しません。Player/Driverの無効化はLateUpdateで検出して破棄します。ツールバーPauseとTimeScale=0では表示フレームの時計も止まります。

Notion「原作仕様・調査」には2種類の爆発spriteを交互表示する記録がありますが、模様・正確な切替間隔は直接確認できていません。今回はEditor設定ツールが生成する自作16×8模様2枚を緑で描画し、切替間隔0.1秒は暫定の見た目だけの値とします。原作画像を抽出・複製せず、音・無敵時間・新しいゲーム機能は追加しません。Configure Player Death Effectで素材とScene参照を保存します。演出ルートにCollider/Rigidbodyは置かず、既存Player Prefabと当たり判定は維持します。既存の表示分離原則への局所追加としてArchitectureへ記録し、新規ADRは追加しません。

### 正式HUD

GameplayHudはSceneに保存されたDriver・Score/Lives/GameOverのText参照だけを保持し、公開int/enumの現在値を表示へ反映します。ScoreとLivesは全Stateで表示し、GAME OVERはGameOver時だけ表示します。値を再計算せず、新ゲーム・加点・被弾・GameOverの変更をLateUpdateで読み直すため、通知経路やゲーム状態のコピーは追加しません。参照不足ではHUD自身を停止してエラーを記録し、本編の進行は変更しません。

Canvasは224×256、倍率0.01のWorld Spaceとしてカメラ内のゲーム領域に合わせます。テキストは入力を受け取らず、EventSystemや新しいパッケージは不要です。既存のDebugStatusOverlayはコード・Scene設定とも変更せず、独立して有効／無効にできます。Editor APIのConfigure Gameplay HUDでHUDだけを配置・参照設定してArcadeへ保存します。

SCORE・LIVES・GAME OVERのTextは、自作5×7ドット・文字送り6px・行高8pxの固定ビットマップFontを共有します。GameplayHudFontは既存のHUD設定ツールからのみ呼ばれるEditor用素材生成処理で、英大文字・数字・空白の二値PNGとFont・Materialを保存します。Point・MipMapなし・非圧縮、Textサイズ8・Best Fitなしとし、Canvasの1単位＝論理1pxと既存矩形配置を維持します。GameplayHudの参照・LateUpdateでの値読み取り・GameOver表示条件、Driverの状態所有と通知経路は変更しません。UFO得点のTextは従来設定を維持します。既存の表示責務の範囲内でフォント素材だけを置き換えるため、新規ADRは追加しません。

白文字・自作字体・上部Score／下部Lives／中央GAME OVERの配置は、参考動画が未指定のため原作一致を未確認の暫定表示です。Livesは既存仕様どおり操作中の1機を含む総数を数値表示します。原作フォントや予備機アイコン、HighScoreは追加しません。追加残機の判定はDriverが担当します。

### 1500点の追加残機

GameplayDriverが非シリアライズの獲得済みフラグを所有します。共通NotifyShotResolvedの従来の加点後、加点前Scoreが1500未満・加点後が1500以上・未獲得ならフラグを確定してLivesを1増やし、その後TryEnterWaveClearへ進みます。得点表・通常弾/UFO/B弾の通知経路と、同期的な加点→WaveClearの順序は維持します。無得点の通知、到達後の追加得点では再獲得しません。

新規ゲーム開始時にScore=0・Lives=3とともにフラグをリセットします。死亡復帰・Wave移行・Disable/Enableではリセットしません。GameplayHudの既存Lives読取を利用し、新しい通知や表示状態を追加しません。Notionの基準は初期3機・1500点で追加1機・各Playerにつき1回で、独立した残機上限は未確認のためクランプは追加しません。根拠・時刻・当時の検証との区別は [LIVES](LIVES.md) を参照します。既存Driver責務への局所追加であり、新規ADRは不要と判断します。

### DebugCarpetBombの連続発射

B入力・Playing・Driver有効状態・Wave変更の監視は既存のFixedUpdateとLateUpdateに残します。初回発射はDefaultExecutionOrder(-100)のFixedUpdateから起動したUniTaskループ内で同期的に行い、以後は0.04〜0.08秒のランダム間隔をTime.fixedTimeAsDoubleの期限にしてUniTask.WaitUntil（PlayerLoopTiming.FixedUpdate）で待ちます。Delayの作成フレームスキップで同じ描画フレーム内の物理stepが除外されるのを避けるため、固定時計の期限を選びます。

期限到達後は専用のUniTaskCompletionSourceを待ち、同コンポーネントのFixedUpdateが入力・State・Waveを確認して完了させることで、その呼び出し位置へ同期的に継続して次のFireBurstを実行します。これによりUniTaskのFixedUpdateランナーから直接発射して他のMonoBehaviourとの順序を変えず、従来の発射位置を維持します。次の期限は各発射時刻から新たに計算し、過去の期限を追いかける連続発射は追加しません。floatの減算からdouble時刻の比較になるため、境界では1物理step程度の差があり得ます。

DebugCarpetBombが連続発射用CTSと再開待ちを所有し、多重開始を抑止します。B解放をFixedUpdateで検出すると待機だけをキャンセルし、飛行中の弾は残します。非Playing・Driver無効・Wave変更は従来どおりFixedUpdate/LateUpdateで検出し、ClearShotsで待機停止と弾回収を行います。自身のDisable/破棄とGameStartingでも停止します。Cancel前に現在参照を外し、固定時刻待機はtokenで即時キャンセル、再開待ちはTrySetCanceledで終了させ、各ループのfinallyがCTSをDisposeします。想定キャンセル以外はForgetの明示的ハンドラーでLogExceptionし、コンポーネントを無効にして回収・停止します。

非同期の各発射前にも現在CTSとの一致・自身の有効状態・Playing・Driver有効・開始Wave・B解放ゲート・B押下を確認します。新ゲームは既存のResetForNewGameでBの一度の解放を要求し、次Waveでは押し続けていれば再開します。プール96発、3発burst、23レーンのシャッフル、得点通知と回収APIは維持します。TimeScale=0・ツールバーPauseでは物理時計が進まず、待機も進みません。通常製品ビルドでは従来どおり型・Scene参照だけを残し、非同期処理を含む開発用処理はビルド条件で除外します。既存非同期原則の局所適用として記録し、新規Manager・Timer・ADRは追加しません。Play Modeの実測確認は一連のUniTask改修後の手動確認とします。

## 状態の正とPrefab

位置はPlayerのTransform、Shot・Ufo・FormationのRigidbody2Dにあります。InvaderはFormation配下のTransformとGameObject有効状態を正とし、別のX/Y・Aliveモデルを持ちません。Formationの `_aliveCount` は個体からの通知で更新する集計値です。Editor検証の観測にはSerializedObjectを使い、検証用のランタイムGetterは追加しません。開発用オーバーレイにはDriverの`CurrentState`・`Lives`・`WaveNumber`・`Score`の読み取り専用propertyを使い、管理Componentは公開しません。

Player、Shot、InvaderのPrefabを `Assets/Prefabs/Gameplay/` にEditor APIで生成します。PlayerのShot参照はArcadeのPrefab instance overrideです。ShotはPlayerの子にせず、発射後のXがPlayerの移動へ追従しないようにします。

Issue #87でPlayerShot PrefabとArcadeの通常弾はInactiveへ変更しました。Rigidbody2DとRenderer参照はEditor保存時に設定済みで、実行時のGetComponent/CacheComponents/Initializeはありません。GameObject.activeSelfが飛行・爆発中の占有、MonoBehaviour.enabledが進行可否の正です。OnEnable/OnDisableで表示・物理・速度を導出します。PauseはActiveのままenabled=falseにして占有・位置を保持し、Resumeで継続します。自然終了は爆発待機後にInactiveへ戻し、ResetFlightは待機も即座に無通知で中断します。Inactiveな親階層への発射は拒否します。設定更新はConfigure PlayerShot Prefabで行えます。

```csharp
var shot = Instantiate(shotPrefab);
shot.TryLaunch(position);
```

この利用例に購読者・Driverや準備呼出順は不要です。通常ゲームのPlayerは進行に応じてSetPlayingで明示的に停止/再開します。B弾の所有者DebugCarpetBombはTryLaunchで発射しResetFlightで回収し、生成時にFlightEndedを一度購読してOnDestroyで解除します。PlayerはAwakeでFlightEndedとBecameReusableを一度購読しOnDestroyで解除します。入力時点の射撃枠を判断し、飛行中の押下は予約しません。RespawnのResetFlightは無通知なので射撃枠も明示的に解放します。B弾はTryLaunchの成否でプールを探し、発射成功でのみレーンを消費します。初回のプール生成時にレーンをシャッフルし、以後は一巡の最後の発射成功時に次巡を準備します。Playerは所有期間中FlightEnded購読を、DriverはPlayerのShotResolved購読を保持するため、一時停止やWaveClearへ遷移した命中処理の途中でも自然終了通知を取りこぼしません。通知の判断履歴は [ADR-0010](adr/0010-player-shot-lifecycle-and-events.md)、現在の状態表現は [ADR-0012](adr/0012-player-shot-unity-state.md) を参照してください。

Issue #97では通常弾の結果をPlayerShot→Player→Driverへ変更しました。PlayerはAwakeでFlightEndedを一度購読しOnDestroyで解除、DriverはStartでPlayer.HitAcceptedとShotResolvedを購読しOnDestroyで解除します。Pause/Disableでは購読を保持します。PlayerInputReaderはDriverのOnEnableで生成、OnDisableでDisposeし、Updateで取得したPlayerInputStateだけをPlayer.SetInputへ渡します。PlayerはFixedUpdateまで短い発射入力を保持します。停止・無効化では未処理の入力値・発射予約だけを破棄し、射撃受付状態（_fireReady）は保持します。WaveClear・Pauseからの再開時に新たなreleaseを一律には要求しません。Respawn時は_fireReady=falseとし、Playing中のrelease→再押下を必要とします。過去の直接購読判断はADR-0011、現在の判断は [ADR-0014](adr/0014-player-shot-results-and-scoring.md) を参照してください。

Player自身のRenderer/ColliderはPrefab保存時、ShotはArcadeのinstance overrideで設定し、Awakeは構成検証だけを行います。SetPlayingやOnEnableから参照を再取得せず、Awakeで進行状態を上書きしないため、DriverとのAwake順に依存しません。FormationにはDriverのStartからPlayerをIPositionProviderとして一度渡し、Rolling照準に現在位置の値だけを使います。EnemyShotは接触先のIHitReceiverに被弾受付を依頼します。Issue #110でFormationのGameplayDriver型・インスタンスの直接保持は撤去しました。必要なScoreのFunc<int>は初期化時に受け取り、EnemyShotControllerへ渡して発射試行時に最新値を読みます。判断は [ADR-0011](adr/0011-player-dependencies.md) を参照してください。

Invaderは5×11個を一度生成し、次Waveでも同じ個体・Rendererを再利用します。Formationは行からInvaderType（Top/Middle/Bottom）を選んでInitializeへ渡します。Invaderは種類をHitTargetTypeへ対応付け、単一Prefabに保存した種類別の任意長Sprite配列から画像を選択します。現行は各2枚のA/Bで、画像全体の比率を維持します。表示用の子Visualだけを縮小し、親Colliderの寸法を変えません。ShotもBorder不要のSimple表示です。

Issue #101では画像参照・現在フレームindexをInvaderが所有します。BeginWaveで先頭へ戻し、Formationの既存MoveFormationから55体すべてへAdvanceAnimationを呼びます。個体ごとの時計は追加せず、死亡個体のフレーム進行も従来どおりです。停止/再開・死亡復帰でフレームをリセットしません。保存済み設定の限定更新はConfigure Invader Animationを使い、Arcadeの再生成は行いません。生成用Editorコードも既存Prefabの未設定配列を補完し、設定済みの任意長配列を維持します。既存の個体表示責務への適合であり、新規ADRは追加しません。

Issue #99ではFormationがCreateInvadersで各個体のDefeatedを一度だけ購読し、OnDestroyで解除するようにしました。BeginWaveや一時停止では購読を変更しません。NotifyDefeatedは生存数と8体以下の敵弾速度を更新します。Issue #139では移動進捗を保持したまま、次回の進捗加算に更新後の生存数を使います。Issue #105で得点通知を弾の所有者へ移したため、Formationは命中元による得点分岐を行いません。

Issue #89では衝突対象をレイヤーで制限します。Player=8、Invader/UFO=9、PlayerShot=10、EnemyShot=11をPrefabへ保存し、Arcadeは継承します。Physics2DではPlayerShot↔PlayerとEnemyShot↔Invader/UFOだけを除外し、他の組合せは維持します。新規生成も同じレイヤーを使い、既存設定更新はConfigure Gameplay Collisionsです。ShieldのDefaultレイヤー・移動区間の画素判定・上下別マスクには変更しません。レイヤー設定の判断は [ADR-0013](adr/0013-hit-receivers-and-ufo-scoring.md)、現在の結果通知・加点順は [ADR-0014](adr/0014-player-shot-results-and-scoring.md) を参照します。

## 入力・更新・停止

- Driver.Update → Player.SetInput：左右矢印・SpaceをReaderで取得し、PlayerInputStateの値を渡す。Playerがrelease/再押下と占有を判定し、短い発射入力を次のFixedUpdateまで保持する。Player.Updateは持たない。
- Player.FixedUpdate：60px/秒相当で左右移動し、端でclamp。受付済みの発射を行う。左右同時押しは従来どおり右優先。
- Formation.FixedUpdate：生存数Nに対しN/60秒を1移動とし、各FixedUpdateで移動進捗へ `fixedDeltaTime * 60 / N` を加算する（0体では移動しない）。進捗1ごとに一括移動し端数を保持するため、撃破前の時間を新周期で再計算しない。通常は2px横移動。左端反転時に1体なら以降の右行程を3pxとし、左行程は常に2px。右行程中の最後の撃破では次の左端反転まで2pxを維持する。端では8px下降・方向反転し、全個体のA/Bを交換する。開発用F長押しは移動用の経過時間加算だけを10倍にし、既存の各移動・侵食ステップを繰り返す。敵弾専用時計とTime.timeScale / fixedDeltaTimeは変更しない。離した後も処理済みステップを除いた端数だけを引き継ぐ。
- Shot：Rigidbody2Dが240px/秒相当で上昇。FixedUpdateで画面上端を確認し、OnTriggerEnter2Dで接触対象を処理する。
- Driver.Update：入力と全滅判定の補完。WaveClearの1秒待機とPlayerDeadの0.5秒待機はDriver所有のUniTaskで行う。次WaveではLives・Score・Player位置・Shot占有を保持し、編隊を初期化する。停止中の入力は受理せず、次Waveでも停止前の射撃受付状態を保持する。死亡からの復帰では編隊を初期化しない。

### WaveClearの非同期待機

加点後の`TryEnterWaveClear`→`ChangeState(WaveClear)`→所有者の停止までは同期処理のままです。Stateが変わるときに旧待機をキャンセルし、WaveClearへ入ると残時間を1秒に設定します。同じStateへの再設定では残時間をリセットせず、待機用CancellationTokenSourceがある間は二重に開始しません。待機の所有者は引き続きGameplayDriverで、他クラスの通知経路・責務は変更しません。

`UniTask.Delay`は`ignoreTimeScale: false`で`Time.deltaTime`に従い、`PlayerLoopTiming.LastUpdate`（通常のMonoBehaviour.Update後）で再開します。TimeScale=0では進まず、UnityツールバーPause中はPlayerLoopが停止するため進みません。EditorのStepでは進んだフレーム分だけ時間が進みます。ゲーム用のAttractへ切り替えるデバッグPauseは別Stateへの遷移なので待機を破棄します。

Disableではスケール付き`Time.timeAsDouble`の開始時との差分を残時間から差し引いて保存し、待機をキャンセルします。Enableではその残時間で再開し、無効中の時間は差し引きません。別State・新ゲーム・破棄では旧待機を破棄します。Cancel前に現在のSource参照を外し、非同期メソッドのfinallyがSourceをDisposeします。キャンセルは即時通知し、該当tokenによるOperationCanceledExceptionだけを正常終了として扱います。その他の例外はForgetの明示的ハンドラーでLogExceptionしDriverを無効にして進行を止めます。

待機完了後はtoken・現在のSourceとの一致・初期化済み・有効・WaveClearを確認し、Formation.BeginWave→Player.ResetShotHistory→Shield4基.BeginWave→Wave番号加算→Playingの順で一度だけ実行します。得点確定・WaveClear遷移の同期順は維持します。

### PlayerDeadの非同期待機

被弾通知でLivesを減らしてPlayerDeadへ移る同期処理は維持し、進入時にDriverが専用のCTS・残時間0.5秒を用意して一度だけUniTask.Delayを開始します。WaveClearと同じscaled時間・LastUpdate再開を使い、TimeScale=0とツールバーPauseでは進みません。Disableはスケール付き時刻差を残時間から引いてキャンセルし、Enableは残時間で再開します。別State・新規ゲーム・破棄は旧待機をキャンセルします。同じStateの再設定では待機を重複開始・リセットしません。

完了時はtoken・現在のCTSとの一致・初期化・有効・PlayerDeadを確認します。Lives=0ならGameOver、残機があればFormation.PreparePlayerRespawn→Player.Respawn→Playingの順です。Score・Wave・Shield損傷・編隊の生存個体や位置は保持し、BeginWaveや通常弾履歴リセットは行いません。CTSは参照を外してからCancelし、各待機のfinallyでDisposeします。想定キャンセル以外は共通のHandleStateWaitExceptionで記録してDriverを無効にします。

共通化は例外ハンドラーに留め、WaveClearとPlayerDeadの待機・残時間・完了処理は専用メソッドで管理します。既存WaveClearの時計・初期化順・キャンセル方式を変えず、汎用Timer等は追加しません。PlayerDeadも旧Update方式に対して、作成フレームの加算スキップ、Update内からLastUpdateへの再開位置変更、Disable境界の時刻差とfloat加算の丸めにより、通常約1フレーム・物理step単位の差があり得ます。実測はPRの検証結果を参照してください。既存の非同期原則の適用であり、新規ADRは追加しません。

厳密なフレーム一致は保証しません。旧Update方式は遷移フレームのdeltaTimeも加算し得ますが、UniTask 2.5.11のDelayは作成フレームを加算しないため、通常約1フレーム後ろへずれ得ます。再開も旧Driver.Update内からLastUpdateへ変わります。Disable境界の残時間はフレーム単位のスケール付き時刻差で保存し、Delay内部のfloat加算や旧方式の`1e-7f`許容とは丸め・境界フレームが異なり得ます。Physics2D内の通知から開始した場合は開始時刻が固定時間の値となるため、その差も物理step／フレーム単位に量子化されます。実測確認はPRの検証結果を参照してください。このDriver内に閉じた待機の変更は既存の非同期設計原則の適用として記録し、新しい汎用時計やADRは追加しません。

Unityの既存Fixed Timestep（0.02秒）を使います。Issue #89のProjectSettings変更はレイヤー名・接触組合せで、物理stepや速度等の設定値は変更しません。全体の独自60Hz時計や「Player→Shot→Formation→Collision」の旧tick順序は廃止しました。Issue #77で追加した敵弾専用60Hz時計・種類別phaseは、Issue #116でEnemyShotControllerへ移し、Physics2Dは0.02秒刻みのままです。速度・周期は従来相当に換算しますが、接触タイミング・境界・端の到達frameは厳密一致を保証せず、描画FPS別の実測も未確認です。

非Playing・Driver無効時はPlayer移動/射撃とFormation更新を止め、表示とCollider/物理シミュレーションを停止します。既存Shotの位置・占有と個体の生死は保持します。停止時は未実行入力とFormationの移動進捗を破棄し、再開時に停止時間を取り戻しません。Player単体のDisableでも自身とShotを、Formation単体のDisableでも配下の表示・物理を停止します。

## 座標・命中・進行

論理画面は従来どおり224×256です。**1論理px＝0.01 Unity単位**、画面中心がUnity原点です。論理座標との対応は `((x - 112) * 0.01, (y - 128) * 0.01)`。物理速度を小さなUnity単位で扱い、ProjectSettingsの速度制限を変更しません。カメラのOrthographic Sizeは1.28です。

- Player初期位置：(112,24)、移動範囲x=8〜216、Sprite幅16px。
- Shot：幅1×高さ4px。位置は下端中央、発射位置はPlayerの8px上、Collider中心は下端から2px上。
- Formation左上初期位置：(32,208)、間隔16px、Colliderは16×8px。Spriteは既存480×241/PPU30の約16×8.03pxの比率を維持。
- Score：Top=30、Middle=20、Bottom=10。55体の合計は990。Driverが通知の受理時に加算する。HUDは未実装。

ShotはInvaderへ同期的に命中を依頼し、Invaderは自身をInactive化してFormationの_aliveCountを更新します。Shotは成立後にTargetTypeを取得して爆発待機へ入り、FlightEndedでPlayerが終了履歴加算→ShotResolvedを発行します。射撃枠は待機完了のBecameReusableで解放します。Driverは更新済み履歴で加点した後、TryEnterWaveClearでPlayingかつFormation.IsClearedなら一度だけWaveClearへ移行します。B弾はFlightEndedを所有者DebugCarpetBombが受け、同じNotifyShotResolvedへ結果を渡します。B弾はPlayerの履歴を進めません。Updateでも同じ全滅判定を補完的に呼び、予約フラグ・別の生存カウンタは持ちません。UFOも同じ終了結果から加点し、終了予定分+1補正はありません。

隣接2体への同時接触は最大1体だけを撃破しますが、どちらが選ばれるかは保証しません。旧独自判定の下段→上段・左→右の優先順位は再現しません。Discrete Triggerと固定物理stepを使うため、高速化時のすり抜け・接触境界の差はローカルで確認が必要です。移動速度・初期位置・WaveClearの1秒等は既存の暫定値を継承しています。

通常弾・B弾とも最終撃破は同じ同期呼び出し内の弾終了・所有者の結果通知・加点後にWaveClearへ遷移します。通常弾だけが所有者Playerの履歴を加算してから通知します。Physics2Dの次のTriggerコールバックまでには停止するため、先に処理されたPlayer被弾・侵攻を優先する既存のStateガードは維持します。Update補完は通知経路外で全滅した場合に限る補完で、同一物理更新でのコールバック順自体は保証しません。

## 検証の範囲

### Player被弾（Issue #61）

EnemyShotは自身のOnTriggerEnter2Dで接触を検出し、PlayerがPlaying・Collider有効・未受理を確認して被弾を成立させます。DriverはPlaying中の通知だけでLivesを1機減らしてPlayerDeadへ遷移します。Playerの受理ガードは復帰時だけ解除します。EnemyShotは処理中ガードと爆発残量・物理停止で重複接触を抑止します。

PlayerDeadでは既存SetPlaying(false)で入力・射撃・編隊・弾・UFOの物理と表示を止め、Player Colliderも無効化します。暫定0.5秒後、Livesが残ればPlayer自身のRespawnで初期位置(112,24)へ戻し、旧自機弾・入力・受理ガードを初期化してPlayingへ直接戻ります。Formationは3種類の旧敵弾・step count・敵弾時計と残留UFO・開始待ちを解除し、位置・方向・生存個体・アニメとScore/Wave・通常終了履歴は保持します。0機ならGameOverで停止し、Attractへは戻りません。詳細は [Lives・復帰](LIVES.md) と [UFO](UFO.md) を参照してください。

### Invader侵攻（Issue #81）

Formationは通常の移動ステップ後、移動先Rigidbody2D座標と各生存Invaderのローカル位置から最下位置を判定します。論理y=40未満でInvadedイベントを同期通知し、Driverは初期化済み・有効・Playing中だけLivesを0にして直ちにGameOverへ遷移します。DriverがInitializeGameplayで一度購読しOnDestroyで解除、停止・Wave変更では購読を維持します（Issue #107）。PlayerDead・復帰経路は使いません。既存ApplyPlayingStateでPlayer・自機弾・編隊・敵弾3種類・UFO・Shieldを停止し、同じFixedUpdateの残りの編隊移動も中断します。Score/Wave・Shield画素・個体の生死は保持します。敵弾Reload用Score参照はIssue #110でFunc<int>へ移し、FormationのDriver型付きフィールド・初期化引数は撤去しました。

接触判定ではなく生存個体アンカーの座標判定です。原作ROMの描画座標とUnityのSprite中心は異なり、40pxは暫定対応です。演出・原作動画との見た目の一致は未確認です。根拠・差分は [侵攻GameOver](GAME_OVER.md) を参照してください。既存の責務と通知方式の拡張に留まり、新規ADRは追加しません。

### 弾同士の相殺（Issue #133・暫定）

EnemyShotはIHitReceiverも実装し、Playing中かつ自身と命中元PlayerShotが有効な飛行中の場合だけ受理します。受理時に自身を4回のphase更新による爆発待機へ移し、step countを完了まで保持します。PlayerShotは既存のFinishFlight(None)からFlightEndedを一度通知します。通常弾履歴更新とB弾所有者の結果通知は即時のまま、Playerの射撃枠解放だけ爆発完了へ遅らせます。Score・Lives・Invader・Shieldは相殺で変更せず、敵弾の再使用は既存Controllerのphase・Reload・列選択・UFO共有枠に従います。処理済み・停止中・PlayerShot以外の命中元は受理しません。

既存のPhysics2D Triggerとレイヤー10↔11を使用し、Scene・Prefab・物理設定の変更や全体管理者への衝突処理追加はありません。これは最小限の暫定仕様で、参考動画での干渉確認、弾種差・画素判定・すり抜け・爆発表示と実時間の原作との照合は未完了です。

### 3種類のEnemyShot（Issue #77）

EnemyShotControllerがRollingのPlayer対応列、Plunger16列、Squiggly15列を選びます。Formationは列番号から生存個体の発射位置だけを返します。単一の敵弾専用60Hz蓄積時計でRolling=0 / Plunger=1 / Squiggly=2のphaseを0→2→1の順に処理し、各種は3tickに1回だけ発射試行または移動します。旧2秒タイマーは廃止しました。他2種のstep countが0またはScore依存Reload値を厳密に超えている場合だけ新規発射できます。ScoreはDriverが所有し、EnemyShotControllerが初期化時に受け取ったFunc<int>を発射試行時に呼んで最新値を取得します。Formation/ControllerにScoreのコピーや同期状態は持ちません。

Issue #112で3弾の操作を集約し、Issue #114で保存参照もControllerへ移しました。FormationはSerializeFieldの_enemyShotsを1つ所有し、Controllerへ実行時の発射位置/Score関数・列数/間隔とUFO共有phase処理・カウンタ更新のコールバックをInitializeで渡します。Controllerを作り直さず、自身の3つの保存参照から実行用配列を作ります。配列・delegate・フラグ・表ポインター・列数/間隔はNonSerializedです。FormationはControllerの一括APIを使い、種類別処理はController内で呼びます。Issue #116で共有時計・phase順をControllerへ移しました。Formationは移動後、Playing中だけ敵弾→UFOのPreparePhysicsStepを行い、位置・生存数を渡してController.Updateを一度呼びます。Controllerは各phaseの処理後・phase進行前にFormationのcountdown更新を呼び、phase 2ではUFO処理がphaseを消費しなかった場合だけSquigglyを処理します。Issue #118で出現カウンタ・待機状態・発進・移動のphase処理をUfoへ移しました。ControllerはSquigglyが空いているかをboolでFormationへ渡し、Formationは下降条件・8体以上か・最新履歴による発進方向を値としてUfoへ渡します。時計・phaseもNonSerializedで、停止・復帰・Wave時の既存順序でResetClockを呼びます。Arcadeの旧3参照は旧フィールドを残した状態でEditor APIによりコピー・保存・再ロードし、GlobalObjectId一致を確認した後に旧フィールドを削除しました。Editor設定ツールは_enemyShots配下の相対プロパティを扱います。新規GameObject/Componentはなく、既存の時計設計を保った責務・設定所有の整理として現文書に記録し、過去ADRは変更しません。

EnemyShotは3つの独立したインスタンスで、発射時count=1・移動ごとに加算し、既存のShield画素判定・Player Trigger・下端消滅を共用します。独立FixedUpdate時計はなく、Controllerのphaseから移動を依頼します。各物理stepの先頭で前stepのMovePosition速度を保持位置へ戻してから、選択された種類の移動先を設定します。Rigidbody2D/GameObject有効状態を正とする点は維持しています。

次Waveで3弾と両表ポインタを初期化、死亡復帰で弾枠だけを整理してポインタと編隊は保持します。初期phase・Wave/死亡時の持越し、爆発待ち省略、ビーム別ISRと0.02秒Physics2Dとの量子化差等は原作との未解決差分です。詳細・Reloadの実境界は [3種類のEnemyShot](ENEMY_SHOTS.md)、限定した時計の設計判断は [ADR-0009](adr/0009-enemy-shot-phases.md) を参照してください。Rolling/Plunger導入当時の記録は [Rolling](ROLLING_ENEMY_SHOT.md)・[Plunger](PLUNGER_ENEMY_SHOT.md) に残します。

### UFO（Issue #79）

Controllerの共有phase 2・Formationの下降条件・8体以上の開始条件は維持します。Ufoは出現カウンタ・待機状態・発進・移動・被弾・解除、Playerは通常弾終了履歴、Driverは得点表と加点を所有します。Issue #103では発進方向の判定もFormationへ移しました。Driverは初期化時にPlayer.GetNormalShotEndCountをFunc<long>として渡し、Formationは発進時にその最新値の偶奇を評価します。回数を複製せず、IPositionProviderはPositionのみの契約を維持します。Issue #105でUfo.Defeated・Formationの得点中継・DriverのB弾専用通知・Player.OwnsShotを撤去しました。B弾は所有者DebugCarpetBombが終了結果を共通得点処理へ渡し、Playerの現在履歴でUFO得点を算出します。Issue #110でScore取得もFunc<int>へ変更し、FormationはGameplayDriver型を参照しません。Driverが既存Score propertyを読む関数を渡し、Formationは初期化時にnull検証します。既存の弾所有者による結果通知原則の適用であり、新規ADRは追加しません。過去の通知判断は [ADR-0014](adr/0014-player-shot-results-and-scoring.md)、現行の詳細は [UFO](UFO.md) を参照してください。

旧モデル・旧Push専用のEditor検証と旧設定ツールは削除しました。代わりに接続・物理設定・Missing Script・Play中の55体と生存集計を確認する `Verify Component Gameplay Setup`、Editor限定の一時停止/再開メニュー、READMEの最小限の操作確認を用意しています。接続チェックは衝突やWave進行の成功を意味しません。

Scene/Prefab移行・Unityコンパイル・Play動作の実行状況はPR本文を参照してください。独立PoC [Physics2DProbe](../Invader/Assets/Prototypes/Physics2DProbe/README.md) は今回変更せず、本編から参照しません。

### 生存数連動の編隊移動（Issue #139）

[ROM解説](https://computerarcheology.com/Arcade/SpaceInvaders/)の生存個体の走査周期と[注釈付きROM](https://computerarcheology.com/Arcade/SpaceInvaders/Code.html)のRackBump（0x1597）・右移動量選択（0x18F1）に基づく。個体を順次描画する原作を編隊一括移動で近似し、正規化した進捗をFormation内部で所有する。50Hz物理更新では1体時に通常1～2回、F中には約12回の移動を同じFixedUpdateで実行し、各回の境界clamp・侵食・アニメ・侵攻通知を維持する。停止通知で残りの移動を中断する。新ゲーム・Waveは進捗0・右2pxへ復元する。既存責務内の局所変更であり、新規ADRは追加しない。アーケードアーカイブス2版動画との実速度・描画順・反転frameの厳密比較は未実施。手動確認はREADMEを参照。

### 生存個体の左右境界（Issue #141）

Formationの各MoveFormationで、`gameObject.activeSelf`がtrueの個体の`transform.localPosition.x`の最小・最大を最大55件走査する。セル半幅8pxを考慮した画面内の個体中心範囲からローカル端を差し引き、親Rigidbody2D.positionのclamp範囲と反転位置を求める。生存0件なら移動処理を返す。全55体では従来の範囲と一致し、外側の列の全滅に応じて移動範囲が広がる。親原点が画面外へ出ても生存セルが画面内なら許容する。

生死・位置の正、集団移動の責務、撃破通知、初期化・停止・復帰フローは変更しない。境界キャッシュ・追加イベント・個体の新APIは設けず、描画/Colliderのenabledや物理同期を要するboundsには依存しない。撃破個体は破棄せず再利用する既存契約を維持し、不正な個体破棄を生存扱いの変更で隠蔽しない。Issue #139の移動進捗・加速・左端反転時の右3px選択と、各移動の侵食・アニメ・侵攻通知を維持する。局所的な境界計算変更のため新規ADRは追加しない。

これは生存ローカル座標とセル幅16pxによる近似であり、原作のVRAM走査・Spriteの画素形状による端判定は未再現。参考動画による厳密な端位置の照合は未実施。手動確認手順と期待結果はREADMEを参照する。

## 弾の爆発待機（Issue #143）

弾枠の占有と命中結果の確定を分離します。FlightEndedは命中時の即時結果通知、BecameReusableはPlayerShotの自然な爆発完了・再利用可能通知です。Playerは前者で通常弾履歴とShotResolved、後者で射撃枠だけを更新し、DriverやB弾の得点経路は変えません。占有は引き続きGameObject.activeSelf、飛行と爆発の区別は弾自身の残量を正とします。外部へComponent参照を公開しません。EnemyShotが相殺元として使うCanHitは、停止・爆発中のPlayerShotを拒否する値だけの判定です。

PlayerShotは16/60秒を既存FixedUpdateで進め（0.02秒では開始後14回）、EnemyShotは既存Controllerの自身のphaseで4回進めます。Invader命中の別16カウントも弾側の待機へ近似し、対象の生死・生存集計は即時のままです。両弾とも待機中はsimulated=falseで移動・再命中・Shield破壊を止め、既存Rendererの黄色と縮尺による簡易表示だけを行います。EnemyShotのStepCountは完了まで加算せず保持し、ResetFlightで0へ戻します。ReloadとSquiggly/UFO共有枠は既存Controllerで維持します。

Pause/Disableは残量を保持して復帰後に再開し、ResetFlightは残量・色・縮尺を即時かつ無通知で復元します。死亡復帰・新ゲームは通常弾と敵弾を回収、次Waveは敵弾を回収し通常弾の占有・爆発残量を保持、B弾は既存の非Playing回収を維持します。通常弾の待機中の押下は予約しません。根拠・初回更新・時間誤差・未検証事項は[敵弾資料](ENEMY_SHOTS.md#爆発待機と通知issue-143)、通知と占有の判断は[ADR-0015](adr/0015-shot-explosion-occupancy.md)を参照してください。

## インベーダー・UFO撃破表示

`EnemyDefeatEffects`はArcade直下の表示専用Componentです。Formationは個体生成時に`Track`で各Invaderを一度登録し、演出が既存`Invader.Defeated`を購読します。管理下の個体を外部へ公開せず、表示用途に限定した登録接続を採用します。Formationの生存数更新・進行責務は変更しません。Ufoは受理済み命中でInactive化した後、保存した撃破位置を`Defeated(Vector3)`で通知します。自然退出・ResetFlight・ResetCycleでは通知しません。

演出は撃破時のワールド位置をScene直下の56個のSpriteRendererへ記録し再利用します。個体やFormationのTransform移動・非アクティブ化に追従せず、WaveClearやPlayerDeadでも表示寿命まで継続します。得点・Lives・Wave・再出現カウンタ・共有phaseは所有しません。56枠は55体とUFOの同時表示に対応し、全枠使用中は追加の表示だけを警告して省略します。毎フレーム検索・撃破時Instantiateはありません。

待機はUniTask.DelayのDeltaTime、Update PlayerLoopを使用し、暫定0.25秒です。TimeScale=0では残時間を保持し、Editor PauseではPlayerLoop停止により進行しません。Componentが所有するCancellationTokenSourceをGameStartingとOnDisableでキャンセル・破棄し、全表示を消去します。Scene破棄もOnDisableで処理します。キャンセル済み待機は再利用された枠を操作せず、通常完了と障害はForget(Debug.LogException)で観測します。GameStarting後は新しいCTSで再利用し、Disable中は通知を購読しません。

`Configure Enemy Defeat Effects`がEditor APIで自作の暫定Sprite 2枚と56枠・参照を生成保存します。InspectorのSprite・色・表示時間で調整でき、既存設定への再実行では画像と調整値を維持します。Notion「原作仕様・調査」は確認済みですが、動画との直接照合は未実施で、模様・色・時間の原作一致は主張しません。既存の表示責務と非同期寿命の原則の局所適用のため、新規ADRは追加しません。

## UFO獲得得点の表示

`GameplayDriver.UfoScoreAwarded(int)`は、既存の`NotifyShotResolved(Ufo)`が進行状態のガードを通り、加点・追加残機処理を終えた時点で、実際のScore差分を通知します。UFO得点表と通常弾終了履歴の参照はDriverだけが行い、既存の加点条件・履歴更新・Wave判定を維持します。

`UfoScoreDisplay`は既存Gameplay HUDのWorld Space Canvas内に保存した単一Textを再利用します。`Ufo.Defeated(Vector3)`で位置とフレーム番号を一時保存し、そのフレームの`UfoScoreAwarded`で位置を一度だけ消費して実獲得点を表示します。現在の通常弾は`TryReceiveHit → Ufo.Defeated → FlightEnded → Playerの終了履歴更新 → ShotResolved → Driver加点`、B弾は`FlightEnded → DebugCarpetBomb → Driver加点`まで同一呼出し内で同期実行される契約を利用します。加点が拒否されれば表示せず、一時位置はLateUpdateで破棄します。自然退出は撃破通知を発行せず、古い位置や位置のない加点通知から表示を作りません。将来通知を非同期化する場合は、この同一フレームの対応付けを再検討します。

Componentは表示位置・待機の寿命だけを所有し、Score・UFOサイクル・撃破演出を操作しません。文字は撃破位置を中心に、既存Canvasの描画順と黒いOutlineで赤い爆発に重ねて読み取れる暫定設定です。表示時間（暫定0.8秒）はComponent、フォント・サイズ・色・縁取りはText/Outlineで調整できます。原作資料はNotion「原作仕様・調査」を確認していますが、動画との直接照合は未実施で、表示時間・字体・描画の原作一致は主張しません。

待機はComponent所有のCancellationTokenSourceとUniTask.NextFrame（Update）を使い、Driverが有効なフレームだけTime.deltaTimeを減算します。TimeScale=0・Driver Disableによる進行停止では時間を保持し、Editor PauseではPlayerLoop停止により待機します。WaveClear・PlayerDead中はDriverが有効なら進行します。再表示・GameStarting・自身のOnDisableでキャンセル・破棄・全消去し、Scene破棄もOnDisableで終了します。キャンセル済み処理は新しい表示を操作せず、非同期の障害はForget(Debug.LogException)で記録します。OnEnable/OnDisableで購読・解除し、生成・Scene検索を実行時に行いません。

`Configure UFO Score Display`で既存Canvasへの追加と参照保存をEditor APIで行います。再実行では表示時間・文字設定を維持し、参照と初期非表示状態を設定します。既存の表示責務・イベントと待機寿命の設計原則を局所適用するため、新規ADRは追加しません。

## ゲーム効果音

`GameplayAudio`はArcade直下の音声専用Componentです。Driver・Player・Ufoと6個の用途別AudioSourceをScene参照で保持し、Score・命中・State遷移・UFOサイクルを操作しません。Formationは生成時にInvaderを一度Track登録するだけで、音のタイミングは管理しません。登録個体の既存Defeatedを音声側が購読・解除し、公開Getterや毎フレームのScene検索は追加しません。Playerには通常弾のTryLaunch成功・射撃枠確定後のShotLaunchedだけを追加します。B弾はPlayerを経由しないため発射音を出しません。死亡はPlayer.HitAccepted、UFO撃破はUfo.Defeatedをそのまま利用します。各個体の既存重複命中拒否と一度の購読を利用し、音声側で生死を二重管理しません。

AudioSourceは移動・通常弾発射・Invader撃破・Player死亡・UFO飛行・UFO撃破の6個で、実行時生成はありません。単発音はPlayOneShotで重ねて再生し、別の発射・撃破で前の単発音を置き換えません。継続音の停止は移動・UFO飛行用Sourceだけに限定し、WaveClear・PlayerDeadでも短い単発音は完了まで再生します。GameOver・Attract・新ゲーム開始・音声ComponentのDisable（Scene破棄を含む）では全音と移動待機を停止します。GameOver/AttractはUpdateで検知、GameStarting/OnDisableは同期的に停止します。

移動音は4つの自作電子音を暫定0.35ゲーム秒周期で巡回します。Component所有のCTSとUniTask.Delay（DeltaTime、Update）で待機し、Formationの移動ステップ・生存数には依存しません。Playing以外・Driver無効・TimeScale=0では継続音と待機を停止し、復帰時は新しい周期で次のパターン音から再開します。新ゲーム・Disableではパターン先頭へ戻します。CTSは停止時にキャンセル・破棄し、古い待機は以降の音を再生しません。障害はForget経由で記録して音声Componentのみを停止します。

UFO飛行音は保存済みUFOのInFlight/isActiveAndEnabledとPlayingをUpdateで確認して単一Sourceをループ再生します。退出・UFO無効化・ゲーム停止は次のUpdateで停止し、撃破はDefeatedハンドラー内で即停止して別Sourceの撃破音を再生します。UFOへの新イベントや再出現・Squiggly共有処理の変更は不要です。TimeScale=0またはDriver Disableでは単発音をPauseし、復帰時にUnPauseします。Unity Editor Pause時の実音停止・聴感は未確認で、手動検証項目に残します。

`Configure Gameplay Audio`はEditor APIと解析的なオシレーター・固定シードのノイズから9個の自作WAV（4移動音＋5効果音）を生成し、モノラルPCMのAudioClipとして保存・参照します。既存Arcadeに音声ルートと6Source・参照だけを追加し、既存の単一AudioListenerを確認します。原作素材・外部音声・新Package・AudioMixerは利用しません。素材生成はランタイムでは実行しません。各SourceのClip・Volume・Pitch、ComponentのMovement Pattern/Intervalを調整できます。ツール再実行では既存WAV・Clip選択・音量・ピッチ・非空のパターン・周期を維持します。

Notion「原作仕様・調査」の、編隊実移動とFleet移動音が別の仕組みである記録を参照しました。生存数別の正確な移動音テンポは今回採用せず、全音色・周期・バランスは暫定です。動画との直接照合や聴感確認を行うまでは原作一致・聴感確認済みと扱いません。既存の演出責務・イベント・非同期寿命の原則を局所適用するため、新規ADRは追加しません。
