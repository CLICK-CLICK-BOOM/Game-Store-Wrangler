//<summary>[DO NOT REMOVE]ThemeManager.cs</summary>

using System.Drawing;
using System.Windows.Forms;

namespace GSWEngine
{
    public class ThemeColors
    {
        public Color Tick { get; set; }
        public Color Warning { get; set; }
        public Color MainBg { get; set; }
        public Color TitleFg { get; set; }
        public Color TitleBarColor { get; set; }
        public Color TitleBarColorInactive { get; set; }
        public Color StoreColFg { get; set; }
        public Color GridFg { get; set; }
        public Color ActionTextFg { get; set; }
        public Color KeepOn { get; set; }
        public Color KeepOff { get; set; }
        public Color LogFg { get; set; }
        public Color AppStatusFg { get; set; }
        public Color GeneralBtnBg { get; set; }
        public Color GeneralBtnFg { get; set; }
        public Color GeneralBtnHover { get; set; }
        public Color GeneralBtnBorder { get; set; }
        public Color OnTopActiveBg { get; set; }
        public Color ExitBtnBg { get; set; }
        public Color ExitBtnFg { get; set; }
        public Color ExitBtnBorder { get; set; }
        public Color RequestorBg { get; set; }
        public Color RequestorHeaderFg { get; set; }
        public Color RequestorListFg { get; set; }
        public Color RequestorHighlightBg { get; set; }
        public Color RequestorHighlightFg { get; set; }
        public string BgFile { get; set; } = string.Empty;
    }

