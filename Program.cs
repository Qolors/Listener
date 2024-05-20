using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using OpenAI_API;
using OpenAI_API.Models;
using OpenAI_API.Chat;

namespace DiscordBot
{
    public class Program
    {
        private DiscordSocketClient _client;
        private OpenAIAPI _chat = new OpenAIAPI("");

        private List<string> sentences = new List<string>();
        private List<string> imageUrls = new List<string>();

        public static void Main(string[] args) => new Program().RunBotAsync().GetAwaiter().GetResult();

        public async Task RunBotAsync()
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(config);

            _client.Log += Log;
            _client.MessageReceived += MessageReceivedAsync;

            var token = "";

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task Log(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {
            // Ignore messages from bots
            if (message.Author.IsBot) return;

            // Example: Respond to "ping" with "pong"
            if ((message.Author.Id == 206232637424140289 || message.Author.Id == 331308445166731266) && message.Channel.Id == 1141540362188505181)
            {
                Console.WriteLine("Got Content Notes");

                if (message.Content.Count() > 0)
                {
                    sentences.Add(message.Content);
                }
                sentences.Add(message.Content);

                if (message.Attachments.Count > 0)
                {
                    foreach (var attachment in message.Attachments)
                    {
                        imageUrls.Add(attachment.Url);
                    }
                }
            }

            int wordCount = sentences
            .SelectMany(sentence => sentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .Count();

            if (wordCount > 500)
            {
                List<string> sendsentences = new List<string>(sentences);
                List<string> sendimages = new List<string>(imageUrls);

                Console.WriteLine("Sending to GPT");

                await GetGPTBlogPost(sendsentences, sendimages);

                imageUrls.Clear();
                sentences.Clear();
            }
            else
            {
                Console.WriteLine(wordCount);
            }

        }

        private async Task GetGPTBlogPost(List<string> sentences, List<string> imageUrls)
        {
            string imageList = imageUrls.Count() > 0 ? string.Join("\n", imageUrls) : "None";

            ChatRequest chatRequest = new ChatRequest()
            {
                Model = Model.GPT4_Turbo,
                Temperature = 0.0,
                MaxTokens = 1000,
                ResponseFormat = ChatRequest.ResponseFormats.JsonObject,
                Messages = new ChatMessage[] {
                new ChatMessage(ChatMessageRole.System, $"""
                You are a blog writer that writes his work in Markdown. You are a writer about the game Oldschool Runescape where you rewrite a player's solo experience in the game. He provides you with notes he takes during his time gaming.
                Turn it into a blog post with decent storytelling. Keep the same manic/random thought process the player has to an extent.

                You are sometimes given images to include in the blog post. Determining where to place them is up to you. Images are provided as Urls.

                You must write the story in Markdown format, but provide the content in JSON format. The JSON must have the following structure - REPLACE THE VALUES WITH YOUR OWN!:
                
                    "FileName": "example-title.md", // CHANGE THIS TO A FILE NAME BASED ON THE TITLE
                    "Title": "Example Title", // CHANGE THIS TO A TITLE BASED ON THE CONTENT YOU GENERATE
                    "Description": "A single sentence description", // CHANGE THIS TO A SINGLE SENTENCE DESCRIPTION BASED ON THE CONTENT
                    "Date": "2024-05-19 <-- Make this Todays Date!!", // CHANGE THIS TO TODAYS DATE
                    "MarkdownContent": "# Boss Kill First Attempts\n\nWe've been working on Agility for quite some time now to unlock a key component and so on." // YOUR GENERATED MARKDOWN BASED ON PLAYER NOTES

                Here are the images Urls you can use in the blog post:
                {imageList}

                Here are the notes the player provided you for this blog post:

                """),
        new ChatMessage(ChatMessageRole.User, $"{string.Join("", sentences)}")
    }
            };

            var results = await _chat.Chat.CreateChatCompletionAsync(chatRequest);

            BlogPostData blogPostData = JsonConvert.DeserializeObject<BlogPostData>(results.ToString());

            CreateMarkdownFileFromGPT(blogPostData);
        }

        private async Task CreateMarkdownFileFromGPT(BlogPostData blogPostData)
        {
            string directory = @"C:\Markdowns";
            string path = Path.Combine(directory, blogPostData.FileName);

            using (StreamWriter sw = new StreamWriter(path))
            {
                await sw.WriteLineAsync($"""
                ---
                title: {blogPostData.Title}
                description: {blogPostData.Description}
                date: {blogPostData.Date}
                scheduled: {blogPostData.Date}
                layout: layouts/post.njk
                ---

                {blogPostData.MarkdownContent}
                """);
            }

        }
    }

    public class BlogPostData
    {
        public string FileName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public string MarkdownContent { get; set; }
    }
}
