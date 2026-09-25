using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Microsoft.SqlServer.Dac;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    // A deliberately bounded literal profile contract, not an MSBuild evaluator or SqlPackage clone.
    // DacProfile.Load silently discards some writable DacDeployOptions, so consume every setting here.
    internal sealed class PublishProfileReader
    {
        private readonly HashSet<string> properties = new(StringComparer.OrdinalIgnoreCase);

        internal bool HasProperty(string name) => properties.Contains(name);

        public string TargetDatabaseName { get; private set; }
        public string TargetConnectionString { get; private set; }
        public DacDeployOptions DeployOptions { get; } = new DacDeployOptions();

        internal static PublishProfileReader Load(string path)
        {
            using var reader = XmlReader.Create(path, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var document = XDocument.Load(reader);
            var root = document.Root;
            XNamespace ns = root?.Name.Namespace ?? XNamespace.None;
            if (root == null || root.Name.LocalName != "Project" ||
                (ns != XNamespace.None && ns != "http://schemas.microsoft.com/developer/msbuild/2003"))
            {
                throw new ArgumentException("Publish profile must have a Project root in the MSBuild namespace or no namespace.");
            }
            CheckAttributes(root, "ToolsVersion");
            CheckContainer(root);
            var result = new PublishProfileReader();
            foreach (var group in root.Elements())
            {
                CheckAttributes(group);
                CheckContainer(group);
                if (group.Name == ns + "PropertyGroup")
                {
                    foreach (var property in group.Elements())
                    {
                        CheckLeaf(property);
                        var name = property.Name.LocalName;
                        if (property.Name.Namespace != ns || !result.properties.Add(name))
                        {
                            throw new ArgumentException($"Duplicate or foreign publish profile property '{name}'.");
                        }
                        switch (name)
                        {
                            case "TargetDatabaseName": result.TargetDatabaseName = property.Value; break;
                            case "TargetConnectionString": result.TargetConnectionString = property.Value; break;
                            case "ProfileVersionNumber":
                                if (property.Value.Trim() != "1")
                                {
                                    throw new ArgumentException("Unsupported publish profile ProfileVersionNumber; expected 1.");
                                }
                                break;
                            default:
                                try
                                {
                                    result.DeployOptions.SetDeployProperty(name, property.Value);
                                }
                                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or TargetInvocationException or OverflowException or FormatException)
                                {
                                    // Values may contain secrets. Do not echo them or the underlying parser error.
                                    throw new ArgumentException($"Unsupported or invalid publish profile property '{name}'. Use a writable DacDeployOptions property in the documented CLI format.");
                                }
                                break;
                        }
                    }
                }
                else if (group.Name == ns + "ItemGroup")
                {
                    foreach (var variable in group.Elements())
                    {
                        if (variable.Name != ns + "SqlCmdVariable")
                        {
                            throw new ArgumentException($"Unsupported publish profile item '{variable.Name.LocalName}'.");
                        }
                        CheckAttributes(variable, "Include");
                        CheckContainer(variable);
                        var name = (string)variable.Attribute("Include");
                        var values = variable.Elements().ToArray();
                        if (string.IsNullOrWhiteSpace(name) || values.Length != 1 || values[0].Name != ns + "Value")
                        {
                            throw new ArgumentException("Publish profile SqlCmdVariable requires Include and exactly one Value element.");
                        }
                        CheckLeaf(values[0]);
                        if (!result.DeployOptions.SqlCommandVariableValues.TryAdd(name, values[0].Value))
                        {
                            throw new ArgumentException($"Duplicate publish profile SQLCMD variable '{name}'.");
                        }
                    }
                }
                else
                {
                    throw new ArgumentException($"Unsupported publish profile element '{group.Name.LocalName}'. Only literal PropertyGroup and ItemGroup settings are supported.");
                }
            }
            return result;
        }

        private static void CheckAttributes(XElement element, string allowed = null)
        {
            foreach (var attribute in element.Attributes())
            {
                if (!attribute.IsNamespaceDeclaration && attribute.Name != allowed)
                {
                    throw new ArgumentException($"Unsupported publish profile attribute '{attribute.Name.LocalName}' on '{element.Name.LocalName}'.");
                }
            }
        }

        private static void CheckContainer(XElement element)
        {
            if (element.Nodes().OfType<XText>().Any(text => !string.IsNullOrWhiteSpace(text.Value)))
            {
                throw new ArgumentException($"Unexpected text in publish profile element '{element.Name.LocalName}'.");
            }
        }

        private static void CheckLeaf(XElement element)
        {
            CheckAttributes(element);
            if (element.HasElements)
            {
                throw new ArgumentException($"Publish profile setting '{element.Name.LocalName}' must contain only a literal value.");
            }
        }
    }
}
