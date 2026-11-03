using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace UnityInvader.Editor
{
    public static class GameplayHudSetup
    {
        [MenuItem("Unity Invader/Configure Gameplay HUD")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring the gameplay HUD.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var drivers = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include);
            if (drivers.Length != 1)
            {
                throw new InvalidOperationException("Arcade requires exactly one GameplayDriver.");
            }
            var roots = scene.GetRootGameObjects().Where(inRoot => inRoot.name == "Gameplay HUD").ToArray();
            if (roots.Length > 1)
            {
                throw new InvalidOperationException("Arcade has multiple Gameplay HUD roots.");
            }
            var root = roots.Length == 1 ? roots[0] :
                new GameObject("Gameplay HUD", typeof(RectTransform), typeof(Canvas), typeof(GameplayHud));
            var canvas = root.GetComponent<Canvas>();
            var hud = root.GetComponent<GameplayHud>();
            if (canvas == null || hud == null)
            {
                throw new InvalidOperationException("Gameplay HUD root must contain Canvas and GameplayHud.");
            }
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = new Vector2(ArcadeViewport.ScreenWidth, ArcadeViewport.ScreenHeight);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.position = new Vector3(0f, 0f, -1f);
            rect.rotation = Quaternion.identity;
            rect.localScale = Vector3.one * ArcadeViewport.UnitsPerPixel;

            // Provisional layout: no claim of matching an unspecified reference video/font.
            Text score = ConfigureText(rect, "Score", new Vector2(8f, 236f), new Vector2(208f, 12f), TextAnchor.MiddleLeft);
            Text lives = ConfigureText(rect, "Lives", new Vector2(8f, 4f), new Vector2(208f, 12f), TextAnchor.MiddleLeft);
            Text gameOver = ConfigureText(rect, "Game Over", new Vector2(40f, 120f), new Vector2(144f, 16f), TextAnchor.MiddleCenter);
            score.text = $"SCORE {drivers[0].Score}";
            lives.text = $"LIVES {drivers[0].Lives}";
            gameOver.text = "GAME OVER";
            gameOver.enabled = drivers[0].CurrentState == GameState.GameOver;
            var data = new SerializedObject(hud);
            data.FindProperty("_driver").objectReferenceValue = drivers[0];
            data.FindProperty("_score").objectReferenceValue = score;
            data.FindProperty("_lives").objectReferenceValue = lives;
            data.FindProperty("_gameOver").objectReferenceValue = gameOver;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save the gameplay HUD in Arcade.");
            }
            Debug.Log("GAMEPLAY_HUD_SAVED: world-space 224x256 Canvas and Driver/Text references saved in Arcade.");
        }

        private static Text ConfigureText(RectTransform inParent, string inName, Vector2 inPosition, Vector2 inSize, TextAnchor inAlignment)
        {
            Transform child = inParent.Find(inName);
            var text = child == null ? new GameObject(inName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>() : child.GetComponent<Text>();
            if (text == null)
            {
                throw new InvalidOperationException($"HUD child {inName} must contain a Text component.");
            }
            var rect = text.rectTransform;
            rect.SetParent(inParent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = inPosition;
            rect.sizeDelta = inSize;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 8;
            text.color = Color.white;
            text.alignment = inAlignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
