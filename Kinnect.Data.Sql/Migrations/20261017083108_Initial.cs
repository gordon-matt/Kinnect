using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kinnect.Data.Sql.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "app");

        migrationBuilder.CreateTable(
            name: "AspNetRoles",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AspNetRoles", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                AccessFailedCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AspNetUsers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ChatRooms",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                AdminUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ChatRooms", x => x.Id));

        migrationBuilder.CreateTable(
            name: "People",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                FamilyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                GivenNames = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                IsMale = table.Column<bool>(type: "bit", nullable: false),
                Bio = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ProfileImagePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Latitude = table.Column<double>(type: "float", nullable: true),
                Longitude = table.Column<double>(type: "float", nullable: true),
                FatherId = table.Column<int>(type: "int", nullable: true),
                MotherId = table.Column<int>(type: "int", nullable: true),
                Occupation = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Education = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Religion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                IsDeceased = table.Column<bool>(type: "bit", nullable: false),
                GedcomId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_People", x => x.Id);
                table.ForeignKey(
                    name: "FK_People_People_FatherId",
                    column: x => x.FatherId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_People_People_MotherId",
                    column: x => x.MotherId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "Tags",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Tags", x => x.Id));

        migrationBuilder.CreateTable(
            name: "AspNetRoleClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ApplicationRoleApplicationUser",
            columns: table => new
            {
                RolesId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                UsersId = table.Column<string>(type: "nvarchar(450)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ApplicationRoleApplicationUser", x => new { x.RolesId, x.UsersId });
                table.ForeignKey(
                    name: "FK_ApplicationRoleApplicationUser_AspNetRoles_RolesId",
                    column: x => x.RolesId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ApplicationRoleApplicationUser_AspNetUsers_UsersId",
                    column: x => x.UsersId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserRoles",
            columns: table => new
            {
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserTokens",
            columns: table => new
            {
                UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ChatMessages",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Content = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                FromUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ToRoomId = table.Column<int>(type: "int", nullable: true),
                ToUserId = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChatMessages", x => x.Id);
                table.ForeignKey(
                    name: "FK_ChatMessages_ChatRooms_ToRoomId",
                    column: x => x.ToRoomId,
                    principalSchema: "app",
                    principalTable: "ChatRooms",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Documents",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                FileSize = table.Column<long>(type: "bigint", nullable: false),
                UploadedByPersonId = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Documents", x => x.Id);
                table.ForeignKey(
                    name: "FK_Documents_People_UploadedByPersonId",
                    column: x => x.UploadedByPersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MediaFolders",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedByPersonId = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MediaFolders", x => x.Id);
                table.ForeignKey(
                    name: "FK_MediaFolders_People_CreatedByPersonId",
                    column: x => x.CreatedByPersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PersonEvents",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PersonId = table.Column<int>(type: "int", nullable: false),
                EventType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                Year = table.Column<short>(type: "smallint", nullable: true),
                Month = table.Column<byte>(type: "tinyint", nullable: true),
                Day = table.Column<byte>(type: "tinyint", nullable: true),
                Place = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Latitude = table.Column<double>(type: "float", nullable: true),
                Longitude = table.Column<double>(type: "float", nullable: true),
                Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                Note = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PersonEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_PersonEvents_People_PersonId",
                    column: x => x.PersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PersonSpouses",
            schema: "app",
            columns: table => new
            {
                PersonId = table.Column<int>(type: "int", nullable: false),
                SpouseId = table.Column<int>(type: "int", nullable: false),
                MarriageYear = table.Column<short>(type: "smallint", nullable: true),
                MarriageMonth = table.Column<byte>(type: "tinyint", nullable: true),
                MarriageDay = table.Column<byte>(type: "tinyint", nullable: true),
                DivorceYear = table.Column<short>(type: "smallint", nullable: true),
                DivorceMonth = table.Column<byte>(type: "tinyint", nullable: true),
                DivorceDay = table.Column<byte>(type: "tinyint", nullable: true),
                EngagementYear = table.Column<short>(type: "smallint", nullable: true),
                EngagementMonth = table.Column<byte>(type: "tinyint", nullable: true),
                EngagementDay = table.Column<byte>(type: "tinyint", nullable: true),
                HasEngagement = table.Column<bool>(type: "bit", nullable: false),
                HasMarriage = table.Column<bool>(type: "bit", nullable: false),
                HasDivorce = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PersonSpouses", x => new { x.PersonId, x.SpouseId });
                table.ForeignKey(
                    name: "FK_PersonSpouses_People_PersonId",
                    column: x => x.PersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PersonSpouses_People_SpouseId",
                    column: x => x.SpouseId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PersonVersions",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                PersonId = table.Column<int>(type: "int", nullable: false),
                VersionData = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ChangedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PersonVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_PersonVersions_People_PersonId",
                    column: x => x.PersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Posts",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                AuthorPersonId = table.Column<int>(type: "int", nullable: false),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Posts", x => x.Id);
                table.ForeignKey(
                    name: "FK_Posts_People_AuthorPersonId",
                    column: x => x.AuthorPersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "MessageNotifications",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                ChatMessageId = table.Column<int>(type: "int", nullable: false),
                FromUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ToUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                IsRead = table.Column<bool>(type: "bit", nullable: false),
                EmailSent = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MessageNotifications", x => x.Id);
                table.ForeignKey(
                    name: "FK_MessageNotifications_ChatMessages_ChatMessageId",
                    column: x => x.ChatMessageId,
                    principalSchema: "app",
                    principalTable: "ChatMessages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "DocumentTags",
            schema: "app",
            columns: table => new
            {
                DocumentId = table.Column<int>(type: "int", nullable: false),
                TagId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DocumentTags", x => new { x.DocumentId, x.TagId });
                table.ForeignKey(
                    name: "FK_DocumentTags_Documents_DocumentId",
                    column: x => x.DocumentId,
                    principalSchema: "app",
                    principalTable: "Documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_DocumentTags_Tags_TagId",
                    column: x => x.TagId,
                    principalSchema: "app",
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Photos",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ThumbnailPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UploadedByPersonId = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                YearTaken = table.Column<short>(type: "smallint", nullable: true),
                MonthTaken = table.Column<byte>(type: "tinyint", nullable: true),
                DayTaken = table.Column<byte>(type: "tinyint", nullable: true),
                AnnotationsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Latitude = table.Column<double>(type: "float", nullable: true),
                Longitude = table.Column<double>(type: "float", nullable: true),
                LatLongAcquiredFromExif = table.Column<bool>(type: "bit", nullable: false),
                FolderId = table.Column<int>(type: "int", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Photos", x => x.Id);
                table.ForeignKey(
                    name: "FK_Photos_MediaFolders_FolderId",
                    column: x => x.FolderId,
                    principalSchema: "app",
                    principalTable: "MediaFolders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Photos_People_UploadedByPersonId",
                    column: x => x.UploadedByPersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Videos",
            schema: "app",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ThumbnailPath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Duration = table.Column<TimeSpan>(type: "time", nullable: true),
                UploadedByPersonId = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                FolderId = table.Column<int>(type: "int", nullable: true),
                IsProcessing = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Videos", x => x.Id);
                table.ForeignKey(
                    name: "FK_Videos_MediaFolders_FolderId",
                    column: x => x.FolderId,
                    principalSchema: "app",
                    principalTable: "MediaFolders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Videos_People_UploadedByPersonId",
                    column: x => x.UploadedByPersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PersonPhotos",
            schema: "app",
            columns: table => new
            {
                PersonId = table.Column<int>(type: "int", nullable: false),
                PhotoId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PersonPhotos", x => new { x.PersonId, x.PhotoId });
                table.ForeignKey(
                    name: "FK_PersonPhotos_People_PersonId",
                    column: x => x.PersonId,
                    principalSchema: "app",
                    principalTable: "People",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PersonPhotos_Photos_PhotoId",
                    column: x => x.PhotoId,
                    principalSchema: "app",
                    principalTable: "Photos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PhotoEvents",
            schema: "app",
            columns: table => new
            {
                PhotoId = table.Column<int>(type: "int", nullable: false),
                PersonEventId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PhotoEvents", x => new { x.PhotoId, x.PersonEventId });
                table.ForeignKey(
                    name: "FK_PhotoEvents_PersonEvents_PersonEventId",
                    column: x => x.PersonEventId,
                    principalSchema: "app",
                    principalTable: "PersonEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PhotoEvents_Photos_PhotoId",
                    column: x => x.PhotoId,
                    principalSchema: "app",
                    principalTable: "Photos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PhotoTags",
            schema: "app",
            columns: table => new
            {
                PhotoId = table.Column<int>(type: "int", nullable: false),
                TagId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PhotoTags", x => new { x.PhotoId, x.TagId });
                table.ForeignKey(
                    name: "FK_PhotoTags_Photos_PhotoId",
                    column: x => x.PhotoId,
                    principalSchema: "app",
                    principalTable: "Photos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PhotoTags_Tags_TagId",
                    column: x => x.TagId,
                    principalSchema: "app",
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "VideoEvents",
            schema: "app",
            columns: table => new
            {
                VideoId = table.Column<int>(type: "int", nullable: false),
                PersonEventId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VideoEvents", x => new { x.VideoId, x.PersonEventId });
                table.ForeignKey(
                    name: "FK_VideoEvents_PersonEvents_PersonEventId",
                    column: x => x.PersonEventId,
                    principalSchema: "app",
                    principalTable: "PersonEvents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_VideoEvents_Videos_VideoId",
                    column: x => x.VideoId,
                    principalSchema: "app",
                    principalTable: "Videos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "VideoTags",
            schema: "app",
            columns: table => new
            {
                VideoId = table.Column<int>(type: "int", nullable: false),
                TagId = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VideoTags", x => new { x.VideoId, x.TagId });
                table.ForeignKey(
                    name: "FK_VideoTags_Tags_TagId",
                    column: x => x.TagId,
                    principalSchema: "app",
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_VideoTags_Videos_VideoId",
                    column: x => x.VideoId,
                    principalSchema: "app",
                    principalTable: "Videos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ApplicationRoleApplicationUser_UsersId",
            table: "ApplicationRoleApplicationUser",
            column: "UsersId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetRoleClaims_RoleId",
            table: "AspNetRoleClaims",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "AspNetRoles",
            column: "NormalizedName",
            unique: true,
            filter: "[NormalizedName] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserClaims_UserId",
            table: "AspNetUserClaims",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserLogins_UserId",
            table: "AspNetUserLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserRoles_RoleId",
            table: "AspNetUserRoles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AspNetUsers",
            column: "NormalizedEmail");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AspNetUsers",
            column: "NormalizedUserName",
            unique: true,
            filter: "[NormalizedUserName] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ChatMessages_ToRoomId",
            schema: "app",
            table: "ChatMessages",
            column: "ToRoomId");

        migrationBuilder.CreateIndex(
            name: "IX_ChatRooms_Name",
            schema: "app",
            table: "ChatRooms",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Documents_UploadedByPersonId",
            schema: "app",
            table: "Documents",
            column: "UploadedByPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_DocumentTags_TagId",
            schema: "app",
            table: "DocumentTags",
            column: "TagId");

        migrationBuilder.CreateIndex(
            name: "IX_MediaFolders_CreatedByPersonId",
            schema: "app",
            table: "MediaFolders",
            column: "CreatedByPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_MessageNotifications_ChatMessageId",
            schema: "app",
            table: "MessageNotifications",
            column: "ChatMessageId");

        migrationBuilder.CreateIndex(
            name: "IX_MessageNotifications_IsRead_EmailSent_CreatedAtUtc",
            schema: "app",
            table: "MessageNotifications",
            columns: new[] { "IsRead", "EmailSent", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_MessageNotifications_ToUserId_IsRead",
            schema: "app",
            table: "MessageNotifications",
            columns: new[] { "ToUserId", "IsRead" });

        migrationBuilder.CreateIndex(
            name: "IX_People_FatherId",
            schema: "app",
            table: "People",
            column: "FatherId");

        migrationBuilder.CreateIndex(
            name: "IX_People_MotherId",
            schema: "app",
            table: "People",
            column: "MotherId");

        migrationBuilder.CreateIndex(
            name: "IX_People_UserId",
            schema: "app",
            table: "People",
            column: "UserId",
            unique: true,
            filter: "\"UserId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_PersonEvents_PersonId",
            schema: "app",
            table: "PersonEvents",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_PersonPhotos_PhotoId",
            schema: "app",
            table: "PersonPhotos",
            column: "PhotoId");

        migrationBuilder.CreateIndex(
            name: "IX_PersonSpouses_SpouseId",
            schema: "app",
            table: "PersonSpouses",
            column: "SpouseId");

        migrationBuilder.CreateIndex(
            name: "IX_PersonVersions_PersonId",
            schema: "app",
            table: "PersonVersions",
            column: "PersonId");

        migrationBuilder.CreateIndex(
            name: "IX_PhotoEvents_PersonEventId",
            schema: "app",
            table: "PhotoEvents",
            column: "PersonEventId");

        migrationBuilder.CreateIndex(
            name: "IX_Photos_FolderId",
            schema: "app",
            table: "Photos",
            column: "FolderId");

        migrationBuilder.CreateIndex(
            name: "IX_Photos_UploadedByPersonId",
            schema: "app",
            table: "Photos",
            column: "UploadedByPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_PhotoTags_TagId",
            schema: "app",
            table: "PhotoTags",
            column: "TagId");

        migrationBuilder.CreateIndex(
            name: "IX_Posts_AuthorPersonId",
            schema: "app",
            table: "Posts",
            column: "AuthorPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_Posts_CreatedAtUtc",
            schema: "app",
            table: "Posts",
            column: "CreatedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_Name",
            schema: "app",
            table: "Tags",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_VideoEvents_PersonEventId",
            schema: "app",
            table: "VideoEvents",
            column: "PersonEventId");

        migrationBuilder.CreateIndex(
            name: "IX_Videos_FolderId",
            schema: "app",
            table: "Videos",
            column: "FolderId");

        migrationBuilder.CreateIndex(
            name: "IX_Videos_UploadedByPersonId",
            schema: "app",
            table: "Videos",
            column: "UploadedByPersonId");

        migrationBuilder.CreateIndex(
            name: "IX_VideoTags_TagId",
            schema: "app",
            table: "VideoTags",
            column: "TagId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ApplicationRoleApplicationUser");

        migrationBuilder.DropTable(
            name: "AspNetRoleClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserLogins");

        migrationBuilder.DropTable(
            name: "AspNetUserRoles");

        migrationBuilder.DropTable(
            name: "AspNetUserTokens");

        migrationBuilder.DropTable(
            name: "DocumentTags",
            schema: "app");

        migrationBuilder.DropTable(
            name: "MessageNotifications",
            schema: "app");

        migrationBuilder.DropTable(
            name: "PersonPhotos",
            schema: "app");

        migrationBuilder.DropTable(
            name: "PersonSpouses",
            schema: "app");

        migrationBuilder.DropTable(
            name: "PersonVersions",
            schema: "app");

        migrationBuilder.DropTable(
            name: "PhotoEvents",
            schema: "app");

        migrationBuilder.DropTable(
            name: "PhotoTags",
            schema: "app");

        migrationBuilder.DropTable(
            name: "Posts",
            schema: "app");

        migrationBuilder.DropTable(
            name: "VideoEvents",
            schema: "app");

        migrationBuilder.DropTable(
            name: "VideoTags",
            schema: "app");

        migrationBuilder.DropTable(
            name: "AspNetRoles");

        migrationBuilder.DropTable(
            name: "AspNetUsers");

        migrationBuilder.DropTable(
            name: "Documents",
            schema: "app");

        migrationBuilder.DropTable(
            name: "ChatMessages",
            schema: "app");

        migrationBuilder.DropTable(
            name: "Photos",
            schema: "app");

        migrationBuilder.DropTable(
            name: "PersonEvents",
            schema: "app");

        migrationBuilder.DropTable(
            name: "Tags",
            schema: "app");

        migrationBuilder.DropTable(
            name: "Videos",
            schema: "app");

        migrationBuilder.DropTable(
            name: "ChatRooms",
            schema: "app");

        migrationBuilder.DropTable(
            name: "MediaFolders",
            schema: "app");

        migrationBuilder.DropTable(
            name: "People",
            schema: "app");
    }
}
