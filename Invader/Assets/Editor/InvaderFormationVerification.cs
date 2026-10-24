using System;
using UnityEditor;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class InvaderFormationVerification
    {
        [MenuItem("Unity Invader/Verify Invader Formation Logic")]
        public static void Verify()
        {
            var formation = new InvaderFormation();
            Require(formation.Invaders.Count == 55, "Expected 55 slots.");
            for (int id = 0; id < 55; id++)
            {
                var slot = formation.Invaders[id];
                Require(slot.Id == id && slot.Row == id / 11 && slot.Column == id % 11, "Slot identity failed.");
                Require(slot.Type == (id < 11 ? InvaderType.Top : id < 33 ? InvaderType.Middle : InvaderType.Bottom), "Row type failed.");
            }
            for (int tick = 0; tick < InvaderFormation.TicksPerStep - 1; tick++)
            {
                formation.Tick();
            }
            Require(formation.X == 32 && formation.AnimationFrame == 0, "Movement must wait for the step.");
            formation.Tick();
            Require(formation.X == 34 && formation.AnimationFrame == 1, "First step/frame failed.");
            for (int step = 1; step < 12; step++)
            {
                Step(formation);
            }
            Require(formation.X == InvaderFormation.MaximumX && formation.Y == 200 && formation.Direction == -1,
                "Right edge must descend exactly 8px and reverse on arrival.");
            Require(formation.AnimationFrame == 0, "Frame must alternate with steps.");
            Step(formation);
            Require(formation.X == 54 && formation.Y == 200, "Must move away from edge without another descent.");
            for (int step = 0; step < 23; step++)
            {
                Step(formation);
            }
            Require(formation.X == InvaderFormation.MinimumX && formation.Y == 192 && formation.Direction == 1,
                "Left edge must descend and reverse.");

            foreach (int fps in new[] { 30, 60, 120 })
            {
                var simulation = new InvaderFormationSimulation();
                for (int frame = 0; frame < fps * 20; frame++)
                {
                    simulation.Advance(1.0 / fps, true);
                }
                Require(simulation.Formation.StepCount == 60 && simulation.Formation.X == 56 &&
                    simulation.Formation.Y == 184 && simulation.Formation.Direction == -1 &&
                    simulation.Formation.AnimationFrame == 0, "30/60/120fps results must match after both edges.");
            }
            var paused = new InvaderFormationSimulation();
            paused.Advance(InvaderFormationSimulation.TickDuration / 2, true);
            paused.Advance(100, false);
            paused.Advance(InvaderFormationSimulation.TickDuration / 2, true);
            paused.Advance(19 * InvaderFormationSimulation.TickDuration, true);
            Require(paused.Formation.StepCount == 0, "Paused time and partial ticks must not carry over.");
            paused.Advance(InvaderFormationSimulation.TickDuration / 2, true);
            Require(paused.Formation.StepCount == 1, "Resume must advance normally.");
            PlayerMovementVerification.Verify();
            Debug.Log("INVADER_FORMATION_LOGIC_PASS: 55 identities/types; 60Hz; 30/60/120fps; A/B; both edges; 8px descent; pause/resume; Player regression.");
        }

        private static void Step(InvaderFormation inFormation)
        {
            for (int tick = 0; tick < InvaderFormation.TicksPerStep; tick++)
            {
                inFormation.Tick();
            }
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
