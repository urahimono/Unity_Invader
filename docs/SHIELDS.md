# Shield（Issue #69）

## 仕様の根拠と暫定値

[Issue #69](https://github.com/urahimono/Unity_Invader/issues/69)、[原作仕様・調査の実仕様確認03](https://app.notion.com/p/3ff4f337d5bf81229a72dbcfd995f070)、[ROM逆アセンブル資料](https://computerarcheology.com/Arcade/SpaceInvaders/Code.html)を参照しています。4基、画素型の局所破壊、Player死亡時に維持・次Wave開始時に復元する扱いを採用しています。

参考動画本体は未提供で、ユーザーの指定により外形・配置を暫定として実装しました。22×16論理pxの外形はコードで新規作成したもので、ROM画像の抽出・コピーではありません。上端の角を斜めに落とし、下中央に6pxの高さの切り欠きを置いています。

| 項目 | 暫定値 |
| --- | --- |
| 左下原点の配置 | X=32 / 78 / 124 / 170、Y=48論理px |
| 寸法・色 | 幅22×高さ16、緑、1論理px=0.01 Unity単位 |
| PlayerShotの削れ | 命中画素の1px後方から進行方向へ、各行の半幅1 / 2 / 3 / 2 / 2 / 1px |
| EnemyShotの削れ | 命中画素の1px後方から進行方向へ、各行の半幅2 / 3 / 3 / 2 / 2 / 1 / 1px |

爆発マスクは自作の即時消去です。原作の厳密な形状・配置・色・SE・爆発表示時間を再現したとは扱いません。Invader下降による侵食は今回未実装です。

## 責務と衝突

- 各Shieldの`bool[] _solid`だけが現在の残存画素の正です。HPはありません。初期画像は復元用テンプレート、Textureへの出力配列は残存画素から作る描画用バッファです。4基でTexture・Sprite・破壊状態を共有しません。
- SpriteはPoint / FullRect表示です。破壊したフレームのLateUpdateで描画用Textureを更新し、複数の物理stepや絨毯爆撃による同フレームの命中をまとめます。毎フレーム無条件では更新しません。生成したSprite・Textureは破棄時に解放します。
- 各Shieldには固定のBoxCollider2D Triggerが1個だけあります。Colliderは領域候補の検出用であり、透明部分で弾を消費しません。画素ごとのColliderやダメージ後のCollider再生成はありません。
- 弾自身のFixedUpdateから、現在位置〜次の予定位置に弾の幅・高さを加えた範囲を`Physics2D.OverlapBox`で調べます。Shield内では残存画素を進行方向順に走査し、最初の画素だけを削ります。領域内の穴を進み続ける場合も毎物理step確認し、移動区間の途中にある1pxも対象にします。
- 現在の弾は鉛直移動、Shieldは回転なし・unit scale・左下原点です。この構成専用の判定であり、任意回転や斜め弾用の汎用衝突基盤ではありません。
- 命中が成立したときだけ既存`ResetFlight`で弾を消費します。PlayerShotのRigidbody2D速度、EnemyShotの4/5px下降とMovePosition、Invader撃破・ScoreやPlayer被弾のTrigger経路は維持します。Bの専用弾も同じPlayerShotを使います。
- DriverがPlaying可否をShieldへ伝えます。停止StateではCollider・破壊受付を止め、残存画素と表示を保持します。復帰では初期化せず、WaveClearの待機完了後、Playingへ戻す前に4基の`BeginWave`を呼びます。

## 保存と確認

`Assets/Prefabs/Gameplay/Shield.prefab`の4 instanceとDriverの参照をArcadeに保存します。初期画像は`Assets/Sprites/Shield/Shield.png`。再設定は **Unity Invader > Configure Shields** です。Editor APIで生成し、既存Scene / PrefabのGUIDを維持します。

実施した限定検証と未実施の確認はPR本文を参照してください。実画面の見た目・実キー操作・原作動画との照合、Invader侵入時の見え方、描画FPSごとの実測は別途確認が必要です。
