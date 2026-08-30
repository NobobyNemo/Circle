const fs = require('node:fs');

const topics = [
  {
    id: 'root',
    title: 'Circle of Fifths — Desktop App',
    content: 'Десктопное приложение для изучения и визуализации кварта-квинтового круга, тренировки слуха (тюнер) и работы с аккордами/текстами песен. Реализовано на Avalonia UI + .NET 10. Музыкальное ядро вынесено в Circle.Core, UI и аудио — в Circle.Desktop.',
    parentId: null,
    tags: ['overview']
  },
  {
    id: 'overview',
    title: 'Overview и технологии',
    content: 'Основные возможности:\n- интерактивный Circle of Fifths с выбором тональности\n- отображение ступеней лада и аккордов\n- тюнер с захватом микрофона\n- библиотека песен с разбором ChordPro и аккордов над текстом\n- аппаратная вкладка настроек аудиовхода\n\nТехстек: C#, .NET 10, Avalonia 12.1, CommunityToolkit.Mvvm, NAudio, ManagedBass.\nРепозиторий также содержит legacy web-части (frontend/backend/shared), которые не используются в десктопе.',
    parentId: 'root',
    tags: ['overview', 'stack']
  },
  {
    id: 'architecture',
    title: 'Архитектура и проекты',
    content: 'Проекты:\n- Circle.Core — class library .NET 10: домен, музыкальные алгоритмы, утилиты.\n- Circle.Desktop — .NET 10 WinExe (net10.0-windows10.0.19041.0), Avalonia, аудио, UI, ViewModels.\n\nDI-контейнер не используется; сервисы создаются вручную (например, MediaCaptureStreamService в MainViewModel). ViewLocator по соглашению заменяет суффикс ViewModel на View. MainWindow содержит меню и ContentControl с DataTemplates для страниц.',
    parentId: 'root',
    tags: ['architecture', 'projects']
  },
  {
    id: 'build-run',
    title: 'Сборка и запуск',
    content: 'Сборка: dotnet build в папке csharp/. Circle.Desktop — WinExe, RuntimeIdentifier win-x64, SelfContained false. Целевая ОС Windows 10.0.19041.0 и выше.\n\nЗапуск разработки: dotnet run --project Circle.Desktop.\n\nПри Packaged=true генерируется MSIX/AppxPackage для sideloading с тестовым сертификатом Circle.Desktop_TestCertificate.pfx.',
    parentId: 'root',
    tags: ['build', 'deploy']
  },
  {
    id: 'domain-models',
    title: 'Доменные модели',
    content: 'Модели в Circle.Core.Domain:\n- Note: имя, октава, опциональный enharmonic alias; Equals учитывает enharmonic.\n- Key: нота + KeyType (Major/Minor).\n- KeyType: Major, Minor.\n- Mode: название + шаблон интервалов (полутонов).\n- ScaleDegree: нота ступени + тип аккорда (maj/min/dim).\n- KeySelection: (KeyType, Index) — сектор на круге.',
    parentId: 'root',
    tags: ['domain', 'models']
  },
  {
    id: 'music-engine',
    title: 'Музыкальное ядро',
    content: 'Алгоритмы в Circle.Core.Music: построение ладов, кварта-квинтовый круг, относительные тональности, аккорды по ступеням, вычисление частот. Используют readonly-коллекции и record/immutable-типы.',
    parentId: 'root',
    tags: ['music', 'core']
  },
  {
    id: 'circle-of-fifths',
    title: 'CircleOfFifths',
    content: 'CircleOfFifths задаёт порядок из 12 мажорных и 12 минорных тональностей. SegmentAngle = 30°. Умеет:\n- находить индекс ключа (IndexOf)\n- выдавать относительную тональность (RelativeKeyMap)\n- строить базовое трезвучие (ступени 0-2-4) по Ionian/Aeolian\n- строить типичную прогрессию I-IV-V-I для выбранного ключа\n\nМажорный порядок: C G D A E B F# Db Ab Eb Bb F.\nМинорный порядок: A E B F# C# G# D# Bb F C G D.',
    parentId: 'music-engine',
    tags: ['circle', 'fifths', 'keys']
  },
  {
    id: 'scale-building',
    title: 'ScaleBuilder, ScaleSpeller, ModeService',
    content: 'ScaleBuilder строит 7-нотную гамму по корневой ноте и шаблону интервалов Mode.Intervals, используя ChromaticPalette.\n\nScaleSpeller гарантирует, что каждая буква A–G встречается ровно один раз, выбирая enharmonic при предпочтении бемолей.\n\nModeService возвращает для ключа все 7 ладов с гаммой и аккордами по ступеням.',
    parentId: 'music-engine',
    tags: ['scales', 'modes', 'spelling']
  },
  {
    id: 'modes-and-chords',
    title: 'ModeCatalog и ModeChords',
    content: 'ModeCatalog содержит 7 диатонических ладов со шаблонами интервалов:\n- Ionian (Major): 2 2 1 2 2 2 1\n- Dorian, Phrygian, Lydian, Mixolydian, Aeolian (Minor), Locrian\n\nModeChords задаёт аккорды по ступеням для каждого лада, например:\n- Ionian: maj min min maj maj min dim\n- Aeolian: min dim maj min min maj maj',
    parentId: 'music-engine',
    tags: ['modes', 'chords']
  },
  {
    id: 'pitch-utils',
    title: 'FrequencyCalculator и расширения',
    content: 'FrequencyCalculator: частота ноты по A4=440 Гц, транспонирование на полтона с корректировкой октавы.\n\nKeyExtensions:\n- Label: для мажора — имя ноты, для минора — +m\n- GetRelative: относительная тональность\n- IsFlat: определяет бемольные тональности по фиксированным спискам\n- MatchesScaleNote/Chord: совпадение со ступенью/аккордом\n\nNoteExtensions: проверка enharmonic-эквивалентности и GetEnharmonic.',
    parentId: 'music-engine',
    tags: ['utils', 'extensions', 'pitch']
  },
  {
    id: 'audio',
    title: 'Аудиоподсистема',
    content: 'Аудио в Circle.Desktop.Audio построено вокруг IAudioCaptureService с событиями SamplesCaptured, ErrorOccurred, DebugMessage. Реализовано несколько backend-захватов; используемый выбирается вручную в MainViewModel. Все реализации конвертируют многоканальные форматы в моно float.',
    parentId: 'root',
    tags: ['audio', 'capture', 'microphone']
  },
  {
    id: 'audio-capture',
    title: 'Реализации захвата аудио',
    content: 'Реализации IAudioCaptureService:\n- MediaCaptureStreamService: WinRT MediaCapture.StartRecordToStreamAsync + InMemoryRandomAccessStream.\n- MediaFrameReaderCaptureService: WinRT MediaCapture + MediaFrameReader.\n- AudioGraphCaptureService: WinRT AudioGraph + frame output node.\n- WasapiAudioCaptureService: NAudio WasapiCapture; fallback на MME WaveInEvent и ASIO.\n- WaveInEventCaptureService: NAudio WASAPI с fallback на MME.\n- BassAudioCaptureService: ManagedBass с перебором WASAPI/DirectSound, форматов и частот.',
    parentId: 'audio',
    tags: ['audio', 'capture', 'naudio', 'bass']
  },
  {
    id: 'pitch-detection',
    title: 'PitchDetector',
    content: 'PitchDetector использует YIN-подобный NSDF-алгоритм:\n- накапливает float-сэмплы в кольцевом буфере\n- вычисляет normalized squared difference\n- ищет пик выше threshold 0.85 с параболической интерполяцией\n- диапазон ~65–1200 Гц\n\nPitchDetector.Analyze переводит частоту в ноту (A4=440) и deviation в cents, возвращая (NoteName, Cents, Frequency).',
    parentId: 'audio',
    tags: ['pitch', 'tuner', 'algorithm']
  },
  {
    id: 'tuner',
    title: 'TunerViewModel и тюнер',
    content: 'TunerViewModel управляет тюнером:\n- запрашивает доступ к микрофону (RequestAccessAsync)\n- стартует/останавливает захват\n- создаёт PitchDetector с SampleRate сервиса\n- обновляет DetectedNote, DetectedFrequency, Cents, NeedleAngle (clamped -45..45°, 1.5°/cent)\n\nSettingsViewModel позволяет выбрать AudioDevice из AvailableInputDevices и устанавливает его на capture-сервисе.',
    parentId: 'audio',
    tags: ['tuner', 'viewmodel', 'microphone']
  },
  {
    id: 'ui',
    title: 'Пользовательский интерфейс',
    content: 'UI на Avalonia 12 с FluentTheme, тёмная тема по умолчанию. MainWindow — меню (Circle, Tuner, Аккорды) и ContentControl с DataTemplate для CirclePage, Tuner, Songs, Settings. ViewLocator связывает ViewModel -> View по соглашению имён.',
    parentId: 'root',
    tags: ['ui', 'avalonia', 'views']
  },
  {
    id: 'main-window',
    title: 'MainWindow',
    content: 'MainWindow.axaml: окно 1200x800, фон #030712. Верхнее меню переключает страницы через MainViewModel. Кнопка ⚙ (Settings) в правом верхнем углу открывает SettingsWindow. ContentControl.DataTemplates связывают CirclePageViewModel, TunerViewModel, SongsViewModel, SettingsViewModel с Views.',
    parentId: 'ui',
    tags: ['main-window', 'navigation']
  },
  {
    id: 'circle-page',
    title: 'CirclePageView и панели круга',
    content: 'CirclePageViewModel объединяет CircleViewModel (визуальный круг) и CirclePanelViewModel (панель ступеней/аккордов).\n\nCircleView.axaml рисует сектора круга. CirclePanelView.axaml отображает ModeRows.\n\nВыбор тональности в круге синхронно обновляет SelectedKey в обоих ViewModel.',
    parentId: 'ui',
    tags: ['circle', 'views', 'panel']
  },
  {
    id: 'settings-view',
    title: 'SettingsWindow и выбор устройства',
    content: 'SettingsWindow/SettingsView позволяет выбрать входное аудиоустройство. SettingsViewModel получает список устройств из IAudioCaptureService.AvailableInputDevices и присваивает _captureService.SelectedInputDevice.',
    parentId: 'ui',
    tags: ['settings', 'audio-device']
  },
  {
    id: 'viewmodels',
    title: 'ViewModels',
    content: 'ViewModels в Circle.Desktop.ViewModels используют CommunityToolkit.Mvvm source generators (ObservableProperty, RelayCommand) и наследуют ViewModelBase. Команды связываются с UI через ICommand.',
    parentId: 'root',
    tags: ['viewmodel', 'mvvm']
  },
  {
    id: 'main-viewmodel',
    title: 'MainViewModel',
    content: 'MainViewModel — корневой DataContext MainWindow. Создаёт единый MediaCaptureStreamService, TunerViewModel, SettingsViewModel, SongsViewModel, CirclePageViewModel (из CircleViewModel + CirclePanelViewModel).\n\nУправляет CurrentPage, SelectedTabIndex, командами ShowCircle/Tuner/Songs/Settings. SettingsRequested открывает SettingsWindow.',
    parentId: 'viewmodels',
    tags: ['main-viewmodel', 'navigation']
  },
  {
    id: 'circle-viewmodel',
    title: 'CircleViewModel',
    content: 'CircleViewModel держит CircleOfFifths и DegreeHighlightBuilder. SelectedKey по умолчанию C major. При изменении SelectedKey обновляются RotationAngle (поворот круга), RelativeKey, DegreeHighlights.\n\nПредоставляет MajorKeys, MinorKeys, SegmentAngle, SelectKeyCommand.',
    parentId: 'viewmodels',
    tags: ['circle', 'viewmodel']
  },
  {
    id: 'circle-panel-viewmodel',
    title: 'CirclePanelViewModel',
    content: 'CirclePanelViewModel строит ModeRowViewModel для выбранной тональности: 7 ладов (для мажора с Ionian, для минора с Aeolian), ScaleSpeller, chord types.\n\nПоддерживает NoteDuration (мс), анимацию воспроизведения PlayDegree/PlayMode через Dispatcher.UIThread.',
    parentId: 'viewmodels',
    tags: ['circle-panel', 'viewmodel', 'playback']
  },
  {
    id: 'songs-viewmodel',
    title: 'SongsViewModel',
    content: 'SongsViewModel управляет библиотекой песен и редактором:\n- импорт ChordPro и ChordsAboveLyrics\n- создание новой песни, сохранение\n- графический и текстовый редактор\n- автопрокрутка, поиск, фильтр по алфавиту\n- работа с артистами/песнями через ChordLibraryService\n\nСохраняет в song.chordpro + song.tab.',
    parentId: 'viewmodels',
    tags: ['songs', 'viewmodel', 'editor']
  },
  {
    id: 'tuner-viewmodel',
    title: 'TunerViewModel',
    content: 'TunerViewModel управляет тюнером: запрос доступа к микрофону, старт/стоп захвата, создание PitchDetector с SampleRate, обновление DetectedNote/Frequency/Cents и NeedleAngle. При отказе микрофона открывает ms-settings:privacy-microphone.',
    parentId: 'viewmodels',
    tags: ['tuner', 'viewmodel']
  },
  {
    id: 'settings-viewmodel',
    title: 'SettingsViewModel',
    content: 'SettingsViewModel отображает AvailableInputDevices и устанавливает SelectedInputDevice на _captureService. При смене устройства обновляется аудиовход, используемый тюнером.',
    parentId: 'viewmodels',
    tags: ['settings', 'viewmodel', 'audio']
  },
  {
    id: 'chord-library',
    title: 'Библиотека песен и аккордов',
    content: 'Библиотека аккордов хранится на диске как папки Artist/Song. ChordLibraryService возвращает ChordLibraryItem (ObservableObject с Name и DirectoryPath) для артистов и песен. Путь к корню библиотеки сохраняется в %LocalAppData%/Circle/settings.json (ChordLibraryPath).\n\nПесня ищется по song.chordpro, .chordpro.txt, .txt; табы — song.tab.',
    parentId: 'root',
    tags: ['chord-library', 'songs', 'files']
  },
  {
    id: 'song-text-codec',
    title: 'SongTextCodec',
    content: 'SongTextCodec парсит/сериализует два формата:\n- ChordPro: [Am]текст\n- ChordsAboveLyrics: аккорды над строкой текста\n\nПоддерживает секции [Куплет], [Припев], [Verse], [Chorus] и повторы xN. Разбирает табулатуры из 6 строк eBGDAE.\n\nСериализует табы в song.tab с маркерами [TAB:n], а текст — в ChordPro с {TAB:n}.',
    parentId: 'chord-library',
    tags: ['chordpro', 'parser', 'tabs']
  },
  {
    id: 'song-models',
    title: 'Модели песен',
    content: 'SongDocument содержит Title и коллекцию SongLine.\n\nSongLine: Lyrics, Chords (ObservableCollection<SongChord>), SectionTitle/Details/RepeatCount, TabText/TabReference, FontSize.\n\nSongChord: Name, Position, FontSize; вычисляет BlockWidth/PixelPosition.\n\nChordLibraryItem — артист или песня с Name и DirectoryPath.',
    parentId: 'chord-library',
    tags: ['models', 'songs']
  },
  {
    id: 'chord-voicings',
    title: 'ChordVoicingCatalog',
    content: 'ChordVoicingCatalog содержит гитарные аппликатуры для аккордов A, Am, B, Bm, C, Cm, D, Dm, E, Em, F, Fm, G, Gm.\n\nФормат нотации: "6/E:0 5/A:2 ... 1/E:0" (струна/нота:лад).\n\nChordVoicingPopupViewModel отображает варианты для выбранного аккорда.',
    parentId: 'chord-library',
    tags: ['guitar', 'voicings', 'chords']
  },
  {
    id: 'helpers',
    title: 'Helpers UI',
    content: 'Вспомогательные классы в Circle.Desktop.Helpers для геометрии круга, подсветки ступеней и цветовой палитры.',
    parentId: 'root',
    tags: ['helpers', 'ui']
  },
  {
    id: 'circle-geometry',
    title: 'CircleGeometry',
    content: 'CircleGeometry — константы холста: Size 500, Center 250/250, OuterRadius 220, MajorInnerRadius 155, MinorOuterRadius 145, MinorInnerRadius 80.\n\nМетоды SegmentAngle, PolarToCartesian и DescribeAnnularSector для рисования секторов кольца.',
    parentId: 'helpers',
    tags: ['geometry', 'drawing']
  },
  {
    id: 'degree-highlight',
    title: 'DegreeHighlightBuilder',
    content: 'DegreeHighlightBuilder для выбранной тональности строит словарь note -> DegreeHighlight: ступень, кольцо (major/minor по chord type), цвет, нота. Использует ScaleSpeller и ModeService.',
    parentId: 'helpers',
    tags: ['highlight', 'degrees', 'colors']
  },
  {
    id: 'rainbow-colors',
    title: 'RainbowColors',
    content: 'RainbowColors — палитра для 7 ступеней (I–VII): красный, оранжевый, жёлтый, зелёный, циан, синий, фиолетовый, осветлённые на 50% смешиванием с белым. DegreeLabels: I, II, III, IV, V, VI, VII.',
    parentId: 'helpers',
    tags: ['colors', 'palette', 'degrees']
  }
];

const store = {
  topics: Object.fromEntries(topics.map(t => [t.id, t]))
};

fs.writeFileSync(
  'C:/Users/win/Desktop/circle/knowledge.json',
  JSON.stringify(store, null, 2)
);

console.log(`Wrote ${topics.length} topics to knowledge.json`);
