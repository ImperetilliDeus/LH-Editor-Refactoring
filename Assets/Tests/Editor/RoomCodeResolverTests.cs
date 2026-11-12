using System;
using System.Reflection;
using NUnit.Framework;
using static TestReflection;

public class RoomCodeResolverTests
{
    [Test]
    public void Resolve_PrefersExplicitRoomCode()
    {
        Assert.That(Resolve("1234", "UnregisteredRoomType"), Is.EqualTo("1234"));
    }

    [Test]
    public void Resolve_ReturnsEmptyWhenCodeIsBlankAndTypeIsUnknown()
    {
        Assert.That(Resolve("   ", "UnregisteredRoomType"), Is.EqualTo(string.Empty));
        Assert.That(Resolve(null, null), Is.EqualTo(string.Empty));
    }

    private static string Resolve(string roomCode, string roomTypeKey)
    {
        MethodInfo method = GetAssemblyType("RoomCodeResolver").GetMethod(
            "Resolve",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string), typeof(string) },
            null);
        Assert.That(method, Is.Not.Null);
        return (string)method.Invoke(null, new object[] { roomCode, roomTypeKey });
    }
}
