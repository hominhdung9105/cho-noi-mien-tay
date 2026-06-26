using UnityEngine;

namespace ChoNoi.UI
{
    public static class FontHelper
    {
        private static Font gameFont;
        private static Font gameBoldFont;

        public static Font GameFont
        {
            get
            {
                if (gameFont == null)
                {
                    gameFont = Resources.Load<Font>("Fonts/Baloo-Regular");
                    if (gameFont == null)
                    {
                        gameFont = Resources.Load<Font>("Fonts/Nunito-Regular");
                    }
                    if (gameFont == null)
                    {
                        gameFont = Resources.Load<Font>("Fonts/Comfortaa-Regular");
                    }
                    if (gameFont == null)
                    {
                        gameFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    }
                }
                return gameFont;
            }
        }

        public static Font GameBoldFont
        {
            get
            {
                if (gameBoldFont == null)
                {
                    gameBoldFont = Resources.Load<Font>("Fonts/Baloo-Regular");
                    if (gameBoldFont == null)
                    {
                        gameBoldFont = Resources.Load<Font>("Fonts/Nunito-Bold");
                    }
                    if (gameBoldFont == null)
                    {
                        gameBoldFont = Resources.Load<Font>("Fonts/Comfortaa-Bold");
                    }
                    if (gameBoldFont == null)
                    {
                        gameBoldFont = GameFont;
                    }
                }
                return gameBoldFont;
            }
        }
    }
}
