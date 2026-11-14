namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Emitted (with the Microsoft.Data.Sqlite package reference) only for programs that call a db function.</summary>
    const string RtDb = """
        public static partial class Rt
        {
        // DbConn is erased to object. Values travel as text so the API does not depend on one database engine.
        public static object DbOpen(string url)
        {
            const string p = "sqlite:";
            if (!url.StartsWith(p, System.StringComparison.Ordinal)) throw new System.ArgumentException("unsupported database url (use sqlite:<file> or sqlite::memory:): " + url);
            var rest = url.Substring(p.Length);
            var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + rest);
            c.Open();
            return c;
        }
        static Microsoft.Data.Sqlite.SqliteCommand DbCmd(object conn, string sql, Aip.Persistent.PList args)
        {
            var cmd = ((Microsoft.Data.Sqlite.SqliteConnection)conn).CreateCommand();
            cmd.CommandText = sql;
            int n = 0; var sb = new System.Text.StringBuilder(); foreach (var ch in sql) { if (ch == '?') sb.Append("@p" + (n++)); else sb.Append(ch); } cmd.CommandText = sb.ToString(); n = 0; foreach (var a in args) cmd.Parameters.Add(new Microsoft.Data.Sqlite.SqliteParameter("@p" + (n++), (string)a));
            return cmd;
        }
        public static long DbExec(object conn, string sql, Aip.Persistent.PList args)
        {
            using var cmd = DbCmd(conn, sql, args);
            return cmd.ExecuteNonQuery();
        }
        public static Aip.Persistent.PList DbQuery(object conn, string sql, Aip.Persistent.PList args)
        {
            using var cmd = DbCmd(conn, sql, args);
            using var r = cmd.ExecuteReader();
            var rows = Aip.Persistent.PList.Empty;
            while (r.Read())
            {
                var row = Aip.Persistent.PList.Empty;
                for (int i = 0; i < r.FieldCount; i++)
                    row = row.Append(r.IsDBNull(i) ? "" : System.Convert.ToString(r.GetValue(i), System.Globalization.CultureInfo.InvariantCulture) ?? "");
                rows = rows.Append(row);
            }
            return rows;
        }
        public static void DbClose(object conn) { ((Microsoft.Data.Sqlite.SqliteConnection)conn).Dispose(); }
        }
        """;

    /// <summary>The project file for a program that uses optional NuGet packages (db: SQLite, mongo: MongoDB.Driver).</summary>
    static string CsprojWithPackages(bool db, bool mongo)
    {
        if (!db && !mongo) return Csproj;
        var refs = (db ? "<PackageReference Include=\"Microsoft.Data.Sqlite\" Version=\"10.0.13\" />" : "")
                 + (mongo ? "<PackageReference Include=\"MongoDB.Driver\" Version=\"3.13.0\" />" : "");
        return Csproj.Replace("</PropertyGroup>", "</PropertyGroup><ItemGroup>" + refs + "</ItemGroup>");
    }
}
