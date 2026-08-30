# Кодстайл и лучшие практики Circle of Fifths (Desktop)

Этот документ описывает соглашения, которым мы придерживаемся в C#-коде десктопного приложения. Основан на официальных рекомендациях Microsoft (.NET Runtime / Roslyn / docs), C# 13 / .NET 10 и Avalonia 11/12.

## 1. Общие принципы

- Пишем идиоматичный современный C# 13: `file-scoped namespaces`, `record`/`record struct`, `init`-only свойства, switch expressions, pattern matching.
- Приоритет — читаемость и корректность, а не микрооптимизации. JIT .NET 10 хорошо деоптимизирует простой идиоматичный код.
- В hot paths (аудио, круг частот, рендеринг) избегаем лишних аллокаций.
- Публичный API должен быть null-safe: проект собирается с `<Nullable>enable</Nullable>`.

## 2. Именование и форматирование

- **PascalCase**: типы, методы, свойства, события, публичные поля-константы.
- **camelCase**: параметры и локальные переменные.
- **`_camelCase`**: приватные/внутренние поля экземпляра.
- **`s_camelCase`**: приватные/внутренние статические поля.
- Интерфейсы — с префиксом `I`.
- Используем `string`, `int` и т.д. вместо `System.String`, `System.Int32`.
- `var` допускается только когда тип очевиден из правой части; иначе пишем тип явно.

```csharp
public sealed class Note
{
    private readonly string _name;
    private static readonly Dictionary<string, Note> s_cache = new();

    public string Name => _name;
    public int Octave { get; }

    public override string ToString() => Name;
}
```

## 3. Классы, структуры и записи

- Используй `record` или `record struct` для неизменяемых value-like/DTO-типов.
- Используй `sealed` по умолчанию для классов, которые не планируются к наследованию.
- Избегай публичных мутабельных полей; вместо них — `init` или private setter.
- Primary constructors — только на простых типах, где параметр не захватывается в несколько методов.

```csharp
public sealed class TunerViewModel : ViewModelBase
{
    private readonly IAudioCaptureService _audioCapture;

    public TunerViewModel(IAudioCaptureService audioCapture)
    {
        ArgumentNullException.ThrowIfNull(audioCapture);
        _audioCapture = audioCapture;
    }
}
```

## 4. Nullability и исключения

- Не используй `!` для заглушки компилятора — исправляй контракт или используй `ArgumentNullException.ThrowIfNull`.
- Лови только те исключения, которые можешь обработать.
- Не лови общий `Exception` без фильтра.

## 5. Асинхронность

- `async/await` для I/O-bound операций.
- В библиотечном коде (Circle.Core) используй `ConfigureAwait(false)`.
- Никогда не используй `async void` (кроме обработчиков событий UI).

## 6. Производительность в .NET 10

- Пиши идиоматичный код — JIT .NET 10 лучше инлайнит, девиртуализует массивы и распределяет объекты на стеке, если они не «убегают».
- В горячих циклах используй `Span<T>` / `ReadOnlySpan<T>` и `ArrayPool<T>.Shared` вместо лишних аллокаций.
- Избегай LINQ в hot paths.
- Для аудиобуферов и математики круга — структуры, readonly, кэширование.
- Профилируй: `dotnet-counters`, `dotnet-trace`, dotTrace, dotMemory, BenchmarkDotNet.

## 7. Avalonia — UI и стиль

- Включай **CompiledBindings** по умолчанию (`<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>`). Для каждого корневого XAML указывай `x:DataType`. Исключения — `ReflectionBinding`.
- Используй `OneWay` привязки для read-only данных, уменьшай количество bindings и converters.
- Виртуализация для длинных списков: `VirtualizingStackPanel`, `ItemsRepeater`, `TreeDataGrid`.
- Дерево визуальных элементов должно быть максимально плоским; избегай глубокой вложенности.
- Для анимаций используй `RenderTransform`, `Opacity`, `Clip` (GPU-composite). Не анимируй `Width`, `Height`, `Margin`, `Padding`, `FontSize` — они запускают layout pass.
- Общие стили выноси в `Application.Styles` или `ThemeDictionaries`; избегай широких селекторов, замедляющих Style Matching.

## 8. MVVM и ViewModels

- ViewModels наследуют `ViewModelBase` и используют source generators `CommunityToolkit.Mvvm` (`[ObservableProperty]`, `[RelayCommand]`).
- Команды — `ICommand`. Избегай лишней логики в code-behind.
- Не обращайся к UI-элементам из ViewModel.

## 9. Работа с аудио

- `IAudioCaptureService` — единый контракт. Реализации конвертируют многоканальные форматы в моно float.
- Тяжёлые DSP-операции — не в UI-потоке.
- Буферы аудио — reuse, pooling, Span<float>.

## 10. Инструменты и автоматизация

- `.editorconfig` в `csharp/` описывает форматирование и стиль.
- Рекомендуется включать в `.csproj`:
  - `<Nullable>enable</Nullable>`
  - `<ImplicitUsings>enable</ImplicitUsings>`
  - `<AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>` (для Circle.Desktop)
- По мере готовности можно включить `EnforceCodeStyleInBuild` и `TreatWarningsAsErrors` для CI.

---

Источники: [Microsoft .NET Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions), [.NET 10 Performance Blog](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/), [Avalonia Performance Docs](https://docs.avaloniaui.net/docs/app-development/performance).
