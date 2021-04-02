﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using tMod64Bot.Services.Config;
using tMod64Bot.Services.Logging;
using tMod64Bot.Services.Tag;
using tMod64Bot.Utils;

namespace tMod64Bot.Services
{
    public sealed class CommandHandler : ServiceBase
    {
        private static readonly CommandError IGNORED_ERRORS = CommandError.BadArgCount | CommandError.UnknownCommand | CommandError.UnmetPrecondition | CommandError.ObjectNotFound;

        private readonly CommandService _commands;
        private readonly ConfigService _config;
        private readonly LoggingService _loggingService;

        public CommandHandler(IServiceProvider services) : base(services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _loggingService = services.GetRequiredService<LoggingService>();
            _config = services.GetRequiredService<ConfigService>();
        }

        public async Task InitializeAsync()
        {
            Client.MessageReceived += HandleCommandAsync;
            
            _commands.CommandExecuted += CommandExecutedAsync;
        }

        private async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (!command.IsSpecified || result.IsSuccess)
                return;

            var error = EmbedHelper.ErrorEmbed(result.ErrorReason!);

            await context.Channel.SendMessageAsync(embed:error);
            
            await _loggingService.Log(LogSeverity.Error, LogSource.Service, $"Error in command Execution: {result.Error!.Value}");
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            if (!(arg is SocketUserMessage msg))
                return;

            var context = new SocketCommandContext(Client, msg);

            var argPos = 0;
            if (msg.HasStringPrefix(_config.Config.BotPrefix, ref argPos) || msg.HasMentionPrefix(Client.CurrentUser, ref argPos))
            {
                var result = await _commands.ExecuteAsync(context, argPos, Services);

                if (result.Error == CommandError.UnknownCommand && !arg.Content.Remove(0, _config.Config.BotPrefix.Length).Contains(' '))
                {
                    var tagName = arg.Content.Remove(0, _config.Config.BotPrefix.Length).ToLower();
                    
                    TagService tagService = Services.GetRequiredService<TagService>();

                    Config.Tag? tagResult = null;
                    
                    try { tagResult = await tagService.GetTag(tagName); }
                    catch (Exception e) { /* ignore */ }
                    
                    if (tagResult == null)
                        return;

                    await context.Channel.SendMessageAsync($"**Tag: {tagName}**\n{tagResult.Content}");
                }
            }
        }
    }
}
