using System;
using Telegram.Bot.Types.InlineQueryResults;

namespace c_sharp_filb_bot
{
    //:hexe:|135|12|7|gif
    //name|index|width|height|extension
    class FilbEmoticonData
    {
        public FilbEmoticonData(string name, int index, int width, int height, string extension)
        {
            Name = name;
            Index = index;
            Width = width;
            Height = height;
            Extension = extension;
        }

        public string Name { get; }
        public int Index { get; }
        public int Width { get; }
        public int Height { get; }
        public string Extension { get; }

        public string ImageFileName => $"{Index}.{Extension}";
        public string FileHost => $"http://filbemoticonbot.bplaced.net/img/{ImageFileName}";
        public string ThumbHost => $"http://filbemoticonbot.bplaced.net/img/thumb/{ImageFileName}";

        public static FilbEmoticonData FromString(string text)
        {
            var tokens = text.Split('|');
            string name = tokens[0];
            int index = int.Parse(tokens[1]);
            int width = int.Parse(tokens[2]);
            int height = int.Parse(tokens[3]);
            string extension = tokens[4];
            return new FilbEmoticonData(name, index, width, height, extension);
        }

        internal int GetScore(string text)
        {
            var missingLetterCost = 5;
            var overheadLetterCost = 10;
            var currentScore = 0;
            var lowest = int.MaxValue;
            var maxLength = Math.Max(text.Length, Name.Length);
            //No offset.
            for (int i = 0; i < maxLength; i++)
            {
                var textChar =(char)0;
                if(i < text.Length)
                    textChar = text[i];
                var nameChar = (char)0;
                //We're still in both ranges.
                if(i< Name.Length && i < text.Length)
                {
                    nameChar = Name[i];
                    currentScore += 2 * Math.Abs(textChar - nameChar);
                }
                //Our text is shorter than the name
                else if(i < Name.Length)
                    currentScore += missingLetterCost;
                else
                    currentScore += overheadLetterCost;
            }
            lowest = currentScore;
            currentScore = 0;
            if(text.Length <= Name.Length)
            {
                //Right aligned
                var start = Name.Length - text.Length;
                for (int i = 0; i < Name.Length; i++)
                {
                    var nameChar = Name[i];
                    var textChar =(char)0;
                    if(i - start > 0)
                    {
                        textChar = text[i - start];
                        currentScore += 2 * Math.Abs(textChar - nameChar);
                    }
                    else
                        currentScore += missingLetterCost;
                }
                lowest = Math.Min(currentScore, lowest);
                currentScore = 0;
            }
            if(text.Length - 1 <= Name.Length)
            {
                //Left-aligned + 1
                for (int i = 0; i < Name.Length; i++)
                {
                    var nameChar = Name[i];
                    var textChar =(char)0;
                    if(i > 0 && i - 1 < text.Length)
                    {
                        textChar = text[i - 1];
                        currentScore += 2 * Math.Abs(textChar - nameChar);
                    }
                    else
                        currentScore += missingLetterCost;
                }
                lowest = Math.Min(currentScore, lowest);
                currentScore = 0;
                //Right-aligned - 1
                var start = Name.Length - text.Length;
                for (int i = 0; i < Name.Length; i++)
                {
                    var nameChar = Name[i];
                    var textChar =(char)0;
                    if(i + 1 - start > 0 && i + 1 - start < text.Length)
                    {
                        textChar = text[i + 1 - start];
                        currentScore += 2 * Math.Abs(textChar - nameChar);
                    }
                    else
                        currentScore += missingLetterCost;
                }
                lowest = Math.Min(currentScore, lowest);
            
            }
            return lowest;
        }

        internal InlineQueryResultBase GenerateQueryResult()
        {
            var result = new InlineQueryResultGif(Index.ToString(), FileHost, ThumbHost);
            result.GifHeight = Height;
            result.GifWidth = Width;
            if(Extension == "png")
            {
                var itmc = new InputTextMessageContent($"<a href=\"{FileHost}\">.</a>");
                itmc.ParseMode = Telegram.Bot.Types.Enums.ParseMode.Html;
                result.InputMessageContent = itmc;
            }
            return result;
        }
    }
}
