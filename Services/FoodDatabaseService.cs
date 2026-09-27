using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CalWidget.Models;

namespace CalWidget.Services
{
    public class FoodDatabaseService
    {
        private readonly string _folderPath;
        private readonly string _productsFile;
        private List<FoodItem>? _cachedProducts;

        public FoodDatabaseService()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _folderPath = Path.Combine(appData, "CalWidget");
            _productsFile = Path.Combine(_folderPath, "products.json");
        }

        public List<FoodItem> LoadProducts()
        {
            if (_cachedProducts != null)
                return _cachedProducts;

            try
            {
                if (File.Exists(_productsFile))
                {
                    string json = File.ReadAllText(_productsFile);
                    var list = JsonSerializer.Deserialize(json, AppJsonContext.Default.ListFoodItem);
                    if (list != null && list.Count > 0)
                    {
                        // Додаємо нові продукти зі seed data, якщо їх ще немає в базі
                        var seed = GetSeedProducts();
                        bool updated = false;
                        foreach (var s in seed)
                        {
                            if (!list.Exists(x => x.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase)))
                            {
                                list.Add(s);
                                updated = true;
                            }
                        }
                        if (updated)
                        {
                            SaveProducts(list);
                        }
                        _cachedProducts = list;
                        return list;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка читання products.json: {ex.Message}");
            }

            // Якщо файл відсутній або порожній - автоматично генеруємо стартовий список
            var seedData = GetSeedProducts();
            SaveProducts(seedData);
            _cachedProducts = seedData;
            return seedData;
        }

        public void ResetToSeed()
        {
            _cachedProducts = null;
            var seedData = GetSeedProducts();
            SaveProducts(seedData);
            _cachedProducts = seedData;
        }

        public void SaveProducts(List<FoodItem> products)
        {
            _cachedProducts = products;
            try
            {
                if (!Directory.Exists(_folderPath))
                {
                    Directory.CreateDirectory(_folderPath);
                }

                string json = JsonSerializer.Serialize(products, AppJsonContext.Default.ListFoodItem);
                string tempFile = _productsFile + ".tmp";
                File.WriteAllText(tempFile, json);

                if (File.Exists(_productsFile))
                {
                    File.Replace(tempFile, _productsFile, null);
                }
                else
                {
                    File.Move(tempFile, _productsFile);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Помилка збереження products.json: {ex.Message}");
            }
        }

        public void AddProduct(FoodItem item)
        {
            var list = LoadProducts();
            list.Add(item);
            SaveProducts(list);
        }

        public static List<FoodItem> GetSeedProducts()
        {
            return new List<FoodItem>
            {
                // 1. Молочні продукти
                new FoodItem("Домашній сир (творог)", "Молочні продукти", false, 160, 16.0, 9.0, 2.5, 150, "г"),
                new FoodItem("Сметана 15%", "Молочні продукти", false, 160, 2.8, 15.0, 3.6, 50, "г"),
                new FoodItem("Молоко 2.5%", "Молочні продукти", true, 52, 2.8, 2.5, 4.7, 250, "мл"),
                new FoodItem("Кефір 1%", "Молочні продукти", true, 40, 3.0, 1.0, 4.0, 250, "мл"),
                new FoodItem("Сир твердий", "Молочні продукти", false, 350, 26.0, 27.0, 0.0, 50, "г"),

                // 2. Хліб, випічка та соуси
                new FoodItem("Білий хліб пшеничний", "Хліб та соуси", false, 265, 8.5, 3.2, 49.0, 50, "г"),
                new FoodItem("Булочки (бургерні / здобні)", "Хліб та соуси", false, 295, 8.0, 4.5, 54.0, 80, "г"),
                new FoodItem("Кетчуп томатний", "Хліб та соуси", false, 105, 1.5, 0.1, 25.0, 30, "г"),

                // 3. Овочі, фрукти та гарніри
                new FoodItem("Помідори свіжі", "Гарніри та овочі", false, 18, 0.9, 0.2, 3.9, 150, "г"),
                new FoodItem("Банан свіжий", "Гарніри та овочі", false, 89, 1.1, 0.3, 22.8, 120, "г"),
                new FoodItem("Гречка варена", "Гарніри та овочі", false, 110, 4.2, 1.1, 21.3, 150, "г"),
                new FoodItem("Рис варений", "Гарніри та овочі", false, 130, 2.7, 0.3, 28.2, 150, "г"),
                new FoodItem("Вівсянка на воді", "Гарніри та овочі", false, 88, 3.0, 1.7, 15.0, 200, "г"),

                // 4. М'ясо та птиця
                new FoodItem("Куряче філе", "М'ясо та птиця", false, 110, 23.0, 1.2, 0.0, 150, "г"),
                new FoodItem("Котлети курячі", "М'ясо та птиця", false, 185, 18.0, 9.0, 8.0, 120, "г"),
                new FoodItem("Яловичина відварна", "М'ясо та птиця", false, 190, 26.0, 9.5, 0.0, 150, "г"),
                new FoodItem("Яйце куряче", "М'ясо та птиця", false, 143, 12.6, 9.5, 0.7, 100, "г"),

                // 5. Напої
                new FoodItem("Coca-Cola / Pepsi звичайна", "Напої", true, 42, 0.0, 0.0, 10.6, 330, "мл"),
                new FoodItem("Coca-Cola Zero / Pepsi Max", "Напої", true, 0.3, 0.0, 0.0, 0.0, 330, "мл"),
                new FoodItem("Сік апельсиновий", "Напої", true, 45, 0.7, 0.2, 10.2, 250, "мл"),
                new FoodItem("Вода питна", "Напої", true, 0, 0.0, 0.0, 0.0, 500, "мл")
            };
        }
    }
}
