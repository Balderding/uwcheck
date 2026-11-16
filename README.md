# uwcheck

A headless test tool for **uwr**, our Ultima Underworld port (the exe was `uw-new.exe` until 2026-11-16) (`O:\uw\uo-new`, repo `uwr`). It runs the game
hidden, drives it with a script, and reports whether the game crashed, halted, hit its own fatal error, refused a
test cheat, or saved levels whose object lists are broken. It can also play the same script on Underworld
Exhumed's port (the reference) and list what differs between the two saves.

The checking itself is not ours: it is the **Underworld Editor (Avalonia)** by Karl Clinckspoor
(<https://github.com/KarlClinckspoor/UltimaUnderworldEditorAvalonia>, MIT), whose "Check in Underworld Exhumed"
does exactly this for Exhumed. uwcheck is a small console program (one file, `Program.cs`) that calls the
editor's core library, `UwEditor.Core`, without its window, and points it at our port. **No editor source, game
file or executable is in this repository**; it builds against a local clone of the editor.

## Why this works on our port

Our `uwr.exe` is built on Underworld Exhumed's port code and keeps its `main.c` and `cheats.c`, so it accepts
the same switches as Exhumed's `uw1port.exe`:

| Switch | What it does |
| --- | --- |
| `--data DIR`, `--home DIR` | the game's files; where saves and settings live |
| `--autoload` | skip the title and menu, go straight into the newest save |
| `--input-script FILE` | key presses and test cheats at set times, in milliseconds since the start |
| `--hidden`, `--no-audio`, `--no-recording` | no window, no sound, no input recording |
| `--screenshot FILE`, `--screenshot-after MS` | save the screen once |
| `--exit-after MS`, `--exit-on-halt` | stop at a time, or when the game halts |

With `EXHUME_TEST_CHEATS=1` in the environment the script can use the test cheats `goto LEVEL X Y` (as a
teleport trap does, so the game's own code loads the level) and `set str|defense|hp N`, and the port prints
each one it ran (`cheat: goto 3 32 11 (not as DOS)`), which is how the result is read back.

## What a check does

1. **A game to test.** `dist\UW1\game` is copied to `<work>\base`, and a save `SAVE1` is made there from the
   editor's built-in default player. Your own saves (`dist\UW1\home`) are never read or touched. The base is
   made again when the game's `LEV.ARK` changes.
2. **The plan** (the editor's `ExhumedCheck.Prepare`). A fresh copy for each run in `<work>\runs\<name>`. The
   player is made experience level 16, strength 70, defense 30, and his hit points are filled before each level,
   so nothing kills him. For each level the editor picks an open, flat, dry square with room around it, and the
   script does `cheat goto` there and waits 5 seconds of play. Then Ctrl+S saves in slot I, so the game writes
   every level it visited back to `LEV.ARK`.
   - `--sweep` adds one stop in every 16x16 area of each level; `--look` turns a full circle at every stop.
   - Level 9 (the Ethereal Void) comes last, after the save. In our first runs the save came after level 9 and
     neither port wrote one; why was not looked into. So level 9 is loaded and watched for crashes, but it is
     not in the save and is not compared.
   - Ours starts with `--autoload` instead of the editor's two Enter presses through the title menu, since our
     menus may be drawn differently.
3. **The run.** The exe is started hidden; its output is captured to `output.txt`. A run that doesn't end within
   three times its scripted length (plus a minute) is killed and counted as hung.
4. **The verdict** (the editor's `ExhumedCheck.Analyze`). A problem is: a non-zero exit (crash, halt, fatal
   error, start-up error, each named), "host fault", "the game stops here", "can no longer run", a refused
   cheat, a level never reached, no save written, or a level in the save that fails the editor's object-list
   checks (`LevelBlock.Check`, the editor's version of the game's `Obj_ListOkay`, and `LevelFixes` list errors). Notes, not
   problems: the game reading through a null pointer (Exhumed reads what DOS would and carries on) and which
   levels were loaded. The editor excuses a crash on arriving at level 9, which old Exhumed builds always did;
   uwcheck counts it, because ours must not crash there.

Exit code 0 means passed, 1 problems, 2 a usage or set-up error.

## Comparing with the reference (`--compare`)

Ours is run twice and Exhumed's `uw1port.exe` twice, on the same script, and the four slot-I saves are
compared byte by byte: each level's 64x64 tiles, 256 mobile objects (27 bytes each: creatures and things that
move), 768 static objects (8 bytes), the free lists, and `PLAYER.DAT`.

The script's times are milliseconds since the start and a frame lands a little earlier or later each run, so **no port
saves the same bytes twice**: between two runs of the same exe, 60-240 creature bytes a level differ
(positions, headings, AI state). So a byte only counts as a difference when both our runs agree with each
other, both reference runs agree with each other, and the two sides disagree. Everything else is listed as
noise. A difference names the tile or object and the byte within its record, with ours/reference in hex.
The exit code is 0 only when all four runs passed and no such byte was found. In practice some noise gets through
(see the results below), so the list is for reading, not a pass/fail gate.

Read it as a pointer for where to look, not as proof: a one-bit field that is noisy on both sides still
passes the filter about one time in eight. The proof that a routine matches the original is the emulator
proof in uo-new (`tools\verify.cmd`).

## Using it

Needs: Windows, the .NET 10 SDK (here `O:\appsnotonc\dotnet10`), a clone of the editor at
`O:\UltimaUnderworldEditorAvalonia` (or change `EditorSrc` in `uwcheck.csproj`), our built port, and a UW1
game folder.

```
build.cmd                                  build (output in ..\..\work\uwcheck\bin)
uwcheck.cmd check                          levels 1-9 on ours
uwcheck.cmd check --levels 3-5 --sweep --look
uwcheck.cmd check --compare                ours x2 and the reference x2, then the save diff
uwcheck.cmd spot 3 32 11 --secs 10         stand on level 3 (32, 11) for 10 s, take a screenshot
uwcheck.cmd --help
```

`spot` refuses a square that is solid rock or outside 1 to 62 (the game kills a player put in rock). It does not
keep the player alive and does not read whether he lived: look at `last-screen.png` (a skull is a death).
A run with problems quotes the port's last lines in its report.

Options: `--port EXE` (default `O:\uw\uo-new\dist\UW1\uwr.exe`), `--ref EXE` or `--compare EXE` (default
`O:\uw\uo-new\underworld-exhumed\uw1\build\port\uw1port.exe`), `--game DIR` (default `O:\uw\uo-new\dist\UW1\game`),
`--work DIR` (default `O:\uw\uo-new\work\uwcheck`), `--shown` (show the game window).

Results, per run in `<work>\runs\<name>\` (`ours`, or `ours-a`, `ours-b`, `reference-a`, `reference-b`):
`report.txt` (the verdict), `output.txt` (everything the port printed), `script.txt`, `last-screen.png`,
`home\SAVE1` (the save the game wrote) and `before\` (the save it started from). The diff is in
`<work>\runs\compare.txt`. The tool deletes and remakes its own run folders and `base` each time, and nothing else.

**Why `uwcheck.cmd` and no `uwcheck.exe`:** Defender's attack-surface rule "block executables unless they meet a
prevalence, age or trusted list criterion" blocks every new unsigned exe on this machine, so the program is built
as a DLL only (`UseAppHost=false`) and run through Microsoft's signed `dotnet.exe`. The editor itself runs the
same way: `dotnet.exe UwEditorAvalonia.dll`.

## Files

| File | |
| --- | --- |
| `Program.cs` | the whole tool: options, the base save, the plan, running the exe, the verdict, the diff, `spot` |
| `uwcheck.csproj` | .NET 10 console app, DLL only, references the editor's `UwEditor.Core` |
| `Directory.Build.props` | puts `bin` and `obj` in `..\..\work\uwcheck`, outside the source |
| `build.cmd`, `uwcheck.cmd` | build and run with the .NET 10 on O:, nothing installed on C: |

## Results so far

2026-11-15/16, `dist\UW1\uwr.exe` against Exhumed's `uw1port.exe` built from the same tree:

- `check`: **passed** on levels 1-9, for ours and for the reference, in every run. Every level was loaded, the
  game saved slot I, and every saved level passed the object-list checks. Neither port crashed on level 9 (the
  editor's assumption that Exhumed crashes there is out of date). Both read through the same null pointers in
  `critter\AI.C` lines 1364-1370 on level 9: the original game's own reads, the same in both.
- `check --compare` (2 + 2 runs, done twice): `PLAYER.DAT` the same; no static-object or free-list difference;
  level 9 is not in the save, so not compared. What is listed changes from one compare to the next, which is
  itself the sign of noise:
  - the lowest heading bit of a creature (byte +2, bit 7) and its position within the tile (byte +3), a few
    to 20 bytes a level, on different creatures each time;
  - a tile's bytes 2-3 here and there (which object is first on that tile, so it follows the creatures);
  - **byte +10, bit 3 of the creatures, a whole level at a time** (59 bytes on level 1 and 80 on level 7 in one
    compare, none in the one before). Counted over the four saves, most creatures of a level carry this bit
    together and it flips between two runs of the same exe, on both sides (level 2: ours 8 and 33 of 75
    creatures, the reference 60 and 7). It is listed only when each side's two runs happen to fall the same
    way, so it is timing noise too, but two runs a side do not filter out a level-wide bit reliably.

  So no rules difference is shown, and `--compare` normally ends with exit code 1 because of this noise: read
  `compare.txt`, do not rely on the exit code. A difference worth chasing is one that comes back in every
  compare on the same object and byte.
- `spot 3 32 11`: passed, screenshot taken.
