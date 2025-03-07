using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System;
using AutoVideoCreator.Application.Interfaces;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using System.Linq;
using FFMpegCore;
using System.Windows;
using System.Threading;

namespace AutoVideoCreator.Application.ViewModels
{
    internal class MainViewModel : ViewModelBase, IMainViewModel
    {
        private readonly string apiKey = "";
        private string _videoSourceFolder = "K:\\Pobrane\\AutoMaker\\Movies";
        private string _inputText = "Wpisz swój tekst tutaj...";
        private bool _isProcessing = false;
        private string _subtitlesText = "Wpisz tekst napisów tutaj...";

        
        private double _videoSaturation = 1.4;  // Domyślna wartość saturacji (1.0 = normalne nasycenie)
        private double _videoBrightness = 0.0;  // Domyślna wartość jasności (0.0 = normalna jasność)
        private double _backgroundVolume = 0.2; // Głośność tła jako 20% oryginalnej głośności

        public double VideoSaturation
        {
            get { return _videoSaturation; }
            set
            {
                if (_videoSaturation == value)
                    return;
                _videoSaturation = Math.Clamp(value, 0.0, 3.0); // Ograniczenie zakresu od 0 do 3
                NotifyOfPropertyChange();
            }
        }

        public double VideoBrightness
        {
            get { return _videoBrightness; }
            set
            {
                if (_videoBrightness == value)
                    return;
                _videoBrightness = Math.Clamp(value, -1.0, 1.0); // Ograniczenie zakresu od -1 do 1
                NotifyOfPropertyChange();
            }
        }

        public double BackgroundVolume
        {
            get { return _backgroundVolume; }
            set
            {
                if (_backgroundVolume == value)
                    return;
                _backgroundVolume = Math.Clamp(value, 0.0, 1.0); // Ograniczenie zakresu od 0 do 1
                NotifyOfPropertyChange();
            }
        }


        public string InputText
        {
            get { return _inputText; }
            set
            {
                if (_inputText == value)
                    return;
                _inputText = value;
                NotifyOfPropertyChange();

                // Automatycznie aktualizuj tekst napisów gdy zmienia się tekst TTS
                SubtitlesText = value;
            }
        }

        public string SubtitlesText
        {
            get { return _subtitlesText; }
            set
            {
                if (_subtitlesText == value)
                    return;
                _subtitlesText = value;
                NotifyOfPropertyChange();
            }
        }

        public bool IsProcessing
        {
            get { return _isProcessing; }
            set
            {
                if (_isProcessing == value)
                    return;
                _isProcessing = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(CanCreateVideo));
            }
        }

        private string _progressStatus = "";
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

        private double _progressValue;
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

        public bool CanCreateVideo
        {
            get
            {
                if (string.IsNullOrWhiteSpace(InputText) || string.IsNullOrEmpty(AudioPath))
                    return false;

                // Usuwamy białe znaki i znaki nowej linii do obliczenia rzeczywistej długości
                var cleanText = InputText.Replace("\r", "")
                                       .Replace("\n", "")
                                       .Replace("\t", "")
                                       .Replace(" ", "");

                // Zakładamy średnie tempo mówienia 15 znaków na sekundę (900 znaków/minutę)
                // To daje bardziej realistyczne oszacowanie dla polskiego TTS
                var estimatedDuration = cleanText.Length / 15.0; // w sekundach

                return !IsProcessing &&
                       !string.IsNullOrWhiteSpace(InputText) &&
                       !string.IsNullOrEmpty(AudioPath) &&
                       estimatedDuration <= 60 && // max 1 minuta
                       cleanText.Length <= 1000; // maksymalnie 1000 znaków
            }
        }

        public string ValidationMessage
        {
            get
            {
                if (string.IsNullOrWhiteSpace(InputText))
                    return "Wprowadź tekst";

                var cleanText = InputText.Replace("\r", "")
                                       .Replace("\n", "")
                                       .Replace("\t", "")
                                       .Replace(" ", "");

                var estimatedDuration = cleanText.Length / 15.0;

                if (estimatedDuration > 60)
                    return "Tekst jest zbyt długi (max. 1 minuta)";
                if (cleanText.Length > 1000)
                    return "Tekst jest zbyt długi (max. 1000 znaków)";
                if (string.IsNullOrEmpty(AudioPath))
                    return "Wybierz ścieżkę wyjściową";

                return string.Empty;
            }
        }

