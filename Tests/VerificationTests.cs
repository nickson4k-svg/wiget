using System;
using System.IO;
using CalWidget.Models;
using CalWidget.Services;

namespace CalWidget.Tests
{
    public static class VerificationTests
    {
        public static void RunAll()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine("  CalWidget - Повна перевірка алгоритмів та даних ");
            Console.WriteLine("==================================================");

            // 1. Тест BMR (Міффліна-Сан Жеора) для чоловіків
            var profileMale = new UserProfile
            {
                Gender = "Male",
                WeightKg = 75,
                HeightCm = 178,
                Age = 28,
                ActivityLevel = 1.375,
                Goal = "Deficit15"
            };

            double bmr = CalorieService.CalculateBmr(profileMale);
            if (Math.Abs(bmr - 1727.5) > 0.01)
                throw new Exception($"Помилка BMR: очікувалось 1727.5, отримано {bmr}");
            Console.WriteLine($"[PASS] BMR (Чоловік): {bmr:F1} ккал");

            double tdee = CalorieService.CalculateTdee(profileMale);
            if (Math.Abs(tdee - 2375.3125) > 0.01)
                throw new Exception($"Помилка TDEE: очікувалось 2375.3, отримано {tdee}");
            Console.WriteLine($"[PASS] TDEE: {tdee:F1} ккал");

            int target = CalorieService.CalculateRecommendedTarget(profileMale);
            if (target != 2019)
                throw new Exception($"Помилка цілі калорій: очікувалось 2019, отримано {target}");
            Console.WriteLine($"[PASS] Норма калорій (Дефіцит -15%): {target} ккал");

            // 2. Тест розподілу БЖВ (Схуднення: Б 30%, Ж 30%, В 40%)
            var macroGoals = CalorieService.CalculateMacroGoals(profileMale);
            if (macroGoals.ProteinGrams <= 0 || macroGoals.FatGrams <= 0 || macroGoals.CarbsGrams <= 0)
                throw new Exception("Невірний розрахунок макронутрієнтів!");
            Console.WriteLine($"[PASS] Розподіл БЖВ: Вуглеводи={macroGoals.CarbsGrams}г, Білки={macroGoals.ProteinGrams}г, Жири={macroGoals.FatGrams}г");

            // 3. Тест FoodDatabaseService та автоініціалізації бази продуктів
            var foodDb = new FoodDatabaseService();
            foodDb.ResetToSeed();
            var products = foodDb.LoadProducts();
            if (products.Count < 10)
                throw new Exception("Каталог продуктів не ініціалізувався або містить замало елементів!");
            Console.WriteLine($"[PASS] FoodDatabaseService успішно ініціалізував базу: {products.Count} позицій");

            // Перевірка курячого філе та коли
            var fillet = products.Find(p => p.Name.Contains("Куряче філе"));
            if (fillet == null || fillet.CaloriesPer100 != 110 || fillet.ProteinPer100 != 23)
                throw new Exception("Помилка даних курячого філе у базі!");
            Console.WriteLine($"[PASS] Перевірено куряче філе: {fillet.CaloriesPer100} ккал, Б: {fillet.ProteinPer100}г, Ж: {fillet.FatPer100}г");

            var cola = products.Find(p => p.Name.Contains("Coca-Cola"));
            if (cola == null || !cola.IsLiquid || cola.Unit != "мл")
                throw new Exception("Помилка даних напою Coca-Cola у базі!");
            Console.WriteLine($"[PASS] Перевірено Coca-Cola: {cola.CaloriesPer100} ккал, IsLiquid={cola.IsLiquid}, Unit={cola.Unit}");

            // Перевірка нових продуктів
            var tvorog = products.Find(p => p.Name.Contains("Домашній сир"));
            if (tvorog == null || tvorog.CaloriesPer100 != 160 || tvorog.ProteinPer100 != 16)
                throw new Exception("Помилка даних домашнього сиру у базі!");
            Console.WriteLine($"[PASS] Перевірено Домашній сир: {tvorog.CaloriesPer100} ккал, Б: {tvorog.ProteinPer100}г, Ж: {tvorog.FatPer100}г");

