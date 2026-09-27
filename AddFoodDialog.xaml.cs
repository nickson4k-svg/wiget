using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CalWidget.Models;
using CalWidget.Services;

namespace CalWidget
{
    public partial class AddFoodDialog : Window
    {
        private readonly StorageService _storageService;
        private readonly Action _onItemAdded;
        private List<FoodItem> _allProducts = new();
        private FoodItem? _selectedProduct;
        private bool _isCustomMode = false;
        private bool _isLoaded = false;

        public AddFoodDialog(StorageService storageService, Action onItemAdded)
        {
            InitializeComponent();
            _storageService = storageService;
            _onItemAdded = onItemAdded;

            _isLoaded = true;
            LoadProductsList();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void LoadProductsList()
        {
            if (!_isLoaded || FoodListBox == null) return;

            _allProducts = _storageService.LoadProducts();
            ApplyFilter();

            if (FoodListBox.Items.Count > 0)
            {
                FoodListBox.SelectedIndex = 0;
            }
        }

        private void ApplyFilter()
        {
            if (!_isLoaded || _isCustomMode || FoodListBox == null) return;

            string query = SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";

            var filtered = _allProducts.Where(p =>
            {
                // Фільтр категорій
                bool matchesCat = true;
                string cat = p.Category ?? "";

                if (CatMeatRadio?.IsChecked == true)
                {
                    matchesCat = cat.Contains("М'яс", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Яйц", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("птиц", StringComparison.OrdinalIgnoreCase);
                }
                else if (CatDairyRadio?.IsChecked == true)
                {
                    matchesCat = cat.Contains("Молоч", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Сир", StringComparison.OrdinalIgnoreCase);
                }
                else if (CatDrinksRadio?.IsChecked == true)
                {
                    matchesCat = p.IsLiquid || cat.Contains("Напо", StringComparison.OrdinalIgnoreCase);
                }
                else if (CatGrainsRadio?.IsChecked == true)
                {
                    matchesCat = cat.Contains("Гарнір", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Круп", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Снек", StringComparison.OrdinalIgnoreCase);
                }

                // Текстовий пошук
                bool matchesSearch = string.IsNullOrEmpty(query) ||
                                     p.Name.ToLowerInvariant().Contains(query);

                return matchesCat && matchesSearch;
            }).ToList();

            FoodListBox.ItemsSource = filtered;

            if (filtered.Count > 0)
            {
                FoodListBox.SelectedIndex = 0;
            }
            else
            {
                _selectedProduct = null;
                if (SelectedFoodTitle != null)
                    SelectedFoodTitle.Text = "Нічого не знайдено за запитом";
                if (LiveSummaryText != null)
                    LiveSummaryText.Text = "Продукт не обрано";
                if (PreviewCalories != null)
                    PreviewCalories.Text = "0 ккал";
            }
        }

        private void CategoryRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || CatCustomRadio == null || CatalogListContainer == null || CustomFormContainer == null || PortionSection == null) return;

            if (CatCustomRadio.IsChecked == true)
            {
                _isCustomMode = true;
                CatalogListContainer.Visibility = Visibility.Collapsed;
                CustomFormContainer.Visibility = Visibility.Visible;
                PortionSection.Visibility = Visibility.Collapsed;
                if (SelectedFoodTitle != null)
                    SelectedFoodTitle.Text = "Введіть параметри власної страви";
            }
            else
            {
                _isCustomMode = false;
                CatalogListContainer.Visibility = Visibility.Visible;
                CustomFormContainer.Visibility = Visibility.Collapsed;
                PortionSection.Visibility = Visibility.Visible;
                ApplyFilter();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded || SearchPlaceholder == null || SearchBox == null) return;

            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            ApplyFilter();
        }

        private void FoodListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || FoodListBox == null || DrinkPresetsPanel == null || FoodPresetsPanel == null || PortionUnitText == null || PortionLabel == null || PortionAmountBox == null) return;

