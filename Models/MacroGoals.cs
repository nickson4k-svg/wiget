namespace CalWidget.Models
{
    public class MacroGoals
    {
        public int Calories { get; set; } = 2100;
        public int ProteinGrams { get; set; } = 130;
        public int FatGrams { get; set; } = 70;
        public int CarbsGrams { get; set; } = 235;

        public bool IsCustom { get; set; } = false;
    }
}
