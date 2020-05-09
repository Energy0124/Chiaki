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
    }
}
