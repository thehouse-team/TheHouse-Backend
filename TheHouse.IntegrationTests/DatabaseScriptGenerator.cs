using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using TheHouse.Application.UseCases;
using TheHouse.Infrastructure.Persistence.Mongo;

namespace TheHouse.IntegrationTests;

// NV-26: genera `scripts/db/01-schema.mongodb.js` y `02-catalogs.mongodb.js`
// a partir del código. Los repositorios reales de MongoDB corren contra una
// base que solo registra (sin servidor): sus índices y las escrituras de los
// seeds de catálogo se convierten en llamadas de `mongosh`. Así el script
// nunca se edita a mano y la prueba detecta cualquier diferencia.
internal static partial class DatabaseScriptGenerator
{
    public const string SchemaFile = "scripts/db/01-schema.mongodb.js";
    public const string CatalogFile = "scripts/db/02-catalogs.mongodb.js";

    // Índices que el driver crea en cada bucket de GridFS al subir el primer
    // archivo (especificación de GridFS); el script los crea por adelantado.
    private static readonly (string Suffix, BsonDocument Keys, BsonDocument Options)[] GridFsIndexes =
    [
        ("files", new BsonDocument { { "filename", 1 }, { "uploadDate", 1 } }, new BsonDocument("name", "filename_1_uploadDate_1")),
        ("chunks", new BsonDocument { { "files_id", 1 }, { "n", 1 } }, new BsonDocument { { "name", "files_id_1_n_1" }, { "unique", true } }),
    ];

    private static readonly HashSet<string> SupportedIndexOptions =
        ["Name", "Unique", "Sparse", "ExpireAfter", "PartialFilterExpression"];

    public static Schema RecordSchema(string repositoryRoot)
    {
        var recorder = new Recorder();
        var database = RecordingDatabase.Create(recorder);
        foreach (var bucket in InitializeRepositoriesAsync(database).GetAwaiter().GetResult())
        {
            recorder.Buckets.Add(bucket);
        }

        foreach (var name in SourceCollectionNames(repositoryRoot))
        {
            recorder.Collections.Add(name);
        }

        return new Schema(
            [.. recorder.Collections.Order(StringComparer.Ordinal)],
            [.. recorder.Indexes.OrderBy(index => index.Collection, StringComparer.Ordinal)],
            [.. recorder.Buckets.Order(StringComparer.Ordinal)]);
    }

    public static async Task<IReadOnlyList<CatalogWrite>> RecordCatalogsAsync()
    {
        var recorder = new Recorder();
        var database = RecordingDatabase.Create(recorder);

        await new UniversitySeedService(new MongoUniversityRepository(database)).SeedAsync(default);

        // Los `_id` de universidad los asigna MongoDB al insertar: las
        // propiedades se ligan por código y el script resuelve el id al correr.
        var universities = new UniversityTestStore();
        await new UniversitySeedService(universities).SeedAsync(default);
        await new PropertySeedService(new MongoPropertyRepository(database), universities).SeedAsync(default);
        var codesById = universities.Universities.ToDictionary(university => university.Id, university => university.Code, StringComparer.Ordinal);

        await new PlaceSeedService(new MongoPlaceRepository(database)).SeedAsync(default);

        return [.. recorder.Writes.Select(write => write with { UniversityCodesById = codesById })];
    }

    // Ejecuta la creación de índices de cada repositorio de MongoDB, contra
    // la base de registro o contra un MongoDB real (prueba opcional).
    public static async Task<IReadOnlyList<string>> InitializeRepositoriesAsync(IMongoDatabase database)
    {
        var buckets = new List<string>();
        foreach (var type in RepositoryTypes())
        {
            var repository = Activator.CreateInstance(type, database)!;
            foreach (var member in Members(type))
            {
                var memberType = member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
                if (IsGeneric(memberType, typeof(IMongoCollection<>)))
                {
                    _ = Read(member, repository);
                }
                else if (memberType == typeof(GridFSBucket))
                {
                    buckets.Add(BucketName(member, repository));
                }
                else if (memberType.IsGenericType && memberType.GetGenericTypeDefinition().Name.StartsWith("UniqueUserIndex", StringComparison.Ordinal))
                {
                    var helper = Read(member, repository)!;
                    await (Task)memberType.GetMethod("EnsureAsync")!.Invoke(helper, [CancellationToken.None])!;
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
                         .Where(method => method.Name.StartsWith("Ensure", StringComparison.Ordinal) && method.Name.Contains("Index", StringComparison.Ordinal))
                         .Where(method => method.GetParameters() is [{ ParameterType: var parameter }] && parameter == typeof(CancellationToken))
                         .OrderBy(method => method.Name, StringComparer.Ordinal))
            {
                await (Task)method.Invoke(repository, [CancellationToken.None])!;
            }

            (repository as IDisposable)?.Dispose();
        }

        return buckets;
    }

    public static IEnumerable<Type> RepositoryTypes() =>
        typeof(MongoUserRepository).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.Namespace == typeof(MongoUserRepository).Namespace)
            .Where(type => type.Name.StartsWith("Mongo", StringComparison.Ordinal) && type.GetConstructor([typeof(IMongoDatabase)]) is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal);

