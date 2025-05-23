using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace PhenX.EntityFrameworkCore.BulkInsert.Tests.DbContext;

public static class Extensions
{
    public static PropertyBuilder<T> AsJsonString<T>(this PropertyBuilder<T> propertyBuilder, string? columnType)
         where T : class
    {
        var converter = new ValueConverter<T, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<T>(v, (JsonSerializerOptions?)null)!
        );

        propertyBuilder.HasConversion(converter).HasColumnType(columnType);
        return propertyBuilder;
    }

}
