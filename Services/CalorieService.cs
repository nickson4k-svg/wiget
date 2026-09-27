using System;
using System.Text.RegularExpressions;
using CalWidget.Models;

namespace CalWidget.Services
{
    public static class CalorieService
    {
        /// <summary>
        /// Розрахунок базового метаболізму (BMR) за формулою Міффліна-Сан Жеора.
        /// </summary>
        public static double CalculateBmr(UserProfile profile)
        {
            // BMR = 10 * вага (кг) + 6.25 * зріст (см) - 5 * вік (років) + s
            // s = +5 для чоловіків, -161 для жінок
            double baseBmr = (10.0 * profile.WeightKg) + (6.25 * profile.HeightCm) - (5.0 * profile.Age);
            bool isMale = string.Equals(profile.Gender, "Male", StringComparison.OrdinalIgnoreCase);
            return isMale ? baseBmr + 5.0 : baseBmr - 161.0;
        }

        /// <summary>
        /// Розрахунок добової витрати енергії з урахуванням активності (TDEE).
        /// </summary>
        public static double CalculateTdee(UserProfile profile)
        {
            double bmr = CalculateBmr(profile);
            return bmr * profile.ActivityLevel;
        }

        /// <summary>
        /// Розрахунок рекомендованої калорійності відповідно до цілі.
        /// </summary>
        public static int CalculateRecommendedTarget(UserProfile profile)
        {
            double tdee = CalculateTdee(profile);
            double multiplier = profile.Goal switch
            {
                "Deficit20" => 0.80,
                "Deficit15" => 0.85,
                "Surplus10" => 1.10,
                "Surplus15" => 1.15,
                _ => 1.00 // "Maintain"
            };

            return (int)Math.Round(tdee * multiplier);
        }

        /// <summary>
        /// Розрахунок розподілу макронутрієнтів (БЖВ) у грамах.
        /// 1 г білка = 4 ккал, 1 г жиру = 9 ккал, 1 г вуглеводів = 4 ккал.
        /// </summary>
        public static MacroGoals CalculateMacroGoals(UserProfile profile)
        {
            int calories = CalculateRecommendedTarget(profile);

            // Розподіл відсотків за цілями:
            // Схуднення: Білки 30%, Жири 30%, Вуглеводи 40%
            // Підтримка: Білки 25%, Жири 25%, Вуглеводи 50%
            // Набір маси: Білки 25%, Жири 20%, Вуглеводи 55%
            double protPct, fatPct, carbsPct;

            if (profile.Goal == "Deficit20" || profile.Goal == "Deficit15")
            {
                protPct = 0.30;
                fatPct = 0.30;
                carbsPct = 0.40;
            }
            else if (profile.Goal == "Surplus10" || profile.Goal == "Surplus15")
            {
                protPct = 0.25;
                fatPct = 0.20;
                carbsPct = 0.55;
            }
            else
            {
                protPct = 0.25;
                fatPct = 0.25;
                carbsPct = 0.50;
            }

            int proteinGrams = (int)Math.Round((calories * protPct) / 4.0);
            int fatGrams = (int)Math.Round((calories * fatPct) / 9.0);
            int carbsGrams = (int)Math.Round((calories * carbsPct) / 4.0);

            return new MacroGoals
            {
                Calories = calories,
                ProteinGrams = proteinGrams,
                FatGrams = fatGrams,
                CarbsGrams = carbsGrams,
                IsCustom = false
            };
        }

        /// <summary>
        /// Парсер рядка швидкого додавання.
        /// Приклади: "450", "+450", "-150", "обід 600", "600 обід", "кава 120 ккал".
        /// </summary>
        public static bool TryParseQuickInput(string? rawInput, out int calories, out string description)
        {
            calories = 0;
            description = string.Empty;

            if (string.IsNullOrWhiteSpace(rawInput))
                return false;

            string text = rawInput.Trim();

            var match = Regex.Match(text, @"(?<number>[+-]?\d{1,5})");
            if (!match.Success)
                return false;

            if (!int.TryParse(match.Groups["number"].Value, out calories))
                return false;

            string remaining = text.Remove(match.Index, match.Length).Trim();
            remaining = Regex.Replace(remaining, @"\b(ккал|kcal|кал|cal)\b", "", RegexOptions.IgnoreCase).Trim();
            remaining = Regex.Replace(remaining, @"\s+", " ").Trim(' ', ',', '-', ':');

            description = remaining;
            return true;
        }
    }
}