            var smetana = products.Find(p => p.Name.Contains("Сметана"));
            if (smetana == null || smetana.CaloriesPer100 != 160 || smetana.FatPer100 != 15)
                throw new Exception("Помилка даних сметани у базі!");
            Console.WriteLine($"[PASS] Перевірено Сметана 15%: {smetana.CaloriesPer100} ккал, Ж: {smetana.FatPer100}г");

            var bread = products.Find(p => p.Name.Contains("Білий хліб"));
            if (bread == null || bread.CaloriesPer100 != 265 || bread.CarbsPer100 != 49)
                throw new Exception("Помилка даних білого хліба у базі!");
            Console.WriteLine($"[PASS] Перевірено Білий хліб: {bread.CaloriesPer100} ккал, В: {bread.CarbsPer100}г");

            var tomato = products.Find(p => p.Name.Contains("Помідори"));
            if (tomato == null || tomato.CaloriesPer100 != 18 || tomato.CarbsPer100 != 3.9)
                throw new Exception("Помилка даних помідорів у базі!");
            Console.WriteLine($"[PASS] Перевірено Помідори: {tomato.CaloriesPer100} ккал, В: {tomato.CarbsPer100}г");

            // 4. Тест автоматичного перерахунку порції (Кола 0.5 л = 500 мл)
            double colaRatio = 500.0 / 100.0;
            double colaCal = cola.CaloriesPer100 * colaRatio;
            double colaCarbs = cola.CarbsPer100 * colaRatio;
            if (Math.Abs(colaCal - 210.0) > 0.01 || Math.Abs(colaCarbs - 53.0) > 0.01)
                throw new Exception($"Помилка перерахунку напою: очікувалось 210 ккал і 53 г вуглеводів, отримано {colaCal} ккал і {colaCarbs} г");
            Console.WriteLine($"[PASS] Автоперерахунок 0.5 л Коли: {colaCal:0} ккал, {colaCarbs:0} г вуглеводів");

            // 5. Тест парсера швидкого введення
            AssertQuickInput("450", 450, "");
            AssertQuickInput("+500", 500, "");
            AssertQuickInput("-150", -150, "");
            AssertQuickInput("обід 600", 600, "обід");
            AssertQuickInput("600 обід", 600, "обід");
            AssertQuickInput("кава з молоком 120 ккал", 120, "кава з молоком");
            AssertQuickInput("pizza 850 kcal", 850, "pizza");
            Console.WriteLine("[PASS] Тести швидкого введення пройдено успішно!");

            // 6. Тест StorageService (history.json, settings.json)
            var storage = new StorageService();
            var today = storage.GetTodaySummary();
            var testEntry = new FoodLogEntry("Тестова страва", 150, "г", 320, 25, 10, 32);
            today.Entries.Add(testEntry);
            storage.SaveTodaySummary(today);

            var reloaded = storage.GetTodaySummary();
            if (!reloaded.Entries.Exists(e => e.Name == "Тестова страва" && e.Calories == 320))
                throw new Exception("Помилка збереження history.json!");
            Console.WriteLine("[PASS] Збереження в history.json перевірено!");

            reloaded.Entries.RemoveAll(e => e.Name == "Тестова страва");
            storage.SaveTodaySummary(reloaded);

            // 7. Тест динамічного перемикача тем (ThemeManager)
            ThemeManager.ApplyTheme("Light");
            if (ThemeManager.CurrentTheme != "Light")
                throw new Exception("ThemeManager failed to switch to Light theme!");
            ThemeManager.ApplyTheme("Dark");
            if (ThemeManager.CurrentTheme != "Dark")
                throw new Exception("ThemeManager failed to switch to Dark theme!");
            Console.WriteLine("[PASS] Перемикання тем (Світла / Темна) перевірено!");

