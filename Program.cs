using System;
using Telegram.Bot;
using System.IO;
using Telegram.Bot.Args;
using System.Collections.Generic;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types;

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

        internal InlineQueryResultBase GenerateQueryResult()
        {
            if(Extension == "gif")
            {
                var result = new InlineQueryResultGif(Index.ToString(), FileHost, ThumbHost);
                result.GifHeight = Height;
                result.GifWidth = Width;
                result.Title = Name;
                return result;
            }
            else if(Extension == "png")
            {
                var result = new InlineQueryResultPhoto(Index.ToString(), FileHost, ThumbHost);
                result.PhotoHeight = Height;
                result.PhotoWidth = Width;
                result.Title = Name;
                return result;
            }
            return new InlineQueryResultArticle(Index.ToString(), "New Extension discovered!", new InputTextMessageContent($"A new Extension named {Extension} was discovered while searching for '{Name}'."));
        }
    }
    class Program
    {
        static TelegramBotClient botClient;
        static List<FilbEmoticonData> entries;
        static void Main(string[] args)
        {
            entries = new List<FilbEmoticonData>();
            var dataFile = @".\filb_dump\data.txt";
            using (FileStream fs = new FileStream(dataFile, FileMode.Open))
            using (StreamReader sr = new StreamReader(fs))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine().Trim();
                    entries.Add(FilbEmoticonData.FromString(line));
                    Console.WriteLine(line);
                }
            }

            var token = System.IO.File.ReadAllText("token.txt");
            botClient = new TelegramBotClient(token);
            var me = botClient.GetMeAsync().Result;
            Console.WriteLine(
                $"Hello, World! I am user {me.Id} and my name is {me.FirstName}."
            );


            botClient.OnMessage += Bot_OnMessage;
            botClient.OnInlineQuery += Bot_OnInlineQuery;
            botClient.OnInlineResultChosen += Bot_OnInlineResultChosen;
            botClient.StartReceiving();
            Console.ReadLine();
        }

        private static void Bot_OnInlineResultChosen(object sender, ChosenInlineResultEventArgs e)
        {
            Console.WriteLine($"{e.ChosenInlineResult.ResultId}");

        }

        private static IEnumerable<InlineQueryResultBase> CollectResult(string query)
        {
            var max = 49;
            var result = new List<InlineQueryResultBase>();
            foreach (var entry in entries)
            {
                if (entry.Name.Contains(query))
                {
                    result.Add(entry.GenerateQueryResult());
                    max--;
                }
                if (max < 0)
                    break;
            }
            return result;
        }

        private static async void Bot_OnInlineQuery(object sender, InlineQueryEventArgs e)
        {
            Console.WriteLine($"{e.InlineQuery.Query}");
            var result = CollectResult(e.InlineQuery.Query);
            await botClient.AnswerInlineQueryAsync(e.InlineQuery.Id, result);
        }

        static async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Text != null)
            {
                Console.WriteLine($"Received a text message in chat {e.Message.Chat.Id}.");

                await botClient.SendTextMessageAsync(
                  chatId: e.Message.Chat,
                  text: "You said:\n" + e.Message.Text
                );
            }
        }
    }
}
