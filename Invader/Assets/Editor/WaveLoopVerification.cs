using System;
using UnityEditor;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class WaveLoopVerification
    {
        [MenuItem("Unity Invader/Verify Wave Loop Logic")]
        public static void Verify()
        {
            InvaderHitVerification.Verify();
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var simulation = new GameplaySimulation();
                var player     = simulation.Player;
                var shot       = simulation.Shot;
                Require(simulation.WaveNumber == 1 && !simulation.WaveClearRequired, "Initial Wave must be 1.");
                simulation.StartNextWave();
                Require(simulation.WaveNumber == 1, "Cannot skip an uncleared wave.");
                for (int wave = 1; wave <= 2; wave++)
                {
                    Clear(simulation, 1.0 / fps);
                    Require(simulation.WaveClearRequired && simulation.Formation.AliveCount == 0 &&
                        simulation.Score == wave * 990 && !shot.Active && !shot.FireReady,
                        "Final real hit must finish death, count, Shot and Score before clear.");
                    var oldFormation = simulation.Formation;
                    int x            = player.X, steps = oldFormation.StepCount;
                    for (int frame = 0; frame < fps; frame++)
                    {
                        simulation.Advance(1.0 / fps, false, new PlayerInputState(true, true, true));
                    }
                    simulation.Advance(1, true, new PlayerInputState(false, true, true));
                    Require(player.X == x && oldFormation.StepCount == steps && simulation.Score == wave * 990 &&
                        !shot.Active && !shot.FireReady, "Clear must freeze all normal progress and input.");
                    simulation.StartNextWave();
                    Require(simulation.WaveNumber == wave + 1 && !simulation.WaveClearRequired &&
                        !ReferenceEquals(oldFormation, simulation.Formation), "Next wave must replace Formation once.");
                    simulation.StartNextWave();
                    Require(simulation.WaveNumber == wave + 1 && ReferenceEquals(player, simulation.Player) &&
                        ReferenceEquals(shot, simulation.Shot) && player.X == x && simulation.Score == wave * 990 &&
                        !shot.FireReady && !shot.Active, "Next wave must preserve Player/Shot/Score and release gate.");
                    var f = simulation.Formation;
                    Require(f.AliveCount == 55 && f.X == 32 && f.Y == 208 && f.Direction == 1 &&
                        f.StepCount == 0 && f.AnimationFrame == 0, "Formation must reset completely.");
                    foreach (var slot in f.Invaders)
                    {
                        Require(slot.Alive, "Every slot must be Alive.");
                    }
                    simulation.Advance(GameplaySimulation.TickDuration, true, new PlayerInputState(false, false, true));
                    Require(!shot.Active && !shot.FireReady, "Held Space must not auto-fire in next wave.");
                    simulation.Advance(GameplaySimulation.TickDuration, true, default);
                    simulation.Advance(GameplaySimulation.TickDuration, true, new PlayerInputState(false, false, true));
                    Require(shot.Active, "Release then re-press must fire.");
                    shot.Deactivate();
                }
                Debug.Log($"WAVE_LOOP_LOGIC_PASS: fps={fps}; Wave={simulation.WaveNumber}; Score={simulation.Score}; Alive={simulation.Formation.AliveCount}");
            }
        }

        internal static void Clear(GameplaySimulation inSimulation, double inElapsed)
        {
            for (int row = 4; row >= 0; row--)
            {
                for (int column = 0; column < 11; column++)
                {
                    InvaderHitVerification.PrepareShot(inSimulation, inSimulation.Formation.X + column * 16,
                        inSimulation.Formation.Y - row * 16 - 8);
                    inSimulation.Advance(inElapsed, true, new PlayerInputState(false, false, true));
                    // 120fps needs a second frame for a tick.
                    if (inSimulation.Shot.Active)
                    {
                        inSimulation.Advance(inElapsed, true, new PlayerInputState(false, false, true));
                    }
                    Require(!inSimulation.Formation.Invaders[row * 11 + column].Alive && !inSimulation.Shot.Active,
                        "Each wave must be cleared with real shot collisions.");
                }
            }
        }

        internal static void Require(bool inCondition, string inMessage)
        {
            if (inCondition == false)
            {
                throw new InvalidOperationException(inMessage);
            }
        }
    }
}
