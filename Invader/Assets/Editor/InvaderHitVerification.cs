using System;
using UnityEditor;
using UnityEngine;

namespace UnityInvader.Editor
{
    public static class InvaderHitVerification
    {
        private const           double           Tick  = GameplaySimulation.TickDuration;
        private static readonly PlayerInputState _fire = new PlayerInputState(false, false, true);

        [MenuItem("Unity Invader/Verify Invader Hit Logic")]
        public static void Verify()
        {
            VerifyKillsAndScore();
            VerifyTickAndEdges();
            VerifyPause();
            VerifyFrameRates();
            PlayerShotVerification.Verify();
            Debug.Log("INVADER_HIT_LOGIC_PASS: AliveCount; dead exclusion; type/cumulative Score; one Shot/one Invader; hit deactivation/release gate; tick order; per-tick overlap; AABB edges; movement after kills; zero alive; non-Playing; 30/60/120fps; Player/Shot/Formation regression.");
        }

        // Fixture setup advances individual models without moving Formation; actual assertions use the shared tick.
        internal static void PrepareShot(GameplaySimulation inSimulation, int inX, int inY)
        {
            inSimulation.EditorPrepareShot(inX, inY);
        }

        private static void VerifyKillsAndScore()
        {
            var simulation = new GameplaySimulation();
            Require(simulation.EditorCapture().Formation.AliveCount == 55 && simulation.Score == 0, "Initial 55 alive/zero Score required.");
            Hit(simulation, 4, 0);
            Require(simulation.Score == 10 && simulation.EditorCapture().Formation.AliveCount == 54 && !simulation.EditorCapture().Shot.Active,
                "Bottom kill must deactivate Shot, decrement count and add 10.");
            Require(!simulation.EditorKillInvader(44) && simulation.EditorCapture().Formation.AliveCount == 54,
                "Repeated kill must not decrement AliveCount.");
            simulation.Advance(2, true, _fire);
            Require(!simulation.EditorCapture().Shot.Active && simulation.Score == 10, "A hit must not re-arm held Fire.");
            simulation.Advance(Tick / 2, true, default);
            simulation.Advance(Tick / 2, true, _fire);
            Require(simulation.EditorCapture().Shot.Active && simulation.EditorCapture().Shot.Y == 32, "Release then press must launch again.");

            var cumulative = new GameplaySimulation();
            Hit(cumulative, 4, 0);
            Hit(cumulative, 2, 0);
            Require(cumulative.Score == 30, "Bottom then Middle must total 30.");
            Hit(cumulative, 0, 0);
            Require(cumulative.Score == 60 && cumulative.EditorCapture().Formation.AliveCount == 52, "Adding Top must total 60 and 52 alive.");
            Require(cumulative.EditorCapture().Formation.Invaders.Count == 55 && cumulative.EditorCapture().Formation.Invaders[44].Id == 44 &&
                cumulative.EditorCapture().Formation.Invaders[44].Row == 4 && cumulative.EditorCapture().Formation.Invaders[44].Column == 0 &&
                cumulative.EditorCapture().Formation.Invaders[44].Type == InvaderType.Bottom, "Dead slot identity must remain intact.");
            var reference = new InvaderFormation();
            for (int tick = 0; tick < 1203; tick++)
            {
                reference.Tick();
            }
            cumulative.Advance(20, true, default);
            Require(SameFormation(cumulative.EditorCapture().Formation, reference.EditorCapture()) && cumulative.EditorCapture().Formation.AliveCount == 52 &&
                cumulative.Score == 60, "Multiple kills must preserve movement, animation, both edges and fixed bounds.");

            var dead = new GameplaySimulation();
            Hit(dead, 4, 0);
            PrepareShot(dead, 32, 140);
            dead.Advance(Tick, true, _fire);
            Require(dead.EditorCapture().Shot.Active && dead.EditorCapture().Formation.AliveCount == 54 && dead.Score == 10,
                "Dead bottom slot must not consume Shot or score again.");
            dead.Advance(3 * Tick, true, _fire);
            Require(!dead.EditorCapture().Shot.Active && !dead.EditorCapture().Formation.Invaders[33].Alive && dead.Score == 20 &&
                dead.EditorCapture().Formation.AliveCount == 53, "Shot must pass through Dead slot and hit the next Alive slot.");

            var sharedEdge = new GameplaySimulation();
            PrepareShot(sharedEdge, 40, 140);
            sharedEdge.Advance(Tick, true, _fire);
            Require(!sharedEdge.EditorCapture().Formation.Invaders[44].Alive && sharedEdge.EditorCapture().Formation.Invaders[45].Alive &&
                sharedEdge.EditorCapture().Formation.AliveCount == 54 && sharedEdge.Score == 10 && !sharedEdge.EditorCapture().Shot.Active,
                "A 1px Shot crossing two adjacent boxes must kill only the first column.");
            sharedEdge.Advance(0, true, _fire);
            Require(sharedEdge.Score == 10, "A frame without a tick must not evaluate collision/score.");

            var all      = new GameplaySimulation();
            for (int row = 4; row >= 0; row--)
            {
                for (int column = 0; column < 11; column++)
                {
                    Hit(all, row, column);
                }
            }
            Require(all.EditorCapture().Formation.AliveCount == 0 && all.Score == 990, "55 real hits must yield zero alive and 990 Score.");
            all.Advance(1, true, default);
            Require(all.WaveClearRequired && all.EditorCapture().Formation.StepCount == 2 && all.EditorCapture().Formation.Invaders.Count == 55 && all.Score == 990,
                "Zero alive must preserve slots/Score and stop until the controller starts the next wave.");
        }

