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
- 現在の状態: Bootstrap → Arcadeの起動経路とGameStateの土台に、Playing上のPlayer左右移動、55体のInvaderFormation、単一PlayerShot、論理命中・撃破・Score加算、全滅から次Waveへのループを実装しています。

Unity Hubから `Invader/` を指定して、上記バージョンのEditorで開いてください。

`Assets/Scenes/Bootstrap.unity` を開いてPlayすると、`Arcade.unity` に切り替わります。

## Windowsでdevelopを更新してUnityを起動する

ルートの `LaunchDevelopUnity.bat` をダブルクリックすると、作業ツリーを確認し、`develop` へ切り替えて `git pull --ff-only origin develop` を実行した後、`Invader/` をUnity Editorで開きます。GitがPATH上で使用でき、originへアクセスできる必要があります。

未コミット変更・未追跡ファイル（Gitignore対象を除く）がある場合、またはGit操作が失敗した場合は起動せず停止します。自動stashや変更の削除は行いません。分岐した履歴は自動解消しません。エラー時はコンソールに原因を表示して待機します。正常時は現在のブランチが `develop` になります。

Unityのバージョンは更新後の `Invader/ProjectSettings/ProjectVersion.txt` から読み、標準の `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe` を使用します。カスタム配置では、環境変数 `UNITY_EDITOR_PATH` に対応バージョンのUnity.exeのフルパスを設定してください（値自体に引用符は含めません）。例: `D:\Unity Editors\6000.6.4f1\Editor\Unity.exe`。バッチ冒頭の設定例でも指定できますが、追跡ファイルの編集は未コミット変更として検出されるため、環境変数の使用を推奨します。

## 操作方法

Player移動の確認はBootstrapからPlayし、Gameビューにフォーカスして左右矢印キーを押してください。
射撃はSpaceです。押しっぱなしで再発射しないため、離してから再押下してください。
ArcadeのGameStateにある `GameplayDriver` の `Development Start Playing` が、開発確認用にGameStateをPlayingへ変更します。
本来のAttract / Credit / PlayerReady遷移ではありません。このフラグを無効にすればAttractのまま開始します。
ゲーム全体の進行は `ArcadeGameplaySimulation`、プレイヤー側のまとまりは `PlayerUnit`、自機本体の状態は `PlayerBody`、自機弾は `PlayerShot` が扱います。
現在の責務境界、State、論理座標、時間・入力の流れは [Architecture](docs/ARCHITECTURE.md) を参照してください。

## 素材・Sceneの設定

画像は指定された `Player.png` を加工せず、全体を16×16論理ピクセル相当に縮小表示しています。
import設定はSprite / Point / 圧縮なし / Mip Mapsなしです。最終的な画面上の見え方はGameビューで確認してください。

`Unity Invader > Configure Player Movement` は既存ArcadeにPlayerとカメラ設定を適用するEditor APIツールです。
`Unity Invader > Verify Player Movement Logic` で、30/60/120fps相当での60Hz移動、
非Playing時の停止・蓄積破棄、左右同時押し、左右端の制限を検証できます。

指定素材 `Assets/Sprites/Invader/InvaderSprites.png` は画像を変更せず、最下部1pxを除く範囲から切り出します。
全6枚のSprite Rectは480×241px、Center Pivot・Full Rectで統一します。
A列のX=540、B列のX=1150、YはTop=483、Middle=242、Bottom=1です。
白い絵の外接矩形による個別トリミングは行いません。PPU=30、共通表示スケール=(1,1)で
アスペクト比を維持し、Sprite全体は論理16×約8.03pxになります。Formationの間隔・端判定は従来どおりです。
importはSprite Multiple / Point / 圧縮なし / Mip Mapsなし / NPOTリサイズなし / 最大4096です。
`Unity Invader > Configure Invader Formation` はUnity Editor APIでimport・slicing・Arcade接続を再適用します。
既存のSprite IDは名前ごとに保持するため、再実行時も参照が維持されます。
`Unity Invader > Verify Invader Formation Logic` は55体の構成、A/B同期、両端の下降反転、停止再開と
30/60/120fps相当の20秒間の同一結果を検証し、既存Playerのロジック検証も実行します。
batchでは `-executeMethod UnityInvader.Editor.InvaderFormationPlayVerification.Verify` で
Bootstrap起動、表示参照、480×241px矩形・Center Pivot・PPU 30・等倍表示、A/Bのみの交換での
論理位置・表示位置の不変、Playerの存在・表示、全非Playing Stateの停止・非表示を検証できます。
このPlay Mode検証は自らEditorを終了するため、`-quit`を付けず専用のbatchプロセスで実行してください。
GUIではBootstrapからPlayし、3種類の行配置、A/Bの見た目、端での下降反転とPlayer左右移動を確認してください。
縦長の歪みとA/B時の左右ぶれが解消した見た目・表示サイズの妥当性はGameビューでの確認事項です。

