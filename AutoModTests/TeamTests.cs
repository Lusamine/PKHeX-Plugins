﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using Xunit;
using static PKHeX.Core.GameVersion;

namespace AutoModTests
{
    /// <summary>
    /// Class to test multiple large pastebins with a mix of legal/illegal mons
    /// </summary>
    public static class TeamTests
    {
        static TeamTests() => TestUtil.InitializePKHeXEnvironment();
        private static string TestPath => TestUtil.GetTestFolder("ShowdownSets");
        private static string LogDirectory => Path.Combine(Directory.GetCurrentDirectory(), "logs");

        private static Dictionary<GameVersion, Dictionary<string, RegenTemplate[]>> RunVerification(string file, GameVersion[] saves)
        {
            var results = new Dictionary<GameVersion, Dictionary<string, RegenTemplate[]>>();
            foreach (var s in saves)
            {
                var legalsets = new List<RegenTemplate>();
                var illegalsets = new List<RegenTemplate>();
                var sav = SaveUtil.GetBlankSAV(s, "ALMUT");
                RecentTrainerCache.SetRecentTrainer(sav);

                var lines = File.ReadAllLines(file).Where(z => !z.StartsWith("====="));

                // Edge case checks for transfers
                // Test PA8 files in BD without moves
                lines = file.Contains("pa8") && (s == BD || s == SP) ? lines.Where(z => !z.StartsWith("- ")) : lines;

                // Filter sets based on if they are present in destination game
                var filter = ShowdownParsing.GetShowdownSets(lines).Distinct(new ShowdownSetComparator()).Where(z => sav.Personal.IsPresentInGame(z.Species, z.Form));
                if (file.Contains("pa8") && (s == BD || s == SP))
                    filter = filter.Where(z => !(z.Species == (int)Species.Giratina && z.Form == 1)); // Giratina Origin from PLA has no item so will fail in BDSP
                var sets = filter.ToList();

                for (int i = 0; i < sets.Count; i++)
                {
                    var set = sets[i];
                    try
                    {
                        Debug.Write($"Checking Set {i:000} [Species: {(Species)set.Species}] from File {file} using Save {s}: ");
                        var regen = new RegenTemplate(set, sav.Generation);
                        var pk = sav.GetLegalFromSet(regen, out _);
                        var la = new LegalityAnalysis(pk);
                        if (la.Valid)
                        {
                            Debug.WriteLine("Valid");
                            legalsets.Add(regen);
                        }
                        else
                        {
                            illegalsets.Add(regen);
                            Debug.WriteLine($"Invalid Set for {(Species)set.Species} in file {file} with set: {set.Text}");
                        }
                    }
                    catch
                    {
                        Debug.WriteLine($"Exception for {(Species)set.Species} in file {file} with set: {set.Text}");
                    }
                }
                results[s] = new Dictionary<string, RegenTemplate[]> { { "legal", legalsets.ToArray() }, { "illegal", illegalsets.ToArray() } };
            }
            return results;
        }

        [Theory]
        [InlineData(AnubisPA8, new[] { PLA, BD })]
        [InlineData(AnubisPB7, new[] { SW, GP })]
        [InlineData(AnubisPB8, new[] { BD })]
        [InlineData(AnubisPK2, new[] { C })]
        [InlineData(AnubisPK3, new[] { SW, US, SN, OR, X, B2, B, Pt, E })]
        [InlineData(AnubisPK4, new[] { SW, US, SN, OR, X, B2, B, Pt })]
        [InlineData(AnubisPK5, new[] { SW, US, SN, OR, X, B2 })]
        [InlineData(AnubisPK6, new[] { SW, US, SN, OR })]
        [InlineData(AnubisPK7, new[] { SW, US })]
        [InlineData(AnubisPK8, new[] { SW })]
        [InlineData(AnubisNTPB7, new[] { GE })]
        [InlineData(AnubisTPK7, new[] { SW, US })]
        [InlineData(AnubisTPK8, new[] { SW })]
        [InlineData(AnubisVCPK7, new[] { SW, US })]

        [InlineData(RoCPA8, new[] { PLA, BD })]
        [InlineData(RoCPB7, new[] { SW, GP })]
        [InlineData(RoCPB8, new[] { BD })]
        [InlineData(RoCPK1, new[] { RD, C })]
        [InlineData(RoCPK2, new[] { C })]
        [InlineData(RoCPK3, new[] { SW, US, SN, OR, X, B2, B, Pt, E })]
        [InlineData(RoCPK4, new[] { SW, US, SN, OR, X, B2, B, Pt })]
        [InlineData(RoCPK5, new[] { SW, US, SN, OR, X, B2 })]
        [InlineData(RoCPK6, new[] { SW, US, SN, OR })]
        [InlineData(RoCPK7, new[] { SW, US })]
        [InlineData(RoCPK8, new[] { SW })]
        [InlineData(RoCNTPK1, new[] { RD })]
        [InlineData(RoCNTPK4, new[] { Pt })]
        [InlineData(RoCNTPK6, new[] { OR })]
        [InlineData(RoCNTPK7, new[] { US })]
        [InlineData(RoCVCPK7, new[] { SW, US })]

