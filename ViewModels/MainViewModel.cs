using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;
using AutoVideoCreator.Application.Interfaces;
using System.IO;
using Microsoft.Win32;
using System.Linq;
using FFMpegCore;
using System.Windows;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

namespace AutoVideoCreator.Application.ViewModels
{
    internal class MainViewModel : ViewModelBase, IMainViewModel
    {
        // Stałe aplikacji
        private readonly string apiKey = "";

        private readonly double charPerSecond = 20.0;
        // Stała dla ścieżki FFmpeg
        private readonly string ffmpegPath = @"D:\ffmpeg";

        private readonly int maxCharacters = 1000;
        private readonly double maxDurationSeconds = 60;
        private double _backgroundVolume = 0.10;

        private string _inputText = "Wpisz swój tekst tutaj...";

        private bool _isProcessing = false;

        private string _progressStatus = "";

        private double _progressValue;

        private string _subtitlesText = "Wpisz tekst napisów tutaj...";

        private double _videoBrightness = 0.0;

        private double _videoSaturation = 1.4;

        // Pola prywatne
        private string _videoSourceFolder = "K:\\Pobrane\\AutoMaker\\Movies";
        public MainViewModel()
        {
            if (!InitializeFFmpeg())
            {
                _isProcessing = true; // Blokujemy możliwość uruchomienia
            }
        }

        public int ApiUsageCharacters => Properties.Settings.Default.ApiUsageCharacters;

        public string AudioPath
        {
            get => Properties.Settings.Default.AudioPath;
            set
            {
                if (Properties.Settings.Default.AudioPath == value) return;
                Properties.Settings.Default.AudioPath = value;
                Properties.Settings.Default.Save();
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(CanCreateVideo));
            }
        }

        public double BackgroundVolume
        {
            get => _backgroundVolume;
            set
            {
                if (_backgroundVolume == value) return;
                _backgroundVolume = Math.Clamp(value, 0.0, 1.0);
                NotifyOfPropertyChange();
            }
        }

        public bool CanCreateVideo
        {
            get
            {
                if (string.IsNullOrWhiteSpace(InputText) || string.IsNullOrEmpty(AudioPath))
                    return false;

                var cleanText = CleanText(InputText);
                var estimatedDuration = cleanText.Length / charPerSecond;

                return !IsProcessing &&
                       !string.IsNullOrEmpty(AudioPath) &&
                       estimatedDuration <= maxDurationSeconds &&
                       cleanText.Length <= maxCharacters;
            }
        }

