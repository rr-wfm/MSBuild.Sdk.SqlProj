﻿using System.IO;

namespace MSBuild.Sdk.SqlProj.DacpacTool
{
    public class InspectOptions
    {
        public FileInfo PreDeploy { get; set; }
        public FileInfo PostDeploy { get; set; }
        public bool Debug { get; set; }
    }
}
