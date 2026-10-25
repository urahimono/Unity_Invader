# Lives・Player復帰・GameOver（Issue #63）

## 根拠と範囲

[Issue #63](https://github.com/urahimono/Unity_Invader/issues/63)、[原作仕様・調査](https://app.notion.com/p/3ff4f337d5bf81229a72dbcfd995f070)の実仕様確認03/04に従います。参考設定の初期3機は操作中の1機を含みます。残機があればPlayerReadyを挟まずPlayingへ直接復帰し、現在のFormation・Score・Waveを保持します。死亡待機0.5秒はROM解析による初期候補の暫定値で、死亡演出や参考動画との厳密な尺合わせは含みません。

1500点の追加1機、Lives HUD、死亡アニメ/音、無敵時間、Attractへの復帰・新規開始/リトライ、侵攻による敗北は対象外です。

## 責務と状態

- DriverのSerializeField `_lives` が唯一のLives管理値です。受理したPlaying中の死亡通知で1だけ減らし、同じ死亡の後続通知はStateで拒否します。3→2→1→0と減り、0以下では消費しません。
- PlayerDead中は従来どおり入力・射撃・編隊・弾・表示とPlayer Colliderを停止します。Driverが暫定0.5秒を待ち、残機があれば復帰を依頼してPlayingへ直接戻します。0機ならGameOverへ移り、停止したままにします。
- Player自身のRespawnが受理ガードを解除し、論理(112,24)＝Unity(0,-1.04)へ戻します。旧入力と発射予約を破棄し、発射ゲートを閉じます。Playingで一度射撃キーを離し、再押下するまで撃ちません。Collider/Rendererの再有効化は従来のSetPlaying経路で行います。
- 復帰ではBeginWaveを呼びません。編隊の現在位置・方向・生存個体・アニメ位相、Score・Wave番号を保持します。従来の停止方針どおり、移動/発射タイマーの端数は破棄し、停止時間を取り戻しません。LivesはWaveClear→次Waveでも変えません。

## 弾の扱い（今回の暫定仕様）

死亡待機中、自機弾はその場で非表示・物理停止し、復帰時にPlayerがResetFlightで消費します。旧弾の占有・飛翔や発射予約を次の機体へ引き継ぎません。

現在の敵弾は単一Rollingスロットで、命中成立時に消費されます。さらに復帰前にFormationがそのスロットと発射タイマーを解除します。Playing再開から通常の暫定2秒周期を待って発射を試み、旧弾の残留による直後の再被弾を防ぎます。新たな無敵時間や複数弾種の整理規則は追加していません。EnemyShotの旧ResetForWaveは、同じ解除を復帰でも使用するためResetFlightという操作名へ変更しました。

GameOverでは復帰しないため、最終機の停止した自機弾は非表示・物理停止した状態のまま保持されます。新規ゲーム開始時の初期化はこのIssueの対象外です。

## 保存と操作

ArcadeのDriverに初期Lives=3をEditor APIで保存済みです。既存の **Unity Invader > Configure Player Hit** はPlayer Collider/Driver参照に加え、初期Lives=3も設定します。全体移行メニューも同じ初期値を設定します。Sceneの構成は手書きせず、Unity生成の追加行の末尾空白だけを正規化しました。既存Prefabの内容とGUIDは変わらず、不要なPrefab/metaの差分は追加していません。

通常は保存済みBootstrapを開いてPlayします。左右矢印で移動、Spaceで射撃し、Driver InspectorのLives・Stateを観測できます。1回目/2回目の被弾は暫定0.5秒後に復帰、3回目はGameOverで停止します。LivesやGameOverの画面表示は追加していません。

## 検証状況

2026-10-25に隔離コピーのUnity 6000.6.4f1非表示バッチで、以下を確認しました。終了コード0、成功ログは `Logs/issue63-unity-final-check.log` です。

- 初期Lives=3。3回の実Trigger命中で2→1→0へ減り、重複被弾/通知で余分に減らない。
- 2回の復帰は暫定待機を経てPlayingへ直接戻り、初期位置・表示・Colliderが復帰。編隊の位置・方向・生存個体・アニメとScore/Waveを保持。
- Unity内の仮想Keyboardで、復帰後の右移動、Space押しっぱなし時の発射抑止、release後の再押下での発射を確認。旧自機弾の占有と旧敵弾は解除。
- 残機2のままWaveClear→Wave3へ進み、Score=1980（1500点越え）でも追加残機なし。
- 最終機はGameOverとなり、2.2秒間追加消費・自動復帰・進行なし。
- 既存の通常/終盤敵弾速度・下端消滅・空列抑制、PlayerShot実接触・Score・次Wave個体再利用も成功。
- Unityコンパイル、静的C#コンパイル、規約/GUID確認、git diff --checkが成功。

最初のバッチは仮想入力をEditor更新で処理してPlayerに届かず、入力確認で中断しました。検証内だけでInputSettingsをメモリ上に複製し、フォーカスによらず通常のPlayerLoopで仮想入力を処理するよう修正して再実行し、成功しました。追跡中の入力設定・ProjectSettingsやWindows入力は変更していません。既存Editorも操作していません。中断ログを含む検証ログはリポジトリ直下のLogs/に保持しています。

描画の目視・実キーボード操作・全FPS条件・死亡演出の厳密な待機尺は未確認として区別します。
