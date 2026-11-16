using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BoltRush
{
    public static class BoltRushRegression
    {
        [MenuItem("BoltRush/QA/Validate Level Rules")]
        public static void RunRules()
        {
            var asset = Resources.Load<TextAsset>("levels");
            var pack = JsonUtility.FromJson<LevelPack>(asset.text);
            Require(pack.levels.Count == 150, 0, "expected 150 levels");
            var random = new System.Random(529);
            int blocked = 0, overflows = 0, recoveries = 0, stored = 0, broken = 0, mixedWins = 0;
            var path = new List<int>();
            var free = new List<int>();
            var moves = new List<Sim.Flush>();

            for (int levelIndex = 0; levelIndex < pack.levels.Count; levelIndex++)
            {
                var level = pack.levels[levelIndex];
                int n = level.index;
                Require(n == levelIndex + 1, n, "catalog order");
                var geo = new LevelGeo(level);
                path.Clear();
                Require(new Solver(geo).Solve(Const.DefaultBuffer, 600000, path) == 1, n, "unsolved");
                var standard = new Sim(geo, Const.DefaultBuffer);
                Require(!standard.TryRemove(-1, null, out _, out _) && !standard.TryRemove(geo.N, null, out _, out _),
                    n, "out-of-bounds move accepted");
                Require(standard.OpenExtraBox(-1, true, moves) < 0 && standard.OpenExtraBox(2, false, moves) < 0,
                    n, "invalid basket lane accepted");
                foreach (int screw in path)
                {
                    Require(standard.TryRemove(screw, null, out _, out _), n, "solution replay");
                    CheckTotals(standard, n);
                }
                Require(standard.Won && AllSeated(standard), n, "solution left loose screws");

                for (int trial = 0; trial < 16; trial++)
                {
                    var board = new Sim(geo, Const.DefaultBuffer + (trial % 4 == 0 ? 1 : 0));
                    for (int turn = 0; !board.Won && turn < geo.N + 8; turn++)
                    {
                        free.Clear();
                        int covered = -1;
                        for (int s = 0; s < geo.N; s++)
                        {
                            if (board.IsFree(s)) free.Add(s);
                            else if (!board.IsRemoved(s)) covered = s;
                        }
                        if (covered >= 0)
                        {
                            ulong before = board.removed;
                            Require(!board.TryRemove(covered, null, out _, out _) && board.removed == before,
                                n, "covered screw removed");
                            blocked++;
                        }
                        if (board.Stuck())
                        {
                            overflows++;
                            Require(free.Count > 0 && !board.TryRemove(free[0], null, out _, out _), n, "move accepted after overflow");
                            int prior = board.bufTotal;
                            int lane = board.FindExtraBox(0, true) >= 0 ? 0 : 1;
                            moves.Clear();
                            int box = board.OpenExtraBox(lane, true, moves);
                            if (box < 0) break;
                            Require(board.bufTotal < prior && moves.Exists(f => f.box == box && !f.fromReserve),
                                n, "recovery must remove a buffered screw");
                            recoveries++;
                        }
                        if (trial % 3 == 1 && turn == 3 && board.bufTotal > 0)
                        {
                            board.StoreBuffer();
                            Require(board.bufTotal == 0, n, "store left screws in buffer");
                            CheckTotals(board, n);
                            stored++;
                        }
                        Require(free.Count > 0, n, "no accessible screw on unfinished board");
                        int id = free[random.Next(free.Count)];
                        if (trial % 3 == 2 && turn == 4)
                        {
                            board.BreakPlate(level.screws[id].plate, null);
                            broken++;
                        }
                        else
                            Require(board.TryRemove(id, null, out _, out _), n, "valid move refused");
                        CheckTotals(board, n);
                    }
                    if (!board.Won) continue;
                    Require(AllSeated(board), n, "mixed win left loose screws");
                    mixedWins++;
                }
            }
            Debug.Log($"BoltRush QA PASS: 150 solution replays; {blocked} covered taps; {overflows} overflows; " +
                $"{recoveries} matching revives; {stored} buffers stored; {broken} plates broken; {mixedWins} mixed wins.");
        }

        public static void RunRulesAndBuildQA()
        {
            RunRules();
            BoltRushTools.BuildAndroidQA();
        }

        static bool AllSeated(Sim board) => board.bufTotal == 0 && Array.TrueForAll(board.reserve, count => count == 0);

        static void CheckTotals(Sim board, int level)
        {
            int filled = 0, reserve = 0, buffered = 0;
            foreach (byte count in board.fill)
            {
                Require(count <= Const.BoxSize, level, "basket exceeded capacity");
                filled += count;
            }
            foreach (int count in board.reserve) reserve += count;
            foreach (int count in board.buf) buffered += count;
            Require(board.bufTotal == buffered && buffered <= board.cap &&
                filled + reserve + buffered == Geo.PopCount(board.removed), level, "a screw duplicated or disappeared");
        }

        static void Require(bool condition, int level, string description)
        {
            if (!condition) throw new InvalidOperationException($"BoltRush level {level}: {description}");
        }
    }
}
