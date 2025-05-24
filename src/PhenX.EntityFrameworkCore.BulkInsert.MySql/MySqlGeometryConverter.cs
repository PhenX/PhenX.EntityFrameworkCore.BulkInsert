using MySqlConnector;

using NetTopologySuite.Geometries;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.MySql;

internal sealed class MySqlGeometryConverter : IValueConverter
{
    public static readonly MySqlGeometryConverter Instance = new();

    private MySqlGeometryConverter()
    {
    }

    public bool TryConvertValue(object source, out object result)
    {
        if (source is Geometry geometry)
        {
            result = MySqlGeometry.FromWkb(geometry.SRID, geometry.ToBinary());
            return true;
        }

        result = source;
        return false;
    }
}
