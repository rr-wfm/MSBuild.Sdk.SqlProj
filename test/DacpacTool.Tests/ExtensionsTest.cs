using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlTools.ServiceLayer.BatchParser.ExecutionEngineCode;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using MSBuild.Sdk.SqlProj.DacpacToolLibNetstandard;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    /// <summary>
    /// Contains tests for the <see cref="Extensions" /> class.
    /// </summary>
    [TestClass]
    public class ExtensionsTest
    {
        private static readonly string[] DatabaseVariable = ["SomeDatabase"];
        private static readonly string[] ServerAndDatabaseVariables = ["SomeDatabase", "SomeServer"];
        private static readonly string[] ServerVariable = ["SomeServer"];

        /// <summary>
        /// Tests that we can add a reference to a model.
        /// </summary>
        [TestMethod]
        public void CanAddReference()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage)
                .AddStoredProcedure("MyProc", "SELECT * FROM MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests that we can get references from a model.
        /// </summary>
        [TestMethod]
        public void CanGetReferences()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();
            var model = new TestModelBuilder()
                .AddReference(referencePackage)
                .Build();
            
            // Act
            var references = model.GetReferencedDacPackages();

            // Assert
            references.Any().ShouldBeTrue();
            references.First().ShouldBe(referencePackage.Replace('\\', Path.AltDirectorySeparatorChar));
        }

        /// <summary>
        /// Tests that we can get model validation errors from a model.
        /// </summary>
        [TestMethod]
        public void CanGetModelValidationErrors()
        {
            // Arrange
            var model = new TestModelBuilder()
                .AddStoredProcedureFromFile("../../../../TestProjectWithErrors/Procedures/csp_Test.sql")
                .Build();

            // Act
            var modelValidationErrors = model.GetModelValidationErrors(Enumerable.Empty<string>());

            // Assert
            modelValidationErrors.ShouldNotBeEmpty();

            var error = modelValidationErrors.First();
            error.Severity.ShouldBe(Microsoft.SqlServer.Dac.Model.ModelErrorSeverity.Error);
            error.ErrorCode.ShouldBe(71501);
            error.SourceName.ShouldBe("../../../../TestProjectWithErrors/Procedures/csp_Test.sql", StringCompareShould.IgnoreCase);
        }

        /// <summary>
        /// Tests reference DatabaseLiteral.
        /// </summary>
        [TestMethod]
        public void AddReferenceDatabaseLiteral()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "SomeDatabase")
                .AddStoredProcedure("MyProc", "SELECT * FROM SomeDatabase.dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests reference DatabaseLiteral in new Format.
        /// </summary>
        [TestMethod]
        public void AddReferenceDatabaseLiteralNewFormat()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "dbl=SomeDatabase")
                .AddStoredProcedure("MyProc", "SELECT * FROM SomeDatabase.dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests reference DatabaseVariable.
        /// </summary>
        [TestMethod]
        public void AddReferenceDatabaseVariable()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "dbv=SomeDatabase")
                .AddSqlCmdVariables(DatabaseVariable)
                .AddStoredProcedure("MyProc", "SELECT * FROM [$(SomeDatabase)].dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests reference ServerVariable and DatabaseVariable.
        /// </summary>
        [TestMethod]
        public void AddReferenceServerVariableAndDatabaseVariable()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "dbv=SomeDatabase|srv=SomeServer")
                .AddSqlCmdVariables(ServerAndDatabaseVariables)
                .AddStoredProcedure("MyProc", "SELECT * FROM [$(SomeServer)].[$(SomeDatabase)].dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        /// <summary>
        /// Tests reference ServerVariable and DatabaseLiteral.
        /// </summary>
        [TestMethod]
        public void AddReferenceServerVariableAndDatabaseLiteral()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "dbl=SomeDatabase|srv=SomeServer")
                .AddSqlCmdVariables(ServerVariable)
                .AddStoredProcedure("MyProc", "SELECT * FROM [$(SomeServer)].[SomeDatabase].dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        [TestMethod]
        public void AddReferenceDatabaseLiteralAndVariable_UsesLiteralValue()
        {
            // Arrange
            var referencePackage = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            // Act
            var model = new TestModelBuilder()
                .AddReference(referencePackage, "dbl=SomeDatabase|dbv=WrongDatabase")
                .AddStoredProcedure("MyProc", "SELECT * FROM SomeDatabase.dbo.MyTable;")
                .Build();

            // Assert
            var validationErrors = model.Validate();
            validationErrors.Any().ShouldBeFalse();
        }

        [TestMethod]
        public void GetPreDeploymentScript_WithoutEmbeddedScript_ReturnsNull()
        {
            var packagePath = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            using var package = DacPackage.Load(packagePath);

            package.GetPreDeploymentScript().ShouldBeNull();
        }

        [TestMethod]
        public void GetPostDeploymentScript_WithoutEmbeddedScript_ReturnsNull()
        {
            var packagePath = new TestModelBuilder()
                .AddTable("MyTable", ("Column1", "nvarchar(100)"))
                .SaveAsPackage();

            using var package = DacPackage.Load(packagePath);

            package.GetPostDeploymentScript().ShouldBeNull();
        }

        [TestMethod]
        public void GetPreAndPostDeploymentScripts_WithEmbeddedScripts_ReturnScriptContents()
        {
            var tempFile = new FileInfo(Path.GetTempFileName());
            var packageBuilder = new PackageBuilder(new TestConsole());
            packageBuilder.SetMetadata("MyPackage", "1.0.0.0");
            packageBuilder.UsingVersion(SqlServerVersion.Sql160);
            packageBuilder.ValidateModel();
            var packageOptions = new PackageOptions
            {
                RefactorLogPath = "../../../../TestProjectWithPrePost/RefactorLog/TestProjectWithPrePost.refactorlog"
            };

            packageBuilder.SaveToDisk(tempFile, packageOptions);

            var preDeploymentFile = new FileInfo("../../../../TestProjectWithPrePost/Pre-Deployment/Script.PreDeployment.sql");
            var postDeploymentFile = new FileInfo("../../../../TestProjectWithPrePost/Post-Deployment/Script.Post Deployment.sql");

            var packageHelper = new PackageHelper(new TestConsole());
            packageHelper.AddPreDeploymentScript(preDeploymentFile, tempFile);
            packageHelper.AddPostDeploymentScript(postDeploymentFile, tempFile);

            using var package = DacPackage.Load(tempFile.FullName);

            package.GetPreDeploymentScript().ShouldMatch(@"PRINT N'Pre deploy'[\r\n]*PRINT N'Script1.sql'[\r\n]*GO[\r\n]*");
            var postDeploymentScript = package.GetPostDeploymentScript();
            postDeploymentScript.ShouldContain("PRINT 'Inserting record into MyTable'");
            postDeploymentScript.ShouldContain("ALTER ROLE db_datareader ADD MEMBER [DbReader];");
            postDeploymentScript.ShouldEndWith($"{Environment.NewLine}GO{Environment.NewLine}");
        }

        [TestMethod]
        public void FormatBatchErrorEventArgs_WithException_UsesMessage()
        {
            // Arrange
            var args = CreateBatchErrorEventArgsWithException("Boom", 12, 7, new InvalidOperationException("boom"));

            // Act
            var result = args.Format("input.sql");

            // Assert
            result.ShouldBe("input.sql(12,7):error Boom");
        }

        [TestMethod]
        public void FormatBatchErrorEventArgs_WithSqlError_UsesSqlErrorDetails()
        {
            // Arrange
            var args = CreateBatchErrorEventArgsWithSqlError("Ignored", 4, 3, CreateSqlError(71501, "Bad SQL"));

            // Act
            var result = args.Format("model.sql");

            // Assert
            result.ShouldBe("model.sql(4,3):error SQL71501: Bad SQL");
        }

        [TestMethod]
        public void FormatBatchParserExecutionErrorEventArgs_WithException_UsesMessage()
        {
            // Arrange
            var args = new BatchParserExecutionErrorEventArgs("Parse failed", "Ignored", ScriptMessageType.Error);
            SetField(args, "line", 9);
            SetField(args, "textSpan", CreateTextSpan(2));
            SetField(args, "exception", new InvalidOperationException("parse failed"));

            // Act
            var result = args.Format("deploy.sql");

            // Assert
            result.ShouldBe("deploy.sql(9,2): error: Parse failed");
        }

        [TestMethod]
        public void FormatBatchParserExecutionErrorEventArgs_WithSqlError_UsesSqlErrorDetails()
        {
            // Arrange
            var args = new BatchParserExecutionErrorEventArgs("Ignored", "Ignored", ScriptMessageType.Error);
            SetField(args, "line", 5);
            SetField(args, "textSpan", CreateTextSpan(11));
            SetField(args, "error", CreateSqlError(50000, "Execution failed"));

            // Act
            var result = args.Format("deploy.sql");

            // Assert
            result.ShouldBe("deploy.sql(5,11): error: SQL50000: Execution failed");
        }

        [TestMethod]
        public void FormatBatchParserExecutionErrorEventArgs_WithoutExceptionOrSqlError_UsesMessageAndDescription()
        {
            // Arrange
            var args = new BatchParserExecutionErrorEventArgs("Parse failed", "Near GO", ScriptMessageType.Error);
            SetField(args, "line", 6);
            SetField(args, "textSpan", CreateTextSpan(4));

            // Act
            var result = args.Format("deploy.sql");

            // Assert
            result.ShouldBe("deploy.sql(6,4): error: Parse failed Near GO");
        }

        [TestMethod]
        public void ParseExternalParts_WhenRegexTimesOut_ShouldThrowArgumentException()
        {
            // Arrange
            // Use a regex designed to trigger catastrophic backtracking so the timeout path is deterministic in test.
            var regex = new Regex("dbl=(?<dbl>(a+)+)$", RegexOptions.None, TimeSpan.FromMilliseconds(1));
            var method = typeof(PackageBuildExtensions).GetMethod("ParseExternalParts", BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(Regex) }, null);

            // Act
            // The trailing '!' prevents a full match and forces catastrophic backtracking in the injected regex.
            Action action = () => method!.Invoke(null, new object[] { "dbl=" + new string('a', 2048) + "!", regex });

            // Assert
            var exception = action.ShouldThrow<TargetInvocationException>().InnerException.ShouldBeOfType<ArgumentException>();
            exception.ParamName.ShouldBe("externalParts");
            exception.Message.ShouldBe(
                "Unable to parse reference external parts. " +
                "Use a database literal or SQLCMD variable metadata such as " +
                "'DatabaseVariableLiteralValue=\"MyDatabase\"', " +
                "'DatabaseSqlCmdVariable=\"MyDatabaseVar\"', or " +
                "'DatabaseSqlCmdVariable=\"MyDatabaseVar\" ServerSqlCmdVariable=\"MyServerVar\"'. " +
                "(Parameter 'externalParts')");
            exception.InnerException.ShouldBeOfType<RegexMatchTimeoutException>();
        }

        private static TextSpan CreateTextSpan(int startIndex)
        {
            var textSpan = new TextSpan();
            SetField(ref textSpan, "iStartIndex", startIndex);
            return textSpan;
        }

        private static BatchErrorEventArgs CreateBatchErrorEventArgsWithException(string message, int line, int startIndex, Exception exception)
        {
            var constructor = typeof(BatchErrorEventArgs).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(string), typeof(int), typeof(TextSpan), typeof(Exception) },
                null);

            return (BatchErrorEventArgs)constructor!.Invoke(new object[] { message, "description", line, CreateTextSpan(startIndex), exception });
        }

        private static BatchErrorEventArgs CreateBatchErrorEventArgsWithSqlError(string message, int line, int startIndex, SqlError error)
        {
            var constructor = typeof(BatchErrorEventArgs).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(SqlError), typeof(TextSpan), typeof(Exception) },
                null);

            var args = (BatchErrorEventArgs)constructor!.Invoke(new object[] { message, error, CreateTextSpan(startIndex), null });
            SetField(args, "line", line);
            return args;
        }

        private static SqlError CreateSqlError(int number, string message)
        {
            var constructor = typeof(SqlError).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(int), typeof(byte), typeof(byte), typeof(string), typeof(string),
                    typeof(string), typeof(int), typeof(Exception)
                },
                null);

            return (SqlError)constructor!.Invoke(new object[] { number, (byte)0, (byte)0, "server", message, "proc", 1, null });
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = FindField(target.GetType(), fieldName)
                ?? throw new InvalidOperationException($"Unable to find field '{fieldName}' on '{target.GetType()}'.");

            field.SetValue(target, value);
        }

        private static void SetField<T>(ref T target, string fieldName, object value) where T : struct
        {
            object boxed = target;
            SetField(boxed, fieldName, value);
            target = (T)boxed;
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            while (type != null)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field;
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