        public string InputText
        {
            get => _inputText;
            set
            {
                if (_inputText == value) return;
                _inputText = value;
                NotifyOfPropertyChange();
                SubtitlesText = value;
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (_isProcessing == value) return;
                _isProcessing = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(CanCreateVideo));
            }
        }

        public string ProgressStatus
        {
            get => _progressStatus;
            set
            {
                if (_progressStatus == value) return;
                _progressStatus = value;
                NotifyOfPropertyChange();
            }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                if (_progressValue == value) return;
                _progressValue = value;
                NotifyOfPropertyChange();
            }
        }

        public string SubtitlesText
        {
            get => _subtitlesText;
            set
            {
                if (_subtitlesText == value) return;
                _subtitlesText = value;
                NotifyOfPropertyChange();
            }
        }

        public string ValidationMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(InputText))
                    return "Wprowadź tekst";

                var cleanText = CleanText(InputText);
                var estimatedDuration = cleanText.Length / charPerSecond;

                if (estimatedDuration > maxDurationSeconds)
                    return "Tekst jest zbyt długi (max. 1 minuta)";
                if (cleanText.Length > maxCharacters)
                    return "Tekst jest zbyt długi (max. 1000 znaków)";
                if (string.IsNullOrEmpty(AudioPath))
                    return "Wybierz ścieżkę wyjściową";

                return string.Empty;
            }
        }

        public double VideoBrightness
        {
            get => _videoBrightness;
            set
            {
                if (_videoBrightness == value) return;
                _videoBrightness = Math.Clamp(value, -1.0, 1.0);
                NotifyOfPropertyChange();
            }
        }

        // Właściwości publiczne
        public double VideoSaturation
        {
            get => _videoSaturation;
            set
            {
                if (_videoSaturation == value) return;
                _videoSaturation = Math.Clamp(value, 0.0, 3.0);
                NotifyOfPropertyChange();
            }
        }

        public string VideoSourceFolder
        {
            get => _videoSourceFolder;
            set
            {
                if (_videoSourceFolder == value) return;
                _videoSourceFolder = value;
                NotifyOfPropertyChange();
            }
        }

        //public async Task Create()
        //{
        //    if (!CanCreateVideo)
        //        return;

        //    try
        //    {
        //        IsProcessing = true;
        //        ProgressValue = 0;
        //        ProgressStatus = "Inicjalizacja...";

        //        if (!Directory.Exists(AudioPath))
        //        {
        //            MessageBox.Show("Ścieżka wyjściowa nie istnieje!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }

        //        if (!Directory.Exists(VideoSourceFolder) || !Directory.GetFiles(VideoSourceFolder, "*.mp4").Any())
        //        {
        //            MessageBox.Show("Folder ze źródłowymi plikami wideo nie istnieje lub jest pusty!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }

        //        // Użycie istniejącego pliku TTS podczas testów
        //        string testTtsFile = @"K:\Pobrane\AutoMaker\504f292e-5a51-42aa-b08f-443446f7006a.mp3";

        //        if (!File.Exists(testTtsFile))
        //        {
        //            MessageBox.Show("Nie znaleziono testowego pliku TTS. Sprawdź czy ścieżka jest poprawna.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        //            return;
        //        }

        //        var fileName = Guid.NewGuid().ToString();
        //        string audioFilePath = Path.Combine(AudioPath, $"{fileName}.mp3");

        //        // Kopiujemy istniejący plik TTS zamiast generować nowy
        //        ProgressStatus = "Kopiowanie pliku audio...";
        //        ProgressValue = 20;
        //        File.Copy(testTtsFile, audioFilePath, true);

        //        // Pobranie długości pliku audio
        //        ProgressStatus = "Analiza pliku audio...";
        //        ProgressValue = 40;
        //        TimeSpan duration = GetDuration(AudioPath, $"{fileName}.mp3");

        //        // Generowanie wideo
        //        ProgressStatus = "Generowanie wideo...";
        //        ProgressValue = 60;
        //        bool result = await GenerateVideo(fileName, duration, InputText);

        //        if (result)
        //        {
        //            ProgressValue = 100;
        //            ProgressStatus = "Zakończono!";
        //            string finalPath = Path.Combine(AudioPath, $"{fileName}_final_subbed.mp4");
        //            MessageBox.Show($"Proces tworzenia wideo zakończony pomyślnie!\nPlik: {finalPath}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        //        }
        //        else
        //        {
        //            MessageBox.Show("Wystąpił błąd podczas tworzenia wideo.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        //    }
        //    finally
        //    {
        //        IsProcessing = false;
        //        ProgressStatus = "";
        //        ProgressValue = 0;
        //    }
        //}

        public void SelectAudioPath() => SelectFolder(path => AudioPath = path);

        public void SelectVideoSourceFolder() => SelectFolder(path => VideoSourceFolder = path);

        private async Task<double> CalculateStartTime(string videoPath, TimeSpan ttsDuration)
        {
            var probeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(ffmpegPath, "ffprobe.exe"),
                    Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };

            probeProcess.Start();
            string durationOutput = await probeProcess.StandardOutput.ReadToEndAsync();
            await probeProcess.WaitForExitAsync();

            // Domyślnie zaczynamy od początku
            double startTime = 0.0;

            // Próbujemy odczytać długość wideo
            if (probeProcess.ExitCode == 0 &&
                double.TryParse(durationOutput.Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double videoDuration))
            {
                // Jeśli wideo jest wystarczająco długie, wybieramy losowe miejsce do rozpoczęcia
                if (videoDuration > ttsDuration.TotalSeconds + 1)
                {
                    double maxStartTime = videoDuration - ttsDuration.TotalSeconds - 1;
                    startTime = new Random().NextDouble() * maxStartTime;
                }
            }

            return startTime;
        }

        // Metoda do czyszczenia tekstu - używana w wielu miejscach
        private string CleanText(string text) =>
            text.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace(" ", "");
        private void CleanupTempFiles(string[] filePaths)
        {
            foreach (var filePath in filePaths)
            {
                try
                {
                    if (File.Exists(filePath)) File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Błąd przy usuwaniu pliku tymczasowego {filePath}: {ex.Message}");
                }
            }
        }

        private async Task<bool> ConvertToVertical(string ffmpegPath, string tempVideoPath, string extractedPath)
        {
            try
            {
                ProgressStatus = "Przygotowanie formatu pionowego...";

                if (!File.Exists(extractedPath))
                    throw new FileNotFoundException("Nie znaleziono pliku źródłowego do konwersji pionowej.");

                // Konwersja wartości jasności i nasycenia do formatu invariant culture
                string saturationStr = VideoSaturation.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string brightnessStr = VideoBrightness.ToString(System.Globalization.CultureInfo.InvariantCulture);

                // Zmodyfikowane parametry - usunięcie niekompatybilnej opcji tune=fastdecode
                // i zmiana niektórych parametrów dla lepszej kompatybilności
                var verticalProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(ffmpegPath, "ffmpeg.exe"),
                        Arguments = $"-hwaccel cuda -i \"{extractedPath}\" " +
                                    $"-vf \"crop=iw/2.4:ih:x=iw/2-iw/4.8,scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,eq=saturation={saturationStr}:brightness={brightnessStr}\" " +
                                    $"-c:v h264_nvenc -preset p1 -rc:v vbr -cq:v 26 -b:v 5M " +
                                    $"-c:a copy -y \"{tempVideoPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    },
                    EnableRaisingEvents = true
                };

                var tcs = new TaskCompletionSource<bool>();
                var errorBuilder = new StringBuilder();

                verticalProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"FFmpeg: {e.Data}");
                    }
                };

                verticalProcess.Exited += (sender, e) => tcs.SetResult(true);

                // Zwiększa priorytet procesu dla szybszego przetwarzania
                verticalProcess.Start();
                verticalProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
                verticalProcess.BeginErrorReadLine();

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(3)));

                if (completedTask != tcs.Task)
                {
                    try { if (!verticalProcess.HasExited) verticalProcess.Kill(); }
                    catch { }
                    throw new TimeoutException("Przekroczono limit czasu podczas konwersji do formatu pionowego.");
                }

                // Dodajemy małe opóźnienie aby upewnić się, że plik został w pełni zapisany
                await Task.Delay(200);

                if (!File.Exists(tempVideoPath))
                {
                    throw new Exception($"Nie utworzono pliku wyjściowego. Kod wyjścia: {verticalProcess.ExitCode}\nSzczegóły:\n{errorBuilder}");
                }

                // Sprawdzamy tylko czy plik istnieje, ponieważ nawet z niezerowym kodem wyjścia
                // ffmpeg często generuje poprawny plik wideo
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas konwersji do formatu pionowego: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void CreateAnimatedSubtitles(string filePath, string tekst, TimeSpan duration)
        {
            try
            {
                var assBuilder = new StringBuilder(32768);
                var words = tekst.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                double totalDuration = duration.TotalSeconds;

                // Współczynnik przyspieszenia nadal 1.15 (15% szybciej)
                const double subtitleSpeedupFactor = 1.15;

                // Szybsze animacje dla lepszej płynności
                const int centerY = 960;
                const int centerX = 540;
                const int animDuration = 50;
                const int fadeDuration = 20;

                // Dodaj nagłówek ASS
                assBuilder.Append(
                    "[Script Info]\r\n" +
                    "ScriptType: v4.00+\r\n" +
                    "PlayResX: 1080\r\n" +
                    "PlayResY: 1920\r\n" +
                    "ScaledBorderAndShadow: yes\r\n\r\n" +
                    "[V4+ Styles]\r\n" +
                    "Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\r\n" +
                    "Style: Default,Montserrat,120,&H00FFFFFF,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,6,0,2,30,30,70,1\r\n\r\n" +
                    "[Events]\r\n" +
                    "Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n");

                var effectTemplates = new[] {
            // Statyczny (dla większości słów)
            $"{{\\pos({centerX},{centerY})\\fad({fadeDuration},{fadeDuration})}}{{0}}",
            // Od lewej (bliżej)
            $"{{\\move({centerX-100},{centerY},{centerX},{centerY},0,{animDuration})\\fad({fadeDuration},{fadeDuration})}}{{0}}",
            // Od prawej (bliżej)
            $"{{\\move({centerX+100},{centerY},{centerX},{centerY},0,{animDuration})\\fad({fadeDuration},{fadeDuration})}}{{0}}"
        };

                var random = new Random(Environment.TickCount);

                // Każde słowo pojawia się nieco szybciej niż jest wymawiane
                double effectiveTextDuration = totalDuration / subtitleSpeedupFactor;

                // Liczymy całkowitą liczbę znaków ze spacjami
                int totalChars = 0;
                foreach (var word in words)
                {
                    totalChars += word.Length;
                }
                // Dodajemy spacje między słowami (N-1 spacji dla N słów)
                if (words.Length > 0)
                    totalChars += words.Length - 1;

                double timePerChar = effectiveTextDuration / totalChars;

                // Opóźnienie początkowe 0.5s
                double initialOffset = 0.5;
                double currentStartTime = initialOffset;

                // LICZNIKI znaków interpunkcyjnych
                int periodCount = 0;      // licznik kropek
                int questionCount = 0;    // licznik znaków zapytania 
                int exclamationCount = 0; // licznik wykrzykników

                // Podstawowe czasy pauz po znakach interpunkcyjnych
                var punctuationBaseTimes = new Dictionary<string, double>
        {
            {".", 0.6},  // bazowa pauza po kropce
            {"!", 0.6},  // bazowa pauza po wykrzykniku
            {"?", 1.0},  // bazowa pauza po znaku zapytania
            {",", 0.35}  // bazowa pauza po przecinku (bez progresji)
        };

                // Pomoże śledzić czy jesteśmy po znaku interpunkcyjnym
                bool afterPunctuation = true; // Traktujemy pierwszy wyraz jak po interpunkcji

                // Generowanie napisów dla każdego słowa
                for (int i = 0; i < words.Length; i++)
                {
                    string word = words[i];
                    int effectIndex = 0;

                    // Wykryj znaki interpunkcyjne na końcu słowa
                    char lastChar = word.Length > 0 ? word[word.Length - 1] : ' ';
                    string lastCharStr = lastChar.ToString();
                    bool hasPunctuation = punctuationBaseTimes.ContainsKey(lastCharStr);

                    // Obliczanie czasu trwania słowa
                    double charCount = word.Length;
                    double wordDuration = charCount * timePerChar;

                    // Minimalne czasy wyświetlania - nie zmieniamy bo są dobre
                    if (word.Length <= 2)
                        wordDuration = Math.Max(wordDuration, 0.12);
                    else if (word.Length <= 5)
                        wordDuration = Math.Max(wordDuration, 0.18);
                    else
                        wordDuration = Math.Max(wordDuration, 0.22);

                    // Specjalne efekty po znakach interpunkcyjnych lub na początku
                    if (afterPunctuation || i == 0)
                    {
                        // Po znaku interpunkcyjnym lub na początku - 75% szans na animację
                        effectIndex = random.Next(100) < 75 ? 1 + random.Next(2) : 0;
                        afterPunctuation = false;
                    }
                    else
                    {
                        // Normalne słowa - 85% szans na statyczny tekst
                        effectIndex = random.Next(100) < 85 ? 0 : 1 + random.Next(2);
                    }

                    double endTime = currentStartTime + wordDuration;

                    // Formatowanie czasu
                    string startTimeStr = FormatAssTime(currentStartTime);
                    string endTimeStr = FormatAssTime(endTime);

                    string effectText = effectTemplates[effectIndex].Replace("{0}", word);

                    assBuilder.AppendFormat("Dialogue: 0,{0},{1},Default,,0,0,0,,{2}\r\n",
                                           startTimeStr, endTimeStr, effectText);

                    currentStartTime = endTime;

                    // Dodanie pauzy po znakach interpunkcyjnych
                    if (hasPunctuation)
                    {
                        // Pobierz bazowy czas pauzy
                        double basePause = punctuationBaseTimes[lastCharStr];
                        double actualPause = basePause;

                        // ZWIĘKSZONE PROGRESYWNE PAUZY - KLUCZOWA ZMIANA
                        if (lastCharStr == ".")
                        {
                            periodCount++;

                            // Pierwsza kropka - normalna pauza
                            if (periodCount == 1)
                                actualPause = basePause; // 0.6s
                                                         // Druga kropka - znacznie dłuższa pauza
                            else if (periodCount == 2)
                                actualPause = basePause * 2.0; // 1.2s zamiast 0.9s
                                                               // Trzecia kropka - jeszcze dłuższa pauza
                            else if (periodCount == 3)
                                actualPause = basePause * 2.5; // 1.5s zamiast 1.08s
                                                               // Czwarta i kolejne kropki - maksymalna pauza
                            else
                                actualPause = basePause * 3.0; // 1.8s

                            Debug.WriteLine($"Kropka #{periodCount}, pauza: {actualPause:F2}s");
                        }
                        // Podobnie dla znaków zapytania
                        else if (lastCharStr == "?")
                        {
                            questionCount++;

                            if (questionCount == 1)
                                actualPause = basePause; // 1.0s
                            else if (questionCount == 2)
                                actualPause = basePause * 1.8; // 1.8s zamiast 1.4s
                            else
                                actualPause = basePause * 2.3; // 2.3s zamiast 1.7s

                            Debug.WriteLine($"Znak zapytania #{questionCount}, pauza: {actualPause:F2}s");
                        }
                        // Podobnie dla wykrzykników
                        else if (lastCharStr == "!")
                        {
                            exclamationCount++;

                            if (exclamationCount == 1)
                                actualPause = basePause; // 0.6s
                            else if (exclamationCount == 2)
                                actualPause = basePause * 2.0; // 1.2s zamiast 0.9s
                            else
                                actualPause = basePause * 2.5; // 1.5s zamiast 1.08s

                            Debug.WriteLine($"Wykrzyknik #{exclamationCount}, pauza: {actualPause:F2}s");
                        }

                        currentStartTime += actualPause;
                        afterPunctuation = true;
                    }
                    else
                    {
                        // Mikroprzerwy między słowami
                        currentStartTime += 0.01;
                    }
                }

                // Zapisz plik
                File.WriteAllText(filePath, assBuilder.ToString(), Encoding.UTF8);
                Debug.WriteLine($"Utworzono napisy ASS z łącznym czasem {currentStartTime:F2}s przy długości TTS {totalDuration:F2}s");
                Debug.WriteLine($"Opóźnienie początkowe: {initialOffset}s, całkowity czas z opóźnieniem: {currentStartTime:F2}s");
                Debug.WriteLine($"Statystyki interpunkcji: {periodCount} kropek, {questionCount} znaków zapytania, {exclamationCount} wykrzykników");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd w CreateAnimatedSubtitles: {ex.Message}");
                throw;
            }
        }


        private async Task<bool> ExtractVideoSegment(string inputVideo, string outputPath, double startTime, TimeSpan duration)
        {
            try
            {
                string startTimeStr = startTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string durationStr = duration.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string volumeStr = BackgroundVolume.ToString(System.Globalization.CultureInfo.InvariantCulture);

                var extractProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(ffmpegPath, "ffmpeg.exe"),
                        Arguments = $"-ss {startTimeStr} -i \"{inputVideo}\" -t {durationStr} -c:v h264_nvenc -preset p1 -b:v 5M -c:a aac -af \"volume={volumeStr}\" -y \"{outputPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    },
                    EnableRaisingEvents = true
                };

                var tcs = new TaskCompletionSource<bool>();
                var errorBuilder = new StringBuilder();

                extractProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"FFmpeg Extract: {e.Data}");
                    }
                };

                extractProcess.Exited += (sender, e) => tcs.SetResult(true);

                extractProcess.Start();
                extractProcess.BeginErrorReadLine();

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(2)));

                if (completedTask != tcs.Task)
                {
                    try { if (!extractProcess.HasExited) extractProcess.Kill(); }
                    catch { }
                    throw new TimeoutException("Przekroczono limit czasu podczas wycinania fragmentu wideo.");
                }

                if (extractProcess.ExitCode != 0 || !File.Exists(outputPath))
                {
                    throw new Exception($"Błąd podczas wycinania fragmentu wideo. Kod wyjścia: {extractProcess.ExitCode}\nSzczegóły:\n{errorBuilder}");
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przy przetwarzaniu wideo: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Pomocnicza metoda formatująca czas dla ASS
        private string FormatAssTime(double seconds)
        {
            TimeSpan ts = TimeSpan.FromSeconds(seconds);
            return $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}.{(ts.Milliseconds / 10):D2}";
        }

        private async Task<bool> GenerateAudio(string ffmpegPath, string tempVideoPath, string audioVideoPath, string ttsAudioPath)
        {
            using var cancellationToken = new CancellationTokenSource();
            try
            {
                ProgressStatus = "Dodawanie ścieżki dźwiękowej...";

                // Sprawdź czy pliki wejściowe istnieją
                if (!File.Exists(tempVideoPath))
                    throw new FileNotFoundException("Nie znaleziono pliku wideo do dodania audio.");
                if (!File.Exists(ttsAudioPath))
                    throw new FileNotFoundException("Nie znaleziono pliku audio TTS.");

                var audioProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(ffmpegPath, "ffmpeg.exe"),
                        Arguments = $"-i \"{tempVideoPath}\" -i \"{ttsAudioPath}\" -filter_complex \"[0:a][1:a]amix=inputs=2:duration=first:dropout_transition=2[a]\" -map 0:v -map \"[a]\" -c:v copy -c:a aac -shortest -y \"{audioVideoPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    },
                    EnableRaisingEvents = true
                };

                var audioTcs = new TaskCompletionSource<bool>();
                var errorBuilder = new StringBuilder();

                audioProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"FFmpeg Audio: {e.Data}");
                    }
                };

                audioProcess.Exited += (sender, e) => audioTcs.SetResult(true);

                audioProcess.Start();
                audioProcess.BeginErrorReadLine();

                var audioCompletedTask = await Task.WhenAny(audioTcs.Task, Task.Delay(TimeSpan.FromMinutes(5)));

                if (audioCompletedTask != audioTcs.Task)
                {
                    try { if (!audioProcess.HasExited) audioProcess.Kill(); }
                    catch { }
                    throw new TimeoutException("Przekroczono limit czasu podczas dodawania ścieżki dźwiękowej.");
                }

                if (audioProcess.ExitCode != 0 || !File.Exists(audioVideoPath))
                {
                    throw new Exception($"Błąd podczas dodawania ścieżki dźwiękowej. Kod wyjścia: {audioProcess.ExitCode}\nSzczegóły:\n{errorBuilder}");
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania ścieżki dźwiękowej: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async Task<bool> GenerateSubtitles(string tekst, string ffmpegPath, string audioVideoPath, string finalVideoPath)
        {
            using var cancellationToken = new CancellationTokenSource();
            try
            {
                ProgressStatus = "Dodawanie napisów...";

                if (!File.Exists(audioVideoPath))
                    throw new FileNotFoundException("Nie znaleziono pliku wideo do dodania napisów.");

                // Tworzenie pliku ASS zamiast SRT
                string assFilePath = Path.Combine(Path.GetDirectoryName(finalVideoPath),
                                               Path.GetFileNameWithoutExtension(finalVideoPath) + ".ass");

                // Pobieranie długości wideo
                var ffprobeExe = Path.Combine(ffmpegPath, "ffprobe.exe");
                var probeProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffprobeExe,
                        Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{audioVideoPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };

                probeProcess.Start();
                string durationOutput = await probeProcess.StandardOutput.ReadToEndAsync();
                await probeProcess.WaitForExitAsync();

                double audioDurationSeconds = 30;
                if (probeProcess.ExitCode == 0 &&
                    double.TryParse(durationOutput.Trim(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double durationSeconds))
                {
                    audioDurationSeconds = durationSeconds;
                }

                // Generowanie pliku ASS z animacjami
                try
                {
                    CreateAnimatedSubtitles(assFilePath, tekst, TimeSpan.FromSeconds(audioDurationSeconds));
                    Debug.WriteLine($"Wygenerowano plik ASS: {assFilePath}");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Błąd podczas generowania napisów: {ex.Message}");
                }

                // Zamieniamy backslashe na forward slashe z zachowaniem jednego backslasha przed dwukropkiem
                string modifiedPath = assFilePath
                    .Replace("\\", "/")
                    .Replace(":", "\\:");

                // Używamy ASS do renderowania napisów
                var captionProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(ffmpegPath, "ffmpeg.exe"),
                        Arguments = $"-i \"{audioVideoPath}\" -vf \"ass='{modifiedPath}'\" -c:a copy -y \"{finalVideoPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true
                    },
                    EnableRaisingEvents = true
                };

                Debug.WriteLine($"Komenda FFmpeg: {captionProcess.StartInfo.Arguments}");

                var captionTcs = new TaskCompletionSource<bool>();
                var errorBuilder = new StringBuilder();
                var outputBuilder = new StringBuilder();

                captionProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"FFmpeg Caption Error: {e.Data}");
                    }
                };

                captionProcess.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        Debug.WriteLine($"FFmpeg Caption Output: {e.Data}");
                    }
                };

                captionProcess.Exited += (sender, e) => captionTcs.SetResult(true);

                captionProcess.Start();
                captionProcess.BeginErrorReadLine();
                captionProcess.BeginOutputReadLine();

                var captionCompletedTask = await Task.WhenAny(captionTcs.Task, Task.Delay(TimeSpan.FromMinutes(5)));

                if (captionCompletedTask != captionTcs.Task)
                {
                    try { if (!captionProcess.HasExited) captionProcess.Kill(); }
                    catch { }
                    throw new TimeoutException("Przekroczono limit czasu podczas dodawania napisów.");
                }

                if (captionProcess.ExitCode != 0 || !File.Exists(finalVideoPath))
                {
                    // Zapisz pełny log do pliku dla analizy
                    string logPath = Path.Combine(Path.GetDirectoryName(finalVideoPath), "ffmpeg_error_log.txt");
                    File.WriteAllText(logPath,
                        $"ERROR LOG:\n{errorBuilder}\n\n" +
                        $"OUTPUT LOG:\n{outputBuilder}\n\n" +
                        $"COMMAND:\n{captionProcess.StartInfo.Arguments}\n\n" +
                        $"ASS CONTENT:\n{File.ReadAllText(assFilePath)}"
                    );

                    throw new Exception($"Błąd podczas dodawania napisów. Kod wyjścia: {captionProcess.ExitCode}\n" +
                                      $"Szczegóły zapisano w pliku: {logPath}");
                }

                Debug.WriteLine("Napisy animowane dodane pomyślnie!");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WYJĄTEK w GenerateSubtitles: {ex}");
                MessageBox.Show($"Błąd podczas dodawania napisów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private async Task GenerateTTS(string tekst, string sciezkaPliku)
        {
            try
            {
                string url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";

                // Escape special characters for JSON
                tekst = tekst.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

                // Konfiguracja z głosem Polyglot-1 i dostosowanymi parametrami
                string json = $@"{{
        ""input"": {{ ""text"": ""{tekst}"" }},
        ""voice"": {{
            ""languageCode"": ""en-US"",
            ""name"": ""en-US-Polyglot-1""
        }},
        ""audioConfig"": {{
            ""audioEncoding"": ""MP3"",
            ""pitch"": -11.0,
            ""speakingRate"": 1.32
        }}
        }}";

                Debug.WriteLine($"TTS Request: {DateTime.Now}, Voice: Polyglot-1, Characters: {tekst.Length}");

                using (HttpClient client = new HttpClient())
                {
                    var response = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                    string responseJson = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        string audioBase64 = System.Text.Json.JsonDocument.Parse(responseJson)
                                                    .RootElement.GetProperty("audioContent")
                                                    .GetString();

                        byte[] audioBytes = Convert.FromBase64String(audioBase64);
                        await File.WriteAllBytesAsync(sciezkaPliku, audioBytes);
                        Debug.WriteLine($"Plik audio zapisany: {sciezkaPliku}");

                        // Zwiększ licznik wykorzystanych znaków
                        int liczbaZnaków = tekst.Length;
                        Properties.Settings.Default.ApiUsageCharacters += liczbaZnaków;
                        Properties.Settings.Default.Save();
                        NotifyOfPropertyChange(nameof(ApiUsageCharacters));
                    }
                    else
                    {
                        Debug.WriteLine($"Błąd: {response.StatusCode} - {responseJson}");
                        throw new Exception($"Błąd API Google TTS: {response.StatusCode}\n{responseJson}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Błąd w GenerateTTS: {ex.Message}");
                throw;
            }
        }
        private async Task<bool> GenerateVideo(string fileName, TimeSpan estimatedDuration, string tekst)
        {
            // Lista wszystkich plików tymczasowych do późniejszego usunięcia
            List<string> tempFiles = new List<string>();

            try
            {
                // Ścieżki plików
                string tempVideoPath = Path.Combine(AudioPath, $"{fileName}_temp.mp4");
                string audioVideoPath = Path.Combine(AudioPath, $"{fileName}_audio.mp4");
                string finalVideoPath = Path.Combine(AudioPath, $"{fileName}_final_subbed.mp4");
                string ttsAudioPath = Path.Combine(AudioPath, $"{fileName}.mp3");
                string subtitlesPath = Path.Combine(AudioPath, $"{fileName}.ass");
                string extractedPath = Path.Combine(AudioPath, $"{fileName}_extract.mp4");
                string finalSubtitlesPath = Path.Combine(AudioPath, $"{fileName}_final_subbed.ass");

                // Dodanie wszystkich plików tymczasowych do listy
                tempFiles.Add(tempVideoPath);
                tempFiles.Add(audioVideoPath);
                tempFiles.Add(extractedPath);
                tempFiles.Add(ttsAudioPath);
                tempFiles.Add(subtitlesPath);
                tempFiles.Add(finalSubtitlesPath);

                var videoFiles = Directory.GetFiles(VideoSourceFolder, "*.mp4");
                if (videoFiles.Length == 0)
                {
                    MessageBox.Show("Brak plików wideo w folderze źródłowym", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                var randomVideo = videoFiles[new Random().Next(videoFiles.Length)];

                TimeSpan actualTtsDuration = GetDuration(Path.GetDirectoryName(ttsAudioPath), Path.GetFileName(ttsAudioPath));
                Debug.WriteLine($"Estymowana długość: {estimatedDuration.TotalSeconds}s, rzeczywista: {actualTtsDuration.TotalSeconds}s");

                var startTimeTask = CalculateStartTime(randomVideo, actualTtsDuration);

                var subtitlesTask = Task.Run(() => CreateAnimatedSubtitles(subtitlesPath, SubtitlesText, actualTtsDuration));

                var startTime = await startTimeTask;
                await subtitlesTask;

                ProgressStatus = "Wycinanie fragmentu wideo...";
                if (!await ExtractVideoSegment(randomVideo, extractedPath, startTime, actualTtsDuration))
                    return false;

                if (!await ConvertToVertical(ffmpegPath, tempVideoPath, extractedPath))
                    return false;

                if (!await GenerateAudio(ffmpegPath, tempVideoPath, audioVideoPath, ttsAudioPath))
                    return false;

                if (!await GenerateSubtitles(SubtitlesText, ffmpegPath, audioVideoPath, finalVideoPath))
                    return false;

                // Czyszczenie wszystkich plików tymczasowych po zakończeniu
                CleanupTempFiles(tempFiles.ToArray());

                return true;
            }
            catch (Exception ex)
            {
                try { CleanupTempFiles(tempFiles.ToArray()); }
                catch { /* Ignorujemy błędy przy czyszczeniu */ }

                MessageBox.Show($"Błąd przy generowaniu wideo: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private TimeSpan GetDuration(string path, string fileName)
        {
            string fullPath = Path.Combine(path, fileName);
            var tfile = TagLib.File.Create(fullPath);
            return tfile.Properties.Duration;
        }

        private bool InitializeFFmpeg()
        {
            var ffmpegExe = Path.Combine(ffmpegPath, "ffmpeg.exe");
            var ffprobeExe = Path.Combine(ffmpegPath, "ffprobe.exe");

            if (!Directory.Exists(ffmpegPath) || !File.Exists(ffmpegExe) || !File.Exists(ffprobeExe))
            {
                MessageBox.Show(
                    "Nie znaleziono wymaganych plików FFmpeg. Proszę zainstalować FFmpeg w folderze D:\\ffmpeg\\",
                    "Brak FFmpeg",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }

            try
            {
                GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd konfiguracji FFmpeg: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return false;
            }
        }
        private void SelectFolder(Action<string> setPath)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                setPath(dialog.FolderName);
            }
        }

        public async Task Create()
        {
            if (!CanCreateVideo)
                return;

            try
            {
                IsProcessing = true;
                ProgressValue = 0;
                ProgressStatus = "Inicjalizacja...";

                if (!Directory.Exists(AudioPath))
                {
                    MessageBox.Show("Ścieżka wyjściowa nie istnieje!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Directory.Exists(VideoSourceFolder) || !Directory.GetFiles(VideoSourceFolder, "*.mp4").Any())
                {
                    MessageBox.Show("Folder ze źródłowymi plikami wideo nie istnieje lub jest pusty!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var fileName = Guid.NewGuid().ToString();
                string audioFilePath = Path.Combine(AudioPath, $"{fileName}.mp3");

                // Generowanie pliku TTS
                ProgressStatus = "Generowanie audio...";
                ProgressValue = 20;
                await GenerateTTS(InputText, audioFilePath);

                // Pobranie długości pliku audio
                ProgressStatus = "Analiza pliku audio...";
                ProgressValue = 40;
                TimeSpan duration = GetDuration(AudioPath, $"{fileName}.mp3");

                // Generowanie wideo
                ProgressStatus = "Generowanie wideo...";
                ProgressValue = 60;
                bool result = await GenerateVideo(fileName, duration, InputText);
                if (result)
                {
                    ProgressValue = 100;
                    ProgressStatus = "Zakończono!";
                    string finalPath = Path.Combine(AudioPath, $"{fileName}_final_subbed.mp4");
                    MessageBox.Show($"Proces tworzenia wideo zakończony pomyślnie!\nPlik: {finalPath}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Wystąpił błąd podczas tworzenia wideo.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Wystąpił błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsProcessing = false;
                ProgressStatus = "";
                ProgressValue = 0;
            }
        }
    }
}