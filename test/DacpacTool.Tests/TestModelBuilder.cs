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

        public TestModelBuilder AddView(string view, string body)
        {
            var viewDefinition = $"CREATE VIEW [{view}] AS {body}";
            sqlModel.AddObjects(viewDefinition);
            return this;
        }

        public TestModelBuilder AddStoredProcedureFromFile(string filename)
        {
            sqlModel.AddOrUpdateObjects(File.ReadAllText(filename), filename, new TSqlObjectOptions());
            return this;
        }

        public TestModelBuilder AddReference(string path, string externalParts = "", bool suppressErrors = false)
        {
            sqlModel.AddReference(path, externalParts, suppressErrors);
            return this;
        }

        public TestModelBuilder AddSqlCmdVariables(string[] variables)
        {
            foreach (var variable in variables)
            {
                var varWithValue = variable.Split('=', 2);
                var variableName = varWithValue[0];
                var variableValue = string.Empty;
                
                if (varWithValue.Length > 1 && varWithValue[1] != string.Empty)
                    variableValue = varWithValue[1];

                sqlModel.AddSqlCmdVariable(variableName, variableValue);
            }
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
