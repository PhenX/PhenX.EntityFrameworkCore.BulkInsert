using System.Data.SqlTypes;

using Microsoft.SqlServer.Types;

using NetTopologySuite.Geometries;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Options;

namespace PhenX.EntityFrameworkCore.BulkInsert.SqlServer;

internal sealed class SqlServerGeometryConverter : IBulkValueConverter
{
    public static readonly SqlServerGeometryConverter Instance = new();

    private SqlServerGeometryConverter()
    {
    }

    public bool TryConvertValue(object source, BulkInsertOptions options, out object result)
    {
        if (source is Geometry geometry)
        {
            var reversed = Reverse(geometry);
            result = SqlGeometry.STGeomFromWKB(new SqlBytes(reversed.AsBinary()), options.SRID);
            return true;
        }

        result = source;
        return false;
    }

    private static Geometry Reverse(Geometry input)
    {
        switch (input)
        {
            case Point point:
                return Reverse(point);

            case LineString lineString:
                return Reverse(lineString);

            case Polygon polygon:
                return Reverse(polygon);

            case MultiPoint multiPoint:
                return Reverse(multiPoint);

            case MultiLineString multiLineString:
                return Reverse(multiLineString);

            case MultiPolygon mpoly:
                return Reverse(mpoly);

            case GeometryCollection gc:
                return Reverse(gc);

            default:
                throw new NotSupportedException($"Unsupported geometry type: {input.GeometryType}");
        }
    }

    private static Point Reverse(Point input)
    {
        return input.Factory.CreatePoint(Swap(input.Coordinate));
    }

    private static LineString Reverse(LineString input)
    {
        return input.Factory.CreateLineString(Swap(input.Coordinates));
    }

    private static MultiPoint Reverse(MultiPoint input)
    {
        return input.Factory.CreateMultiPoint(input.Geometries.OfType<Point>().Select(Reverse).ToArray());
    }

    private static MultiLineString Reverse(MultiLineString input)
    {
        return input.Factory.CreateMultiLineString(input.Geometries.OfType<LineString>().Select(Reverse).ToArray());
    }

    private static MultiPolygon Reverse(MultiPolygon input)
    {
        return input.Factory.CreateMultiPolygon(input.Geometries.OfType<Polygon>().Select(Reverse).ToArray());
    }

    private static GeometryCollection Reverse(GeometryCollection input)
    {
        return input.Factory.CreateGeometryCollection(input.Geometries.Select(Reverse).ToArray());
    }

    private static Polygon Reverse(Polygon input)
    {
        var factory = input.Factory;

        return input.Factory.CreatePolygon(
            factory.CreateLinearRing(Swap(input.Shell.Coordinates)),
            input.Holes.Select(h => factory.CreateLinearRing(Swap(h.Coordinates))).ToArray());
    }

    private static Coordinate Swap(Coordinate c) => new Coordinate(c.Y, c.X);

    private static Coordinate[] Swap(Coordinate[] coords) => coords.Select(Swap).ToArray();

}
