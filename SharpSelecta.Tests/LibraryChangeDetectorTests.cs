using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class LibraryChangeDetectorTests
{
    private readonly ManualTimeProvider _time = new();
    private readonly FakeLibraryChangeSource _source = new();
    private readonly List<Exception> _errors = [];
    private readonly Reconciler _reconciler = new();

    private static TaskCompletionSource CompletedRun()
    {
        var run = new TaskCompletionSource();
        run.SetResult();
        return run;
    }

    private LibraryChangeDetector CreateDetector()
    {
        var detector = new LibraryChangeDetector(_source, _time, _reconciler.Run, _errors.Add);
        detector.Watch(["/music"]);
        return detector;
    }

    private void FileChanged(string path) => _source.Raise(new LibraryChange(path, IsDirectory: false));

    private void Wait(double seconds) => _time.Advance(TimeSpan.FromSeconds(seconds));

    [Test]
    public async Task ABurstOfEvents_FiresOnce_AfterTwoQuietSeconds()
    {
        using var detector = CreateDetector();

        FileChanged("/music/a.mp3");
        Wait(1);
        FileChanged("/music/b.flac");
        Wait(1.5);
        await Assert.That(_reconciler.Count).IsEqualTo(0);

        Wait(0.5);
        await Assert.That(_reconciler.Count).IsEqualTo(1);

        Wait(10);
        await Assert.That(_reconciler.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AnEndlessBurst_FiresTenSecondsAfterItsFirstEvent()
    {
        using var detector = CreateDetector();

        for (var i = 0; i < 9; i++)
        {
            FileChanged($"/music/{i}.mp3");
            Wait(1);
        }

        FileChanged("/music/last.mp3");
        await Assert.That(_reconciler.Count).IsEqualTo(0);

        Wait(1);
        await Assert.That(_reconciler.Count).IsEqualTo(1);
    }

    [Test]
    [Arguments("/music/cover.jpg")]
    [Arguments("/music/a.mp3.part")]
    [Arguments("/music/.goutputstream-X1")]
    public async Task FilesThatAreNotAudio_AreIgnored(string path)
    {
        using var detector = CreateDetector();

        FileChanged(path);
        Wait(20);

        await Assert.That(_reconciler.Count).IsEqualTo(0);
    }

    [Test]
    public async Task AudioExtensions_CountWhateverTheirCase()
    {
        using var detector = CreateDetector();

        FileChanged("/music/A.FLAC");
        Wait(2);

        await Assert.That(_reconciler.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DirectoryEvents_Count_EvenWithADotInTheName()
    {
        using var detector = CreateDetector();

        _source.Raise(new LibraryChange("/music/Crane Bunch (Vol. 3)", IsDirectory: true));
        Wait(2);

        await Assert.That(_reconciler.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ARenameCounts_WhenEitherNameIsAudio()
    {
        using var detector = CreateDetector();

        _source.Raise(new LibraryChange("/music/a.mp3", IsDirectory: false, OldPath: "/music/a.mp3.part"));
        Wait(2);
        _source.Raise(new LibraryChange("/music/a.bak", IsDirectory: false, OldPath: "/music/a.mp3"));
        Wait(2);

        await Assert.That(_reconciler.Count).IsEqualTo(2);
    }

    [Test]
    public async Task EventsDuringAReconcile_FireOnceMoreAfterItFinishes_NeverInParallel()
    {
        using var detector = CreateDetector();
        _reconciler.Next = new TaskCompletionSource();

        FileChanged("/music/a.mp3");
        Wait(2);
        await Assert.That(_reconciler.Count).IsEqualTo(1);

        FileChanged("/music/b.mp3");
        Wait(2);
        FileChanged("/music/c.mp3");
        Wait(2);
        await Assert.That(_reconciler.Count).IsEqualTo(1);

        var first = _reconciler.Next;
        _reconciler.Next = CompletedRun();
        first.SetResult();

        await Assert.That(_reconciler.Count).IsEqualTo(2);
    }

    [Test]
    public async Task AReconcileWithoutEventsDuringIt_DoesNotRunAgain()
    {
        using var detector = CreateDetector();
        _reconciler.Next = new TaskCompletionSource();

        FileChanged("/music/a.mp3");
        Wait(2);
        _reconciler.Next.SetResult();
        Wait(20);

        await Assert.That(_reconciler.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AnEventDuringAReconcile_ThatIsStillDebouncingWhenItFinishes_WaitsForItsQuietPeriod()
    {
        using var detector = CreateDetector();
        _reconciler.Next = new TaskCompletionSource();

        FileChanged("/music/a.mp3");
        Wait(2);
        FileChanged("/music/b.mp3");
        _reconciler.Next.SetResult();
        await Assert.That(_reconciler.Count).IsEqualTo(1);

        Wait(2);
        await Assert.That(_reconciler.Count).IsEqualTo(2);
    }

    [Test]
    public async Task AFailure_RecreatesThatRootsWatcher_AndReconcilesRightAway()
    {
        using var detector = CreateDetector();
        FileChanged("/music/a.mp3");

        _source.Fail("/music");

        await Assert.That(_reconciler.Count).IsEqualTo(1);
        await Assert.That(_source.Recreated).IsEquivalentTo(["/music"]);

        Wait(20);
        await Assert.That(_reconciler.Count).IsEqualTo(1);
    }

    [Test]
    public async Task AFailedReconcile_IsReported_AndTheNextBurstStillFires()
    {
        using var detector = CreateDetector();
        var failure = new TaskCompletionSource();
        failure.SetException(new IOException("disk gone"));
        _reconciler.Next = failure;

        FileChanged("/music/a.mp3");
        Wait(2);
        _reconciler.Next = CompletedRun();
        FileChanged("/music/b.mp3");
        Wait(2);

        await Assert.That(_errors.Single().Message).IsEqualTo("disk gone");
        await Assert.That(_reconciler.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Watch_PassesTheRootsToTheSource_AndStoppingDropsAPendingBurst()
    {
        using var detector = CreateDetector();
        await Assert.That(_source.Roots).IsEquivalentTo(["/music"]);

        FileChanged("/music/a.mp3");
        detector.Watch([]);
        Wait(20);

        await Assert.That(_source.Roots).IsEmpty();
        await Assert.That(_reconciler.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Dispose_StopsWatching_AndIgnoresLaterEvents()
    {
        var detector = CreateDetector();

        detector.Dispose();
        FileChanged("/music/a.mp3");
        Wait(20);

        await Assert.That(_source.Roots).IsEmpty();
        await Assert.That(_reconciler.Count).IsEqualTo(0);
    }

    // Each reconcile returns Next's task, so a test decides when it finishes.
    private sealed class Reconciler
    {
        public int Count { get; private set; }

        public TaskCompletionSource Next { get; set; } = CompletedRun();

        public Task Run()
        {
            Count++;
            return Next.Task;
        }
    }
}
