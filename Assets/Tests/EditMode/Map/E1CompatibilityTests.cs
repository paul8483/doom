using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Doom.Wad;
using Doom.Map;
using Doom.Specials;
using Doom.Things;
using Doom.Game;

namespace Doom.Map.Tests
{
    public enum CompatibilityStatus
    {
        Implemented,
        HarmlessVisual,
        ProgressionBlocker,
        Unsupported
    }

    public readonly struct CompatibilityEntry
    {
        public readonly string Kind; // "linedef" / "sector" / "thing"
        public readonly int Type;
        public readonly CompatibilityStatus Status;
        public readonly string Note;

        public CompatibilityEntry(string kind, int type, CompatibilityStatus status, string note)
        {
            Kind = kind;
            Type = type;
            Status = status;
            Note = note;
        }

        public override string ToString() =>
            $"{Kind} {Type}: {Status} — {Note}";
    }

    /// Baseline E1 compatibility matrix for Stage 7. Classifies every linedef
    /// special, sector special, and thing type on E1M1–E1M9. Fails on unknown
    /// types that cannot be classified; known ProgressionBlockers are reported
    /// (cleared later by Tasks 3/14) and must not be reclassified as harmless.
    public class E1CompatibilityTests
    {
        static string FreedoomPath =>
            Path.Combine(Application.streamingAssetsPath, "wads", "freedoom1.wad");

        static readonly string[] E1Maps =
        {
            "E1M1", "E1M2", "E1M3", "E1M4", "E1M5", "E1M6", "E1M7", "E1M8", "E1M9"
        };