            // 8. Тест безпечного завантаження діалогу AddFoodDialog (STA)
            Exception? dialogException = null;
            var staThread = new System.Threading.Thread(() =>
            {
                try
                {
                    var testStorage = new StorageService();
                    var dialog = new AddFoodDialog(testStorage, () => { });
                }
                catch (Exception ex)
                {
                    dialogException = ex;
                }
            });
            staThread.SetApartmentState(System.Threading.ApartmentState.STA);
            staThread.Start();
            staThread.Join();

            if (dialogException != null)
            {
                throw new Exception($"Помилка ініціалізації AddFoodDialog: {dialogException.Message}\n{dialogException.StackTrace}");
            }
            Console.WriteLine("[PASS] Вікно AddFoodDialog успішно створено та перевірено без помилок!");

            // 7. Бенчмарк швидкодії холодного старту (Storage + JSON Source Generation)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var benchStorage = new StorageService();
            var loadedSettings = benchStorage.LoadSettings();
            var todaySummary = benchStorage.GetTodaySummary();
            sw.Stop();
            Console.WriteLine($"[PERF] Ініціалізація сховища та читання JSON (Source Gen): {sw.Elapsed.TotalMilliseconds:F2} мс");
            if (sw.ElapsedMilliseconds > 100)
            {
                throw new Exception($"I/O занадто повільне: {sw.ElapsedMilliseconds} мс");
            }
            Console.WriteLine("[PASS] Тест надшвидкого старту пройдено (< 100 мс)!");

            // 8. Тест розумного графіка прийомів їжі (MealScheduleService)
            var scheduleSettings = new AppSettings
            {
                FirstMealTime = "09:00",
                MealCount = 4,
                MealIntervalHours = 3.5,
                Goals = new MacroGoals { Calories = 2000, ProteinGrams = 140, FatGrams = 60, CarbsGrams = 225 }
            };

            var morningTime = new DateTime(2026, 9, 25, 8, 30, 0); // 08:30
            var scheduleOverview = MealScheduleService.CalculateOverview(scheduleSettings, morningTime);
            if (scheduleOverview.Meals.Count != 4)
                throw new Exception($"Очікувалось 4 прийоми, отримано {scheduleOverview.Meals.Count}");
            if (scheduleOverview.Meals[0].CalorieTarget != 500) // 25% of 2000 = 500
                throw new Exception($"Сніданок: очікувалось 500 ккал, отримано {scheduleOverview.Meals[0].CalorieTarget}");
            if (scheduleOverview.Meals[1].CalorieTarget != 700) // 35% of 2000 = 700
                throw new Exception($"Обід: очікувалось 700 ккал, отримано {scheduleOverview.Meals[1].CalorieTarget}");
            if (!scheduleOverview.Headline.Contains("Сніданок"))
                throw new Exception($"Заголовок повинен вказувати на Сніданок: {scheduleOverview.Headline}");

            // Тест часу обіду (12:35)
            var lunchTime = new DateTime(2026, 9, 25, 12, 35, 0);
            var lunchOverview = MealScheduleService.CalculateOverview(scheduleSettings, lunchTime);
            if (!lunchOverview.IsMealTimeNow || !lunchOverview.Headline.Contains("Час їсти"))
                throw new Exception($"Повинен бути активний стан 'Час їсти': {lunchOverview.Headline}");

            Console.WriteLine("[PASS] Розумний графік прийомів їжі (MealScheduleService) перевірено успішно!");

            Console.WriteLine("\nУСІ ТЕСТИ УСПІШНО ПРОЙДЕНО (100% SUCCESS)!");
        }

        private static void AssertQuickInput(string input, int expectedCal, string expectedDesc)
        {
            if (!CalorieService.TryParseQuickInput(input, out int cal, out string desc))
                throw new Exception($"Не вдалося розпарсити '{input}'");

            if (cal != expectedCal)
                throw new Exception($"'{input}': очікувалось {expectedCal} ккал, отримано {cal}");

            if (expectedDesc != "" && !desc.Equals(expectedDesc, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"'{input}': очікувався опис '{expectedDesc}', отримано '{desc}'");
        }
    }
}
