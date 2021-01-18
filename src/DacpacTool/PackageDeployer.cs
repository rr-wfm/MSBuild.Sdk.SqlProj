using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.SqlTools.ServiceLayer.BatchParser.ExecutionEngineCode;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class PackageDeployer : IBatchEventsHandler
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
            var references = model.GetReferencedDacPackages();

            if (!references.Any())
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

        public void SetProperty(string key, string value)
        {
            try
            {
                // Convert value into the appropriate type depending on the key
                object propertyValue = key switch
                {
                    "AdditionalDeploymentContributorArguments" => value,
                    "AdditionalDeploymentContributorPaths" => value,
                    "AdditionalDeploymentContributors" => value,
                    "AllowDropBlockingAssemblies" => bool.Parse(value),
                    "AllowIncompatiblePlatform" => bool.Parse(value),
                    "AllowUnsafeRowLevelSecurityDataMovement" => bool.Parse(value),
                    "BackupDatabaseBeforeChanges" => bool.Parse(value),
                    "BlockOnPossibleDataLoss" => bool.Parse(value),
                    "BlockWhenDriftDetected" => bool.Parse(value),
                    "CommandTimeout" => int.Parse(value),
                    "CommentOutSetVarDeclarations" => bool.Parse(value),
                    "CompareUsingTargetCollation" => bool.Parse(value),
                    "CreateNewDatabase" => bool.Parse(value),
                    "DatabaseLockTimeout" => int.Parse(value),
                    "DatabaseSpecification" => ParseDatabaseSpecification(value),
                    "DeployDatabaseInSingleUserMode" => bool.Parse(value),
                    "DisableAndReenableDdlTriggers" => bool.Parse(value),
                    "DoNotAlterChangeDataCaptureObjects" => bool.Parse(value),
                    "DoNotAlterReplicatedObjects" => bool.Parse(value),
                    "DoNotDropObjectTypes" => ParseObjectTypes(value),
                    "DropConstraintsNotInSource" => bool.Parse(value),
                    "DropDmlTriggersNotInSource" => bool.Parse(value),
                    "DropExtendedPropertiesNotInSource" => bool.Parse(value),
                    "DropIndexesNotInSource" => bool.Parse(value),
                    "DropObjectsNotInSource" => bool.Parse(value),
                    "DropPermissionsNotInSource" => bool.Parse(value),
                    "DropRoleMembersNotInSource" => bool.Parse(value),
                    "DropStatisticsNotInSource" => bool.Parse(value),
                    "ExcludeObjectTypes" => ParseObjectTypes(value),
                    "GenerateSmartDefaults" => bool.Parse(value),
                    "IgnoreAnsiNulls" => bool.Parse(value),
                    "IgnoreAuthorizer" => bool.Parse(value),
                    "IgnoreColumnCollation" => bool.Parse(value),
                    "IgnoreColumnOrder" => bool.Parse(value),
                    "IgnoreComments" => bool.Parse(value),
                    "IgnoreCryptographicProviderFilePath" => bool.Parse(value),
                    "IgnoreDdlTriggerOrder" => bool.Parse(value),
                    "IgnoreDdlTriggerState" => bool.Parse(value),
                    "IgnoreDefaultSchema" => bool.Parse(value),
                    "IgnoreDmlTriggerOrder" => bool.Parse(value),
                    "IgnoreDmlTriggerState" => bool.Parse(value),
                    "IgnoreExtendedProperties" => bool.Parse(value),
                    "IgnoreFileAndLogFilePath" => bool.Parse(value),
                    "IgnoreFilegroupPlacement" => bool.Parse(value),
                    "IgnoreFileSize" => bool.Parse(value),
                    "IgnoreFillFactor" => bool.Parse(value),
                    "IgnoreFullTextCatalogFilePath" => bool.Parse(value),
                    "IgnoreIdentitySeed" => bool.Parse(value),
                    "IgnoreIncrement" => bool.Parse(value),
                    "IgnoreIndexOptions" => bool.Parse(value),
                    "IgnoreIndexPadding" => bool.Parse(value),
                    "IgnoreKeywordCasing" => bool.Parse(value),
                    "IgnoreLockHintsOnIndexes" => bool.Parse(value),
                    "IgnoreLoginSids" => bool.Parse(value),
                    "IgnoreNotForReplication" => bool.Parse(value),
                    "IgnoreObjectPlacementOnPartitionScheme" => bool.Parse(value),
                    "IgnorePartitionSchemes" => bool.Parse(value),
                    "IgnorePermissions" => bool.Parse(value),
                    "IgnoreQuotedIdentifiers" => bool.Parse(value),
                    "IgnoreRoleMembership" => bool.Parse(value),
                    "IgnoreRouteLifetime" => bool.Parse(value),
                    "IgnoreSemicolonBetweenStatements" => bool.Parse(value),
                    "IgnoreTableOptions" => bool.Parse(value),
                    "IgnoreTablePartitionOptions" => bool.Parse(value),
                    "IgnoreUserSettingsObjects" => bool.Parse(value),
                    "IgnoreWhitespace" => bool.Parse(value),
                    "IgnoreWithNocheckOnCheckConstraints" => bool.Parse(value),
                    "IgnoreWithNocheckOnForeignKeys" => bool.Parse(value),
                    "IncludeCompositeObjects" => bool.Parse(value),
                    "IncludeTransactionalScripts" => bool.Parse(value),
                    "LongRunningCommandTimeout" => int.Parse(value),
                    "NoAlterStatementsToChangeClrTypes" => bool.Parse(value),
                    "PopulateFilesOnFileGroups" => bool.Parse(value),
                    "RegisterDataTierApplication" => bool.Parse(value),
                    "RunDeploymentPlanExecutors" => bool.Parse(value),
                    "ScriptDatabaseCollation" => bool.Parse(value),
                    "ScriptDatabaseCompatibility" => bool.Parse(value),
                    "ScriptDatabaseOptions" => bool.Parse(value),
                    "ScriptDeployStateChecks" => bool.Parse(value),
                    "ScriptFileSize" => bool.Parse(value),
                    "ScriptNewConstraintValidation" => bool.Parse(value),
                    "ScriptRefreshModule" => bool.Parse(value),
                    "SqlCommandVariableValues" => throw new ArgumentException("SQLCMD variables should be set using the --sqlcmdvar command line argument and not as a property."),
                    "TreatVerificationErrorsAsWarnings" => bool.Parse(value),
                    "UnmodifiableObjectWarnings" => bool.Parse(value),
                    "VerifyCollationCompatibility" => bool.Parse(value),
                    "VerifyDeployment" => bool.Parse(value),
                    _ => throw new ArgumentException($"Unknown property with name {key}", nameof(key))
                };

                PropertyInfo property = typeof(DacDeployOptions).GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
                property.SetValue(DeployOptions, propertyValue);

                var parsedValue = propertyValue switch
                {
                    ObjectType[] o => string.Join(',', o),
                    DacAzureDatabaseSpecification s => $"{s.Edition},{s.MaximumSize},{s.ServiceObjective}",
                    _ => propertyValue.ToString()
                };

                _console.WriteLine($"Setting property {key} to value {parsedValue}");
            }
            catch (FormatException)
            {
                throw new ArgumentException($"Unable to parse value for property with name {key}: {value}", nameof(value));
            }
        }

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

        private ObjectType[] ParseObjectTypes(string value)
        {
            if (value.Contains(';', StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Expected object types to be comma-seperated instead of semi-colon separated");
            }

            var objectTypes = value.Split(',');
            var result = new ObjectType[objectTypes.Length];

            for (int i = 0; i < objectTypes.Length; i++)
            {
                if (!Enum.TryParse(objectTypes[i], false, out ObjectType objectType))
                {
                    throw new ArgumentException($"Unknown object type {objectTypes[i]} specified.", nameof(value));
                }

                result[i] = objectType;
            }

            return result;
        }

        private DacAzureDatabaseSpecification ParseDatabaseSpecification(string value)
        {
            if (value.Contains(';', StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Expected database specification to be comma-seperated instead of semi-colon separated");
            }

            var specification = value.Split(",", 3);
            if (specification.Length != 3)
            {
                throw new ArgumentException("Expected at least 3 parameters for DatabaseSpecification; Edition, MaximumSize and ServiceObjective", nameof(value));
            }

            if (!Enum.TryParse(specification[0], false, out DacAzureEdition edition))
            {
                throw new ArgumentException($"Unknown edition '{specification[0]}' specified.", nameof(value));
            }

            if (!int.TryParse(specification[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int maximumSize))
            {
                throw new ArgumentException($"Unable to parse maximum size '{specification[1]}' as an integer.", nameof(value));
            }

            return new DacAzureDatabaseSpecification
            {
                Edition = edition,
                MaximumSize = maximumSize,
                ServiceObjective = specification[2]
            };
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
