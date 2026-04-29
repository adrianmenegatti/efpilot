using EfPilot.Core.Migrations;

namespace EfPilot.Core.Tests.Migrations;

public sealed class MigrationFileAnalyzerTests
{
    [Fact]
    public void IsEmptyMigration_ShouldReturnTrue_WhenUpAndDownAreEmpty()
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

        var analyzer = new MigrationFileAnalyzer();

        var result = analyzer.IsEmptyMigration(file.Path);

        Assert.True(result);
    }

    [Fact]
    public void IsEmptyMigration_ShouldReturnTrue_WhenMethodsOnlyContainComments()
    {
        using var file = TestMigrationFile.Create(
            """
            using Microsoft.EntityFrameworkCore.Migrations;

            public partial class EmptyMigration : Migration
            {
                protected override void Up(MigrationBuilder migrationBuilder)
                {
                    // no changes
                }

                protected override void Down(MigrationBuilder migrationBuilder)
                {
                    /*
                       no changes
                    */
                }
            }
            """);

        var analyzer = new MigrationFileAnalyzer();

        var result = analyzer.IsEmptyMigration(file.Path);

        Assert.True(result);
    }

    [Fact]
    public void IsEmptyMigration_ShouldReturnFalse_WhenUpHasOperation()
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
                    migrationBuilder.DropColumn(
                        name: "Code",
                        table: "Loans");
                }
            }
            """);

        var analyzer = new MigrationFileAnalyzer();

        var result = analyzer.IsEmptyMigration(file.Path);

        Assert.False(result);
    }
}