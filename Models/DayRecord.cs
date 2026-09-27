using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CalWidget.Models
{
    public class DayRecord
    {
        public string Date { get; set; } = string.Empty; // Format: "yyyy-MM-dd"
        public List<CalorieEntry> Entries { get; set; } = new();

        [JsonIgnore]
        public int TotalCalories => Entries?.Sum(e => e.Calories) ?? 0;
    }
}
