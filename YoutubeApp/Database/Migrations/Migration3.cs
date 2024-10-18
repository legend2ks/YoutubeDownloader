using System.Data.SQLite;
using Dapper;

namespace YoutubeApp.Database.Migrations;

public static class Migration3
{
    public static void Apply(SQLiteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        connection.Execute("ALTER TABLE Downloads ADD MissingFormats INTEGER NOT NULL DEFAULT 0", null, transaction);
        connection.Execute("PRAGMA user_version = 3", null, transaction);
        transaction.Commit();
    }
}