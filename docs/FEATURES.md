# Миминус ОС — feature inventory extracted from the reference videos

Source: MrGrevts, "Наш ответ BOLGENOS!" parts 1–3 (`references/miminus-ref{1,2,3}.mp4`).
Frames sampled at 1/3 s and read directly; this is the spec the app implements.

## The joke
BOLGENOS was a reskinned Ubuntu presented as a from-scratch national OS.
The parody answer: take Windows XP, put "МИМИНУС ОС / Copyright Попов" on the
wallpaper, rename bundled third-party programs, and call it your own OS. The
antivirus is a Notepad text file. Part 3 rebrands to "Миминус 7" and finishes by
"detecting Popov" and playing the BolgenOS TV news clip.

## Video 1 (ref1, 4:24) — the desktop tour
- Blue water wallpaper, dense grid of ~60 labelled desktop icons.
- Taskbar: Пуск button, task buttons, quick launch, tray icons, clock.
- XP two-column Start menu. Left: Интернет (Opera), Электронная почта
  (Outlook Express), Media Player Classic, µTorrent, Блокнот, Microsoft Office
  Word 2003, QIP 2005, "Все программы". Right: Мои документы, Мои рисунки,
  Моя музыка, Мой компьютер, Панель управления, Подключения, Принтеры и факсы,
  Справка и поддержка, Поиск, Выполнить… Footer: Выход из системы / Выключение.
- Tray balloon "Сервисное сообщение — Сеть প… Проверьте настройки подключения.
  (Connect Failed)".
- Hover tooltips on icons, incl. path tooltips
  ("Размещение: C:\Program Files\Windows Mobile Resources\…").
- Opera: Express-панель — 3×3 speed-dial thumbnails, address bar, toolbars,
  Яндекс search box, "Что такое Экспресс-панель?" / "Скрыть Экспресс-панель".
- Excel 2003: grid, formula bar with a long =ЕСЛИ() formula, column/row headers,
  Лист1/Лист2/Лист3 tabs, финансовая задача (NPV, MIRR, Срок окупаемости,
  Рентабельность), toolbars, status bar "Готово".
- Мой компьютер: XP task pane (Системные задачи / Другие места / Подробно),
  drives C:/D:/E:, DVD F:/G:/H:, Mobile Device, Nokia Phone Browser,
  USB-видеоустройство. Status bar "Объектов: 9".

## Video 2 (ref2, 5:00) — Миминус ОС proper
- Yellow (#FFD200) wallpaper, black "МИМИНУС ОС", subtitle "Copyright Попов".
- "Веб-браузер Firefox" (renamed) on mozilla-europe.org, Firefox 3.6 download page.
- Explorer window "Революционные дистрибутивы", path
  `C:\Documents and Settings\Admin\Рабочий стол\Революционные дистрибутивы`.
  Contents: `АНТИВИРУС ГРЕВЦОВА.txt`, `Эфрате.jpeg` (454×364 JPEG Image),
  `File.АЗЦqЧИГ` (0 КБ), `Сапер` (ярлык, tooltip «Сыграйте в игру "Сапер"!»).
  Task pane: Задачи для изображений / Задачи для файлов и папок / Другие места /
  Подробно. Status bar shows type, size, "Мой компьютер".
- Context menu → "Открыть с помощью" → Nero PhotoSnap Viewer, Paint.NET,
  Opera Internet Browser, Microsoft Office Picture Manager, Выбрать программу…
- Блокнот as the antivirus: title "АНТИВИРУС ГРЕВЦОВА.txt - Блокнот",
  menus Файл/Правка/Формат/Вид/Справка. Text is typed and edited live:
  "Вас приветствует антивирус Касперского 2009" → word replaced → "…Гревцова 2009",
  then "Ищи вирусы!", "то sсriрt ИЩИ ВИРУСЫ БЛЯТЬ!", "найдено 0 вирусов!".
- Paint.NET 3.35: canvas, tool box, Палитра window with colour wheel + Основной/
  Дополнительный swatches, menu bar, drawing freehand lines over a photo,
  status bar with cursor coords and image size.
- Сапер (Minesweeper): 9×9, LED mine counter + timer, smiley reset button,
  reveal / flag / numbers / chording, Игра + Справка menus.

## Video 3 (ref3, 7:03) — Миминус 7, BOLGENOS повержен
- Rebrand to "Миминус 7" with Windows-7-style wallpapers (blue flag, dark "7",
  olive/green, orange). Live wallpaper switching.
- Свойства: Экран dialog — tabs Темы / Рабочий стол / Заставка / Оформление /
  Параметры; monitor preview, wallpaper list, Расположение + Цвет dropdowns,
  Обзор…, "Настройка рабочего стола…", OK / Отмена / Применить.
- Folder "ПОЛЕЗНЫЕ ФИШКИ МИМИНУСА".
- Right-click → Создать submenu (Папку, Ярлык, Текстовый документ, Точечный
  рисунок, документ Microsoft Word/Excel, Архив WinRAR…).
- "Выбор программы" (Open With) dialog with program list + "Использовать для
  всех файлов такого типа" checkbox.
- Калькулятор Плюс: standard/scientific pad, Вид/Правка/Справка menus,
  unit-conversion mode.
- Opera + Google, search query "скачать интернет".
- Media player (AIMP-style): playlist, Библиотека / Интернет-радио /
  Оформление tabs, video output window playing "BolgenOS on TV", transport
  controls, seek bar, volume, "Воспроизведение остановлено" status.
- Finale in Блокнот: screen filled with "ОПАСНОСТЬ!" repeated, then
  ">>>НА ВАШЕМ КОМПЬЮТЕРЕ ОБНАРУЖЕН 0 ПОПОВ!" and "-Лечить?".
  Save-changes confirmation dialog on close.

## Implementation targets
Everything above is rebuilt from scratch: no OS controls are used. Win32 gives a
window and a GL 3.3 context; every pixel — chrome, text, icons, cursors — is
drawn by our own GLSL. Audio is OpenAL with procedurally synthesised PCM.
UI strings ship in Russian and English with a runtime toggle.
