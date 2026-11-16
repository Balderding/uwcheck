// uwcheck: our port checked by the Underworld Editor's own "Check in Underworld Exhumed", headless.
//
// The editor (O:\UltimaUnderworldEditorAvalonia, MIT, Karl Clinckspoor) plans a check of a game: a copy with a
// saved game, an input script that sends the player to each level with the port's test cheat `goto` (the game's
// own code loads each), keeps the player alive with `set`, and saves in slot I; then it reads the port's output
// (crashes, halts, the game's fatal error, refused cheats) and runs its object-list checks on the levels the game
// wrote back. Our uw-new.exe keeps Exhumed's main.c and cheats.c, so the same plan runs on it. Differences from the
// editor: the save is loaded by --autoload (our menus may be ours), and level 9 is not excused (the editor assumes
// Exhumed crashes there).
//
//   uwcheck check [--levels 1-8|1,3,5] [--sweep] [--look] [--port EXE] [--compare [REF_EXE]] [--shown]
//   common: --game DIR (the UW1 game folder), --ref EXE (Exhumed uw1port), --work DIR (results)
//   uwcheck spot LEVEL X Y [--secs N] [--port EXE] [--shown]      the player on that square, a screenshot
//
// Results go to work\uwcheck\runs\<name>\ (report.txt, output.txt, script.txt, last-screen.png). Exit code 0 = passed.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UltimateEditor.Games;
using UwEditor.Core;

