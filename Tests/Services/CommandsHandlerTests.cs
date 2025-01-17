﻿using CustomCommandSystem.Common.Enums;
using CustomCommandSystem.Common.Extensions;
using CustomCommandSystem.Common.Interfaces.Services;
using CustomCommandSystem.Services;
using CustomCommandSystem.Services.Cleaner;
using CustomCommandSystem.Services.Executer;
using CustomCommandSystem.Services.Loader;
using CustomCommandSystem.Services.Parser;
using CustomCommandSystem.Services.Utils;
using GTANetworkAPI;
using NSubstitute;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using static CustomCommandSystem.Tests.Services.Data.TestCommands;

namespace CustomCommandSystem.Tests.Services
{
    class CommandsHandlerTests
    {
#nullable disable
        private StringWriter _stringWriter;
        private CommandsConfiguration _configuration;
        private Player _player;
        private CommandsHandler _commandsHandler;

#nullable enable

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _configuration = new CommandsConfiguration { RunCommandMethodInMainThread = false };
            _player = new Player(new NetHandle());
            var cleaner = new MessageCleaner(_configuration);
            var commandParser = new CommandParser();
            var argumentsConverter = new ArgumentsConverter(_configuration);
            var argumentsParser = new ArgumentsParser(argumentsConverter);
            var logger = Substitute.For<ILogger>();
            var fastMethodInvoker = new FastMethodInvoker();
            var commandsLoader = new CommandsLoader(fastMethodInvoker, logger, argumentsConverter);
            var wrongUsageHandler = new WrongUsageHandler(_configuration);
            var methodExecuter = new MethodExecuter(argumentsParser, _configuration, wrongUsageHandler);
            var methodsParser = new MethodsParser();
            _commandsHandler = new CommandsHandler(cleaner, commandParser, argumentsParser, commandsLoader, methodExecuter, methodsParser, _configuration, wrongUsageHandler);

            commandsLoader.LoadCommands(Assembly.GetExecutingAssembly());
            argumentsConverter.SetConverter(typeof(OutputTestModel), 2, (player, inputData, args, cancel) => new OutputTestModel { Id = int.Parse(args[0]), AnyString = args[1] });
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _stringWriter.Dispose();
            ConsoleUtils.ResetOut();
        }

        [SetUp]
        public void SetUp()
        {
            _stringWriter = new StringWriter();
            Console.SetOut(_stringWriter);
        }

        [Test]
        [TestCase("/output test test test test", ExpectedResult = "Output 1 test test test test")]
        [TestCase("/console 5 test test test", ExpectedResult = "Output 2 5 test test test")]
        [TestCase("/ConsoleOutput 5 test", ExpectedResult = "Output 3 5 test")]
        [TestCase("/ConsoleOutput", ExpectedResult = "Output empty called")]
        public string ExecuteCommand_TestCommandsWork(string cmd)
        {
            _commandsHandler.ExecuteCommand(_player, cmd);

            return _stringWriter.ToString()!;
        }

        [Test]
        [TestCase("Test asd", false, "null", UsageOutputType.OneUsage, ExpectedResult = "USAGE: /Test [test1] [test2]")]
        [TestCase("/Test asd", false, "null", UsageOutputType.AllUsages,
            ExpectedResult = "USAGES:\r\n/Test [test1] [test2]\r\n/Test\r\n/Test [vector3] [testInt] [a] [b] [c] [remainingText]\r\n/Test")]
        [TestCase("Test asd", true, "null", UsageOutputType.OneUsage, ExpectedResult = "USAGE: /Test [test1] [test2]")]
        [TestCase("/Test asd", true, "nothing", UsageOutputType.AllUsages,
            ExpectedResult = "USAGES:\r\n/Test [test1] [test2]\r\n/Test\r\n/Test [vector3] [testInt] [a = test] [b = 5] [c = nothing] [remainingText = ]\r\n/Test")]
        [TestCase("/Test asd", false, "null", UsageOutputType.OneUsageOnWrongTypes, ExpectedResult = "The command was used incorrectly.")]
        [TestCase("Test asd", false, "null", UsageOutputType.AllUsagesOnWrongTypes, ExpectedResult = "The command was used incorrectly.")]
        [TestCase("/Test4", false, "null", UsageOutputType.OneUsage, ExpectedResult = "USAGE: /Test4 [a] [b]")]
        [TestCase("Test4", false, "null", UsageOutputType.OneUsageOnWrongTypes, ExpectedResult = "The command was used incorrectly.")]
        public string ExecuteCommand_OutputsUsage(string cmd, bool addDefaultValues, string nullValueName, UsageOutputType usageOutputType)
        {
            var output = string.Empty;
            _configuration.MessageOutputHandler = (data) => output = data.MessageToOutput;
            _configuration.UsageAddDefaultValues = addDefaultValues;
            _configuration.NullDefaultValueName = nullValueName;
            _configuration.UsageOutputType = usageOutputType;

            _commandsHandler.ExecuteCommand(_player, cmd);

            return output;
        }
    }
}
