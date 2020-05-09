using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordBot.Data;
using LiteDB;

namespace DiscordBot.Services
{
    public class CommandHandlingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private IServiceProvider _provider;
        private LiteDatabase _database;

        public CommandHandlingService(IServiceProvider provider, DiscordSocketClient discord, CommandService commands,
            LiteDatabase database)
        {
            _discord = discord;
            _commands = commands;
            _provider = provider;
            _database = database;

            _discord.MessageReceived += MessageReceived;
        }

        public event Action<SocketUserMessage> LogMessage;
        public event Action<string> LogDebug;

        public async Task InitializeAsync(IServiceProvider provider)
        {
            _provider = provider;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), provider);
            // Add additional initialization code here...
        }

        private async Task MessageReceived(SocketMessage rawMessage)
        {
            // Ignore system messages and messages from bots
            if (!(rawMessage is SocketUserMessage message)) return;
            if (message.Source != MessageSource.User) return;

            int argPos = 0;

            // if message come from bot, do nothing
            if (message.Author.IsBot) return;

            // if user input string prefix, @mention the bot or send dm, continue, else return
            if (!(message.HasStringPrefix("chiaki", ref argPos)
                  || message.HasMentionPrefix(_discord.CurrentUser, ref argPos)
                  || message.Channel is SocketDMChannel)) return;

            var context = new SocketCommandContext(_discord, message);

            // log message
            LogMessage(message);

            // skip whitespace after prefix
            for (var i = argPos; i < message.Content.Length; i++)
            {
                if (message.Content[i] == ' ')
                {
                    argPos++;
                }
                else
                {
                    break;
                }
            }

            var result = await _commands.ExecuteAsync(context, argPos, _provider);

            if (result.Error.HasValue &&
                result.Error.Value == CommandError.UnknownCommand)
                return;

            if (result.Error.HasValue &&
                result.Error.Value != CommandError.UnknownCommand)
                await context.Channel.SendMessageAsync(result.ToString());

            // Add points to the user for using the bot
            // Do this asynchronously, on another task, to prevent database access (and levelup notifications?) from halting the bot
            _ = UpdateLevelAsync(context);
        }

        private Task UpdateLevelAsync(SocketCommandContext context)
        {
            var users = _database.GetCollection<User>("users");
            var user = users.FindOne(u => u.Id == context.User.Id) ?? new User {Id = context.User.Id};
            ++user.Points;
            users.Upsert(user);

            // If sending a levelup notification, flag this Task as async and remove the following line
            return Task.CompletedTask;
        }
    }
}