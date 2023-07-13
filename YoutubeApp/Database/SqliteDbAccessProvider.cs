using System.Data.SQLite;
using Dapper;

namespace YoutubeApp.Database;

public class SqliteDbAccessProvider
{
    public SqliteDbAccessProvider()
    {
        const string connStr = "Data Source=./data.dat;Version=3;foreign keys=True;";
        Connection = new SQLiteConnection(connStr);
        Connection.Open();
        Initialize();
    }

    private void Initialize()
    {
        Connection.Execute(CreateDownloadsTableSql);
        Connection.Execute(CreateDownloadFilesTableSql);
        Connection.Execute(CreateChannelsTableSql);
        Connection.Execute(CreateVideosTableSql);
        Connection.Execute(CreateChannelCategoriesTableSql);
        Connection.Execute(CreateConfigsTableSql);
    }

    public SQLiteConnection Connection { get; }

    private const string CreateDownloadsTableSql = @"
        CREATE TABLE IF NOT EXISTS Downloads (
            Id INTEGER NOT NULL,
            VideoId TEXT NOT NULL,
            Title TEXT NOT NULL,
            Priority INTEGER NOT NULL,
            SelectedVariant TEXT,
            Duration TEXT,
            Filename TEXT,
            Container TEXT,
            Formats TEXT,
            Variants TEXT,
            SaveTo TEXT,
            Uuid TEXT NOT NULL,
            UploadDate DATE,
            ChannelId TEXT,
            Enabled INTEGER NOT NULL DEFAULT 0,
            BytesLoaded INTEGER NOT NULL DEFAULT 0,
            Completed INTEGER NOT NULL DEFAULT 0,
            ChannelTitle TEXT,
            Filesize NUMBER,
            PRIMARY KEY (Id)
        )";

    private const string CreateDownloadFilesTableSql = @"
        CREATE TABLE IF NOT EXISTS DownloadFiles (
            DownloadId INTEGER NOT NULL,
            FileId TEXT NOT NULL,
            Filesize INTEGER NOT NULL,
            PRIMARY KEY (DownloadId, FileId),
            FOREIGN KEY (DownloadId)
                REFERENCES Downloads (Id)
        )";

    private const string CreateChannelsTableSql = @"
        CREATE TABLE IF NOT EXISTS Channels (
            Id INTEGER NOT NULL,
            UniqueId TEXT NOT NULL,
            ListId TEXT NOT NULL,
            Title TEXT NOT NULL,
            Path TEXT NOT NULL,
            VideoCount INTEGER,
            CategoryId INTEGER,
            LastUpdate TEXT,
            PRIMARY KEY (Id),
            FOREIGN KEY (CategoryId)
                REFERENCES ChannelCategories (Id)
                ON DELETE CASCADE,
            UNIQUE (ListId ASC)
        )";

    private const string CreateVideosTableSql = @"
        CREATE TABLE IF NOT EXISTS Videos (
            Id INTEGER NOT NULL,
            VideoId TEXT NOT NULL,
            ChannelId INTEGER NOT NULL,
            Title TEXT NOT NULL,
            Watched INTEGER NOT NULL DEFAULT 0,
            Duration TEXT NOT NULL,
            Timestamp INTEGER NOT NULL,
            PublishedAt TEXT,
            PRIMARY KEY (Id),
            FOREIGN KEY (ChannelId)
                REFERENCES Channels (Id)
                ON DELETE CASCADE
        )";

    private const string CreateChannelCategoriesTableSql = @"
        CREATE TABLE IF NOT EXISTS ChannelCategories (
            Id INTEGER NOT NULL,
            Title TEXT NOT NULL,
            Position INTEGER NOT NULL,
            Parent INTEGER,
            PRIMARY KEY (Id),
            FOREIGN KEY (Parent)
                REFERENCES ChannelCategories (Id)
                ON DELETE CASCADE
        )";

    private const string CreateConfigsTableSql = @"
        CREATE TABLE IF NOT EXISTS Configs (
            Key TEXT NOT NULL,
            Value TEXT,
            PRIMARY KEY (Key)
        )";
}