        [Test]
        public void E1_maps_load_and_every_type_is_classified()
        {
            using var wad = WadFile.Open(FreedoomPath);

            var lineTypes = new SortedSet<int>();
            var sectorTypes = new SortedSet<int>();
            var thingTypes = new SortedSet<int>();

            foreach (string mapName in E1Maps)
            {
                var map = MapData.Load(wad, mapName);
                Assert.That(map.Things.Any(t => t.Type == 1), Is.True,
                    $"{mapName} must have a Player 1 start");

                foreach (var ld in map.LineDefs)
                    if (ld.Special != 0) lineTypes.Add(ld.Special);
                foreach (var s in map.Sectors)
                    if (s.Special != 0) sectorTypes.Add(s.Special);
                foreach (var t in map.Things)
                    thingTypes.Add(t.Type);
            }

            var entries = new List<CompatibilityEntry>();
            foreach (int t in lineTypes)
                entries.Add(ClassifyLinedef(t));
            foreach (int t in sectorTypes)
                entries.Add(ClassifySector(t));
            foreach (int t in thingTypes)
                entries.Add(ClassifyThing(t));

            var blockers = entries
                .Where(e => e.Status == CompatibilityStatus.ProgressionBlocker)
                .ToList();
            var unsupported = entries
                .Where(e => e.Status == CompatibilityStatus.Unsupported)
                .ToList();

            var report = new StringBuilder();
            report.AppendLine("=== E1 compatibility matrix (freedoom1.wad) ===");
            report.AppendLine($"Linedef specials: {lineTypes.Count}, sector: {sectorTypes.Count}, things: {thingTypes.Count}");
            foreach (var e in entries)
                report.AppendLine(e.ToString());
            report.AppendLine("--- Progression blockers ---");
            foreach (var e in blockers)
                report.AppendLine(e.ToString());
            Debug.Log(report.ToString());
            TestContext.WriteLine(report.ToString());

            // Unknown classification is represented by Unsupported with note starting "unknown"
            var unknown = entries.Where(e =>
                e.Note != null &&
                e.Note.StartsWith("unknown", StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.That(unknown, Is.Empty,
                "Unclassified E1 types (potential blockers):\n" +
                string.Join("\n", unknown.Select(e => e.ToString())));

            // Baseline: Exit linedefs must be classified. After Task 3 they are
            // Implemented (IsExecutable); before that they were ProgressionBlocker.
            bool hasExit = entries.Any(e => e.Kind == "linedef" && IsExitSpecial(e.Type));
            Assert.That(hasExit, Is.True, "E1 must contain exit linedefs.");
            var exitEntries = entries.Where(e => e.Kind == "linedef" && IsExitSpecial(e.Type)).ToList();
            Assert.That(exitEntries.All(e =>
                    e.Status == CompatibilityStatus.Implemented ||
                    e.Status == CompatibilityStatus.ProgressionBlocker),
                Is.True,
                "Exit linedefs must be Implemented or ProgressionBlocker, never HarmlessVisual.");
        }

        [Test]
        public void E1_campaign_route_targets_exist_in_wad()
        {
            using var wad = WadFile.Open(FreedoomPath);
            var available = new List<string>();
            foreach (var lump in wad.Directory)
            {
                if (WadMapNames.IsMapMarker(lump.Name) &&
                    CampaignRoute.TryNormalize(lump.Name, out string canonical) &&
                    canonical.StartsWith("E1", StringComparison.Ordinal))
                    available.Add(canonical);
            }

            foreach (string map in E1Maps)
                Assert.That(available, Does.Contain(map), $"Missing {map} in freedoom1.wad");

            // Every normal/secret hop resolves without throw against the real WAD set.
            foreach (string map in E1Maps)
            {
                var normal = CampaignRoute.Resolve(map, ExitKind.Normal, available);
                if (map == "E1M8")
                    Assert.That(normal.Outcome, Is.EqualTo(CampaignOutcome.EpisodeComplete));
                else
                    Assert.That(available, Does.Contain(normal.NextMap));

                var secret = CampaignRoute.Resolve(map, ExitKind.Secret, available);
                Assert.That(secret.Outcome, Is.EqualTo(CampaignOutcome.NextMap));
                Assert.That(available, Does.Contain(secret.NextMap));
            }
        }

        public static CompatibilityEntry ClassifyLinedef(int type)
        {
            if (!LineSpecialTable.TryGet(type, out var sp))
                return new CompatibilityEntry("linedef", type, CompatibilityStatus.Unsupported,
                    "unknown linedef special not in LineSpecialTable");

            if (sp.IsExecutable)
                return new CompatibilityEntry("linedef", type, CompatibilityStatus.Implemented,
                    $"{sp.Category} (executable)");

            switch (sp.Category)
            {
                case SpecialCategory.Exit:
                    return new CompatibilityEntry("linedef", type, CompatibilityStatus.ProgressionBlocker,
                        "Exit classified but not yet executed (Task 3)");
                case SpecialCategory.Teleport:
                    return new CompatibilityEntry("linedef", type, CompatibilityStatus.ProgressionBlocker,
                        "Teleport classified but not yet executed (Task 14)");
                case SpecialCategory.Crusher:
                    // No crusher linedefs on Freedoom E1; keep classified for Doom II maps.
                    return new CompatibilityEntry("linedef", type, CompatibilityStatus.Unsupported,
                        "Crusher classified; not present on E1 (deferred)");
                case SpecialCategory.Donut:
                    return new CompatibilityEntry("linedef", type, CompatibilityStatus.Unsupported,
                        "Donut classified; not present on E1 (deferred)");
                case SpecialCategory.Light:
                    return new CompatibilityEntry("linedef", type, CompatibilityStatus.HarmlessVisual,
                        "Light special — cosmetic (static lightlevel still applies)");
                case SpecialCategory.Scroll:
                    return new CompatibilityEntry("linedef", type, CompatibilityStatus.HarmlessVisual,
                        "Scroll special — cosmetic UV motion deferred");
                default:
                    return new CompatibilityEntry("linedef", type, CompatibilityStatus.Unsupported,
                        $"unknown non-executable category {sp.Category}");
            }
        }

        public static CompatibilityEntry ClassifySector(int special)
        {
            // Damaging floors (Stage 6b). Special 11 also exits at low HP (Task 3).
            if (SectorDamageTable.DamagePerTick(special) > 0)
            {
                if (special == 11)
                    return new CompatibilityEntry("sector", special, CompatibilityStatus.Implemented,
                        "floor damage + low-HP exit (special 11)");
                return new CompatibilityEntry("sector", special, CompatibilityStatus.Implemented,
                    $"floor damage {SectorDamageTable.DamagePerTick(special)}/tic");
            }

            switch (special)
            {
                case 9:
                    return new CompatibilityEntry("sector", special, CompatibilityStatus.Implemented,
                        "secret sector — counted by LevelStatsTracker");
                case 1: case 2: case 3: case 8: case 12: case 13: case 17:
                    return new CompatibilityEntry("sector", special, CompatibilityStatus.HarmlessVisual,
                        "sector light effect — static lightlevel still applies");
                case 10: case 14:
                    // Not present on Freedoom E1; keep classified for completeness.
                    return new CompatibilityEntry("sector", special, CompatibilityStatus.Unsupported,
                        "timed door sector special not executed (not on E1)");
                default:
                    return new CompatibilityEntry("sector", special, CompatibilityStatus.Unsupported,
                        "unknown sector special");
            }
        }

        public static CompatibilityEntry ClassifyThing(int type)
        {
            // Spawn points
            if (type >= 1 && type <= 4)
                return new CompatibilityEntry("thing", type, CompatibilityStatus.Implemented,
                    $"player {type} start");
            if (type == 11)
                return new CompatibilityEntry("thing", type, CompatibilityStatus.HarmlessVisual,
                    "deathmatch start (ignored)");
            if (type == 14)
                return new CompatibilityEntry("thing", type, CompatibilityStatus.Implemented,
                    "teleport destination (TeleportRules / TeleportExecutor)");

            if (MonsterTable.TryGet(type, out _))
                return new CompatibilityEntry("thing", type, CompatibilityStatus.Implemented,
                    "E1 monster AI");

            if (ItemRules.IsPickup(type))
                return new CompatibilityEntry("thing", type, CompatibilityStatus.Implemented,
                    "E1 pickup");

            if (ThingTable.TryGet(type, out var def))
            {
                if (def.Has(ThingFlags.CountKill))
                    return new CompatibilityEntry("thing", type, CompatibilityStatus.ProgressionBlocker,
                        $"CountKill monster '{def.Sprite}' without MonsterTable AI");

                if (def.Has(ThingFlags.Shootable) && type == BarrelRules.DoomEdNum)
                    return new CompatibilityEntry("thing", type, CompatibilityStatus.Implemented,
                        "barrel HP + BEXP splash");

                // Known pickups not wired in ItemRules (chainsaw, rockets, invuln, …)
                if (IsDeferredPickup(type))
                    return new CompatibilityEntry("thing", type, CompatibilityStatus.Unsupported,
                        $"pickup '{def.Sprite}' not in Stage 6e ItemRules");

                return new CompatibilityEntry("thing", type, CompatibilityStatus.HarmlessVisual,
                    $"decoration '{def.Sprite}'");
            }

            return new CompatibilityEntry("thing", type, CompatibilityStatus.Unsupported,
                "unknown thing type not in ThingTable");
        }

        static bool IsExitSpecial(int type) =>
            LineSpecialTable.TryGet(type, out var sp) && sp.Category == SpecialCategory.Exit;

        static bool IsDeferredPickup(int type)
        {
            switch (type)
            {
                case 2005: // chainsaw
                case 2003: // rocket launcher
                case 2004: // plasma
                case 2006: // BFG
                case 82:   // super shotgun
                case 2010: case 2046: // rockets
                case 2047: case 17:   // cells
                case 2022: // invuln
                case 2024: // invis
                case 2026: // map
                case 2045: // light amp
                case 83:   // megasphere
                    return true;
                default:
                    return false;
            }
        }
    }
}
