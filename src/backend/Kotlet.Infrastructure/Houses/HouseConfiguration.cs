using Kotlet.Domain.Houses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kotlet.Infrastructure.Houses;

internal sealed class HouseConfiguration : IEntityTypeConfiguration<House>
{
    public void Configure(EntityTypeBuilder<House> builder)
    {
        builder.ToTable("houses");
        builder.HasKey(house => house.Id);
        builder.Property(house => house.Id).HasColumnName("id");
        builder.Property(house => house.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.HasData(new House { Id = DefaultHouse.Id, Name = DefaultHouse.Name });
    }
}
