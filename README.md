# Unity_Invader

Unityを使用してSpace Invadersの挙動を再現する個人開発・学習プロジェクトです。参考プレイ動画を基準に忠実な再現を目指します。

元ゲームから抽出した画像・音声・動画などの素材は含みません。これらの素材をリポジトリへ追加しない方針です。

設計文脈は次のリポジトリ文書で共有します。現在構造の詳細はArchitectureを正本とし、このREADMEには起動・操作・検証手順を記載します。

- [Project principles](docs/PROJECT_PRINCIPLES.md): 長期間維持する設計原則
- [Architecture](docs/ARCHITECTURE.md): 現在の実装構造と未実装の境界
- [Architecture Decision Records](docs/adr/README.md): 重要な設計判断と理由
- [作業ルール](AGENTS.md): 開発手順と設計資料の参照ルール

## 起動方法

- Unity Project Root: `Invader/`
- Unity: `6000.6.4f1`
- 現在の状態: Player左右移動、自機の単一弾、55体の編隊、命中・撃破・Score・次Waveに加え、Rolling敵弾の発射・下降・消滅を実装しています。必要なArcade SceneとPrefabは保存済みです。

Unity Hubから `Invader/` を指定して、上記バージョンのEditorで開いてください。

`Assets/Scenes/Bootstrap.unity` を開いてPlayすると、`Arcade.unity` に切り替わります。

## Windowsでdevelopを更新してUnityを起動する

ルートの `LaunchDevelopUnity.bat` をダブルクリックすると、作業ツリーを確認し、`develop` へ切り替えて `git pull --ff-only origin develop` を実行した後、`Invader/` をUnity Editorで開きます。GitがPATH上で使用でき、originへアクセスできる必要があります。

未コミット変更・未追跡ファイル（Gitignore対象を除く）がある場合、またはGit操作が失敗した場合は原則として起動せず停止します。ただし、**次の3つのUnity ProjectSettingsファイルだけが未ステージ変更になっている場合**に限り、変更を`git stash`へ自動退避してから続行します。

- `Invader/ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json`
- `Invader/ProjectSettings/ProjectAuditorSettings.asset`
- `Invader/ProjectSettings/QualitySettings.asset`

この例外は、ステージ済み変更や他の未コミット・未追跡ファイルが1つでもあれば適用しません。自動退避は変更を破棄せずローカルのstashに保存するため、`git stash list`で確認できます。実際に採用したいUnity設定変更は、起動バッチ実行前に内容を確認し、通常のGit手順でコミットしてください。stashは自動削除しません。

分岐した履歴は自動解消しません。エラー時はコンソールに原因を表示して待機します。正常時は現在のブランチが `develop` になります。

Unityのバージョンは更新後の `Invader/ProjectSettings/ProjectVersion.txt` から読み、標準の `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe` を使用します。カスタム配置では、環境変数 `UNITY_EDITOR_PATH` に対応バージョンのUnity.exeのフルパスを設定してください（値自体に引用符は含めません）。例: `D:\Unity Editors\6000.6.4f1\Editor\Unity.exe`。バッチ冒頭の設定例でも指定できますが、追跡ファイルの編集は未コミット変更として検出されるため、環境変数の使用を推奨します。

## Component版Playingの再生成（通常の起動には不要）

保存済みのScene・Prefabを使用する通常の起動では移行操作は不要です。以下はSceneの再生成が必要な場合だけの手順です。Issue #59では既存のPlayer・Invader・PlayerShotを作り直さず、敵弾だけをEditor APIで追加しました。

1. Unity **6000.6.4f1**でこのブランチの `Invader/` を開き、スクリプトのコンパイルを待ちます。
2. Play停止中に **Unity Invader > Migrate Arcade to Components** を実行します。未保存Sceneには保存確認が出ます。既存Arcadeを開き、Player・InvaderFormation・PlayerShotという3つのルートを置き換えて保存します。これらに手作業の調整があれば、実行前に退避してください。
3. `Assets/Prefabs/Gameplay/` にPlayer・PlayerShot・InvaderのPrefabと白い弾画像をEditor APIで生成します。既存Prefabは上書きしないため再実行時も調整を保持します。既存DriverのStateと開発用開始フラグは保持し、Score=0・Wave=1へ設定します。Spriteのimport/slicing設定やPackage、ProjectSettings、Bootstrap、SampleSceneは変更しません。
4. Arcadeを開いたまま **Unity Invader > Verify Component Gameplay Setup** を実行し、`COMPONENT_GAMEPLAY_SETUP_PASS` を確認します。接続・Rigidbody/Collider・カメラ倍率・Missing Scriptを確認するだけで、衝突/入力/Waveのテストではありません。
5. **Assets/Scenes/Bootstrap.unity** を開いてPlayし、Arcadeへ切り替わることを確認します。Gameplayの `Development Start Playing` が有効ならPlaying、無効ならAttractのままです。開始/リトライ・クレジットの実装は含みません。

移行メニューは既存の素材 `Assets/Sprites/Player/Player.png` と `Assets/Sprites/Invader/InvaderSprites.png` のTop/Middle/BottomのA/B（6枚）をそのまま使用します。Playerの全体画像を幅16px相当、Invaderの480×241px・PPU30・Center Pivotを約16×8.03px相当で表示する比率を維持します。画像は追加抽出・加工しません。Shot画像はメニューで生成する単色です。Simple描画と子Visualの倍率を使い、9-slice用Borderには依存しません。