        public string VideoSourceFolder
        {
            get { return _videoSourceFolder; }
            set
            {
                if (_videoSourceFolder == value)
                    return;
                _videoSourceFolder = value;
                NotifyOfPropertyChange();
            }
        }

        public string AudioPath
        {
            get { return Properties.Settings.Default.AudioPath; }
            set
            {
                if (Properties.Settings.Default.AudioPath == value)
                    return;

                Properties.Settings.Default.AudioPath = value;
                Properties.Settings.Default.Save();
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(CanCreateVideo));
            }
        }

        public int ApiUsageCharacters
        {
            get => Properties.Settings.Default.ApiUsageCharacters;
        }

        public MainViewModel()
        {
            string ffmpegPath = @"D:\ffmpeg";
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
                _isProcessing = true; // Blokujemy możliwość uruchomienia
                return;
            }

            try
            {
                GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd konfiguracji FFmpeg: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                _isProcessing = true; // Blokujemy możliwość uruchomienia
            }
        }

        public void SelectAudioPath()
        {
            var sfd = new OpenFolderDialog();
            bool? result = sfd.ShowDialog();
            if (result == true)
            {
                AudioPath = sfd.FolderName;
            }
        }

        public void SelectVideoSourceFolder()
        {
            var sfd = new OpenFolderDialog();
            bool? result = sfd.ShowDialog();
            if (result == true)
            {
                VideoSourceFolder = sfd.FolderName;
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

        //        var fileName = Guid.NewGuid().ToString();
        //        string audioFilePath = Path.Combine(AudioPath, $"{fileName}.mp3");

        //        // Generowanie pliku TTS
        //        ProgressStatus = "Generowanie audio...";
        //        ProgressValue = 20;
        //        await GenerateTTS(InputText, audioFilePath);

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

                // Użycie istniejącego pliku TTS podczas testów
                string testTtsFile = @"K:\Pobrane\AutoMaker\504f292e-5a51-42aa-b08f-443446f7006a.mp3";

                if (!File.Exists(testTtsFile))
                {
                    MessageBox.Show("Nie znaleziono testowego pliku TTS. Sprawdź czy ścieżka jest poprawna.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var fileName = Guid.NewGuid().ToString();
                string audioFilePath = Path.Combine(AudioPath, $"{fileName}.mp3");

                // Kopiujemy istniejący plik TTS zamiast generować nowy
                ProgressStatus = "Kopiowanie pliku audio...";
                ProgressValue = 20;
                File.Copy(testTtsFile, audioFilePath, true);

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

        private TimeSpan GetDuration(string path, string fileName)
        {
            string fullPath = Path.Combine(path, fileName);
            var tfile = TagLib.File.Create(fullPath);
            return tfile.Properties.Duration;
        }

        private async Task<bool> GenerateVideo(string fileName, TimeSpan ttsDuration, string tekst)
        {
            try
            {
                // Ścieżki plików
                string ffmpegPath = @"D:\ffmpeg";
                string tempVideoPath = Path.Combine(AudioPath, $"{fileName}_temp.mp4");
                string audioVideoPath = Path.Combine(AudioPath, $"{fileName}_audio.mp4");
                string finalVideoPath = Path.Combine(AudioPath, $"{fileName}_final_subbed.mp4");
                string ttsAudioPath = Path.Combine(AudioPath, $"{fileName}.mp3");
                string subtitlesPath = Path.Combine(AudioPath, $"{fileName}.srt");
                string extractedPath = Path.Combine(AudioPath, $"{fileName}_extract.mp4");
                CancellationTokenSource cancellationToken = new CancellationTokenSource();

                // Wybór losowego pliku wideo - bez weryfikacji, obsługa błędów bezpośrednio w trakcie konwersji
                var videoFiles = Directory.GetFiles(VideoSourceFolder, "*.mp4");
                if (videoFiles.Length == 0)
                {
                    MessageBox.Show("Brak plików wideo w folderze źródłowym", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Wybierz losowy plik wideo
                var randomVideo = videoFiles[new Random().Next(videoFiles.Length)];
                var startTime = 0.0; // Domyślnie zaczynamy od początku

                // Sprawdzamy podstawowe informacje o pliku za pomocą FFprobe (bezpośrednio)
                var ffprobeExe = Path.Combine(ffmpegPath, "ffprobe.exe");
                var probeProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffprobeExe,
                        Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{randomVideo}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true
                    }
                };

                probeProcess.Start();
                string durationOutput = await probeProcess.StandardOutput.ReadToEndAsync();
                await probeProcess.WaitForExitAsync();

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
                    else
                    {
                        // Jeśli wideo jest za krótkie, zaczynamy od początku
                        startTime = 0;
                    }
                }

                // Generowanie napisów
                CreateSubtitles(subtitlesPath, SubtitlesText, ttsDuration);

                // KROK 1: Wycinanie fragmentu wideo z przyciszonym dźwiękiem
                ProgressStatus = "Wycinanie fragmentu wideo...";

                try
                {
                    // Bezpośrednie wywołanie FFmpeg dla szybszego działania z opcją -ss przed inputem
                    string startTimeStr = startTime.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string durationStr = ttsDuration.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string volumeStr = BackgroundVolume.ToString(System.Globalization.CultureInfo.InvariantCulture);

                    var extractProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = Path.Combine(ffmpegPath, "ffmpeg.exe"),
                            // Zachowujemy ścieżkę dźwiękową (-c:a aac) i przyciszamy ją (-af "volume=0.2")
                            Arguments = $"-ss {startTimeStr} -i \"{randomVideo}\" -t {durationStr} -c:v h264_nvenc -preset p1 -b:v 5M -c:a aac -af \"volume={volumeStr}\" -y \"{extractedPath}\"",
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

                    if (extractProcess.ExitCode != 0 || !File.Exists(extractedPath))
                    {
                        throw new Exception($"Błąd podczas wycinania fragmentu wideo. Kod wyjścia: {extractProcess.ExitCode}\nSzczegóły:\n{errorBuilder}");
                    }

                    // KROK 2: Konwersja do formatu pionowego z modyfikacją nasycenia i jasności
                    await ConvertToVertical(ffmpegPath, tempVideoPath, extractedPath);

                    // KROK 3: Dodawanie audio TTS
                    await GenerateAudio(ffmpegPath, tempVideoPath, audioVideoPath, ttsAudioPath);

                    // KROK 4: Dodawanie napisów
                    await GenerateSubtitles(SubtitlesText, ffmpegPath, audioVideoPath, finalVideoPath);

                    // Sprzątanie plików tymczasowych
                    try
                    {
                        if (File.Exists(tempVideoPath)) File.Delete(tempVideoPath);
                        if (File.Exists(audioVideoPath)) File.Delete(audioVideoPath);
                        if (File.Exists(extractedPath)) File.Delete(extractedPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Błąd przy usuwaniu plików tymczasowych: {ex.Message}");
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd przy przetwarzaniu wideo: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przy generowaniu wideo: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }



        private async Task ConvertToVertical(string ffmpegPath, string tempVideoPath, string extractedPath)
        {
            using var cancellationToken = new CancellationTokenSource();
            try
            {
                ProgressStatus = "Przygotowanie formatu pionowego...";

                if (!File.Exists(extractedPath))
                    throw new FileNotFoundException("Nie znaleziono pliku źródłowego do konwersji pionowej.");

                // Konwersja wartości jasności i nasycenia do formatu invariant culture
                string saturationStr = VideoSaturation.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string brightnessStr = VideoBrightness.ToString(System.Globalization.CultureInfo.InvariantCulture);

                var verticalProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(ffmpegPath, "ffmpeg.exe"),
                        // WAŻNA ZMIANA: Usunąłem opcję -an i dodałem -c:a copy, aby zachować ścieżkę audio
                        // Dodałem również filtr eq dla nasycenia i jasności
                        Arguments = $"-i \"{extractedPath}\" -vf \"crop=iw/2.4:ih:x=iw/2-iw/4.8,scale=1080:1920:force_original_aspect_ratio=increase,crop=1080:1920,eq=saturation={saturationStr}:brightness={brightnessStr}\" -c:v libx264 -crf 23 -preset faster -c:a copy -y \"{tempVideoPath}\"",
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

                verticalProcess.Start();
                verticalProcess.BeginErrorReadLine();

                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMinutes(5)));

                if (completedTask != tcs.Task)
                {
                    try { if (!verticalProcess.HasExited) verticalProcess.Kill(); }
                    catch { }
                    throw new TimeoutException("Przekroczono limit czasu podczas konwersji do formatu pionowego.");
                }

                if (verticalProcess.ExitCode != 0 || !File.Exists(tempVideoPath))
                {
                    throw new Exception($"Błąd podczas konwersji do formatu pionowego. Kod wyjścia: {verticalProcess.ExitCode}\nSzczegóły:\n{errorBuilder}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas konwersji do formatu pionowego: {ex.Message}");
            }
        }

        private async Task GenerateAudio(string ffmpegPath, string tempVideoPath, string audioVideoPath, string ttsAudioPath)
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
                        // WAŻNA ZMIANA: Teraz miksujemy obie ścieżki audio razem
                        // -filter_complex "[0:a][1:a]amix=inputs=2:duration=first:dropout_transition=2[a]" 
                        // -map 0:v -map "[a]"
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
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas dodawania ścieżki dźwiękowej: {ex.Message}");
            }
        }

        private async Task GenerateSubtitles(string tekst, string ffmpegPath, string audioVideoPath, string finalVideoPath)
        {
            using var cancellationToken = new CancellationTokenSource();
            try
            {
                ProgressStatus = "Dodawanie napisów...";

                if (!File.Exists(audioVideoPath))
                    throw new FileNotFoundException("Nie znaleziono pliku wideo do dodania napisów.");

                // Tworzenie pliku SRT
                string srtFilePath = Path.Combine(Path.GetDirectoryName(finalVideoPath),
                                               Path.GetFileNameWithoutExtension(finalVideoPath) + ".srt");

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

                // Generowanie pliku SRT
                CreateSubtitles(srtFilePath, tekst, TimeSpan.FromSeconds(audioDurationSeconds));
                Debug.WriteLine($"Wygenerowano plik SRT: {srtFilePath}");

                // Kluczowa zmiana: używamy dokładnie takiej samej składni jak w działającej komendzie
                // Zamieniamy backslashe na forward slashe z zachowaniem jednego backslasha przed dwukropkiem
                string modifiedPath = srtFilePath
                    .Replace("\\", "/")          // Zamień backslashe na forward slashe
                    .Replace(":", "\\:");        // Escapuj dwukropek w nazwie dysku

                // DOKŁADNA składnia jak w działającej komendzie
                var captionProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(ffmpegPath, "ffmpeg.exe"),
                        Arguments = $"-i \"{audioVideoPath}\" -vf \"subtitles='{modifiedPath}':force_style='FontSize=24,Alignment=2,MarginV=70'\" -c:a copy -y \"{finalVideoPath}\"",
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
                        $"SRT CONTENT:\n{File.ReadAllText(srtFilePath)}"
                    );

                    throw new Exception($"Błąd podczas dodawania napisów. Kod wyjścia: {captionProcess.ExitCode}\n" +
                                      $"Szczegóły zapisano w pliku: {logPath}");
                }

                // Usuwanie plików tymczasowych
                try
                {
                    // Możemy zostawić pliki napisów do inspekcji przy debugowaniu
                    // Odkomentuj poniższe linie, gdy wszystko działa poprawnie
                    // if (File.Exists(srtFilePath)) File.Delete(srtFilePath);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Błąd przy usuwaniu plików tymczasowych: {ex.Message}");
                }

                Debug.WriteLine("Napisy dodane pomyślnie!");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WYJĄTEK w GenerateSubtitles: {ex}");
                throw new Exception($"Błąd podczas dodawania napisów: {ex.Message}", ex);
            }
        }

        private void CreateSubtitles(string filePath, string tekst, TimeSpan duration)
        {
            try
            {
                const int CHARS_PER_MINUTE = 150;
                double charsPerSecond = CHARS_PER_MINUTE / 60.0;

                // Dzielimy tekst na słowa i usuwamy puste
                var words = tekst.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                // Obliczanie czasu dla każdego segmentu
                var srtBuilder = new StringBuilder();
                double totalDuration = duration.TotalSeconds;

                // Obliczamy sumę długości wszystkich słów, żeby ustalić proporcje
                int totalCharacters = words.Sum(w => w.Length);

                // Minimalna długość słowa to 2 znaki dla celów kalkulacji
                totalCharacters = Math.Max(totalCharacters, words.Length * 2);

                double currentStartTime = 0;

                // W tym przypadku każde słowo to osobny segment
                for (int i = 0; i < words.Length; i++)
                {
                    string word = words[i];

                    // Ustalamy efektywną długość słowa (minimum 2 znaki)
                    int effectiveLength = Math.Max(word.Length, 2);

                    // Obliczenie czasu wyświetlania proporcjonalnie do długości słowa 
                    // w stosunku do całkowitej długości tekstu
                    double wordDuration = totalDuration * effectiveLength / totalCharacters;

                    // Dodatkowy czas dla dłuższych słów
                    if (word.Length > 8)
                        wordDuration *= 1.2; // 20% więcej czasu dla bardzo długich słów
                    else if (word.Length > 5)
                        wordDuration *= 1.1; // 10% więcej czasu dla dłuższych słów

                    // Minimalny czas wyświetlania słowa to 0.3 sekundy dla krótkich słów, 
                    // 0.5 sekundy dla średnich i 0.8 dla długich
                    if (word.Length <= 3)
                        wordDuration = Math.Max(wordDuration, 0.3);
                    else if (word.Length <= 6)
                        wordDuration = Math.Max(wordDuration, 0.5);
                    else
                        wordDuration = Math.Max(wordDuration, 0.8);

                    double endTime = currentStartTime + wordDuration;

                    string startTimeStr = TimeSpan.FromSeconds(currentStartTime).ToString(@"hh\:mm\:ss\,fff");
                    string endTimeStr = TimeSpan.FromSeconds(endTime).ToString(@"hh\:mm\:ss\,fff");

                    srtBuilder.AppendLine((i + 1).ToString());
                    srtBuilder.AppendLine($"{startTimeStr} --> {endTimeStr}");
                    srtBuilder.AppendLine(word);
                    srtBuilder.AppendLine();

                    // Następne słowo zaczyna się tuż po zakończeniu bieżącego
                    currentStartTime = endTime;
                }

                // Sprawdzamy, czy łączny czas napisów nie przekracza czasu trwania audio
                if (currentStartTime > totalDuration)
                {
                    // Jeśli przekracza, to skalujemy wszystkie napisy proporcjonalnie
                    double scaleFactor = totalDuration / currentStartTime;

                    // Generujemy napisy od nowa z przeskalowanym czasem
                    srtBuilder.Clear();
                    currentStartTime = 0;

                    for (int i = 0; i < words.Length; i++)
                    {
                        string word = words[i];
                        int effectiveLength = Math.Max(word.Length, 2);
                        double wordDuration = totalDuration * effectiveLength / totalCharacters * scaleFactor;

                        if (word.Length > 8)
                            wordDuration *= 1.2;
                        else if (word.Length > 5)
                            wordDuration *= 1.1;

                        if (word.Length <= 3)
                            wordDuration = Math.Max(wordDuration, 0.3 * scaleFactor);
                        else if (word.Length <= 6)
                            wordDuration = Math.Max(wordDuration, 0.5 * scaleFactor);
                        else
                            wordDuration = Math.Max(wordDuration, 0.8 * scaleFactor);

                        double endTime = currentStartTime + wordDuration;

                        string startTimeStr = TimeSpan.FromSeconds(currentStartTime).ToString(@"hh\:mm\:ss\,fff");
                        string endTimeStr = TimeSpan.FromSeconds(endTime).ToString(@"hh\:mm\:ss\,fff");

                        srtBuilder.AppendLine((i + 1).ToString());
                        srtBuilder.AppendLine($"{startTimeStr} --> {endTimeStr}");
                        srtBuilder.AppendLine(word);
                        srtBuilder.AppendLine();

                        currentStartTime = endTime;
                    }
                }

                File.WriteAllText(filePath, srtBuilder.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd w CreateSubtitles: {ex.Message}");
                throw;
            }
        }


        private async Task GenerateTTS(string tekst, string sciezkaPliku)
        {
            try
            {
                string url = $"https://texttospeech.googleapis.com/v1/text:synthesize?key={apiKey}";

                // Escape special characters for JSON
                tekst = tekst.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

                string json = $@"{{
        ""input"": {{ ""text"": ""{tekst}"" }},
        ""voice"": 
        {{ ""languageCode"": ""pl-PL"", ""ssmlGender"": ""MALE"" }},
        ""audioConfig"": 
        {{ ""audioEncoding"": ""MP3"" }}
        }}";

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
                        Console.WriteLine($"Plik audio zapisany: {sciezkaPliku}");

                        // Zwiększ licznik wykorzystanych znaków
                        int liczbaZnaków = tekst.Length;
                        Properties.Settings.Default.ApiUsageCharacters += liczbaZnaków;
                        Properties.Settings.Default.Save();
                        NotifyOfPropertyChange(nameof(ApiUsageCharacters));
                    }
                    else
                    {
                        Console.WriteLine($"Błąd: {response.StatusCode} - {responseJson}");
                        throw new Exception($"Błąd API Google TTS: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd w GenerateTTS: {ex.Message}");
                throw;
            }
        }
    }
}