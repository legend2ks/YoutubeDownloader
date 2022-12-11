using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using YoutubeApp.Models;

namespace YoutubeApp.Database;

public class SettingsData
{
    private readonly SQLiteConnection _dbConn;

    public SettingsData(SqliteDbAccessProvider sqliteDbAccessProvider)
    {
        _dbConn = sqliteDbAccessProvider.Connection;
    }

    public List<SettingItem> GetSettings()
    {
        var result = _dbConn.Query<SettingItem>("SELECT * FROM Configs");
        return result.ToList();
    }

    public void SaveSetting(string key, string value)
    {
        const string stmt = @"INSERT INTO Configs (Key, Value) VALUES (@Key, @Value) 
                        ON CONFLICT DO UPDATE SET Value = @Value";
        _dbConn.Execute(stmt, new { Key = key, Value = value });
    }
}