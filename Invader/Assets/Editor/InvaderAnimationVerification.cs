using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class InvaderAnimationVerification
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Verify()
        {
            var formation = UnityEngine.Object.FindAnyObjectByType<InvaderFormation>();
            var driver    = UnityEngine.Object.FindAnyObjectByType<GameplayDriver>();
            var player    = UnityEngine.Object.FindAnyObjectByType<Player>();
            var invaders  = formation.GetComponentsInChildren<Invader>(true);
            Require(invaders.Length == 55, "Saved Arcade creates 55 Invaders.");
            VerifyFirstFrames(invaders);
            driver.EditorSetState(GameState.Playing);
            // Isolate the existing movement clock; do not depend on how many physics steps ran before this callback.
            typeof(InvaderFormation).GetField("_elapsed", PrivateInstance).SetValue(formation, 0f);
            for (int tick = 0; tick < 16; tick++)
            {
                Call(formation, "UpdateFormation");
            }
            VerifyFrames(invaders, 0);
            Call(formation, "UpdateFormation");
            VerifyFrames(invaders, 1);

            driver.EditorSetState(GameState.Attract);
            for (int tick = 0; tick < 20; tick++)
            {
                Call(formation, "UpdateFormation");
            }
            VerifyFrames(invaders, 1);
            driver.EditorSetState(GameState.Playing);
            VerifyFrames(invaders, 1);
            var shot = (PlayerShot)new SerializedObject(player).FindProperty("_shot").objectReferenceValue;
            Require(invaders[0].TryReceiveHit(shot), "Kill while on B frame.");
            for (int tick = 0; tick < 17; tick++)
            {
                Call(formation, "UpdateFormation");
            }
            VerifyFrames(invaders, 0);
            Require(invaders[0].gameObject.activeSelf == false, "Animation never revives a defeated Invader.");
            Call(formation, "PreparePlayerRespawn");
            VerifyFrames(invaders, 0);
            for (int tick = 0; tick < 17; tick++)
            {
                Call(formation, "UpdateFormation");
            }
            VerifyFrames(invaders, 1);
            Call(formation, "BeginWave");
            VerifyFirstFrames(invaders);
            VerifyThreeFrames();
            Debug.Log("INVADER_ANIMATION_PASS: saved row sprites/types, 20/60-second movement clock, synchronized A/B, pause/resume, defeated frames, wave reset and three-frame cycle.");
        }

        public static void VerifyFirstFrames(Invader[] inInvaders)
        {
            VerifyFrames(inInvaders, 0);
        }

        private static void VerifyFrames(Invader[] inInvaders, int inFrame)
        {
            string[] types = { "Top", "Middle", "Middle", "Bottom", "Bottom" };
            for (int index = 0; index < inInvaders.Length; index++)
            {
                int row     = index / InvaderFormation.Columns;
                var invader = inInvaders[index];
                var sprite  = invader.GetComponentInChildren<SpriteRenderer>(true).sprite;
                var target  = row == 0 ? HitTargetType.InvaderTop : row < 3 ? HitTargetType.InvaderMiddle : HitTargetType.InvaderBottom;
                Require(sprite != null && sprite.name == types[row] + (inFrame == 0 ? "FrameA" : "FrameB"),
                    $"Row Sprite and shared timing: {invader.name}, expected={types[row]} frame {inFrame}, actual={sprite?.name}.");
                Require(invader.TargetType == target, "Invader maps its type to the existing hit category.");
            }
        }

        private static void VerifyThreeFrames()
        {
            var prefab   = AssetDatabase.LoadAssetAtPath<Invader>(ComponentGameplayMigration.PrefabPath + "/Invader.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Invader/InvaderSprites.png").OfType<Sprite>().ToArray();
                var frames  = new[] { "TopFrameA", "TopFrameB", "MiddleFrameA" }
                    .Select(inName => sprites.Single(inSprite => inSprite.name == inName)).ToArray();
                var data    = new SerializedObject(instance);
                var setting = data.FindProperty("_topFrames");
                setting.arraySize = frames.Length;
                for (int index = 0; index < frames.Length; index++)
                {
                    setting.GetArrayElementAtIndex(index).objectReferenceValue = frames[index];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                Call(instance, "Initialize", InvaderType.Top);
                Call(instance, "BeginWave");
                var renderer = instance.GetComponentInChildren<SpriteRenderer>(true);
                for (int step = 0; step < 7; step++)
                {
                    Require(renderer.sprite == frames[step % frames.Length], "Three distinct frames cycle without Formation changes.");
                    Call(instance, "AdvanceAnimation");
                }
                Call(instance, "BeginWave");
                Require(renderer.sprite == frames[0], "Three-frame wave reset.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance.gameObject);
            }
        }

        private static void Call(object inTarget, string inName, params object[] inArguments)
        {
            inTarget.GetType().GetMethod(inName, PrivateInstance).Invoke(inTarget, inArguments);
        }

        private static void Require(bool inCondition, string inMessage)
        {
            if (inCondition == false)
            {
                throw new InvalidOperationException(inMessage);
            }
        }
    }
}
