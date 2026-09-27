using System;

namespace CalWidget.Models
{
    public class FoodLogEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Name { get; set; } = string.Empty;
        public double PortionAmount { get; set; } // e.g. 150 (g) or 500 (ml)
        public string Unit { get; set; } = "г"; // "г" або "мл"

        public double Calories { get; set; }
        public double Protein { get; set; }
        public double Fat { get; set; }
        public double Carbs { get; set; }

        public FoodLogEntry() { }

        public FoodLogEntry(string name, double portion, string unit, double cal, double prot, double fat, double carbs)
        {
            Name = name;
            PortionAmount = portion;
            Unit = unit;
            Calories = cal;
            Protein = prot;
            Fat = fat;
            Carbs = carbs;
            Timestamp = DateTime.Now;
        }
    }
}
