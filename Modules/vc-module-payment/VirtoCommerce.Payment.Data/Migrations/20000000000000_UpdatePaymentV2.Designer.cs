// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using VirtoCommerce.PaymentModule.Data.Repositories;

namespace VirtoCommerce.PaymentModule.Data.Migrations
{
    [DbContext(typeof(PaymentDbContext))]
    [Migration("20000000000000_UpdatePaymentV2")]
    partial class UpdatePaymentV2
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.2.3-servicing-35854")
                .HasAnnotation("Relational:MaxIdentifierLength", 128)
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("VirtoCommerce.PaymentModule.Data.Model.StorePaymentMethodEntity", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(128);

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(128);

                    b.Property<string>("Description");

                    b.Property<bool>("IsActive");

                    b.Property<bool>("IsAvailableForPartial");

                    b.Property<string>("LogoUrl")
                        .HasMaxLength(2048);

                    b.Property<string>("Name")
                        .HasMaxLength(128);

                    b.Property<int>("Priority");

                    b.Property<string>("StoreId")
                        .HasMaxLength(128);

                    b.Property<string>("TypeName")
                        .HasMaxLength(128);

                    b.HasKey("Id");

                    b.HasIndex("TypeName", "StoreId")
                        .IsUnique()
                        .HasName("IX_StorePaymentMethodEntity_TypeName_StoreId")
                        .HasFilter("[StoreId] IS NOT NULL");

                    b.ToTable("StorePaymentMethod");
                });
#pragma warning restore 612, 618
        }
    }
}
