using Microsoft.Win32;
using System;
using System.Drawing;
using Autodesk.AutoCAD.Windows;

namespace PartManager.UI
{
    public class PaletteSettings
    {
        private const string REGISTRY_KEY = @"Software\PartManager\PaletteSettings";

        public DockSides DockPosition { get; set; }
        public Point Location { get; set; }
        public Size FloatingSize { get; set; }
        public Size DockedSize { get; set; }
        public bool Visible { get; set; }

        public static PaletteSettings Load()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_KEY))
                {
                    if (key == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[Settings] Registry 키 없음 - 기본값 사용");
                        return GetDefaultSettings();
                    }

                    int dockValue = (int)key.GetValue("DockPosition", 1);
                    if (dockValue == 0) dockValue = 1;
                    DockSides dockPosition = ParseDockPosition(dockValue);

                    var settings = new PaletteSettings
                    {
                        DockPosition = dockPosition,
                        Location = new Point(
                            (int)key.GetValue("LocationX", 100),
                            (int)key.GetValue("LocationY", 100)
                        ),
                        FloatingSize = new Size(
                            (int)key.GetValue("FloatingSizeWidth", 320),
                            (int)key.GetValue("FloatingSizeHeight", 500)
                        ),
                        DockedSize = new Size(
                            (int)key.GetValue("DockedSizeWidth", 320),
                            (int)key.GetValue("DockedSizeHeight", 500)
                        ),
                        Visible = (int)key.GetValue("Visible", 1) == 1
                    };

                    settings.FloatingSize = ValidateSize(settings.FloatingSize);
                    settings.DockedSize = ValidateSize(settings.DockedSize);
                    settings.Location = ValidateLocation(settings.Location, settings.FloatingSize);

                    return settings;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] 로드 실패: {ex.Message}");
                return GetDefaultSettings();
            }
        }

        private static DockSides ParseDockPosition(int value)
        {
            if (value > 15 || value < 0)
            {
                return DockSides.None;
            }

            switch (value)
            {
                case 0: return DockSides.None;
                case 1: return DockSides.Left;
                case 2: return DockSides.Right;
                case 4: return DockSides.Top;
                case 8: return DockSides.Bottom;
                default: return DockSides.None;
            }
        }

        public void Save()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_KEY))
                {
                    if (key == null)
                        return;

                    if (DockPosition == DockSides.None)
                        DockPosition = DockSides.Left;

                    int dockValue = GetDockPositionValue(DockPosition);
                    key.SetValue("DockPosition", dockValue, RegistryValueKind.DWord);

                    key.SetValue("LocationX", Location.X, RegistryValueKind.DWord);
                    key.SetValue("LocationY", Location.Y, RegistryValueKind.DWord);

                    key.SetValue("FloatingSizeWidth", FloatingSize.Width, RegistryValueKind.DWord);
                    key.SetValue("FloatingSizeHeight", FloatingSize.Height, RegistryValueKind.DWord);

                    key.SetValue("DockedSizeWidth", DockedSize.Width, RegistryValueKind.DWord);
                    key.SetValue("DockedSizeHeight", DockedSize.Height, RegistryValueKind.DWord);

                    key.SetValue("Visible", Visible ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] 저장 실패: {ex.Message}");
            }
        }

        private static int GetDockPositionValue(DockSides dock)
        {
            switch (dock)
            {
                case DockSides.None: return 0;
                case DockSides.Left: return 1;
                case DockSides.Right: return 2;
                case DockSides.Top: return 4;
                case DockSides.Bottom: return 8;
                default: return 0;
            }
        }

        private static Size ValidateSize(Size size)
        {
            const int defaultWidth = 320;
            const int defaultHeight = 500;
            const int minWidth = 250;
            const int minHeight = 400;
            const int maxWidth = 500;
            const int maxHeight = 700;

            int width = size.Width;
            int height = size.Height;

            if (width < minWidth || width > maxWidth)
                width = defaultWidth;

            if (height < minHeight || height > maxHeight)
                height = defaultHeight;

            return new Size(width, height);
        }

        private static Point ValidateLocation(Point location, Size size)
        {
            try
            {
                var allScreens = System.Windows.Forms.Screen.AllScreens;
                bool isOnScreen = false;

                foreach (var screen in allScreens)
                {
                    var bounds = screen.WorkingArea;

                    if (location.X >= bounds.Left &&
                        location.X + size.Width <= bounds.Right &&
                        location.Y >= bounds.Top &&
                        location.Y + size.Height <= bounds.Bottom)
                    {
                        isOnScreen = true;
                        break;
                    }
                }

                if (!isOnScreen)
                {
                    var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                    var workingArea = primaryScreen.WorkingArea;

                    int x = workingArea.Left + (workingArea.Width - size.Width) / 2;
                    int y = workingArea.Top + (workingArea.Height - size.Height) / 2;

                    return new Point(x, y);
                }

                return location;
            }
            catch
            {
                return new Point(100, 100);
            }
        }

        private static PaletteSettings GetDefaultSettings()
        {
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
            var workingArea = primaryScreen.WorkingArea;

            const int defaultWidth = 320;
            const int defaultHeight = 500;

            int centerX = workingArea.Left + (workingArea.Width - defaultWidth) / 2;
            int centerY = workingArea.Top + (workingArea.Height - defaultHeight) / 2;

            return new PaletteSettings
            {
                DockPosition = DockSides.Left, // None
                Location = new Point(centerX, centerY),
                FloatingSize = new Size(defaultWidth, defaultHeight),
                DockedSize = new Size(defaultWidth, defaultHeight),
                Visible = true
            };
        }

        public static void Reset()
        {
            try
            {
                Registry.CurrentUser.DeleteSubKey(REGISTRY_KEY, false);
            }
            catch { }
        }
    }
}
