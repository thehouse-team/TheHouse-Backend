using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;
using Xunit.Abstractions;

namespace TheHouse.IntegrationTests;

// NV-26: el script de la base de datos (scripts/db/) debe ser idéntico al que
// se genera desde el código. Para regenerarlo después de cambiar un índice,
// una colección o un catálogo:
//
//   $env:THEHOUSE_WRITE_DB_SCRIPTS = "1"; dotnet test TheHouse.IntegrationTests --filter DatabaseScriptTests
//
// La prueba contra un MongoDB real es opcional y la activan dos variables
// (ver scripts/db/README.md): THEHOUSE_DBSCRIPT_MONGO y THEHOUSE_DBSCRIPT_DB.
public sealed partial class DatabaseScriptTests(ITestOutputHelper output)
{
    private const string WriteVariable = "THEHOUSE_WRITE_DB_SCRIPTS";

    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Schema_script_matches_the_indexes_and_collections_of_the_code()
    {
        var schema = DatabaseScriptGenerator.RecordSchema(Root);

        Assert.Equal(34, schema.Collections.Count);
        Assert.Equal(["documents", "media"], schema.Buckets);
        Assert.All(schema.Indexes, index => Assert.Contains(index.Collection, schema.Collections));
        Assert.Equal(
            schema.Indexes.Count,
            schema.Indexes.Select(index => (index.Collection, index.Options["name"].AsString)).Distinct().Count());

        AssertFile(DatabaseScriptGenerator.SchemaFile, DatabaseScriptGenerator.RenderSchema(schema));
    }

    [Fact]
    public async Task Catalog_script_matches_the_seed_data_of_the_code()
    {
        var writes = await DatabaseScriptGenerator.RecordCatalogsAsync();

        Assert.Equal(10, writes.Count(write => write.Collection == "universities"));
        Assert.Equal(12, writes.Count(write => write.Collection == "properties"));
        Assert.Equal(60, writes.Count(write => write.Collection == "places"));
        Assert.Equal(82, writes.Count);

        AssertFile(DatabaseScriptGenerator.CatalogFile, DatabaseScriptGenerator.RenderCatalogs(writes));
    }

