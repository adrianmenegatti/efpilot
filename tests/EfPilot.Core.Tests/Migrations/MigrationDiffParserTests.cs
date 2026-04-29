using EfPilot.Core.Migrations;

namespace EfPilot.Core.Tests.Migrations;

public sealed class MigrationDiffParserTests
{
    [Fact]
    public void Parse_ShouldReturnEmpty_WhenUpIsEmpty()
    {
        using var file = TestMigrationFile.Create(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            public partial class EmptyMigration : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                }
            }
            """);

        var parser = new MigrationDiffParser();

        var result = parser.Parse(file.Path);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_ShouldDetectAddColumn()
    {
        using var file = TestMigrationFile.Create(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            public partial class AddLoanCode : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AddColumn<string>(
                        name: "Code",
                        table: "Loans",
                        type: "text",
                        nullable: true);
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                }
            }
            """);

        var parser = new MigrationDiffParser();

        var result = parser.Parse(file.Path);

        Assert.Single(result);
        Assert.Equal("AddColumn", result[0].Operation);
        Assert.Equal("Add column 'Code' to 'Loans'", result[0].Description);
    }

    [Fact]
    public void Parse_ShouldDetectDropColumn()
    {
        using var file = TestMigrationFile.Create(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            public partial class DropLoanCode : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.DropColumn(
                        name: "Code",
                        table: "Loans");
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                }
            }
            """);

        var parser = new MigrationDiffParser();

        var result = parser.Parse(file.Path);

        Assert.Single(result);
        Assert.Equal("DropColumn", result[0].Operation);
        Assert.Equal("Drop column 'Code' from 'Loans'", result[0].Description);
    }

    [Fact]
    public void Parse_ShouldDetectCreateTable()
    {
        using var file = TestMigrationFile.Create(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            public partial class CreateCustomers : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.CreateTable(
                        name: "Customers",
                        columns: table => new
                        {
                            Id = table.Column<Guid>(type: "uuid", nullable: false)
                        },
                        constraints: table =>
                        {
                            table.PrimaryKey("PK_Customers", x => x.Id);
                        });
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                }
            }
            """);

        var parser = new MigrationDiffParser();

        var result = parser.Parse(file.Path);

        Assert.Single(result);
        Assert.Equal("CreateTable", result[0].Operation);
        Assert.Equal("Create table 'Customers'", result[0].Description);
    }

    [Fact]
    public void Parse_ShouldDetectMultipleOperations()
    {
        using var file = TestMigrationFile.Create(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            public partial class MultipleChanges : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    migrationBuilder.AddColumn<string>(
                        name: "Code",
                        table: "Loans",
                        type: "text",
                        nullable: true);

                    migrationBuilder.CreateIndex(
                        name: "IX_Loans_Code",
                        table: "Loans",
                        column: "Code");
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                }
            }
            """);

        var parser = new MigrationDiffParser();

        var result = parser.Parse(file.Path);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Operation == "AddColumn");
        Assert.Contains(result, x => x.Operation == "CreateIndex");
    }
}