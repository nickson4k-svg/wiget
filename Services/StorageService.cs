using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CalWidget.Models;

namespace CalWidget.Services
{
    public class StorageService
    {
        private readonly string _folderPath;
        private readonly string _settingsFile;
        private readonly string _historyFile;
        private FoodDatabaseService? _foodDatabaseService;

        // In-memory кеш для усунення зайвого дискового I/O та парсингу
        private AppSettings? _cachedSettings;
        private Dictionary<string, DailySummary>? _cachedHistory;

        public StorageService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _folderPath = Path.Combine(appData, "CalWidget");
            _settingsFile = Path.Combine(_folderPath, "settings.json");
            _historyFile = Path.Combine(_folderPath, "history.json");
        }

        private FoodDatabaseService FoodDb => _foodDatabaseService ??= new FoodDatabaseService();

        #region Settings

        public AppSettings LoadSettings()
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            try
            {
                if (File.Exists(_settingsFile))
                {
                    string json = File.ReadAllText(_settingsFile);
                    var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.AppSettings);
                    if (settings != null)
                    {
                        _cachedSettings = settings;
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка читання settings.json: {ex.Message}");
            }

            var defaultSettings = new AppSettings();
            defaultSettings.Goals = CalorieService.CalculateMacroGoals(defaultSettings.Profile);
            _cachedSettings = defaultSettings;
            SaveSettings(defaultSettings);
            return defaultSettings;
        }

        public void SaveSettings(AppSettings settings)
        {
            _cachedSettings = settings;
            try
            {
                string json = JsonSerializer.Serialize(settings, AppJsonContext.Default.AppSettings);
                WriteFileAtomic(_settingsFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка збереження settings.json: {ex.Message}");
            }
        }

        #endregion

        #region History

        public static string GetTodayKey() => DateTime.Today.ToString("yyyy-MM-dd");

        public Dictionary<string, DailySummary> LoadHistory()
        {
            if (_cachedHistory != null)
                return _cachedHistory;

            try
            {
                if (File.Exists(_historyFile))
                {
                    string json = File.ReadAllText(_historyFile);
                    var dict = JsonSerializer.Deserialize(json, AppJsonContext.Default.DictionaryStringDailySummary);
                    if (dict != null)
                    {
                        _cachedHistory = dict;
                        return dict;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка читання history.json: {ex.Message}");
            }

            _cachedHistory = new Dictionary<string, DailySummary>();
            return _cachedHistory;
        }

        public DailySummary GetTodaySummary()
        {
            var history = LoadHistory();
            string todayKey = GetTodayKey();

            if (!history.TryGetValue(todayKey, out var summary))
            {
                summary = new DailySummary { Date = todayKey };
                history[todayKey] = summary;
                SaveHistory(history);
            }

            return summary;
        }

        public void SaveTodaySummary(DailySummary todaySummary)
        {
            var history = LoadHistory();
            history[todaySummary.Date] = todaySummary;
            SaveHistory(history);
        }

        public void SaveHistory(Dictionary<string, DailySummary> history)
        {
            _cachedHistory = history;
            try
            {
                string json = JsonSerializer.Serialize(history, AppJsonContext.Default.DictionaryStringDailySummary);
                WriteFileAtomic(_historyFile, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка збереження history.json: {ex.Message}");
            }
        }

        #endregion

        #region Products Catalog Delegation (Lazy)

        public List<FoodItem> LoadProducts() => FoodDb.LoadProducts();

        public void SaveProducts(List<FoodItem> products) => FoodDb.SaveProducts(products);

        public void AddCustomProduct(FoodItem item) => FoodDb.AddProduct(item);

        #endregion

        private void WriteFileAtomic(string filePath, string content)
        {
            try
            {
                if (!Directory.Exists(_folderPath))
                {
                    Directory.CreateDirectory(_folderPath);
                }

                string tempFile = filePath + ".tmp";
                File.WriteAllText(tempFile, content);

                if (File.Exists(filePath))
                {
                    File.Replace(tempFile, filePath, null);
                }
                else
                {
                    File.Move(tempFile, filePath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка запису файлу {filePath}: {ex.Message}");
            }
        }
    }
}
