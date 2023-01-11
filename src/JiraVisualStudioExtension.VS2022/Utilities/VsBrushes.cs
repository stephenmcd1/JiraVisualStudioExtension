using Microsoft.VisualStudio.Shell;

namespace JiraVisualStudioExtension.Utilities
{
    /// <summary>
    /// Helper class for re-using some standard Brushes provided by Visual Studio.  These are defined in 
    /// different assemblies between VS 2015 and VS 2022 so referencing them in XAML is difficult.
    /// </summary>
    internal static class Brushes
    {
        public static object CommandBarSelectedKey = VsBrushes.CommandBarSelectedKey;
        public static object CommandBarSelectedBorderKey = VsBrushes.CommandBarSelectedBorderKey;
        
        public static object CommandBarHoverKey = VsBrushes.CommandBarHoverKey;
        public static object CommandBarHoverOverSelectedKey = VsBrushes.CommandBarHoverOverSelectedKey;
        
        public static object ToolWindowTextKey = VsBrushes.ToolWindowTextKey;
        public static object ToolWindowButtonDownKey = VsBrushes.ToolWindowButtonDownKey;
        public static object ToolWindowButtonDownBorderKey = VsBrushes.ToolWindowButtonDownBorderKey;
        public static object ToolWindowButtonDownActiveGlyphKey = VsBrushes.ToolWindowButtonDownActiveGlyphKey;
    }
}