Sceneは `Assets/Editor/ArcadeSceneSetup.cs` のUnity Editor APIで生成しています。
`Unity Invader > Create Initial Scenes` はSceneが未作成の場合のみ使用できます。
既存Sceneの上書きを防ぐため、作成済みのプロジェクトで再実行するとエラーになります。

開発はIssueを起点に、`develop` から `codex/` ブランチを作成し、検証後に `develop` 向けDraft PRでレビューします。作業ルールは [AGENTS.md](AGENTS.md) と [Invader/AGENTS.md](Invader/AGENTS.md) を参照してください。

## Shotの検証

`Unity Invader > Verify Player Shot Logic` またはbatchの
`-executeMethod UnityInvader.Editor.PlayerShotVerification.Verify` で単一スロット、
release待ち、固定X、4px/tick、上端消滅、非Playing停止、30/60/120fps相当の一致と
既存Player/Formationの回帰検証を実行できます。
`-executeMethod UnityInvader.Editor.PlayerShotPlayVerification.Verify` はInput Systemの仮想Keyboardによる
左右キー・Spaceの押下／release、Bootstrap起動、単一Shot表示と座標変換、全非Playing Stateでの
非表示・停止・状態保持と再開を検証します。自身でEditorを終了するため`-quit`を付けず、専用batchで実行します。
GUIではBootstrapからPlayし、Gameビューで左右キーとSpaceを操作してください。
押しっぱなしで再発射しないこと、離して再押下で発射すること、Player移動中もShotのXが
固定されること、非Playing時の非表示・停止と、Player/Formationの従来動作を確認してください。

## 命中・Scoreの検証

`Unity Invader > Verify Invader Hit Logic` またはbatchの
`-executeMethod UnityInvader.Editor.InvaderHitVerification.Verify` でAliveCount、Dead除外、種類別/累積Score、
1 Shot=1体、命中時Shot非Active・release待ち、tick順、2tick中の一時的な命中、HitBox境界、
複数撃破後の移動、55体撃破、非Playing停止、30/60/120fpsと既存回帰検証を実行します。
`-executeMethod UnityInvader.Editor.InvaderHitPlayVerification.Verify` はBootstrapから起動し、
仮想KeyboardのSpace/左右キーを実際のControllerへ渡して命中・表示・release待ち・再発射、
非Playing停止/非表示・再開と、生存個体が残る間のPlaying維持を検証します。
Play検証では単一GameplayDriverを確認し、Player表示を無効にした20描画フレームの各フレームで実測deltaTimeの1回分だけ進むことと、
GameplayDriverを無効にすると表示側だけでは進まないことも検証します。
`Unity Invader > Configure Gameplay Driver` はUnity Editor APIでArcadeのGameStateへ専用Controllerを接続します。
Play検証は自身でEditorを終了するため、専用batchで`-quit`を付けず実行してください。
GUIでは狙ったInvaderとShotだけが消えること、残った編隊の移動・A/B同期・下降反転、
左右移動と押しっぱなし禁止・release後再発射を確認してください。Scoreは `GameStateController.Playing.Score` で取得できます。

## Waveループの検証

`Unity Invader > Verify Wave Loop Logic` またはbatchの
`-executeMethod UnityInvader.Editor.WaveLoopVerification.Verify` は
30/60/120fps相当で55体を実命中により2Wave撃破し、Wave 1→2→3、累積Score=1980、
初期化・保持・停止・Fire gateと既存Player/Shot/Formation/Collision回帰を検証します。
`-executeMethod UnityInvader.Editor.WaveLoopPlayVerification.Verify` は
Bootstrapから実ControllerでWaveClearへ遷移し、約1秒の停止、Wave 2での撃破とWave 3到達、
仮想Keyboardの押しっぱなし/release/再押下、同じ55 Rendererの再利用を検証します。
専用batchで実行し、このPlay検証には `-quit` を付けないでください。
GUIでは最後の撃破後の停止、55体の再表示、次Waveの操作・射撃・撃破、
表示重複・位置飛びの有無を確認してください。Score/WaveNumberは論理プロパティで確認できます。
