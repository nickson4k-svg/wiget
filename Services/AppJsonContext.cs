using System.Collections.Generic;
using System.Text.Json.Serialization;
using CalWidget.Models;

namespace CalWidget.Services
{
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        GenerationMode = JsonSourceGenerationMode.Default)]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(UserProfile))]
    [JsonSerializable(typeof(MacroGoals))]
    [JsonSerializable(typeof(DailySummary))]
    [JsonSerializable(typeof(FoodLogEntry))]
    [JsonSerializable(typeof(List<FoodLogEntry>))]
    [JsonSerializable(typeof(Dictionary<string, DailySummary>))]
    [JsonSerializable(typeof(FoodItem))]
    [JsonSerializable(typeof(List<FoodItem>))]
    [JsonSerializable(typeof(DayRecord))]
    [JsonSerializable(typeof(CalorieEntry))]
    [JsonSerializable(typeof(List<CalorieEntry>))]
    [JsonSerializable(typeof(List<string>))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
