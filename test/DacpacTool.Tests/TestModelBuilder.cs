using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    internal class TestModelBuilder
    {
        private TSqlModel sqlModel;
        
        public TestModelBuilder()
        {
            sqlModel = new TSqlModel(SqlServerVersion.Sql110, new TSqlModelOptions
            {
                AnsiNullsOn = true,
                Collation = "SQL_Latin1_General_CP1_CI_AI",
                CompatibilityLevel = 110,
                QuotedIdentifierOn = true,
            });
        }

        public TestModelBuilder AddTable(string tableName, params (string name, string type)[] columns)
        {
            var columnsDefinition = string.Join(",", columns.Select(column => $"{column.name} {column.type}"));
            var tableDefinition = $"CREATE TABLE [{tableName}] ({columnsDefinition});";
            sqlModel.AddObjects(tableDefinition);
            return this;
        }

        public TestModelBuilder AddStoredProcedure(string procName, string body)
        {
            var procDefinition = $"CREATE PROCEDURE [{procName}] AS BEGIN {body} END";
            sqlModel.AddObjects(procDefinition);
            return this;
        }

        public TestModelBuilder AddStoredProcedureFromFile(string filename)
        {
            sqlModel.AddOrUpdateObjects(File.ReadAllText(filename), filename, new TSqlObjectOptions());
            return this;
        }

        public TestModelBuilder AddReference(string path)
        {
            sqlModel.AddReference(path, string.Empty);
            return this;
        }

        public TSqlModel Build()
        {
            return sqlModel;
        }

        public string SaveAsPackage(string extension = ".dacpac")
        {
            var tempfilename = Path.GetTempFileName();
            var filename = Path.ChangeExtension(tempfilename, extension);
            DacPackageExtensions.BuildPackage(filename, sqlModel, new PackageMetadata());
            return filename;
        }
    }
}
