using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Customers.Infrastructure.Migrations;

[DbContext(typeof(CustomersDbContext))]
[Migration("20261007220000_InitialCreate")]
public sealed class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "customers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                PersonType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Document = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                FoundationDate = table.Column<DateOnly>(type: "date", nullable: true),
                StateRegistration = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                MunicipalRegistration = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                Phone = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_customers", x => x.Id));

        migrationBuilder.CreateTable(
            name: "customer_addresses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                PostalCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                Street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Complement = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                Neighborhood = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                State = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                IbgeCityCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                Latitude = table.Column<decimal>(type: "numeric(12,8)", precision: 12, scale: 8, nullable: true),
                Longitude = table.Column<decimal>(type: "numeric(12,8)", precision: 12, scale: 8, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_customer_addresses", x => x.Id);
                table.ForeignKey(
                    name: "FK_customer_addresses_customers_CustomerId",
                    column: x => x.CustomerId,
                    principalTable: "customers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_customer_addresses_CustomerId",
            table: "customer_addresses",
            column: "CustomerId");

        migrationBuilder.CreateIndex(
            name: "IX_customer_addresses_PostalCode",
            table: "customer_addresses",
            column: "PostalCode");

        migrationBuilder.CreateIndex(
            name: "IX_customers_DisplayName",
            table: "customers",
            column: "DisplayName");

        migrationBuilder.CreateIndex(
            name: "IX_customers_Document",
            table: "customers",
            column: "Document",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_customers_Email",
            table: "customers",
            column: "Email");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "customer_addresses");
        migrationBuilder.DropTable(name: "customers");
    }
}
