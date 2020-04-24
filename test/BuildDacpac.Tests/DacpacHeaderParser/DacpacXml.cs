﻿using System;
using System.IO;
using System.Text;
using Microsoft.Data.Tools.Schema.Sql.Packaging;

namespace MSBuild.Sdk.SqlProj.BuildDacpac.Tests.DacpacHeaderParser
{
    public class DacPacXml : IDisposable
    {
        private readonly string _dacPath;
        private Package _package;

        public DacPacXml(string dacPath)
        {
            _dacPath = dacPath;
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

        public Stream GetStream(string fileName)
        {
            var part = _package.GetPart(new Uri(string.Format("/{0}", fileName), UriKind.Relative));
            var stream = part.GetStream();
            return stream;
        }

        public void SetXml(string fileName, string xml)
        {
            var part = _package.GetPart(new Uri(string.Format("/{0}", fileName), UriKind.Relative));
            var stream = part.GetStream();

            var bytes = Encoding.ASCII.GetBytes(xml);
            stream.SetLength(bytes.Length);
            stream.Write(bytes, 0, bytes.Length);

            _package.Close();
            _package = Package.Open(_dacPath, FileMode.Open, FileAccess.ReadWrite);
        }

        public void Close()
        {
            _package.Close();
        }
    }
}
