# 3種類のEnemyShot（Issue #77）

## ROMから採用する規則

[Issue #77](https://github.com/urahimono/Unity_Invader/issues/77)・[実仕様確認02](https://app.notion.com/p/3ff4f337d5bf81229a72dbcfd995f070)と、[Computer Archeologyの逆アセンブル](https://computerarcheology.com/Arcade/SpaceInvaders/Code.html)を参照します。以下は資料の概数コメントだけでなく、記載アドレスの命令・データと照合した規則です。

| 種類 | phase / shotSync | 発射元 |
| --- | --- | --- |
| Rolling | 0 | Player対応列の最下段生存Invader |
| Plunger | 1 | ColFireTable 0x00〜0x0Fの16列 |
| Squiggly | 2 | ColFireTable 0x06〜0x14の15列 |

Plunger列順：`1,7,1,1,1,4,11,1,6,3,1,1,11,9,2,8`。

Squiggly列順：`11,1,6,3,1,1,11,9,2,8,2,11,4,7,10`。

列は左から1始まりで、Formationが0始まりへ変換して指定列を下から検索します。表は循環します。空列でも表のポインタを進めて不発とし、別列へ代替しません（059C〜05A8）。飛行中やReload不成立では表を消費しません。Plungerは残り1体で新規発射を止め、飛行中の弾は終了まで継続します（04F1〜050Bの再初期化後のskip設定に対応）。Rollingは弾枠の再初期化ごとに最初の発射試行だけを見送ります（047D〜0489、04AB〜04B3）。

発射成立時のstep countは**1**（05B7〜05BF）、以後の移動ごとにbyteとして1加算します（05CF〜05D0）。発射判定では他の2種類のcountをそれぞれ確認し、**0は比較を省略、それ以外はReload値を厳密に超えている場合だけ可**とします（0579〜0595）。単純な秒数タイマーではありません。

ScoreはDriverが所有し、初期化時に渡す`Func<int>`（`() => Score`）をFormationが発射試行時に呼んで最新値を取得します（Issue #110）。FormationはDriver型・インスタンスを直接保持せず、Scoreを複製しません。コールバックは初期化時にnull検証します。170E〜1728はScoreの上位BCD byteを`02,10,20,30`と比較し、対応する`30,10,0B,08,07`を選びます。下位2桁を比較しないため、実際の境界は次のとおりです。

| Score（10進） | Reload（16進 / 10進） |
| --- | --- |
| 0〜299 | 0x30 / 48 |
| 300〜1099 | 0x10 / 16 |
| 1100〜2099 | 0x0B / 11 |
| 2100〜3099 | 0x08 / 8 |
| 3100〜9999 | 0x07 / 7 |

200と201、1000と1001等では切り替わりません。Scoreが変更されると以後の発射試行で新しい値を参照します。

## Unityでの制御と近似

Formation内だけに1/60秒の蓄積時計を持ち、種類別phaseで発射試行または飛行中の1stepを依頼します。shotSyncの種類対応とタイマーの下降順を使い、phaseは**0→2→1→0**です。同じ種類の更新は3仮想tickごとになります。暫定2秒周期・1秒位相差のInspector設定は廃止しました。

EnemyShot自身の独立したFixedUpdate時計はなくし、選ばれたphaseから`AdvanceStep`を呼びます。位置の正は引き続きRigidbody2Dです。移動先と既存Shield画素判定・下端消滅・Player Trigger処理はEnemyShotが担当し、Formationへ衝突処理は移していません。通常4px/step、残り8体以下で5px/stepは3種類に適用します。

Physics2Dは既存の0.02秒刻みのままです。1回のFixedUpdateで通常1個、ときに2個の仮想tickを順番に処理します。この設定では同じ種類を同じ物理stepで二度動かしません。各物理stepの先頭で3弾のMovePositionを現在位置へ保持して前stepの速度継続を防ぎ、その後に選択された種類だけの移動先を設定します。2種類が別の仮想tickで動いても、実際のMovePosition/Triggerは同一の物理stepで確定する場合があります。原作のビーム位置別ISR、描画・当たり判定順、約60Hzの厳密な実時間同期は再現していません。

非Playing停止時は位置・占有・step countを保持し、表示・物理・発射を止めます。再開で停止時間を取り戻さず、仮想時計の端数とphaseを初期化します。Player復帰時は3弾とcount・Rolling初回見送りを初期化し、編隊・Score・Wave・Shield状態と列ポインタは保持します。Waveでは3弾と時計に加え両方の列ポインタを先頭へ戻します。初期phase=0と、死亡/Wave時のこれらの扱いは現行進行を維持する近似で、ROMとの厳密な持越し照合は未完了です。

Fデバッグ倍率は従来どおりFormationの移動時計だけへ適用し、敵弾時計・下降速度・Player・待機時間・グローバル時間は変えません。B絨毯爆撃は既存PlayerShot経路を使用します。

## 未解決の差分

- 敵弾の爆発アニメーションと待ち時間は未実装です。ROMでは爆発完了後に弾枠が再初期化されるのに対し、本実装はShield/Player命中・下端到達で即座に弾を解除しcount=0へ戻します。この差は他種のReload成立時刻にも影響します。
- Scoreは既存のintで保持し、9999超でも巻き戻さず最速Reloadを継続します。4桁BCDの桁あふれは再現しません。
- Rolling列選択の範囲外不発・clampなし、発射位置・3×8px矩形判定、既存Shield破壊mask、既存の死亡待機/復帰・Wave進行は従来のUnity側仕様を維持しています。ROMの座標・描画衝突との完全一致ではありません。
- 見た目はRolling橙・Plunger水色・Squiggly紫の暫定矩形です。原作画像を抽出せず、既存の生成済み白Spriteを使用します。
- Issue #79でUFOとのSquiggly処理枠共有を追加しました。phase 2でSquiggly終了を待ち、UFO飛行中はSquigglyの新規発射を止めます。出現条件・加点と近似は [UFO](UFO.md) を参照してください。原作4コマ画像・爆発・SE、弾同士の相殺は未実装です。
- 参考動画との視覚・時間照合、描画FPS別測定、実画面/実キー、Development Build/製品ビルド実行は未確認です。

## 保存と確認

保存済みBootstrapから起動します。Squigglyは独立した`SquigglyEnemyShot.prefab`とScene参照をEditor APIで保存し、再設定は **Unity Invader > Configure Squiggly EnemyShot** です。全体移行メニューでも3種類を設定します。再実行では既存の同じ弾を再利用します。

Unity 6000.6.4f1の隔離コピー・非表示バッチでコンパイル・Scene保存・限定Playが成功しました。15/16列と循環・空列・Reload境界、Rolling初回見送り、実Rigidbody2Dのphase順・非phase停止・4/5px切替、3弾の自然同時飛行を確認しました。SquigglyでShieldを削って開いた穴を通過し、実Physics2D TriggerでPlayer被弾・Lives=2・復帰時の3弾解除も確認しています。

復帰の観測では検証側だけで既存の死亡待機を進め、次の自然発射より前に解除結果を確認しました。B+F検証では追加被弾を避けるためPlayer Colliderだけを一時的に無効化し、既存PlayerShotの実接触によるScore=990・Wave=2・Shield復元を確認しました。製品の待機・当たり判定に検証用の変更はありません。詳しい結果はPR本文、ログ・一時検証コードはGit対象外のリポジトリ直下`Logs/`に保存しています。
