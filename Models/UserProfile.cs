namespace CalWidget.Models
{
    public class UserProfile
    {
        public string Gender { get; set; } = "Male"; // "Male" or "Female"
        public double WeightKg { get; set; } = 75.0;
        public double HeightCm { get; set; } = 178.0;
        public int Age { get; set; } = 28;
        
        // Рівні активності: 1.20, 1.375, 1.55, 1.725, 1.90
        public double ActivityLevel { get; set; } = 1.375;

        // Цілі: "Deficit20", "Deficit15", "Maintain", "Surplus10", "Surplus15"
        public string Goal { get; set; } = "Deficit15";
    }
}
