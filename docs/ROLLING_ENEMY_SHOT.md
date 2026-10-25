# Rolling EnemyShot（Issue #59）

## 作業状態

Unity 6000.6.4f1のEditor APIでArcadeの敵弾参照とEnemyShot Prefabを保存しました。Scene・Prefab・対応するmetaをGit管理します。2026-10-25に、承認済みの隔離プロジェクトの非表示バッチでコンパイル・限定Play確認が成功しました。既存EditorやWindowsデスクトップは操作していません。

## 根拠と今回の範囲

[Issue #59](https://github.com/urahimono/Unity_Invader/issues/59) と [原作仕様・調査「実仕様確認02」](https://app.notion.com/p/3ff4f337d5bf81229a72dbcfd995f070) を基準とします。列検索の参考は [ROM逆アセンブルのFindColumn/FindInColumn](https://computerarcheology.com/Arcade/SpaceInvaders/Code.html) です。

- RollingはPlayerに対応する列の最もPlayer側に近い生存個体を選びます。
- 通常は3/60秒ごとに4px、残数8体以下では5px下降します。平均80/100px毎秒に相当します。
- 今回はRollingだけを実装し、Score連動reload、Plunger/Squiggly、Player被弾、弾相殺、Shield等は実装しません。

## 暫定仕様・座標対応

- 発射試行はPlayingの物理時間で**2秒ごと**です。最初の縦切りとして発射と消滅を観察しやすい間隔を採用し、原作reload rateとは扱いません。Inspectorで調整できます。
- Rolling用の弾オブジェクトは1つを再利用します。飛行中の試行は予約せず破棄し、遅延分をまとめて発射しません。
- 対応列は現在の編隊左上の中心Xを基準に16px幅の区間へ対応付けます。中心±8pxがその列で、境界の同距離では右列です。範囲外・指定列全滅なら発射しません。隣列探索・端列へのclamp・ROMのbyte wrapは追加しません。これは現Unity座標への対応で、ROM境界挙動の完全再現や動画照合済みを意味しません。
- 各列は下段から上段へ走査し、GameObjectが有効な個体を発射元とします。個体中心から8px下（既存Invader hitboxの半分4px＋敵弾高さの半分4px）を弾の中心にします。
- 表示・Colliderは**橙色の3×8px矩形**です。既存の生成済み白いSpriteを再利用し、子VisualのSimple描画で寸法を合わせます。原作Rollingの画像・アニメ・判定寸法の確定値ではありません。
- 次Waveでは旧Waveの敵弾を非アクティブ化し、発射/移動の経過時間を初期化します。停止Stateでは弾の位置・占有を保持して表示・物理を停止し、再開時に停止時間を取り戻しません。経過時間の端数は破棄します。

## 責務と更新

FormationはPlayerのTransformだけを参照してXを読み、所有するInvader群から発射元を選びます。Driverの内部Getterや個体配列を外部公開しません。既存DriverのState・Score・Wave処理、Playerの移動と射撃、PlayerShotの接触処理は変更しません。

EnemyShotはKinematic Rigidbody2DとTriggerを持ち、FixedUpdateで0.05秒周期のstepを蓄積してMovePositionへ渡します。既存Fixed Timestepは0.02秒なので、実際のstepの時刻は0.04/0.06秒等へ量子化されます。描画フレーム数では進めず、1stepの下降量を4/5pxで維持します。残数が8以下になったら飛行中の弾も次のstepから高速化します。

弾の上端まで画面下端（Unity y=-1.28）を抜けたら非アクティブ化します。GameObject有効状態を飛行中の正とし、毎回同じTransform/Rigidbody2Dへ発射位置を設定します。PlayerShotの子にはしません。接触時の反応はこの段階では持ちません。

## 起動とEditor API設定

通常は生成処理不要です。保存済みの `Assets/Scenes/Bootstrap.unity` を開いてPlayするとArcadeへ切り替わり、開発用Playing開始フラグにより約2秒後からRollingが発射されます。左右矢印でPlayerを動かし、Spaceで自機弾を発射します。EnemyShotはHierarchy上では待機中は非アクティブ、飛行中は有効になります。PrefabのVisualとBoxCollider2DをInspectorで確認できます。

参照を再設定する場合だけ、Play停止中に **Unity Invader > Configure Rolling EnemyShot** を実行します。

`UnityInvader.Editor.RollingEnemyShotSetup.Configure` は既存Arcadeを開き、`Assets/Prefabs/Gameplay/EnemyShot.prefab` とその `.meta` を生成し、Scene内の敵弾とFormation→Player/EnemyShot参照を保存します。既存Prefabがあれば上書きせず、既存Player・自機弾・編隊を作り直しません。従来の全体移行メニューからも同じ設定処理を呼びます。

Scene/PrefabのYAMLは手書きせず、Editor APIの生成結果を採用しました。Arcadeの既存metaは同一で、EnemyShot Prefabと新規スクリプトにはそれぞれmetaを追加しています。

## 確認手段

専用バッチ入口 `UnityInvader.Editor.RollingEnemyShotVerification.Run` は設定保存後にBootstrapからPlayし、次を確認します。通常のEditorインポートでは実行しません。

- 自動発射と通常4px step、停止中の位置/占有/非表示/物理停止。
- 本物の個体撃破通知で残数を8へ減らし、飛行中の5px stepへの切替。
- 対応列全滅・範囲外で他列に補完しないこと。
- 実際の画面下端での非アクティブ化。
- 全滅のScore=990・WaveClear・次Waveの55体再利用と敵弾解除。
- 次Waveで既存PlayerShotを実際に接触させ、1体撃破と10点加算・自機弾消費。

少数状態のFixture作成はEditor限定のReflection/SerializedObjectで行い、ランタイム検証用APIは増やしません。バッチ内だけで物理Keyboard deviceを無効にし、ユーザーのWindows入力は操作しません。

実行結果：上記の確認は成功しました。通常下降は0.3秒で24px、残数8では0.3秒で30pxでした。PlayerShotの実接触後は生存54・Score=1000となり、自機弾は消費されました。Unity C#コンパイルと静的規約/GUID確認、`git diff --check`も成功しています。

初回のサンドボックス内起動はライセンスサービスへの接続待ちとなり、権限昇格後の初回インポート直後のPlayは追加ドメインリロードで中断しました。確認入口をインポート完了待ちにして再実行し、成功ログ `Logs/issue59-unity-play-check.log` を保存しています。初回中断ログも `Logs/` に保持しています。

非表示・nographicsバッチのため、描画の目視、実キーボードによるPlayer移動、原作の見た目/発射頻度の正確さ、全FPS条件は未検証です。これらを成功扱いにはしません。専用バッチ実行時は `-batchmode -nographics -executeMethod UnityInvader.Editor.RollingEnemyShotVerification.Run` と `-logFile <リポジトリ>/Logs/<ログ名>` を指定します。確認入口が終了するため `-quit` は付けません。
