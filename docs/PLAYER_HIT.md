# Player被弾・PlayerDead（Issue #61）

以下はIssue #61時点の実装・検証記録です。現在はIssue #63でLives・復帰・GameOverを追加しており、PlayerDeadで停止し続ける挙動から変更しています。現在の仕様は [Lives・復帰](LIVES.md) を参照してください。

## 作業状態

PR #60マージ済みのdevelop（7fa5425）を基準に実装しました。2026-10-25に今回の実行承認を受け、隔離プロジェクトコピーのUnity 6000.6.4f1非表示バッチでPlayer Prefab・Arcadeを保存し、コンパイルと実Trigger確認に成功しました。既存Editor・GUI・Windows入力は操作していません。

## 今回の仕様

- EnemyShot自身のOnTriggerEnter2DがPlayerへTryReceiveHitを依頼します。PlayerShot、Invader、Shieldへの接触ルールは追加しません。
- PlayerがPlaying・有効状態・Collider有効・未受理を確認し、受理ガードを立てて自身の入力・表示・Collider・自機弾を停止し、Driverへ通知します。DriverはPlaying中だけPlayerDeadへ遷移し、既存のSetPlaying経路でFormationと敵弾も停止します。
- 成立したEnemyShotは直ちに非アクティブ化して消費します。弾の処理中ガードとPlayerの受理ガードで、同一接触・後続接触を二重受理しません。
- PlayerDeadからの自動復帰、Lives、GameOver、演出、無敵時間は実装しません。Playerの受理ガードはこのプレイ中に解除しません。
- 命中しない敵弾の下降・下端消滅・再利用、残数8以下の速度切替、既存のPlayer移動・自機弾・Score・Waveの計算は維持します。死亡とWaveClearが同じ物理stepに発生した場合も、既存の非Playing拒否に従い、通知順序に独自の優先規則は加えません。

## ColliderとScene参照

Playerは既存のTransform移動とSprite表示を維持し、ルートにTrigger BoxCollider2Dを追加します。新規Colliderのsize/offsetは既存Sprite.bounds.size/centerから設定し、既存ルートの0.01倍率をそのまま使います。保存値はローカルsize=(16,16)、offset=(0,0)、ワールドでは0.16×0.16 Unity単位＝16×16論理pxです。現Player表示の矩形全体を被弾対象とする暫定設定であり、原作のpixel単位の被弾形状の確定値とは扱いません。Spriteやimport設定は変更しません。

PlayerにはRigidbody2Dを追加しません。EnemyShotの既存Kinematic Rigidbody2DとTriggerを利用します。TriggerはStatic/Kinematic間でも通知されるため、物理応答やPlayerの移動方式を変える必要はありません（[Unity RigidbodyType2D.Static](https://docs.unity3d.com/ja/current/ScriptReference/RigidbodyType2D.Static.html)）。この組合せで実Trigger命中を確認済みです。

新規Player ColliderはPrefabでは無効に保存し、Playing中のみPlayerが有効化します。Attract・WaveClear・PlayerDeadやPlayer単体のDisableでは無効です。Player→GameplayDriverの通知用参照をArcadeのPrefab instance overrideで設定します。Driverや管理Componentを返すGetterは追加しません。

## 保存・起動方法

通常は保存済みScene/Prefabを使用し、生成操作は不要です。`UnityInvader.Editor.PlayerHitSetup.Configure` は既存Player PrefabをLoadPrefabContentsで開いてColliderを追加・保存し、ArcadeのDriver参照を設定・保存します。既存GUID・Sprite・Transform・他のPrefab設定を保持し、既存Colliderがあればsize/offset調整を保持します。従来の全体移行メニューにも同じ設定を組み込みました。Scene/Prefabの構成はEditor APIで生成し、追加行の末尾空白だけを正規化しています。

保存後は通常のBootstrapを開いてPlayします。敵弾を避ければ継続し、命中するとDriver InspectorのStateがPlayerDeadとなり、表示・進行が停止します。再設定する場合だけ、Play停止中に **Unity Invader > Configure Player Hit** を使用します。

## 必要最小限の確認

既存の専用バッチ `UnityInvader.Editor.RollingEnemyShotVerification.Run` を拡張し、通常のEditorインポートでは自動実行しません。

1. Prefab Collider・Scene参照、Bootstrap起動、非Playingの被弾拒否を確認。
2. 有効なColliderを持つPlayerを敵弾から離し、既存の速度切替・下端消滅・Score/Wave・PlayerShot実接触を確認。
3. Player上方から敵弾を下向きに進め、実TriggerでPlayerDeadへ遷移し、敵弾が消費されることを確認。
4. 後続の被弾・接触を拒否し、2.2秒間の停止でPlayer・Formation・自機弾の位置/占有保持、追加発射抑止、Score/Wave不変、自動復帰なしを確認。

上記は成功しました。通常下降は0.3秒で24px、残数8体では0.3秒で30px。全滅Score=990・次Waveの同じ55体再利用、自機弾の実接触後のScore=1000/生存54も確認しました。そこからPlayerへの実命中でEnemyShotを消費し、PlayerDeadで2.2秒間Score=1000/Wave=2/生存54と位置・占有を保持し、追加発射・自動復帰がないことを確認しました。成功ログは `Logs/issue61-unity-final-check.log`、Unity終了コードは0です。

初回はRequireComponentによる極小Colliderの先行追加を既存調整済みと誤認したため寸法確認が失敗しました。RequireComponentでの自動追加を外し、Editor APIで明示的に追加・設定する形に修正しました。次の実行では検証側が非シリアライズfieldをSerializedObjectで読もうとしたため中断し、Reflectionへ修正して最終実行が成功しています。中断ログもLogs/に保持しています。

検証用fixtureと観測はEditor側に置き、ランタイムの検証専用APIは追加しません。静的C#コンパイル・規約/GUID確認・git diff --checkも実施しています。描画の目視・実キーボード操作・全FPS条件は未検証です。