    // Colecciones que el código usa con nombre literal fuera de una propiedad
    // (por ejemplo `deleted_accounts`, que solo se escribe al eliminar una cuenta).
    public static IReadOnlyCollection<string> SourceCollectionNames(string repositoryRoot) =>
        Directory.EnumerateFiles(Path.Combine(repositoryRoot, "TheHouse.Infrastructure"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(path => CollectionLiteral().Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

    public static string RenderSchema(Schema schema)
    {
        var text = new StringBuilder();
        text.Append(Header(
            "Esquema de la base de datos de TheHouse: colecciones, índices y buckets de GridFS.",
            "01-schema.mongodb.js"));
        text.Append("""
            const collections = [

            """);
        foreach (var name in schema.Collections)
        {
            text.Append("  ").Append(Js(name)).Append(",\n");
        }

        text.Append("];\n\n// [colección, llaves, opciones]: mismos nombres y opciones que los repositorios del código.\nconst indexes = [\n");
        foreach (var index in schema.Indexes)
        {
            text.Append("  [").Append(Js(index.Collection)).Append(", ").Append(Js(index.Keys)).Append(", ").Append(Js(index.Options)).Append("],\n");
        }

        text.Append("];\n\n// Buckets de GridFS (`<bucket>.files` y `<bucket>.chunks`) con los índices de la especificación de GridFS.\nconst buckets = [")
            .Append(string.Join(", ", schema.Buckets.Select(Js))).Append("];\nconst gridFsIndexes = [\n");
        foreach (var (suffix, keys, options) in GridFsIndexes)
        {
            text.Append("  [").Append(Js(suffix)).Append(", ").Append(Js(keys)).Append(", ").Append(Js(options)).Append("],\n");
        }

        text.Append("""
            ];

            function ensureCollection(name) {
              if (target.getCollectionInfos({ name: name }, { nameOnly: true }).length > 0) {
                print(`colección existente: ${name}`);
                return;
              }
              target.createCollection(name);
              print(`colección creada:    ${name}`);
            }

            // Si ya existe un índice con el mismo nombre y otras opciones, MongoDB
            // responde un error y el script se detiene: no borra ni recrea índices.
            function ensureIndex(collection, keys, options) {
              const existed = target.getCollection(collection).getIndexes().some((index) => index.name === options.name);
              try {
                target.getCollection(collection).createIndex(keys, options);
              } catch (error) {
                print(`ERROR en el índice ${collection}.${options.name}: ${error.codeName || error.message}`);
                throw error;
              }
              print(`${existed ? "índice existente:" : "índice creado:   "} ${collection}.${options.name}`);
            }

            for (const name of collections) {
              ensureCollection(name);
            }
            for (const bucket of buckets) {
              for (const [suffix, keys, options] of gridFsIndexes) {
                ensureCollection(`${bucket}.${suffix}`);
                ensureIndex(`${bucket}.${suffix}`, keys, options);
              }
            }
            for (const [collection, keys, options] of indexes) {
              ensureIndex(collection, keys, options);
            }

            print(`Esquema listo en ${dbName}: ${collections.length} colecciones, ${indexes.length} índices y ${buckets.length} buckets de GridFS.`);

            """);
        return text.ToString();
    }

    public static string RenderCatalogs(IReadOnlyList<CatalogWrite> writes)
    {
        var text = new StringBuilder();
        text.Append(Header(
            "Catálogos públicos de TheHouse: universidades, propiedades y lugares de búsqueda.\n// Sin datos personales. Requiere haber ejecutado antes 01-schema.mongodb.js.",
            "02-catalogs.mongodb.js"));
        text.Append("""
            // Los `_id` de universidad los asigna MongoDB: las propiedades se ligan por código.
            function universityId(code) {
              const university = target.universities.findOne({ code: code }, { projection: { _id: 1 } });
              if (university === null) {
                throw new Error(`Falta la universidad ${code}: el catálogo de universidades debe cargarse primero.`);
              }
              return university._id.toHexString();
            }

            const totals = {};
            function upsert(collection, filter, update) {
              const result = target.getCollection(collection).updateOne(filter, update, { upsert: true });
              const entry = totals[collection] || (totals[collection] = { inserted: 0, updated: 0, unchanged: 0 });
              if (result.upsertedCount > 0) {
                entry.inserted += 1;
              } else if (result.modifiedCount > 0) {
                entry.updated += 1;
              } else {
                entry.unchanged += 1;
              }
            }


            """);
        foreach (var group in writes.GroupBy(write => write.Collection))
        {
            text.Append("// ").Append(group.Key).Append(" (").Append(group.Count().ToString(CultureInfo.InvariantCulture)).Append(")\n");
            foreach (var write in group)
            {
                text.Append("upsert(").Append(Js(write.Collection)).Append(",\n  ").Append(Js(write.Filter)).Append(",\n  ")
                    .Append(Js(write.Update, write.UniversityCodesById)).Append(");\n");
            }

            text.Append('\n');
        }

        text.Append("""
            for (const [collection, entry] of Object.entries(totals)) {
              const total = target.getCollection(collection).countDocuments({});
              print(`${collection}: ${entry.inserted} insertados, ${entry.updated} actualizados, ${entry.unchanged} sin cambios (${total} en total)`);
            }
            print(`Catálogos listos en ${dbName}.`);

            """);
        return text.ToString();
    }

    private static string Header(string description, string file) => $$"""
        // NV-26 — {{description}}
        //
        // ARCHIVO GENERADO desde el código por TheHouse.IntegrationTests/DatabaseScriptTests.cs:
        // no se edita a mano (ver scripts/db/README.md para regenerarlo).
        //
        // Uso:   mongosh "<cadena de conexión>" --file scripts/db/{{file}}
        // Base:  variable de entorno THEHOUSE_DB (por defecto thehouse_u2).
        // Idempotente: solo crea o actualiza lo que falta; nunca borra colecciones, índices ni documentos.
        // No contiene cadenas de conexión, usuarios, contraseñas ni datos personales.

        const dbName = (typeof process !== "undefined" && process.env.THEHOUSE_DB) || "thehouse_u2";
        const target = db.getSiblingDB(dbName);


        """;

    // --- Conversión de BSON a JavaScript de mongosh, conservando tipos.

    private static readonly JsonSerializerOptions StringOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Js(string value) => JsonSerializer.Serialize(value, StringOptions);

    private static string Js(BsonValue value, IReadOnlyDictionary<string, string>? universityCodes = null) => value switch
    {
        BsonDocument document when document.ElementCount == 0 => "{}",
        BsonDocument document => "{ " + string.Join(", ", document.Elements.Select(element => Key(element.Name) + ": " + Js(element.Value, universityCodes))) + " }",
        BsonArray array => "[" + string.Join(", ", array.Select(item => Js(item, universityCodes))) + "]",
        BsonString text when universityCodes is not null && universityCodes.TryGetValue(text.Value, out var code) => $"universityId({Js(code)})",
        BsonString text => Js(text.Value),
        BsonInt32 number => number.Value.ToString(CultureInfo.InvariantCulture),
        BsonInt64 number => $"NumberLong(\"{number.Value.ToString(CultureInfo.InvariantCulture)}\")",
        BsonDouble number when double.IsInteger(number.Value) => $"Double({number.Value.ToString("R", CultureInfo.InvariantCulture)})",
        BsonDouble number => number.Value.ToString("R", CultureInfo.InvariantCulture),
        BsonDecimal128 number => $"NumberDecimal(\"{number.Value.ToString()}\")",
        BsonObjectId id => $"ObjectId(\"{id.Value}\")",
        BsonDateTime date => $"ISODate(\"{date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture)}\")",
        BsonBoolean flag => flag.Value ? "true" : "false",
        BsonNull => "null",
        _ => throw new NotSupportedException($"NV-26: tipo BSON sin conversión a mongosh: {value.BsonType}."),
    };

    private static string Key(string name) => JsIdentifier().IsMatch(name) ? name : Js(name);

    // --- Reflexión sobre los repositorios.

    private static IEnumerable<MemberInfo> Members(Type type) =>
        type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => !field.Name.Contains('<', StringComparison.Ordinal))
            .Cast<MemberInfo>()
            .Concat(type.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Where(property => property.GetIndexParameters().Length == 0))
            .OrderBy(member => member.Name, StringComparer.Ordinal);

    private static object? Read(MemberInfo member, object instance) =>
        member is FieldInfo field ? field.GetValue(instance) : ((PropertyInfo)member).GetValue(instance);

    private static string BucketName(MemberInfo member, object repository)
    {
        var bucket = (GridFSBucket)Read(member, repository)!;
        return bucket.Options.BucketName;
    }

    private static bool IsGeneric(Type type, Type definition) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == definition;

    internal static RenderArgs<T> Args<T>() =>
        new(BsonSerializer.LookupSerializer<T>(), BsonSerializer.SerializerRegistry);

    internal static IndexSpec RenderIndex<T>(string collection, CreateIndexModel<T> model)
    {
        var keys = model.Keys.Render(Args<T>());
        var options = model.Options ?? new CreateIndexOptions();
        foreach (var property in options.GetType().GetProperties().Where(property => !SupportedIndexOptions.Contains(property.Name)))
        {
            if (property.GetValue(options) is not null)
            {
                throw new NotSupportedException($"NV-26: la opción de índice {property.Name} ({collection}) no está soportada por el generador del script.");
            }
        }

        var rendered = new BsonDocument("name", options.Name ?? string.Join("_", keys.Elements.Select(element => $"{element.Name}_{element.Value}")));
        if (options.Unique is { } unique)
        {
            rendered.Add("unique", unique);
        }

        if (options.Sparse is { } sparse)
        {
            rendered.Add("sparse", sparse);
        }

        if (options.ExpireAfter is { } expireAfter)
        {
            rendered.Add("expireAfterSeconds", checked((int)expireAfter.TotalSeconds));
        }

        if (options is CreateIndexOptions<T> { PartialFilterExpression: { } partial })
        {
            rendered.Add("partialFilterExpression", partial.Render(Args<T>()));
        }

        return new IndexSpec(collection, keys, rendered);
    }

    [GeneratedRegex("""GetCollection<\w+>\(\s*"([a-z_]+)"\s*\)""")]
    private static partial Regex CollectionLiteral();

    [GeneratedRegex("^[A-Za-z_$][A-Za-z0-9_$]*$")]
    private static partial Regex JsIdentifier();
}

internal sealed record Schema(IReadOnlyList<string> Collections, IReadOnlyList<IndexSpec> Indexes, IReadOnlyList<string> Buckets);

internal sealed record IndexSpec(string Collection, BsonDocument Keys, BsonDocument Options);

internal sealed record CatalogWrite(string Collection, BsonDocument Filter, BsonDocument Update)
{
    public IReadOnlyDictionary<string, string>? UniversityCodesById { get; init; }
}

internal sealed class Recorder
{
    public HashSet<string> Collections { get; } = new(StringComparer.Ordinal);

    public List<IndexSpec> Indexes { get; } = [];

    public HashSet<string> Buckets { get; } = new(StringComparer.Ordinal);

    public List<CatalogWrite> Writes { get; } = [];
}

// Base de MongoDB que solo registra: no abre conexiones.
public class RecordingDatabase : DispatchProxy
{
    internal Recorder Recorder { get; private set; } = null!;

    internal static IMongoDatabase Create(Recorder recorder)
    {
        var database = DispatchProxy.Create<IMongoDatabase, RecordingDatabase>();
        ((RecordingDatabase)(object)database).Recorder = recorder;
        return database;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        switch (method.Name)
        {
            case "GetCollection":
                var name = (string)args![0]!;
                Recorder.Collections.Add(name);
                return typeof(RecordingCollection<>).MakeGenericType(method.GetGenericArguments()[0])
                    .GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [Recorder, (IMongoDatabase)(object)this, name]);
            case "get_DatabaseNamespace":
                return new DatabaseNamespace("thehouse_u2");
            case "get_Settings":
                return new MongoDatabaseSettings();
            case "get_Client":
                return RecordingClient.Instance;
            default:
                throw new NotSupportedException($"NV-26: la base de registro no implementa {method.Name}.");
        }
    }
}

// Cliente mínimo para construir `GridFSBucket` (solo se lee el nombre del bucket).
public class RecordingClient : DispatchProxy
{
    internal static readonly IMongoClient Instance = DispatchProxy.Create<IMongoClient, RecordingClient>();

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod!.Name == "get_Settings" ? new MongoClientSettings() : null;
}

public class RecordingCollection<T> : DispatchProxy
{
    private Recorder recorder = null!;
    private IMongoDatabase database = null!;
    private string name = null!;

