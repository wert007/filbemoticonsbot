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
        const long creatorId = 17856797;
        private const string strContentDefault = "If you just want to search for emoticons, then please don't use the #. Otherwise use #all to discover random emoticons. Or use #cheat to get to know them by name.";
        private const string strContentHelp = "Use #all to discover random emoticons. Or use #cheat to get to know them by name. Otherwise you can just search for them without a # at the start.";
        private const int MaxEntries = 50;

        static void Main(string[] args)
        {

            try
            {
                var token = System.IO.File.ReadAllText("token.txt");
                botClient = new TelegramBotClient(token);
            }
            catch(Exception e)
            {
                Console.WriteLine("FATAL ERROR: Could not initialize TelegramBotClient.");
                Console.WriteLine();
                Console.WriteLine(e);
                Console.ReadLine();
            }



            entries = new List<FilbEmoticonData>();
            var dataFile = @".\data.txt";
            if(!System.IO.File.Exists(dataFile))
            {
                Report("FATAL ERROR: Could not load Emoticon data base.",  new FileNotFoundException("File not found", dataFile));
            }
            else
            {
                using (FileStream fs = new FileStream(dataFile, FileMode.Open))
                using (StreamReader sr = new StreamReader(fs))
                {
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine().Trim();
                        var entry = FilbEmoticonData.FromString(line);
                        if(entry == null)
                        {
                            Report("ERROR: Could not read data.txt. Malformed line '" + line + "'.");
                            continue;
                        }
                        entries.Add(entry);
                    }
                }
            }

            if(System.IO.File.Exists(recentQueriesFile))
                recentQueries = DeserializeRecentQueries();
            else
                recentQueries = new Dictionary<long, List<InlineQueryResultBase>>();
            
            var me = botClient.GetMeAsync().Result;
            //Bot is running.
            Console.WriteLine(
                "Hello, World! I am user " + me.Id + " and my name is " + me.FirstName + "."
            );

            botClient.OnMessage += Bot_OnMessage;
            botClient.OnInlineQuery += Bot_OnInlineQuery;
            botClient.StartReceiving();
            Console.ReadLine();
        }

        private static bool MessageExceptionTo(long creatorId, Exception exception)
        {
            var result = true;
            try{
                botClient.SendTextMessageAsync(new ChatId(creatorId), exception.ToString());
            }
            catch(Exception ex)
            {
                Console.WriteLine("ERROR: While trying to send a Exception another Error occurred.");
                Console.WriteLine();
                Console.WriteLine(ex);
                result = false;
            }
            return result;
        }

        private static bool MessageExceptionTo(long creatorId, string exception)
        {
            var result = true;
            try{
                botClient.SendTextMessageAsync(new ChatId(creatorId), exception.ToString());
            }
            catch(Exception ex)
            {
                Console.WriteLine("ERROR: While trying to send a Exception another Error occurred.");
                Console.WriteLine();
                Console.WriteLine(ex);
                result = false;
            }
            return result;
        }

        private static List<InlineQueryResultBase> CollectCheatSheet(string query, int max = 50)
        { 
            entries.Sort((a, b) => a.GetScore(query).CompareTo(b.GetScore(query)));

            var result = new List<InlineQueryResultBase>();
            foreach (var entry in entries)
            {
                if (entry.GetScore(query) < 100)
                {
                    result.Add(entry.GenerateCheatSheet());
                    max--;
                }
                if (max <= 0)
                    break;
            }
            return result;
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

        private static int[] GenerateRandomIndices(uint count)
        {
            var indices = new List<int>();
            if(count >= entries.Count)
            {
                for (int i = 0; i < entries.Count; i++)
                    indices.Add(i);
                return indices.ToArray();
            }
            while(indices.Count < count)
            {
                var current = random.Next(entries.Count);
                if(!indices.Contains(current))
                    indices.Add(current);
            }
            return indices.ToArray();
        }
        private static List<InlineQueryResultBase> GetRandomEmoticons(uint count)
        {
            var result = new List<InlineQueryResultBase>();
            var indices = GenerateRandomIndices(count);
            foreach (var index in indices)
            {
                result.Add(entries[index].GenerateQueryResult());
            }
            return result;
        }

        private static List<InlineQueryResultBase> GetRandomCheatSheets(uint count)
        {
            var result = new List<InlineQueryResultBase>();
            var indices = GenerateRandomIndices(count);
            foreach (var index in indices)
            {
                result.Add(entries[index].GenerateCheatSheet());
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
                   result = recentQueries[user];
                else
                    result = GetRandomEmoticons(MaxEntries);
            }
            else
            {
                if(query.StartsWith("#"))
                {
                    //TODO: Maybe add parameters..
                    var commandFound = false;
                    switch (query)
                    {
                        case "#random":
                        case "#discover":
                        case "#all":
                        //German
                        case "#zufall":
                        case "#entdecken":
                        case "#alle":
                            result = GetRandomEmoticons(MaxEntries);
                            commandFound = true;
                            break;
                        case "#cheat":
                            result = GetRandomCheatSheets(MaxEntries);
                            commandFound = true;
                            break;
                        case "#help":
                            result = new List<InlineQueryResultBase>();
                            var contentHelp = new InputTextMessageContent(strContentHelp);
                            var helpArticle = new InlineQueryResultArticle("help", "You need some help?", contentHelp);
                            helpArticle.Description = strContentHelp;
                            var confusedEntry = entries.FirstOrDefault(entry => entry.Index == 10);
                            if(confusedEntry != null)
                            {
                                helpArticle.ThumbUrl = confusedEntry.ThumbHost;
                                helpArticle.ThumbHeight = confusedEntry.Height;
                                helpArticle.ThumbWidth = confusedEntry.Width;
                            }
                            result.Add(helpArticle);
                            commandFound = true;
                            break;
                    }
                    var tokens = query.Split(' ');
                    if(tokens.Length == 2)
                    {
                        switch (tokens[0])
                        {
                            case "#cheat":
                                result = CollectCheatSheet(tokens[1]);
                                commandFound = true;
                                break;
                        }
                    }
                    if(!commandFound)
                    {
                        result = new List<InlineQueryResultBase>();
                        var contentDefault = new InputTextMessageContent(strContentDefault);
                        var defaultArticle = new InlineQueryResultArticle("help", "You need some help?", contentDefault);
                        defaultArticle.Description = strContentDefault;
                        var confusedEntry = entries.FirstOrDefault(entry => entry.Index == 10);
                        if(confusedEntry != null)
                        {
                            defaultArticle.ThumbUrl = confusedEntry.ThumbHost;
                            defaultArticle.ThumbHeight = confusedEntry.Height;
                            defaultArticle.ThumbWidth = confusedEntry.Width;
                        }
                        result.Add(defaultArticle);
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
                    else
                    {
                        //TODO: Add article.
                    }
                }
            }
            try
            {
                await botClient.AnswerInlineQueryAsync(e.InlineQuery.Id, result);
            }
            catch(Exception ex)
            {
                Report("ERROR: Error while trying to send InlineQueryResults.", ex);
            }
        }

        private static void SerializeRecentQueries()
        {
            try {
                using(var fs = new FileStream(recentQueriesFile, FileMode.Create))
                using(var sw = new StreamWriter(fs))
                {
                    foreach (var recentQuery in recentQueries)
                    {
                        sw.WriteLine(recentQuery.Key.ToString() + ":" + string.Join(",", recentQuery.Value.Select(q => q.Id)));
                    }
                }
            }
            catch(Exception ex)
            {
                Report("ERROR: Could not write recentQueries.", ex);
            }
        }

        private static Dictionary<long, List<InlineQueryResultBase>> DeserializeRecentQueries()
        {
            var result = new Dictionary<long, List<InlineQueryResultBase>>();
            try {
                using(var fs = new FileStream(recentQueriesFile, FileMode.Create))
                using(var sw = new StreamReader(fs))
                {
                    while(!sw.EndOfStream)
                    {
                        var line = sw.ReadLine();
                        var tokens = line.Split(':');
                        if(tokens.Length != 2)
                        {
                            Report("ERROR: Could not read recentQueries. Line '" + line + "' is malformed.");
                            continue;
                        }
                        long userId = -1;
                        if(!long.TryParse(tokens[0], out userId))
                        {
                            Report("ERROR: Could not read recentQueries. Line '" + line + "' is malformed.");
                            continue;
                        }
                        result.Add(userId, new List<InlineQueryResultBase>());
                        foreach (var strId in tokens[1].Split(','))
                        {
                            int id = -1;
                            if(!int.TryParse(strId, out id))
                            {
                                Report("ERROR: Could not read recentQueries. Line '" + line + "' is malformed.");
                                continue;
                            }
                            var entry = entries.FirstOrDefault(e => e.Index == id);
                            if(entry == null)
                            {
                                Report("ERROR: Could not read recentQueries. No entry with Index " + id + ".");
                                continue;
                            }
                            result[userId].Add(entry.GenerateQueryResult());
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Report("ERROR: Could not read recentQueries.", ex);
            }
            return result;
        }

        private static bool Report(string message, Exception source = null)
        {
            Console.WriteLine(message);
            if(source != null)
            {
                Console.WriteLine();
                Console.WriteLine(source);
                return MessageExceptionTo(creatorId, source);
            }
            else{
                MessageExceptionTo(creatorId, message);
            }
            return false;
        }

        static async void Bot_OnMessage(object sender, MessageEventArgs e)
        { 
            await botClient.SendTextMessageAsync(
                chatId: e.Message.Chat,
                text: "Please use this bot inline. For example ``` @filbemoticonsbot #help ``` or ``` @filbemoticonsbot lmao ```",
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown
            );
        }
    }
}
