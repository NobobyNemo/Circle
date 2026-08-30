using System.Speech.Recognition;
using System.Speech.AudioFormat;
using System.Runtime.InteropServices;

Console.WriteLine("=== System.Speech Audio Capture Test (.NET 10) ===");

try
{
    var engine = new SpeechRecognitionEngine();
    engine.SetInputToDefaultAudioDevice();
    
    var audioFormat = engine.AudioFormat;
    Console.WriteLine($"Audio format: {audioFormat.EncodingFormat}, {audioFormat.SamplesPerSecond}Hz, {audioFormat.BitsPerSample}bit");
    
    // Try AudioLevelUpdated - this fires when audio is being captured
    var audioLevelUpdatedCount = 0;
    engine.AudioLevelUpdated += (s, e) =>
    {
        audioLevelUpdatedCount++;
        if (audioLevelUpdatedCount <= 5 || audioLevelUpdatedCount % 50 == 0)
            Console.WriteLine($"  AudioLevel: {e.AudioLevel} (#{audioLevelUpdatedCount})");
    };
    
    engine.AudioSignalProblemOccurred += (s, e) =>
    {
        Console.WriteLine($"  Audio problem: {e.AudioSignalProblem}");
    };
    
    // Try RecognizeAsync (Single mode) - this blocks until recognition completes
    var grammar = new DictationGrammar();
    engine.LoadGrammar(grammar);
    
    // Try with timeout
    engine.BabbleTimeout = TimeSpan.FromSeconds(3);
    engine.InitialSilenceTimeout = TimeSpan.FromSeconds(3);
    
    Console.WriteLine("Starting recognition (single mode, 3s timeout)...");
    var result = engine.Recognize(TimeSpan.FromSeconds(3));
    
    if (result != null)
        Console.WriteLine($"Recognized: {result.Text}");
    else
        Console.WriteLine("No recognition result (timeout or no speech)");
    
    Console.WriteLine($"Total AudioLevelUpdated events: {audioLevelUpdatedCount}");
    
    if (audioLevelUpdatedCount > 0)
        Console.WriteLine("SUCCESS: System.Speech can capture audio!");
    else
    {
        Console.WriteLine("No audio level events. Trying SetInputToAudioStream with MemoryStream...");
        
        // Try using SAPI COM directly via interop
        // ISpRecognizer -> SetInput -> ISpAudio
        // Actually, let's try a different approach: use RecognizeAsync with Multiple
        engine.RecognizeAsync(RecognizeMode.Multiple);
        Console.WriteLine("RecognizeAsync(Multiple) started, waiting 5s...");
        Thread.Sleep(5000);
        engine.RecognizeAsyncStop();
        Console.WriteLine($"AudioLevelUpdated events after Multiple: {audioLevelUpdatedCount}");
    }
    
    engine.Dispose();
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"HRESULT: 0x{Marshal.GetHRForException(ex):X8}");
}

Console.WriteLine("\n=== Done ===");
