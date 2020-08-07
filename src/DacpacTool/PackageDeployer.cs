using System;
using System.IO;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class PackageDeployer : IDisposable
    {
        private readonly SqlConnectionStringBuilder _builder = new SqlConnectionStringBuilder();
        private readonly DacDeployOptions _deployOptions = new DacDeployOptions();
        private DacPackage _package;

        public void LoadPackage(FileInfo dacpacPackage)
        {
            Console.WriteLine($"Loading package from '{dacpacPackage.FullName}'");
            _package = DacPackage.Load(dacpacPackage.FullName);
        }

        public void UseTargetServer(string targetServer)
        {
            Console.WriteLine($"Using target server '{targetServer}'");
            _builder.DataSource = targetServer;
        }

        public void UseSqlAuthentication(string username)
        {
            Console.WriteLine("Enter password:");
            _builder.UserID = username;
            _builder.Password = Console.ReadLine();
            Console.WriteLine("Using SQL Server Authentication");
        }

        public void UseWindowsAuthentication()
        {
            _builder.IntegratedSecurity = true;
            Console.WriteLine("Using Windows Authentication");
        }

        public void Deploy(string targetDatabaseName)
        {
            Console.WriteLine($"Deploying to database '{targetDatabaseName}'");
            var services = new DacServices(_builder.ConnectionString);
            services.Deploy(_package, targetDatabaseName, true, _deployOptions);
            Console.WriteLine($"Succesfully deployed database '{targetDatabaseName}'");
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
                    "DatabaseSpecification" => throw new NotSupportedException(),
                    "DeployDatabaseInSingleUserMode" => bool.Parse(value),
                    "DisableAndReenableDdlTriggers" => bool.Parse(value),
                    "DoNotAlterChangeDataCaptureObjects" => bool.Parse(value),
                    "DoNotAlterReplicatedObjects" => bool.Parse(value),
                    "DoNotDropObjectTypes" => throw new NotSupportedException(),
                    "DropConstraintsNotInSource" => bool.Parse(value),
                    "DropDmlTriggersNotInSource" => bool.Parse(value),
                    "DropExtendedPropertiesNotInSource" => bool.Parse(value),
                    "DropIndexesNotInSource" => bool.Parse(value),
                    "DropObjectsNotInSource" => bool.Parse(value),
                    "DropPermissionsNotInSource" => bool.Parse(value),
                    "DropRoleMembersNotInSource" => bool.Parse(value),
                    "DropStatisticsNotInSource" => bool.Parse(value),
                    "ExcludeObjectTypes" => throw new NotSupportedException(),
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
                property.SetValue(_deployOptions, propertyValue);

                Console.WriteLine($"Setting property {key} to value {value}");
            }
            catch (FormatException)
            {
                throw new ArgumentException($"Unable to parse value for property with name {key}: {value}", nameof(value));
            }
        }

        public void Dispose()
        {
            _package?.Dispose();
            _package = null;
        }

        private void EnsurePackageLoaded()
        {
            if (_package == null)
            {
                throw new InvalidOperationException("Package has not been loaded. Call LoadPackage first.");
            }
        }
    }
}
