namespace Tracker.Core;

/// <summary>
/// TrackerCoordinator の engine event dispatch、snapshot 更新、publish、observer 通知順序を担当する partial class。
/// </summary>
public sealed partial class TrackerCoordinator
{
    /// <summary>
    /// TrackerUpdateResult の emitted event 順に snapshot、publish、observer 通知を dispatch する。
    /// </summary>
    private void DispatchResult(TrackerUpdateResult result, DateTimeOffset receivedAt)
    {
        var framesByNumber = result.CommittedFrames.ToDictionary(frame => frame.FrameNumber);

        foreach (var emittedEvent in result.EmittedEvents)
        {
            switch (emittedEvent.Kind)
            {
                case TrackerEventKind.ProfileSwitched:
                    ApplyProfileSwitch(emittedEvent.ProfileName ?? currentSettings.ProfileName);
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
    }

    private void PublishFrame(TrackerFrame frame)
    {
        try
        {
            var packet = packetGenerator.Generate(frame);
            publisher.Publish(packet);
            snapshotStore.RecordPublishSuccess();
        }
        catch
        {
            snapshotStore.RecordPublishFailure();
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