static class UwCheck
{
    const string Root = @"O:\uw\uo-new";
    static string Game = Root + @"\dist\UW1\game";
    static string Port = Root + @"\dist\UW1\uw-new.exe";
    static string RefPort = Root + @"\underworld-exhumed\uw1\build\port\uw1port.exe";
    static string Work = Root + @"\work\uwcheck";

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        try { return Run(args); }
        catch (Exception ex) { Console.Error.WriteLine("uwcheck: " + ex.Message); return 2; }
    }

    static int Run(string[] a)
    {
        if (a.Length == 0 || a[0] == "--help" || a[0] == "-h") { Usage(); return 2; }
        string cmd = a[0];
        var levels = Enumerable.Range(1, 9).ToList();
        // the editor puts level 9 last, after the save (no save is taken in the Ethereal Void); a crash there is still
        // counted against our port (Recount)
        var opt = new ExhumedOptions { PortCrashesOnLevel9 = true };
        bool compare = false, shown = false;
        int secs = 8;
        var pos = new List<string>();
        for (int i = 1; i < a.Length; i++)
        {
            string s = a[i];
            if (s == "--levels" && i + 1 < a.Length) levels = ParseLevels(a[++i]);
            else if (s == "--sweep") opt.Sweep = true;
            else if (s == "--look") opt.LookAround = true;
            else if (s == "--shown") shown = true;
            else if (s == "--port" && i + 1 < a.Length) Port = Path.GetFullPath(a[++i]);
            else if (s == "--game" && i + 1 < a.Length) Game = Path.GetFullPath(a[++i]);
            else if (s == "--ref" && i + 1 < a.Length) RefPort = Path.GetFullPath(a[++i]);
            else if (s == "--work" && i + 1 < a.Length) Work = Path.GetFullPath(a[++i]);
            else if (s == "--secs" && i + 1 < a.Length) secs = int.Parse(a[++i], CultureInfo.InvariantCulture);
            else if (s == "--compare")
            {
                compare = true;
                if (i + 1 < a.Length && !a[i + 1].StartsWith("-")) RefPort = Path.GetFullPath(a[++i]);
            }
            else if (s.StartsWith("-")) throw new ArgumentException("unknown option " + s);
            else pos.Add(s);
        }
        if (!File.Exists(Port)) throw new FileNotFoundException("no port at " + Port);
        var session = BaseSession();
        if (cmd == "check") return Check(session, levels, opt, compare, shown);
        if (cmd == "spot" && pos.Count == 3)
            return Spot(session, int.Parse(pos[0]), int.Parse(pos[1]), int.Parse(pos[2]), secs, shown);
        Usage();
        return 2;
    }

    static void Usage()
    {
        Console.WriteLine("uwcheck check [--levels 1-8|1,3,5] [--sweep] [--look] [--port EXE] [--compare [REF_EXE]] [--shown]");
        Console.WriteLine("uwcheck spot LEVEL X Y [--secs N] [--port EXE] [--shown]");
        Console.WriteLine("  common: --game DIR, --ref EXE, --work DIR");
        Console.WriteLine("  port: " + Port + "\n  reference (--compare): " + RefPort + "\n  game: " + Game + "\n  results: " + Work + "\\runs");
    }

    static List<int> ParseLevels(string s)
    {
        var r = new List<int>();
        foreach (var part in s.Split(','))
        {
            var ab = part.Split('-');
            int lo = int.Parse(ab[0]), hi = ab.Length > 1 ? int.Parse(ab[1]) : lo;
            for (int l = lo; l <= hi; l++) r.Add(l);
        }
        return r;
    }

    // A copy of the game with SAVE1 made round the editor's default player (the game folder has no saves; the
    // user's own saves in dist\UW1\home are never used). Rebuilt when the game's LEV.ARK changes.
    static GameSession BaseSession()
    {
        string baseDir = Path.Combine(Work, "base");
        string stamp = Path.Combine(baseDir, "made-from.txt");
        string levSrc = Directory.GetFiles(Path.Combine(Game, "data"), "LEV.ARK", new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive }).First();
        string want = Game + " " + File.GetLastWriteTimeUtc(levSrc).Ticks + " " + new FileInfo(levSrc).Length;
        if (!File.Exists(stamp) || File.ReadAllText(stamp) != want)
        {
            if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);   // ours: made by this tool only
            Directory.CreateDirectory(baseDir);
            PlayTest.CopyGame(Game, baseDir, baseDir);
            var s0 = GameSession.Open(baseDir);
            DefaultPlayer.WriteSave(s0, Path.Combine(baseDir, "SAVE1"), s0.LevArk.ToBytes(), "uwcheck");
            File.WriteAllText(stamp, want);
        }
        return GameSession.Open(Path.Combine(baseDir, "SAVE1"), baseDir);
    }

    // The editor's plan, made to start by --autoload: the two Enter presses through the title menu are dropped.
    static ExhumedPlan Plan(GameSession s, IList<int> levels, ExhumedOptions opt, string folder, bool shown)
    {
        var plan = ExhumedCheck.Prepare(s, null, null, levels, folder, true, true, opt, shown);
        var lines = File.ReadAllLines(plan.Script).Where(l => !l.Contains("# Journey Onward") && !l.Contains("# saved game I")).ToList();
        File.WriteAllLines(plan.Script, lines);
        plan.Arguments.Add("--autoload");
        return plan;
    }

    static (int code, string output, bool timedOut) Launch(string exe, ExhumedPlan plan)
    {
        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = Path.GetDirectoryName(exe),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var x in plan.Arguments) psi.ArgumentList.Add(x);
        foreach (var kv in plan.Environment) psi.Environment[kv.Key] = kv.Value;
        var sb = new StringBuilder();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (o, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        p.ErrorDataReceived += (o, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        Console.WriteLine("running " + Path.GetFileName(exe) + " for " + (plan.ExitAfterMs / 1000) + " s of game time ...");
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        bool done = p.WaitForExit(plan.ExitAfterMs * 3 + 60000);
        if (!done) { try { p.Kill(true); } catch { } p.WaitForExit(5000); }
        else p.WaitForExit();
        File.WriteAllText(Path.Combine(plan.Folder, "output.txt"), sb.ToString());
        return (done ? p.ExitCode : -1, sb.ToString(), !done);
    }

    static ExhumedResult CheckOne(GameSession s, IList<int> levels, ExhumedOptions opt, string name, string exe, bool shown, out ExhumedPlan plan)
    {
        string folder = Path.Combine(Work, "runs", name);
        plan = Plan(s, levels, opt, folder, shown);
        var (code, output, timedOut) = Launch(exe, plan);
        var r = Recount(ExhumedCheck.Analyze(s, plan, code, output, timedOut));
        string report = exe + "\n" + plan.Note + "\n\n" + r.Report();
        File.WriteAllText(Path.Combine(folder, "report.txt"), report);
        Console.WriteLine("== " + name + ": " + report);
        return r;
    }

    // The editor excuses a crash on arriving at UW1 level 9 (Exhumed used to crash there): ours must not, so it
    // counts. Repeated notes (the same null read each frame) are said once, with a count.
    static ExhumedResult Recount(ExhumedResult r)
    {
        if (r.PortCrashedOn > 0) r.Problems.Add("the port crashed on arriving at level " + r.PortCrashedOn + " (exit " + r.ExitCode + ")");
        var notes = r.Notes.GroupBy(n => n).Select(g => g.Count() > 1 ? g.Key + " (" + g.Count() + " times)" : g.Key).ToList();
        r.Notes.Clear();
        r.Notes.AddRange(notes);
        return r;
    }

    static int Check(GameSession s, List<int> levels, ExhumedOptions opt, bool compare, bool shown)
    {
        var ours = CheckOne(s, levels, opt, compare ? "ours-a" : "ours", Port, shown, out var pOurs);
        if (!compare) return ours.Passed ? 0 : 1;
        if (!File.Exists(RefPort)) throw new FileNotFoundException("no reference port at " + RefPort);
        var ours2 = CheckOne(s, levels, opt, "ours-b", Port, shown, out var pOurs2);
        CheckOne(s, levels, opt, "reference-a", RefPort, shown, out var pA);
        CheckOne(s, levels, opt, "reference-b", RefPort, shown, out var pB);
        int diffs = CompareSaves(s, new[] { pOurs, pOurs2 }, new[] { pA, pB }, levels);
        if (!ours2.Passed) return 1;
        return ours.Passed && diffs == 0 ? 0 : 1;
    }

    // Slot I as each game saved it after the same script. The script runs on the real clock, so even the reference
    // never saves the same bytes twice (creatures move a frame more or less). Each side is run twice, and only a
    // byte both our runs agree on, both reference runs agree on, and the two sides do not, counts: a steady
    // difference, not timing noise.
    // A difference is a pointer, not a proof (the routines' proof is the emulator's, tools\verify.cmd).
    static int CompareSaves(GameSession s, ExhumedPlan[] ours, ExhumedPlan[] refs, List<int> levels)
    {
        var sb = new StringBuilder("Slot I, ours against the reference (bytes each side's two runs agree on):\n");
        int diffs = 0;
        string lev = Path.GetFileName(s.LevArkPath);
        var plans = new[] { ours[0], ours[1], refs[0], refs[1] };
        var paths = plans.Select(p => Path.Combine(p.Home, "SAVE1", lev)).ToArray();
        if (paths.Any(f => !File.Exists(f))) { sb.AppendLine("  NOT COMPARED: a run wrote no slot I"); diffs++; }
        else if (plans.Any(p => File.ReadAllBytes(Path.Combine(p.Home, "SAVE1", lev)).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(p.Before, lev)))))
        { sb.AppendLine("  NOT COMPARED: a run did not write slot I (its LEV.ARK is the starting one)"); diffs++; }
        else
        {
            var arks = paths.Select(f => Archive.Open(File.ReadAllBytes(f), s.Kind)).ToArray();
            foreach (int lv in levels)
            {
                int blk = s.Profile.LevelBlock(lv);
                if (!arks.All(k => k.Has(blk))) { if (arks.Any(k => k.Has(blk))) { sb.AppendLine("  level " + lv + ": not in every save"); diffs++; } continue; }
                byte[] o = arks[0].Read(blk), o2 = arks[1].Read(blk), a = arks[2].Read(blk), b = arks[3].Read(blk);
                int noise = 0;
                var steady = new List<int>();
                for (int i = 0; i < new[] { o.Length, o2.Length, a.Length, b.Length }.Min(); i++)
                {
                    if (a[i] != b[i] || o[i] != o2[i]) { noise++; continue; }
                    if (o[i] != a[i]) steady.Add(i);
                }
                if (steady.Count == 0) { sb.AppendLine("  level " + lv + ": the same (" + noise + " bytes differ within a side)"); continue; }
                diffs++;
                // UW1 level block: 64x64 tiles of 4 bytes, 256 mobile objects of 27 bytes, 768 static of 8, then the free lists
                var where = steady.GroupBy(i => i < 0x4000 ? string.Format("tile ({0}, {1})", (i / 4) % 64, (i / 4) / 64)
                    : i < 0x5B00 ? "mobile object " + ((i - 0x4000) / 27)
                    : i < 0x7300 ? "static object " + (256 + (i - 0x5B00) / 8)
                    : "free lists/rest")
                    .Select(g => g.Key + " [" + string.Join(",", g.Select(i => i < 0x4000 ? "+" + (i % 4) : i < 0x5B00 ? string.Format("+{0}: {1:X2}/{2:X2}", (i - 0x4000) % 27, o[i], a[i]) : i < 0x7300 ? "+" + ((i - 0x5B00) % 8) : string.Format("0x{0:X4}", i))) + "]");
                sb.AppendLine("  level " + lv + ": " + steady.Count + " steady bytes (" + noise + " noise, differing within a side): " + string.Join("; ", where));
            }
        }
        var pd = plans.Select(p => Path.Combine(p.Home, "SAVE1", "PLAYER.DAT")).ToArray();
        if (pd.All(File.Exists))
        {
            var x = pd.Select(File.ReadAllBytes).ToArray();
            var st = Enumerable.Range(0, x.Min(v => v.Length)).Where(i => x[0][i] == x[1][i] && x[2][i] == x[3][i] && x[0][i] != x[2][i]).ToList();
            sb.AppendLine("  PLAYER.DAT: " + (st.Count == 0 ? "the same" : string.Join(", ", st.Select(i => string.Format("0x{0:X2}: {1:X2}/{2:X2}", i, x[0][i], x[2][i])))));
            if (st.Count > 0) diffs++;
        }
        sb.AppendLine(diffs == 0 ? "SAME." : "DIFFERENT in " + diffs + " part(s) (ours/reference shown as hex).");
        File.WriteAllText(Path.Combine(Work, "runs", "compare.txt"), sb.ToString());
        Console.WriteLine(sb.ToString());
        return diffs;
    }

    // The player put on one square (the cheat, as a teleport trap does) and a screenshot after secs seconds.
    static int Spot(GameSession s, int level, int x, int y, int secs, bool shown)
    {
        string folder = Path.Combine(Work, "runs", string.Format("spot-{0}-{1}-{2}", level, x, y));
        var opt = new ExhumedOptions { PortCrashesOnLevel9 = false };
        var plan = Plan(s, new[] { level }, opt, folder, shown);
        plan.Visits.Clear();
        plan.Visits.Add(new ExhumedVisit { Level = level, X = x, Y = y, At = 12000 });
        plan.SavesAtEnd = false;
        int exitAt = 12000 + secs * 1000;
        File.WriteAllText(plan.Script, "# uwcheck spot\n12000 cheat goto " + level + " " + x + " " + y + "\n");
        plan.ExitAfterMs = exitAt;
        for (int i = 0; i + 1 < plan.Arguments.Count; i++)
        {
            if (plan.Arguments[i] == "--exit-after") plan.Arguments[i + 1] = exitAt.ToString(CultureInfo.InvariantCulture);
            if (plan.Arguments[i] == "--screenshot-after") plan.Arguments[i + 1] = (exitAt - 500).ToString(CultureInfo.InvariantCulture);
        }
        var (code, output, timedOut) = Launch(Port, plan);
        var r = Recount(ExhumedCheck.Analyze(s, plan, code, output, timedOut));
        string report = Port + "\nspot: level " + level + " (" + x + ", " + y + "), " + secs + " s there\n\n" + r.Report();
        File.WriteAllText(Path.Combine(folder, "report.txt"), report);
        Console.WriteLine(report);
        return r.Passed ? 0 : 1;
    }
}
