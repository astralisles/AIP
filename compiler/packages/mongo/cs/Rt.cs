public static partial class Rt
{
// MongoConn is erased to object (a MongoDbConn). Documents, filters and updates are JSON text (extended JSON).
sealed class MongoDbConn { public MongoDB.Driver.MongoClient Client = null!; public MongoDB.Driver.IMongoDatabase Db = null!; }
public static object MongoOpen(string url, string database)
{
    var c = new MongoDbConn();
    c.Client = new MongoDB.Driver.MongoClient(url);
    c.Db = c.Client.GetDatabase(database);
    c.Db.RunCommand<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("ping", 1)); // fail now if the server is unreachable
    return c;
}
static MongoDB.Driver.IMongoCollection<MongoDB.Bson.BsonDocument> MongoColl(object conn, string name) => ((MongoDbConn)conn).Db.GetCollection<MongoDB.Bson.BsonDocument>(name);
static MongoDB.Driver.BsonDocumentFilterDefinition<MongoDB.Bson.BsonDocument> MongoFilter(string json) => new MongoDB.Driver.BsonDocumentFilterDefinition<MongoDB.Bson.BsonDocument>(MongoDB.Bson.BsonDocument.Parse(json));
static string MongoId(MongoDB.Bson.BsonValue v) => v.IsObjectId ? v.AsObjectId.ToString() : v.IsString ? v.AsString : v.ToString() ?? "";
public static string MongoInsert(object conn, string collection, string doc)
{
    var d = MongoDB.Bson.BsonDocument.Parse(doc);
    MongoColl(conn, collection).InsertOne(d);
    return MongoId(d["_id"]);
}
public static Aip.Persistent.PList MongoFind(object conn, string collection, string filter)
{
    var list = Aip.Persistent.PList.Empty;
    var settings = new MongoDB.Bson.IO.JsonWriterSettings { OutputMode = MongoDB.Bson.IO.JsonOutputMode.RelaxedExtendedJson };
    foreach (var d in MongoDB.Driver.IAsyncCursorExtensions.ToList(MongoColl(conn, collection).FindSync<MongoDB.Bson.BsonDocument>(MongoFilter(filter)))) list = list.Append(MongoDB.Bson.BsonExtensionMethods.ToJson(d, settings));
    return list;
}
public static long MongoUpdate(object conn, string collection, string filter, string update)
{
    var u = new MongoDB.Driver.BsonDocumentUpdateDefinition<MongoDB.Bson.BsonDocument>(MongoDB.Bson.BsonDocument.Parse(update));
    return MongoColl(conn, collection).UpdateMany(MongoFilter(filter), u).ModifiedCount;
}
public static long MongoDelete(object conn, string collection, string filter) => MongoColl(conn, collection).DeleteMany(MongoFilter(filter)).DeletedCount;
public static void MongoDrop(object conn, string collection) { ((MongoDbConn)conn).Db.DropCollection(collection); }
public static void MongoClose(object conn) { ((MongoDbConn)conn).Client.Dispose(); }
}
