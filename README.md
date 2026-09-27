# CalWidget — Native Calories & Macro Tracker Widget for Windows

A lightweight, fast, and elegant native desktop widget for tracking calories and macronutrients (proteins, fats, carbs) styled with intuitive **Macro Rings**.

![CalWidget Screenshot](assets/IMG_20260927_200856.jpg)

---

## Key Features

- **Macro Fitness Rings:** Three concentric animated rings displaying progress for Fats, Proteins, and Carbs.
- **Fluted Glass Optical Shader Effect:** Custom high-performance in-memory shader engine rendering ribbed frosted glass (Paper Design aesthetic) with real-time backdrop distortion, dynamic shadows, edge bevels, and zero GC allocations at 60 FPS.
- **Instant Theme Switching:** Seamlessly toggle between Light and Dark themes with settings auto-saved.
- **Rich Food Catalog:** Easily add food and beverages with pre-configured gram or volume quick-select buttons.
- **BMR & TDEE Calculator:** Built-in Mifflin-St Jeor formula for accurate daily targets and customizable macro goals.
- **Ultra-Lightweight & Fast:** Runs natively with minimal memory footprint (< 25 MB) and instant launch time.

---

## Installation & Running

### Option 1. Run Pre-Built Executable (Recommended)

1. Clone or download the repository:
   ```cmd
   git clone https://github.com/nickson4k-svg/wiget.git
   cd wiget
   ```
2. Launch `run.bat` or run `publish/CalWidget.exe` directly.

### Option 2. Build from Source (.NET 8 SDK)

1. Ensure **.NET 8 SDK** (or newer) is installed.
2. Open a terminal in the project directory and run:
   ```cmd
   dotnet publish -c Release -o publish
   ```
3. Run the compiled `CalWidget.exe` from the generated `publish` folder.

---

## Project Structure

```text
├── assets/              # Screenshots and media assets
│   └── screenshot.png   # Main application screenshot
├── publish/             # Standalone compiled CalWidget.exe
├── Helpers/             # Fluted glass shader engine, hotkeys, native window helpers
├── Models/              # Data models (MacroGoals, DayRecord, AppSettings, etc.)
├── Services/            # Storage, calorie calculations, and food database services
├── Themes/              # WPF Light and Dark theme resources
├── App.xaml / MainWindow.xaml / SettingsWindow.xaml / AddFoodDialog.xaml
├── run.bat              # Quick launch batch script
└── README.md            # Project documentation
```