        private static void VerifyTickAndEdges()
        {
            var spawn = new GameplaySimulation();
            spawn.Advance(Tick, true, new PlayerInputState(false, true, true));
            Require(spawn.EditorCapture().Player.X == 113 && spawn.EditorCapture().Shot.X == 113 && spawn.EditorCapture().Shot.Y == 32,
                "Player must move before Shot spawn.");
            spawn.Advance(Tick, true, new PlayerInputState(false, true, true));
            Require(spawn.EditorCapture().Player.X == 114 && spawn.EditorCapture().Shot.X == 113 && spawn.EditorCapture().Shot.Y == 36,
                "Shared clock must preserve fixed X, 4px/tick and one shot slot.");

            var ordering = new GameplaySimulation();
            ordering.Advance(19 * Tick, true, default);
            PrepareShot(ordering, 24, 140);
            ordering.Advance(Tick, true, _fire);
            Require(ordering.EditorCapture().Formation.X == 34 && ordering.EditorCapture().Formation.AliveCount == 55 && ordering.EditorCapture().Shot.Active,
                "Formation must move before collision, leaving the old left edge behind.");

            var twoTicks = new GameplaySimulation();
            PrepareShot(twoTicks, 32, 140);
            twoTicks.Advance(1.0 / 30, true, _fire);
            Require(twoTicks.EditorCapture().Formation.AliveCount == 54 && twoTicks.Score == 10 && !twoTicks.EditorCapture().Shot.Active,
                "First-tick-only overlap must hit even if the second tick would be above the box.");

            var verticalEdge = new GameplaySimulation();
            PrepareShot(verticalEdge, 32, 132);
            verticalEdge.Advance(Tick, true, _fire);
            Require(verticalEdge.EditorCapture().Shot.Y == 136 && verticalEdge.EditorCapture().Formation.AliveCount == 55,
                "Shot top touching box bottom has no positive overlap.");
            verticalEdge.Advance(Tick, true, _fire);
            Require(verticalEdge.EditorCapture().Formation.AliveCount == 54, "The next 4px tick must overlap, without a vertical gap.");
            foreach (int x in new[] { 23, 201 })
            {
                var miss = new GameplaySimulation();
                PrepareShot(miss, x, 140);
                miss.Advance(Tick, true, _fire);
                Require(miss.EditorCapture().Formation.AliveCount == 55 && miss.Score == 0 && miss.EditorCapture().Shot.Active,
                    "Shot outside the left/right box edges must miss.");
            }
        }

