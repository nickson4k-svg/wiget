using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CalWidget.Helpers;
using CalWidget.Models;
using CalWidget.Services;

namespace CalWidget
{
    public partial class MainWindow : Window
    {
        private readonly StorageService _storageService;
        private readonly AppSettings _settings;
        private readonly HotkeyHelper _hotkeyHelper;
        private readonly TrayIconHelper _trayHelper;
        private readonly DispatcherTimer _midnightTimer;

        // Оптичний шейдер каскадного рифленого скла Fluted Glass (Paper Design)
        private bool _isUpdatingGlass = false;

        // Стан висувної бічної панелі каталогу (Flyout Drawer)
        private bool _isCatalogOpen = false;
        private double _preOpenLeft = -1;
        private List<FoodItem> _allCatalogProducts = new();
        private FoodItem? _selectedFlyoutProduct;
        private bool _isCatalogLoaded = false;

        // Dependency Properties для плавних анімацій кілець БЖВ та лічильника калорій
        public static readonly DependencyProperty FatProgressProperty =
            DependencyProperty.Register(nameof(FatProgress), typeof(double), typeof(MainWindow),
                new PropertyMetadata(0.0, (d, e) => ((MainWindow)d).OnFatProgressChanged((double)e.NewValue)));

        public static readonly DependencyProperty ProteinProgressProperty =
            DependencyProperty.Register(nameof(ProteinProgress), typeof(double), typeof(MainWindow),
                new PropertyMetadata(0.0, (d, e) => ((MainWindow)d).OnProteinProgressChanged((double)e.NewValue)));

        public static readonly DependencyProperty CarbsProgressProperty =
            DependencyProperty.Register(nameof(CarbsProgress), typeof(double), typeof(MainWindow),
                new PropertyMetadata(0.0, (d, e) => ((MainWindow)d).OnCarbsProgressChanged((double)e.NewValue)));

        public static readonly DependencyProperty CaloriesCountProperty =
            DependencyProperty.Register(nameof(CaloriesCount), typeof(double), typeof(MainWindow),
                new PropertyMetadata(0.0, (d, e) => ((MainWindow)d).OnCaloriesCountChanged((double)e.NewValue)));

        public double FatProgress
        {
            get => (double)GetValue(FatProgressProperty);
            set => SetValue(FatProgressProperty, value);
        }

        public double ProteinProgress
        {
            get => (double)GetValue(ProteinProgressProperty);
            set => SetValue(ProteinProgressProperty, value);
        }

        public double CarbsProgress
        {
            get => (double)GetValue(CarbsProgressProperty);
            set => SetValue(CarbsProgressProperty, value);
        }

        public double CaloriesCount
        {
            get => (double)GetValue(CaloriesCountProperty);
            set => SetValue(CaloriesCountProperty, value);
        }

        // Обробники зміни значень під час DoubleAnimation (оновлюють дуги за тригонометрією, центр 95, 95)
        private void OnFatProgressChanged(double val)
        {
            RingGeometryHelper.UpdateArcPath(RingFatsPath, new Point(98, 98), 87, val);
            RingGeometryHelper.UpdateArcPath(RingFatsHighlightPath, new Point(98, 98), 87, val);
        }

        private void OnProteinProgressChanged(double val)
        {
            RingGeometryHelper.UpdateArcPath(RingProteinPath, new Point(98, 98), 74, val);
            RingGeometryHelper.UpdateArcPath(RingProteinHighlightPath, new Point(98, 98), 74, val);
        }

        private void OnCarbsProgressChanged(double val)
        {
            RingGeometryHelper.UpdateArcPath(RingCarbsPath, new Point(98, 98), 61, val);
            RingGeometryHelper.UpdateArcPath(RingCarbsHighlightPath, new Point(98, 98), 61, val);
        }

        private void OnCaloriesCountChanged(double val)
        {
            if (CaloriesEatenText != null)
            {
                CaloriesEatenText.Text = $"{(int)Math.Round(val):N0} ккал".Replace(',', ' ');
            }
        }

        // Іконки для перемикача теми
        private const string SunIconData = "M12,18C11.11,18 10.26,17.8 9.5,17.45C11.56,16.5 13,14.42 13,12C13,9.58 11.56,7.5 9.5,6.55C10.26,6.2 11.11,6 12,6A6,6 0 0,1 18,12A6,6 0 0,1 12,18M20,8.69V4H15.31L12,0.69L8.69,4H4V8.69L0.69,12L4,15.31V20H8.69L12,23.31L15.31,20H20V15.31L23.31,12L20,8.69Z";
        private const string MoonIconData = "M17.75,4.09L15.22,6.03L16.13,9.09L13.5,7.28L10.87,9.09L11.78,6.03L9.25,4.09L12.44,4L13.5,1L14.56,4L17.75,4.09M21.25,11L19.61,12.25L20.2,14.23L18.5,13.06L16.8,14.23L17.39,12.25L15.75,11L17.81,10.95L18.5,9L19.19,10.95L21.25,11M18.97,15.95C19.8,15.87 20.69,17.05 20.16,17.8C19.84,18.25 19.5,18.67 19.08,19.07C15.17,23 8.84,23 4.94,19.07C1.03,15.17 1.03,8.83 4.94,4.93C5.34,4.53 5.76,4.17 6.21,3.85C6.96,3.32 8.14,4.21 8.06,5.04C7.79,7.9 8.75,10.87 10.95,13.06C13.14,15.26 16.1,16.22 18.97,15.95Z";