            if (FoodListBox.SelectedItem is FoodItem item)
            {
                _selectedProduct = item;
                if (SelectedFoodTitle != null)
                    SelectedFoodTitle.Text = $"Обрано: {item.Name} ({item.Category})";

                if (item.IsLiquid)
                {
                    DrinkPresetsPanel.Visibility = Visibility.Visible;
                    FoodPresetsPanel.Visibility = Visibility.Collapsed;
                    PortionUnitText.Text = "мл";
                    PortionLabel.Text = "Об'єм:";
                    PortionAmountBox.Text = item.DefaultPortion > 0 ? item.DefaultPortion.ToString("0") : "250";
                }
                else
                {
                    DrinkPresetsPanel.Visibility = Visibility.Collapsed;
                    FoodPresetsPanel.Visibility = Visibility.Visible;
                    PortionUnitText.Text = "г";
                    PortionLabel.Text = "Порція:";
                    PortionAmountBox.Text = item.DefaultPortion > 0 ? item.DefaultPortion.ToString("0") : "150";
                }

                RecalculatePortion();
            }
        }

        private void PortionAmountBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            RecalculatePortion();
        }

        private void SetPortion_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || PortionAmountBox == null) return;
            if (sender is Button btn && btn.Tag is string tagStr)
            {
                PortionAmountBox.Text = tagStr;
                PortionAmountBox.CaretIndex = PortionAmountBox.Text.Length;
            }
        }

        private void RecalculatePortion()
        {
            if (!_isLoaded || _selectedProduct == null || LiveSummaryText == null || PreviewCalories == null || PortionAmountBox == null) return;

            string rawText = PortionAmountBox.Text.Replace(',', '.');
            if (double.TryParse(rawText, NumberStyles.Any, CultureInfo.InvariantCulture, out double amount) && amount > 0)
            {
                double ratio = amount / 100.0;
                double cal = _selectedProduct.CaloriesPer100 * ratio;
                double prot = _selectedProduct.ProteinPer100 * ratio;
                double fat = _selectedProduct.FatPer100 * ratio;
                double carbs = _selectedProduct.CarbsPer100 * ratio;

                LiveSummaryText.Text = $"У цій порції: {cal:0} ккал | Б: {prot:0.#}г | Ж: {fat:0.#}г | В: {carbs:0.#}г";
                PreviewCalories.Text = $"{cal:0} ккал";
            }
            else
            {
                LiveSummaryText.Text = "Введіть розмір порції";
                PreviewCalories.Text = "0 ккал";
            }
        }

        private void AddFoodButton_Click(object sender, RoutedEventArgs e)
        {
            var today = _storageService.GetTodaySummary();

            if (_isCustomMode)
            {
                string name = string.IsNullOrWhiteSpace(CustomNameBox.Text) ? "Власна страва" : CustomNameBox.Text.Trim();
                double.TryParse(CustomCalBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double cal);
                double.TryParse(CustomPortionBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double portion);
                double.TryParse(CustomCarbsBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double carbs);
                double.TryParse(CustomProteinBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double prot);
                double.TryParse(CustomFatBox.Text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double fat);

                if (portion <= 0) portion = 100;

                var entry = new FoodLogEntry(name, portion, "г", cal, prot, fat, carbs);
                today.Entries.Add(entry);
                _storageService.SaveTodaySummary(today);

                if (SaveToCatalogCheck?.IsChecked == true)
                {
                    double ratio = 100.0 / portion;
                    var newItem = new FoodItem(
                        name,
                        "Власні страви",
                        false,
                        Math.Round(cal * ratio, 1),
                        Math.Round(prot * ratio, 1),
                        Math.Round(fat * ratio, 1),
                        Math.Round(carbs * ratio, 1),
                        portion,
                        "г"
                    );
                    _storageService.AddCustomProduct(newItem);
                }

                _onItemAdded?.Invoke();
                Close();
                return;
            }

            if (_selectedProduct != null)
            {
                string rawText = PortionAmountBox.Text.Replace(',', '.');
                if (!double.TryParse(rawText, NumberStyles.Any, CultureInfo.InvariantCulture, out double amount) || amount <= 0)
                {
                    amount = _selectedProduct.DefaultPortion;
                }

                double ratio = amount / 100.0;
                double cal = Math.Round(_selectedProduct.CaloriesPer100 * ratio, 1);
                double prot = Math.Round(_selectedProduct.ProteinPer100 * ratio, 1);
                double fat = Math.Round(_selectedProduct.FatPer100 * ratio, 1);
                double carbs = Math.Round(_selectedProduct.CarbsPer100 * ratio, 1);

                string unit = _selectedProduct.IsLiquid ? "мл" : "г";
                var entry = new FoodLogEntry(_selectedProduct.Name, amount, unit, cal, prot, fat, carbs);
                today.Entries.Add(entry);
                _storageService.SaveTodaySummary(today);

                _onItemAdded?.Invoke();
                Close();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
