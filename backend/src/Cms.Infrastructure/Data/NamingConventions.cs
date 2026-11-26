using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Data;

/// <summary>
/// Converts PascalCase EF names to the snake_case names used by the hand-written SQL scripts.
/// Only table and column names are rewritten; explicit index and FK names are left untouched.
/// </summary>
public static class NamingConventions
{
    public static void ApplySnakeCase(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Preserve only the known legacy Dev 3 outbox mapping. The canonical
            // V4 mapping uses common.outbox_messages and must stay snake_case.
            var isLegacyNotifOutbox =
                string.Equals(entityType.GetSchema(), "notif", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entityType.GetTableName(), "OutboxMessages", StringComparison.OrdinalIgnoreCase);

            if (isLegacyNotifOutbox)
            {
                continue;
            }

            var tableName = entityType.GetTableName();
            if (!string.IsNullOrWhiteSpace(tableName))
            {
                entityType.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName();
                if (!string.IsNullOrWhiteSpace(columnName))
                {
                    property.SetColumnName(ToSnakeCase(columnName));
                }
            }
        }
    }

    public static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length + 8);

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];

            if (current is ' ' or '-')
            {
                builder.Append('_');
                continue;
            }

            if (char.IsUpper(current))
            {
                var hasPrevious = i > 0;
                var previousIsLowerOrDigit = hasPrevious &&
                    (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]));
                var nextIsLower = i + 1 < value.Length && char.IsLower(value[i + 1]);

                if (hasPrevious && (previousIsLowerOrDigit || nextIsLower) && builder[^1] != '_')
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }
}
