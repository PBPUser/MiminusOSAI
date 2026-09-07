# Миминус ОС

A pseudo-operating system rebuilt from the three reference videos in
`references/`, written in C# against **raw OpenGL 3.3 core, GLSL and OpenAL**.

Nothing on screen is an operating-system control. Win32 supplies exactly three
things — a window, a message queue, and a GDI device context used once at startup
to rasterise font glyphs. Every pixel after that is a quad in a batched GLSL
pipeline: window frames, menus, icons, the mouse pointer, the boot screen. Every
sound is synthesised into a PCM buffer at startup and played through OpenAL.
There are no images, fonts, audio files or third-party packages in the project.

## The joke it reproduces

BOLGENOS was a reskinned Ubuntu presented as a from-scratch national OS. The
videos are the answer: take Windows XP, write **МИМИНУС ОС / Copyright Попов**
on the wallpaper, rename the bundled programs, and call it your own. The
antivirus is a Notepad file. Part 3 rebrands to «Миминус 7» and finishes by
scanning the computer for Popovs.

All of that is implemented literally. See [`docs/FEATURES.md`](docs/FEATURES.md)
for the frame-by-frame inventory the build was written against.

## Running

```
dotnet run --project src/Host -- --skip-boot
```

`tools/build.sh` builds the whole solution (`src/MiminusOS.slnx`) and copies the
program DLLs into `apps/` beside the executable.

Requires the .NET 10 SDK and a GPU with OpenGL 3.3. OpenAL is loaded at runtime
from `OpenAL32.dll`; if it is missing the OS runs silently instead of failing.

### Command line

```
--size=WxH        window size (default 1280x800)
--fullscreen, -f  borderless full screen
--lang=ru|en      start in this language
--theme=NAME      lunablue | lunaolive | lunasilver | seven | classic
--wallpaper=NAME  yellow | wave | seven | dark | green | bliss | azure |
                  sunset | matrix | space | plaid | blueprint
--skip-boot       jump straight to the desktop
--open=A,B        launch these programs at start
--mount=PATH      mount a real folder as a drive (repeatable; --mount=X:PATH
                  picks the letter). Read-only unless --mount-writable
--update-url=U    read the version manifest from U (a URL or a file)
--mute            start with sound off
--stats           FPS / draw-call overlay
--screenshot=PATH render to PNG and exit
--at=SECONDS      capture at this time (else --frames=N)
--fps=N           frame cap, 0 for uncapped (default 60)
```

`--open` accepts any program id (`notepad`, `explorer`, `minesweeper`,
`calculator`, `paint`, `player`, `browser`, `orega`, `spreadsheet`, `display`,
`effects`, `appearance`, `advanced`, `terminal`, `taskmgr`, `mycomputer`,
`controlpanel`, `sound`, `language`, `allinone`, `voice`, `update`, `about`,
`run`) plus a few shorthands
that come with a document: `antivirus`, `scan` (opens the antivirus and runs
it), `revolutionary`, `tricks`, `photo`, `startmenu`, `shutdown`, `openwith`,
`sound`, `language`, `balloon`.

```
dotnet run --project src/Host -- --open=scan          # watch the antivirus find zero Popovs
dotnet run --project src/Host -- --theme=seven --wallpaper=seven --lang=en
```

## What is in it

**Boot** — fake POST that fails to detect BOLGENOS, a splash with marching
progress blocks, an XP-style welcome screen, and a shutdown that ends on "it is
now safe to turn off your computer".

**Shell** — desktop with 55 icons, column-major grid layout, rubber-band
selection, icon dragging with grid snap, renaming in place (F2), and the full
right-click menu including a working *Создать* submenu. Files and folders can be
renamed anywhere they appear; a folder called Windows refuses to go until Ctrl is
held, and then goes, and the system carries on — which is the whole point of that
scene in part 3.

Taskbar with Start button, quick launch, window buttons, notification area, a
clickable RU/EN indicator, and the «Сервисное сообщение» balloon from part 1.
Its property sheet works: locking shows or hides the grab handles, auto-hide
really slides the bar away and hands the space back to maximised windows, "keep
on top" is the order the layers are painted in, and grouping collapses several
windows of one program into a single button once the bar runs short of room.

