using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace UnityInvader.Editor
{
    public static class UfoScoreDisplaySetup
    {
        [MenuItem("Unity Invader/Configure UFO Score Display")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Stop Play Mode before configuring the UFO score display.");
            }
            if (Application.isBatchMode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }
            var scene     = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var driver    = UnityEngine.Object.FindObjectsByType<GameplayDriver>(FindObjectsInactive.Include).Single();
            var ufo       = UnityEngine.Object.FindObjectsByType<Ufo>(FindObjectsInactive.Include).Single();
            var hud       = UnityEngine.Object.FindObjectsByType<GameplayHud>(FindObjectsInactive.Include).Single();
            var canvas    = hud.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
            {
                throw new InvalidOperationException("Configure the existing world-space Gameplay HUD first.");
            }
            var child = hud.transform.Find("UFO Score");
            if (child == null)
            {
                child = new GameObject("UFO Score", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Text), typeof(Outline), typeof(UfoScoreDisplay)).transform;
                child.SetParent(hud.transform, false);
                var newText = child.GetComponent<Text>();
                var rect    = newText.rectTransform;
                rect.anchorMin        = new Vector2(0.5f, 0.5f);
                rect.anchorMax        = new Vector2(0.5f, 0.5f);
                rect.pivot            = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta        = new Vector2(24f, 8f);
                newText.font               = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                newText.fontSize           = 8;
                newText.color              = Color.white;
                newText.alignment          = TextAnchor.MiddleCenter;
                newText.horizontalOverflow = HorizontalWrapMode.Overflow;
                newText.verticalOverflow   = VerticalWrapMode.Overflow;
                newText.raycastTarget      = false;
                var outline = child.GetComponent<Outline>();
                outline.effectColor    = Color.black;
                outline.effectDistance = new Vector2(0.5f, -0.5f);
            }
            var text   = child.GetComponent<Text>();
            var effect = child.GetComponent<UfoScoreDisplay>();
            if (text == null || effect == null || text.font == null || text.raycastTarget ||
                child.GetComponent<Collider2D>() != null || child.GetComponent<Rigidbody2D>() != null)
            {
                throw new InvalidOperationException("UFO Score requires configured presentation components without physics or input interception.");
            }
            text.enabled = false;
            text.text    = string.Empty;
            var data = new SerializedObject(effect);
            data.FindProperty("_driver").objectReferenceValue = driver;
            data.FindProperty("_ufo").objectReferenceValue    = ufo;
            data.FindProperty("_text").objectReferenceValue   = text;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (EditorSceneManager.SaveScene(scene) == false)
            {
                throw new InvalidOperationException("Could not save the UFO score display.");
            }
            AssetDatabase.SaveAssets();
            ComponentGameplayVerification.Verify();
            Debug.Log("UFO_SCORE_DISPLAY_SAVED: existing world-space HUD reused; Driver/UFO/Text references saved.");
        }
    }
}
