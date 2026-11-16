using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoltRush
{
    public static class Const
    {
        public const int ActiveBoxes = 2;
        public const int BoxSize = 3;
        public const int PaletteSize = 6;
        public const int DefaultBuffer = 5;
        public const float BoardHalfX = 4.0f, BoardHalfY = 3.7f;
    }

    [Serializable] public class ScrewData { public float x, y; public int color; public int plate; }
    [Serializable] public class PlateData { public float x, y, w, h, angle; public int layer, tint; }

    [Serializable]
    public class LevelData
    {
        public int index, minBuffer, seed;
        public List<PlateData> plates = new List<PlateData>();
        public List<ScrewData> screws = new List<ScrewData>();
        public List<int> boxes = new List<int>();
    }

    [Serializable] public class LevelPack { public List<LevelData> levels = new List<LevelData>(); }

    public static class Geo
    {
        // The drawn head has radius 60 px in a 128 px sprite, rendered at 0.66 world units.
        public const float ScrewHeadRadius = 0.3094f;
        public static bool Inside(PlateData p, float x, float y, float pad)
        {
            float a = p.angle * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            float dx = x - p.x, dy = y - p.y;
            float lx = dx * c + dy * s;
            float ly = -dx * s + dy * c;
            return Mathf.Abs(lx) <= p.w * 0.5f + pad && Mathf.Abs(ly) <= p.h * 0.5f + pad;
        }

        public static int GlassStyle(int level, int plate) => (level * 3 + plate * 5) % 8;

        public static bool GlassContains(PlateData plate, int style, float x, float y, float padding)
        {
            float angle = plate.angle * Mathf.Deg2Rad;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float dx = x - plate.x, dy = y - plate.y;
            float u = 2f * (dx * c + dy * s) / plate.w;
            float v = 2f * (-dx * s + dy * c) / plate.h;
            return GlassDistance(u, v, style, plate.h > 1.1f) >= -2f * padding / plate.h;
        }

        public static bool GlassCoversHead(PlateData plate, int style, float x, float y)
            => GlassCoversHead(plate, style, x, y, plate.x, plate.y, plate.angle);

        public static bool GlassCoversHead(PlateData plate, int style, float x, float y,
            float plateX, float plateY, float plateAngle)
        {
            float angle = plateAngle * Mathf.Deg2Rad;
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            float dx = x - plateX, dy = y - plateY;
            float lx = dx * c + dy * s, ly = -dx * s + dy * c;
            const float r = ScrewHeadRadius;
            if (MathF.Abs(lx) > plate.w * 0.5f + r || MathF.Abs(ly) > plate.h * 0.5f + r) return false;

            float scaleX = 2f / plate.w, scaleY = 2f / plate.h;
            bool tall = plate.h > 1.1f;
            const int divisions = 10;
            for (int iy = -divisions; iy <= divisions; iy++)
            for (int ix = -divisions; ix <= divisions; ix++)
            {
                if (ix * ix + iy * iy > divisions * divisions) continue;
                if (GlassDistance((lx + ix * (r / divisions)) * scaleX,
                    (ly + iy * (r / divisions)) * scaleY, style, tall) >= 0f) return true;
            }
            return false;
        }

        public static float GlassDistance(float u, float v, int style, bool tall)
        {
            float a = MathF.Abs(u), b = MathF.Abs(v);
            if (!tall)
            {
                float xl = 0.96f - a;
                float roof;
                switch (style)
                {
                    case 0: roof = 0.93f - 0.41f * MathF.Pow(MathF.Sin(4.6f * u), 2); break; // three lobes
                    case 1: roof = 0.68f + 0.28f * MathF.Cos(3.14f * u); break; // spindle
                    case 2: roof = 0.72f - 0.17f * MathF.Cos(6.28f * u); break; // bow tie
                    case 3: roof = 0.84f - 0.13f * MathF.Cos(7.4f * u); break; // ribbon
                    case 4: roof = 0.83f - 0.36f * MathF.Max(0f, a - 0.55f); break; // facets
                    case 5: roof = 0.63f + 0.28f * MathF.Sin(3.14f * a); break; // linked moons
                    case 6: roof = 0.58f + 0.36f * MathF.Abs(MathF.Cos(4.65f * u)); break; // beads
                    default: roof = 0.89f - 0.41f * a; break; // tapered blade
                }
                float shift = style == 3 ? 0.19f * MathF.Sin(u * 4.9f) : style == 7 ? 0.20f * u : 0f;
                return MathF.Min(xl - 0.15f * b, roof - MathF.Abs(v - shift));
            }

            float screwBed = 1f - MathF.Sqrt(MathF.Pow((a - 0.63f) / 0.28f, 2f) +
                MathF.Pow((b - 0.53f) / 0.35f, 2f));
            return MathF.Max(screwBed * 0.28f, TallGlassDistance(u, v, a, b, style));
        }

        static float TallGlassDistance(float u, float v, float a, float b, int style)
        {
            float radius = MathF.Sqrt(u * u + v * v);
            float angle = MathF.Atan2(v, u);
            float contour;
            switch (style)
            {
                case 0: contour = 0.86f - 0.11f * MathF.Cos(4f * angle); break; // four petals
                case 1: contour = 0.84f + 0.12f * MathF.Cos(6f * angle); break; // rosette
                case 2: return MathF.Min(0.97f - a, MathF.Min(0.93f - b, 1.36f - a - b)); // jewel
                case 3: return MathF.Min(0.98f - a, 0.82f - MathF.Abs(v - 0.16f * MathF.Sin(4.5f * u)) - 0.17f * a); // wave
                case 4: contour = 0.82f + 0.10f * MathF.Sin(5f * angle); break; // star
                case 5: return MathF.Min(0.97f - a, 0.93f - b - 0.30f * MathF.Exp(-u * u * 15f)); // double leaf
                case 6: contour = 0.86f + 0.10f * MathF.Cos(3f * angle + 0.8f); break; // trefoil
                default: return MathF.Min(0.98f - a, MathF.Min(0.92f - b, 1.38f - MathF.Abs(u - 0.22f * v) - b)); // cut prism
            }
            return MathF.Min(contour - radius, 0.99f - MathF.Max(a, b));
        }

        public static int PopCount(ulong v)
        {
            int n = 0;
            while (v != 0) { v &= v - 1; n++; }
            return n;
        }
    }

    // Static geometry of one level: which screws are covered by which plates.
    public sealed class LevelGeo
    {
        public readonly LevelData L;
        public readonly int N;
        public readonly ulong[] PlateMask;
        public readonly ulong[] CoverMask;
        public readonly ulong All;

        public LevelGeo(LevelData l)
        {
            L = l;
            N = l.screws.Count;
            if (N > 63) throw new InvalidOperationException("Too many screws");
            PlateMask = new ulong[l.plates.Count];
            CoverMask = new ulong[N];
            for (int s = 0; s < N; s++) PlateMask[l.screws[s].plate] |= 1UL << s;
            All = (1UL << N) - 1UL;
            for (int s = 0; s < N; s++)
            {
                var sd = l.screws[s];
                for (int q = sd.plate + 1; q < l.plates.Count; q++)
                    if (Geo.GlassCoversHead(l.plates[q], Geo.GlassStyle(l.index, q), sd.x, sd.y)) CoverMask[s] |= PlateMask[q];
            }
        }
    }

    // Game rules. The same class drives the real game and the solver, so they can never disagree.
    public sealed class Sim
    {
        public struct Flush { public int color, box, slot; public bool fromReserve; }

        public readonly LevelGeo geo;
        public ulong removed;
        public byte[] fill;
        public int[] buf = new int[Const.PaletteSize];
        public int[] reserve = new int[Const.PaletteSize];
        int[] extraBoxes = { -1, -1 };
        public const int ExtraLaneCount = 2;
        public int bufTotal;
        public int cap;

        public Sim(LevelGeo g, int cap)
        {
            geo = g;
            this.cap = cap;
            fill = new byte[g.L.boxes.Count];
        }

        public Sim Clone()
        {
            var c = (Sim)MemberwiseClone();
            c.fill = (byte[])fill.Clone();
            c.buf = (int[])buf.Clone();
            c.reserve = (int[])reserve.Clone();
            c.extraBoxes = (int[])extraBoxes.Clone();
            return c;
        }

        public bool Won { get { return removed == geo.All; } }
        public bool IsRemoved(int s) { return ((removed >> s) & 1UL) != 0; }
        public bool IsFree(int s) => (uint)s < (uint)geo.N && !IsRemoved(s) && (geo.CoverMask[s] & ~removed) == 0;
        public int Remaining(int plate) { return Geo.PopCount(geo.PlateMask[plate] & ~removed); }

        // Index of the active box that accepts this color, or -1.
        public int Target(int color)
        {
            int seen = 0;
            var boxes = geo.L.boxes;
            for (int j = 0; j < boxes.Count; j++)
            {
                if (ExtraLaneOf(j) >= 0) continue;
                if (fill[j] >= Const.BoxSize) continue;
                if (seen >= Const.ActiveBoxes) break;
                seen++;
                if (boxes[j] == color) return j;
            }
            for (int lane = 0; lane < ExtraLaneCount; lane++)
                if (IsExtraLaneOpen(lane) && boxes[extraBoxes[lane]] == color) return extraBoxes[lane];
            return -1;
        }

        public void ActiveBoxes(List<int> into)
        {
            into.Clear();
            for (int j = 0; j < fill.Length && into.Count < Const.ActiveBoxes; j++)
                if (ExtraLaneOf(j) < 0 && fill[j] < Const.BoxSize) into.Add(j);
            for (int lane = 0; lane < ExtraLaneCount; lane++)
                if (IsExtraLaneOpen(lane)) into.Add(extraBoxes[lane]);
        }

        public int ExtraLaneOf(int box)
        {
            if (box < 0) return -1;
            for (int lane = 0; lane < ExtraLaneCount; lane++)
                if (extraBoxes[lane] == box) return lane;
            return -1;
        }

        public bool IsExtraLaneOpen(int lane) => extraBoxes[lane] >= 0 && fill[extraBoxes[lane]] < Const.BoxSize;

        public int FindExtraBox(int lane, bool mustMatchBuffer)
        {
            if ((uint)lane >= ExtraLaneCount || IsExtraLaneOpen(lane)) return -1;
            int active = 0, fallback = -1;
            for (int j = 0; j < fill.Length; j++)
            {
                if (fill[j] >= Const.BoxSize) continue;
                if (ExtraLaneOf(j) >= 0) continue;
                if (active++ < Const.ActiveBoxes) continue;
                if (fallback < 0) fallback = j;
                if (buf[geo.L.boxes[j]] > 0) return j;
            }
            return mustMatchBuffer ? -1 : fallback;
        }

        public int OpenExtraBox(int lane, bool mustMatchBuffer, List<Flush> flushes)
        {
            int index = FindExtraBox(lane, mustMatchBuffer);
            if (index < 0) return -1;
            extraBoxes[lane] = index;
            FlushBuffer(flushes);
            return index;
        }

        public void StoreBuffer()
        {
            for (int color = 0; color < buf.Length; color++)
            {
                reserve[color] += buf[color];
                buf[color] = 0;
            }
            bufTotal = 0;
        }

        public void BreakPlate(int plate, List<Flush> flushes)
        {
            for (int i = 0; i < geo.N; i++)
            {
                if (geo.L.screws[i].plate != plate || IsRemoved(i)) continue;
                removed |= 1UL << i;
                reserve[geo.L.screws[i].color]++;
            }
            FlushBuffer(flushes);
        }

        public bool TryRemove(int s, List<Flush> flushes, out int box, out int slot)
        {
            box = -1; slot = -1;
            if (Stuck() || !IsFree(s)) return false;
            int c = geo.L.screws[s].color;
            int t = Target(c);
            if (t >= 0) { slot = fill[t]; fill[t]++; box = t; }
            else if (bufTotal < cap) { buf[c]++; bufTotal++; }
            else return false;
            removed |= 1UL << s;
            FlushBuffer(flushes);
            return true;
        }

        void FlushBuffer(List<Flush> flushes)
        {
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int c = 0; c < Const.PaletteSize; c++)
                {
                    while (buf[c] > 0 || reserve[c] > 0)
                    {
                        int t = Target(c);
                        if (t < 0) break;
                        bool fromReserve = buf[c] == 0;
                        if (flushes != null) flushes.Add(new Flush { color = c, box = t, slot = fill[t], fromReserve = fromReserve });
                        fill[t]++;
                        if (fromReserve) reserve[c]--;
                        else { buf[c]--; bufTotal--; }
                        changed = true;
                    }
                }
            }
        }

        public bool Stuck() => !Won && cap > 0 && bufTotal >= cap;

        public ulong Hash()
        {
            ulong h = 1469598103934665603UL;
            for (int i = 0; i < fill.Length; i++) { h ^= fill[i]; h *= 1099511628211UL; }
            for (int c = 0; c < buf.Length; c++) { h ^= (ulong)(buf[c] + 7); h *= 1099511628211UL; }
            return h;
        }
    }

    public sealed class Solver
    {
        readonly LevelGeo g;
        int nodes, nodeCap;
        HashSet<(ulong, ulong)> dead;

        public Solver(LevelGeo geo) { g = geo; }

        // 1 solved, 0 unsolvable with this buffer, -1 node cap hit.
        public int Solve(int bufferCap, int cap, List<int> path)
        {
            nodes = 0; nodeCap = cap;
            dead = new HashSet<(ulong, ulong)>();
            return Dfs(new Sim(g, bufferCap), path);
        }

        // Smallest buffer that still allows a win. -1 = search too big, -2 = unsolvable.
        public int MinBuffer(int maxCap, int cap)
        {
            for (int b = 0; b <= maxCap; b++)
            {
                int r = Solve(b, cap, null);
                if (r == 1) return b;
                if (r == -1) return -1;
            }
            return -2;
        }

        int Dfs(Sim s, List<int> path)
        {
            if (s.Won) return 1;
            if (s.Stuck()) return 0;
            if (++nodes > nodeCap) return -1;
            var key = (s.removed, s.Hash());
            if (dead.Contains(key)) return 0;

            var free = new List<int>(8);
            int forced = -1;
            for (int i = 0; i < g.N; i++)
            {
                if (!s.IsFree(i)) continue;
                free.Add(i);
                if (forced < 0 && s.Target(g.L.screws[i].color) >= 0) forced = i;
            }

            // Delivering into a waiting box is never worse than waiting, so it is a forced move.
            if (forced >= 0)
            {
                var c = s.Clone();
                int b, sl;
                c.TryRemove(forced, null, out b, out sl);
                if (path != null) path.Add(forced);
                int r = Dfs(c, path);
                if (r == 1) return 1;
                if (path != null) path.RemoveAt(path.Count - 1);
                if (r == 0) dead.Add(key);
                return r;
            }

            if (s.bufTotal >= s.cap) { dead.Add(key); return 0; }

            free.Sort((a, b2) => s.Remaining(g.L.screws[a].plate).CompareTo(s.Remaining(g.L.screws[b2].plate)));
            bool capHit = false;
            for (int k = 0; k < free.Count; k++)
            {
                var c = s.Clone();
                int b, sl;
                c.TryRemove(free[k], null, out b, out sl);
                if (path != null) path.Add(free[k]);
                int r = Dfs(c, path);
                if (r == 1) return 1;
                if (path != null) path.RemoveAt(path.Count - 1);
                if (r == -1) capHit = true;
            }
            if (!capHit) dead.Add(key);
            return capHit ? -1 : 0;
        }
    }

    public static class Generator
    {
        public static int LastBuildNull, LastTried;

        static void Shuffle<T>(IList<T> list, System.Random r)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = r.Next(i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }

        public static int TargetBuffer(int n) { return Mathf.Clamp(1 + (n - 1) / 6, 1, 4); }

        // Levels are solvable by construction: a valid unscrewing order is generated first and colors are
        // assigned along it. Difficulty is then raised by swapping colors while the level stays solvable,
        // until the smallest buffer that still wins reaches the target for this level number.
        public static LevelData Make(int n, int attempts, int seedOffset = 0)
        {
            int target = TargetBuffer(n);
            LevelData best = null;
            int bestScore = int.MaxValue;
            LastBuildNull = 0; LastTried = 0;
            for (int a = 0; a < attempts; a++)
            {
                var r = new System.Random(n * 7919 + a * 104729 + 13 + seedOffset);
                var L = Build(n, r);
                if (L == null) { LastBuildNull++; continue; }
                var geo = new LevelGeo(L);
                var probe = new Sim(geo, Const.DefaultBuffer);
                int free = 0;
                for (int i = 0; i < geo.N; i++) if (probe.IsFree(i)) free++;
                if (free < (n <= 3 ? 3 : 2)) continue;

                var order = RandomOrder(geo, r);
                for (int i = 0; i < geo.N; i++) L.screws[order[i]].color = L.boxes[i / Const.BoxSize];
                LastTried++;

                var solver = new Solver(geo);
                int cur = solver.MinBuffer(Const.DefaultBuffer, 30000);
                if (cur < 0) continue;

                int steps = 0;
                while (cur < target && steps < 120)
                {
                    steps++;
                    int i = r.Next(geo.N), j = r.Next(geo.N);
                    int ci = L.screws[i].color, cj = L.screws[j].color;
                    if (ci == cj) continue;
                    L.screws[i].color = cj; L.screws[j].color = ci;
                    int mb = solver.MinBuffer(Const.DefaultBuffer, 12000);
                    if (mb >= cur && mb <= target) cur = mb;
                    else { L.screws[i].color = ci; L.screws[j].color = cj; }
                }

                int score = Mathf.Abs(cur - target) * 10 + a;
                if (score < bestScore)
                {
                    best = CloneLevel(L);
                    best.index = n; best.minBuffer = cur; best.seed = a;
                    bestScore = score;
                }
                if (cur == target) return best;
            }
            return best;
        }

        static LevelData CloneLevel(LevelData s)
        {
            var c = new LevelData();
            foreach (var p in s.plates) c.plates.Add(new PlateData { x = p.x, y = p.y, w = p.w, h = p.h, angle = p.angle, layer = p.layer, tint = p.tint });
            foreach (var q in s.screws) c.screws.Add(new ScrewData { x = q.x, y = q.y, color = q.color, plate = q.plate });
            c.boxes.AddRange(s.boxes);
            return c;
        }

        static List<int> RandomOrder(LevelGeo geo, System.Random r)
        {
            var sim = new Sim(geo, Const.DefaultBuffer);
            var order = new List<int>();
            var free = new List<int>();
            while (order.Count < geo.N)
            {
                free.Clear();
                for (int i = 0; i < geo.N; i++) if (sim.IsFree(i)) free.Add(i);
                int pick = free[r.Next(free.Count)];
                order.Add(pick);
                sim.removed |= 1UL << pick;
            }
            return order;
        }

        static Vector2[] Shape(int k, out float w, out float h)
        {
            if (k == 2) { w = 2.7f; h = 0.95f; return new[] { new Vector2(-1f, 0f), new Vector2(1f, 0f) }; }
            if (k == 3) { w = 3.9f; h = 0.95f; return new[] { new Vector2(-1.4f, 0f), new Vector2(0f, 0f), new Vector2(1.4f, 0f) }; }
            w = 2.7f; h = 1.9f;
            return new[] { new Vector2(-0.85f, -0.5f), new Vector2(0.85f, -0.5f), new Vector2(-0.85f, 0.5f), new Vector2(0.85f, 0.5f) };
        }

        static float PickAngle(System.Random r)
        {
            double u = r.NextDouble();
            if (u < 0.35) return (r.NextDouble() < 0.5 ? 0f : 90f) + (float)(r.NextDouble() * 12 - 6);
            return (float)(r.NextDouble() * 160 - 80);
        }

        static LevelData Build(int n, System.Random r)
        {
            int S = Mathf.Min(9 + 3 * ((n - 1) / 2), 42);
            int G = S / 3;
            int maxC = Mathf.Clamp(3 + (n - 1) / 4, 3, Const.PaletteSize);
            int C = Mathf.Min(maxC, G);

            var pal = new List<int> { 0, 1, 2, 3, 4, 5 };
            Shuffle(pal, r);
            pal.RemoveRange(C, pal.Count - C);
            var per = new int[C];
            for (int i = 0; i < C; i++) per[i] = 1;
            for (int k = 0; k < G - C; k++) per[r.Next(C)]++;

            var L = new LevelData();
            for (int i = 0; i < C; i++)
                for (int j = 0; j < per[i]; j++) L.boxes.Add(pal[i]);
            Shuffle(L.boxes, r);

            var counts = new List<int>();
            int rem = S;
            while (rem > 0)
            {
                int k;
                if (rem <= 4) k = rem;
                else
                {
                    double u = r.NextDouble();
                    k = u < 0.3 ? 2 : (u < 0.75 ? 3 : 4);
                    if (rem - k == 1) k = 3;
                }
                counts.Add(k);
                rem -= k;
            }
            Shuffle(counts, r);

            const float limX = 4.0f, limY = 3.7f;
            for (int i = 0; i < counts.Count; i++)
            {
                float w, h;
                var offs = Shape(counts[i], out w, out h);
                bool placed = false;
                for (int t = 0; t < 400 && !placed; t++)
                {
                    float ang = PickAngle(r);
                    float px = (float)(r.NextDouble() * 7.0 - 3.5);
                    float py = (float)(r.NextDouble() * 6.4 - 3.2);
                    float ca = Mathf.Cos(ang * Mathf.Deg2Rad), sa = Mathf.Sin(ang * Mathf.Deg2Rad);

                    bool ok = true;
                    for (int sx = -1; sx <= 1 && ok; sx += 2)
                        for (int sy = -1; sy <= 1 && ok; sy += 2)
                        {
                            float lx = sx * w * 0.5f, ly = sy * h * 0.5f;
                            float wx = px + lx * ca - ly * sa, wy = py + lx * sa + ly * ca;
                            if (Mathf.Abs(wx) > limX || Mathf.Abs(wy) > limY) ok = false;
                        }
                    if (!ok) continue;

                    var pts = new Vector2[offs.Length];
                    for (int q = 0; q < offs.Length && ok; q++)
                    {
                        pts[q] = new Vector2(px + offs[q].x * ca - offs[q].y * sa, py + offs[q].x * sa + offs[q].y * ca);
                        for (int e = 0; e < L.screws.Count; e++)
                            if (Vector2.Distance(pts[q], new Vector2(L.screws[e].x, L.screws[e].y)) < 0.72f) { ok = false; break; }
                    }
                    if (!ok) continue;

                    var plate = new PlateData { x = px, y = py, w = w, h = h, angle = ang, layer = i, tint = r.Next(6) };
                    int covered = 0;
                    for (int e = 0; e < L.screws.Count; e++)
                        if (Geo.Inside(plate, L.screws[e].x, L.screws[e].y, 0.1f)) covered++;
                    // Every plate above the first should cover something, otherwise the board is flat and easy.
                    if (i > 0 && covered == 0 && t < 300) continue;

                    L.plates.Add(plate);
                    for (int q = 0; q < pts.Length; q++)
                        L.screws.Add(new ScrewData { x = pts[q].x, y = pts[q].y, color = 0, plate = i });
                    placed = true;
                }
                if (!placed) return null;
            }
            return L;
        }
    }
}