The Windows key belongs to МИМИНУС while the window has the focus — a low-level
hook takes it from the host shell — and opens the Start menu, with Win+E, Win+R,
Win+F, Win+D, Win+U, Win+L and Win+Pause behind it. XP two-column Start menu with
a working *All Programs* tree.

**Window manager** — draggable and resizable windows with eight-way edge grips,
z-order, focus, minimise/maximise/restore, modal dialogs that block their owner,
cascade and tile, and per-theme chrome.

**Programs**

| | |
|---|---|
| Блокнот | full text editor — caret, selection, word wrap, undo, find/replace, real clipboard. When it holds `АНТИВИРУС ГРЕВЦОВА.txt` it grows an *Антивирус* menu whose scan types its warnings into the document and reports how many Popovs it found. |
| Проводник | task pane that changes with the selection, address bar, four view modes, navigation history, context menus, *Открыть с помощью*. |
| Сапер | complete Minesweeper — LED counters, smiley, flags and question marks, chording, safe first click, three presets, best times. |
| Paint | editable bitmap with pencil/brush/eraser/line/rect/ellipse/fill/picker, colour wheel, zoom, undo, greyscale/invert/blur. |
| Проигрыватель | playlist, transport, seek, equalizer, spectrum; audio tracks are generated chiptunes, and the video track draws the *BolgenOS on TV* news segment. |
| Веб-браузер Firefox | tabs, history, address bar; renders Opera's Экспресс-панель, the Mozilla download page, and the «скачать интернет» Google results from part 3. |
| Таблица Миминус | spreadsheet with a real recursive-descent formula parser (cell and range refs, `СУММ`/`ЧПС`/`ЕСЛИ` and friends, cycle detection), preloaded with the NPV/MIRR exercise from part 1. |
| Калькулятор Плюс | standard, scientific and unit-conversion modes. |
| Свойства: Экран | all five tabs, live monitor preview, wallpaper and theme switching. |
| Командная строка | shell over the virtual filesystem — `dir`, `cd`, `type`, `start`, `color`, `scan`, `bolgenos`. |
| Всё в одном | the utility that "is suitable for everything": invented months (бенабрь, нехабрь), temperature, weather, forms of address, and how to pronounce and decline «Михаил Гревцов» — spoken aloud through the speech engine Windows already has. |
| Центр обновления | reads a version manifest published in the project's GitHub repository, then downloads, verifies, unpacks and installs what it announces. |
| Что нового | the tour the system shows itself the first time it starts after an update — nine cards, each illustration built from the same icons and rectangles as the rest of the OS. |
| | plus Task Manager, Control Panel, Sound properties, Properties sheets, Open With, Run, About, Распознавание голоса (which listens, thinks, and admits it is unfinished). |

**Themes** — XP Luna in blue, olive and silver, a Windows-7 pastiche for
«Миминус 7», and Windows Classic. Twelve procedurally generated wallpapers.
Display properties carries the XP *Эффекты*, *Оформление* and *Дополнительно*
sheets, including a font-smoothing switch that cycles ClearType, standard and
no antialiasing and rebuilds the glyph atlas in place.

**Audio** — 28 synthesised effects (startup chord, shutdown fall, window and
menu movements, dialog stings, Minesweeper explosion, antivirus beeps) plus
generated music. Sounds are positioned in 3D from the screen coordinate that
triggered them, so a menu opening on the left is audibly on the left.

## Translations

All 1061 user-visible strings live in `src/Core/lang/ru.json` and
`src/Core/lang/en.json`
as `"key": "text"`. The files are embedded in the assembly so the program runs
standalone, and also copied next to the executable so they can be edited without
rebuilding — a file on disk overrides the embedded copy, key by key.

Code never contains display text. `L.T("notepad.save_changes")` looks a string
up; `L.F("mine.seconds", n)` fills in `{0}` placeholders. Filesystem node names,
window titles, theme names and tooltips are all key-backed, so switching
language at runtime (tray indicator, Ctrl+Shift+L, or Control Panel → Regional
Options) re-renders everything including file and folder names.

To add a language, copy `en.json`, translate the values, and load it by name.
To verify a catalogue:

```
python tools/check-lang.py
```

