# Unity_Invader

Unityを使用してSpace Invadersの挙動を再現する個人開発・学習プロジェクトです。参考プレイ動画を基準に忠実な再現を目指します。

元ゲームから抽出した画像・音声・動画などの素材は含みません。これらの素材をリポジトリへ追加しない方針です。

- Unity Project Root: `Invader/`
- Unity: `6000.6.4f1`
- 現在の状態: Bootstrap → Arcadeの起動経路とGameStateの土台に、Playing上のPlayer左右移動を実装しています。

Unity Hubから `Invader/` を指定して、上記バージョンのEditorで開いてください。

`Assets/Scenes/Bootstrap.unity` を開いてPlayすると、`Arcade.unity` に切り替わります。
Arcadeの `GameState` オブジェクトにある `GameStateController` が現在Stateを保持します。
初期値の `Attract` は土台としての仮置きで、原作の起動ルールを確定するものではありません。

後続コードは `UnityInvader.GameStateController.CurrentState` で現在Stateを参照し、
`ChangeState(GameState nextState)` で変更できます。`UnityInvader.GameState` は
`Attract` / `PlayerReady` / `Playing` / `PlayerDead` / `WaveClear` / `GameOver` の6値です。
本来の開始遷移や各Stateのルール、Invader / Shot、Score / Waveはまだ実装していません。

Player移動の確認はBootstrapからPlayし、Gameビューにフォーカスして左右矢印キーを押してください。
ArcadeのPlayerにある `Development Start Playing` が、開発確認用にGameStateをPlayingへ変更します。
本来のAttract / Credit / PlayerReady遷移ではありません。このフラグを無効にすればAttractのまま開始します。
Playing以外ではPlayerの移動を更新せず、Spriteも非表示にします。左右同時押しは停止です。

`PlayerInputReader` がInput SystemのLeft / Rightボタンをboolの `PlayerInputState` に変換します。
Unity非依存の `PlayerSimulation` はフレーム時間を蓄積して1/60秒ごとに `PlayerModel.Tick` を呼びます。
Playing以外では蓄積時間を破棄し、再開時の追いつき移動を防ぎます。
速度は暫定で1論理ピクセル/tick（60ピクセル/秒）、初期位置は(112, 24)、X範囲は8〜216です。
原作の厳密な速度・左右端位置の再現は後続工程です。

論理画面は224×256、原点は左下です。`PlayerController` が中心原点のUnity座標へ
`(X - 112, Y - 128)` で写し、Transformの値はゲームルールへ戻しません。
`ArcadeViewport` は表示領域を224:256に保ち、Gameビューの縦横比による見切れを防ぎます。
画像は指定された `Player.png` を加工せず、全体を16×16論理ピクセル相当に縮小表示しています。
import設定はSprite / Point / 圧縮なし / Mip Mapsなしです。最終的な画面上の見え方はGameビューで確認してください。

`Unity Invader > Configure Player Movement` は既存ArcadeにPlayerとカメラ設定を適用するEditor APIツールです。
`Unity Invader > Verify Player Movement Logic` で、30/60/120fps相当での60Hz移動、
非Playing時の停止・蓄積破棄、左右同時押し、左右端の制限を検証できます。

Sceneは `Assets/Editor/ArcadeSceneSetup.cs` のUnity Editor APIで生成しています。
`Unity Invader > Create Initial Scenes` はSceneが未作成の場合のみ使用できます。
既存Sceneの上書きを防ぐため、作成済みのプロジェクトで再実行するとエラーになります。

開発はIssueを起点に、`develop` から `codex/` ブランチを作成し、検証後に `develop` 向けDraft PRでレビューします。作業ルールは [AGENTS.md](AGENTS.md) と [Invader/AGENTS.md](Invader/AGENTS.md) を参照してください。
