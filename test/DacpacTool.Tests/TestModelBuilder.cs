using System.IO;
using System.Linq;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests;

internal class TestModelBuilder
{
    private readonly TSqlModel _sqlModel = new(SqlServerVersion.Sql110, new TSqlModelOptions
    {
        AnsiNullsOn = true,
        Collation = "SQL_Latin1_General_CP1_CI_AI",
        CompatibilityLevel = 110,
        QuotedIdentifierOn = true,
    });

    public TestModelBuilder AddTable(string tableName, params (string name, string type)[] columns)
    {
        var columnsDefinition = string.Join(",", columns.Select(column => $"{column.name} {column.type}"));
        var tableDefinition = $"CREATE TABLE [{tableName}] ({columnsDefinition});";
        _sqlModel.AddObjects(tableDefinition);
        return this;
    }

    public TestModelBuilder AddStoredProcedure(string procName, string body, string? fileName = null)
    {
        var procDefinition = $"CREATE PROCEDURE [{procName}] AS BEGIN {body} END";
        if (!string.IsNullOrEmpty(fileName))
        {
            _sqlModel.AddOrUpdateObjects(procDefinition, fileName, new TSqlObjectOptions());
        }
        else
        {
            _sqlModel.AddObjects(procDefinition);
        }
        return this;
    }

    public TestModelBuilder AddView(string view, string body)
    {
        var viewDefinition = $"CREATE VIEW [{view}] AS {body}";
        _sqlModel.AddObjects(viewDefinition);
        return this;
    }

    public TestModelBuilder AddStoredProcedureFromFile(string filename)
    {
        _sqlModel.AddOrUpdateObjects(File.ReadAllText(filename), filename, new TSqlObjectOptions());
        return this;
    }

    public TestModelBuilder AddReference(string path, string externalParts = "", bool suppressErrors = false)
    {
        _sqlModel.AddReference(path, externalParts, suppressErrors);
        return this;
    }

    public TestModelBuilder AddSqlCmdVariables(string[] variableNames)
    {
        _sqlModel.AddSqlCmdVariables(variableNames);
        return this;
    }

    public TSqlModel Build()
    {
        return _sqlModel;
    }

    public string SaveAsPackage(string extension = ".dacpac")
    {
        var tempfilename = Path.GetTempFileName();
        var filename = Path.ChangeExtension(tempfilename, extension);
        DacPackageExtensions.BuildPackage(filename, _sqlModel, new PackageMetadata());
        return filename;
    }
}