It reports keys used in code but missing from the catalogues, keys present in
one language only, `{0}` placeholder mismatches between languages, and orphans.

## Layout

```
src/MiminusOS.slnx        thirteen projects: the engine, the host, eleven programs

src/Core/                 Miminus.Core.dll — everything a program is written against
  Platform/               Win32, WGL context creation, OpenGL 3.3 bindings, clipboard
  Graphics/               batched renderer, GDI-baked glyph atlas, vector icons, PNG
  Audio/                  OpenAL bindings, DSP primitives, the sound set
  UI/                     theme, widgets, menus, text editor, immediate-mode context
  Sys/                    virtual filesystem, host mounts, localisation, update, speech
  Shell/                  window manager, desktop, taskbar, Start menu, boot, dialogs
  lang/                   translation catalogues

src/Host/                 MiminusOS.exe — the window, the frame loop, the command line
src/Apps/<Name>/          one program assembly each, discovered at startup

latest.txt                the published version manifest
tools/                    build script, catalogue validator
docs/FEATURES.md          what the reference videos show
```

### Programs are DLLs, loaded only when used

A program is a class implementing `IProgram` — an id, a name key, an icon, and a
factory that makes its window. Each lives in its own `Miminus.App.*.dll` under
`apps/` beside the executable, so the shell never names a window type and adding
a program means dropping in a DLL.

None of them is read at startup. What the Start menu needs — id, name, icon — is
cached in `apps/programs.index`, keyed by each DLL's size and timestamp, so a
normal boot opens no program assemblies at all. A DLL is read the first time one
of its programs is actually launched, into its own **collectible** load context;
when its last window closes and a grace period passes, the context is unloaded
and the runtime reclaims it. `Miminus.Core` is deliberately resolved from the
default context instead, so the shell and every program share one set of types.

The command prompt's `apps` command lists what is resident, and `apps free`
drops everything idle and reports whether the runtime finished the job:

```
Miminus.App.Notepad            загружена    1
Miminus.App.Minesweeper        выгружена    0
Загружено библиотек: 2. Программ всего: 23.
```

### Mounting a real folder

`--mount=D:\Documents` grafts a host directory into the virtual filesystem as a
drive under Мой компьютер. Directories are enumerated lazily on first open,
text files are read on demand, and PNG and BMP images are decoded by the
project's own decoders. Mounts are **read-only** unless `--mount-writable` is
given, and host files are never deleted.

### Updates, end to end

`Центр обновления` reads `latest.txt` from
[the project repository](https://github.com/PBPUser/MiminusOSAI): plain
`key = value` lines naming the newest version, its release date, size, notes,
download page, and the built package with its SHA-256.

Pressing *Установить обновление* carries it through: the package is streamed to
`update/` and hashed as it arrives, a package whose hash was not published — or
does not match — is refused before anything is unpacked, the archive is expanded
beside the executable, and a one-shot script waits for the OS to exit, copies the
staged build over the installation and starts it again. Publishing a new version
is a one-file edit; a fork can point the check somewhere else with
`--update-url=`, which also accepts a path (`tools/test-update.txt` exercises the
whole cycle).

When the repository is unreachable the centre falls back to the copy shipped
beside the executable and says so — the network in this OS has always been
"checked" rather than connected.

### How the rendering works

One vertex stream, one shader. Each vertex carries position, UV, fill and border
colour, plus the shape's centre, half-extents, corner radius and border width —
so the fragment shader can evaluate a signed-distance rounded rectangle without
a second pipeline, and untextured fills never break the batch. A typical desktop
frame is a handful of draw calls.

Fonts are rasterised once through GDI into a texture atlas (white glyph, alpha =
coverage) and tinted per draw, which is what makes Cyrillic and Latin work
without shipping a font file. Icons are short vector programs evaluated in a
32×32 design space and scaled to whatever rectangle the caller asks for, so the
same code draws a 16px tray icon and a 48px desktop icon.

### How input is layered

Painting runs back to front; input is claimed front to back. Menus resolve their
input first, the taskbar and Start menu then reserve their strip so no window can
steal a click meant for them, windows run their widgets while they paint, and the
desktop — the bottom layer — sees only what nothing above it wanted.
