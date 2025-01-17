﻿using CustomCommandSystem.Common.Delegates;
using CustomCommandSystem.Common.Exceptions;
using CustomCommandSystem.Common.Extensions;
using CustomCommandSystem.Common.Interfaces.Services;
using CustomCommandSystem.Common.Models;
using CustomCommandSystem.Services.Utils;
using GTANetworkAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CustomCommandSystem.Services.Executer
{
    internal class MethodExecuter : ICommandMethodExecuter
    {
        internal static BeforeCommandExecuteDelegate? BeforeCommandExecute { get; set; }
        internal static AfterCommandExecuteDelegate? AfterCommandExecute { get; set; }

        private readonly ICommandArgumentsParser _argumentsParser;
        private readonly ICommandsConfiguration _configuration;
        private readonly IWrongUsageHandler _wrongUsageHandler;

        public MethodExecuter(ICommandArgumentsParser argumentsParser, ICommandsConfiguration configuration, IWrongUsageHandler wrongUsageHandler)
            => (_argumentsParser, _configuration, _wrongUsageHandler) = (argumentsParser, configuration, wrongUsageHandler);

        public async Task<bool> TryExecuteSuitable(Player player, UserInputData userInputData, CommandData commandData, List<CommandMethodData> possibleMethods)
        {
            var suitableMethodInfo = await GetSuitableMethodInfo(player, possibleMethods, userInputData);
            if (suitableMethodInfo.MethodData is null)
            {
                if (suitableMethodInfo.OutputCommandUsedWrongIfNoneFound && _configuration.CommandUsedIncorrectly is { } text)
                    _configuration.MessageOutputHandler.Invoke(new CommandOutputData(player, text, userInputData));
                var wrongUsageHandlerWorked = _wrongUsageHandler.Handle(player, userInputData, commandData.Methods, possibleMethods);
                if (!wrongUsageHandlerWorked && _configuration.CommandDoesNotExistError is { } text2)
                    _configuration.MessageOutputHandler.Invoke(new CommandOutputData(player, text2, userInputData));
                return false;
            }

            var args = suitableMethodInfo.ConvertedArgs ?? Array.Empty<object>();

            if (!await AreCustomRequirementsMet(suitableMethodInfo.MethodData, player, args)) return true;

            var cancel = new CancelEventArgs();
            BeforeCommandExecute?.Invoke(player, userInputData, args, cancel);
            if (cancel.Cancel) return true;

            if (_configuration.RunCommandMethodInMainThread)
                RunSync(suitableMethodInfo.MethodData, args);
            else
                Run(suitableMethodInfo.MethodData, args);


            AfterCommandExecute?.Invoke(player, userInputData, args);
            return true;
        }

        private async Task<SuitableMethodInfo> GetSuitableMethodInfo(Player player, List<CommandMethodData> possibleMethods, UserInputData userInputData)
        {
            var outputCommandUsedWrongIfNoneFound = false;
            foreach (var possibleMethod in possibleMethods)
            {
                try
                {
                    var convertedArgs = await _argumentsParser.ParseInvokeArguments(player, possibleMethod, userInputData).ToArrayAsync();
                    if (convertedArgs != null)
                        return new SuitableMethodInfo
                        {
                            ConvertedArgs = convertedArgs,
                            OutputCommandUsedWrongIfNoneFound = false,
                            MethodData = possibleMethod
                        };
                }
                catch (ExpectionWithoutOutput) { }
                catch (Exception)
                {
                    outputCommandUsedWrongIfNoneFound = true;
                }
            }

            return new SuitableMethodInfo { OutputCommandUsedWrongIfNoneFound = outputCommandUsedWrongIfNoneFound };
        }

        private async Task<bool> AreCustomRequirementsMet(CommandMethodData methodData, Player player, object?[] methodArgs)
        {
            if (methodData.RequirementCheckers.Length == 0) return true;

            CustomCommandInfo? customCommandInfo = methodData.IsCommandInfoRequired ? (CustomCommandInfo)methodArgs[methodData.UserParametersStartIndex - 1]! : null;
            var args = new ArraySegment<object?>(methodArgs, methodData.UserParametersStartIndex, methodArgs.Length - methodData.UserParametersStartIndex);

            bool worked = await NAPI.Task.RunWait(() =>
            {
                foreach (var checker in methodData.RequirementCheckers)
                    if (!checker.CanExecute(player, customCommandInfo, args))
                        return false;
                return true;
            });

            return worked;
        }

        private void RunSync(CommandMethodData methodData, object?[] args)
        {
            NAPI.Task.Run(() =>
            {
                if (methodData.FastInvokeHandler is FastInvokeHandler nonStaticHandler)
                    nonStaticHandler.Invoke(methodData.Instance, args);
                else if (methodData.FastInvokeHandler is FastInvokeHandlerStatic staticHandler)
                    staticHandler.Invoke(args);
            });
        }

        private void Run(CommandMethodData methodData, object?[] args)
        {
            if (methodData.FastInvokeHandler is FastInvokeHandler nonStaticHandler)
                nonStaticHandler.Invoke(methodData.Instance, args);
            else if (methodData.FastInvokeHandler is FastInvokeHandlerStatic staticHandler)
                staticHandler.Invoke(args);
        }
    }
}