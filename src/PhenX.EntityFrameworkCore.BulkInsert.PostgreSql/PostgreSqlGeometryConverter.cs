using Microsoft.EntityFrameworkCore.Metadata;

using NetTopologySuite.Geometries;

using NpgsqlTypes;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

internal sealed class PostgreSqlGeometryConverter : IBulkValueConverter, IPostgresTypeProvider
{
    public static readonly PostgreSqlGeometryConverter Instance = new();

    private PostgreSqlGeometryConverter()
    {
    }

    public bool TryConvertValue(object source, BulkInsertOptions options, out object result)
    {
        if (source is Geometry geometry)
        {
            if (geometry.SRID != options.SRID)
            {
                geometry = geometry.Copy();
                geometry.SRID = options.SRID;
            }

            result = geometry.ToBinary();
            return true;
        }

        result = source;
        return false;
    }

    public bool TryGetType(IProperty property, out NpgsqlDbType result)
    {
        if (property.ClrType.IsAssignableTo(typeof(Geometry)))
        {
            result = NpgsqlDbType.Bytea;
            return true;
        }

        result = default;
        return false;
    }
}
