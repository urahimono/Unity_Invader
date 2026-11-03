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

`Assets/Scenes/Bootstrap.unity` を開いてPlayすると、`Arcade.unity` に切り替わります。起動直後はAttract（開始待ち）で停止します。**Enter**で1Pゲームを開始し、GameOver後もEnterで最初からリトライできます（Score=0、Wave=1、Lives=3）。左右矢印は移動、Spaceは射撃です。開始操作はUnityでの割当であり、原作の開始ボタンを断定しません。Enterは一度離してから押してください。長押しやPlaying/PlayerDead/WaveClear中の押下ではリセットしません。

リトライは同じSceneで所有者の状態を復元します。通常弾・敵弾・UFO・B弾、Shieldの損傷、履歴・待機時計をリセットします。SpaceとBは新ゲーム中のrelease後に再押下してください。旧`Development Start Playing`設定は互換用に保持しますが、実行時は無視します。EditorのPause/Resumeは途中状態の保持用であり、新規ゲーム開始ではありません。

開発用メニュー **Unity Invader > Component Gameplay > Pause (Attract)** は、開始待ちと同じAttractへ切り替えます。この状態でEnterを離して押すと、通常の開始操作として**新規ゲームへリセット**されます。途中状態を保持して再開する場合はEnterを使わず、同メニューの **Resume (Playing)** を選んでください。これはUnityツールバーのPause（PlayerLoopの一時停止）とは別の操作です。

EditorとDevelopment Buildでは、Gameビュー左上の黒背景に開発用の`DEBUG`表示（State・Lives・Wave）が出ます。GameplayDriverの現在値を直接読み、PlayerDead・GameOver中も表示します。通常の製品ビルドでは描画しません。原作HUDとは別の確認用表示です。保存済みArcadeに設定済みで、再設定は **Unity Invader > Configure Debug Status Overlay** を使います。

## Player死亡演出

自機の被弾時は、その位置に自作の簡易死亡Spriteを2パターンで表示します。表示期間は既存PlayerDeadの0.5秒待機に従い、復帰・GameOverで消えます。Invader侵攻の直接GameOverでは被弾演出を出しません。新ゲームや無効化で古い表示要求を破棄し、再Enableだけでは再生しません。

Notion資料には2種類の爆発spriteを交互表示する記録がありますが、具体的な模様・切替間隔は未照合です。16×8の自作模様と0.1秒切替は暫定表示で、原作を厳密再現したものではありません。音・当たり判定・残機・待機時間は変更しません。再設定はPlay停止中に **Unity Invader > Configure Player Death Effect** を使います。実施済み検証と後日のPlay確認項目はPR本文を参照してください。

## ゲーム画面HUD

Arcadeには、通常製品ビルドでも表示される正式HUD（上部のSCORE、下部のLIVES、GameOver時のGAME OVER）を保存しています。LIVESは操作中の1機を含む総数です。GameplayDriverの現在値を表示するだけで、得点計算・残機管理・入力は変更しません。文字は自作5×7ドットのビットマップフォント（英大文字・数字・空白、文字送り6px、行高8px）です。配置・字体は参考動画未指定の暫定表示で、原作の厳密な見た目を再現済みとは扱いません。

既存のDEBUG表示とは独立しています。見た目を確認する際は必要に応じてPlay中のDebugStatusOverlayだけを無効にできます。正式HUDの再設定は、Play停止中に **Unity Invader > Configure Gameplay HUD** を実行します。ArcadeのHUDだけを配置・設定して保存するため、ゲーム本編の再生成は不要です。

確認は、BootstrapからPlay→AttractでSCORE 0／LIVES 3／GAME OVER非表示→Enterで開始→撃破による加点→被弾によるLIVES減少→GameOverでGAME OVER表示→Enterの新ゲームで0／3・GAME OVER非表示へ戻る順に行います。DEBUG無効でも正式HUDが更新され、横長・縦長のGameビューでもカメラの224:256領域内に収まることを確認してください。実施済み・未実施の範囲はPR本文を参照します。

フォントの生成元は`GameplayHudFont.cs`、保存先は`Assets/Fonts/GameplayHud/`です。既存のConfigure Gameplay HUDで再生成できます。字形は外部フォント・原作素材から抽出せずコードで定義し、PNGは透明／白の二値、Pointフィルタ・MipMap無効・非圧縮で保存します。Textはサイズ8・Best Fit無効で1texelを論理1pxに対応させ、既存のCanvasと矩形配置を維持します。UFO得点のText・Outlineは変更しません。

