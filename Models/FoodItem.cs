using System;

namespace CalWidget.Models
{
    public class FoodItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double CaloriesPer100 { get; set; }
        public double ProteinPer100 { get; set; }
        public double FatPer100 { get; set; }
        public double CarbsPer100 { get; set; }
        public string Unit { get; set; } = "г"; // ОБОВ'ЯЗКОВО відкриті { get; set; }!
        public bool IsLiquid { get; set; } = false; // true для напоїв (мл/л)
        public double DefaultPortion { get; set; } = 100;

        // --- Властивості для багатого UI каталогу з відкритими get; set; ---
        public string IconEmoji
        {
            get
            {
                if (IsLiquid || (!string.IsNullOrEmpty(Category) && Category.Contains("Напо"))) return "🥤";
                if (!string.IsNullOrEmpty(Category) && (Category.Contains("М'яс") || Category.Contains("Яйц") || Category.Contains("птиц"))) return "🥩";
                if (!string.IsNullOrEmpty(Category) && (Category.Contains("Молоч") || Category.Contains("Сир") || Category.Contains("Сметан") || Category.Contains("творог"))) return "🥛";
                if (!string.IsNullOrEmpty(Category) && (Category.Contains("Хліб") || Category.Contains("Булоч") || Category.Contains("соус") || Category.Contains("Кетчуп") || Category.Contains("випіч"))) return "🍞";
                if (!string.IsNullOrEmpty(Category) && (Category.Contains("Гарнір") || Category.Contains("Овоч") || Category.Contains("Круп") || Category.Contains("Помідор") || Category.Contains("Банан") || Category.Contains("Снек"))) return "🍚";
                return "🍽️";
            }
            set { }
        }

        public string IconBgBrushHex
        {
            get
            {
                if (IsLiquid || (!string.IsNullOrEmpty(Category) && Category.Contains("Напо"))) return "#1A365D"; // Синьо-бірюзовий
                if (!string.IsNullOrEmpty(Category) && (Category.Contains("М'яс") || Category.Contains("Яйц") || Category.Contains("птиц"))) return "#4C1D24"; // М'який червонуватий
                if (!string.IsNullOrEmpty(Category) && (Category.Contains("Молоч") || Category.Contains("Сир") || Category.Contains("Сметан") || Category.Contains("творог"))) return "#1E2A3E"; // Молочно-синій
                if (!string.IsNullOrEmpty(Category) && (Category.Contains("Хліб") || Category.Contains("Булоч") || Category.Contains("соус") || Category.Contains("Кетчуп") || Category.Contains("випіч"))) return "#4A2E18"; // Теплий коричнево-хлібний
                if (!string.IsNullOrEmpty(Category) && (Category.Contains("Гарнір") || Category.Contains("Овоч") || Category.Contains("Круп") || Category.Contains("Помідор") || Category.Contains("Банан") || Category.Contains("Снек"))) return "#203828"; // Теплий рослинно-зелений
                return "#26262D";
            }
            set { }
        }

        public string UnitPer100Text
        {
            get => IsLiquid ? "ккал / 100мл" : "ккал / 100г";
            set { }
        }

        public string ProteinBadgeText
        {
            get => $"Б: {ProteinPer100:0.#}г";
            set { }
        }

        public string FatBadgeText
        {
            get => $"Ж: {FatPer100:0.#}г";
            set { }
        }

        public string CarbsBadgeText
        {
            get => $"В: {CarbsPer100:0.#}г";
            set { }
        }

        public string CaloriesFormatted
        {
            get => $"{CaloriesPer100:0}";
            set { }
        }

        public FoodItem() { }

        public FoodItem(string name, string category, bool isLiquid, double cal, double prot, double fat, double carbs, double defaultPortion = 100, string? unit = null)
        {
            Name = name;
            Category = category;
            IsLiquid = isLiquid;
            CaloriesPer100 = cal;
            ProteinPer100 = prot;
            FatPer100 = fat;
            CarbsPer100 = carbs;
            DefaultPortion = defaultPortion;
            Unit = unit ?? (isLiquid ? "мл" : "г");
        }
    }
}
