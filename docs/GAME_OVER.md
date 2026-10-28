# Invader侵攻によるGameOver

[Issue #81](https://github.com/urahimono/Unity_Invader/issues/81)の実装です。通常被弾のLives減少・PlayerDead・復帰は [LIVES.md](LIVES.md) を参照してください。

## 原作の根拠と暫定座標

[Computer ArcheologyのROM解析](https://computerarcheology.com/Arcade/SpaceInvaders/Code.html)では、0x0141〜0179で生存Invaderを選び、座標計算後の0x016A〜016Dで下位バイトを0x28（40）と比較し、40未満なら0x1971へ分岐します。0x1971〜1976はinvaded=1を設定して終了処理へ進み、0x16E6〜170BではPlayer死亡扱い・残り機数表示0となります。PlayerやShieldとのCollider接触は侵攻の条件ではありません。

原作の値は回転した画面の描画座標です。現Unityは224×256論理画面のSprite中心（現Prefabアンカー）を使い、最も下にいる生存Invaderの論理yが40未満で敗北とする暫定対応です。Unity単位では `(40 - 128) * 0.01 = -0.88`。40ちょうどでは発動しません。反復下降の浮動小数誤差だけを吸収するため比較に0.000001 Unity単位（0.0001論理px）の許容差を使います。中心とスプライト端の差、原作動画での見た目は未照合で、厳密一致ではありません。

## 実装経路

- InvaderFormationの既存2px横移動・端での8px下降・Shield侵食・アニメを実行後、移動先のRigidbody2D位置と子Invaderのローカル位置を使います。子のworld Transform同期の遅延に依存しません。
- activeSelfの生存個体のみが対象です。撃破済みの最下行は判定せず、生存0なら侵攻通知せず既存のWaveClearへ進みます。毎フレームのScene検索、新しいTrigger、位置補正は追加しません。
- Issue #107でFormationからDriverへの直接呼び出しをInvadedイベントへ変更しました。DriverがInitializeGameplayで一度だけNotifyInvadedを購読し、OnDestroyで解除します。停止・再開・死亡復帰・Wave変更では購読を増やしません。通知は同期的で、判定条件と処理順は維持します。
- Driver.NotifyInvadedは初期化済み・有効・Playing中だけ受理し、Lives=0、GameOverへ直接遷移します。通常NotifyPlayerHitと0.5秒の復帰待機は使いません。停止でFormationのPlayingがfalseになるため通知後の繰り返し移動・敵弾/UFO更新も止まります。Fの10倍速でも各ステップで判定します。
- 既存ApplyPlayingStateで全体を停止し、Score/Wave・破壊済Shield・撃破済Invader・通常弾履歴を保持します。非Playingでの追加被弾・撃破・侵攻通知は受け付けません。高度なイベント優先順位は追加せず、先に受理された既存State遷移に従います。

FormationのGameplayDriver参照と初期化引数は敵弾Reload用のScore取得に残っています。今回撤去したのは侵攻時の直接呼び出しであり、Driver依存の全面解消ではありません。

## 対象外・未確認

原作の死亡演出・GAME OVER文言・音・待ち時間・Attract復帰、追加残機、再スタートは未実装です。今回は開発用オーバーレイで即時GameOver/Lives=0を確認します。実画面・実キー操作・原作動画との照合・製品ビルド実行は未確認です。限定検証結果はPR本文に記載します。
