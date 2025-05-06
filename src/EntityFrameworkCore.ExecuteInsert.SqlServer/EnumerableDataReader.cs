using System.Data;

namespace EntityFrameworkCore.ExecuteInsert.SqlServer;

public class EnumerableDataReader<T> : IDataReader
{
    private readonly IEnumerator<T> _enumerator;
    private readonly PropertyAccessor[] _properties;
    private readonly Dictionary<string, int> _ordinalMap;

    public EnumerableDataReader(IEnumerable<T> rows, PropertyAccessor[] properties)
    {
        _enumerator = rows.GetEnumerator();
        _properties = properties;
        _ordinalMap = properties
            .Select((p, i)  => new
            {
                Property = p,
                Index = i,
            })
            .ToDictionary(
                p => p.Property.Name,
                p => p.Index
            );
    }

    public virtual object GetValue(int i)
    {
        if (_enumerator.Current != null)
        {
            return _properties[i].GetValue(_enumerator.Current);
        }

        return DBNull.Value;
    }

    public bool Read() => _enumerator.MoveNext();

    public int FieldCount => _properties.Length;
    public Type GetFieldType(int i) => _properties[i].ProviderClrType;

    public int GetOrdinal(string name) => _ordinalMap.GetValueOrDefault(name, -1);

    public int Depth => 0;
    public bool IsClosed => false;
    public int RecordsAffected => 0;

    public void Close()
    {
    }

    public void Dispose()
    {
        _enumerator.Dispose();
    }

    public DataTable GetSchemaTable() => throw new NotImplementedException();

    public bool NextResult() => throw new NotImplementedException();

    public int GetValues(object[] values) => throw new NotImplementedException();

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