    [Fact]
    public void Scripts_contain_no_secrets_or_personal_data()
    {
        foreach (var file in Directory.EnumerateFiles(Path.Combine(Root, "scripts", "db")))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotMatch(Email(), text);
            Assert.DoesNotMatch(Phone(), text);
            Assert.DoesNotMatch(ConnectionString(), text);
            Assert.DoesNotMatch(Secret(), text);
        }
    }

    [Fact]
    public void Scripts_never_drop_or_delete()
    {
        foreach (var file in new[] { DatabaseScriptGenerator.SchemaFile, DatabaseScriptGenerator.CatalogFile })
        {
            var text = File.ReadAllText(Path.Combine(Root, file));
            Assert.DoesNotMatch(Destructive(), text);
        }
    }

    // Prueba opcional contra MongoDB real (T04): crea el esquema y los
    // catálogos con el CÓDIGO en una base temporal y los compara con la base
    // que creó el SCRIPT con mongosh. Sin las variables no hace nada.
    [Fact]
    public async Task Script_database_equals_code_database_on_real_mongodb()
    {
        var connection = Environment.GetEnvironmentVariable("THEHOUSE_DBSCRIPT_MONGO");
        var scriptDatabaseName = Environment.GetEnvironmentVariable("THEHOUSE_DBSCRIPT_DB");
        if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(scriptDatabaseName))
        {
            output.WriteLine("Omitida: THEHOUSE_DBSCRIPT_MONGO y THEHOUSE_DBSCRIPT_DB no están definidas.");
            return;
        }

        var client = new MongoClient(connection);
        var scriptDatabase = client.GetDatabase(scriptDatabaseName);
        var codeDatabase = client.GetDatabase($"nv26_code_{Guid.NewGuid():N}");
        try
        {
            var buckets = await DatabaseScriptGenerator.InitializeRepositoriesAsync(codeDatabase);
            foreach (var bucket in buckets)
            {
                // El driver crea los índices de GridFS al subir el primer archivo.
                var gridFs = new GridFSBucket(codeDatabase, new GridFSBucketOptions { BucketName = bucket });
                var id = await gridFs.UploadFromBytesAsync("nv-26", [1]);
                await gridFs.DeleteAsync(id);
            }

            var universities = new MongoUniversityRepository(codeDatabase);
            await new UniversitySeedService(universities).SeedAsync(default);
            await new PropertySeedService(new MongoPropertyRepository(codeDatabase), universities).SeedAsync(default);
            await new PlaceSeedService(new MongoPlaceRepository(codeDatabase)).SeedAsync(default);

            var schema = DatabaseScriptGenerator.RecordSchema(Root);
            var scriptCollections = (await (await scriptDatabase.ListCollectionNamesAsync()).ToListAsync()).ToHashSet(StringComparer.Ordinal);
            var expected = schema.Collections.Concat(schema.Buckets.SelectMany(bucket => new[] { $"{bucket}.files", $"{bucket}.chunks" })).ToHashSet(StringComparer.Ordinal);
            Assert.Equal(expected.Order(StringComparer.Ordinal), scriptCollections.Order(StringComparer.Ordinal));

            var codeCollections = await (await codeDatabase.ListCollectionNamesAsync()).ToListAsync();
            var compared = 0;
            foreach (var name in codeCollections.Order(StringComparer.Ordinal))
            {
                Assert.Contains(name, scriptCollections);
                var codeIndexes = await IndexesAsync(codeDatabase, name);
                var scriptIndexes = await IndexesAsync(scriptDatabase, name);
                Assert.Equal(codeIndexes, scriptIndexes);
                compared += codeIndexes.Count;
            }

            output.WriteLine($"Índices idénticos: {compared} en {codeCollections.Count} colecciones creadas por el código.");

            Assert.Equal(await CatalogAsync(codeDatabase, "universities", "code"), await CatalogAsync(scriptDatabase, "universities", "code"));
            Assert.Equal(await PropertiesAsync(codeDatabase), await PropertiesAsync(scriptDatabase));
            Assert.Equal(await CatalogAsync(codeDatabase, "places", "osmNodeId"), await CatalogAsync(scriptDatabase, "places", "osmNodeId"));
            output.WriteLine("Catálogos idénticos: universidades, propiedades y lugares.");
        }
        finally
        {
            await client.DropDatabaseAsync(codeDatabase.DatabaseNamespace.DatabaseName);
        }
    }

    private static async Task<List<string>> IndexesAsync(IMongoDatabase database, string collection)
    {
        var indexes = await (await database.GetCollection<BsonDocument>(collection).Indexes.ListAsync()).ToListAsync();
        return [.. indexes
            .Select(index => new BsonDocument(index.Elements.Where(element => element.Name != "ns").OrderBy(element => element.Name, StringComparer.Ordinal)))
            .Select(index => index.ToJson())
            .Order(StringComparer.Ordinal)];
    }

    // Documentos sin `_id` (lo asigna MongoDB), ordenados por su clave natural.
    private static async Task<List<string>> CatalogAsync(IMongoDatabase database, string collection, string key)
    {
        var documents = await database.GetCollection<BsonDocument>(collection).Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        return [.. documents
            .OrderBy(document => document[key].ToString(), StringComparer.Ordinal)
            .Select(document =>
            {
                document.Remove("_id");
                return document.ToJson();
            })];
    }

    // Propiedades con `universityId` traducido al código de la universidad.
    private static async Task<List<string>> PropertiesAsync(IMongoDatabase database)
    {
        var codes = (await database.GetCollection<BsonDocument>("universities").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync())
            .ToDictionary(document => document["_id"].AsObjectId.ToString(), document => document["code"].AsString, StringComparer.Ordinal);
        var documents = await database.GetCollection<BsonDocument>("properties").Find(FilterDefinition<BsonDocument>.Empty).ToListAsync();
        return [.. documents
            .OrderBy(document => document["_id"].AsObjectId)
            .Select(document =>
            {
                document["universityId"] = codes[document["universityId"].AsString];
                return document.ToJson();
            })];
    }

    private static void AssertFile(string relativePath, string expected)
    {
        var path = Path.Combine(Root, relativePath);
        if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, expected);
        }

        Assert.True(File.Exists(path), $"Falta {relativePath}. Regenera con {WriteVariable}=1.");
        var actual = File.ReadAllText(path).ReplaceLineEndings("\n");
        Assert.True(
            actual == expected,
            $"{relativePath} no coincide con el código. Regenera con {WriteVariable}=1 y revisa el diff.");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TheHouse.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encontró TheHouse.slnx.");
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    // Teléfonos con separadores o lada (+52); los ids numéricos de OpenStreetMap no cuentan.
    [GeneratedRegex(@"\+52|\(\d{2,3}\)\s?\d|\d{2,3}[ -]\d{3,4}[ -]\d{4}")]
    private static partial Regex Phone();

    [GeneratedRegex(@"mongodb(\+srv)?://", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectionString();

    [GeneratedRegex(@"password\s*[:=]|passwordHash|apiKey|secret\s*[:=]", RegexOptions.IgnoreCase)]
    private static partial Regex Secret();

    [GeneratedRegex(@"\.(drop|dropIndex|dropIndexes|dropDatabase|deleteOne|deleteMany|remove|findOneAndDelete)\(")]
    private static partial Regex Destructive();
}
