# Shield（Issue #69 / #71）

## 仕様の根拠と暫定値

[Issue #69](https://github.com/urahimono/Unity_Invader/issues/69)、[原作仕様・調査の実仕様確認03](https://app.notion.com/p/3ff4f337d5bf81229a72dbcfd995f070)、[ROM逆アセンブル資料](https://computerarcheology.com/Arcade/SpaceInvaders/Code.html)を参照しています。4基、画素型の局所破壊、Player死亡時に維持・次Wave開始時に復元する扱いを採用しています。

参考動画本体は未提供で、ユーザーの指定により外形・配置を暫定として実装しました。22×16論理pxの外形はコードで新規作成したもので、ROM画像の抽出・コピーではありません。上端の角を斜めに落とし、下中央に6pxの高さの切り欠きを置いています。

| 項目 | 暫定値 |
| --- | --- |
| 左下原点の配置 | X=32 / 78 / 124 / 170、Y=48論理px |
| 寸法・色 | 幅22×高さ16、緑、1論理px=0.01 Unity単位 |
| PlayerShotの削れ | 命中画素の1px後方から進行方向へ、各行の半幅1 / 2 / 3 / 2 / 2 / 1px |
| EnemyShotの削れ | 命中画素の1px後方から進行方向へ、各行の半幅2 / 3 / 3 / 2 / 2 / 1 / 1px |

爆発マスクは自作の即時消去です。原作の厳密な形状・配置・色・SE・爆発表示時間を再現したとは扱いません。

## Invaderによる侵食（Issue #71）

原作のSprite描画による画面バイトの上書きに対し、今回は生存InvaderのCollider矩形（現状約16×8論理px）全面を侵食範囲の近似に使います。Spriteの透明画素まで削る可能性があり、不透明画素の輪郭との厳密一致ではありません。既存Invader画像のImporter・読み取り設定は変更しません。

Formationの各移動ステップで、移動前のInvader Colliderと移動量をShieldへ渡します。Shieldは前後のColliderを含むswept boundsで既存Triggerを検索し、重なる残存画素だけを消去します。2pxの横移動、8pxの下降、同一FixedUpdate内の複数ステップをそれぞれ処理し、移動間の画素を残しません。端で横移動と下降を同時に行う場合も、前後を含む矩形による近似です。

侵食依頼はFormationの移動時のみで、毎フレームの全シーン検索や新しいScene参照はありません。無効・撃破済み・Collider無効のInvaderは対象外です。空の画素を再び変更せず、実際に残存画素を消した場合だけ既存LateUpdateの描画更新を要求します。

Editor / Development BuildではF長押しで編隊の移動テンポだけを10倍にでき、侵食の確認に使えます。各移動ステップの侵食を省略せず、B絨毯爆撃とも併用できます。原作仕様ではない開発用操作です。

非Playingでは編隊移動・Invader Collider・Shieldの受付が停止するため侵食しません。Player死亡・復帰では侵食状態を保持し、次Waveでは弾による破壊とともに4基を復元します。侵食でInvaderの生死・移動・Score・Livesを変更しません。侵攻によるGameOverやPlayerとの接触は未実装です。

## 責務と衝突

- 各Shieldの`bool[] _solid`だけが現在の残存画素の正です。HPはありません。初期画像は復元用テンプレート、Textureへの出力配列は残存画素から作る描画用バッファです。4基でTexture・Sprite・破壊状態を共有しません。
- SpriteはPoint / FullRect表示です。破壊したフレームのLateUpdateで描画用Textureを更新し、複数の物理stepや絨毯爆撃による同フレームの命中をまとめます。毎フレーム無条件では更新しません。生成したSprite・Textureは破棄時に解放します。
- 各Shieldには固定のBoxCollider2D Triggerが1個だけあります。Colliderは領域候補の検出用であり、透明部分で弾を消費しません。画素ごとのColliderやダメージ後のCollider再生成はありません。
- 自弾はFixedUpdate、敵弾はFormationの種類別60Hz位相で、現在〜移動予定の矩形中心・寸法・方向・命中元をHitInfoで渡します。ProjectileHitDetectorがOverlapBoxでISweptHitReceiver候補を検索し、副作用なしの画素距離照会で最短の1件だけを選びます。ShieldのIHitReceiver.TryReceiveHitが同じ区間の残存画素を再確認し、既存マスクで破壊します。穴はfalseを返して通過し、区間途中の1pxも判定します。TriggerからISweptHitReceiverへの転送は除外するため二重破壊しません。
- 弾はShield具体型に依存しません。受理後の自弾はInactive化してFlightEndedを1回通知し、敵弾はResetFlightで回収します。通常対象へのTriggerと移動区間対象との全体的な優先順位は統合せず、従来の呼出タイミングを保ちます。
- 現在の弾は鉛直移動、Shieldは回転なし・unit scale・左下原点です。この構成専用の判定であり、任意回転や斜め弾用の汎用衝突基盤ではありません。
- 命中が成立したときだけ既存`ResetFlight`で弾を消費します。PlayerShotのRigidbody2D速度、EnemyShotの4/5px下降とMovePosition、Invader撃破・ScoreやPlayer被弾のTrigger経路は維持します。Bの専用弾も同じPlayerShotを使います。
- DriverがPlaying可否をShieldへ伝えます。停止StateではCollider・破壊受付を止め、残存画素と表示を保持します。復帰では初期化せず、WaveClearの待機完了後、Playingへ戻す前に4基の`BeginWave`を呼びます。

## 保存と確認

`Assets/Prefabs/Gameplay/Shield.prefab`の4 instanceとDriverの参照をArcadeに保存します。初期画像は`Assets/Sprites/Shield/Shield.png`。再設定は **Unity Invader > Configure Shields** です。Editor APIで生成し、既存Scene / PrefabのGUIDを維持します。

実施した限定検証と未実施の確認はPR本文を参照してください。実画面の見た目・実キー操作・原作動画との照合、Invader侵入時の見え方、描画FPSごとの実測は別途確認が必要です。
