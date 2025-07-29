using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyPostgresApi.Migrations
{
    /// <inheritdoc />
    public partial class AddUserToBoardPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_saved_images_users_UserId",
                schema: "maskinen",
                table: "saved_images");

            migrationBuilder.RenameColumn(
                name: "Title",
                schema: "maskinen",
                table: "saved_images",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Photographer",
                schema: "maskinen",
                table: "saved_images",
                newName: "photographer");

            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "maskinen",
                table: "saved_images",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                schema: "maskinen",
                table: "saved_images",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "SourceLink",
                schema: "maskinen",
                table: "saved_images",
                newName: "source_link");

            migrationBuilder.RenameColumn(
                name: "SavedAt",
                schema: "maskinen",
                table: "saved_images",
                newName: "saved_at");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                schema: "maskinen",
                table: "saved_images",
                newName: "image_url");

            migrationBuilder.RenameIndex(
                name: "IX_saved_images_UserId",
                schema: "maskinen",
                table: "saved_images",
                newName: "IX_saved_images_user_id");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                schema: "maskinen",
                table: "saved_images",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "photographer",
                schema: "maskinen",
                table: "saved_images",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "source_link",
                schema: "maskinen",
                table: "saved_images",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<DateTime>(
                name: "saved_at",
                schema: "maskinen",
                table: "saved_images",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "NOW()");

            migrationBuilder.CreateTable(
                name: "board_posts",
                schema: "maskinen",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_board_posts", x => x.id);
                    table.ForeignKey(
                        name: "FK_board_posts_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "maskinen",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_board_posts_user_id",
                schema: "maskinen",
                table: "board_posts",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_saved_images_users_user_id",
                schema: "maskinen",
                table: "saved_images",
                column: "user_id",
                principalSchema: "maskinen",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_saved_images_users_user_id",
                schema: "maskinen",
                table: "saved_images");

            migrationBuilder.DropTable(
                name: "board_posts",
                schema: "maskinen");

            migrationBuilder.RenameColumn(
                name: "title",
                schema: "maskinen",
                table: "saved_images",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "photographer",
                schema: "maskinen",
                table: "saved_images",
                newName: "Photographer");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "maskinen",
                table: "saved_images",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                schema: "maskinen",
                table: "saved_images",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "source_link",
                schema: "maskinen",
                table: "saved_images",
                newName: "SourceLink");

            migrationBuilder.RenameColumn(
                name: "saved_at",
                schema: "maskinen",
                table: "saved_images",
                newName: "SavedAt");

            migrationBuilder.RenameColumn(
                name: "image_url",
                schema: "maskinen",
                table: "saved_images",
                newName: "ImageUrl");

            migrationBuilder.RenameIndex(
                name: "IX_saved_images_user_id",
                schema: "maskinen",
                table: "saved_images",
                newName: "IX_saved_images_UserId");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "maskinen",
                table: "saved_images",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Photographer",
                schema: "maskinen",
                table: "saved_images",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "SourceLink",
                schema: "maskinen",
                table: "saved_images",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SavedAt",
                schema: "maskinen",
                table: "saved_images",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddForeignKey(
                name: "FK_saved_images_users_UserId",
                schema: "maskinen",
                table: "saved_images",
                column: "UserId",
                principalSchema: "maskinen",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
