using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class PushDisplayVerification
    {
        public static void Verify()
        {
            var forbidden = new[] { typeof(GameplayDriver), typeof(GameplaySimulation), typeof(PlayerBody),
                typeof(PlayerShot), typeof(PlayerUnit), typeof(InvaderFormation), typeof(InvaderSlot) };
            const BindingFlags members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var type in new[] { typeof(PlayerController), typeof(InvaderFormationController) })
            {
                Require(type.GetFields(members).All(inField => !forbidden.Contains(inField.FieldType)),
                    "Views must not hold a Driver or mutable game model.");
                Require(type.GetMethods(members).All(inMethod => !forbidden.Contains(inMethod.ReturnType) &&
                    inMethod.GetParameters().All(inParameter => !forbidden.Contains(inParameter.ParameterType))),
                    "Views must not accept or retrieve a Driver or mutable game model.");
                Require(type.GetMethod("Update", members) == null && type.GetMethod("LateUpdate", members) == null,
                    "Views must receive display data rather than pull it each frame.");
            }
            Require(typeof(GameplayDriver).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .All(inMethod => inMethod.Name.StartsWith("Editor", StringComparison.Ordinal)), "Driver must not expose runtime getters or commands.");
            foreach (var type in new[] { typeof(GameplaySimulation), typeof(PlayerUnit), typeof(InvaderFormation), typeof(InvaderFormationSimulation) })
            {
                Require(type.GetMethods(members).Where(inMethod => inMethod.IsPublic)
                    .All(inMethod => !forbidden.Contains(inMethod.ReturnType)), "Owned mutable models must not escape.");
            }

            var scene     = EditorSceneManager.OpenScene("Assets/Scenes/Arcade.unity");
            var driver    = UnityEngine.Object.FindAnyObjectByType<GameplayDriver>();
            var player    = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            var formation = UnityEngine.Object.FindAnyObjectByType<InvaderFormationController>();
            Require(driver != null && player != null && formation != null, "Scene components required.");
            var data = new SerializedObject(driver);
            Require(data.FindProperty("_playerController").objectReferenceValue == player &&
                data.FindProperty("_formationController").objectReferenceValue == formation, "Driver must own both Scene view references.");
            foreach (var root in scene.GetRootGameObjects())
            {
                Require(root.GetComponentsInChildren<Component>(true).All(inComponent => inComponent != null), "Missing script in Arcade.");
            }

            var simulation = new GameplaySimulation();
            var buffer     = new InvaderDisplayState[55];
            for (int frame = 0; frame < 10; frame++)
            {
                simulation.CopyDisplayState(true, out var display, buffer);
                player.ApplyDisplay(display);
                formation.ApplyDisplay(buffer);
            }
            long before    = GC.GetAllocatedBytesForCurrentThread();
            for (int frame = 0; frame < 200; frame++)
            {
                simulation.CopyDisplayState(true, out var display, buffer);
                player.ApplyDisplay(display);
                formation.ApplyDisplay(buffer);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Require(allocated == 0, $"Display generation/push must reuse buffers; allocated={allocated}.");
            Debug.Log("PUSH_DISPLAY_BOUNDARY_PASS: no View -> Driver/model API; Driver -> Views Scene references; immutable display values; 200 pushes allocate zero managed bytes after warmup.");
            // Do not save the verification's generated renderers into the Scene.
            EditorSceneManager.OpenScene("Assets/Scenes/Bootstrap.unity");
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
