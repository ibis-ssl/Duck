using Tracker.Core;

namespace Tracker.Server.Tracking;

public sealed class TrackerCoordinator
{
    private readonly object gate = new();
    private readonly ITrackerEngine engine;
    private readonly TrackerPacketGenerator packetGenerator;
    private readonly TrackerEngineSettings settings;
    private readonly TrackedSnapshotStore snapshotStore;
    private readonly ITrackerPacketPublisher publisher;
    private readonly IReadOnlyList<ITrackerObserver> observers;
    private readonly ILogger<TrackerCoordinator> logger;

    public TrackerCoordinator(
        ITrackerEngine engine,
        TrackerPacketGenerator packetGenerator,
        TrackerEngineSettings settings,
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        IEnumerable<ITrackerObserver> observers,
        ILogger<TrackerCoordinator> logger)
    {
        this.engine = engine;
        this.packetGenerator = packetGenerator;
        this.settings = settings;
        this.snapshotStore = snapshotStore;
        this.publisher = publisher;
        this.observers = observers.ToArray();
        this.logger = logger;
    }

    public TrackerUpdateResult ProcessPacket(
        SSL_WrapperPacket? packet,
        DateTimeOffset receivedAt,
        TrackerProfileSwitchRequest? profileSwitchRequest = null)
    {
        lock (gate)
        {
            var result = engine.Update(packet, settings, profileSwitchRequest);
            var framesByNumber = result.CommittedFrames.ToDictionary(frame => frame.FrameNumber);

            foreach (var emittedEvent in result.EmittedEvents)
            {
                switch (emittedEvent.Kind)
                {
                    case TrackerEventKind.ProfileSwitched:
                        ApplyProfileSwitch(emittedEvent.ProfileName ?? settings.ProfileName);
                        break;
                    case TrackerEventKind.GeometryReset:
                        snapshotStore.ClearLatestFrame();
                        NotifyObservers(observer => observer.OnGeometryReset());
                        break;
                    case TrackerEventKind.WorldFrameCommitted:
                        if (TryGetFrame(framesByNumber, emittedEvent.FrameNumber, out var committedFrame))
                        {
                            snapshotStore.UpdateLatestFrame(committedFrame, receivedAt);
                            PublishFrame(committedFrame);
                            NotifyObservers(observer => observer.OnWorldFrameCommitted(committedFrame));
                        }

                        break;
                    case TrackerEventKind.KickDetected:
                        if (TryGetFrame(framesByNumber, emittedEvent.FrameNumber, out var kickFrame) && kickFrame.KickedBall is not null)
                        {
                            NotifyObservers(observer => observer.OnKickDetected(kickFrame.KickedBall, kickFrame));
                        }

                        break;
                    case TrackerEventKind.ContactChanged:
                        if (TryGetFrame(framesByNumber, emittedEvent.FrameNumber, out var contactFrame))
                        {
                            NotifyObservers(observer => observer.OnContactChanged(contactFrame));
                        }

                        break;
                    case TrackerEventKind.BallLeftField:
                        if (TryGetFrame(framesByNumber, emittedEvent.FrameNumber, out var leftFieldFrame) && leftFieldFrame.BallLeftField is not null)
                        {
                            NotifyObservers(observer => observer.OnBallLeftField(leftFieldFrame.BallLeftField, leftFieldFrame));
                        }

                        break;
                }
            }

            return result;
        }
    }

    private void ApplyProfileSwitch(string profileName)
    {
        snapshotStore.SetActiveProfileName(profileName);
        snapshotStore.ClearLatestFrame();
        NotifyObservers(observer => observer.OnProfileSwitched(profileName));
    }

    private void PublishFrame(TrackerFrame frame)
    {
        try
        {
            publisher.Publish(packetGenerator.Generate(frame));
            snapshotStore.RecordPublishSuccess();
        }
        catch (Exception ex)
        {
            snapshotStore.RecordPublishFailure();
            logger.LogWarning(ex, "Failed to publish tracker packet for frame {FrameNumber}", frame.FrameNumber);
        }
    }

    private void NotifyObservers(Action<ITrackerObserver> notify)
    {
        foreach (var observer in observers)
        {
            notify(observer);
        }
    }

    private static bool TryGetFrame(
        IReadOnlyDictionary<uint, TrackerFrame> framesByNumber,
        uint? frameNumber,
        out TrackerFrame frame)
    {
        if (frameNumber is not null && framesByNumber.TryGetValue(frameNumber.Value, out frame!))
        {
            return true;
        }

        frame = null!;
        return false;
    }
}
