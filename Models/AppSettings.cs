using System.Collections.Generic;

namespace CalWidget.Models
{
    public class AppSettings
    {
        public double WindowLeft { get; set; } = -1;
        public double WindowTop { get; set; } = -1;
        public bool IsTopMost { get; set; } = true;
        public bool HideAfterQuickAdd { get; set; } = false;
        public string CurrentTheme { get; set; } = "Dark"; // "Dark" or "Light"

        // Налаштування ефекту скла (Fluted Glass & Mica Fluent)
        public bool EnableFlutedGlass { get; set; } = true;
        public double GlassOpacity { get; set; } = 0.60; // 0.10 .. 0.80 (10% - 80%)
        public double RibDistortion { get; set; } = 0.75; // 0.0 .. 1.5 (виразність / контраст смужок)
        public int RibWidth { get; set; } = 12; // 8px .. 32px
        public int BlurRadius { get; set; } = 3; // 0px .. 15px

        public UserProfile Profile { get; set; } = new();
        public MacroGoals Goals { get; set; } = new();

        // Розумний графік прийомів їжі (Meal Timing & Schedule)
        public string FirstMealTime { get; set; } = "09:00"; // "HH:mm"
        public int MealCount { get; set; } = 4; // 3, 4, 5
        public double MealIntervalHours { get; set; } = 3.5; // години між прийомами
        public string CompletedMealsDate { get; set; } = string.Empty;
        public List<string> CompletedMealNames { get; set; } = new();
    }
}
