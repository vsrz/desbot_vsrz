using System;
namespace desBot
{
    /// <summary>
    /// IRC colors used with control characters
    /// </summary>
    public enum IrcColor
    {
        White,
        Black,
        Navy,   //dark blue
        Green,
        Red,
        Brown,
        Purple,
        Orange,
        Yellow,
        Lime,   //light green
        Teal,   //blue/green mix
        Cyan,   //light blue
        Blue,   //slightly less dark blue then Navy
        Pink,
        DarkGrey,
        LightGrey,
        Transparent = 99,
    }

    /// <summary>
    /// Utilities for creating IRC control characters for text colors etc
    /// </summary>
    public static class ControlCharacter
    {
        /// <summary>
        /// If set to false, these functions won't generate new control characters
        /// </summary>
        public static bool Enabled
        { 
            get 
            { 
                return State.ControlCharacters.Value;
            }
        }

        /// <summary>
        /// Control character for restoring colors to previous
        /// </summary>
        /// <returns>A string representing ^C</returns>
        public static string ColorRestore()
        {
            if (!Enabled) return "";
            return new string((char)3, 1);
        }

        /// <summary>
        /// Control character for setting the foreground color
        /// </summary>
        /// <param name="foreground">The color to set as foreground color</param>
        /// <returns>A string representing ^CM, where M is a color ID</returns>
        public static string Color(IrcColor foreground)
        {
            if (!Enabled) return "";
            return ColorRestore() + ((int)foreground).ToString();
        }

        /// <summary>
        /// Control character for settings the foreground and background color
        /// </summary>
        /// <param name="foreground">The color to set as foreground color</param>
        /// <param name="background">The color to set as background color</param>
        /// <returns>A string representing ^CM,N where M and N are color IDs</returns>
        public static string Color(IrcColor foreground, IrcColor background)
        {
            if (!Enabled) return "";
            return Color(foreground) + "," + ((int)background).ToString();
        }

        /// <summary>
        /// Control character for swapping foreground and background color
        /// </summary>
        /// <returns>A string representing ^R</returns>
        public static string ColorReverse()
        {
            if (!Enabled) return "";
            return new string((char)22, 1);
        }

        /// <summary>
        /// Control character for toggling bold
        /// </summary>
        /// <returns>A string representing ^B</returns>
        public static string Bold()
        {
            if (!Enabled) return "";
            return new string((char)2, 1);
        }

        /// <summary>
        /// Control character for toggling underline
        /// </summary>
        /// <returns>A string representing ^U</returns>
        public static string Underline()
        {
            if (!Enabled) return "";
            return new string((char)31, 1);
        }

        /// <summary>
        /// Control character for restoring to default
        /// </summary>
        /// <returns>A string respresenting ^O</returns>
        public static string Restore()
        {
            if (!Enabled) return "";
            return new string((char)15, 1);
        }

        /// <summary>
        /// Strips control characters from a string
        /// </summary>
        /// <param name="text">The text to strip of control characters</param>
        /// <returns>A string containing no control characters</returns>
        public static string Strip(string text)
        {
            string result = "";
            int colorstrip = 0;
            foreach (char c in text)
            {
                if (c == 3)
                {
                    colorstrip = 1;
                }
                else if (c == 2 || c == 31 || c == 22 || c == 15)
                {
                    continue;
                }
                else if (colorstrip != 0)
                {
                    if (char.IsDigit(c))
                    {
                        continue;
                    }
                    else if (c == ',' && colorstrip == 1)
                    {
                        colorstrip = 2;
                    }
                    else
                    {
                        colorstrip = 0;
                        result += c;
                    }
                }
                else result += c;
            }
            return result;
        }

        /// <summary>
        /// Escapes control characters in the specified string, so it can be serialized
        /// </summary>
        /// <param name="text">The string to escape</param>
        /// <returns>An escaped string</returns>
        public static string Serialize(string text)
        {
            string result = "";
            foreach (char c in text)
            {
                if (c == 2)
                {
                    result += "^B";
                }
                else if (c == 3)
                {
                    result += "^C";
                }
                else if (c == 15)
                {
                    result += "^O";
                }
                else if (c == 22)
                {
                    result += "^R";
                }
                else if (c == 31)
                {
                    result += "^U";
                }
                else if (c == '^')
                {
                    result += "^^";
                }
                else
                {
                    result += c;
                }
            }
            return result;
        }

        /// <summary>
        /// Un-escapes control characters in the specified string
        /// </summary>
        /// <param name="text">The string that was previously escaped</param>
        /// <returns>The original string</returns>
        public static string Deserialize(string text)
        {
            string result = "";
            bool flag = false;
            foreach (char c in text)
            {
                if (flag)
                {
                    switch (c)
                    {
                        case 'B':
                            result += (char)2;
                            break;
                        case 'C':
                            result += (char)3;
                            break;
                        case 'O':
                            result += (char)15;
                            break;
                        case 'R':
                            result += (char)22;
                            break;
                        case 'U':
                            result += (char)31;
                            break;
                        case '^':
                            result += "^";
                            break;
                    }
                    flag = false;
                }
                else if (c == '^') flag = true;
                else result += c;
            }
            return result;
        }
    }
}