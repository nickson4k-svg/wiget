using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CalWidget.Models
{
    public class DailySummary
    {
        public string Date { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
        public List<FoodLogEntry> Entries { get; set; } = new();

        [JsonIgnore]
        public double TotalCalories => Entries?.Sum(e => e.Calories) ?? 0;

        [JsonIgnore]
        public double TotalProtein => Entries?.Sum(e => e.Protein) ?? 0;

        [JsonIgnore]
        public double TotalFat => Entries?.Sum(e => e.Fat) ?? 0;

        [JsonIgnore]
        public double TotalCarbs => Entries?.Sum(e => e.Carbs) ?? 0;
    }
}
