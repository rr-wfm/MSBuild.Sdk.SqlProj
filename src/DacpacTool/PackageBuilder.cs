using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public sealed class PackageBuilder : IDisposable
    {
        private readonly IConsole _console;
        private bool? _modelValid;

        private List<int> _suppressedWarnings = new ();
        private Dictionary<string,List<int>> _suppressedFileWarnings = new Dictionary<string, List<int>>(StringComparer.InvariantCultureIgnoreCase);

        public PackageBuilder(IConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public void UsingVersion(SqlServerVersion version)
        {
            Model = new TSqlModel(version, Options);
            _console.WriteLine($"Using SQL Server version {version}");
        }

        public void AddReference(string referenceFile, string externalParts = null, bool suppressErrorsForMissingDependencies = false)
        {
            // Ensure that the model has been created
            EnsureModelCreated();

            ValidateReference(referenceFile);

            _console.WriteLine($"Adding reference to {referenceFile} with external parts {externalParts} and SuppressMissingDependenciesErrors {suppressErrorsForMissingDependencies}");
            Model.AddReference(referenceFile, externalParts, suppressErrorsForMissingDependencies);
        }

        private static void ValidateReference(string referenceFile)
        {
            // Make sure the file exists
            if (!File.Exists(referenceFile))
            {
                throw new ArgumentException($"Unable to find reference file {referenceFile}", nameof(referenceFile));
            }

            // Make sure the file is a .dacpac file
            string fileType = Path.GetExtension(referenceFile);
            if (!fileType.Equals(".dacpac", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Invalid filetype {fileType}, was expecting .dacpac", nameof(referenceFile));
            }
        }

        public void AddSqlCmdVariables(string[] variables)
        {
            // Ensure that the model has been created
            EnsureModelCreated();

            Model.AddSqlCmdVariables(variables);
        }

        public bool AddInputFile(FileInfo inputFile)
        {
            ArgumentNullException.ThrowIfNull(inputFile);

            // Ensure that the model has been created
            EnsureModelCreated();

            // Make sure the file exists
            if (!inputFile.Exists)
            {
                throw new ArgumentException($"Unable to find input file {inputFile}", nameof(inputFile));
            }

            _console.WriteLine($"Adding {inputFile.FullName} to the model");

            try
            {
                TSqlObjectOptions sqlObjectOptions = new TSqlObjectOptions();
                Model.AddOrUpdateObjects(File.ReadAllText(inputFile.FullName), inputFile.FullName, new TSqlObjectOptions());
                return true;
            }
            catch (DacModelException dex)
            {
                _console.WriteLine(dex.Format(inputFile.Name));
                return false;
            }
        }

        public void AddPreDeploymentScript(FileInfo script, FileInfo outputFile)
        {
            ArgumentNullException.ThrowIfNull(outputFile);

            AddScript(script, outputFile, "/predeploy.sql");
        }

        public void AddPostDeploymentScript(FileInfo script, FileInfo outputFile)
        {
            ArgumentNullException.ThrowIfNull(outputFile);

            AddScript(script, outputFile, "/postdeploy.sql");
        }

        public bool ValidateModel()
        {
            // Ensure that the model has been created
            EnsureModelCreated();

            // Validate the model and write out validation messages
            var modelErrors = Model.GetModelValidationErrors(Enumerable.Empty<string>());
            int validationErrors = 0;
            foreach (var modelError in modelErrors)
            {
                if (modelError.Severity == ModelErrorSeverity.Error)
                {
                    validationErrors++;
                    _console.WriteLine(modelError.GetOutputMessage(modelError.Severity));
                }
                else if (modelError.Severity == ModelErrorSeverity.Warning)
                {
                    ProcessWarning(modelError);
                }
                else
                {
                    _console.WriteLine(modelError.GetOutputMessage(modelError.Severity));
                }
            }

            if (validationErrors > 0)
            {
                _modelValid = false;
                _console.WriteLine($"Found {validationErrors} error(s), skip building package");
            }
            else
            {
                _modelValid = true;
            }

            return _modelValid.Value;

            void ProcessWarning(ModelValidationError modelError)
            {
                if (_suppressedWarnings.Contains(modelError.ErrorCode))
                    return;

                if (_suppressedFileWarnings.TryGetValue(modelError.SourceName, out var suppressedFileWarnings) && suppressedFileWarnings.Contains(modelError.ErrorCode))
                    return;

                if (TreatTSqlWarningsAsErrors)
                {
                    validationErrors++;
                }

                _console.WriteLine(modelError.GetOutputMessage(TreatTSqlWarningsAsErrors
                    ? ModelErrorSeverity.Error
                    : ModelErrorSeverity.Warning));
            }
        }

        public void SaveToDisk(FileInfo outputFile, PackageOptions packageOptions = null)
        {
            ArgumentNullException.ThrowIfNull(outputFile);

            // Ensure that the model has been created and metadata has been set
            EnsureModelCreated();
            EnsureModelValidated();
            EnsureMetadataCreated();

            // Check if the file already exists
            if (outputFile.Exists)
            {
                // Delete the existing file
                _console.WriteLine($"Deleting existing file {outputFile.FullName}");
                outputFile.Delete();
            }

            _console.WriteLine($"Writing model to {outputFile.FullName}");
            DacPackageExtensions.BuildPackage(outputFile.FullName, Model, Metadata, packageOptions ?? new PackageOptions { });
        }

        public void SetMetadata(string name, string version)
        {
            Metadata = new PackageMetadata
            {
                Name = name,
                Version = version,
            };

            _console.WriteLine($"Using package name {name} and version {version}");
        }

        public void SetProperty(string key, string value)
        {
            try
            {
                // Convert value into the appropriate type depending on the key
                object propertyValue = key switch
                {
                    "QueryStoreIntervalLength" => int.Parse(value, CultureInfo.InvariantCulture),
                    "QueryStoreFlushInterval" => int.Parse(value, CultureInfo.InvariantCulture),
                    "QueryStoreDesiredState" => Enum.Parse<QueryStoreDesiredState>(value),
                    "QueryStoreCaptureMode" => Enum.Parse<QueryStoreCaptureMode>(value),
                    "ParameterizationOption" => Enum.Parse<ParameterizationOption>(value),
                    "PageVerifyMode" => Enum.Parse<PageVerifyMode>(value),
                    "QueryStoreMaxStorageSize" => int.Parse(value, CultureInfo.InvariantCulture),
                    "NumericRoundAbortOn" => bool.Parse(value),
                    "NestedTriggersOn" => bool.Parse(value),
                    "HonorBrokerPriority" => bool.Parse(value),
                    "FullTextEnabled" => bool.Parse(value),
                    "FileStreamDirectoryName" => value,
                    "DbScopedConfigQueryOptimizerHotfixesSecondary" => bool.Parse(value),
                    "DbScopedConfigQueryOptimizerHotfixes" => bool.Parse(value),
                    "NonTransactedFileStreamAccess" => Enum.Parse<NonTransactedFileStreamAccess>(value),
                    "DbScopedConfigParameterSniffingSecondary" => bool.Parse(value),
                    "QueryStoreMaxPlansPerQuery" => int.Parse(value, CultureInfo.InvariantCulture),
                    "QuotedIdentifierOn" => bool.Parse(value),
                    "VardecimalStorageFormatOn" => bool.Parse(value),
                    "TwoDigitYearCutoff" => short.Parse(value, CultureInfo.InvariantCulture),
                    "Trustworthy" => bool.Parse(value),
                    "TransformNoiseWords" => bool.Parse(value),
                    "TornPageProtectionOn" => bool.Parse(value),
                    "TargetRecoveryTimeUnit" => Enum.Parse<TimeUnit>(value),
                    "QueryStoreStaleQueryThreshold" => int.Parse(value, CultureInfo.InvariantCulture),
                    "TargetRecoveryTimePeriod" => int.Parse(value, CultureInfo.InvariantCulture),
                    "ServiceBrokerOption" => Enum.Parse<ServiceBrokerOption>(value),
                    "RecursiveTriggersOn" => bool.Parse(value),
                    "DelayedDurabilityMode" => Enum.Parse<DelayedDurabilityMode>(value),
                    "RecoveryMode" => Enum.Parse<RecoveryMode>(value),
                    "ReadOnly" => bool.Parse(value),
                    "SupplementalLoggingOn" => bool.Parse(value),
                    "DbScopedConfigParameterSniffing" => bool.Parse(value),
                    "DbScopedConfigMaxDOPSecondary" => int.Parse(value, CultureInfo.InvariantCulture),
                    "DbScopedConfigMaxDOP" => int.Parse(value, CultureInfo.InvariantCulture),
                    "AutoShrink" => bool.Parse(value),
                    "AutoCreateStatisticsIncremental" => bool.Parse(value),
                    "AutoCreateStatistics" => bool.Parse(value),
                    "AutoClose" => bool.Parse(value),
                    "ArithAbortOn" => bool.Parse(value),
                    "AnsiWarningsOn" => bool.Parse(value),
                    "AutoUpdateStatistics" => bool.Parse(value),
                    "AnsiPaddingOn" => bool.Parse(value),
                    "AnsiNullDefaultOn" => bool.Parse(value),
                    "MemoryOptimizedElevateToSnapshot" => bool.Parse(value),
                    "TransactionIsolationReadCommittedSnapshot" => bool.Parse(value),
                    "AllowSnapshotIsolation" => bool.Parse(value),
                    "Collation" => value,
                    "AnsiNullsOn" => bool.Parse(value),
                    "AutoUpdateStatisticsAsync" => bool.Parse(value),
                    "CatalogCollation" => Enum.Parse<CatalogCollation>(value),
                    "ChangeTrackingAutoCleanup" => bool.Parse(value),
                    "DbScopedConfigLegacyCardinalityEstimationSecondary" => bool.Parse(value),
                    "DbScopedConfigLegacyCardinalityEstimation" => bool.Parse(value),
                    "DBChainingOn" => bool.Parse(value),
                    "DefaultLanguage" => value,
                    "DefaultFullTextLanguage" => value,
                    "DateCorrelationOptimizationOn" => bool.Parse(value),
                    "DatabaseStateOffline" => bool.Parse(value),
                    "CursorDefaultGlobalScope" => bool.Parse(value),
                    "CursorCloseOnCommit" => bool.Parse(value),
                    "Containment" => Enum.Parse<Containment>(value),
                    "ConcatNullYieldsNull" => bool.Parse(value),
                    "CompatibilityLevel" => int.Parse(value, CultureInfo.InvariantCulture),
                    "ChangeTrackingRetentionUnit" => Enum.Parse<TimeUnit>(value),
                    "ChangeTrackingRetentionPeriod" => int.Parse(value, CultureInfo.InvariantCulture),
                    "ChangeTrackingEnabled" => bool.Parse(value),
                    "UserAccessOption" => Enum.Parse<UserAccessOption>(value),
                    "WithEncryption" => bool.Parse(value),
                    _ => throw new ArgumentException($"Unknown property with name {key}", nameof(key))
                };

                PropertyInfo property = typeof(TSqlModelOptions).GetProperty(key, BindingFlags.Public | BindingFlags.Instance);

                if (property == null)
                {
                    throw new ArgumentException($"Property with name {key} not found", nameof(key));
                }

                property.SetValue(Options, propertyValue);

                _console.WriteLine($"Setting property {key} to value {value}");
            }
            catch (FormatException)
            {
                throw new ArgumentException($"Unable to parse value for property with name {key}: {value}", nameof(value));
            }
        }

        public void Dispose()
        {
            Model?.Dispose();
            Model = null;
        }

        public TSqlModelOptions Options { get; } = new TSqlModelOptions();
        public TSqlModel Model { get; private set; }

        public PackageMetadata Metadata { get; private set; }

        private void EnsureModelCreated()
        {
            if (Model == null)
            {
                throw new InvalidOperationException("Model has not been initialized. Call UsingVersion first.");
            }
        }

        private void EnsureMetadataCreated()
        {
            if (Metadata == null)
            {
                throw new InvalidOperationException("Package metadata has not been initialized. Call SetMetadata first.");
            }
        }

        private void EnsureModelValidated()
        {
            if (_modelValid == null)
            {
                throw new InvalidOperationException("Model has not been validated. Call ValidateModel first.");
            }
        }

        private void AddScript(FileInfo script, FileInfo outputFile, string path)
        {
            if (_modelValid != true)
            {
                throw new InvalidOperationException("Cannot add pre and post scripts before model has been validated.");
            }

            if (script == null)
            {
                return;
            }

            if (!script.Exists)
            {
                throw new ArgumentException($"Unable to find script file {script.FullName}", nameof(script));
            }

            using (var package = Package.Open(outputFile.FullName, FileMode.Open, FileAccess.ReadWrite))
            {
                _console.WriteLine($"Adding {script.FullName} to package");
                WritePart(script, package, path);

                package.Close();
            }
        }

        private static void WritePart(FileInfo file, Package package, string path)
        {
            var part = package.CreatePart(new Uri(path, UriKind.Relative), "text/plain");

            using (var stream = part.GetStream())
            {
                using var parser = new ScriptParser(file.FullName, new IncludeVariableResolver());
                var buffer = Encoding.UTF8.GetBytes(parser.GenerateScript());
                stream.Write(buffer, 0, buffer.Length);
            }
        }

        public bool TreatTSqlWarningsAsErrors { get; set; }

        private static readonly char[] separator = new [] { ',', ';' };

        public void AddWarningsToSuppress(string suppressionList)
        {
            _suppressedWarnings.AddRange(ParseSuppressionList(suppressionList));
        }

        public void AddFileWarningsToSuppress(FileInfo inputFile, string suppressionList)
        {
            ArgumentNullException.ThrowIfNull(inputFile);

            var warningList = ParseSuppressionList(suppressionList);
            if (warningList.Count > 0)
            {
                if (!_suppressedFileWarnings.TryGetValue(inputFile.FullName, out var list))
                {
                    _suppressedFileWarnings.Add(inputFile.FullName, warningList);
                }
                else
                {
                    list.AddRange(warningList.FindAll((x) => !list.Contains(x)));
                }
            }

        }

        private static List<int> ParseSuppressionList(string suppressionList)
        {
            var result = new List<int>();
            if (!string.IsNullOrEmpty(suppressionList))
            {
                foreach (var str in suppressionList.Split(separator, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(str.Trim(), out var value))
                    {
                        result.Add(value);
                    }
                }
            }

            return result;
        }

        public void GenerateCreateScript(FileInfo dacpacFile, string databaseName, DacDeployOptions deployOptions)
        {
            ArgumentNullException.ThrowIfNull(dacpacFile);

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("The database name is mandatory.", nameof(databaseName));
            }

            var scriptFileName = $"{databaseName}_Create.sql";
            _console.WriteLine($"Generating create script {scriptFileName}");

            using var package = DacPackage.Load(dacpacFile.FullName);

            if (package == null)
            {
                throw new InvalidOperationException($"Unable to load package {dacpacFile.FullName}");
            }

            if (dacpacFile.DirectoryName == null)
            {
                throw new InvalidOperationException($"Unable to determine directory for package {dacpacFile.FullName}");
            }

            using var file = File.Create(Path.Combine(dacpacFile.DirectoryName, scriptFileName));

            DacServices.GenerateCreateScript(file, package, databaseName, deployOptions);
        }
    }
}
