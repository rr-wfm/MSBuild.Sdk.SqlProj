using System;
using System.IO;
using Microsoft.Data.Tools.Schema.Sql.Packaging;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests.DacpacHeaderParser
{
    public class DacPacXml : IDisposable
    {
        private readonly Package _package;

        public DacPacXml(string dacPath)
        {
            _package = Package.Open(dacPath, FileMode.Open, FileAccess.ReadWrite);
        }

        public void Dispose()
        {
            Close();
        }

        public string GetXml(string fileName)
        {
            var part = _package.GetPart(new Uri(string.Format("/{0}", fileName), UriKind.Relative));
            var stream = part.GetStream();

            return new StreamReader(stream).ReadToEnd();
        }

        public void Close()
        {
            _package.Close();
        }
    }
}
