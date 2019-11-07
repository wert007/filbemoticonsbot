using System;
using Telegram.Bot;
using System.IO;
using Telegram.Bot.Args;
using System.Collections.Generic;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types;
using System.Linq;

namespace c_sharp_filb_bot
{
    class Program
    {
        private static Random random = new Random();
        static TelegramBotClient botClient;
        static List<FilbEmoticonData> entries;
        static Dictionary<long, List<InlineQueryResultBase>> recentQueries;
        const int MaxRecentQueries = 5;
        const string recentQueriesFile = @".\recentQueries.txt";
        static void Main(string[] args)
        {
            entries = new List<FilbEmoticonData>();
            var dataFile = @".\data.txt";
            using (FileStream fs = new FileStream(dataFile, FileMode.Open))
            using (StreamReader sr = new StreamReader(fs))
            {
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine().Trim();
                    var entry = FilbEmoticonData.FromString(line);
                    if(entry == null)
                    {
                        Console.WriteLine($"ERROR: Could not read data.txt. Malformed line '{line}'.");
                        continue;
                    }
                    entries.Add(entry);
                }
            }

            if(System.IO.File.Exists(recentQueriesFile))
                recentQueries = DeserializeRecentQueries();
            else
                recentQueries = new Dictionary<long, List<InlineQueryResultBase>>();
            
            var token = System.IO.File.ReadAllText("token.txt");
            botClient = new TelegramBotClient(token);
            var me = botClient.GetMeAsync().Result;
            Console.WriteLine(
                $"Hello, World! I am user {me.Id} and my name is {me.FirstName}."
            );

            botClient.OnMessage += Bot_OnMessage;
            botClient.OnInlineQuery += Bot_OnInlineQuery;
            botClient.StartReceiving();
            Console.ReadLine();
        }
        private static List<InlineQueryResultBase> CollectResult(string query, int max = 50)
        {
            entries.Sort((a, b) => a.GetScore(query).CompareTo(b.GetScore(query)));

            var result = new List<InlineQueryResultBase>();
            foreach (var entry in entries)
            {
                if (entry.GetScore(query) < 100)
                {
                    result.Add(entry.GenerateQueryResult());
                    max--;
                }
                if (max <= 0)
                    break;
            }
            return result;
        }

        private static int[] GenerateRandomIndices(int count)
        {
            var indices = new List<int>();
            while(indices.Count < count)
            {
                var current = random.Next(entries.Count);
                if(!indices.Contains(current))
                    indices.Add(current);
            }
            return indices.ToArray();
        }
        private static List<InlineQueryResultBase> GetRandomEmoticons(int count)
        {
            var result = new List<InlineQueryResultBase>();
            var indices = GenerateRandomIndices(count);
            foreach (var index in indices)
            {
                result.Add(entries[index].GenerateQueryResult());
            }
            return result;
        }

        private static List<InlineQueryResultBase> GetRandomCheatSheet(int count)
        {
            var result = new List<InlineQueryResultBase>();
            var indices = GenerateRandomIndices(count);
            foreach (var index in indices)
            {
                var current = entries[index];
                var articleContent = new InputTextMessageContent("TODO");
                var article = new InlineQueryResultArticle(current.Index.ToString(), current.Name, articleContent);
                article.ThumbUrl = current.ThumbHost;
                article.ThumbHeight = current.Height;
                article.ThumbWidth = current.Width;
                result.Add(article);
            }
            return result;
        }

        private static async void Bot_OnInlineQuery(object sender, InlineQueryEventArgs e)
        {
            var query = e.InlineQuery.Query.Trim().ToLower();
            var user = e.InlineQuery.From.Id;
            List<InlineQueryResultBase> result = null;
            if(string.IsNullOrWhiteSpace(query))
            {
                if(recentQueries.ContainsKey(user))
                {
                   result = recentQueries[user];
                }
                else{
                    //Give some random emoticons.
                    result = GetRandomEmoticons(50);
                }
            }
            else
            {
                if(query.StartsWith("#"))
                {
                    switch (query)
                    {
                        case "#random":
                        case "#discover":
                        case "#all":
                        //German
                        case "#zufall":
                        case "#entdecken":
                        case "#alle":
                            result = GetRandomEmoticons(50);
                            break;
                        case "#cheat":
                            result = GetRandomCheatSheet(50);
                            break;
                        case "#help":
                            //todo
                            result = new List<InlineQueryResultBase>();
                            var contentHelp = new InputTextMessageContent("Use #all to discover randomsmileys. Otherwise just search for some.");
                            var helpArticle = new InlineQueryResultArticle("help", "You need some help?", contentHelp);
                            result.Add(helpArticle);
                            break;
                        default:
                            //TODO: Maybe add an article here?
                            
                            result = new List<InlineQueryResultBase>();
                            var contentDefault = new InputTextMessageContent("Use #all to discover randomsmileys or #help if you want to learn more. Otherwise just search for some.");
                            var defaultArticle = new InlineQueryResultArticle("help", "You need some help?", contentDefault);
                            result.Add(defaultArticle);
                            break;
                    }
                }
                else
                {
                    result = CollectResult(query);
                    if(result.Count > 0)
                    {
                        if(!recentQueries.ContainsKey(user))
                            recentQueries.Add(user, new List<InlineQueryResultBase>());
                        if(!recentQueries[user].Any(r => r.Id == result[0].Id))
                        {
                            recentQueries[user].Add(result[0]);
                            if(recentQueries[user].Count > MaxRecentQueries)
                                recentQueries[user].RemoveAt(0);
                            SerializeRecentQueries();
                        }
                    }
                }
            }
            await botClient.AnswerInlineQueryAsync(e.InlineQuery.Id, result);
        }

        private static void SerializeRecentQueries()
        {
            using(var fs = new FileStream(recentQueriesFile, FileMode.Create))
            using(var sw = new StreamWriter(fs))
            {
                foreach (var recentQuery in recentQueries)
                {
                    sw.WriteLine($"{recentQuery.Key}:{string.Join(",", recentQuery.Value.Select(q => q.Id))}");
                }
            }
        }

        private static Dictionary<long, List<InlineQueryResultBase>> DeserializeRecentQueries()
        {
            var result = new Dictionary<long, List<InlineQueryResultBase>>();
            using(var fs = new FileStream(recentQueriesFile, FileMode.Create))
            using(var sw = new StreamReader(fs))
            {
                while(!sw.EndOfStream)
                {
                    var line = sw.ReadLine();
                    var tokens = line.Split(':');
                    if(tokens.Length != 2)
                    {
                        Console.WriteLine($"ERROR: Could not read recentQueries. Line '{line}' is malformed.");
                        continue;
                    }
                    if(!long.TryParse(tokens[0], out long userId))
                    {
                        Console.WriteLine($"ERROR: Could not read recentQueries. Line '{line}' is malformed.");
                        continue;
                    }
                    result.Add(userId, new List<InlineQueryResultBase>());
                    foreach (var strId in tokens[1].Split(','))
                    {
                        if(!int.TryParse(strId, out var id))
                        {
                            Console.WriteLine($"ERROR: Could not read recentQueries. Line '{line}' is malformed.");
                            continue;
                        }
                        var entry = entries.FirstOrDefault(e => e.Index == id);
                        if(entry == null)
                        {
                            Console.WriteLine($"ERROR: Could not read recentQueries. No entry with Index {id}.");
                            continue;
                        }
                        result[userId].Add(entry.GenerateQueryResult());
                    }
                }
            }
            return result;
        }

        static async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            //TODO
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
