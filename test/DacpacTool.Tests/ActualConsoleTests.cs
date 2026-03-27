using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    [DoNotParallelize]
    public class ActualConsoleTests
    {
        [TestMethod]
        public void ReadLine_ReturnsConsoleInput()
        {
            var originalIn = Console.In;

            try
            {
                Console.SetIn(new StringReader("hello" + Environment.NewLine));

                var console = new ActualConsole();

                console.ReadLine().ShouldBe("hello");
            }
            finally
            {
                Console.SetIn(originalIn);
            }
        }

        [TestMethod]
        public void WriteLine_WritesToConsoleOutput()
        {
            var originalOut = Console.Out;
            using var writer = new StringWriter();

            try
            {
                Console.SetOut(writer);

                var console = new ActualConsole();

                console.WriteLine("hello");

                writer.ToString().ShouldBe("hello" + Environment.NewLine);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }
    }
}
