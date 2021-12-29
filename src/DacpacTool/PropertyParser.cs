using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.SqlServer.Dac;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    /// <summary>
    /// Allows to parse properties from build or deploy options
    /// </summary>
    public static class PropertyParser
    {
        private static readonly IDictionary<string, Func<string, object>> CustomParsers = new Dictionary<string, Func<string, object>>();

        static PropertyParser()
        {
            CustomParsers.Add("DoNotDropObjectTypes", ParseObjectTypes);
            CustomParsers.Add("ExcludeObjectTypes", ParseObjectTypes);
            CustomParsers.Add("DatabaseSpecification", ParseDatabaseSpecification);
        }

        /// <summary>
        /// Converts the <see cref="BuildOptions"/> to a <see cref="DacDeployOptions"/> object with all properties
        /// </summary>
        /// <param name="options">The build options</param>
        /// <returns>The <see cref="DacDeployOptions"/> object</returns>
        public static DacDeployOptions ToDacDeployOptions(this BuildOptions options)
        {
            var deployOptions = new DacDeployOptions();

            if (options.DeployProperty != null)
            {
                foreach (var buildProperty in options.DeployProperty.Where(p => string.IsNullOrWhiteSpace(p) == false))
                {
                    var databaseProperty = DatabaseProperty.Create(buildProperty);
                    deployOptions.SetDeployProperty(databaseProperty.Name, databaseProperty.Value);
                }
            }

            return deployOptions;
        }

        private static PropertyInfo GetDacDeployOptionsProperty(string propertyName)
        {
            var property = typeof(DacDeployOptions).GetProperties()
                .SingleOrDefault(p => string.Compare(p.Name, propertyName, StringComparison.OrdinalIgnoreCase) == 0);

            return property;
        }

        public static ObjectType[] ParseObjectTypes(string value)
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

        public static DacAzureDatabaseSpecification ParseDatabaseSpecification(string value)
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

        public static object SetDeployProperty(this DacDeployOptions deployOptions, string name, string value)
        {
            object propertyValue;
            var property = GetDacDeployOptionsProperty(name);

            if (property == null)
            {
                throw new ArgumentException($@"Unknown property with name {name}", nameof(name));
            }

            if (CustomParsers.TryGetValue(property.Name, out var parser))
            {
                propertyValue = parser.Invoke(value);
            }
            else
            {
                propertyValue =  StringToTypedValue(value, property.PropertyType);
            }

            if (propertyValue != null)
            {
                property.SetValue(deployOptions, propertyValue);
            }

            return propertyValue;
        }

        private static object StringToTypedValue(string stringValue, Type targetType)
        {
            object result = null;

            var converter = TypeDescriptor.GetConverter(targetType);

            if (converter.CanConvertFrom(typeof(string)))
            {
                result = converter.ConvertFromString(null, CultureInfo.InvariantCulture, stringValue);
            }

            return result;
        }
    }
}
