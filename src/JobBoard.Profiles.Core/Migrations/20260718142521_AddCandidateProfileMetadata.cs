using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobBoard.Profiles.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateProfileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The old free-text résumé link is retired — the résumé is now an uploaded file (see the new
            // Resume* columns below). Drop it outright rather than let EF's heuristic rename it into a new,
            // unrelated column (it would otherwise repoint old résumé URLs at PortfolioUrl).
            migrationBuilder.DropColumn(
                name: "ResumeUrl",
                table: "CandidateProfiles");

            migrationBuilder.AddColumn<string>(
                name: "PortfolioUrl",
                table: "CandidateProfiles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Availability",
                table: "CandidateProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesiredRole",
                table: "CandidateProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "CandidateProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubUrl",
                table: "CandidateProfiles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInUrl",
                table: "CandidateProfiles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "CandidateProfiles",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "CandidateProfiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeContentType",
                table: "CandidateProfiles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeFileName",
                table: "CandidateProfiles",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResumeObjectName",
                table: "CandidateProfiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearsOfExperience",
                table: "CandidateProfiles",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Availability",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "DesiredRole",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "GitHubUrl",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "LinkedInUrl",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "ResumeContentType",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "ResumeFileName",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "ResumeObjectName",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "YearsOfExperience",
                table: "CandidateProfiles");

            migrationBuilder.DropColumn(
                name: "PortfolioUrl",
                table: "CandidateProfiles");

            migrationBuilder.AddColumn<string>(
                name: "ResumeUrl",
                table: "CandidateProfiles",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }
    }
}
