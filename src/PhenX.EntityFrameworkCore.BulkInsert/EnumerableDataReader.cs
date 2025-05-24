using System.Data;

using PhenX.EntityFrameworkCore.BulkInsert.Abstractions;
using PhenX.EntityFrameworkCore.BulkInsert.Metadata;

namespace PhenX.EntityFrameworkCore.BulkInsert;

internal sealed class EnumerableDataReader<T>(IEnumerable<T> rows, IReadOnlyList<ColumnMetadata> columns, List<IValueConverter>? converters) : IDataReader
{
    private readonly IEnumerator<T> _enumerator = rows.GetEnumerator();
    private readonly Dictionary<string, int> _ordinalMap =
        columns
            .Select((c, i) => (Column: c, Index: i))
            .ToDictionary(
                p => p.Column.PropertyName,
                p => p.Index
            );

    public object GetValue(int i)
    {
        var current = _enumerator.Current;
        if (current == null)
        {
            return DBNull.Value;
        }

        return GetAndConvertValue(columns[i], current);
    }

    public int GetValues(object[] values)
    {
        var current = _enumerator.Current;
        if (current == null)
        {
            return 0;
        }

        for (var i = 0; i < columns.Count; i++)
        {
            values[i] = GetAndConvertValue(columns[i], current);
        }

        return columns.Count;
    }

    private object GetAndConvertValue(ColumnMetadata column, T entity)
    {
        var result = column.GetValue(entity!)!;
        if (converters != null)
        {
            foreach (var converter in converters)
            {
                if (converter.TryConvertValue(result, out var temp))
                {
                    result = temp;
                    break;
                }
            }
        }

        return result;
    }

    public bool Read() => _enumerator.MoveNext();

    public Type GetFieldType(int i) => columns[i].ClrType;

    public int GetOrdinal(string name) => _ordinalMap.GetValueOrDefault(name, -1);

    public int FieldCount => columns.Count;

    public int Depth => 0;

    public int RecordsAffected => 0;

    public bool IsClosed => false;


    public void Close()
    {
    }

    public void Dispose()
    {
        _enumerator.Dispose();
    }

    public DataTable GetSchemaTable() => throw new NotImplementedException();

    public bool NextResult() => throw new NotImplementedException();

    public bool IsDBNull(int i) => GetValue(i) is DBNull;

    public object this[int i] => throw new NotImplementedException();

    public object this[string name] => throw new NotImplementedException();

    public string GetString(int i) => throw new NotImplementedException();

    public bool GetBoolean(int i) => throw new NotImplementedException();

    public byte GetByte(int i) => throw new NotImplementedException();

    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotImplementedException();

    public char GetChar(int i) => throw new NotImplementedException();

    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotImplementedException();

    public IDataReader GetData(int i) => throw new NotImplementedException();

    public string GetDataTypeName(int i) => throw new NotImplementedException();

    public DateTime GetDateTime(int i) => throw new NotImplementedException();

    public decimal GetDecimal(int i) => throw new NotImplementedException();

    public double GetDouble(int i) => throw new NotImplementedException();

    public float GetFloat(int i) => throw new NotImplementedException();

    public Guid GetGuid(int i) => throw new NotImplementedException();

    public short GetInt16(int i) => throw new NotImplementedException();

    public int GetInt32(int i) => throw new NotImplementedException();

    public long GetInt64(int i) => throw new NotImplementedException();

    public string GetName(int i) => throw new NotImplementedException();
}