        [InlineData(UnderlevelPK1, new[] { RD, C })]
        [InlineData(UnderlevelPK2, new[] { C })]
        [InlineData(UnderlevelPK3, new[] { SW, US, SN, OR, X, B2, B, Pt, E })]
        [InlineData(UnderlevelPK4, new[] { SW, US, SN, OR, X, B2, B, Pt })]
        [InlineData(UnderlevelPK5, new[] { SW, US, SN, OR, X, B2 })]
        [InlineData(UnderlevelPK6, new[] { SW, US, SN, OR })]
        [InlineData(UnderlevelPK7, new[] { SW, US })]
        [InlineData(UnderlevelNTPK4, new[] { Pt })]
        [InlineData(UnderlevelVCPK7, new[] { SW, US })]
        public static void VerifyFile(string path, GameVersion[] testversions)
        {
            Directory.CreateDirectory(LogDirectory);
            var full = Path.Combine(TestPath, path);
            var res = RunVerification(full, testversions);
            var msg = "\n";
            var error = string.Empty;
            var testfailed = false;
            foreach (var (gv, sets) in res)
            {
                var illegalcount = sets["illegal"].Length;
                if (illegalcount == 0)
                    continue;
                testfailed = true;
                msg += $"GameVersion {gv} : Illegal: {illegalcount} | Legal: {sets["legal"].Length}\n";
                error += $"\n\n=============== GameVersion: {gv} ===============\n\n";
                error += string.Join("\n\n", sets["illegal"].Select(x => x.Text));
            }
            var fileName = $"{Path.GetFileName(path).Replace('.', '_')}{DateTime.Now:_yyyy-MM-dd-HH-mm-ss}.log";
            if (error.Trim().Length > 0)
                File.WriteAllText(Path.Combine(LogDirectory, fileName), error);
            testfailed.Should().BeFalse(msg);
        }

        // Anubis test file paths
        private const string AnubisPA8 = "Anubis Tests/Anubis - pa8.txt";
        private const string AnubisPB7 = "Anubis Tests/Anubis - pb7.txt";
        private const string AnubisPB8 = "Anubis Tests/Anubis - pb8.txt";
        private const string AnubisPK2 = "Anubis Tests/Anubis - pk2.txt";
        private const string AnubisPK3 = "Anubis Tests/Anubis - pk3.txt";
        private const string AnubisPK4 = "Anubis Tests/Anubis - pk4.txt";
        private const string AnubisPK5 = "Anubis Tests/Anubis - pk5.txt";
        private const string AnubisPK6 = "Anubis Tests/Anubis - pk6.txt";
        private const string AnubisPK7 = "Anubis Tests/Anubis - pk7.txt";
        private const string AnubisPK8 = "Anubis Tests/Anubis - pk8.txt";
        private const string AnubisNTPB7 = "Anubis Tests/Anubis notransfer - pb7.txt";
        private const string AnubisTPK7 = "Anubis Tests/Anubis transferred - pk7.txt";
        private const string AnubisTPK8 = "Anubis Tests/Anubis transferred - pk8.txt";
        private const string AnubisVCPK7 = "Anubis Tests/Anubis VC - pk7.txt";

        // RoC's PC test file paths
        private const string RoCPA8 = "RoCs-PC Tests/RoC - pa8.txt";
        private const string RoCPB7 = "RoCs-PC Tests/RoC - pb7.txt";
        private const string RoCPB8 = "RoCs-PC Tests/RoC - pb8.txt";
        private const string RoCPK1 = "RoCs-PC Tests/RoC - pk1.txt";
        private const string RoCPK2 = "RoCs-PC Tests/RoC - pk2.txt";
        private const string RoCPK3 = "RoCs-PC Tests/RoC - pk3.txt";
        private const string RoCPK4 = "RoCs-PC Tests/RoC - pk4.txt";
        private const string RoCPK5 = "RoCs-PC Tests/RoC - pk5.txt";
        private const string RoCPK6 = "RoCs-PC Tests/RoC - pk6.txt";
        private const string RoCPK7 = "RoCs-PC Tests/RoC - pk7.txt";
        private const string RoCPK8 = "RoCs-PC Tests/RoC - pk8.txt";
        private const string RoCNTPK1 = "RoCs-PC Tests/RoC notransfer - pk1.txt";
        private const string RoCNTPK4 = "RoCs-PC Tests/RoC notransfer - pk4.txt";
        private const string RoCNTPK6 = "RoCs-PC Tests/RoC notransfer - pk6.txt";
        private const string RoCNTPK7 = "RoCs-PC Tests/RoC notransfer - pk7.txt";
        private const string RoCVCPK7 = "RoCs-PC Tests/RoC VC - pk7.txt";

        // Underleveled test file paths
        private const string UnderlevelPK1 = "Underleveled Tests/Underlevel - pk1.txt";
        private const string UnderlevelPK2 = "Underleveled Tests/Underlevel - pk2.txt";
        private const string UnderlevelPK3 = "Underleveled Tests/Underlevel - pk3.txt";
        private const string UnderlevelPK4 = "Underleveled Tests/Underlevel - pk4.txt";
        private const string UnderlevelPK5 = "Underleveled Tests/Underlevel - pk5.txt";
        private const string UnderlevelPK6 = "Underleveled Tests/Underlevel - pk6.txt";
        private const string UnderlevelPK7 = "Underleveled Tests/Underlevel - pk7.txt";
        private const string UnderlevelNTPK4 = "Underleveled Tests/Underlevel notransfer - pk4.txt";
        private const string UnderlevelVCPK7 = "Underleveled Tests/Underlevel VC - pk7.txt";
    }

    internal class ShowdownSetComparator : IEqualityComparer<ShowdownSet>
    {
        public bool Equals([DisallowNull] ShowdownSet x, [DisallowNull] ShowdownSet y) => x!.Text.Trim() == y!.Text.Trim();
        public int GetHashCode([DisallowNull] ShowdownSet obj) => obj.Text.GetHashCode();
    }
}
