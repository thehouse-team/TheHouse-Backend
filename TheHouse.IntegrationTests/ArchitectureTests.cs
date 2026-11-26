using System.Reflection;
using MongoDB.Driver;
using TheHouse.Application.UseCases;

namespace TheHouse.IntegrationTests;

// NV-01 1.3.0-u2 (T13, NV-01-R13): fronteras del monolito modular. Reglas de
// la constitución §6 y §7 comprobadas por reflexión y con la base de registro
// de NV-26, sin paquetes nuevos. El mapa de módulos está también en
// `docs/arquitectura/modulos.md`; una prueba exige que coincidan.
public sealed class ArchitectureTests
{
    private const string UseCasesNamespace = "TheHouse.Application.UseCases";

    private static readonly Assembly Domain = typeof(TheHouse.Domain.Entities.Room).Assembly;
    private static readonly Assembly Application = typeof(SmartMatchService).Assembly;
    private static readonly Assembly Infrastructure = typeof(TheHouse.Infrastructure.Persistence.Mongo.MongoUserRepository).Assembly;
    private static readonly Assembly Host = typeof(Program).Assembly;

    private static readonly Module[] Modules =
    [
        new("Identity", ["MongoUserRepository", "MongoRefreshTokenStore"], ["users", "refresh_tokens", "deleted_accounts"],
            ["RegistrationService", "LoginService", "LogoutService", "SessionService", "AccountService", "AccountDeletionService"]),
        new("Profile", ["MongoProfileRepository", "MongoPreferencesRepository"], ["user_profiles", "user_preferences"],
            ["ProfileService", "PreferencesService", "ProfileDeletionParticipant"]),
        new("Catalog", ["MongoUniversityRepository", "MongoPropertyRepository"], ["universities", "properties"],
            ["UniversityQueryService", "UniversitySearchService", "UniversitySeedService", "PropertyQueryService", "PropertySeedService"]),
        new("Listings", ["MongoLandlordProfileRepository", "MongoBuildingRepository", "MongoRoomRepository"], ["landlord_profiles", "buildings", "rooms"],
            ["LandlordService", "BuildingService", "RoomService", "ListingsDeletionParticipant"]),
        new("Media", ["MongoMediaRepository"], ["media_items", "verification_documents"],
            ["MediaService", "MediaPort"]),
        new("Search", ["MongoPlaceRepository"], ["places"],
            ["SearchService", "RoomCatalog", "PlaceSeedService", "DemoRoomSeedService"]),
        new("Favorites", ["MongoFavoriteRepository"], ["favorites"],
            ["FavoriteService", "RoomMigrationService", "FavoritesDeletionParticipant"]),
        new("Messaging", ["MongoConversationRepository", "MongoRentalRequestRepository"], ["conversations", "messages", "rental_requests"],
            ["ConversationService", "RentalRequestService", "MessagingDeletionParticipant"]),
        new("Visits", ["MongoVisitRepository"], ["visits", "visit_idempotency"],
            ["VisitService", "LandlordVisitService", "VisitsDeletionParticipant"]),
        new("Rentals", ["MongoAgreementRepository"], ["agreements"],
            ["AgreementService", "RentalHistoryQuery", "RentalsDeletionParticipant"]),
        new("Reviews", ["MongoReviewRepository"], ["reviews"],
            ["RoomReviewService", "ReviewsDeletionParticipant"]),
        new("Notifications", ["MongoNotificationRepository", "MongoPushDeviceRepository"], ["notifications", "push_devices"],
            ["NotificationService", "NotificationPublisher", "PushDeviceService", "PushDispatcher", "NotificationsDeletionParticipant"]),
        new("Moderation",
            ["MongoStaffRoleRepository", "MongoAuditRepository", "MongoVerificationReviewRepository", "MongoReportRepository", "MongoUserSuspensionRepository", "MongoModerationRejectionRepository"],
            ["staff_roles", "audit_events", "verification_reviews", "reports", "user_suspensions", "moderation_rejections"],
            ["StaffService", "ModerationService", "ReportService", "ReviewService", "ReviewModerationService", "ContentModerationService", "ModerationRejectionService", "AccountSuspensionQuery", "ModerationDeletionParticipant"]),
        new("Roommates", ["MongoRoommateProfileRepository"], ["roommate_profiles"],
            ["RoommateService", "RoommatesDeletionParticipant"]),
        new("SmartMatch", ["MongoSmartMatchRepository", "MongoSmartMatchExplanationCache"],
            ["smart_match_feedback", "smart_match_dismissals", "smart_match_daily_stats", "smart_match_explanations"],
            ["SmartMatchService", "SmartMatchExplanationService", "SmartMatchDeletionParticipant"]),
        new("Analytics", ["MongoRoomViewRepository"], ["room_views"],
            ["RoomViewService", "LandlordStatsService", "AnalyticsDeletionParticipant"]),
    ];

