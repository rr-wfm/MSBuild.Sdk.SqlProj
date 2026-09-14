using System;
using System.IO;
using System.IO.Packaging;
using System.Reflection;
using Microsoft.SqlServer.Dac.Model;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    /// <summary>
    /// Modifies the Origin.xml part of a .dacpac package to set the ProjectGuid,
    /// using reflection against the internal Microsoft.Data.Tools.Schema.Sql.Dac.DacOrigin type.
    /// </summary>
    public sealed class DacOriginModifier
    {
        private const string OriginPartUri = "/Origin.xml";
        private const string DacOriginTypeName = "Microsoft.Data.Tools.Schema.Sql.Dac.DacOrigin";

        private readonly IConsole _console;

        public DacOriginModifier(IConsole console)
        {
            _console = console ?? throw new ArgumentNullException(nameof(console));
        }

        public void SetProjectGuid(Package package, Guid projectGuid)
        {
            ArgumentNullException.ThrowIfNull(package);

            var dacOriginType = ResolveDacOriginType();

            var openMethod = dacOriginType.GetMethod(
                "Open",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Stream) },
                modifiers: null)
                ?? throw new InvalidOperationException($"Unable to locate the static Open(Stream) method on {DacOriginTypeName}.");

            var writeMethod = dacOriginType.GetMethod(
                "Write",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                types: new[] { typeof(Stream) },
                modifiers: null)
                ?? throw new InvalidOperationException($"Unable to locate the Write(Stream) method on {DacOriginTypeName}.");

            var projectGuidProperty = dacOriginType.GetProperty(
                "ProjectGuid",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Unable to locate the ProjectGuid property on {DacOriginTypeName}.");

            _console.WriteLine($"Setting ProjectGuid {projectGuid} on {OriginPartUri} in package");

            var partUri = new Uri(OriginPartUri, UriKind.Relative);
            if (!package.PartExists(partUri))
            {
                throw new InvalidOperationException($"Unable to find {OriginPartUri} in package.");
            }

            var part = package.GetPart(partUri);

            // Read the existing Origin.xml into an in-memory copy so the part stream can be rewritten.
            object dacOrigin;
            using (var originStream = part.GetStream(FileMode.Open, FileAccess.Read))
            using (var buffer = new MemoryStream())
            {
                originStream.CopyTo(buffer);
                buffer.Position = 0;
                dacOrigin = openMethod.Invoke(null, new object[] { buffer })
                    ?? throw new InvalidOperationException($"Unable to read {OriginPartUri} from package.");
            }

            projectGuidProperty.SetValue(dacOrigin, projectGuid);

            using var output = new MemoryStream();
            writeMethod.Invoke(dacOrigin, new object[] { output });

            // Overwrite the part contents with the updated Origin.xml.
            using var partStream = part.GetStream(FileMode.Create, FileAccess.Write);
            output.Position = 0;
            output.CopyTo(partStream);
        }

        private static Type ResolveDacOriginType()
        {
            // Ensure the assembly containing DacOrigin is loaded by touching a public type from it.
            _ = typeof(TSqlModel);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(DacOriginTypeName, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException(
                $"Unable to resolve internal type {DacOriginTypeName}. The referenced version of Microsoft.SqlServer.DacFx may be incompatible.");
        }
    }
}
