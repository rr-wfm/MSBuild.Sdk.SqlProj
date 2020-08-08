using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class PackageDeployer : IDisposable
    {
        private readonly IConsole _console;

        public PackageDeployer(IConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public SqlConnectionStringBuilder ConnectionStringBuilder { get; private set; } = new SqlConnectionStringBuilder();
        public DacDeployOptions DeployOptions { get; private set; } = new DacDeployOptions();
        public DacPackage Package { get; private set; }

        public void LoadPackage(FileInfo dacpacPackage)
        {
            if (!dacpacPackage.Exists)
            {
                throw new ArgumentException($"File {dacpacPackage.FullName} does not exist.", nameof(dacpacPackage));
            }

            Package = DacPackage.Load(dacpacPackage.FullName);
            _console.WriteLine($"Loaded package '{Package.Name}' version '{Package.Version}' from '{dacpacPackage.FullName}'");
        }

        public void UseTargetServer(string targetServer)
        {
            EnsurePackageLoaded();

            _console.WriteLine($"Using target server '{targetServer}'");
            ConnectionStringBuilder.DataSource = targetServer;
        }

        public void UseSqlAuthentication(string username, string password)
        {
            EnsurePackageLoaded();

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
            EnsurePackageLoaded();

            ConnectionStringBuilder.IntegratedSecurity = true;
            _console.WriteLine("Using Windows Authentication");
        }

        public void Deploy(string targetDatabaseName)
        {
            EnsurePackageLoaded();
            EnsureConnectionStringComplete();

            _console.WriteLine($"Deploying to database '{targetDatabaseName}'");
            var services = new DacServices(ConnectionStringBuilder.ConnectionString);
            services.Deploy(Package, targetDatabaseName, true, DeployOptions);
            _console.WriteLine($"Succesfully deployed database '{targetDatabaseName}'");
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
                    "SqlCommandVariableValues" => throw new NotSupportedException(),
                    "TreatVerificationErrorsAsWarnings" => bool.Parse(value),
                    "UnmodifiableObjectWarnings" => bool.Parse(value),
                    "VerifyCollationCompatibility" => bool.Parse(value),
                    "VerifyDeployment" => bool.Parse(value),
                    _ => throw new ArgumentException($"Unknown property with name {key}", nameof(key))
                };

                PropertyInfo property = typeof(DacDeployOptions).GetProperty(key, BindingFlags.Public | BindingFlags.Instance);
                property.SetValue(DeployOptions, propertyValue);

                _console.WriteLine($"Setting property {key} to value {value}");
            }
            catch (FormatException)
            {
                throw new ArgumentException($"Unable to parse value for property with name {key}: {value}", nameof(value));
            }
        }

        public void Dispose()
        {
            Package?.Dispose();
            Package = null;
        }

        private void EnsurePackageLoaded()
        {
            if (Package == null)
            {
                throw new InvalidOperationException("Package has not been loaded. Call LoadPackage first.");
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
            var objectTypes = value.Split(';');
            var result = new ObjectType[objectTypes.Length];

            for (int i = 0; i < objectTypes.Length; i++)
            {
                if (!Enum.TryParse<ObjectType>(objectTypes[i], false, out ObjectType objectType))
                {
                    throw new ArgumentException($"Unknown object type {objectTypes[i]} specified.", nameof(value));
                }

                result[i] = objectType;
            }

            return result;
        }

        private DacAzureDatabaseSpecification ParseDatabaseSpecification(string value)
        {
            var specification = value.Split(";", 3);
            if (specification.Length != 3)
            {
                throw new ArgumentException("Expected at least 3 parameters for DatabaseSpecification; Edition, MaximumSize and ServiceObjective", nameof(value));
            }

            if (!Enum.TryParse<DacAzureEdition>(specification[0], false, out DacAzureEdition edition))
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
    }
}
