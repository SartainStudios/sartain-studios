using System.Net;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace SartainStudios.Api.Service.Test.Infrastructure;

internal static class MongoErrors
{
    public static MongoWriteException DuplicateKey(string message = "E11000 duplicate key error")
    {
        return Write(ServerErrorCategory.DuplicateKey, 11000, message);
    }

    public static MongoWriteException Uncategorized(string message = "write failed")
    {
        return Write(ServerErrorCategory.Uncategorized, 1, message);
    }

    private static MongoWriteException Write(ServerErrorCategory category, int code, string message)
    {
        var connectionId = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));

        return new MongoWriteException(connectionId, BuildWriteError(category, code, message), null, null);
    }

    private static WriteError BuildWriteError(ServerErrorCategory category, int code, string message)
    {
        var constructor = typeof(WriteError)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .First();

        var arguments = constructor
            .GetParameters()
            .Select(parameter => Argument(parameter, category, code, message))
            .ToArray();

        return (WriteError)constructor.Invoke(arguments);
    }

    private static object? Argument(ParameterInfo parameter, ServerErrorCategory category, int code, string message)
    {
        if (parameter.ParameterType == typeof(ServerErrorCategory)) return category;

        if (parameter.ParameterType == typeof(int)) return code;

        if (parameter.ParameterType == typeof(string)) return message;

        if (parameter.ParameterType == typeof(BsonDocument)) return null;

        return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
    }
}