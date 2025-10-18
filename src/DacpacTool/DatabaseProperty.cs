using System;

namespace MSBuild.Sdk.SqlProj.DacpacTool;

internal sealed class DatabaseProperty
{
    private DatabaseProperty(string name, string value)
    {
        this.Name = name;
        this.Value = value;
    }

    public string Name { get; }

    public string Value { get; }

    public static DatabaseProperty Create(string property)
    {
        var propertyKeyValuePair = property.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);

        if (propertyKeyValuePair.Length != 2)
        {
            throw new ArgumentException($"Unexpected number of parameters in property {property}");
        }

        return new DatabaseProperty(propertyKeyValuePair[0], propertyKeyValuePair[1]);
    }
}
