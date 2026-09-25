using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class DirectDacpacReferenceTests
    {
        public TestContext TestContext { get; set; }

        private string directory;
        private string sdkDirectory;
        private string reference;
        private string project;

        [TestInitialize]
        public void Initialize()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
            sdkDirectory = Path.Combine(root, "src/MSBuild.Sdk.SqlProj/Sdk");
            directory = Path.Combine(root, "test/DacpacTool.Tests/obj/DirectReferences", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(directory, "references with spaces"));
            reference = Path.Combine(directory, "references with spaces", "Library.dacpac");
            project = Path.Combine(directory, "Consumer.csproj");
            using var model = new TSqlModel(SqlServerVersion.Sql160, new TSqlModelOptions());
            model.AddObjects("CREATE TABLE dbo.ReferencedTable (Id int NOT NULL);");
            DacPackageExtensions.BuildPackage(reference, model, new PackageMetadata { Name = "Library", Version = "1.0.0" });
        }

        [TestCleanup]
        public void Cleanup() => Directory.Delete(directory, true);

        [TestMethod]
        [DataRow("", "", "", false)]
        [DataRow("OtherDatabase", "", "", false)]
        [DataRow("", "OtherDatabaseName", "", false)]
        [DataRow("", "OtherDatabaseName", "ServerName", true)]
        [DataRow("OtherDatabase", "", "ServerName", true)]
        public async Task Build_ResolvesDirectReference(string database, string databaseVariable, string serverVariable, bool absolute)
        {
            var item = new XElement("ArtifactReference", new XAttribute("Include", "LogicalLibrary"),
                new XElement("HintPath", absolute ? reference : "references with spaces/Library.dacpac"),
                new XAttribute("DatabaseVariableLiteralValue", database),
                new XAttribute("DatabaseSqlCmdVariable", databaseVariable),
                new XAttribute("ServerSqlCmdVariable", serverVariable),
                new XAttribute("SuppressMissingDependenciesErrors", "True"));
            WriteProject(item);
            var qualifier = serverVariable.Length > 0 ? $"[$({serverVariable})]." : "";
            qualifier += databaseVariable.Length > 0 ? $"[$({databaseVariable})]." : database.Length > 0 ? $"[{database}]." : "";
            File.WriteAllText(Path.Combine(directory, "View.sql"), $"CREATE VIEW dbo.ConsumerView AS SELECT Id FROM {qualifier}[dbo].[ReferencedTable];");

            var result = await Build();
            result.ExitCode.ShouldBe(0, result.Output);
            result.Output.ShouldContain($"external parts dbl={database}|dbv={databaseVariable}|srv={serverVariable} and SuppressMissingDependenciesErrors True");
            File.Exists(Path.Combine(directory, "bin/Release/netstandard2.0/Library.dacpac")).ShouldBeTrue();
            using var package = DacPackage.Load(Path.Combine(directory, "bin/Release/netstandard2.0/Consumer.dacpac"));
            package.Name.ShouldBe("Consumer");
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task Build_WithoutHintPathUsesInclude(bool absolute, bool emptyHint)
        {
            var item = new XElement("ArtifactReference", new XAttribute("Include", absolute ? reference : "references with spaces/Library.dacpac"));
            if (emptyHint)
            {
                item.Add(new XElement("HintPath", ""));
            }
            WriteProject(item);
            var result = await Build();
            result.ExitCode.ShouldBe(0, result.Output);
            result.Output.ShouldContain($"Adding reference to {reference}");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Build_HintPathWinsOverDifferentInclude(bool includeExists)
        {
            var alternate = Path.Combine(directory, "Alternate.dacpac");
            if (includeExists)
            {
                // Invalid content: using Include instead of HintPath must fail compilation.
                File.WriteAllText(alternate, "Not a DACPAC");
            }
            WriteProject(new XElement("ArtifactReference", new XAttribute("Include", alternate), new XElement("HintPath", reference)));
            var result = await Build();
            result.ExitCode.ShouldBe(0, result.Output);
            result.Output.ShouldContain($"Adding reference to {reference}");
            File.Exists(Path.Combine(directory, "bin/Release/netstandard2.0/Alternate.dacpac")).ShouldBeFalse();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Build_MissingSelectedPathFailsWithoutFallback(bool hasHint)
        {
            var item = new XElement("ArtifactReference", new XAttribute("Include", hasHint ? reference : "Missing.dacpac"));
            if (hasHint)
            {
                item.Add(new XElement("HintPath", "Missing.dacpac"));
            }
            WriteProject(item);
            var result = await Build();
            result.ExitCode.ShouldNotBe(0, result.Output);
            result.Output.ShouldContain("ArtifactReference file does not exist:");
            result.Output.ShouldContain(Path.Combine(directory, "Missing.dacpac"));
        }

        [TestMethod]
        public async Task Build_MixedPrivateReferencesOnlyCopiesSelectedFiles()
        {
            var copiedReference = Path.Combine(directory, "Copied.dacpac");
            File.Copy(reference, copiedReference);
            WriteProject(new XElement("ArtifactReference", new XAttribute("Include", reference),
                new XElement("Private", "false"), new XElement("DatabaseVariableLiteralValue", "External")));
            var document = XDocument.Load(project);
            document.Root.Element("ItemGroup").Add(new XElement("ArtifactReference", new XAttribute("Include", copiedReference),
                new XElement("Private", "true"), new XElement("DatabaseVariableLiteralValue", "Copied")));
            document.Save(project);
            File.WriteAllText(Path.Combine(directory, "View.sql"),
                "CREATE VIEW dbo.ConsumerView AS SELECT Id FROM [External].dbo.ReferencedTable UNION ALL SELECT Id FROM Copied.dbo.ReferencedTable;");

            var build = await Build();
            build.ExitCode.ShouldBe(0, build.Output);
            var output = Path.Combine(directory, "bin/Release/netstandard2.0");
            File.Exists(Path.Combine(output, "Library.dacpac")).ShouldBeFalse();
            File.Exists(Path.Combine(output, "Copied.dacpac")).ShouldBeTrue();
            var copy = await Run("msbuild", project, "-t:GetCopyToOutputDirectoryItems", "-p:Configuration=Release",
                "-getItem:AllItemsFullPathWithTargetPath", "-nologo");
            copy.ExitCode.ShouldBe(0, copy.Output);
            copy.Output.ShouldNotContain("Library.dacpac");
            copy.Output.ShouldContain("Copied.dacpac");
        }

        [TestMethod]
        [DataRow(false, "", true)]
        [DataRow(true, "", true)]
        [DataRow(false, "true", true)]
        [DataRow(false, "false", false)]
        [DataRow(false, "False", false)]
        public async Task CopyTargets_RespectPrivateInFreshInstance(bool legacy, string privateValue, bool expectCopy)
        {
            var item = legacy
                ? new XElement("DacpacReference", new XAttribute("Include", "LegacyIdentity"), new XAttribute("DacpacFile", reference), new XAttribute("ExternalParts", "dbl=|dbv=|srv="))
                : new XElement("ArtifactReference", new XAttribute("Include", "LogicalLibrary"), new XElement("HintPath", "references with spaces/Library.dacpac"));
            if (privateValue.Length > 0)
            {
                item.Add(new XElement("Private", privateValue));
            }
            WriteProject(item);
            File.WriteAllText(Path.Combine(directory, "View.sql"), "CREATE VIEW dbo.ConsumerView AS SELECT Id FROM dbo.ReferencedTable;");
            var build = await Build();
            build.ExitCode.ShouldBe(0, build.Output);
            build.Output.ShouldContain($"Adding reference to {reference}");
            File.Exists(Path.Combine(directory, "bin/Release/netstandard2.0/Library.dacpac")).ShouldBe(expectCopy);
            foreach (var target in new[] { "CopyDacpacs", "GetCopyToOutputDirectoryItems" })
            {
                var result = await Run("msbuild", project, $"-t:{target}", "-p:Configuration=Release", "-getItem:AllItemsFullPathWithTargetPath", "-nologo");
                result.ExitCode.ShouldBe(0, result.Output);
                result.Output.Contains("Library.dacpac", StringComparison.Ordinal).ShouldBe(expectCopy, result.Output);
                result.Output.ShouldContain("Consumer.dacpac");
            }
        }

        [TestMethod]
        [DataRow(false, "", true)]
        [DataRow(true, "", true)]
        [DataRow(false, "true", true)]
        [DataRow(true, "true", true)]
        [DataRow(false, "false", false)]
        [DataRow(true, "false", false)]
        public async Task Build_DownstreamCSharpProjectRespectsPrivate(bool buildProjectReferences, string privateValue, bool expectCopy)
        {
            var item = new XElement("ArtifactReference", new XAttribute("Include", "LogicalLibrary"), new XElement("HintPath", "references with spaces/Library.dacpac"));
            if (privateValue.Length > 0)
            {
                item.Add(new XElement("Private", privateValue));
            }
            WriteProject(item);
            var downstream = Path.Combine(directory, "Downstream");
            Directory.CreateDirectory(downstream);
            var downstreamProject = Path.Combine(downstream, "Downstream.csproj");
            new XDocument(new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"),
                new XElement("PropertyGroup", new XElement("TargetFramework", "net8.0")),
                new XElement("ItemGroup", new XElement("ProjectReference", new XAttribute("Include", project),
                    new XAttribute("ReferenceOutputAssembly", "false"))))).Save(downstreamProject);
            File.WriteAllText(Path.Combine(downstream, "Class1.cs"), "public class Class1 { }");
            var initial = await Build();
            initial.ExitCode.ShouldBe(0, initial.Output);
            // A separate build must discover copy items without relying on metadata from Build.
            var result = await Run("build", downstreamProject, "-c", "Release", "-nologo", "-v:minimal",
                $"-p:BuildProjectReferences={buildProjectReferences}", $"-p:DacpacToolExe={typeof(Program).Assembly.Location}");
            result.ExitCode.ShouldBe(0, result.Output);
            var output = Path.Combine(downstream, "bin/Release/net8.0");
            File.Exists(Path.Combine(output, "Downstream.dll")).ShouldBeTrue();
            File.Exists(Path.Combine(output, "Consumer.dacpac")).ShouldBeTrue();
            File.Exists(Path.Combine(output, "Library.dacpac")).ShouldBe(expectCopy);
        }

        [TestMethod]
        [DataRow("true")]
        [DataRow("false")]
        public async Task Build_MissingReferenceFailsEvenWhenOutputIsUpToDate(string privateValue)
        {
            WriteProject(new XElement("ArtifactReference", new XAttribute("Include", "LogicalLibrary"), new XElement("HintPath", reference), new XElement("Private", privateValue)));
            var initial = await Build();
            initial.ExitCode.ShouldBe(0, initial.Output);
            File.Delete(reference);
            var result = await Build();
            result.ExitCode.ShouldNotBe(0, result.Output);
            result.Output.ShouldContain("ArtifactReference file does not exist:");
            result.Output.ShouldContain("Library.dacpac");
        }

        [TestMethod]
        [DataRow("true")]
        [DataRow("false")]
        public async Task Build_ChangedReferenceInvalidatesIncrementalBuild(string privateValue)
        {
            WriteProject(new XElement("ArtifactReference", new XAttribute("Include", "LogicalLibrary"), new XElement("HintPath", reference), new XElement("Private", privateValue)));
            var first = await Build();
            first.ExitCode.ShouldBe(0, first.Output);
            first.Output.ShouldContain("SuppressMissingDependenciesErrors False");
            var second = await Build();
            second.ExitCode.ShouldBe(0, second.Output);
            second.Output.ShouldNotContain("Adding reference to");
            File.SetLastWriteTimeUtc(reference, DateTime.UtcNow.AddSeconds(2));
            var third = await Build();
            third.ExitCode.ShouldBe(0, third.Output);
            third.Output.ShouldContain("Adding reference to");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Resolve_PreservesLegacyMetadataAndDoesNotDuplicateGeneratedReferences(bool copyFirst)
        {
            WriteProject(new XElement("DacpacReference", new XAttribute("Include", "LegacyIdentity"),
                new XAttribute("DacpacFile", reference), new XAttribute("ExternalParts", "dbl=Legacy|dbv=|srv=")));
            var document = XDocument.Load(project);
            document.Root.Add(new XElement("ItemGroup", new XElement("ArtifactReference", new XAttribute("Include", "ArtifactIdentity"),
                new XElement("HintPath", reference), new XElement("DatabaseVariableLiteralValue", "ArtifactDatabase"))));
            document.Root.Add(new XElement("Target", new XAttribute("Name", "SupplyProjectReference"),
                new XElement("ItemGroup", new XElement("_ResolvedProjectReferencePaths", new XAttribute("Include", reference), new XAttribute("DatabaseVariableLiteralValue", "ProjectDatabase")))));
            document.Root.Add(new XElement("ItemGroup",
                new XElement("PackageReference", new XAttribute("Include", "FakePackage"), new XAttribute("Version", "1.0"), new XAttribute("DatabaseSqlCmdVariable", "PackageDatabase"))));
            var packageDirectory = Path.Combine(directory, "package");
            Directory.CreateDirectory(Path.Combine(packageDirectory, "tools"));
            File.Copy(reference, Path.Combine(packageDirectory, "tools/FakePackage.dacpac"));
            document.Root.Add(new XElement("PropertyGroup", new XElement("PkgFakePackage", packageDirectory)));
            var targets = (copyFirst ? "CopyDacpacs;" : "") + "SupplyProjectReference;ResolveDatabaseReferences;ResolveDatabaseReferences;CopyDacpacs";
            document.Root.Add(new XElement("Target", new XAttribute("Name", "Snapshot"), new XAttribute("DependsOnTargets", targets),
                new XElement("WriteLinesToFile", new XAttribute("File", "references.txt"), new XAttribute("Overwrite", "true"),
                    new XAttribute("Lines", "@(DacpacReference->'%(Identity)|%(DacpacFile)|%(ExternalParts)')"))));
            document.Save(project);
            var result = await Run("msbuild", project, "-t:Snapshot", "-nologo");
            result.ExitCode.ShouldBe(0, result.Output);
            var lines = File.ReadAllLines(Path.Combine(directory, "references.txt"));
            lines.Length.ShouldBe(4);
            lines.Count(line => line.Contains("dbl=ArtifactDatabase|dbv=|srv=", StringComparison.Ordinal)).ShouldBe(1);
            lines.Count(line => line.Contains("dbl=Legacy|dbv=|srv=", StringComparison.Ordinal)).ShouldBe(1);
            lines.Count(line => line.Contains("dbl=ProjectDatabase|dbv=|srv=", StringComparison.Ordinal)).ShouldBe(1);
            lines.Count(line => line.Contains("dbl=|dbv=PackageDatabase|srv=", StringComparison.Ordinal)).ShouldBe(1);
        }

        [TestMethod]
        public async Task ScratchProjects_BothDirectReferencesResolveAndEachIsRequired()
        {
            var root = Path.GetFullPath(Path.Combine(sdkDirectory, "../../.."));
            var source = Path.Combine(root, "test/DirectDacpacReferences");
            var fixture = Path.Combine(directory, "ScratchProjects");
            // Copy only fixture inputs; every invocation owns its bin/obj, including across test hosts.
            foreach (var name in new[] { "ProducerA", "ProducerB", "Consumer" })
            {
                var destination = Path.Combine(fixture, name);
                Directory.CreateDirectory(destination);
                foreach (var sql in Directory.EnumerateFiles(Path.Combine(source, name), "*.sql", SearchOption.AllDirectories))
                {
                    var copy = Path.Combine(destination, Path.GetRelativePath(Path.Combine(source, name), sql));
                    Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    File.Copy(sql, copy);
                }
                var document = XDocument.Load(Path.Combine(source, name, name + ".csproj"));
                document.Root.Element("PropertyGroup").Element("TargetFramework").Value.ShouldBe("net10.0");
                ((string)document.Root.Element("PropertyGroup").Element("RunSqlCodeAnalysis")).ShouldBe("True");
                ((string)document.Root.Element("PropertyGroup").Element("TreatTSqlWarningsAsErrors")).ShouldBe("True");
                // Follow the template's default rule selection, not a fixture-specific override.
                document.Root.Descendants("CodeAnalysisRules").ShouldBeEmpty();
                var template = XDocument.Load(Path.Combine(root, "src/MSBuild.Sdk.SqlProj.Templates/templates/sqlproj/sqlproj.csproj"));
                var expectedPackages = template.Root.Descendants("PackageReference").ToArray();
                var actualPackages = document.Root.Descendants("PackageReference").ToArray();
                actualPackages.Length.ShouldBe(expectedPackages.Length);
                foreach (var expected in expectedPackages)
                {
                    var actual = actualPackages.Single(package => (string)package.Attribute("Include") == (string)expected.Attribute("Include"));
                    ((string)actual.Attribute("Version")).ShouldBe((string)expected.Attribute("Version"));
                    ((string)actual.Element("PrivateAssets")).ShouldBe((string)expected.Element("PrivateAssets"));
                    ((string)actual.Element("IncludeAssets")).ShouldBe((string)expected.Element("IncludeAssets"));
                }
                foreach (var import in document.Root.Elements("Import"))
                {
                    import.SetAttributeValue("Project", Path.Combine(sdkDirectory, Path.GetFileName(import.Attribute("Project").Value)));
                }
                document.Save(Path.Combine(destination, name + ".csproj"));
            }
            foreach (var name in new[] { "ProducerA", "ProducerB", "Consumer" })
            {
                var result = await BuildScratch(name);
                result.ExitCode.ShouldBe(0, result.Output);
                AssertAnalysis(name, result.Output);
            }

            var consumerOutput = Path.Combine(fixture, "Consumer/bin/Release/net10.0");
            File.Exists(Path.Combine(consumerOutput, "ProducerA.dacpac")).ShouldBeTrue();
            File.Exists(Path.Combine(consumerOutput, "ProducerB.dacpac")).ShouldBeTrue();
            using (var model = TSqlModel.LoadFromDacpac(Path.Combine(consumerOutput, "Consumer.dacpac"), new ModelLoadOptions(), TestContext.CancellationToken))
            {
                model.GetObjects(DacQueryScopes.UserDefined, View.TypeClass)
                    .Any(view => view.Name.ToString() == "[dbo].[AccountOrder]").ShouldBeTrue();
            }

            var missingA = await BuildScratch("Consumer", "-p:OmitProducerA=true");
            missingA.ExitCode.ShouldNotBe(0, missingA.Output);
            missingA.Output.ShouldContain("error SQL71561");
            missingA.Output.ShouldContain("[ProducerA].[dbo].[Account]");
            missingA.Output.ShouldContain("unresolved reference");
            missingA.Output.ShouldNotContain("Analyzing package");
            var missingB = await BuildScratch("Consumer", "-p:OmitProducerB=true");
            missingB.ExitCode.ShouldNotBe(0, missingB.Output);
            missingB.Output.ShouldContain("error SQL71561");
            missingB.Output.ShouldContain("[ProducerB].[dbo].[Order]");
            missingB.Output.ShouldContain("unresolved reference");
            missingB.Output.ShouldNotContain("Analyzing package");
            var final = await BuildScratch("Consumer");
            final.ExitCode.ShouldBe(0, final.Output);
            AssertAnalysis("Consumer", final.Output);

            void AssertAnalysis(string name, string output)
            {
                output.ShouldContain("Analyzing package");
                output.ShouldContain("Using analyzers: Microsoft.Rules.Data, SqlServer.Rules");
                output.ShouldNotContain("SQLPROJ0001");
                output.ShouldContain("Successfully analyzed package");
                output.ShouldNotContain("SQL71561");
                output.ShouldNotContain("SuppressMissingDependenciesErrors True");
                var report = XDocument.Load(Path.Combine(fixture, name, $"obj/Release/net10.0/{name}.CodeAnalysis.xml"));
                report.Root.Name.LocalName.ShouldBe("Problems");
                report.Root.Elements().ShouldBeEmpty();
                TestContext.WriteLine(output);
            }

            Task<(int ExitCode, string Output)> BuildScratch(string name, string option = "-p:OmitProducerA=false") =>
                Run("build", Path.Combine(fixture, name, name + ".csproj"), "-c", "Release", "-t:Rebuild", "-nologo", "-v:minimal", "-warnaserror",
                    $"-p:DacpacToolExe={typeof(Program).Assembly.Location}", "-p:TreatTSqlWarningsAsErrors=True", option);
        }

        private void WriteProject(XElement referenceItem)
        {
            new XDocument(new XElement("Project",
                new XElement("Import", new XAttribute("Project", Path.Combine(sdkDirectory, "Sdk.props"))),
                new XElement("PropertyGroup", new XElement("TargetFramework", "netstandard2.0"), new XElement("TreatTSqlWarningsAsErrors", "True")),
                new XElement("ItemGroup", referenceItem, new XElement("SqlCmdVariable", new XAttribute("Include", "OtherDatabaseName")), new XElement("SqlCmdVariable", new XAttribute("Include", "ServerName"))),
                new XElement("Import", new XAttribute("Project", Path.Combine(sdkDirectory, "Sdk.targets"))))).Save(project);
        }

        private Task<(int ExitCode, string Output)> Build() => Run("build", project, "-c", "Release", "-nologo", "-v:minimal", $"-p:DacpacToolExe={typeof(Program).Assembly.Location}");

        private async Task<(int ExitCode, string Output)> Run(params string[] arguments)
        {
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            using var process = Process.Start(startInfo);
            process.ShouldNotBeNull();
            var stdout = process.StandardOutput.ReadToEndAsync(TestContext.CancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(TestContext.CancellationToken);
            await process.WaitForExitAsync(TestContext.CancellationToken);
            return (process.ExitCode, await stdout + await stderr);
        }
    }
}
