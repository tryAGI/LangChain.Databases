using Microsoft.Extensions.VectorData;

namespace LangChain.Databases.IntegrationTests;

[TestFixture]
public partial class DatabaseTests
{
    [TestCase(SupportedDatabase.InMemory)]
    [TestCase(SupportedDatabase.Postgres)]
    [TestCase(SupportedDatabase.OpenSearch)]
    [Obsolete]
    public async Task CreateAndDeleteCollection_Ok(SupportedDatabase database)
    {
        await using var environment = await StartEnvironmentForAsync(database);
        var store = environment.VectorStore;

        var exists = await store.CollectionExistsAsync(environment.CollectionName);
        exists.Should().BeFalse();

        var collection = store.GetCollection<string, TestVectorRecord>(environment.CollectionName);
        await collection.EnsureCollectionExistsAsync();

        exists = await store.CollectionExistsAsync(environment.CollectionName);
        exists.Should().BeTrue();

        var names = new List<string>();
        await foreach (var name in store.ListCollectionNamesAsync())
        {
            names.Add(name);
        }
        names.Should().Contain(environment.CollectionName);

        await store.EnsureCollectionDeletedAsync(environment.CollectionName);

        exists = await store.CollectionExistsAsync(environment.CollectionName);
        exists.Should().BeFalse();
    }

    [TestCase(SupportedDatabase.InMemory)]
    [TestCase(SupportedDatabase.Postgres)]
    [TestCase(SupportedDatabase.OpenSearch)]
    [Obsolete]
    public async Task UpsertAndGet_Ok(SupportedDatabase database)
    {
        await using var environment = await StartEnvironmentForAsync(database);
        var collection = environment.VectorStore.GetCollection<string, TestVectorRecord>(environment.CollectionName);
        await collection.EnsureCollectionExistsAsync();

        var record1 = new TestVectorRecord
        {
            Text = "apple",
            Color = "red",
            Embedding = Embeddings["apple"],
        };
        var record2 = new TestVectorRecord
        {
            Text = "orange",
            Color = "orange",
            Embedding = Embeddings["orange"],
        };

        await collection.UpsertAsync(record1);
        await collection.UpsertAsync(record2);

        var retrieved1 = await collection.GetAsync(record1.Id);
        retrieved1.Should().NotBeNull();
        retrieved1!.Text.Should().Be("apple");

        var retrieved2 = await collection.GetAsync(record2.Id);
        retrieved2.Should().NotBeNull();
        retrieved2!.Text.Should().Be("orange");
    }

    [TestCase(SupportedDatabase.InMemory)]
    [TestCase(SupportedDatabase.Postgres)]
    [TestCase(SupportedDatabase.OpenSearch)]
    [Obsolete]
    public async Task BatchUpsertAndGet_Ok(SupportedDatabase database)
    {
        await using var environment = await StartEnvironmentForAsync(database);
        var collection = environment.VectorStore.GetCollection<string, TestVectorRecord>(environment.CollectionName);
        await collection.EnsureCollectionExistsAsync();

        var records = new[]
        {
            new TestVectorRecord { Text = "apple", Color = "red", Embedding = Embeddings["apple"] },
            new TestVectorRecord { Text = "orange", Color = "orange", Embedding = Embeddings["orange"] },
            new TestVectorRecord { Text = "banana", Color = "yellow", Embedding = Embeddings["banana"] },
        };

        await collection.UpsertAsync(records);

        foreach (var record in records)
        {
            var retrieved = await collection.GetAsync(record.Id);
            retrieved.Should().NotBeNull();
            retrieved!.Text.Should().Be(record.Text);
        }
    }

    [TestCase(SupportedDatabase.InMemory)]
    [TestCase(SupportedDatabase.Postgres)]
    [TestCase(SupportedDatabase.OpenSearch)]
    [Obsolete]
    public async Task DeleteRecord_Ok(SupportedDatabase database)
    {
        await using var environment = await StartEnvironmentForAsync(database);
        var collection = environment.VectorStore.GetCollection<string, TestVectorRecord>(environment.CollectionName);
        await collection.EnsureCollectionExistsAsync();

        var record = new TestVectorRecord
        {
            Text = "apple",
            Color = "red",
            Embedding = Embeddings["apple"],
        };

        await collection.UpsertAsync(record);

        var retrieved = await collection.GetAsync(record.Id);
        retrieved.Should().NotBeNull();

        await collection.DeleteAsync(record.Id);

        var deleted = await collection.GetAsync(record.Id);
        deleted.Should().BeNull();
    }

