using System.Threading.Tasks;
using Discord.Commands;

namespace DiscordBot.Modules
{
    public class InfoModule : ModuleBase<SocketCommandContext>
    {
        [Command("info")]
        public Task Info()
            => ReplyAsync(
                $"Hello {Context.User.Username}, {Context.Client.CurrentUser.Username} desu. はじめまして。\n");
        
        [Command("I love you")]
        public Task ILoveYou()
            => ReplyAsync(
                $"{Context.User.Username}, I love you too!\n");
        
        [Command("sing")]
        public Task Sing()
            => ReplyAsync(
                $"La la la~\n");
        
        [Command("bye")]
        public Task Bye()
            => ReplyAsync(
                $"Bye bye!\n");
    }
}