    public static class ThemeManager
    {
        public static ThemeColors GetTheme(int index)
        {
            return index switch
            {
                1 => new ThemeColors
                { // ALARM - THE GOLD STANDARD
                    Tick = Color.FromArgb(255, 70, 0),
                    Warning = Color.FromArgb(109, 63, 69),
                    MainBg = Color.FromArgb(0, 0, 0),
                    TitleFg = Color.FromArgb(255, 222, 000),
                    TitleBarColor = Color.FromArgb(43, 0, 0),
                    TitleBarColorInactive = Color.FromArgb(8, 0, 0),
                    StoreColFg = Color.FromArgb(255, 255, 255),
                    GridFg = Color.FromArgb(220, 185, 0),
                    ActionTextFg = Color.FromArgb(0, 150, 0),
                    KeepOn = Color.FromArgb(255, 224, 0),
                    KeepOff = Color.FromArgb(82, 82, 0),
                    LogFg = Color.FromArgb(0, 113, 0),
                    AppStatusFg = Color.FromArgb(255, 215, 0),
                    GeneralBtnBg = Color.FromArgb(30, 30, 30),
                    GeneralBtnFg = Color.FromArgb(255, 255, 255),
                    GeneralBtnHover = Color.FromArgb(121, 0, 0),
                    GeneralBtnBorder = Color.FromArgb(255, 0, 0),
                    OnTopActiveBg = Color.FromArgb(59, 0, 0),
                    ExitBtnBg = Color.FromArgb(60, 0, 0),
                    ExitBtnFg = Color.FromArgb(255, 255, 255),
                    ExitBtnBorder = Color.FromArgb(112, 0, 0),
                    RequestorBg = Color.FromArgb(0, 0, 0),
                    RequestorHeaderFg = Color.FromArgb(255, 0, 0),
                    RequestorListFg = Color.FromArgb(255, 255, 255),
                    RequestorHighlightBg = Color.FromArgb(63, 0, 0),
                    RequestorHighlightFg = Color.FromArgb(255, 255, 255),
                    BgFile = "Alarm.jpg"
                },
                2 => new ThemeColors
                { // CYBER
                    Tick = Color.FromArgb(116, 218, 135),
                    Warning = Color.FromArgb(109, 0, 114),
                    MainBg = Color.FromArgb(10, 10, 15),
                    TitleFg = Color.FromArgb(132, 236, 255),
                    TitleBarColor = Color.FromArgb(40, 0, 40),
                    TitleBarColorInactive = Color.FromArgb(20, 0, 20),
                    StoreColFg = Color.FromArgb(0, 255, 255),
                    GridFg = Color.FromArgb(0, 180, 180),
                    ActionTextFg = Color.FromArgb(200, 80, 150),
                    KeepOn = Color.FromArgb(0, 255, 255),
                    KeepOff = Color.FromArgb(20, 80, 82),
                    LogFg = Color.FromArgb(255, 0, 255),
                    AppStatusFg = Color.FromArgb(76, 255, 255),
                    GeneralBtnBg = Color.FromArgb(20, 20, 40),
                    GeneralBtnFg = Color.FromArgb(255, 0, 255),
                    GeneralBtnHover = Color.FromArgb(40, 40, 87),
                    GeneralBtnBorder = Color.FromArgb(0, 255, 255),
                    OnTopActiveBg = Color.FromArgb(61, 0, 0),
                    ExitBtnBg = Color.FromArgb(40, 0, 40),
                    ExitBtnFg = Color.FromArgb(0, 255, 255),
                    ExitBtnBorder = Color.FromArgb(255, 0, 255),
                    RequestorBg = Color.FromArgb(10, 10, 20),
                    RequestorHeaderFg = Color.FromArgb(255, 0, 255),
                    RequestorListFg = Color.FromArgb(0, 255, 255),
                    RequestorHighlightBg = Color.FromArgb(0, 255, 255),
                    RequestorHighlightFg = Color.FromArgb(0, 0, 0),
                    BgFile = "Cyber.jpg"
                },
                3 => new ThemeColors
                { // DEEP
                    Tick = Color.FromArgb(0, 255, 255),
                    Warning = Color.FromArgb(0, 141, 133),
                    MainBg = Color.FromArgb(0, 32, 15),
                    TitleFg = Color.FromArgb(255, 222, 000),
                    TitleBarColor = Color.FromArgb(0, 40, 40),
                    TitleBarColorInactive = Color.FromArgb(0, 20, 20),
                    StoreColFg = Color.FromArgb(0, 159, 155),
                    GridFg = Color.FromArgb(0, 200, 120),
                    ActionTextFg = Color.FromArgb(180, 180, 0),
                    KeepOn = Color.FromArgb(0, 255, 127),
                    KeepOff = Color.FromArgb(0, 40, 40),
                    LogFg = Color.FromArgb(0, 255, 127),
                    AppStatusFg = Color.FromArgb(0, 255, 127),
                    GeneralBtnBg = Color.FromArgb(0, 39, 40),
                    GeneralBtnFg = Color.FromArgb(48, 214, 180),
                    GeneralBtnHover = Color.FromArgb(0, 80, 80),
                    GeneralBtnBorder = Color.FromArgb(0, 128, 128),
                    OnTopActiveBg = Color.FromArgb(104, 16, 0),
                    ExitBtnBg = Color.FromArgb(0, 76, 0),
                    ExitBtnFg = Color.FromArgb(255, 255, 255),
                    ExitBtnBorder = Color.FromArgb(0, 255, 127),
                    RequestorBg = Color.FromArgb(0, 15, 15),
                    RequestorHeaderFg = Color.FromArgb(0, 255, 255),
                    RequestorListFg = Color.FromArgb(0, 255, 127),
                    RequestorHighlightBg = Color.FromArgb(0, 128, 128),
                    RequestorHighlightFg = Color.FromArgb(255, 255, 255),
                    BgFile = "Deep.jpg"
                },
                4 => new ThemeColors
                { // ARCTIC
                    Tick = Color.FromArgb(205, 206, 96),
                    Warning = Color.FromArgb(0, 132, 137),
                    MainBg = Color.FromArgb(0, 55, 56),
                    TitleFg = Color.FromArgb(255, 255, 255),
                    TitleBarColor = Color.FromArgb(56, 123, 127),
                    TitleBarColorInactive = Color.FromArgb(0, 57, 59),
                    StoreColFg = Color.FromArgb(255, 255, 255),
                    GridFg = Color.FromArgb(67, 255, 255),
                    ActionTextFg = Color.FromArgb(0, 196, 255),
                    KeepOn = Color.FromArgb(30, 233, 255),
                    KeepOff = Color.FromArgb(100, 120, 140),
                    LogFg = Color.FromArgb(30, 213, 255),
                    AppStatusFg = Color.FromArgb(0, 233, 216),
                    GeneralBtnBg = Color.FromArgb(40, 70, 90),
                    GeneralBtnFg = Color.FromArgb(200, 230, 255),
                    GeneralBtnHover = Color.FromArgb(60, 90, 120),
                    GeneralBtnBorder = Color.FromArgb(150, 200, 255),
                    OnTopActiveBg = Color.FromArgb(107, 0, 0),
                    ExitBtnBg = Color.FromArgb(25, 25, 112),
                    ExitBtnFg = Color.FromArgb(255, 255, 255),
                    ExitBtnBorder = Color.FromArgb(224, 255, 255),
                    RequestorBg = Color.FromArgb(0, 60, 82),
                    RequestorHeaderFg = Color.FromArgb(25, 255, 255),
                    RequestorListFg = Color.FromArgb(0, 142, 146),
                    RequestorHighlightBg = Color.FromArgb(30, 144, 255),
                    RequestorHighlightFg = Color.FromArgb(255, 255, 255),
                    BgFile = "Arctic.jpg"
                },
                _ => new ThemeColors
                { // CUBIC
                    Tick = Color.FromArgb(177, 157, 0),
                    Warning = Color.FromArgb(38, 74, 106),
                    MainBg = Color.FromArgb(0, 0, 0),
                    TitleFg = Color.FromArgb(255, 255, 255),
                    TitleBarColor = Color.FromArgb(60, 60, 60),
                    TitleBarColorInactive = Color.FromArgb(30, 30, 30),
                    StoreColFg = Color.FromArgb(255, 255, 255),
                    GridFg = Color.FromArgb(144, 196, 255),
                    ActionTextFg = Color.FromArgb(0, 210, 212),
                    KeepOn = Color.FromArgb(200, 200, 200),
                    KeepOff = Color.FromArgb(97, 100, 101),
                    LogFg = Color.FromArgb(63, 154, 66),
                    AppStatusFg = Color.FromArgb(159, 161, 158),
                    GeneralBtnBg = Color.FromArgb(40, 40, 40),
                    GeneralBtnFg = Color.FromArgb(255, 255, 255),
                    GeneralBtnHover = Color.FromArgb(60, 60, 59),
                    GeneralBtnBorder = Color.FromArgb(100, 100, 100),
                    OnTopActiveBg = Color.FromArgb(175, 80, 82),
                    ExitBtnBg = Color.FromArgb(0, 0, 0),
                    ExitBtnFg = Color.FromArgb(160, 160, 160),
                    ExitBtnBorder = Color.FromArgb(80, 80, 80),
                    RequestorBg = Color.FromArgb(20, 20, 20),
                    RequestorHeaderFg = Color.FromArgb(160, 160, 160),
                    RequestorListFg = Color.FromArgb(255, 255, 255),
                    RequestorHighlightBg = Color.FromArgb(80, 80, 80),
                    RequestorHighlightFg = Color.FromArgb(255, 255, 255),
                    BgFile = "Cubic.jpg"
                }
            };
        }

        public static void StripFocus(Control btn)
        {
            btn.TabStop = false;
            btn.GotFocus += (s, e) =>
            {
                if (btn.FindForm() is Form f) f.ActiveControl = null;
            };
        }
    }
}