    private static readonly string[] ForbiddenInCore =
        ["MongoDB.", "StackExchange.Redis", "Microsoft.ML.OnnxRuntime", "Azure.", "SkiaSharp", "FirebaseAdmin", "Google.Apis", "Microsoft.AspNetCore."];

    [Fact]
    public void R1_project_references_follow_the_dependency_rule()
    {
        Assert.Empty(InternalReferences(Domain));
        Assert.Equal(["TheHouse.Domain"], InternalReferences(Application));
        Assert.Equal(["TheHouse.Application", "TheHouse.Domain"], InternalReferences(Infrastructure));
        Assert.Equal(["TheHouse.Application", "TheHouse.Domain", "TheHouse.Infrastructure"], InternalReferences(Host));
    }

    [Fact]
    public void R2_domain_and_application_do_not_reference_infrastructure_packages()
    {
        foreach (var assembly in new[] { Domain, Application })
        {
            var forbidden = assembly.GetReferencedAssemblies()
                .Select(reference => reference.Name!)
                .Where(name => ForbiddenInCore.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal) || name == prefix.TrimEnd('.')))
                .ToArray();
            Assert.True(forbidden.Length == 0, $"{assembly.GetName().Name} referencia {string.Join(", ", forbidden)}.");
        }
    }

    [Fact]
    public void R3_each_collection_has_exactly_one_owner_module()
    {
        var owners = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var type in DatabaseScriptGenerator.RepositoryTypes())
        {
            var module = Modules.SingleOrDefault(candidate => candidate.Repositories.Contains(type.Name));
            Assert.True(module is not null, $"R6: el repositorio {type.Name} no tiene módulo en el mapa.");
            foreach (var collection in CollectionsOf(type))
            {
                Assert.True(module!.Collections.Contains(collection), $"{type.Name} ({module.Name}) usa `{collection}`, que no es de su módulo.");
                if (owners.TryGetValue(collection, out var other))
                {
                    Assert.True(other == module.Name, $"`{collection}` la usan los módulos {other} y {module.Name}.");
                }

                owners[collection] = module.Name;
            }
        }

        // Todas las colecciones del esquema de NV-26 (incluidas las que se
        // usan con nombre literal, como `deleted_accounts`) tienen un dueño.
        var schema = DatabaseScriptGenerator.RecordSchema(RepositoryRoot());
        var mapped = Modules.SelectMany(module => module.Collections).ToArray();
        Assert.Equal(mapped.Length, mapped.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(schema.Collections.Order(StringComparer.Ordinal), mapped.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void R4_service_dependencies_between_modules_have_no_cycles()
    {
        var moduleOf = ComponentModules();
        var edges = new HashSet<(string From, string To)>();
        foreach (var component in Components())
        {
            foreach (var parameter in Constructor(component).GetParameters())
            {
                if (moduleOf.TryGetValue(parameter.ParameterType.Name, out var target) && target != moduleOf[component.Name])
                {
                    edges.Add((moduleOf[component.Name], target));
                }
            }
        }

        Assert.Equal(ExpectedEdges.Order(), edges.Order());
        var cycle = FindCycle(edges);
        Assert.True(cycle is null, $"Ciclo entre módulos: {string.Join(" → ", cycle ?? [])}.");
    }

    [Fact]
    public void R5_http_types_do_not_use_infrastructure_types()
    {
        var offenders = Host.GetTypes()
            .Where(type => type.Namespace is "TheHouse.Http" || type.Namespace?.StartsWith("TheHouse.Http.", StringComparison.Ordinal) == true)
            .SelectMany(type => UsedTypes(type).Where(used => used.Assembly == Infrastructure).Select(used => $"{type.Name} → {used.Name}"))
            .Distinct()
            .ToArray();
        Assert.True(offenders.Length == 0, string.Join("; ", offenders));
    }

    [Fact]
    public void R6_every_use_case_component_belongs_to_one_module()
    {
        var moduleOf = ComponentModules();
        var unmapped = Components().Where(component => !moduleOf.ContainsKey(component.Name)).Select(component => component.Name).ToArray();
        Assert.True(unmapped.Length == 0, $"Sin módulo en el mapa: {string.Join(", ", unmapped)}.");
        var missing = Modules.SelectMany(module => module.Components).Where(name => Components().All(component => component.Name != name)).ToArray();
        Assert.True(missing.Length == 0, $"En el mapa pero no existen: {string.Join(", ", missing)}.");
    }

    [Fact]
    public void Module_map_document_matches_the_test()
    {
        // Solo la tabla de la sección «## Módulos».
        var rows = File.ReadAllLines(Path.Combine(RepositoryRoot(), "docs", "arquitectura", "modulos.md"))
            .SkipWhile(line => line != "## Módulos")
            .Skip(1)
            .TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal))
            .Where(line => line.StartsWith("| ", StringComparison.Ordinal))
            .ToArray();
        foreach (var module in Modules)
        {
            var row = rows.SingleOrDefault(line => line.StartsWith($"| {module.Name} |", StringComparison.Ordinal));
            Assert.True(row is not null, $"Falta el módulo {module.Name} en docs/arquitectura/modulos.md.");
            foreach (var name in module.Collections.Concat(module.Components))
            {
                Assert.True(row!.Contains($"`{name}`", StringComparison.Ordinal), $"docs/arquitectura/modulos.md: {module.Name} no menciona `{name}`.");
            }
        }
    }

    // Dependencias de servicio a servicio permitidas entre módulos (documentadas).
    private static readonly (string From, string To)[] ExpectedEdges =
    [
        ("Favorites", "Search"),
        ("Media", "Moderation"),
        ("Moderation", "Identity"),
        ("Moderation", "Listings"),
        ("Roommates", "Messaging"),
        ("SmartMatch", "Moderation"),
        ("SmartMatch", "Search"),
        ("Visits", "Search"),
    ];

    private static string[] InternalReferences(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith("TheHouse", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

    // Componentes: clases de UseCases que reciben puertos o servicios del backend.
    private static Type[] Components() =>
        [.. Application.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true } && type.Namespace == UseCasesNamespace)
            .Where(type => !typeof(Exception).IsAssignableFrom(type))
            .Where(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType.Namespace?.StartsWith("TheHouse", StringComparison.Ordinal) == true &&
                                  (parameter.ParameterType.IsInterface || parameter.ParameterType.Namespace == UseCasesNamespace)))];

    private static Dictionary<string, string> ComponentModules() =>
        Modules.SelectMany(module => module.Components.Select(component => (component, module.Name)))
            .ToDictionary(pair => pair.component, pair => pair.Name, StringComparer.Ordinal);

    private static ConstructorInfo Constructor(Type type) =>
        type.GetConstructors().OrderByDescending(constructor => constructor.GetParameters().Length).First();

    private static IReadOnlyCollection<string> CollectionsOf(Type repositoryType)
    {
        var recorder = new Recorder();
        var repository = Activator.CreateInstance(repositoryType, RecordingDatabase.Create(recorder))!;
        foreach (var member in repositoryType.GetMembers(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            var memberType = member switch
            {
                FieldInfo field => field.FieldType,
                PropertyInfo property when property.GetIndexParameters().Length == 0 => property.PropertyType,
                _ => null,
            };
            if (memberType is { IsGenericType: true } && memberType.GetGenericTypeDefinition() == typeof(IMongoCollection<>))
            {
                _ = member is FieldInfo field ? field.GetValue(repository) : ((PropertyInfo)member).GetValue(repository);
            }
        }

        (repository as IDisposable)?.Dispose();
        return recorder.Collections;
    }

    private static IEnumerable<Type> UsedTypes(Type type)
    {
        const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        var used = new List<Type>();
        used.AddRange(type.GetFields(all).Select(field => field.FieldType));
        used.AddRange(type.GetProperties(all).Select(property => property.PropertyType));
        foreach (var method in type.GetMethods(all).Cast<MethodBase>().Concat(type.GetConstructors(all)))
        {
            used.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
            if (method is MethodInfo info)
            {
                used.Add(info.ReturnType);
            }
        }

        return used.SelectMany(Expand);
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        if (type.HasElementType)
        {
            return Expand(type.GetElementType()!);
        }

        return type.IsGenericType ? [type, .. type.GetGenericArguments().SelectMany(Expand)] : [type];
    }

    private static string[]? FindCycle(IReadOnlyCollection<(string From, string To)> edges)
    {
        var graph = edges.GroupBy(edge => edge.From).ToDictionary(group => group.Key, group => group.Select(edge => edge.To).ToArray());
        var state = new Dictionary<string, int>();
        var path = new Stack<string>();

        string[]? Visit(string node)
        {
            state[node] = 1;
            path.Push(node);
            foreach (var next in graph.GetValueOrDefault(node, []))
            {
                if (state.GetValueOrDefault(next) == 1)
                {
                    return [.. path.Reverse().SkipWhile(item => item != next), next];
                }

                if (state.GetValueOrDefault(next) == 0 && Visit(next) is { } cycle)
                {
                    return cycle;
                }
            }

            path.Pop();
            state[node] = 2;
            return null;
        }

        foreach (var node in graph.Keys)
        {
            if (state.GetValueOrDefault(node) == 0 && Visit(node) is { } cycle)
            {
                return cycle;
            }
        }

        return null;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TheHouse.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No se encontró TheHouse.slnx.");
    }

    private sealed record Module(string Name, string[] Repositories, string[] Collections, string[] Components);
}