        private static void VerifyPause()
        {
            foreach (GameState state in Enum.GetValues(typeof(GameState)))
            {
                if (state == GameState.Playing)
                {
                    continue;
                }
                var simulation = new GameplaySimulation();
                PrepareShot(simulation, 32, 136);
                simulation.Advance(Tick / 2, true, _fire);
                simulation.Advance(100, state == GameState.Playing, new PlayerInputState(false, true));
                Require(simulation.EditorCapture().Player.X == 32 && simulation.EditorCapture().Shot.Y == 136 && simulation.EditorCapture().Shot.Active &&
                    !simulation.EditorCapture().Shot.FireReady && simulation.EditorCapture().Formation.StepCount == 0 && simulation.Score == 0 &&
                    simulation.EditorCapture().Formation.AliveCount == 55, "Non-Playing must stop Player/Shot/Formation/input/collision/Score.");
                simulation.Advance(Tick / 2, true, _fire);
                Require(simulation.Score == 0 && simulation.EditorCapture().Shot.Y == 136, "Paused fractional time must be discarded.");
                simulation.Advance(Tick / 2, true, _fire);
                Require(simulation.Score == 10 && simulation.EditorCapture().Formation.AliveCount == 54, "Resume must hit on the next full tick.");
            }
        }

        private static void VerifyFrameRates()
        {
            GameplaySimulation reference = null;
            foreach (int fps in new[] { 30, 60, 120 })
            {
                var simulation = new GameplaySimulation();
                for (int frame = 0; frame < fps * 20; frame++)
                {
                    int phase = frame % fps;
                    simulation.Advance(1.0 / fps, true,
                        new PlayerInputState(phase >= fps / 2, phase < fps / 2, phase < fps / 5));
                }
                Require(simulation.Score > 0 && simulation.EditorCapture().Formation.AliveCount < 55, "FPS comparison must exercise real hits.");
                if (reference != null)
                {
                    Require(simulation.EditorCapture().Player.X == reference.EditorCapture().Player.X && simulation.Score == reference.Score &&
                        simulation.EditorCapture().Shot.Active == reference.EditorCapture().Shot.Active && simulation.EditorCapture().Shot.X == reference.EditorCapture().Shot.X &&
                        simulation.EditorCapture().Shot.Y == reference.EditorCapture().Shot.Y && simulation.EditorCapture().Shot.FireReady == reference.EditorCapture().Shot.FireReady &&
                        simulation.EditorCapture().Formation.AliveCount == reference.EditorCapture().Formation.AliveCount && SameFormation(simulation.EditorCapture().Formation, reference.EditorCapture().Formation),
                        "30/60/120fps must match all logical state after 1200 ticks.");
                    for (int id = 0; id < 55; id++)
                    {
                        Require(simulation.EditorCapture().Formation.Invaders[id].Alive == reference.EditorCapture().Formation.Invaders[id].Alive,
                            "Every killed ID must match across frame rates.");
                    }
                }
                reference = simulation;
                Debug.Log($"INVADER_HIT_FPS_PASS: fps={fps}; Score={simulation.Score}; Alive={simulation.EditorCapture().Formation.AliveCount}; PlayerX={simulation.EditorCapture().Player.X}; Shot=({simulation.EditorCapture().Shot.X},{simulation.EditorCapture().Shot.Y},{simulation.EditorCapture().Shot.Active}); Formation=({simulation.EditorCapture().Formation.X},{simulation.EditorCapture().Formation.Y}); Steps={simulation.EditorCapture().Formation.StepCount}");
            }
        }

        private static void Hit(GameplaySimulation inSimulation, int inRow, int inColumn)
        {
            int id = inRow * 11 + inColumn;
            PrepareShot(inSimulation, inSimulation.EditorCapture().Formation.X + inColumn * 16, inSimulation.EditorCapture().Formation.Y - inRow * 16 - 8);
            inSimulation.Advance(Tick, true, _fire);
            Require(!inSimulation.EditorCapture().Formation.Invaders[id].Alive && !inSimulation.EditorCapture().Shot.Active, "Expected target must die and consume Shot.");
        }

        private static bool SameFormation(FormationVerificationState inA, FormationVerificationState inB)
        {
            return inA.X == inB.X && inA.Y == inB.Y && inA.Direction == inB.Direction && inA.StepCount == inB.StepCount &&
                inA.AnimationFrame == inB.AnimationFrame;
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
