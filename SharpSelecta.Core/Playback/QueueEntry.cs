using SharpSelecta.Core.Library;

namespace SharpSelecta.Core.Playback;

public sealed record QueueEntry(Track Track, QueueEntrySource Source);
