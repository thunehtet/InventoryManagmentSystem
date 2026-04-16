using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClothInventoryApp.Migrations
{
    /// <inheritdoc />
    public partial class SyncInquiryApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "Tenants",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedTenantId",
                table: "ContactInquiries",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<string>(
                name: "BusinessName",
                table: "ContactInquiries",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "BusinessType",
                table: "ContactInquiries",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ReviewRemarks",
                table: "ContactInquiries",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "ContactInquiries",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "ContactInquiries",
                type: "varchar(450)",
                maxLength: 450,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ContactInquiries",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UploadedFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UploadedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OriginalFileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StoredFileName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Extension = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    RelativePath = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UploadedFiles_AspNetUsers_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_UploadedFiles_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SubscriptionPaymentRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RequestedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PlanId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PlanNameSnapshot = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BillingCycle = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ExpectedPrice = table.Column<int>(type: "int", nullable: false),
                    Last6TransactionId = table.Column<string>(type: "varchar(6)", maxLength: 6, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PaymentProofFileId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ReviewRemarks = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ApprovedSubscriptionId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPaymentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPaymentRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPaymentRequests_AspNetUsers_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubscriptionPaymentRequests_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPaymentRequests_TenantSubscriptions_ApprovedSubs~",
                        column: x => x.ApprovedSubscriptionId,
                        principalTable: "TenantSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubscriptionPaymentRequests_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPaymentRequests_UploadedFiles_PaymentProofFileId",
                        column: x => x.PaymentProofFileId,
                        principalTable: "UploadedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ContactInquiries_ApprovedTenantId",
                table: "ContactInquiries",
                column: "ApprovedTenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactInquiries_ReviewedByUserId",
                table: "ContactInquiries",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactInquiries_Status_SubmittedAt",
                table: "ContactInquiries",
                columns: new[] { "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPaymentRequests_ApprovedSubscriptionId",
                table: "SubscriptionPaymentRequests",
                column: "ApprovedSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPaymentRequests_PaymentProofFileId",
                table: "SubscriptionPaymentRequests",
                column: "PaymentProofFileId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPaymentRequests_PlanId",
                table: "SubscriptionPaymentRequests",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPaymentRequests_RequestedByUserId",
                table: "SubscriptionPaymentRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPaymentRequests_ReviewedByUserId",
                table: "SubscriptionPaymentRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPaymentRequests_TenantId_Status_SubmittedAt",
                table: "SubscriptionPaymentRequests",
                columns: new[] { "TenantId", "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadedFiles_Category_CreatedAt",
                table: "UploadedFiles",
                columns: new[] { "Category", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UploadedFiles_TenantId",
                table: "UploadedFiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_UploadedFiles_UploadedByUserId",
                table: "UploadedFiles",
                column: "UploadedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContactInquiries_AspNetUsers_ReviewedByUserId",
                table: "ContactInquiries",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ContactInquiries_Tenants_ApprovedTenantId",
                table: "ContactInquiries",
                column: "ApprovedTenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContactInquiries_AspNetUsers_ReviewedByUserId",
                table: "ContactInquiries");

            migrationBuilder.DropForeignKey(
                name: "FK_ContactInquiries_Tenants_ApprovedTenantId",
                table: "ContactInquiries");

            migrationBuilder.DropTable(
                name: "SubscriptionPaymentRequests");

            migrationBuilder.DropTable(
                name: "UploadedFiles");

            migrationBuilder.DropIndex(
                name: "IX_ContactInquiries_ApprovedTenantId",
                table: "ContactInquiries");

            migrationBuilder.DropIndex(
                name: "IX_ContactInquiries_ReviewedByUserId",
                table: "ContactInquiries");

            migrationBuilder.DropIndex(
                name: "IX_ContactInquiries_Status_SubmittedAt",
                table: "ContactInquiries");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "ApprovedTenantId",
                table: "ContactInquiries");

            migrationBuilder.DropColumn(
                name: "BusinessName",
                table: "ContactInquiries");

            migrationBuilder.DropColumn(
                name: "BusinessType",
                table: "ContactInquiries");

            migrationBuilder.DropColumn(
                name: "ReviewRemarks",
                table: "ContactInquiries");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "ContactInquiries");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "ContactInquiries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ContactInquiries");
        }
    }
}