    [TestCase(SupportedDatabase.InMemory)]
    [TestCase(SupportedDatabase.Postgres)]
    [TestCase(SupportedDatabase.OpenSearch)]
    [Obsolete]
    public async Task BatchDelete_Ok(SupportedDatabase database)
    {
        await using var environment = await StartEnvironmentForAsync(database);
        var collection = environment.VectorStore.GetCollection<string, TestVectorRecord>(environment.CollectionName);
        await collection.EnsureCollectionExistsAsync();

        var record1 = new TestVectorRecord
        {
            Text = "apple",
            Embedding = Embeddings["apple"],
        };
        var record2 = new TestVectorRecord
        {
            Text = "orange",
            Embedding = Embeddings["orange"],
        };
        var record3 = new TestVectorRecord
        {
            Text = "banana",
            Embedding = Embeddings["banana"],
        };

        await collection.UpsertAsync(record1);
        await collection.UpsertAsync(record2);
        await collection.UpsertAsync(record3);

        // Batch delete two of the three records
        await collection.DeleteAsync([record1.Id, record2.Id]);

        var deleted1 = await collection.GetAsync(record1.Id);
        deleted1.Should().BeNull();

        var deleted2 = await collection.GetAsync(record2.Id);
        deleted2.Should().BeNull();

        // Third record should still exist
        var remaining = await collection.GetAsync(record3.Id);
        remaining.Should().NotBeNull();
        remaining!.Text.Should().Be("banana");
    }

    [TestCase(SupportedDatabase.InMemory)]
    [TestCase(SupportedDatabase.Postgres)]
    [TestCase(SupportedDatabase.OpenSearch)]
    [Obsolete]
    public async Task GetServiceMetadata_Ok(SupportedDatabase database)
    {
        await using var environment = await StartEnvironmentForAsync(database);
        var store = environment.VectorStore;
        var collection = store.GetCollection<string, TestVectorRecord>(environment.CollectionName);

        // VectorStore should return VectorStoreMetadata
        var storeMetadata = store.GetService(typeof(VectorStoreMetadata)) as VectorStoreMetadata;
        storeMetadata.Should().NotBeNull();
        storeMetadata!.VectorStoreSystemName.Should().NotBeNullOrEmpty();

        // VectorStoreCollection should return VectorStoreCollectionMetadata
        var collectionMetadata = collection.GetService(typeof(VectorStoreCollectionMetadata)) as VectorStoreCollectionMetadata;
        collectionMetadata.Should().NotBeNull();
        collectionMetadata!.VectorStoreSystemName.Should().Be(storeMetadata.VectorStoreSystemName);
        collectionMetadata.CollectionName.Should().Be(environment.CollectionName);
    }

    [TestCase(SupportedDatabase.InMemory)]
    [TestCase(SupportedDatabase.Postgres)]
    [TestCase(SupportedDatabase.OpenSearch)]
    [Obsolete]
    public async Task SimilaritySearch_Ok(SupportedDatabase database)
    {
        await using var environment = await StartEnvironmentForAsync(database);
        var collection = environment.VectorStore.GetCollection<string, TestVectorRecord>(environment.CollectionName);
        await collection.EnsureCollectionExistsAsync();

        // Add all embeddings
        foreach (var kvp in Embeddings)
        {
            await collection.UpsertAsync(new TestVectorRecord
            {
                Text = kvp.Key,
                Embedding = kvp.Value,
            });
        }

        // Search for items similar to "lemon"
        var results = new List<VectorSearchResult<TestVectorRecord>>();
        await foreach (var result in collection.SearchAsync(
            new ReadOnlyMemory<float>(Embeddings["lemon"]),
            top: 5))
        {
            results.Add(result);
        }

        results.Should().HaveCount(5);

        var similarTexts = results.Select(r => r.Record.Text).ToArray();
        similarTexts[0].Should().Be("lemon");
        similarTexts.Should().Contain("orange");
        similarTexts.Should().Contain("peach");
        similarTexts.Should().Contain("banana");
        similarTexts.Should().Contain("apple");
    }
}
