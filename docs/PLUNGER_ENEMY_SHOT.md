# Plunger EnemyShot（Issue #75）

Issue #77でSquigglyを追加し、以下に記録した暫定2秒周期を3種類共通のphase・step count・Score依存Reloadへ置き換えました。現在の仕様と原作との差は [3種類のEnemyShot](ENEMY_SHOTS.md) を参照してください。以下はIssue #75当時の実装・検証記録です。

## 発射元と既存経路

[Issue #75](https://github.com/urahimono/Unity_Invader/issues/75) と [ROM解析のColFireTable（0x1D00）](https://computerarcheology.com/Arcade/SpaceInvaders/Code.html) を根拠に、左から1始まりの列順を使います。

`1, 7, 1, 1, 1, 4, 11, 1, 6, 3, 1, 1, 11, 9, 2, 8`

Formationが0始まりへ変換し、指定列の最下段生存Invaderを探します。16個で循環し、空列でもポインタを進めて不発とします。代替列は探しません。飛行中・残り1体以下・非Playingでは新規発射せず、列ポインタも進めません。既に飛行中のPlungerは残り1体になっても継続します。RollingのPlayer対応列選択は維持します。

RollingとPlungerはScene内の別々のEnemyShotで、位置・飛行中の占有・衝突・再利用を独立管理します。EnemyShotの既存下降（3/60秒ごとに4px、残り8体以下で5px）・Shield画素破壊/穴通過・Player Trigger被弾・画面下端消滅をそのまま使用します。新しい弾Componentや衝突処理の複製はありません。

## 暫定制御と未実装

- Formationの単一時計でRollingを2秒ごとに試行し、その中間でPlungerを試行します。Playing開始・復帰後の初回はRollingが2秒、Plungerが3秒です。以後も各種2秒周期で、飛行中の試行は予約せず捨てます。既存Inspectorの`Rolling Fire Interval`を両種の暫定周期として使用します。
- Fデバッグは移動時計だけを10倍にし、この共通時計と両敵弾の下降速度は変えません。
- 原作の3種類のshot step数・refresh別処理順・Score依存reloadは未実装です。Squiggly追加時にこの小さな試行制御を置き換える予定であり、原作の発射同期・頻度を再現したものではありません。
- Plungerは既存の生成済み白Spriteを水色にした3×8px矩形です。Rollingの橙色と区別する暫定描画で、原作の4コマ形状・爆発・SEは未実装です。抽出素材は追加していません。
- 列ポインタはWave開始で0へ戻し、Player死亡・復帰では保持します。Waveまたぎの原作挙動は未確定のため、この扱いは暫定です。

非Playingでは両弾の表示・物理・発射を停止して位置と占有を保持します。Player復帰時は両方の残留弾と発射時計を解除し、編隊の位置・方向・生存個体・Score・Wave・Shield破壊状態を保持します。次Waveでは両弾と発射時計、列ポインタを初期化します。LivesやShield復元の既存責務は変更していません。

## アセットと確認

保存済みBootstrapから起動できます。`Assets/Prefabs/Gameplay/PlungerEnemyShot.prefab`とArcadeのFormation参照はUnity Editor APIで保存します。再設定は **Unity Invader > Configure Plunger EnemyShot**、全体再生成では既存の移行メニューからも設定します。既存Prefabを上書きせず、再実行時はSceneの同じ弾を再利用します。

Unity 6000.6.4f1の隔離コピー・非表示バッチでコンパイルと限定Playが成功しました。16列と循環・空列・最下段生存個体・飛行中/残り1体でのポインタ保持、自然発射の同時飛行、両種の4/5px下降、F中の共通時計、停止Stateと下端消滅を確認しました。PlungerでShieldを繰り返し削って穴を通過し、実Physics2D TriggerでPlayer被弾・Lives=2・復帰時の両弾解除を確認しました。仮想B+F入力で既存PlayerShotの接触によるScore=990・Wave=2とShield復元・両弾/ポインタ初期化・Plunger再利用も確認しました。検証コードとログはGit対象外の`Logs/`に保存しています。

実画面の目視、実キー操作、原作動画との照合、描画FPS別の測定、Development Build/製品ビルドの実行は未確認です。詳しい検証範囲はPR本文を参照してください。
