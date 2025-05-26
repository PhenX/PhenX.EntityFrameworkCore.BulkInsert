using MySqlConnector;

using NetTopologySuite.Geometries;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.MySql;

internal sealed class MySqlGeometryConverter : IBulkValueConverter
{
    public static readonly MySqlGeometryConverter Instance = new();

    private MySqlGeometryConverter()
    {
    }

    public bool TryConvertValue(object source, BulkInsertOptions options, out object result)
    {
        if (source is Geometry geometry)
        {
            result = MySqlGeometry.FromWkb(options.SRID, geometry.ToBinary());
            return true;
        }

        result = source;
        return false;
    }
}
