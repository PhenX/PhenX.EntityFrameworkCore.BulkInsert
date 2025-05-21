using System.Data.Common;

using Microsoft.EntityFrameworkCore.Storage;

namespace PhenX.EntityFrameworkCore.BulkInsert.Extensions;

internal readonly record struct ConnectionInfo(DbConnection Connection, bool WasClosed, IDbContextTransaction Transaction, bool WasBegan);
