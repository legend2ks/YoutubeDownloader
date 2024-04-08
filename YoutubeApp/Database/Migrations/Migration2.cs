using System.Data.SQLite;
using Dapper;

namespace YoutubeApp.Database.Migrations;

public static class Migration2
{
    public static void Apply(SQLiteConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        connection.Execute("ALTER TABLE Downloads ADD Chapters TEXT", null, transaction);
        connection.Execute("PRAGMA user_version = 2", null, transaction);
        transaction.Commit();
    }
}