見た目の確認はGameビューを224×256または整数倍（448×512など）にし、DEBUG表示を無効にして上記の状態を撮影してください。Play中のScore/Lives Textに一時的に`SCORE 2147483647`／`LIVES 2147483647`を入力してPauseし、必要ならGameplayHudを一時無効にして、切れずに表示されることを確認します（Play停止後は保存しない）。文字の各ドットの寸法、半透明の滲み、GAME OVERの中央配置、既存のゲーム描画とUFO得点・撃破演出が変わらないことを確認します。非整数倍率では画面全体の拡大率に応じて物理ピクセルの幅が不均等になり得ます。画面倍率・カメラの仕様は変更していません。

1500点に到達すると、1ゲームにつき一度だけ残機が1増えます。初期3機は操作中の1機を含み、死亡や次Waveでは獲得済み状態を維持し、GameOver後Enterの新ゲームで再び獲得可能になります。根拠と未確認範囲は [Lives・追加残機](docs/LIVES.md) を参照してください。

## Windowsでdevelopを更新してUnityを起動する

ルートの `LaunchDevelopUnity.bat` をダブルクリックすると、作業ツリーを確認し、`develop` へ切り替えて `git pull --ff-only origin develop` を実行した後、`Invader/` をUnity Editorで開きます。GitがPATH上で使用でき、originへアクセスできる必要があります。

未コミット変更・未追跡ファイル（Gitignore対象を除く）がある場合、またはGit操作が失敗した場合は原則として起動せず停止します。ただし、**次の3つのUnity ProjectSettingsファイルだけが未ステージ変更になっている場合**に限り、変更を`git stash`へ自動退避してから続行します。

- `Invader/ProjectSettings/Packages/com.unity.learn.iet-framework/Settings.json`
- `Invader/ProjectSettings/ProjectAuditorSettings.asset`
- `Invader/ProjectSettings/QualitySettings.asset`

この例外は、ステージ済み変更や他の未コミット・未追跡ファイルが1つでもあれば適用しません。自動退避は変更を破棄せずローカルのstashに保存するため、`git stash list`で確認できます。実際に採用したいUnity設定変更は、起動バッチ実行前に内容を確認し、通常のGit手順でコミットしてください。stashは自動削除しません。

分岐した履歴は自動解消しません。エラー時はコンソールに原因を表示して待機します。正常時は現在のブランチが `develop` になります。

Unityのバージョンは更新後の `Invader/ProjectSettings/ProjectVersion.txt` から読み、標準の `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe` を使用します。カスタム配置では、環境変数 `UNITY_EDITOR_PATH` に対応バージョンのUnity.exeのフルパスを設定してください（値自体に引用符は含めません）。例: `D:\Unity Editors\6000.6.4f1\Editor\Unity.exe`。バッチ冒頭の設定例でも指定できますが、追跡ファイルの編集は未コミット変更として検出されるため、環境変数の使用を推奨します。

## Unity CLI Loop（Windowsでのローカルセットアップ）

