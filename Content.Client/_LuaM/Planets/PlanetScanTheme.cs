using System.Numerics;
using Robust.Client.Graphics;

// -- LuaM: минимальная замена темы приборов и подписей Wolfgate (CombatConsole, AsteroidMining) для сканера планет; модули целиком не переносим
namespace Content.Client._WG.CombatConsole
{
    public sealed class WGPlanetScanSkin
    {
        public Color Accent = Color.FromHex("#46D7FF");
        public Color Text = Color.FromHex("#E6EDF3");
        public Color TextMuted = Color.FromHex("#8A9BAB");
        public Color Ink = Color.FromHex("#0A0E13");
        public Color Glass = Color.FromHex("#0E141B");
        public Color EdgeSoft = Color.FromHex("#23303E");
        public Color Good = Color.FromHex("#3CE39B");
        public Color Caution = Color.FromHex("#FFB547");
        public Color Danger = Color.FromHex("#FF4D5E");
    }

    public static class WGConsoleDigital
    {
        public static void Panel(DrawingHandleScreen handle, UIBox2 box, float scale, Color fill, Color edge,
            bool brackets = true, float corner = 8)
        {
            handle.DrawRect(box, fill);
            handle.DrawRect(box, edge, false);
        }
    }

    public static class WGInstrumentGlass
    {
        public static void Window(DrawingHandleScreen handle, UIBox2 box, float scale)
        {
        }
    }

    public static class WGInstrumentTheme
    {
        public static readonly WGPlanetScanSkin Skin = new();

        public static bool Digital => true;
    }
}

namespace Content.Client._WG.AsteroidMining
{
    public static class WGMiningGauges
    {
        public static void Brackets(DrawingHandleScreen handle, Vector2 center, float half, float rotation, Color color, float scale)
        {
            var arm = MathF.Max(4f * scale, half * 0.45f);
            var cos = MathF.Cos(rotation);
            var sin = MathF.Sin(rotation);
            for (var corner = 0; corner < 4; corner++)
            {
                var sx = corner is 0 or 3 ? -1f : 1f;
                var sy = corner < 2 ? -1f : 1f;
                for (var inset = 0f; inset < 2f; inset++)
                {
                    var tip = new Vector2(sx * (half - inset * scale), sy * (half - inset * scale));
                    Line(tip, tip - new Vector2(sx * arm, 0f));
                    Line(tip, tip - new Vector2(0f, sy * arm));
                }
            }

            void Line(Vector2 from, Vector2 to)
            {
                handle.DrawLine(center + Turn(from), center + Turn(to), color);
            }

            Vector2 Turn(Vector2 point)
            {
                return new Vector2(point.X * cos - point.Y * sin, point.X * sin + point.Y * cos);
            }
        }

        public static void TextLeft(DrawingHandleScreen handle, Font font, string text, Vector2 topLeft, float width, float scale,
            Color color)
        {
            var fitted = FitScale(handle, font, text, scale, width);
            if (fitted > 0)
                handle.DrawString(font, topLeft, text, fitted, color);
        }

        public static float TextRight(DrawingHandleScreen handle, Font font, string text, float right, float top, float width,
            float scale, Color color)
        {
            var fitted = FitScale(handle, font, text, scale, width);
            if (fitted <= 0)
                return 0f;
            var drawn = handle.GetDimensions(font, text, fitted).X;
            handle.DrawString(font, new Vector2(right - drawn, top), text, fitted, color);
            return drawn;
        }

        private static float FitScale(DrawingHandleScreen handle, Font font, string text, float maximum, float width)
        {
            var scale = Step(maximum);
            if (scale == 0 || width <= 0)
                return 0;
            var measured = handle.GetDimensions(font, text, scale).X;
            scale = Step(scale * MathF.Min(1, width / MathF.Max(1, measured)));
            while (scale > 0 && handle.GetDimensions(font, text, scale).X > width)
                scale = Step(scale - 0.125f);
            return scale;
        }

        private static float Step(float scale)
        {
            return float.IsFinite(scale) && scale >= 0.125f ? MathF.Floor(MathF.Min(scale, 4) * 8) / 8 : 0;
        }
    }
}
