using System;

namespace CalWidget.Models
{
    public class CalorieEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public int Calories { get; set; }
        public string Description { get; set; } = string.Empty;

        public CalorieEntry() { }

        public CalorieEntry(int calories, string description)
        {
            Calories = calories;
            Description = description;
            Timestamp = DateTime.Now;
        }
    }
}