    internal static IMongoCollection<T> Create(Recorder recorder, IMongoDatabase database, string name)
    {
        var collection = DispatchProxy.Create<IMongoCollection<T>, RecordingCollection<T>>();
        var proxy = (RecordingCollection<T>)(object)collection;
        proxy.recorder = recorder;
        proxy.database = database;
        proxy.name = name;
        return collection;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        switch (method.Name)
        {
            case "get_Indexes":
                return RecordingIndexes<T>.Create(recorder, name);
            case "get_CollectionNamespace":
                return new CollectionNamespace(database.DatabaseNamespace, name);
            case "get_DocumentSerializer":
                return BsonSerializer.LookupSerializer<T>();
            case "get_Settings":
                return new MongoCollectionSettings();
            case "get_Database":
                return database;
            case "UpdateOneAsync":
                var filter = args!.OfType<FilterDefinition<T>>().Single();
                var update = args!.OfType<UpdateDefinition<T>>().Single();
                if (args!.OfType<UpdateOptions>().SingleOrDefault() is not { IsUpsert: true })
                {
                    throw new NotSupportedException("NV-26: los seeds de catálogo deben escribir con upsert.");
                }

                recorder.Writes.Add(new CatalogWrite(
                    name,
                    filter.Render(DatabaseScriptGenerator.Args<T>()),
                    update.Render(DatabaseScriptGenerator.Args<T>()).AsBsonDocument));
                return Task.FromResult<UpdateResult>(new UpdateResult.Acknowledged(0, 0, BsonNull.Value));
            default:
                throw new NotSupportedException($"NV-26: la colección de registro no implementa {method.Name}.");
        }
    }
}

public class RecordingIndexes<T> : DispatchProxy
{
    private Recorder recorder = null!;
    private string name = null!;

