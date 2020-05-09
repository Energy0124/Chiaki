using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace DiscordBot.Services
{
    public class LogService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly CommandHandlingService _commandHandlingService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _discordLogger;
        private readonly ILogger _commandsLogger;
        private readonly ILogger _debugLogger;

        public LogService(DiscordSocketClient discord, CommandService commands,
            CommandHandlingService commandHandlingService, ILoggerFactory loggerFactory)
        {
            _discord = discord;
            _commands = commands;
            _commandHandlingService = commandHandlingService;

            _loggerFactory = loggerFactory;
            _discordLogger = _loggerFactory.CreateLogger("discord");
            _commandsLogger = _loggerFactory.CreateLogger("commands");
            _debugLogger = _loggerFactory.CreateLogger("debug");

            _discord.Log += LogDiscord;
            _commands.Log += LogCommand;
            _commandHandlingService.LogDebug += LogDebug;
            _commandHandlingService.LogMessage += LogMessage;
        }

        private Task LogDiscord(LogMessage message)
        {
            _discordLogger.Log(
                LogLevelFromSeverity(message.Severity),
                0,
                message,
                message.Exception,
                (_1, _2) => message.ToString(prependTimestamp: false));
            return Task.CompletedTask;
        }

        private void LogDebug(string message)
        {
            _debugLogger.LogDebug(message);
        }

        private void LogMessage(SocketUserMessage message)
        {
            _debugLogger.LogDebug(string.Format("{0}: {1} ({2}{3})",
                (object) message.Author, (object) message.Content, (object) message.Id,
                message.Attachments.Count > 0
                    ? (object) string.Format(", {0} Attachments", (object) message.Attachments.Count)
                    : (object) ""));
        }

        private Task LogCommand(LogMessage message)
        {
            // Return an error message for async commands
            if (message.Exception is CommandException command)
            {
                // Don't risk blocking the logging task by awaiting a message send; ratelimits!?
                var _ = command.Context.Channel.SendMessageAsync($"Error: {command.Message}");
            }

            _commandsLogger.Log(
                LogLevelFromSeverity(message.Severity),
                0,
                message,
                message.Exception,
                (_1, _2) => message.ToString(prependTimestamp: false));
            return Task.CompletedTask;
        }

        private static LogLevel LogLevelFromSeverity(LogSeverity severity)
            => (LogLevel) (Math.Abs((int) severity - 5));
    }
}