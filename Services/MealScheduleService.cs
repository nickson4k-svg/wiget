using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CalWidget.Models;

namespace CalWidget.Services
{
    public class ScheduledMeal
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public TimeSpan Time { get; set; }
        public string TimeString => Time.ToString(@"hh\:mm");
        public int CalorieTarget { get; set; }
        public int ProteinTarget { get; set; }
        public int FatTarget { get; set; }
        public int CarbsTarget { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsActiveNow { get; set; }
    }

    public class ScheduleOverview
    {
        public string Headline { get; set; } = string.Empty;
        public string TargetLine { get; set; } = string.Empty;
        public bool IsMealTimeNow { get; set; }
        public List<ScheduledMeal> Meals { get; set; } = new();
    }

    public static class MealScheduleService
    {
        public static ScheduleOverview CalculateOverview(AppSettings settings, DateTime now)
        {
            var overview = new ScheduleOverview();
            string todayKey = now.ToString("yyyy-MM-dd");

            // Якщо дата змінилась — очищаємо виконані прийоми
            if (settings.CompletedMealsDate != todayKey)
            {
                settings.CompletedMealsDate = todayKey;
                settings.CompletedMealNames.Clear();
            }

            // Парсимо час першого прийому
            if (!TimeSpan.TryParseExact(settings.FirstMealTime, @"h\:mm", CultureInfo.InvariantCulture, out var firstMealTime) &&
                !TimeSpan.TryParseExact(settings.FirstMealTime, @"hh\:mm", CultureInfo.InvariantCulture, out firstMealTime))
            {
                firstMealTime = new TimeSpan(9, 0, 0);
            }

            int count = Math.Clamp(settings.MealCount, 3, 5);
            double intervalHours = settings.MealIntervalHours > 0 ? settings.MealIntervalHours : 3.5;
            var interval = TimeSpan.FromHours(intervalHours);

            var mealsConfig = GetMealTemplates(count);
            var goals = settings.Goals;

            var mealsList = new List<ScheduledMeal>();
            for (int i = 0; i < mealsConfig.Count; i++)
            {
                var (name, ratio) = mealsConfig[i];
                var mealTime = firstMealTime + TimeSpan.FromMinutes(interval.TotalMinutes * i);

                // Нормалізація в межах 24 годин
                if (mealTime.TotalHours >= 24)
                {
                    mealTime = TimeSpan.FromMinutes(mealTime.TotalMinutes % (24 * 60));
                }

                bool isCompleted = settings.CompletedMealNames.Contains(name);

                var meal = new ScheduledMeal
                {
                    Id = $"meal_{i}_{name}",
                    Name = name,
                    Time = mealTime,
                    CalorieTarget = (int)Math.Round(goals.Calories * ratio),
                    ProteinTarget = (int)Math.Round(goals.ProteinGrams * ratio),
                    FatTarget = (int)Math.Round(goals.FatGrams * ratio),
                    CarbsTarget = (int)Math.Round(goals.CarbsGrams * ratio),
                    IsCompleted = isCompleted
                };

                mealsList.Add(meal);
            }

            var currentTime = now.TimeOfDay;

            // Визначаємо активний або наступний прийом
            ScheduledMeal? currentOrNextMeal = null;
            bool isCurrentWindow = false;

            // 1. Перевіряємо, чи зараз вікно прийому не виконаної страви (від -20 хв до +45 хв від планового часу)
            foreach (var m in mealsList)
            {
                if (m.IsCompleted) continue;

                var diffMin = (currentTime - m.Time).TotalMinutes;
                if (diffMin >= -20 && diffMin <= 45)
                {
                    m.IsActiveNow = true;
                    currentOrNextMeal = m;
                    isCurrentWindow = true;
                    break;
                }
            }

            // 2. Якщо вікна прямо зараз немає, шукаємо перший майбутній невиконаний прийом
            if (currentOrNextMeal == null)
            {
                currentOrNextMeal = mealsList.FirstOrDefault(m => !m.IsCompleted && m.Time > currentTime)
                                   ?? mealsList.FirstOrDefault(m => !m.IsCompleted);
            }

            if (currentOrNextMeal != null)
            {
                if (isCurrentWindow)
                {
                    overview.IsMealTimeNow = true;
                    overview.Headline = $"🍽️ Час їсти: {currentOrNextMeal.Name}! (~{currentOrNextMeal.CalorieTarget} ккал)";
                    overview.TargetLine = $"Ціль: ~{currentOrNextMeal.CalorieTarget} ккал | {currentOrNextMeal.ProteinTarget}г Б · {currentOrNextMeal.FatTarget}г Ж · {currentOrNextMeal.CarbsTarget}г В";
                }
                else
                {
                    string countdown = FormatCountdown(currentTime, currentOrNextMeal.Time);
                    overview.Headline = $"🕒 Наступний: {currentOrNextMeal.Name} о {currentOrNextMeal.TimeString} ({countdown})";
                    overview.TargetLine = $"Ціль: ~{currentOrNextMeal.CalorieTarget} ккал | {currentOrNextMeal.ProteinTarget}г Б · {currentOrNextMeal.FatTarget}г Ж · {currentOrNextMeal.CarbsTarget}г В";
                }
            }
            else
            {
                // Усі прийоми їжі виконано
                overview.IsMealTimeNow = false;
                overview.Headline = "✨ Всі заплановані прийоми їжі на сьогодні виконано!";
                overview.TargetLine = $"Норма дня: {goals.Calories} ккал | {goals.ProteinGrams}г Б · {goals.FatGrams}г Ж · {goals.CarbsGrams}г В";
            }

            overview.Meals = mealsList;
            return overview;
        }

        private static List<(string Name, double Ratio)> GetMealTemplates(int count)
        {
            return count switch
            {
                3 => new List<(string, double)>
                {
                    ("Сніданок", 0.35),
                    ("Обід", 0.40),
                    ("Вечеря", 0.25)
                },
                5 => new List<(string, double)>
                {
                    ("Сніданок", 0.20),
                    ("Другий сніданок", 0.15),
                    ("Обід", 0.35),
                    ("Полуденок", 0.10),
                    ("Вечеря", 0.20)
                },
                _ => new List<(string, double)> // 4 meals default
                {
                    ("Сніданок", 0.25),
                    ("Обід", 0.35),
                    ("Перекус", 0.15),
                    ("Вечеря", 0.25)
                }
            };
        }

        private static string FormatCountdown(TimeSpan current, TimeSpan target)
        {
            var diff = target - current;
            if (diff < TimeSpan.Zero)
            {
                // Якщо прийом призначено на наступну добу
                diff += TimeSpan.FromHours(24);
            }

            int hours = (int)diff.TotalHours;
            int mins = diff.Minutes;

            if (hours > 0)
            {
                return $"через {hours} год {mins} хв";
            }
            if (mins <= 1)
            {
                return "менш ніж за хвилину";
            }
            return $"через {mins} хв";
        }
    }
}