本編の生成物はPoCの一時生成物とは異なり、Git除外しません。ローカルで生成・確認したArcade、Prefab、画像と対応する `.meta` は、差分をレビューして同じ作業ブランチへ追加する必要があります。Scene/PrefabのYAMLは直接編集しないでください。作業ツリーが変更されるため、生成後は未コミットのまま `LaunchDevelopUnity.bat` を使わず、まず差分を確認してください。

## 操作と責務

Issue #61でPlayer被弾を追加しました。敵弾がPlayerの16×16px Triggerへ命中すると一度だけ受理し、DriverがPlayingからPlayerDeadへ遷移します。既存の停止方針に合わせて表示・入力・弾・編隊が止まり、自動復帰やLives管理は行いません。Prefab・Sceneは保存済みで、非表示バッチの実Trigger確認が成功しています。描画の目視・実キー操作は未確認です。詳細は [Player被弾](docs/PLAYER_HIT.md) を参照してください。

Gameビューで左右矢印キーを押すと移動し、Spaceで射撃します。同時発射は1発で、押しっぱなしでは再発射せず、離して再押下が必要です。発射中の押下も消費します。ShotはPlayerの子ではなく、Player移動後もShotのXは一定です。画面上端と命中時に非アクティブ化し、同じShotを次の発射に再利用します。

DriverがState・Score・Wave待機/進行、Playerが入力・移動・射撃受付、Shotが自身のTriggerと消費、Invaderが自身の撃破/表示、Formationが55体の一斉移動・A/B・生存集計を担当します。GameplaySimulationと旧表示Controller/モデル/スナップショットは廃止しました。[Architecture](docs/ARCHITECTURE.md) と [ADR-0008](docs/adr/0008-component-gameplay.md) が現在の責務の正本です。

1px＝0.01 Unity単位、224×256の画面比率です。速度はPlayer=60px/秒、Shot=240px/秒、Formationは20/60秒ごとに2px、端で8px下降します。既存Fixed Timestep 0.02秒を使い、ProjectSettingsを変更しません。旧60Hz時計・tick順序は維持せず、Triggerの境界・命中タイミングには差があります。同時接触で1発が倒すのは最大1体ですが、左右どちらが先かは保証しません。

## ローカルで必要な最小限の確認

Issue #59のRolling EnemyShotはScene・Prefab保存済みで、Unity 6000.6.4f1の非表示バッチによる限定Play確認が成功しました。約2秒ごとに発射を試み、通常80px/秒、残数8体以下で100px/秒相当で下降します。発射周期と橙色3×8px矩形は暫定です。[操作・仕様区分・確認結果](docs/ROLLING_ENEMY_SHOT.md) を参照してください。描画の目視と実キーによる操作は未確認です。

以下は**未実行の確認手順・期待結果**です。全旧モデルテストの移植や反復実行は行いません。

| 確認 | 期待結果 |
| --- | --- |
| BootstrapからPlay、左右移動・左右同時押し・端 | Arcadeへ遷移し、Playerが動く。右優先、左右端で停止 |
| Space押しっぱなし→離す→再押下 | 同時1発。押しっぱなしでは再発射しない。飛行中の押下は後から発射されない |
| Shotが画面上端を越える | 消費され、release後に同じ弾を再発射できる |
| 5×11個体と移動・A/B・端の下降反転 | Hierarchyに55個のInvader。全体が同期し、画像の比率・位置ぶれが退行しない |
| Top/Middle/Bottomへ命中 | 命中した1体と弾だけが消える。DriverのScoreが30/20/10増える |
| 隣接個体の境界へ1発、撃破済み個体へ再発射 | 1発で多重撃破・多重加点しない。対象の優先順位は未保証 |
| 全55体を撃破 | 累計Score=990、WaveClearで約1秒停止、その後Wave=2・55体復帰。同じ個体/Renderer/Shotを使用し、Player位置とScoreを保持 |
| 弾の飛行中にPause (Attract)→Resume (Playing) | メニュー **Unity Invader > Component Gameplay** を使用。表示・移動・射撃・物理接触が停止し、位置・弾占有・生死を保持して再開。releaseはPlaying中に確認する |
| Driverのチェックボックスを無効→有効 | 他Componentだけで進行しない。再有効化で現在Stateに応じて再開 |
| Player/Formation単体のDisable→Enable | 自身と配下の表示/物理が停止し、復帰時に二重生成しない |
| 30/60/120fpsなどで短く操作 | 実時間あたりの速度に大きな差がないこと。接触frameや左右優先の厳密一致は保証対象外 |

ScoreとWave番号はDriver、生存数はFormationのInspectorで確認できます。Play中の `Verify Component Gameplay Setup` は55体と有効状態/生存集計の一致を確認し、Score・Wave・生存数をConsoleへ出します。観測はEditor側のSerializedObjectを使用し、ゲーム操作・接触の成功を自動検証するものではありません。

旧GameplaySimulation・表示Push・論理モデル専用のEditor検証/設定メニューは削除しました。新メニューは `ComponentGameplayMigration` と `ComponentGameplayVerification` です。移行のバッチ入口は `-executeMethod UnityInvader.Editor.ComponentGameplayMigration.Migrate`（`-batchmode -quit`）、ログはリポジトリの `Logs/` へ指定します。

独立した [Physics2DProbe](Invader/Assets/Prototypes/Physics2DProbe/README.md) は参考として残し、本編は参照しません。残機・復帰・Shield・UFO、Rolling以外の敵弾や弾同士の相殺は未実装です。
