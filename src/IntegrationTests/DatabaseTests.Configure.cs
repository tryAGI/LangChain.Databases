using DotNet.Testcontainers.Builders;
using LangChain.Databases.OpenSearch;
using LangChain.Databases.Postgres;
using CommunityToolkit.VectorData.InMemory;
using Testcontainers.PostgreSql;

namespace LangChain.Databases.IntegrationTests;

public partial class DatabaseTests
{
    [Obsolete]
    internal static async Task<DatabaseTestEnvironment> StartEnvironmentForAsync(SupportedDatabase database, CancellationToken cancellationToken = default)
    {
        switch (database)
        {
            case SupportedDatabase.InMemory:
                {
                    return new DatabaseTestEnvironment
                    {
                        VectorStore = new InMemoryVectorStore(),
                    };
                }
            case SupportedDatabase.Postgres:
                {
                    var port = Random.Shared.Next(49152, 65535);
                    var container = new PostgreSqlBuilder()
                        .WithImage("pgvector/pgvector:pg16")
                        .WithPassword("password")
                        .WithDatabase("test")
                        .WithUsername("postgres")
                        .WithPortBinding(hostPort: port, containerPort: 5432)
                        .Build();

                    await container.StartAsync(cancellationToken);

                    return new DatabaseTestEnvironment
                    {
                        VectorStore = new PostgresVectorStore(container.GetConnectionString()),
                        Container = container,
                    };
                }
            case SupportedDatabase.OpenSearch:
                {
                    const string password = "StronG#1235";

                    var container = new ContainerBuilder()
                        .WithImage("opensearchproject/opensearch:2.19.4")
                        .WithPortBinding(9200, assignRandomHostPort: true)
                        .WithEnvironment("discovery.type", "single-node")
                        .WithEnvironment("plugins.security.disabled", "true")
                        .WithEnvironment("OPENSEARCH_INITIAL_ADMIN_PASSWORD", password)
                        .WithWaitStrategy(Wait.ForUnixContainer()
                            .UntilHttpRequestIsSucceeded(r => r
                                .ForPort(9200)
                                .ForPath("/")
                                .ForResponseMessageMatching(response =>
                                    Task.FromResult(response.IsSuccessStatusCode))))
                        .Build();

                    await container.StartAsync(cancellationToken);

                    var port = container.GetMappedPublicPort(9200);

                    return new DatabaseTestEnvironment
                    {
                        VectorStore = new OpenSearchVectorStore(new OpenSearchVectorDatabaseOptions
                        {
                            ConnectionUri = new Uri($"http://localhost:{port}"),
                            Username = "admin",
                            Password = password,
                        }),
                        Container = container,
                        Port = port,
                    };
                }
            default:
                throw new ArgumentOutOfRangeException(nameof(database), database, null);
        }
    }
}
