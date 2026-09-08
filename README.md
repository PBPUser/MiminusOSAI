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

Version 8 does to itself what Windows 8 did to Windows: a full-screen board of
tiles instead of a menu, five buttons hidden off the right edge, a lock screen
in front of the logon screen, and every gradient thrown away. The old Start menu
was not removed — this system keeps both, and each one has the other in it.

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
--theme=NAME      metro | lunablue | lunaolive | lunasilver | seven | classic
--wallpaper=NAME  yellow | wave | seven | dark | green | bliss | azure |
                  sunset | matrix | space | plaid | blueprint
--skip-boot       jump straight to the desktop
--lock            start on the lock screen
--open=A,B        launch these programs at start
--mount=PATH      mount a real folder as a drive (repeatable; --mount=X:PATH
                  picks the letter). Read-only unless --mount-writable
--update-url=U    read the version manifest from U (a URL or a file)
--dpi=N           interface scale: 96 (default), 120 or 144
--depth=N         colour quality: 16, 24 or 32 bits
--refresh=N       frame cap in Hz, 0 for uncapped
--drag=X1,Y1,X2,Y2[,T]
                  scripted drag: press, travel, release
--click=X,Y[,T]   synthesise a click; --rclick=X,Y[,T] uses the other button,
                  which is the only way a scripted run reaches a context menu