    internal static IMongoIndexManager<T> Create(Recorder recorder, string name)
    {
        var indexes = DispatchProxy.Create<IMongoIndexManager<T>, RecordingIndexes<T>>();
        var proxy = (RecordingIndexes<T>)(object)indexes;
        proxy.recorder = recorder;
        proxy.name = name;
        return indexes;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        switch (method.Name)
        {
            case "CreateOneAsync":
                return Task.FromResult(Record(args!.OfType<CreateIndexModel<T>>().Single()));
            case "CreateManyAsync":
                var names = args!.OfType<IEnumerable<CreateIndexModel<T>>>().Single().Select(Record).ToArray();
                return Task.FromResult<IEnumerable<string>>(names);
            case "ListAsync":
                return Task.FromResult<IAsyncCursor<BsonDocument>>(new EmptyCursor());
            default:
                throw new NotSupportedException($"NV-26: el administrador de índices de registro no implementa {method.Name}.");
        }
    }

    private string Record(CreateIndexModel<T> model)
    {
        var index = DatabaseScriptGenerator.RenderIndex(name, model);
        recorder.Indexes.Add(index);
        return index.Options["name"].AsString;
    }

    private sealed class EmptyCursor : IAsyncCursor<BsonDocument>
    {
        public IEnumerable<BsonDocument> Current => [];

        public bool MoveNext(CancellationToken cancellationToken = default) => false;

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public void Dispose()
        {
        }
    }
}
