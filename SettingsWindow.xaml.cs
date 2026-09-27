using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CalWidget.Models;
using CalWidget.Services;

namespace CalWidget
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;
        private readonly StorageService _storageService;
        private readonly Action _onSettingsUpdated;
        private bool _isInitializing = true;

        public SettingsWindow(AppSettings settings, StorageService storageService, Action onSettingsUpdated)
        {
            InitializeComponent();
            _settings = settings;
            _storageService = storageService;
            _onSettingsUpdated = onSettingsUpdated;

            LoadDataToUi();
            _isInitializing = false;
            Recalculate();
            RefreshItemsList();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void LoadDataToUi()
        {
            // Тема оформлення
            if (_settings.CurrentTheme == "Light")
                ThemeLightRadio.IsChecked = true;
            else
                ThemeDarkRadio.IsChecked = true;

            var p = _settings.Profile;

            if (p.Gender == "Female")
                FemaleRadio.IsChecked = true;
            else
                MaleRadio.IsChecked = true;

            WeightBox.Text = p.WeightKg.ToString("F1", CultureInfo.InvariantCulture);
            HeightBox.Text = p.HeightCm.ToString("F0", CultureInfo.InvariantCulture);
            AgeBox.Text = p.Age.ToString();

            // Рівень активності
            foreach (ComboBoxItem item in ActivityCombo.Items)
            {
                if (double.TryParse(item.Tag?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                {
                    if (Math.Abs(val - p.ActivityLevel) < 0.01)
                    {
                        ActivityCombo.SelectedItem = item;
                        break;
                    }
                }
            }

            // Ціль
            foreach (ComboBoxItem item in GoalCombo.Items)
            {
                if (item.Tag?.ToString() == p.Goal)
                {
                    GoalCombo.SelectedItem = item;
                    break;
                }
            }

            TargetCalBox.Text = _settings.Goals.Calories.ToString();
            TargetCarbsBox.Text = _settings.Goals.CarbsGrams.ToString();
            TargetProteinBox.Text = _settings.Goals.ProteinGrams.ToString();
            TargetFatBox.Text = _settings.Goals.FatGrams.ToString();

            // Ефект скла
            EnableGlassCheck.IsChecked = _settings.EnableFlutedGlass;
            GlassOpacitySlider.Value = Math.Clamp(_settings.GlassOpacity, 0.10, 0.80);
            RibDistortionSlider.Value = Math.Clamp(_settings.RibDistortion, 0.0, 1.5);
            RibWidthSlider.Value = Math.Clamp(_settings.RibWidth, 8, 32);
            BlurRadiusSlider.Value = Math.Clamp(_settings.BlurRadius, 0, 15);
            UpdateGlassLabels();

            // Розклад прийомів їжі
            FirstMealTimeBox.Text = string.IsNullOrWhiteSpace(_settings.FirstMealTime) ? "09:00" : _settings.FirstMealTime;
            MealCountCombo.SelectedIndex = _settings.MealCount switch { 3 => 0, 5 => 2, _ => 1 };
            MealIntervalBox.Text = _settings.MealIntervalHours.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private void GlassControl_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            UpdateGlassLabels();

            _settings.EnableFlutedGlass = EnableGlassCheck.IsChecked == true;
            _settings.GlassOpacity = GlassOpacitySlider.Value;
            _settings.RibDistortion = RibDistortionSlider.Value;
            _settings.RibWidth = (int)Math.Round(RibWidthSlider.Value);
            _settings.BlurRadius = (int)Math.Round(BlurRadiusSlider.Value);

            _storageService.SaveSettings(_settings);
            _onSettingsUpdated?.Invoke();
        }

        private void UpdateGlassLabels()
        {
            if (GlassOpacityValText != null) GlassOpacityValText.Text = $"{(int)Math.Round(GlassOpacitySlider.Value * 100)}%";
            if (RibDistortionValText != null) RibDistortionValText.Text = $"{RibDistortionSlider.Value:0.00}";
            if (RibWidthValText != null) RibWidthValText.Text = $"{(int)Math.Round(RibWidthSlider.Value)} px";
            if (BlurRadiusValText != null) BlurRadiusValText.Text = $"{(int)Math.Round(BlurRadiusSlider.Value)} px";
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing) return;

            string selectedTheme = ThemeLightRadio.IsChecked == true ? "Light" : "Dark";
            ThemeManager.ApplyTheme(selectedTheme);
            _settings.CurrentTheme = selectedTheme;
            _storageService.SaveSettings(_settings);
            _onSettingsUpdated?.Invoke();
        }

        private void Recalculate_Event(object sender, RoutedEventArgs e)
        {
            if (!_isInitializing)
            {
                Recalculate();
            }
        }

        private void Recalculate()
        {
            var tempProfile = ReadProfileFromUi();

            double bmr = CalorieService.CalculateBmr(tempProfile);
            double tdee = CalorieService.CalculateTdee(tempProfile);
            var macroGoals = CalorieService.CalculateMacroGoals(tempProfile);

            if (BmrResultText != null)
                BmrResultText.Text = $"{bmr:N0} ккал";

            if (TdeeResultText != null)
                TdeeResultText.Text = $"{tdee:N0} ккал";

            if (TargetResultText != null)
                TargetResultText.Text = $"{macroGoals.Calories:N0}";

            // Автоматично підставляємо розраховані значення, якщо користувач змінює стать/вагу/ціль
            if (!_settings.Goals.IsCustom && TargetCalBox != null && TargetCarbsBox != null && TargetProteinBox != null && TargetFatBox != null)
            {
                TargetCalBox.Text = macroGoals.Calories.ToString();
                TargetCarbsBox.Text = macroGoals.CarbsGrams.ToString();
                TargetProteinBox.Text = macroGoals.ProteinGrams.ToString();
                TargetFatBox.Text = macroGoals.FatGrams.ToString();
            }
        }

        private UserProfile ReadProfileFromUi()
        {
            var p = new UserProfile();
            p.Gender = FemaleRadio.IsChecked == true ? "Female" : "Male";

            if (double.TryParse(WeightBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double w) && w > 20)
                p.WeightKg = w;

            if (double.TryParse(HeightBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double h) && h > 50)
                p.HeightCm = h;

            if (int.TryParse(AgeBox.Text, out int age) && age > 5)
                p.Age = age;

            if (ActivityCombo.SelectedItem is ComboBoxItem actItem &&
                double.TryParse(actItem.Tag?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double act))
            {
                p.ActivityLevel = act;
            }

            if (GoalCombo.SelectedItem is ComboBoxItem goalItem && goalItem.Tag != null)
            {
                p.Goal = goalItem.Tag.ToString()!;
            }

            return p;
        }

        private void RefreshItemsList()
        {
            var today = _storageService.GetTodaySummary();
            TodayItemsList.ItemsSource = null;
            TodayItemsList.ItemsSource = today.Entries.OrderByDescending(e => e.Timestamp).ToList();
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string entryId)
            {
                var today = _storageService.GetTodaySummary();
                var entry = today.Entries.FirstOrDefault(x => x.Id == entryId);
                if (entry != null)
                {
                    today.Entries.Remove(entry);
                    _storageService.SaveTodaySummary(today);
                    RefreshItemsList();
                    _onSettingsUpdated?.Invoke();
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.Profile = ReadProfileFromUi();

            int.TryParse(TargetCalBox.Text.Replace(" ", "").Replace(",", ""), out int targetCal);
            int.TryParse(TargetCarbsBox.Text.Replace(" ", "").Replace(",", ""), out int targetCarbs);
            int.TryParse(TargetProteinBox.Text.Replace(" ", "").Replace(",", ""), out int targetProt);
            int.TryParse(TargetFatBox.Text.Replace(" ", "").Replace(",", ""), out int targetFat);

            if (targetCal <= 0) targetCal = CalorieService.CalculateRecommendedTarget(_settings.Profile);
            if (targetProt <= 0) targetProt = (int)Math.Round((targetCal * 0.3) / 4.0);
            if (targetFat <= 0) targetFat = (int)Math.Round((targetCal * 0.3) / 9.0);
            if (targetCarbs <= 0) targetCarbs = (int)Math.Round((targetCal * 0.4) / 4.0);

            _settings.Goals.Calories = targetCal;
            _settings.Goals.CarbsGrams = targetCarbs;
            _settings.Goals.ProteinGrams = targetProt;
            _settings.Goals.FatGrams = targetFat;
            _settings.Goals.IsCustom = true;

            // Збереження розкладу прийомів їжі
            if (!string.IsNullOrWhiteSpace(FirstMealTimeBox.Text))
            {
                _settings.FirstMealTime = FirstMealTimeBox.Text.Trim();
            }
            if (MealCountCombo.SelectedItem is ComboBoxItem mcItem && int.TryParse(mcItem.Tag?.ToString(), out int mc))
            {
                _settings.MealCount = mc;
            }
            if (double.TryParse(MealIntervalBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double interval) && interval >= 1 && interval <= 8)
            {
                _settings.MealIntervalHours = interval;
            }

            _settings.CurrentTheme = ThemeLightRadio.IsChecked == true ? "Light" : "Dark";

            _storageService.SaveSettings(_settings);
            _onSettingsUpdated?.Invoke();
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
