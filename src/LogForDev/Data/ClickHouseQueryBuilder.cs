using System.Text;

namespace LogForDev.Data;

public class ClickHouseQueryBuilder
{
    private readonly string _tableName;
    private readonly List<string> _selectColumns = new();
    private readonly List<WhereClause> _whereClauses = new();
    private readonly List<OrderByClause> _orderByClauses = new();
    private int? _limit;
    private int? _offset;

    public ClickHouseQueryBuilder(string tableName)
    {
        _tableName = tableName ?? throw new ArgumentNullException(nameof(tableName));
    }

    public ClickHouseQueryBuilder Select(params string[] columns)
    {
        _selectColumns.AddRange(columns);
        return this;
    }

    public ClickHouseQueryBuilder Where(string column, object value)
    {
        return Where(column, "=", value);
    }

    public ClickHouseQueryBuilder Where(string column, string @operator, object value)
    {
        _whereClauses.Add(new WhereClause
        {
            Column = column,
            Operator = @operator,
            Value = value
        });
        return this;
    }

    public ClickHouseQueryBuilder WhereIn(string column, IEnumerable<object> values)
    {
        var valueList = values.ToList();
        if (valueList.Any())
        {
            _whereClauses.Add(new WhereClause
            {
                Column = column,
                Operator = "IN",
                Value = valueList
            });
        }
        return this;
    }

    public ClickHouseQueryBuilder WhereLike(string column, string value)
    {
        _whereClauses.Add(new WhereClause
        {
            Column = column,
            Operator = "ILIKE",
            Value = $"%{value}%"
        });
        return this;
    }

    public ClickHouseQueryBuilder WhereBetween(string column, object from, object to)
    {
        Where(column, ">=", from);
        Where(column, "<=", to);
        return this;
    }

    public ClickHouseQueryBuilder OrderBy(string column, string direction = "ASC")
    {
        _orderByClauses.Add(new OrderByClause
        {
            Column = column,
            Direction = direction.ToUpper()
        });
        return this;
    }

    public ClickHouseQueryBuilder OrderByDesc(string column)
    {
        return OrderBy(column, "DESC");
    }

    public ClickHouseQueryBuilder Limit(int limit)
    {
        _limit = limit;
        return this;
    }

    public ClickHouseQueryBuilder Offset(int offset)
    {
        _offset = offset;
        return this;
    }

    public ClickHouseQueryBuilder Paginate(int page, int pageSize)
    {
        _limit = pageSize;
        _offset = (page - 1) * pageSize;
        return this;
    }

    public string BuildSelect()
    {
        var sql = new StringBuilder();

        // SELECT clause
        if (_selectColumns.Any())
        {
            sql.Append("SELECT ");
            sql.Append(string.Join(", ", _selectColumns));
        }
        else
        {
            sql.Append("SELECT *");
        }

        // FROM clause
        sql.Append($" FROM {_tableName}");

        // WHERE clause
        if (_whereClauses.Any())
        {
            sql.Append(" WHERE ");
            var whereConditions = _whereClauses.Select(BuildWhereCondition);
            sql.Append(string.Join(" AND ", whereConditions));
        }

        // ORDER BY clause
        if (_orderByClauses.Any())
        {
            sql.Append(" ORDER BY ");
            var orderByParts = _orderByClauses.Select(o => $"{o.Column} {o.Direction}");
            sql.Append(string.Join(", ", orderByParts));
        }

        // LIMIT clause
        if (_limit.HasValue)
        {
            sql.Append($" LIMIT {_limit.Value}");
        }

        // OFFSET clause
        if (_offset.HasValue)
        {
            sql.Append($" OFFSET {_offset.Value}");
        }

        return sql.ToString();
    }

    public string BuildCount()
    {
        var sql = new StringBuilder();
        sql.Append($"SELECT count() FROM {_tableName}");

        if (_whereClauses.Any())
        {
            sql.Append(" WHERE ");
            var whereConditions = _whereClauses.Select(BuildWhereCondition);
            sql.Append(string.Join(" AND ", whereConditions));
        }

        return sql.ToString();
    }

    private string BuildWhereCondition(WhereClause clause)
    {
        var escapedValue = clause.Operator.ToUpper() switch
        {
            "IN" when clause.Value is IEnumerable<object> values =>
                $"('{string.Join("','", values.Select(v => EscapeString(v?.ToString() ?? "")))}')",
            "ILIKE" =>
                $"'%{EscapeString(clause.Value?.ToString() ?? "")}%'",
            _ =>
                $"'{EscapeString(clause.Value?.ToString() ?? "")}'",
        };

        return clause.Operator.ToUpper() switch
        {
            "IN" => $"{clause.Column} IN {escapedValue}",
            "ILIKE" => $"{clause.Column} ILIKE {escapedValue}",
            _ => $"{clause.Column} {clause.Operator} {escapedValue}",
        };
    }

    private static string EscapeString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace("\\", "\\\\")
            .Replace("'", "\\'");
    }

    private class WhereClause
    {
        public string Column { get; set; } = string.Empty;
        public string Operator { get; set; } = "=";
        public object? Value { get; set; }
    }

    private class OrderByClause
    {
        public string Column { get; set; } = string.Empty;
        public string Direction { get; set; } = "ASC";
    }
}
