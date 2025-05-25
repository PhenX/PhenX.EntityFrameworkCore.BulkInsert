using NetTopologySuite.Geometries;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;

namespace PhenX.EntityFrameworkCore.BulkInsert.Oracle;

internal sealed class OracleGeometryConverter : IBulkValueConverter
{
    public static readonly OracleGeometryConverter Instance = new();

    private OracleGeometryConverter()
    {
    }

    public bool TryConvertValue(object source, out object result)
    {
        if (source is Geometry geometry)
        {
            // result = SqlGeometry.STGeomFromWKB(new SqlBytes(reversed.AsBinary()), geometry.SRID);
            result = null!;
            return true;
        }

        result = source;
        return false;
    }
}