UnityパッケージはOpenUPMのScoped Registry経由で **3.21.3** に固定しています。以下はユーザーのWindows PCで行う後続手順です。Web版CodexからPCやUnity Editorへ直接接続できることは前提にしていません。[公式手順（V3.21.3）](https://github.com/hatayama/unity-cli-loop/blob/v3.21.3/README_ja.md)も参照してください。

1. Unity Hubから、このブランチの `Invader/` をUnity **6000.6.4f1**で開き、Package Managerの解決とコンパイルの完了を待ちます。Unity CLI Loopが3.21.3になり、Consoleにコンパイルエラーがないことを確認してください。`Packages/packages-lock.json` の更新はUnityに任せ、差分をレビューしてGit管理します。手動で依存関係やハッシュを補完しません。
2. リポジトリルートでPowerShellを開き、CLIインストーラーを保存して内容を確認します。ダウンロードしただけではインストールされません。

   ```powershell
   New-Item -ItemType Directory -Force .\Logs | Out-Null
   Invoke-WebRequest 'https://raw.githubusercontent.com/hatayama/unity-cli-loop/v3.21.3/scripts/install.ps1' -OutFile .\Logs\install-uloop.ps1
   Get-Content .\Logs\install-uloop.ps1
   ```

   内容を確認した後、次を実行します。実行ポリシーなどで拒否された場合は、表示された原因を確認してください。

   ```powershell
   & .\Logs\install-uloop.ps1
   ```

   CLI本体の既定の配置先は `%LOCALAPPDATA%\Programs\uloop\bin` です。インストーラーの案内に従ってPowerShellを開き直し、`uloop -v` で検出を確認します。グローバルCLIとUnityパッケージのバージョンは別管理で、プロジェクト用runnerはパッケージ由来のpinに従って取得されます。
3. **リポジトリルートから `Invader/` に移動**して、解決済みパッケージからCodex用Skillsをインストールします。`--root git` により保存先をリポジトリ直下の `.agents/skills/` にします。

   ```powershell
   Set-Location .\Invader
   uloop skills install --agents --root git
   uloop status
   uloop list
   ```

   この後も `uloop` は `Invader/` 内で実行してください。Skillsをリポジトリルートから使うエージェントにも、この作業ディレクトリを指定します。`status` が対象Editorの応答可能な状態を返し、`list` がツール一覧を返すことを確認します。失敗した場合は `Window > Unity CLI Loop > Settings` とConsoleを確認してください。
4. 接続後の最小確認として、次を `Invader/` で実行します。出力ログはリポジトリ直下の `Logs/` に保存します。

   ```powershell
   uloop compile *> ..\Logs\uloop-compile.log
   uloop get-logs --max-count 20 *> ..\Logs\uloop-console.log
   ```

   compileの成功結果とエラーログの有無を確認します。Play Modeの確認は `Assets/Scenes/Bootstrap.unity` を開き、インストールした `uloop-control-play-mode` Skillの手順で再生・停止し、上記「起動方法」のAttract→Enter→Playingを確認してください。コンパイル成功だけをゲームの動作確認とは扱いません。

`LaunchDevelopUnity.bat` はdevelopへ切り替えるため、作業ブランチの導入確認には使わず、Hubから `Invader/` を直接開いてください。developへマージされた後は従来どおり利用できます。バッチ自体の変更はありません。

生成物は、`Invader/.uloop/`（パッケージから自動同期される `project-runner-pin.json` とローカル出力を含む）、`.agents/skills/uloop-*/`、既存の `Library/`・`UserSettings/`、リポジトリ直下の `Logs/` をGit管理しません。pinやSkillsは手編集せず、パッケージ解決・Skillsインストールで再生成します。CLI本体とrunnerキャッシュもPC側で管理します。設定の再現元となるmanifestとUnityが更新したPackage LockはGit管理します。

## Component版Playingの再生成（通常の起動には不要）

保存済みのScene・Prefabを使用する通常の起動では移行操作は不要です。以下はSceneの再生成が必要な場合だけの手順です。Issue #59では既存のPlayer・Invader・PlayerShotを作り直さず、敵弾だけをEditor APIで追加しました。

1. Unity **6000.6.4f1**でこのブランチの `Invader/` を開き、スクリプトのコンパイルを待ちます。
2. Play停止中に **Unity Invader > Migrate Arcade to Components** を実行します。未保存Sceneには保存確認が出ます。既存Arcadeを開き、Player・InvaderFormation・PlayerShotという3つのルートを置き換えて保存します。これらに手作業の調整があれば、実行前に退避してください。
3. `Assets/Prefabs/Gameplay/` にPlayer・PlayerShot・InvaderのPrefabと白い弾画像をEditor APIで生成します。既存Prefabは上書きしないため再実行時も調整を保持します。既存DriverのStateと開発用開始フラグは保持し、Score=0・Wave=1へ設定します。Spriteのimport/slicing設定やPackage、ProjectSettings、Bootstrap、SampleSceneは変更しません。
4. Arcadeを開いたまま **Unity Invader > Verify Component Gameplay Setup** を実行し、`COMPONENT_GAMEPLAY_SETUP_PASS` を確認します。接続・Rigidbody/Collider・カメラ倍率・Missing Scriptを確認するだけで、衝突/入力/Waveのテストではありません。
5. **Assets/Scenes/Bootstrap.unity** を開いてPlayし、Arcadeへ切り替わることを確認します。Attractで停止した後、EnterでPlayingへ進むことを確認します。旧開発用開始フラグには依存しません。クレジットは対象外です。

移行メニューは既存の素材 `Assets/Sprites/Player/Player.png` と `Assets/Sprites/Invader/InvaderSprites.png` のTop/Middle/BottomのA/B（6枚）をそのまま使用します。Playerの全体画像を幅16px相当、Invaderの480×241px・PPU30・Center Pivotを約16×8.03px相当で表示する比率を維持します。画像は追加抽出・加工しません。Shot画像はメニューで生成する単色です。Simple描画と子Visualの倍率を使い、9-slice用Borderには依存しません。

本編の生成物はPoCの一時生成物とは異なり、Git除外しません。ローカルで生成・確認したArcade、Prefab、画像と対応する `.meta` は、差分をレビューして同じ作業ブランチへ追加する必要があります。Scene/PrefabのYAMLは直接編集しないでください。作業ツリーが変更されるため、生成後は未コミットのまま `LaunchDevelopUnity.bat` を使わず、まず差分を確認してください。

## 操作と責務

Editor / Development Buildでは、Gameビューで **Fキーを押している間だけ編隊の移動テンポが10倍**になります。離すと通常へ戻り、B絨毯爆撃とも併用できます。横2px・端で8px下降の各ステップとShield侵食をそのまま繰り返す開発用操作です。Player・弾・敵弾の発射間隔・待機時間やTime.timeScaleは変更しません。通常の製品ビルドでは無効です。保存済みArcadeに設定済みで、再設定は **Unity Invader > Configure Debug Formation Speed** です。

Shield4基は自弾・3種類の敵弾の命中位置から画素単位で削れ、空いた穴を弾が通過します。Player死亡・復帰では穴を保持し、次Wave開始時に4基とも復元します。B長押しでも同じ処理で破壊できます。Invaderの横移動・下降でもColliderの通過範囲が侵食されます。形状・配置・破壊マスクとCollider全面による侵食は暫定仕様です。[仕様区分と実装](docs/SHIELDS.md) を参照してください。再設定は **Unity Invader > Configure Shields** です。

Editor / Development Buildでは、Gameビューで **Bキーを長押しすると絨毯爆撃**します。下部の23レーン（10論理px間隔）をシャッフルし、3発ずつ約0.04〜0.08秒間隔で発射します。専用PlayerShotは最大96発を再利用し、通常のSpace射撃とは独立しています。Bを離すと追加発射を止め、非Playing時には専用弾を回収します。次Waveでも押し続けていれば再開します。原作仕様ではない開発用操作で、通常の製品ビルドでは動作しません。保存済みArcadeに設定済みで、再設定は **Unity Invader > Configure Debug Carpet Bomb** を使います。

Playerの16×16px Triggerへ敵弾が命中すると、DriverがLivesを1機減らしてPlayerDeadへ遷移します。初期3機は操作中の1機を含み、暫定0.5秒後、残機があれば初期位置へ戻ってPlayingへ直接復帰し、0機ならGameOverで停止します。編隊の進捗・Score・Waveは保持します。LivesはDriver Inspectorで確認できます。詳細と検証状況は [Lives・復帰](docs/LIVES.md)、被弾の基礎実装は [Player被弾](docs/PLAYER_HIT.md) を参照してください。

Gameビューで左右矢印キーを押すと移動し、Spaceで射撃します。同時発射は1発で、押しっぱなしでは再発射せず、離して再押下が必要です。発射中の押下も消費します。ShotはPlayerの子ではなく、Player移動後もShotのXは一定です。画面上端と命中時は簡易爆発表示で停止し、16/60秒相当の待機完了まで同じShotを再利用できません（0.02秒更新では14回、約0.28秒）。待機中の押下も予約しません。命中結果・得点は即時に確定します。

DriverがState・Score・Wave・Livesと死亡待機、Playerが入力・移動・射撃受付と復帰、Shotが自身のTriggerと消費、Invaderが自身の撃破/表示、Formationが55体の一斉移動・A/B・生存集計を担当します。GameplaySimulationと旧表示Controller/モデル/スナップショットは廃止しました。[Architecture](docs/ARCHITECTURE.md) と [ADR-0008](docs/adr/0008-component-gameplay.md) が現在の責務の正本です。

1px＝0.01 Unity単位、224×256の画面比率です。速度はPlayer=60px/秒、Shot=240px/秒、Formationは生存数Nに対してN/60秒ごとに2px、端で8px下降・反転します（55体：約0.917秒、20体：約0.333秒、8体：約0.133秒、1体：約0.0167秒）。最後の1体は左端で右向きに反転した後の右移動が3pxになり、左移動は2pxです。右移動中に1体になった場合、その右行程は2pxのままです。既存Fixed Timestep 0.02秒を使い、ProjectSettingsを変更しません。旧60Hz時計・tick順序は維持せず、Triggerの境界・命中タイミングには差があります。同時接触で1発が倒すのは最大1体ですが、左右どちらが先かは保証しません。

Rolling・Plunger・Squigglyの3種類が独立したEnemyShotを再利用します。Squigglyは固定15列、Plungerは固定16列の最下段生存Invaderから発射し、空列でも表を進めます。Formationの敵弾専用60Hz phaseと他弾のstep count・Score依存Reloadで発射/更新を制御し、旧2秒タイマーは廃止しました。色付き3×8px矩形と初期phase・Wave時の列ポインタ初期化は暫定です。保存済みScene・Prefabを使用し、Squiggly再設定は **Unity Invader > Configure Squiggly EnemyShot** です。[ROM根拠・Unity上の近似・未解決事項](docs/ENEMY_SHOTS.md) を参照してください。

UFOは編隊が初期高さより下降してからカウンタを進め、生存8体以上かつSquiggly枠が空いた時に出現します。方向・得点は通常PlayerShotの終了履歴で決まり、B弾は履歴対象外です。命中時に通常弾の終了履歴を更新して15周期の表から加点し、開発用オーバーレイのScoreで確認できます。再設定は **Unity Invader > Configure UFO**、仕様と時間/座標の近似は [UFO](docs/UFO.md) を参照してください。

## ローカルで必要な最小限の確認

Issue #59のRolling EnemyShotはScene・Prefab保存済みで、Unity 6000.6.4f1の非表示バッチによる限定Play確認が成功しました。当時は約2秒ごとの暫定発射でした。Issue #77で3種類共通のphase・Reloadへ移行済みです。通常80px/秒、残数8体以下で100px/秒相当で下降します。橙色3×8px矩形は暫定です。[操作・仕様区分・確認結果](docs/ROLLING_ENEMY_SHOT.md) を参照してください。描画の目視と実キーによる操作は未確認です。

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

独立した [Physics2DProbe](Invader/Assets/Prototypes/Physics2DProbe/README.md) は参考として残し、本編は参照しません。原作HUDの厳密な再現や一部の演出は未実装です。

## 開始・リトライ／弾相殺の手動確認（Issues #131・#133）

保存済み `Bootstrap.unity` をPlayし、次を確認してください。ゲーム動作の実確認はユーザー環境のUnity 6000.6.4f1で行います。過去に追加した専用Editorバッチ検証 `GameStartVerification` / `ProjectileCancellationVerification` は保守対象から外して削除しました（追加当時もUnityでは未実施）。これらの`-executeMethod`入口は現在使えません。

1. 起動直後がAttractで停止しており、左右・Spaceではゲームが始まらず、Enterを離してから押すとPlayingになり移動・射撃できる。
2. Enter長押しやPlaying中のEnterでScore・Waveがリセットされない。
3. 3回の被弾またはInvader侵攻でGameOverになり、EnterでScore=0・Lives=3・Wave=1、Invader55体・Player初期位置・Shield4基が復元され、再び攻撃が進行する。リトライを複数回試す。
4. 通常PlayerShotとRolling／Plunger／Squigglyの敵弾が接触したとき双方が爆発待機へ入り、Score／Livesは変化しない。待機完了後にSpaceを離し再押下すると自機弾が再発射できる。開発用B弾も可能な範囲で同様に確認する。
5. 通常の被弾・死亡復帰、Shieldの損傷・次Wave復元、UFO出現・得点、全滅から次Waveへの移行が引き続き動作する。

物理接触や実時間の待機、製品ビルドでの操作確認は、静的なコード確認の成功だけでは代用できません。

### 編隊加速の手動確認（Issue #139）

Unity 6000.6.4f1でBootstrapをPlayし、Enterを離して押して開始します。生存数はFormation Inspectorで観測し、Spaceまたは開発用Bで実際に撃破してください。`_aliveCount`だけの書き換えや個体の手動無効化は集計と生死が不一致になるため使いません。

| 操作・観測 | 期待結果 |
| --- | --- |
| 55・20・8・1体付近で移動間隔を比較 | N/60秒を目安に連続加速する。0.02秒の物理更新で量子化され、1体時は1更新に1～2移動する |
| Bで連続撃破し、撃破直前・直後を比較 | 進捗を維持して以後の加算だけが加速し、過去の経過時間による大量移動がない |
| 2体→右移動中に1体にする。左右の反転を観測 | 現在の右行程は2px、左行程は2px、次の左端反転後の右行程は3px。左移動中に1体になった場合も次の右行程から3px |
| UnityツールバーのPauseとStepでFormationの位置を観測 | 1px=0.01単位。反転以外は移動回数×2pxまたは3px。複数移動のある物理更新は単一移動と区別する。端は生存個体の範囲でclampし、8px下降・反転する |
| Fを長押しして離す（55体・1体の両方） | 編隊の進捗だけ10倍。1体では約12移動/物理更新となり、解除後は通常速度に戻る。Player移動・射撃、敵弾3種のphase・UFO時計はFで加速しない（敵弾は従来の8体以下の高速化を維持） |
| Pause (Attract)→数秒待機→Resume (Playing)、DriverとFormationのDisable/Enable、Player被弾→復帰 | 停止中に進まず、復帰時に停止時間を取り戻さない。位置・方向・生死・アニメを維持し、移動進捗は0から再開する |
| Shield到達・侵攻・全滅・次Wave・GameOver後Enter | 各移動でShield侵食と同期アニメを維持。侵攻でGameOverとなり残り移動を中断。0体では移動せずWaveClear。新ゲーム・次Waveは55体・進捗0・右2pxへ戻る |

これはROMの生存個体走査を編隊一括移動で近似した仕様です。原作の順次描画やアーケードアーカイブス2版動画との厳密な速度・反転frame比較は未確認です。

### 生存個体に応じた左右反転（Issue #141）

各移動で生存個体（`gameObject.activeSelf`）のローカルXの最小・最大を求め、セル幅16pxを含む範囲が画面に収まる位置でclamp・8px下降・反転します。全55体では従来と同じ端です。外側の列が全滅すると、その側への移動範囲が16pxずつ広がります。親の基準座標が画面外へ出る場合も、生存個体の中心は論理X=8〜216pxに収まります。ROMのVRAM走査や種類別Spriteの画素形状による端判定は再現していません。参考動画との厳密な端位置の照合も未実施です。

Bootstrapから開始し、Spaceで実際に撃破して次を確認してください（生存数だけの書き換えや個体の手動無効化は行いません）。UnityツールバーのPause/Stepで位置を観測できます。1px=0.01単位で、論理Xは `112 + FormationのRigidbody2D.position.x / 0.01 + 個体のlocalPosition.x / 0.01` です。

| 操作・観測 | 期待結果 |
| --- | --- |
| 55体を維持して左右反転 | 従来どおり左端個体中心8px・右端個体中心216pxで反転し、8px下降 |
| 左端列のみ全滅／右端列のみ全滅 | 全滅した側へ親の移動限界が16px広がり、反対側の限界は同じ。列の一部が生存していればその列を端として扱う |
| 右へ移動中に最右列の最後の個体を撃破（左も同様） | 次の移動から新しい生存端を使用。旧境界で余計な反転・下降をしない |
| 中央列・左端列・右端列にそれぞれ1体だけ残す | 残った個体が左右の中心8〜216pxまで移動。親原点が画面外でも生存個体は画面内。左2px、左端反転後の右3pxを維持 |
| 1体でF長押し→解除、Pause/Resume・死亡復帰 | 複数移動でも各回で端を判定し、引っかかりや同位置での反転反復がない。F解除・時計停止/復帰は既存どおり |
| Shield到達・侵攻・全滅・次Wave・GameOver後の新規開始 | 侵食・同期アニメ・GameOver中断・WaveClearを維持。次Wave/新規開始で55体と元の境界へ復元。敵弾・UFO・Playerの更新周期は変わらない |
