using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MSBuild.Sdk.SqlProj.DacpacTool.Diagram;
using MSBuild.Sdk.SqlProj.DacpacTool.Diagram.Model;
using Shouldly;

namespace MSBuild.Sdk.SqlProj.DacpacTool.Tests
{
    [TestClass]
    public class DatabaseModelToMermaidTests
    {
        [TestMethod]
        public void CreateMermaid_SortsForeignKeysByRawPrincipalTableAndName()
        {
            var foreignKeyColumn = new SimpleColumn
            {
                Name = "PrincipalId",
                StoreType = "int",
            };

            var dependentTable = new SimpleTable
            {
                Schema = "dbo",
                Name = "Dependent",
                Columns = { foreignKeyColumn },
                ForeignKeys =
                {
                    new SimpleForeignKey
                    {
                        Name = "FK_Dependent_CustomerDetail",
                        PrincipalTable = new SimpleTable { Schema = "dbo", Name = "CustomerDetail" },
                        Columns = { foreignKeyColumn },
                    },
                    new SimpleForeignKey
                    {
                        Name = "FK_Dependent_Customer_Detail",
                        PrincipalTable = new SimpleTable { Schema = "dbo", Name = "Customer Detail" },
                        Columns = { foreignKeyColumn },
                    },
                },
            };

            var diagram = new DatabaseModelToMermaid(new List<SimpleTable> { dependentTable }).CreateMermaid();

            diagram.IndexOf(": FK_Dependent_Customer_Detail", StringComparison.Ordinal).ShouldBeLessThan(
                diagram.IndexOf(": FK_Dependent_CustomerDetail", StringComparison.Ordinal));
        }
    }
}
