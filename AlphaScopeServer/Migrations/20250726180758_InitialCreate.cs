using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AlphaScopeServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    LocalContentId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AccountId = table.Column<int>(type: "INTEGER", nullable: true),
                    HomeWorldId = table.Column<short>(type: "INTEGER", nullable: true),
                    CurrentWorldId = table.Column<short>(type: "INTEGER", nullable: true),
                    TerritoryId = table.Column<short>(type: "INTEGER", nullable: true),
                    CurrentJobId = table.Column<byte>(type: "INTEGER", nullable: true),
                    CurrentJobLevel = table.Column<short>(type: "INTEGER", nullable: true),
                    PlayerPos = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AvatarLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LastScannedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsPrivate = table.Column<bool>(type: "INTEGER", nullable: false),
                    HideInSearch = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.LocalContentId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GameAccountId = table.Column<int>(type: "INTEGER", nullable: false),
                    PrimaryCharacterLocalContentId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ApiKey = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    AppRoleId = table.Column<int>(type: "INTEGER", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadedPlayersCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadedPlayerInfoCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadedRetainersCount = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadedRetainerInfoCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FetchedPlayerInfoCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SearchedNamesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSyncedTime = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlayerCustomizationHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerLocalContentId = table.Column<long>(type: "INTEGER", nullable: false),
                    BodyType = table.Column<byte>(type: "INTEGER", nullable: true),
                    GenderRace = table.Column<byte>(type: "INTEGER", nullable: true),
                    Height = table.Column<byte>(type: "INTEGER", nullable: true),
                    Face = table.Column<byte>(type: "INTEGER", nullable: true),
                    SkinColor = table.Column<byte>(type: "INTEGER", nullable: true),
                    Nose = table.Column<byte>(type: "INTEGER", nullable: true),
                    Jaw = table.Column<byte>(type: "INTEGER", nullable: true),
                    MuscleMass = table.Column<byte>(type: "INTEGER", nullable: true),
                    BustSize = table.Column<byte>(type: "INTEGER", nullable: true),
                    TailShape = table.Column<byte>(type: "INTEGER", nullable: true),
                    Mouth = table.Column<byte>(type: "INTEGER", nullable: true),
                    EyeShape = table.Column<byte>(type: "INTEGER", nullable: true),
                    SmallIris = table.Column<bool>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerCustomizationHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerCustomizationHistory_Players_PlayerLocalContentId",
                        column: x => x.PlayerLocalContentId,
                        principalTable: "Players",
                        principalColumn: "LocalContentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerLodestone",
                columns: table => new
                {
                    PlayerLocalContentId = table.Column<long>(type: "INTEGER", nullable: false),
                    LodestoneId = table.Column<int>(type: "INTEGER", nullable: true),
                    CharacterCreationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AvatarLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerLodestone", x => x.PlayerLocalContentId);
                    table.ForeignKey(
                        name: "FK_PlayerLodestone_Players_PlayerLocalContentId",
                        column: x => x.PlayerLocalContentId,
                        principalTable: "Players",
                        principalColumn: "LocalContentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerNameHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerLocalContentId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerNameHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerNameHistory_Players_PlayerLocalContentId",
                        column: x => x.PlayerLocalContentId,
                        principalTable: "Players",
                        principalColumn: "LocalContentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerProfileVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerLocalContentId = table.Column<long>(type: "INTEGER", nullable: false),
                    VisitorId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    VisitedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerProfileVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerProfileVisits_Players_PlayerLocalContentId",
                        column: x => x.PlayerLocalContentId,
                        principalTable: "Players",
                        principalColumn: "LocalContentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerTerritoryHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerLocalContentId = table.Column<long>(type: "INTEGER", nullable: false),
                    TerritoryId = table.Column<short>(type: "INTEGER", nullable: true),
                    PlayerPos = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    WorldId = table.Column<short>(type: "INTEGER", nullable: true),
                    FirstSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerTerritoryHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerTerritoryHistory_Players_PlayerLocalContentId",
                        column: x => x.PlayerLocalContentId,
                        principalTable: "Players",
                        principalColumn: "LocalContentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlayerWorldHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PlayerLocalContentId = table.Column<long>(type: "INTEGER", nullable: false),
                    WorldId = table.Column<short>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerWorldHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerWorldHistory_Players_PlayerLocalContentId",
                        column: x => x.PlayerLocalContentId,
                        principalTable: "Players",
                        principalColumn: "LocalContentId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCharacters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    LocalContentId = table.Column<long>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AvatarLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    HideFullProfile = table.Column<bool>(type: "INTEGER", nullable: false),
                    HideTerritoryInfo = table.Column<bool>(type: "INTEGER", nullable: false),
                    HideCustomizations = table.Column<bool>(type: "INTEGER", nullable: false),
                    HideInSearchResults = table.Column<bool>(type: "INTEGER", nullable: false),
                    HideRetainersInfo = table.Column<bool>(type: "INTEGER", nullable: false),
                    HideAltCharacters = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProfileTotalVisitCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastProfileVisitDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCharacters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLodestoneCharacters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    LodestoneId = table.Column<int>(type: "INTEGER", nullable: false),
                    NameAndWorld = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    AvatarLink = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLodestoneCharacters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLodestoneCharacters_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCustomizationHistory_CreatedAt",
                table: "PlayerCustomizationHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCustomizationHistory_PlayerLocalContentId",
                table: "PlayerCustomizationHistory",
                column: "PlayerLocalContentId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerLodestone_LodestoneId",
                table: "PlayerLodestone",
                column: "LodestoneId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerNameHistory_CreatedAt",
                table: "PlayerNameHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerNameHistory_PlayerLocalContentId",
                table: "PlayerNameHistory",
                column: "PlayerLocalContentId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfileVisits_PlayerLocalContentId",
                table: "PlayerProfileVisits",
                column: "PlayerLocalContentId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerProfileVisits_VisitedAt",
                table: "PlayerProfileVisits",
                column: "VisitedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Players_AccountId",
                table: "Players",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_CreatedAt",
                table: "Players",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Players_CurrentWorldId",
                table: "Players",
                column: "CurrentWorldId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_HomeWorldId",
                table: "Players",
                column: "HomeWorldId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Name",
                table: "Players",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTerritoryHistory_FirstSeenAt",
                table: "PlayerTerritoryHistory",
                column: "FirstSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTerritoryHistory_LastSeenAt",
                table: "PlayerTerritoryHistory",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerTerritoryHistory_PlayerLocalContentId",
                table: "PlayerTerritoryHistory",
                column: "PlayerLocalContentId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerWorldHistory_CreatedAt",
                table: "PlayerWorldHistory",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlayerWorldHistory_PlayerLocalContentId",
                table: "PlayerWorldHistory",
                column: "PlayerLocalContentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCharacters_LocalContentId",
                table: "UserCharacters",
                column: "LocalContentId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCharacters_UserId",
                table: "UserCharacters",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLodestoneCharacters_LodestoneId",
                table: "UserLodestoneCharacters",
                column: "LodestoneId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLodestoneCharacters_UserId",
                table: "UserLodestoneCharacters",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ApiKey",
                table: "Users",
                column: "ApiKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GameAccountId",
                table: "Users",
                column: "GameAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PrimaryCharacterLocalContentId",
                table: "Users",
                column: "PrimaryCharacterLocalContentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerCustomizationHistory");

            migrationBuilder.DropTable(
                name: "PlayerLodestone");

            migrationBuilder.DropTable(
                name: "PlayerNameHistory");

            migrationBuilder.DropTable(
                name: "PlayerProfileVisits");

            migrationBuilder.DropTable(
                name: "PlayerTerritoryHistory");

            migrationBuilder.DropTable(
                name: "PlayerWorldHistory");

            migrationBuilder.DropTable(
                name: "UserCharacters");

            migrationBuilder.DropTable(
                name: "UserLodestoneCharacters");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
