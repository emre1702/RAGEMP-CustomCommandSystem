﻿using CustomCommandSystem.Common.Interfaces.Services;
using CustomCommandSystem.Common.Models;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace CustomCommandSystem.Services.Parser
{
    internal class MethodsParser : ICommandMethodsParser
    {

        public IEnumerable<CommandMethodData> GetPossibleMethods(string cmd, string[] userArgs, CommandData commandData)
        {
            var methods = FilterByArgsAmount(commandData.Methods, userArgs.Length);
            return methods;
        }

        private IEnumerable<CommandMethodData> FilterByArgsAmount(IEnumerable<CommandMethodData> methods, int argsAmount)
            => methods.Where(m => m.ExactUserArgsAmount.HasValue
                ? m.ExactUserArgsAmount.Value == argsAmount
                : m.MinUserArgsAmount <= argsAmount && m.MaxUserArgsAmount >= argsAmount);
    }
}
