using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    internal static class PublishProfileWriter
    {
        internal static void Create(CreatePublishProfileOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(options.Output);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.TargetServerName);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.TargetDatabaseName);

            var server = options.TargetServerName;
            if (options.TargetPort.HasValue)
            {
                if (options.TargetPort < 1 || options.TargetPort > 65535)
                {
                    throw new ArgumentException("Target port must be between 1 and 65535.", nameof(options));
                }
                var separator = server.LastIndexOf(',');
                if (separator >= 0)
                {
                    server = server[..separator];
                }
                server = $"{server},{options.TargetPort.Value}";
            }

            // Build from separate fields, never interpolate a connection string or accept secrets.
            var connection = new SqlConnectionStringBuilder
            {
                DataSource = server,
                IntegratedSecurity = true,
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = false,
                PersistSecurityInfo = false,
            };
            XNamespace ns = "http://schemas.microsoft.com/developer/msbuild/2003";
            var document = new XDocument(new XElement(ns + "Project",
                new XAttribute("ToolsVersion", "4.0"),
                new XElement(ns + "PropertyGroup",
                    new XElement(ns + "ProfileVersionNumber", "1"),
                    new XElement(ns + "TargetDatabaseName", options.TargetDatabaseName),
                    new XElement(ns + "TargetConnectionString", connection.ConnectionString),
                    new XElement(ns + "BlockOnPossibleDataLoss", "True"),
                    new XElement(ns + "DropObjectsNotInSource", "False"))));

            // Serialize completely before touching the destination, including XML character validation.
            using var content = new MemoryStream();
            document.Save(content);
            content.Position = 0;
            var temporaryPath = Path.Combine(options.Output.DirectoryName ?? throw new ArgumentException("Output must have a parent directory.", nameof(options)), $".{Guid.NewGuid():N}.tmp");
            try
            {
                using (var file = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    content.CopyTo(file);
                }
                // Same-directory rename: no check-then-create race and no partial overwritten profile.
                File.Move(temporaryPath, options.Output.FullName, options.Force);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
