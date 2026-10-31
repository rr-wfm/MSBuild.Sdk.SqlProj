using System.Xml;

namespace MSBuild.Sdk.SqlProj.DacpacTool.UnitTests.DacpacHeaderParser
{
    public class HeaderParser
    {
        private readonly string _dacPacPath;

        public HeaderParser(string dacPacPath)
        {
            _dacPacPath = dacPacPath;
        }

        public List<CustomData> GetCustomData()
        {
            var dac = new DacPacXml(_dacPacPath);
            var xml = dac.GetXml("Model.xml");

            var reader = XmlReader.Create(new StringReader(xml));
            reader.MoveToContent();

            var data = new List<CustomData>();
            CustomData? currentCustomData = null;

            while (reader.Read())
            {
                if (reader.Name == "CustomData" && reader.NodeType == XmlNodeType.Element)
                {
                    var cat = reader.GetAttribute("Category")
                        ?? throw new InvalidDataException("Missing Category attribute.");
                    var type = reader.GetAttribute("Type")
                        ?? throw new InvalidDataException("Missing Type attribute.");

                    currentCustomData = new CustomData(cat, type);
                    data.Add(currentCustomData);
                }

                if (reader.Name == "Metadata" && reader.NodeType == XmlNodeType.Element)
                {
                    var name = reader.GetAttribute("Name")
                        ?? throw new InvalidDataException("Missing Name attribute.");
                    var value = reader.GetAttribute("Value")
                        ?? throw new InvalidDataException("Missing Value attribute.");

                    if (currentCustomData == null)
                    {
                        throw new InvalidDataException("Metadata must follow CustomData.");
                    }
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