        public MainWindow()
        {
            InitializeComponent();

            _storageService = new StorageService();
            _settings = _storageService.LoadSettings();

            // Застосовуємо тему з конфігурації
            ThemeManager.ApplyTheme(_settings.CurrentTheme);
            UpdateThemeIcon(ThemeManager.CurrentTheme);
            ThemeManager.ThemeChanged += UpdateThemeIcon;

            _hotkeyHelper = new HotkeyHelper();
            _trayHelper = new TrayIconHelper();

            _midnightTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _midnightTimer.Tick += MidnightTimer_Tick;
            _midnightTimer.Start();

        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyWindowPositionAndState();

            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                _dpiScaleX = dpi.DpiScaleX;
                _dpiScaleY = dpi.DpiScaleY;
            }
            catch { }
            UpdateRingRelativeOffset();

            SizeChanged += (s, ev) =>
            {
                UpdateRingRelativeOffset();
                UpdateGlassViewboxes();
            };

            // Трей Windows
            _trayHelper.Initialize(this, "CalWidget - Калькулятор калорій та БЖВ");
            _trayHelper.TrayLeftClicked += OnTrayLeftClicked;
            _trayHelper.TrayRightClicked += OnTrayRightClicked;

            // Глобальний хоткей: Ctrl + Alt + C
            _hotkeyHelper.Register(this, NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT, 0x43); // 0x43 = 'C'
            _hotkeyHelper.HotkeyPressed += OnHotkeyPressed;

            // Запускаємо плавну вхідну анімацію від 0 до поточного прогресу
            UpdateUi(animate: true);

            // Генеруємо оптичне заломлення фону Fluted Glass зі шпалер робочого столу (Варіант 2)
            Dispatcher.BeginInvoke(new Action(InitWallpaperGlass), DispatcherPriority.Loaded);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                IntPtr hwnd = helper.EnsureHandle();
                if (hwnd != IntPtr.Zero)
                {
                    // Додаємо Win32 HwndSourceHook для миттєвого оновлення GPU Viewbox (144+ FPS)
                    var source = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
                    source?.AddHook(WndProc);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SourceInitialized error: {ex.Message}");
            }

            WindowGlassHelper.ApplyFrostedGlass(this, _settings.CurrentTheme == "Dark");
        }

        private const int WM_MOVE = 0x0003;
        private const int WM_MOVING = 0x0216;
        private const int WM_EXITSIZEMOVE  = 0x0232;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            try
            {
                switch (msg)
                {
                    case WM_MOVING:
                        unsafe
                        {
                            RECT* pRect = (RECT*)lParam;
                            if (pRect != null)
                            {
                                UpdateGlassViewboxesDirect(pRect->Left, pRect->Top, pRect->Right - pRect->Left, pRect->Bottom - pRect->Top);
                            }
                        }
                        break;

                    case WM_MOVE:
                        short x = (short)(lParam.ToInt64() & 0xFFFF);
                        short y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
                        UpdateGlassViewboxesDirect(x, y, ActualWidth * _dpiScaleX, ActualHeight * _dpiScaleY);
                        break;

                    case WM_EXITSIZEMOVE:
                        UpdateGlassViewboxes();
                        SaveWindowPosition();
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WndProc error: {ex.Message}");
            }
            return IntPtr.Zero;
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            UpdateGlassViewboxes();
        }

        private void UpdateThemeIcon(string theme)
        {
            if (ThemeIconPath == null) return;
            ThemeIconPath.Data = Geometry.Parse(theme == "Dark" ? SunIconData : MoonIconData);
            ThemeToggleBtn.ToolTip = theme == "Dark" ? "Увімкнути світлу тему" : "Увімкнути темну тему";
            WindowGlassHelper.ApplyFrostedGlass(this, theme == "Dark");
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
            _settings.CurrentTheme = ThemeManager.CurrentTheme;
            _storageService.SaveSettings(_settings);
            UpdateThemeIcon(ThemeManager.CurrentTheme);
            InitWallpaperGlass();
        }

        private void ApplyWindowPositionAndState()
        {
            Topmost = _settings.IsTopMost;
            if (PinBtn != null) PinBtn.Opacity = _settings.IsTopMost ? 1.0 : 0.6;

            if (_settings.WindowLeft >= 0 && _settings.WindowTop >= 0)
            {
                double virtualLeft = SystemParameters.VirtualScreenLeft;
                double virtualTop = SystemParameters.VirtualScreenTop;
                double virtualWidth = SystemParameters.VirtualScreenWidth;
                double virtualHeight = SystemParameters.VirtualScreenHeight;

                if (_settings.WindowLeft >= virtualLeft &&
                    _settings.WindowLeft + Width <= virtualLeft + virtualWidth &&
                    _settings.WindowTop >= virtualTop &&
                    _settings.WindowTop + Height <= virtualTop + virtualHeight)
                {
                    Left = _settings.WindowLeft;
                    Top = _settings.WindowTop;
                    return;
                }
            }

            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 24;
            Top = workArea.Bottom - Height - 24;
        }

