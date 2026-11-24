using System.Runtime.InteropServices;
using SharpSelecta.Audio;

namespace SharpSelecta.Tests;

public class AudioThreadTests
{
    [Test]
    public async Task Work_RunsOnOneThreadThatIsntTheCallers()
    {
        var first = await AudioThread.InvokeAsync(() => Environment.CurrentManagedThreadId);
        var second = AudioThread.Invoke(() => Environment.CurrentManagedThreadId);

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first).IsNotEqualTo(Environment.CurrentManagedThreadId);
    }

    [Test]
    public async Task OnWindows_TheThreadIsInTheMultithreadedApartment()
    {
        Skip.When(!OperatingSystem.IsWindows(), "COM apartments are Windows only");

        var apartment = AudioThread.Invoke(() => Thread.CurrentThread.GetApartmentState());

        await Assert.That(apartment).IsEqualTo(ApartmentState.MTA);
    }

    [Test]
    public async Task AnException_ReachesTheCaller()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => AudioThread.Invoke(() => throw new InvalidOperationException("boom")));

        await Assert.That(thrown.Message).IsEqualTo("boom");
        await Assert.That(AudioThread.Invoke(() => 42)).IsEqualTo(42);
    }

    [Test]
    public async Task InvokingFromTheAudioThread_RunsInline()
    {
        var nested = await AudioThread.InvokeAsync(() => AudioThread.Invoke(() => AudioThread.IsCurrent));

        await Assert.That(nested).IsTrue();
    }

    [Test]
    public async Task NoAssemblyOfOursUsesBuiltInComInterop()
    {
        var assemblies = new[] { typeof(AudioThread).Assembly, typeof(SharpSelecta.App.App).Assembly, typeof(SharpSelecta.Core.Library.Track).Assembly };

        var comImports = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => Attribute.IsDefined(t, typeof(ComImportAttribute)))
            .Select(t => t.FullName)
            .ToList();

        await Assert.That(comImports).IsEmpty();
    }
}
