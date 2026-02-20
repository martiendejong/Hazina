using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hazina.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialGeometricSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ThoughtSpaces",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Domain = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Dimensions = table.Column<int>(type: "int", nullable: false),
                    GlobalCurvature = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    LearningVelocity = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    TunnelingCapability = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThoughtSpaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Concepts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ThoughtSpaceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConceptId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    BaseConfusion = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    MasteryLevel = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    LocalCurvature = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    PracticeCount = table.Column<int>(type: "int", nullable: false),
                    PositionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastPracticedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Concepts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Concepts_ThoughtSpaces_ThoughtSpaceId",
                        column: x => x.ThoughtSpaceId,
                        principalTable: "ThoughtSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConceptConnections",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FromConceptId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToConceptId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Strength = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConceptConnections_Concepts_FromConceptId",
                        column: x => x.FromConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConceptConnections_Concepts_ToConceptId",
                        column: x => x.ToConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ThoughtSpaceId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConceptId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    QualityScore = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    CurvatureBefore = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    CurvatureAfter = table.Column<decimal>(type: "decimal(10,6)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningEvents_Concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "Concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningEvents_ThoughtSpaces_ThoughtSpaceId",
                        column: x => x.ThoughtSpaceId,
                        principalTable: "ThoughtSpaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConceptConnections_FromConcept",
                table: "ConceptConnections",
                column: "FromConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_ConceptConnections_FromTo",
                table: "ConceptConnections",
                columns: new[] { "FromConceptId", "ToConceptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConceptConnections_ToConcept",
                table: "ConceptConnections",
                column: "ToConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_LastPracticedAt",
                table: "Concepts",
                column: "LastPracticedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_ThoughtSpaceConceptId",
                table: "Concepts",
                columns: new[] { "ThoughtSpaceId", "ConceptId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Concepts_ThoughtSpaceId",
                table: "Concepts",
                column: "ThoughtSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningEvents_ConceptId",
                table: "LearningEvents",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningEvents_ConceptTime",
                table: "LearningEvents",
                columns: new[] { "ConceptId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningEvents_OccurredAt",
                table: "LearningEvents",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_LearningEvents_ThoughtSpaceId",
                table: "LearningEvents",
                column: "ThoughtSpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_ThoughtSpaces_UserDomain",
                table: "ThoughtSpaces",
                columns: new[] { "UserId", "Domain" });

            migrationBuilder.CreateIndex(
                name: "IX_ThoughtSpaces_UserId",
                table: "ThoughtSpaces",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConceptConnections");

            migrationBuilder.DropTable(
                name: "LearningEvents");

            migrationBuilder.DropTable(
                name: "Concepts");

            migrationBuilder.DropTable(
                name: "ThoughtSpaces");
        }
    }
}
