using NetTopologySuite.Geometries;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.PostgreSql;

internal sealed class PostgreSqlGeometryConverter : IBulkValueConverter
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

            result = geometry;
            return true;
        }

        result = source;
        return false;
    }
}
