﻿using LangChain.Serve.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LangChain.Databases.Sqlite;

public static class ServeExtensions
{
    public static IServiceCollection AddSQLiteConversationRepository(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IConversationRepository>(sp => new SqLiteConversationRepository(connectionString));
        return services;
    }
}