using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Hazina.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedGeometricData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ThoughtSpaces",
                columns: new[] { "Id", "CreatedAt", "Dimensions", "Domain", "GlobalCurvature", "LearningVelocity", "TunnelingCapability", "UpdatedAt", "UserId" },
                values: new object[] { "ts-example-programming", new DateTime(2026, 2, 20, 4, 0, 0, 0, DateTimeKind.Utc), 12, "programming", 0.85m, 0m, 0.5m, new DateTime(2026, 2, 20, 4, 0, 0, 0, DateTimeKind.Utc), "demo-user" });

            migrationBuilder.InsertData(
                table: "Concepts",
                columns: new[] { "Id", "BaseConfusion", "ConceptId", "CreatedAt", "Description", "LastPracticedAt", "LocalCurvature", "MasteryLevel", "Name", "PositionJson", "PracticeCount", "ThoughtSpaceId", "UpdatedAt" },
                values: new object[,]
                {
                    { "concept-algorithms", 2.5m, "algorithms", new DateTime(2026, 2, 16, 10, 0, 0, 0, DateTimeKind.Utc), "Understanding algorithmic thinking, sorting, searching, and optimization", new DateTime(2026, 2, 17, 16, 0, 0, 0, DateTimeKind.Utc), 2.25m, 0.1m, "Algorithms and Problem Solving", "[0.7, 0.8, 0.6, 0.9, 1.0, 0.7, 0.8, 0.9, 0.6, 0.7, 0.8, 0.7]", 2, "ts-example-programming", new DateTime(2026, 2, 17, 16, 0, 0, 0, DateTimeKind.Utc) },
                    { "concept-functions", 1.2m, "functions", new DateTime(2026, 2, 17, 10, 0, 0, 0, DateTimeKind.Utc), "Understanding function definitions, parameters, return values, and scope", new DateTime(2026, 2, 18, 14, 0, 0, 0, DateTimeKind.Utc), 0.72m, 0.4m, "Functions and Methods", "[0.4, 0.5, 0.3, 0.6, 0.7, 0.4, 0.5, 0.6, 0.3, 0.4, 0.5, 0.4]", 5, "ts-example-programming", new DateTime(2026, 2, 18, 14, 0, 0, 0, DateTimeKind.Utc) },
                    { "concept-variables", 0.5m, "variables", new DateTime(2026, 2, 18, 10, 0, 0, 0, DateTimeKind.Utc), "Understanding variable declaration, assignment, and different data types", new DateTime(2026, 2, 19, 10, 0, 0, 0, DateTimeKind.Utc), 0.15m, 0.7m, "Variables and Data Types", "[0.2, 0.3, 0.1, 0.4, 0.5, 0.2, 0.3, 0.4, 0.1, 0.2, 0.3, 0.2]", 10, "ts-example-programming", new DateTime(2026, 2, 19, 10, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "ConceptConnections",
                columns: new[] { "Id", "CreatedAt", "FromConceptId", "Reason", "Strength", "ToConceptId", "Type" },
                values: new object[,]
                {
                    { "conn-func-to-algo", new DateTime(2026, 2, 17, 10, 0, 0, 0, DateTimeKind.Utc), "concept-functions", "Functions are the building blocks for implementing algorithms", 0.95m, "concept-algorithms", 0 },
                    { "conn-var-to-func", new DateTime(2026, 2, 18, 10, 0, 0, 0, DateTimeKind.Utc), "concept-variables", "Understanding variables is essential before learning functions", 0.9m, "concept-functions", 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ConceptConnections",
                keyColumn: "Id",
                keyValue: "conn-func-to-algo");

            migrationBuilder.DeleteData(
                table: "ConceptConnections",
                keyColumn: "Id",
                keyValue: "conn-var-to-func");

            migrationBuilder.DeleteData(
                table: "Concepts",
                keyColumn: "Id",
                keyValue: "concept-algorithms");

            migrationBuilder.DeleteData(
                table: "Concepts",
                keyColumn: "Id",
                keyValue: "concept-functions");

            migrationBuilder.DeleteData(
                table: "Concepts",
                keyColumn: "Id",
                keyValue: "concept-variables");

            migrationBuilder.DeleteData(
                table: "ThoughtSpaces",
                keyColumn: "Id",
                keyValue: "ts-example-programming");
        }
    }
}