--mute            start with sound off
--stats           FPS / draw-call overlay
--screenshot=PATH render to PNG and exit
--at=SECONDS      capture at this time (else --frames=N)
--fps=N           frame cap, 0 for uncapped (default 60)
```

The icons come in two families and the difference is deliberate: a program
written for the desktop wears the Windows 7 look — saturated, rounded, lit from
the top with a gloss across its upper half and a shadow under it — and a program
written for the full screen wears the Windows 8 one, which is a flat tile in a
flat colour with a white glyph cut out of it. That contrast is the visual
argument version 8 was making, so both halves of it are drawn.

`--open` accepts any program id (`notepad`, `explorer`, `minesweeper`,
`calculator`, `paint`, `player`, `browser`, `orega`, `spreadsheet`, `display`,
`effects`, `appearance`, `advanced`, `terminal`, `taskmgr`, `mycomputer`,
`controlpanel`, `sound`, `language`, `allinone`, `voice`, `update`, `about`,
`run`, and the ones version 8 brought — `pcsettings`, `store`, `weather`,
`photos`, `notes`, `defrag`, `charmap`, `devmgr`, `regedit`, `access`,
`power`, `personalise`, `screenres`, `calculator8`) plus a few shorthands
that come with a document: `antivirus`, `scan` (opens the antivirus and runs
it), `revolutionary`, `tricks`, `photo`, `startmenu`, `shutdown`, `openwith`,
`sound`, `language`, `balloon`, and the version 8 screens — `start`, `charms`,
`settingscharm`, `switcher`, `lock`.

```
dotnet run --project src/Host -- --open=scan          # watch the antivirus find zero Popovs
dotnet run --project src/Host -- --open=start         # the tile board
dotnet run --project src/Host -- --theme=seven --wallpaper=seven --lang=en
```

## What is in it

**Boot** — on a machine that has never run it, setup asks its questions first,
in version 8's out-of-box shape: one flat colour edge to edge, one question on
it, a heading in very large type at the top left and a single button in the
bottom right. It asks for a language, a colour, a name and a look — and the
colour is the accent the tiles, the taskbar and every «Миминус 8» window are
built out of afterwards, so choosing one repaints the screen there and then and
the choice is kept in `settings.txt`. After that, every start is a fake POST
that fails to detect BOLGENOS, a splash — four flat tiles and a ring of dots,
the way version 8 replaced the marching blocks — and an XP-style welcome
screen — which has a song, synthesised at boot from the same oscillators as the
rest of the sound, whose tempo changes every couple of bars. Shutdown ends on
"it is now safe to turn off your computer".

**Version 8** — the whole of the reason this is version 8.

*Начальный экран* is a full screen of solid-colour tiles in three groups. Six of
them are alive and turn over every few seconds: the clock shows the time and then
the uptime, the antivirus counts Popovs and finds none, the weather reports 23
бенабря from Миминусоград. Typing any letter starts a search over every
registered program, the right button raises the app bar, and *Все приложения*
lists whatever is in `apps/` — a program dropped in by somebody else included.

*The old Start menu is still there.* Which one the Start button and the Windows
key open is a switch in the taskbar properties, and it opens the menu unless it
is changed. Neither hides the other: the menu has «Начальный экран» in its right
column, and the board's app bar has «Меню Пуск» on it.

*Чудо-кнопки* come out of the right edge when the pointer finds one of the right
corners, or on Win+C. Поиск, общий доступ, пуск, устройства, параметры — and
turning the computer off is three clicks inside «Параметры», exactly as it was.
The settings pane's volume and brightness sliders are real; brightness is a veil
drawn over the finished frame, and it is the only place it can be set.

*The corners and the left edge*: the bottom-left corner is where the Start button
used to be and shows a thumbnail of the board; the right button on it opens the
Win+X list of administrative places. The top-left corner brings out a strip of
the running programs.

*Специальные возможности* are four switches that each do something. The
magnifier reads the finished frame back out of the framebuffer around the
pointer and draws those pixels again, enlarged, in a strip along the top. The
on-screen keyboard types into the same list of characters the real keyboard
fills, so every text box in the system takes it without being told. The narrator
speaks through the engine the machine already has. High contrast is a theme —
black, white and cyan — so the whole shell repaints in two colours and keeps
working. The pointer is drawn from primitives, so «крупный указатель» is one
multiplication, and window animations can be turned off.

*Электропитание* is three plans and two timers, and all five are real: power in
this machine is the frame rate and the backlight, so the saver holds the loop to
thirty frames and dims the picture, high performance takes the cap off, the
screen really goes black after the first timer, and the machine really puts up
the lock screen after the second.

*The tray flyouts*: the clock drops a panel with an analogue face, the long
date and the month laid out as a grid with today ringed — the one seven drew and
eight kept — and the language indicator opens version 8's list of input methods,
with the current one marked and the other a click away.

*Экран блокировки* stands in front of the logon screen: a picture in the accent
colour with a drawn skyline along the bottom, the time in enormous numerals a
quarter of the way down, the date directly under it and a row of status glyphs
under that. A key, a click or a drag upwards lifts it out of the way; Win+L puts
it back. Behind it is version 8's logon screen — the round account picture in the
middle, the name under it, a password box with an arrow that lets anybody in
because there is no password, and the power and ease-of-access buttons in the
corners.

*Программы во весь экран*: F11 fills the screen with the focused program, over
the taskbar and without a frame; a bar with its name and a close button drops out
of the top edge when the pointer goes looking for it. «Параметры компьютера»,
«Магазин», «Погода», «Фотографии» and «Заметки» open that way by themselves.

*Snap*: a window carried against the left or right edge takes half the screen and
one carried against the top takes all of it, with the target shown before it is
let go. Win+Left, Win+Right, Win+Up and Win+Down do the same from the keyboard.

*Animations*: windows grow out of themselves when they open, shrink into the
taskbar when they are minimised, leave an outline behind when they close, and the
Start menu unrolls out of the taskbar rather than appearing.

**Stopping** — anything that escapes a frame takes the system down on its own
blue screen: an invented stop code over the real exception type, message and the
top of the stack, then a memory dump that counts to 100 and a key press that
restarts into POST. `crash` at the command prompt raises one on purpose.

**Shell** — desktop with 55 icons, column-major grid layout, rubber-band
selection, icon dragging with grid snap, renaming in place (F2), and the full
right-click menu including a working *Создать* submenu. Files and folders can be
renamed anywhere they appear; a folder called Windows refuses to go until Ctrl is
held, and then goes, and the system carries on — which is the whole point of that
scene in part 3.

Taskbar in the shape Windows 7 gave it — the superbar. Pinned programs and
running windows share one row of square icon buttons: a pinned program that is
not running is a dimmed icon, a running one gets a lit tile, several windows of
one program stack behind a single button, and hovering one raises a preview card
per window with a close cross on it. The far right end is the sliver that shows
the desktop. «Объединять кнопки панели задач» turns all of that off and gives
back one labelled button per window, with the pinned programs kept beside them
as the quick launch strip. Notification area, a clickable RU/EN indicator, and
the «Сервисное сообщение» balloon from part 1.

The row belongs to the user: a button can be picked up and carried along the bar
to a new place, anything dropped onto the bar from the desktop or a folder window
pins whatever opens it, and a button's own menu pins or unpins it. Dropping a
running program that was not pinned into the row pins it there, because the
pinned order is the only order the bar remembers. That order goes into
`settings.txt` as `taskbar_pinned`, so the bar looks the same after a restart.
Its property sheet works: locking shows or hides the grab handles, auto-hide
really slides the bar away and hands the space back to maximised windows, "keep
on top" is the order the layers are painted in, and grouping collapses several
windows of one program into a single button once the bar runs short of room.

The Windows key belongs to МИМИНУС while the window has the focus — a low-level
hook takes it from the host shell — and opens the Start menu, with Win+E, Win+R,
Win+F, Win+D, Win+U, Win+L and Win+Pause behind it. XP two-column Start menu with
a working *All Programs* tree, which lists whatever programs are registered so a
custom one appears without any menu being edited.

Files, folders and shortcuts drag between the desktop and any folder window, and
between folder windows: the drag belongs to the shell rather than to either end,
so a folder under the pointer takes the drop, the view takes it otherwise, and an
icon dropped on the desktop lands where it was let go. A mounted folder moves the
real directory on disk when the mount is writable.

Clicking the tray speaker drops the XP volume panel — a standing slider, a mute
box, and the wheel over the speaker for a quick change.

**Window manager** — draggable and resizable windows with eight-way edge grips,
z-order, focus, minimise/maximise/restore, modal dialogs that block their owner,
cascade and tile, and per-theme chrome.

**Programs**

| | |
|---|---|
| Блокнот | full text editor — caret, selection, word wrap, undo, find/replace, real clipboard. When it holds `АНТИВИРУС ГРЕВЦОВА.txt` it grows an *Антивирус* menu whose scan types its warnings into the document and reports how many Popovs it found. |
| Проводник | task pane that changes with the selection, address bar, four view modes, navigation history, context menus, *Открыть с помощью*. |
| Сапер | complete Minesweeper — LED counters, smiley, flags and question marks, chording, safe first click, three presets, best times. |
| Paint | editable bitmap with pencil/brush/eraser/line/rect/ellipse/fill/picker, zoom, undo, greyscale/invert/blur — under the Windows 7 ribbon: a blue *Файл* tab, the tools and thicknesses in named groups, a two-row palette beside the two current colours, the colour wheel behind *Изменение цветов*, and a status bar carrying the pointer position and a zoom slider. |
| Проигрыватель | playlist, transport, seek, equalizer, spectrum; audio tracks are generated chiptunes, and the video track draws the *BolgenOS on TV* news segment. |
| Веб-браузер Firefox / Орега | tabs in the title bar, history, and an address pill with the padlock and the bookmark star inside it; the menu bar is folded into one button — three lines for Фигефох, the round O for Орега. Renders Opera's Экспресс-панель as a grid of cards, the Mozilla download page, and the «скачать интернет» Google results from part 3. |
| Таблица Миминус | spreadsheet with a real recursive-descent formula parser (cell and range refs, `СУММ`/`ЧПС`/`ЕСЛИ` and friends, cycle detection), preloaded with the NPV/MIRR exercise from part 1. |
| Калькулятор Плюс | standard, scientific and unit-conversion modes, wearing the Windows 7 keypad: a white display with the running expression above the number, a memory row, and МС and MR greyed out until there is something in memory. |
| Калькулятор (во весь экран) | the same arithmetic behind version 8's face — flat keys, an accent equals, a mode switch on the app bar. Both calculators share one engine and each has a button that opens the other. |
| Свойства: Экран | all five tabs, live monitor preview, wallpaper and theme switching. |
| Командная строка | shell over the virtual filesystem — `dir`, `cd`, `type`, `start`, `color`, `scan`, `bolgenos`. |
| Всё в одном | the utility that "is suitable for everything": invented months (бенабрь, нехабрь), temperature, weather, forms of address, and how to pronounce and decline «Михаил Гревцов» — spoken aloud through the speech engine Windows already has. |
| Центр обновления | a page of the Control Panel and, separately, a page of «Параметры компьютера». Both read the one service, which fetches a version manifest published in the project's GitHub repository, then downloads, verifies, unpacks and installs what it announces — so a check begun on one page is already running when the other opens. |
| Что нового | the tour the system shows itself the first time it starts after an update — nine cards, each illustration built from the same icons and rectangles as the rest of the OS. |
| Проводник (лента) | the folder window wears the version 8 ribbon under every theme: Файл / Главная / Поделиться / Вид, groups with their names under them, a chevron that rolls it up, and a breadcrumb address bar with a search box. |
| Панель управления | rebuilt the way 7 did it: eight categories, each a blue heading with its tasks listed under it, a «Просмотр» control that swaps to the wall of icons, a column of links down the left and a search box. It is also a frame that navigates rather than one that opens windows: «Персонализация», «Разрешение экрана» and «Центр обновления» are pages inside it, the arrows walk the trail, and the breadcrumb is clickable. |
| Центр специальных возможностей | the magnifier, the narrator, the on-screen keyboard and high contrast, with the pointer size and the animation switch under them. |
| Электропитание | three power plans that really change the frame cap and the brightness, two timers that really turn the screen off and lock the machine, and what the power button should do. |
| Диспетчер устройств | the console tree. Half of it is invented with the computer; the display adapter is the card actually drawing the window, read out of the GL driver, and the sound device says whether OpenAL loaded. There is one device with a yellow mark on it, because there always is. |
| Редактор реестра | the two-pane editor over `registry.txt`. Changing the accent value repaints the system while the dialog is still closing. |
| Диспетчер задач | opens as a bare list of programs and one button. *Подробнее* opens it into the version 8 window: a process table with the CPU, memory, disk and network columns washed in colour by load and totalled in their headings, a performance page of graphs, and a start-up list. |
| Параметры компьютера | the second control panel version 8 shipped: theme and wallpaper pickers, the account name, brightness, DPI, refresh, colour depth, volume, the Start switches and the language — every one of them the same setting the old sheets change. |
| Магазин Миминус | a catalogue of what is installed already. Two entries are not: BOLGENOS is refused on principle, and Распознавание голоса 2 has been «скоро» since 2010. |
| Погода | full screen: Миминусоград, in the calendar «Всё в одном» invented, with a week of бенабрь and нехабрь. |
| Фотографии | every picture on the machine, mounts included, as a wall of thumbnails and one at a time with an app bar. |
| Заметки | coloured notes typed straight onto the board, kept as ordinary text files in «Мои документы» — so they show up in the folder window and open in Notepad. |
| Дефрагментация диска | the two bands of coloured blocks, the analysis, the blocks walking across, and a report explaining that a filesystem which lives in memory cannot be fragmented. |
| Таблица символов | the grid of glyphs with the magnifier that follows the pointer, the collecting box and a Copy button that really uses the clipboard. |
| | plus Sound properties, Properties sheets, Open With, Run, About, Распознавание голоса (which listens, thinks, and admits it is unfinished). |

**Display** — the settings are not decoration. The refresh rate on the Monitor
sheet really caps the frame rate, and colour quality really reduces the colour:
at 16-bit the shader snaps every channel to 32 levels and the title-bar
gradients band. DPI (96, 120 or 144) scales the layout *and* re-rasterises the
text: glyphs are baked into the atlas at the device size and drawn in logical
units, so at 144 DPI an 11-pixel Tahoma is built from 16 real pixels rather than
stretched from 11.

**Settings are kept** — theme, wallpaper, language, volume, scale, refresh,
smoothing and every taskbar, accessibility and power switch live in
`settings.txt` beside the executable, written whenever something changes. It is
plain `key = value` text, and it is not part of an update package, so it
survives one.

**And there is a registry** — `registry.txt`, one line per value with its full
path in front of it, holding what the machine *is* rather than how it has been
left: the product name, the build, the computer name, the registered owner, and
the accent colour the flat theme is built out of. That last one is the point of
having it: there is exactly one copy of that colour and the registry has it, so
changing it in «Параметры компьютера» and typing it into «Редактор реестра» are
the same act, and the second repaints the system without the window closing.
`regedit` opens the editor, and the accent is only ever *chosen* in PC settings —
the classic Personalisation window shows it and sends you there.

**Themes** — «Миминус 8» (flat: no gradients, no rounded corners, one accent
colour, and a close button that goes red), XP Luna in blue, olive and silver, a
Windows-7 pastiche for «Миминус 7», and Windows Classic. Fourteen procedurally
generated wallpapers.
Display properties carries the XP *Эффекты*, *Оформление* and *Дополнительно*
sheets, including a font-smoothing switch that cycles ClearType, standard and
no antialiasing and rebuilds the glyph atlas in place.

**Audio** — 28 synthesised effects (startup chord, shutdown fall, window and
menu movements, dialog stings, Minesweeper explosion, antivirus beeps) plus
generated music. Sounds are positioned in 3D from the screen coordinate that
triggered them, so a menu opening on the left is audibly on the left.

## Translations

All 1716 user-visible strings live in `src/Core/lang/ru.json` and
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
src/MiminusOS.slnx        24 projects: the engine, the host, and 22 program
                          assemblies — one per program family, and one each for
                          the full-screen ones

src/Core/                 Miminus.Core.dll — everything a program is written against
  Platform/               Win32, WGL context creation, OpenGL 3.3 bindings, clipboard
  Graphics/               batched renderer, GDI-baked glyph atlas, vector icons, PNG
  Audio/                  OpenAL bindings, DSP primitives, the sound set
  UI/                     theme, widgets, menus, text editor, immediate-mode context
  Sys/                    virtual filesystem, host mounts, localisation, update, speech
  Shell/                  window manager, desktop, taskbar, Start menu and Start
                          screen, charms, lock screen, boot, dialogs
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

Any assembly counts, not only the ones that shipped: dropping `MyProgram.dll`
into `apps/` — or into a folder of its own under it, next to whatever it depends
on — is all it takes to add a program, and it appears in *All Programs* by
itself. `samples/HelloProgram` is a working example, built against
`Miminus.Core.dll` and nothing else.

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

### Releases

Built packages live on the
[releases page](https://github.com/PBPUser/MiminusOSAI/releases), one per tag,
not in the tree. `latest.txt` names the current one and its SHA-256, which is
what the update centre downloads and checks.

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
