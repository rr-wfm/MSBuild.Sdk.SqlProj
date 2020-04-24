using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace MSBuild.Sdk.SqlProj.BuildDacpac.Tests.DacpacHeaderParser
{
    public class HeaderParser
    {
        private readonly string _dacPacPath;

        public HeaderParser(string dacPacPath)
        {
            _dacPacPath = dacPacPath;
        }

        public bool HasCustomData(string category, string type, string name, string value)
        {
            return GetCustomData()
                .Where(d => d.Category == "SqlCmdVariables"
                    && d.Type == "SqlCmdVariable")
                .SelectMany(d => d.Items)
                .Where(i => i.Name == name
                    && i.Value == value)
                .ToList().Count == 1;
        }

        public List<CustomData> GetCustomData()
        {
            var dac = new DacPacXml(_dacPacPath);
            var xml = dac.GetXml("Model.xml");

            var reader = XmlReader.Create(new StringReader(xml));
            reader.MoveToContent();

            var data = new List<CustomData>();
            CustomData currentCustomData = null;

            while (reader.Read())
            {
                if (reader.Name == "CustomData" && reader.NodeType == XmlNodeType.Element)
                {
                    var cat = reader.GetAttribute("Category");
                    var type = reader.GetAttribute("Type");

                    currentCustomData = new CustomData(cat, type);
                    data.Add(currentCustomData);
                }

                if (reader.Name == "Metadata" && reader.NodeType == XmlNodeType.Element)
                {
                    var name = reader.GetAttribute("Name");
                    var value = reader.GetAttribute("Value");

                    currentCustomData.AddMetadata(name, value);
                }

                if (reader.Name == "Header" && reader.NodeType == XmlNodeType.EndElement)
                {
                    break; //gone too far
                }
            }
            dac.Close();

            return data;
        }
    }
}
