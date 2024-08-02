using System;
using System.IO;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlTools.ServiceLayer.BatchParser.ExecutionEngineCode;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    internal sealed class PackageDeployer : IBatchEventsHandler
    {
        private readonly IConsole _console;
        private string _currentSource;

        public PackageDeployer(IConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public SqlConnectionStringBuilder ConnectionStringBuilder { get; private set; } = new SqlConnectionStringBuilder();
        public DacDeployOptions DeployOptions { get; private set; } = new DacDeployOptions();

        public void UseTargetServer(string targetServer)
        {
            _console.WriteLine($"Using target server '{targetServer}'");
            ConnectionStringBuilder.DataSource = targetServer;
        }

        public void UseTargetServerAndPort(string targetServer, int targetPort)
        {
            _console.WriteLine($"Using target server '{targetServer}' on port {targetPort}");
            ConnectionStringBuilder.DataSource = $"{targetServer},{targetPort}";
        }

        public void UseSqlAuthentication(string username, string password)
        {
            ConnectionStringBuilder.UserID = username;
            if (string.IsNullOrWhiteSpace(password))
            {
                _console.WriteLine("Enter password:");
                ConnectionStringBuilder.Password = _console.ReadLine();
            }
            else
            {
                ConnectionStringBuilder.Password = password;
            }

            _console.WriteLine("Using SQL Server Authentication");
        }

        public void UseWindowsAuthentication()
        {
            ConnectionStringBuilder.IntegratedSecurity = true;
            _console.WriteLine("Using Windows Authentication");
        }

        public void UseEncrypt(bool encrypt)
        {
            ConnectionStringBuilder.Encrypt = encrypt;
            _console.WriteLine($"Using encrypt: {encrypt}");
        }

        public void SetSqlCmdVariable(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Key must have a value.", nameof(key));
            }
            else if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"SQLCMD variable '{key}' has no value. Specify a value in the project file or on the command line.", nameof(value));
            }

            DeployOptions.SqlCommandVariableValues.Add(key, value);
            _console.WriteLine($"Adding SQLCMD variable '{key}' with value '{value}'");
        }

        public void RunPreDeploymentScriptFromReferences(FileInfo dacpacPackage, string targetDatabaseName)
        {
            RunDeploymentScriptFromReferences(dacpacPackage, targetDatabaseName, true);
        }

        public void Deploy(FileInfo dacpacPackage, string targetDatabaseName)
        {
            EnsureConnectionStringComplete();

            if (!dacpacPackage.Exists)
            {
                throw new ArgumentException($"File {dacpacPackage.FullName} does not exist.", nameof(dacpacPackage));
            }

            using var package = DacPackage.Load(dacpacPackage.FullName);
            _console.WriteLine($"Deploying package '{package.Name}' version '{package.Version}' to database '{targetDatabaseName}'");

            try
            {
                var services = new DacServices(ConnectionStringBuilder.ConnectionString);
                services.Message += HandleDacServicesMessage;
                services.Deploy(package, targetDatabaseName, true, DeployOptions);
                _console.WriteLine($"Successfully deployed database '{targetDatabaseName}'");
            }
            catch (DacServicesException ex)
            {
                if (ex.InnerException != null)
                {
                    _console.WriteLine($"ERROR: Deployment of database '{targetDatabaseName}' failed: {ex.InnerException.Message}");
                }
                else
                {
                    _console.WriteLine($"ERROR: Deployment of database '{targetDatabaseName}' failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _console.WriteLine($"ERROR: An unknown error occurred while deploying database '{targetDatabaseName}': {ex.Message}");
            }
        }

        public void RunPostDeploymentScriptFromReferences(FileInfo dacpacPackage, string targetDatabaseName)
        {
            RunDeploymentScriptFromReferences(dacpacPackage, targetDatabaseName, false);
        }

        private void RunDeploymentScriptFromReferences(FileInfo dacpacPackage, string targetDatabaseName, bool isPreDeploy)
        {
            using var model = new TSqlModel(dacpacPackage.FullName, DacSchemaModelStorageType.Memory);
            var references = model.GetReferencedDacPackages().ToList();

            if (references.Count == 0)
            {
                return;
            }

            var builder = new SqlConnectionStringBuilder(ConnectionStringBuilder.ConnectionString);
            if (!isPreDeploy)
            {
                // Only set initial catalog for post-deployment script since database might not exist yet for pre-deployment
                builder.InitialCatalog = targetDatabaseName;
            }

            var executionEngineConditions = new ExecutionEngineConditions { IsSqlCmd = true };
            using var engine = new ExecutionEngine();
            using var connection = new SqlConnection(builder.ConnectionString);
            connection.Open();

            foreach (var reference in references)
            {
                if (!File.Exists(reference))
                {
                    // TODO Format output to allow warning to show up in Error List
                    _console.WriteLine($"warning: Unable to find referenced .dacpac at '{reference}'");
                    continue;
                }

                using var referencedPackage = DacPackage.Load(reference);
                var script = isPreDeploy ? referencedPackage.GetPreDeploymentScript() : referencedPackage.GetPostDeploymentScript();
                if (string.IsNullOrEmpty(script))
                {
                    continue;
                }

                string scriptPrefix = isPreDeploy ? "pre" : "post";
                _console.WriteLine($"Running {scriptPrefix}-deployment script for referenced package '{referencedPackage.Name}' version '{referencedPackage.Version}'");
                _currentSource = $"{referencedPackage.Name}/{scriptPrefix}deploy.sql";

                var scriptExecutionArgs = new ScriptExecutionArgs(script, connection, 0, executionEngineConditions, this);
                AddSqlCmdVariables(scriptExecutionArgs, targetDatabaseName);

                engine.BatchParserExecutionError += (sender, args) => _console.WriteLine(args.Format(_currentSource));
                engine.ScriptExecutionFinished += (sender, args) => _console.WriteLine($"Executed {scriptPrefix}-deployment script for referenced package " +
                    $"'{referencedPackage.Name}' version '{referencedPackage.Version}' with result: {args.ExecutionResult}");
                engine.ExecuteScript(scriptExecutionArgs);
            }
        }

        private void AddSqlCmdVariables(ScriptExecutionArgs args, string targetDatabaseName)
        {
            args.Variables.Add("DatabaseName", targetDatabaseName);
            foreach (var variable in DeployOptions.SqlCommandVariableValues)
            {
                args.Variables.Add(variable.Key, variable.Value);
            }
        }

        private void HandleDacServicesMessage(object sender, DacMessageEventArgs args)
        {
            var message = args.Message;
            if (message.MessageType == DacMessageType.Message)
            {
                _console.WriteLine(message.Message);
            }
            else
            {
                _console.WriteLine($"{message.MessageType} {message.Prefix}{message.Number}: {message.Message}");
            }
        }

        public void SetDeployProperty(string deployProperty)
            => this.SetDeployProperties(new[] { deployProperty });

        public void SetDeployProperties(string[] deployProperties) => this.DeployOptions.SetDeployProperties(deployProperties, _console);

        private void EnsureConnectionStringComplete()
        {
            if (string.IsNullOrWhiteSpace(ConnectionStringBuilder.DataSource))
            {
                throw new InvalidOperationException("A target server has not been set. Call UseTargetServer first.");
            }

            if (string.IsNullOrWhiteSpace(ConnectionStringBuilder.UserID) && ConnectionStringBuilder.IntegratedSecurity == false)
            {
                throw new InvalidOperationException("No authentication information has been set. Call UseSqlServerAuthentication or UseWindowsAuthentication first.");
            }
        }

        void IBatchEventsHandler.OnBatchCancelling(object sender, EventArgs args)
        {
            // Nothing to do here
        }

        void IBatchEventsHandler.OnBatchError(object sender, BatchErrorEventArgs args)
        {
            _console.WriteLine(args.Format(_currentSource));
        }

        void IBatchEventsHandler.OnBatchMessage(object sender, BatchMessageEventArgs args)
        {
            _console.WriteLine($"{_currentSource}: {args.Message}");
        }

        void IBatchEventsHandler.OnBatchResultSetFinished(object sender, EventArgs args)
        {
            // Nothing to do here
        }

        void IBatchEventsHandler.OnBatchResultSetProcessing(object sender, BatchResultSetEventArgs args)
        {
            // Nothing to do here
        }
    }
}