        private bool _isCustomDragging = false;
        private NativeMethods.POINT _dragStartCursorPos;
        private double _dragStartLeft;
        private double _dragStartTop;

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    MoveToPrimaryScreen();
                    return;
                }

                // Глобальне відстеження курсора: повністю усуває застрягання на другому моніторі
                // та блокування межами моніторів (Aero Snap)
                if (NativeMethods.GetCursorPos(out var pt))
                {
                    _dragStartCursorPos = pt;
                    _dragStartLeft = Left;
                    _dragStartTop = Top;
                    _isCustomDragging = true;
                    CaptureMouse();
                }
            }
        }

        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isCustomDragging)
            {
                if (e.LeftButton == MouseButtonState.Pressed && NativeMethods.GetCursorPos(out var pt))
                {
                    double deltaX = (pt.X - _dragStartCursorPos.X) / _dpiScaleX;
                    double deltaY = (pt.Y - _dragStartCursorPos.Y) / _dpiScaleY;

                    Left = _dragStartLeft + deltaX;
                    Top = _dragStartTop + deltaY;

                    UpdateGlassViewboxes();
                }
                else
                {
                    _isCustomDragging = false;
                    ReleaseMouseCapture();
                    SaveWindowPosition();
                }
            }
        }

        private void Window_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isCustomDragging)
            {
                _isCustomDragging = false;
                ReleaseMouseCapture();
                SaveWindowPosition();
            }
        }

        private void MoveToPrimaryScreen_Click(object sender, RoutedEventArgs e)
        {
            MoveToPrimaryScreen();
        }

        public void MoveToPrimaryScreen()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 24;
            Top = workArea.Bottom - Height - 24;
            SaveWindowPosition();
            UpdateRingRelativeOffset();
            UpdateGlassViewboxes();
        }

        private void SaveWindowPosition()
        {
            // Зберігаємо позицію віджета лише в компактному стані
            if (!_isCatalogOpen)
            {
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
                _storageService.SaveSettings(_settings);
            }
        }

        private void MidnightTimer_Tick(object? sender, EventArgs e)
        {
            UpdateUi(animate: true);
        }

        public void UpdateUi(bool animate = true)
        {
            var today = _storageService.GetTodaySummary();
            var goals = _settings.Goals;

            double currentCal = today.TotalCalories;
            double targetCal = goals.Calories;
            double remCal = targetCal - currentCal;

            // 1. Текстовий показник денної норми (без написів про залишок)
            CaloriesTargetText.Text = $"з {targetCal:N0} ккал".Replace(',', ' ');

            // 2. Цільові відсотки заповнення кілець
            double targetFatProgress = goals.FatGrams > 0 ? today.TotalFat / goals.FatGrams : 0.0;
            double targetProtProgress = goals.ProteinGrams > 0 ? today.TotalProtein / goals.ProteinGrams : 0.0;
            double targetCarbsProgress = goals.CarbsGrams > 0 ? today.TotalCarbs / goals.CarbsGrams : 0.0;

            if (animate)
            {
                StartSmoothAnimation(currentCal, targetFatProgress, targetProtProgress, targetCarbsProgress);
            }
            else
            {
                // Миттєве встановлення значень без анімації
                BeginAnimation(FatProgressProperty, null);
                BeginAnimation(ProteinProgressProperty, null);
                BeginAnimation(CarbsProgressProperty, null);
                BeginAnimation(CaloriesCountProperty, null);

                FatProgress = targetFatProgress;
                ProteinProgress = targetProtProgress;
                CarbsProgress = targetCarbsProgress;
                CaloriesCount = currentCal;

                OnFatProgressChanged(targetFatProgress);
                OnProteinProgressChanged(targetProtProgress);
                OnCarbsProgressChanged(targetCarbsProgress);
                OnCaloriesCountChanged(currentCal);
            }

            // 3. Блок деталізації БЖВ під кільцями (3 колонки: тільки назва та з'їдено з норми)
            CarbsEatenText.Text = $"{today.TotalCarbs:0} з {goals.CarbsGrams:0} г";
            ProteinEatenText.Text = $"{today.TotalProtein:0} з {goals.ProteinGrams:0} г";
            FatEatenText.Text = $"{today.TotalFat:0} з {goals.FatGrams:0} г";

            // Стан кнопки Undo
            UndoBtn.IsEnabled = today.Entries.Count > 0;

            // Оновлення підказки в треї
            string trayTip = remCal >= 0
                ? $"CalWidget: {currentCal:0}/{targetCal:0} ккал (ще {remCal:0})"
                : $"CalWidget: {currentCal:0}/{targetCal:0} ккал (+{Math.Abs(remCal):0})";
            _trayHelper.UpdateTooltip(trayTip);

            // 4. Оновлення блоку розумного графіка прийомів їжі (Meal Timing)
            UpdateMealScheduleUi();
        }

        /// <summary>
        /// Запускає плавну анімацію заповнення дуг кілець та числового лічильника калорій
        /// за допомогою DoubleAnimation та CubicEase (EaseOut) тривалістю 700 мс.
        /// </summary>
        private void StartSmoothAnimation(double targetCal, double targetFat, double targetProt, double targetCarbs)
        {
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(700);

            var animFat = new DoubleAnimation
            {
                From = FatProgress,
                To = targetFat,
                Duration = duration,
                EasingFunction = ease
            };

            var animProt = new DoubleAnimation
            {
                From = ProteinProgress,
                To = targetProt,
                Duration = duration,
                EasingFunction = ease
            };

            var animCarbs = new DoubleAnimation
            {
                From = CarbsProgress,
                To = targetCarbs,
                Duration = duration,
                EasingFunction = ease
            };

            var animCal = new DoubleAnimation
            {
                From = CaloriesCount,
                To = targetCal,
                Duration = duration,
                EasingFunction = ease
            };

            BeginAnimation(FatProgressProperty, animFat);
            BeginAnimation(ProteinProgressProperty, animProt);
            BeginAnimation(CarbsProgressProperty, animCarbs);
            BeginAnimation(CaloriesCountProperty, animCal);
        }

        // ==============================================================
        // ЛОГІКА БІЧНОЇ ВИСУВНОЇ ПАНЕЛІ КАТАЛОГУ (FLYOUT DRAWER)
        // ==============================================================

        public void OpenCatalog()
        {
            if (_isCatalogOpen) return;
            _isCatalogOpen = true;

            var workArea = SystemParameters.WorkArea;
            double targetWidth = 740;
            _preOpenLeft = Left;

            // Якщо розширене вікно виходить за межі екрана справа — плавно зсуваємо вліво
            if (Left + targetWidth > workArea.Right)
            {
                double newLeft = Math.Max(workArea.Left, workArea.Right - targetWidth - 12);
                var leftAnim = new DoubleAnimation(Left, newLeft, TimeSpan.FromMilliseconds(350))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(LeftProperty, leftAnim);
            }

            var widthAnim = new DoubleAnimation(Width, targetWidth, TimeSpan.FromMilliseconds(350))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            widthAnim.Completed += (s, e) =>
            {
                FlyoutSearchBox?.Focus();
                UpdateCascadeGlass();
            };
            BeginAnimation(WidthProperty, widthAnim);

            if (!_isCatalogLoaded)
            {
                _isCatalogLoaded = true;
                LoadCatalogProducts();
            }
        }

        public void CloseCatalog()
        {
            if (!_isCatalogOpen) return;
            _isCatalogOpen = false;

            double targetWidth = 300;
            var widthAnim = new DoubleAnimation(Width, targetWidth, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            widthAnim.Completed += (s, e) =>
            {
                UpdateCascadeGlass();
            };
            BeginAnimation(WidthProperty, widthAnim);

            // Якщо попередньо зсували вікно вліво — повертаємо на початкове положення
            if (_preOpenLeft >= 0 && Math.Abs(_preOpenLeft - Left) > 1)
            {
                var leftAnim = new DoubleAnimation(Left, _preOpenLeft, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(LeftProperty, leftAnim);
            }
        }

        private void OpenCatalog_Click(object sender, RoutedEventArgs e)
        {
            if (_isCatalogOpen)
            {
                CloseCatalog();
            }
            else
            {
                OpenCatalog();
            }
        }

        private void CloseCatalog_Click(object sender, RoutedEventArgs e)
        {
            CloseCatalog();
        }

        private void LoadCatalogProducts()
        {
            if (!_isCatalogLoaded || FlyoutFoodListBox == null) return;

            _allCatalogProducts = _storageService.LoadProducts();
            ApplyCatalogFilter();

            if (FlyoutFoodListBox.Items.Count > 0 && FlyoutFoodListBox.SelectedItem == null)
            {
                FlyoutFoodListBox.SelectedIndex = 0;
            }
        }

        private void ApplyCatalogFilter()
        {
            if (!_isCatalogLoaded || FlyoutFoodListBox == null) return;

            string query = FlyoutSearchBox?.Text?.Trim().ToLowerInvariant() ?? "";

            var filtered = _allCatalogProducts.Where(p =>
            {
                bool matchesCat = true;
                string cat = p.Category ?? "";

                if (CatMeatRadio?.IsChecked == true)
                {
                    matchesCat = cat.Contains("М'яс", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("птиц", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Яйц", StringComparison.OrdinalIgnoreCase);
                }
                else if (CatDairyRadio?.IsChecked == true)
                {
                    matchesCat = cat.Contains("Молоч", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Сир", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Сметан", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("творог", StringComparison.OrdinalIgnoreCase);
                }
                else if (CatDrinksRadio?.IsChecked == true)
                {
                    matchesCat = p.IsLiquid || cat.Contains("Напо", StringComparison.OrdinalIgnoreCase);
                }
                else if (CatBakeryRadio?.IsChecked == true)
                {
                    matchesCat = cat.Contains("Хліб", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("соус", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Булоч", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Кетчуп", StringComparison.OrdinalIgnoreCase);
                }
                else if (CatGrainsRadio?.IsChecked == true)
                {
                    matchesCat = cat.Contains("Гарнір", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Овоч", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Круп", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Помідор", StringComparison.OrdinalIgnoreCase) ||
                                 cat.Contains("Банан", StringComparison.OrdinalIgnoreCase);
                }

                bool matchesSearch = string.IsNullOrEmpty(query) ||
                                     p.Name.ToLowerInvariant().Contains(query);

                return matchesCat && matchesSearch;
            }).ToList();

            FlyoutFoodListBox.ItemsSource = filtered;

            if (filtered.Count > 0)
            {
                FlyoutFoodListBox.SelectedIndex = 0;
            }
            else
            {
                _selectedFlyoutProduct = null;
                if (FlyoutLiveSummary != null)
                    FlyoutLiveSummary.Text = "Нічого не знайдено";
            }
        }

        private void FlyoutCategoryRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isCatalogLoaded) return;
            ApplyCatalogFilter();
        }

        private void FlyoutSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isCatalogLoaded || FlyoutSearchPlaceholder == null || FlyoutSearchBox == null) return;

            FlyoutSearchPlaceholder.Visibility = string.IsNullOrEmpty(FlyoutSearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;

            ApplyCatalogFilter();
        }

        private void FlyoutFoodListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isCatalogLoaded || FlyoutFoodListBox == null || FlyoutDrinkPresets == null ||
                FlyoutFoodPresets == null || FlyoutPortionUnitText == null ||
                FlyoutPortionLabel == null || FlyoutPortionBox == null) return;

            if (FlyoutFoodListBox.SelectedItem is FoodItem item)
            {
                _selectedFlyoutProduct = item;

                if (item.IsLiquid)
                {
                    FlyoutDrinkPresets.Visibility = Visibility.Visible;
                    FlyoutFoodPresets.Visibility = Visibility.Collapsed;
                    FlyoutPortionUnitText.Text = "мл";
                    FlyoutPortionLabel.Text = "Об'єм:";
                    FlyoutPortionBox.Text = item.DefaultPortion > 0 ? item.DefaultPortion.ToString("0") : "250";
                }
                else
                {
                    FlyoutDrinkPresets.Visibility = Visibility.Collapsed;
                    FlyoutFoodPresets.Visibility = Visibility.Visible;
                    FlyoutPortionUnitText.Text = "г";
                    FlyoutPortionLabel.Text = "Порція:";
                    FlyoutPortionBox.Text = item.DefaultPortion > 0 ? item.DefaultPortion.ToString("0") : "150";
                }

                RecalculateFlyoutPortion();
            }
        }

        private void FlyoutPortionChip_Click(object sender, RoutedEventArgs e)
        {
            if (!_isCatalogLoaded || FlyoutPortionBox == null) return;
            if (sender is Button btn && btn.Tag is string tagStr)
            {
                FlyoutPortionBox.Text = tagStr;
                FlyoutPortionBox.CaretIndex = FlyoutPortionBox.Text.Length;
            }
        }

        private void FlyoutPortionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isCatalogLoaded) return;
            RecalculateFlyoutPortion();
        }

        private void RecalculateFlyoutPortion()
        {
            if (!_isCatalogLoaded || _selectedFlyoutProduct == null || FlyoutLiveSummary == null || FlyoutPortionBox == null) return;

            string rawText = FlyoutPortionBox.Text.Replace(',', '.');
            if (double.TryParse(rawText, NumberStyles.Any, CultureInfo.InvariantCulture, out double amount) && amount > 0)
            {
                double ratio = amount / 100.0;
                double cal = _selectedFlyoutProduct.CaloriesPer100 * ratio;
                double prot = _selectedFlyoutProduct.ProteinPer100 * ratio;
                double fat = _selectedFlyoutProduct.FatPer100 * ratio;
                double carbs = _selectedFlyoutProduct.CarbsPer100 * ratio;

                FlyoutLiveSummary.Text = $"У цій порції: {cal:0} ккал | Б: {prot:0.#}г | Ж: {fat:0.#}г | В: {carbs:0.#}г";
            }
            else
            {
                FlyoutLiveSummary.Text = "Введіть розмір порції";
            }
        }

        private void FlyoutAddFood_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFlyoutProduct != null)
            {
                string rawText = FlyoutPortionBox.Text.Replace(',', '.');
                if (!double.TryParse(rawText, NumberStyles.Any, CultureInfo.InvariantCulture, out double amount) || amount <= 0)
                {
                    amount = _selectedFlyoutProduct.DefaultPortion;
                }

                double ratio = amount / 100.0;
                double cal = Math.Round(_selectedFlyoutProduct.CaloriesPer100 * ratio, 1);
                double prot = Math.Round(_selectedFlyoutProduct.ProteinPer100 * ratio, 1);
                double fat = Math.Round(_selectedFlyoutProduct.FatPer100 * ratio, 1);
                double carbs = Math.Round(_selectedFlyoutProduct.CarbsPer100 * ratio, 1);

                string unit = _selectedFlyoutProduct.IsLiquid ? "мл" : "г";
                var entry = new FoodLogEntry(_selectedFlyoutProduct.Name, amount, unit, cal, prot, fat, carbs);

                var today = _storageService.GetTodaySummary();
                today.Entries.Add(entry);
                _storageService.SaveTodaySummary(today);

                UpdateUi(animate: true);
                CloseCatalog();
            }
        }

        // ==============================================================
        // ДІЇ ВІДЖЕТА: UNDO, PIN, SETTINGS, TRAY
        // ==============================================================

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            var today = _storageService.GetTodaySummary();
            if (today.Entries.Count > 0)
            {
                today.Entries.RemoveAt(today.Entries.Count - 1);
                _storageService.SaveTodaySummary(today);
                UpdateUi(animate: true);
            }
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.IsTopMost = !_settings.IsTopMost;
            Topmost = _settings.IsTopMost;
            if (PinBtn != null) PinBtn.Opacity = _settings.IsTopMost ? 1.0 : 0.6;
            _storageService.SaveSettings(_settings);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settingsWnd = new SettingsWindow(_settings, _storageService, () =>
                {
                    UpdateThemeIcon(ThemeManager.CurrentTheme);
                    UpdateUi(animate: true);
                })
                {
                    Owner = this
                };
                settingsWnd.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка відкриття налаштувань:\n{ex.Message}", "CalWidget", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void HideButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCatalogOpen)
            {
                CloseCatalog();
            }
            Hide();
        }

        private void OnTrayLeftClicked()
        {
            Dispatcher.Invoke(() =>
            {
                if (IsVisible && WindowState != WindowState.Minimized)
                {
                    Hide();
                }
                else
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                }
            });
        }

        private void OnTrayRightClicked()
        {
            Dispatcher.Invoke(() =>
            {
                if (ContextMenu != null)
                {
                    ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                    ContextMenu.IsOpen = true;
                }
            });
        }

        private void OnHotkeyPressed()
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle);
                OpenCatalog();
            });
        }

        private void TrayShowHide_Click(object sender, RoutedEventArgs e)
        {
            OnTrayLeftClicked();
        }

        private void ResetToday_Click(object sender, RoutedEventArgs e)
        {
            var today = _storageService.GetTodaySummary();
            today.Entries.Clear();
            _storageService.SaveTodaySummary(today);
            UpdateUi(animate: true);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            _trayHelper.Dispose();
            _hotkeyHelper.Dispose();
            Application.Current.Shutdown();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveWindowPosition();
            _trayHelper.Dispose();
            _hotkeyHelper.Dispose();
        }

        // ==============================================================
        // РОЗУМНИЙ ГРАФІК ПРИЙОМІВ ЇЖІ (SMART MEAL TIMING & SCHEDULE)
        // ==============================================================

        private void UpdateMealScheduleUi()
        {
            if (NextMealHeadline == null || NextMealTarget == null) return;

            var overview = MealScheduleService.CalculateOverview(_settings, DateTime.Now);

            NextMealHeadline.Text = overview.Headline;
            NextMealTarget.Text = overview.TargetLine;

            if (overview.IsMealTimeNow)
            {
                MealIconEmoji.Text = "🍽️";
                var emeraldBrush = (Brush)FindResource("MacroEmeraldBrush");
                MealIconBadge.Background = emeraldBrush;
                MealScheduleCard.BorderBrush = emeraldBrush;
            }
            else
            {
                MealIconEmoji.Text = "🕒";
                MealIconBadge.Background = (Brush)FindResource("InputBgBrush");
                MealScheduleCard.BorderBrush = (Brush)FindResource("CardBorderBrush");
            }

            // Наповнюємо розгорнутий міні-таймлайн
            if (MealTimelineItemsContainer != null)
            {
                MealTimelineItemsContainer.Children.Clear();

                foreach (var meal in overview.Meals)
                {
                    var rowBorder = new Border
                    {
                        Margin = new Thickness(0, 1.5, 0, 1.5),
                        Padding = new Thickness(6, 4, 6, 4),
                        CornerRadius = new CornerRadius(6),
                        Background = meal.IsActiveNow 
                            ? (Brush)FindResource("HoverBgBrush") 
                            : (Brush)FindResource("InputBgBrush")
                    };

                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    // Час
                    var timeText = new TextBlock
                    {
                        Text = meal.TimeString,
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("TextMutedBrush"),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(timeText, 0);
                    grid.Children.Add(timeText);

                    // Назва прийому
                    var nameText = new TextBlock
                    {
                        Text = meal.Name,
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = meal.IsCompleted 
                            ? (Brush)FindResource("TextMutedBrush") 
                            : (Brush)FindResource("TextPrimaryBrush"),
                        TextDecorations = meal.IsCompleted ? TextDecorations.Strikethrough : null,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 0, 4, 0)
                    };
                    Grid.SetColumn(nameText, 1);
                    grid.Children.Add(nameText);

                    // Калорії та Білки
                    var targetText = new TextBlock
                    {
                        Text = $"{meal.CalorieTarget} ккал · {meal.ProteinTarget}г Б",
                        FontSize = 9.5,
                        Foreground = (Brush)FindResource("TextMutedBrush"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    };
                    Grid.SetColumn(targetText, 2);
                    grid.Children.Add(targetText);

                    // Кнопка-галочка (Checkbox)
                    var checkBtn = new Button
                    {
                        Tag = meal.Name,
                        Style = (Style)FindResource("MinimalIconButton"),
                        Padding = new Thickness(2),
                        ToolTip = meal.IsCompleted ? "Зняти позначку" : "Позначити прийом їжі як виконаний"
                    };
                    checkBtn.Click += MealCheckBtn_Click;

                    var checkBadge = new Border
                    {
                        Width = 18,
                        Height = 18,
                        CornerRadius = new CornerRadius(4),
                        BorderThickness = new Thickness(1),
                        BorderBrush = meal.IsCompleted 
                            ? (Brush)FindResource("MacroEmeraldBrush") 
                            : (Brush)FindResource("InputBorderBrush"),
                        Background = meal.IsCompleted 
                            ? (Brush)FindResource("MacroEmeraldBrush") 
                            : Brushes.Transparent
                    };
                    if (meal.IsCompleted)
                    {
                        checkBadge.Child = new TextBlock
                        {
                            Text = "✓",
                            FontSize = 10,
                            FontWeight = FontWeights.Bold,
                            Foreground = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                    }

                    checkBtn.Content = checkBadge;
                    Grid.SetColumn(checkBtn, 3);
                    grid.Children.Add(checkBtn);

                    rowBorder.Child = grid;
                    MealTimelineItemsContainer.Children.Add(rowBorder);
                }
            }
        }

        private void MealScheduleCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (MealTimelinePanel == null || MealTimelineArrow == null) return;

            bool isVisible = MealTimelinePanel.Visibility == Visibility.Visible;
            MealTimelinePanel.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            MealTimelineArrow.Data = Geometry.Parse(isVisible ? "M7,10L12,15L17,10H7Z" : "M7,14L12,9L17,14H7Z");
        }

        private void MealCheckBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string mealName)
            {
                if (_settings.CompletedMealNames.Contains(mealName))
                {
                    _settings.CompletedMealNames.Remove(mealName);
                }
                else
                {
                    _settings.CompletedMealNames.Add(mealName);
                }
                _storageService.SaveSettings(_settings);
                UpdateMealScheduleUi();
            }
        }

        private WriteableBitmap? _masterGlassBitmap;
        private WriteableBitmap? _fatsGlassBitmap;
        private WriteableBitmap? _proteinGlassBitmap;
        private WriteableBitmap? _carbsGlassBitmap;
        private double _virtualLeft = 0;
        private double _virtualTop = 0;
        private double _dpiScaleX = 1.0;
        private double _dpiScaleY = 1.0;
        private double _ringRelX = 52.0;
        private double _ringRelY = 46.0;
        private double _lastScreenLeft = double.NaN;
        private double _lastScreenTop = double.NaN;

        public void UpdateRingRelativeOffset()
        {
            if (RingGridContainer != null && RingGridContainer.IsVisible && PresentationSource.FromVisual(this) != null)
            {
                try
                {
                    GeneralTransform transform = RingGridContainer.TransformToAncestor(this);
                    Point p = transform.Transform(new Point(0, 0));
                    _ringRelX = p.X;
                    _ringRelY = p.Y;
                }
                catch { }
            }
        }

        /// <summary>
        /// Ініціалізує текстури рифленого скла високої роздільної здатності на основі активних шпалер Windows (Варіант 2).
        /// Генерація відбувається паралельно на всіх ядрах CPU (~20 мс), після чого позиціонування під час DragMove виконується миттєво через GPU ImageBrush.Viewbox (144+ FPS).
        /// </summary>
        public void InitWallpaperGlass()
        {
            if (_isUpdatingGlass) return;
            _isUpdatingGlass = true;

            try
            {
                _virtualLeft = SystemParameters.VirtualScreenLeft;
                _virtualTop = SystemParameters.VirtualScreenTop;
                int virtW = Math.Max(1920, (int)Math.Round(SystemParameters.VirtualScreenWidth));
                int virtH = Math.Max(1080, (int)Math.Round(SystemParameters.VirtualScreenHeight));

                byte[]? rawPixels = WallpaperHelper.LoadWallpaperPixels(virtW, virtH, out int w, out int h);
                if (rawPixels == null)
                {
                    rawPixels = new byte[virtW * virtH * 4];
                    byte bg = (byte)(ThemeManager.CurrentTheme == "Dark" ? 22 : 242);
                    for (int i = 0; i < rawPixels.Length; i += 4)
                    {
                        rawPixels[i + 0] = bg;
                        rawPixels[i + 1] = bg;
                        rawPixels[i + 2] = bg;
                        rawPixels[i + 3] = 255;
                    }
                }

                var baseOpts = new FlutedGlassOptions
                {
                    EnableFlutedGlass = _settings.EnableFlutedGlass,
                    GlassOpacity = _settings.GlassOpacity,
                    StripeWidth = _settings.RibWidth,
                    Distortion = _settings.RibDistortion,
                    Shadows = 0.40,
                    BlurRadius = _settings.BlurRadius,
                    Edges = 0.32,
                    IsDarkTheme = ThemeManager.CurrentTheme == "Dark"
                };

                // 2. Кільце Жирів (Amber)
                var fatOpts = new FlutedGlassOptions
                {
                    EnableFlutedGlass = baseOpts.EnableFlutedGlass,
                    GlassOpacity = 0.30,
                    StripeWidth = Math.Max(4, baseOpts.StripeWidth),
                    Distortion = baseOpts.Distortion,
                    Shadows = 0.45,
                    BlurRadius = baseOpts.BlurRadius,
                    Edges = baseOpts.Edges,
                    IsDarkTheme = baseOpts.IsDarkTheme,
                    TintR = 245f,
                    TintG = 158f,
                    TintB = 11f,
                    TintOpacity = 0.40f
                };

                // 3. Кільце Білка (Sky-blue)
                var protOpts = new FlutedGlassOptions
                {
                    EnableFlutedGlass = baseOpts.EnableFlutedGlass,
                    GlassOpacity = 0.30,
                    StripeWidth = Math.Max(4, baseOpts.StripeWidth),
                    Distortion = baseOpts.Distortion,
                    Shadows = 0.45,
                    BlurRadius = baseOpts.BlurRadius,
                    Edges = baseOpts.Edges,
                    IsDarkTheme = baseOpts.IsDarkTheme,
                    TintR = 59f,
                    TintG = 130f,
                    TintB = 246f,
                    TintOpacity = 0.40f
                };

                // 4. Кільце Вуглеводів (Coral-red)
                var carbsOpts = new FlutedGlassOptions
                {
                    EnableFlutedGlass = baseOpts.EnableFlutedGlass,
                    GlassOpacity = 0.30,
                    StripeWidth = Math.Max(4, baseOpts.StripeWidth),
                    Distortion = baseOpts.Distortion,
                    Shadows = 0.45,
                    BlurRadius = baseOpts.BlurRadius,
                    Edges = baseOpts.Edges,
                    IsDarkTheme = baseOpts.IsDarkTheme,
                    TintR = 239f,
                    TintG = 68f,
                    TintB = 68f,
                    TintOpacity = 0.40f
                };

                // Генерація всіх 4 текстур паралельно на всіх ядрах CPU (~20 мс)
                Parallel.Invoke(
                    () => _masterGlassBitmap = FlutedGlassGenerator.CreateWallpaperGlassBitmap(rawPixels, virtW, virtH, baseOpts),
                    () => _fatsGlassBitmap = FlutedGlassGenerator.CreateWallpaperGlassBitmap(rawPixels, virtW, virtH, fatOpts),
                    () => _proteinGlassBitmap = FlutedGlassGenerator.CreateWallpaperGlassBitmap(rawPixels, virtW, virtH, protOpts),
                    () => _carbsGlassBitmap = FlutedGlassGenerator.CreateWallpaperGlassBitmap(rawPixels, virtW, virtH, carbsOpts)
                );

                if (CascadeGlassBrush != null) CascadeGlassBrush.ImageSource = _masterGlassBitmap;
                if (RingFatsGlassBrush != null) RingFatsGlassBrush.ImageSource = _fatsGlassBitmap;
                if (RingProteinGlassBrush != null) RingProteinGlassBrush.ImageSource = _proteinGlassBitmap;
                if (RingCarbsGlassBrush != null) RingCarbsGlassBrush.ImageSource = _carbsGlassBitmap;

                UpdateRingRelativeOffset();
                UpdateGlassViewboxes();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"InitWallpaperGlass error: {ex.Message}");
            }
            finally
            {
                _isUpdatingGlass = false;
            }
        }

        /// <summary>
        /// Оновлює Viewbox прямокутники семплінгу шпалер на GPU.
        /// </summary>
        public void UpdateGlassViewboxes()
        {
            if (!IsLoaded || Visibility != Visibility.Visible || PresentationSource.FromVisual(this) == null) return;

            try
            {
                Point tl = PointToScreen(new Point(0, 0));
                Point br = PointToScreen(new Point(ActualWidth, ActualHeight));
                UpdateGlassViewboxesDirect(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateGlassViewboxes error: {ex.Message}");
            }
        }

        /// <summary>
        /// Миттєво оновлює прямокутники GPU Viewbox за абсолютними піксельними координатами екрана (0.001 мс, 144+ FPS).
        /// </summary>
        private void UpdateGlassViewboxesDirect(double screenLeft, double screenTop, double screenW, double screenH)
        {
            try
            {
                if (Math.Abs(screenLeft - _lastScreenLeft) < 0.25 && Math.Abs(screenTop - _lastScreenTop) < 0.25)
                {
                    return;
                }
                _lastScreenLeft = screenLeft;
                _lastScreenTop = screenTop;

                double screenX = screenLeft - _virtualLeft;
                double screenY = screenTop - _virtualTop;

                if (CascadeGlassBrush != null)
                {
                    CascadeGlassBrush.Viewbox = new Rect(screenX, screenY, Math.Max(10, screenW), Math.Max(10, screenH));
                }

                if (RingGridContainer != null && RingGridContainer.IsVisible)
                {
                    double dpi = screenW / Math.Max(1, ActualWidth);
                    double ringScreenX = screenX + (_ringRelX * dpi);
                    double ringScreenY = screenY + (_ringRelY * dpi);

                    if (RingFatsGlassBrush != null)
                    {
                        RingFatsGlassBrush.Viewbox = new Rect(ringScreenX + (11.0 * dpi), ringScreenY + (11.0 * dpi), 174.0 * dpi, 174.0 * dpi);
                    }
                    if (RingProteinGlassBrush != null)
                    {
                        RingProteinGlassBrush.Viewbox = new Rect(ringScreenX + (24.0 * dpi), ringScreenY + (24.0 * dpi), 148.0 * dpi, 148.0 * dpi);
                    }
                    if (RingCarbsGlassBrush != null)
                    {
                        RingCarbsGlassBrush.Viewbox = new Rect(ringScreenX + (37.0 * dpi), ringScreenY + (37.0 * dpi), 122.0 * dpi, 122.0 * dpi);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateGlassViewboxesDirect error: {ex.Message}");
            }
        }

        public void UpdateCascadeGlass(bool force = false)
        {
            InitWallpaperGlass();
        }
    }
}
