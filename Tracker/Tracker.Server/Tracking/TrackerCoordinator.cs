using Tracker.Core;

namespace Tracker.Server.Tracking;

public sealed class TrackerCoordinator
{
    private static readonly TimeSpan TrackerDiagnosticsLogInterval = TimeSpan.FromSeconds(1);
    private readonly object gate = new();
    private readonly ITrackerEngine engine;
    private readonly TrackerPacketGenerator packetGenerator;
    private TrackerEngineSettings currentSettings;
    private TrackerPublisherOptions currentPublisherOptions;
    private readonly TrackedSnapshotStore snapshotStore;
    private readonly ITrackerPacketPublisher publisher;
    private readonly IReadOnlyList<ITrackerObserver> observers;
    private readonly ILogger<TrackerCoordinator> logger;
    private TrackerResolvedOptions appliedOptions;
    private TrackerResolvedOptions desiredOptions;
    private TrackerRuntimeOverrides desiredRuntimeOverrides = new();
    private PendingProfileSwitchRequest? pendingRequest;
    private PendingProfileSwitchRequest? inFlightRequest;
    private int nextRequestVersion = 1;
    private bool isProcessingUpdate;
    private DateTimeOffset lastTrackerDiagnosticsLogAt = DateTimeOffset.MinValue;

    public TrackerCoordinator(
        ITrackerEngine engine,
        TrackerPacketGenerator packetGenerator,
        TrackerEngineSettings settings,
        TrackerPublisherOptions publisherOptions,
        TrackedSnapshotStore snapshotStore,
        ITrackerPacketPublisher publisher,
        IEnumerable<ITrackerObserver> observers,
        ILogger<TrackerCoordinator> logger)
    {
        this.engine = engine;
        this.packetGenerator = packetGenerator;
        currentSettings = CloneSettings(settings);
        currentPublisherOptions = ClonePublisherOptions(publisherOptions);
        this.snapshotStore = snapshotStore;
        this.publisher = publisher;
        this.observers = observers.ToArray();
        this.logger = logger;
        appliedOptions = new TrackerResolvedOptions
        {
            Enabled = true,
            EngineSettings = CloneSettings(settings),
            PublisherOptions = ClonePublisherOptions(publisherOptions),
        };
        desiredOptions = CloneResolvedOptions(appliedOptions);
        desiredRuntimeOverrides = new TrackerRuntimeOverrides();
        publisher.ApplyConfiguration(currentPublisherOptions);
    }

    public TrackerUpdateResult ProcessPacket(
        SSL_WrapperPacket? packet,
        DateTimeOffset receivedAt,
        TrackerProfileSwitchRequest? profileSwitchRequest = null)
    {
        lock (gate)
        {
            return ExecuteUpdates(packet, receivedAt, profileSwitchRequest);
        }
    }

    public void RequestProfileSwitch(
        TrackerResolvedOptions targetOptions,
        DateTimeOffset receivedAt,
        TrackerRuntimeOverrides? runtimeOverrides = null)
    {
        lock (gate)
        {
            var normalizedTarget = CloneResolvedOptions(targetOptions);
            var normalizedRuntimeOverrides = runtimeOverrides is null
                ? new TrackerRuntimeOverrides()
                : CloneRuntimeOverrides(runtimeOverrides);
            if (AreResolvedOptionsEquivalent(desiredOptions, normalizedTarget) &&
                AreRuntimeOverridesEquivalent(desiredRuntimeOverrides, normalizedRuntimeOverrides) &&
                pendingRequest is null &&
                inFlightRequest is null)
            {
                return;
            }

            desiredOptions = normalizedTarget;
            desiredRuntimeOverrides = normalizedRuntimeOverrides;
            pendingRequest = new PendingProfileSwitchRequest(
                nextRequestVersion++,
                normalizedTarget,
                normalizedRuntimeOverrides);

            if (isProcessingUpdate)
            {
                return;
            }

            _ = ExecuteUpdates(packet: null, receivedAt, explicitProfileSwitchRequest: null);
        }
    }

    private void ApplyProfileSwitch(string profileName)
    {
        if (inFlightRequest is not null)
        {
            appliedOptions = CloneResolvedOptions(inFlightRequest.TargetOptions);
            desiredOptions = CloneResolvedOptions(inFlightRequest.TargetOptions);
            desiredRuntimeOverrides = CloneRuntimeOverrides(inFlightRequest.RuntimeOverrides);
            currentSettings = CloneSettings(inFlightRequest.TargetOptions.EngineSettings);
            currentPublisherOptions = ClonePublisherOptions(inFlightRequest.TargetOptions.PublisherOptions);
            publisher.ApplyConfiguration(currentPublisherOptions);
            snapshotStore.SwitchActiveProfile(profileName);
            inFlightRequest = null;
        }
        else
        {
            snapshotStore.SwitchActiveProfile(profileName);
        }

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

    private TrackerUpdateResult ExecuteUpdates(
        SSL_WrapperPacket? packet,
        DateTimeOffset receivedAt,
        TrackerProfileSwitchRequest? explicitProfileSwitchRequest)
    {
        isProcessingUpdate = true;
        try
        {
            var committedFrames = new List<TrackerFrame>();
            var emittedEvents = new List<TrackerEvent>();
            var latePacketDropCount = 0;
            var firstIteration = true;

            do
            {
                var updatePacket = firstIteration ? packet : null;
                var switchRequest = explicitProfileSwitchRequest ?? PromotePendingRequest();
                var result = engine.Update(updatePacket, currentSettings, switchRequest);
                LogTrackerDiagnostics(updatePacket, result, receivedAt);
                DispatchResult(result, receivedAt);

                committedFrames.AddRange(result.CommittedFrames);
                emittedEvents.AddRange(result.EmittedEvents);
                latePacketDropCount += result.Diagnostics.LatePacketDropCount;
                firstIteration = false;
                explicitProfileSwitchRequest = null;
            }
            while (pendingRequest is not null);

            return new TrackerUpdateResult
            {
                CommittedFrames = committedFrames,
                EmittedEvents = emittedEvents,
                Diagnostics = new TrackerEngineDiagnostics
                {
                    LatePacketDropCount = latePacketDropCount,
                },
            };
        }
        finally
        {
            isProcessingUpdate = false;
        }
    }

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

    private void LogTrackerDiagnostics(
        SSL_WrapperPacket? packet,
        TrackerUpdateResult result,
        DateTimeOffset receivedAt)
    {
        if (!logger.IsEnabled(LogLevel.Information) || result.CommittedFrames.Count == 0)
        {
            return;
        }

        var newestFrame = result.CommittedFrames[^1];
        var detection = packet?.Detection;
        if (!ShouldLogTrackerDiagnostics(receivedAt, detection, newestFrame))
        {
            return;
        }

        lastTrackerDiagnosticsLogAt = receivedAt;
        logger.LogInformation(
            "Tracker diagnostics profile={ProfileName} rawFrame={RawFrameNumber} rawCamera={RawCameraId} rawBalls={RawBallCount} rawBallDetails=[{RawBallDetails}] rawBlue=[{RawBlueDetails}] rawYellow=[{RawYellowDetails}] trackedFrame={TrackedFrameNumber} trackedBalls={TrackedBallCount} trackedBallDetails=[{TrackedBallDetails}] trackedRobots={TrackedRobotCount} trackedRobotDetails=[{TrackedRobotDetails}] robotOutVisibility={RobotOutputVisibilityThreshold} robotHalfLifeSec={RobotVisibilityHalfLifeSeconds} ballOutVisibility={BallOutputVisibilityThreshold} ballHalfLifeSec={BallVisibilityHalfLifeSeconds} ballLifetimeNs={BallTrackLifetimeNs}",
            currentSettings.ProfileName,
            detection?.FrameNumber,
            detection?.CameraId,
            detection?.Balls.Count ?? 0,
            FormatRawBalls(detection?.Balls),
            FormatRawRobots(detection?.RobotsBlue, TrackerTeam.Blue),
            FormatRawRobots(detection?.RobotsYellow, TrackerTeam.Yellow),
            newestFrame.FrameNumber,
            newestFrame.Balls.Count,
            FormatTrackedBalls(newestFrame.Balls),
            newestFrame.Robots.Count,
            FormatTrackedRobots(newestFrame.Robots),
            currentSettings.RobotTracker.OutputVisibilityThreshold,
            currentSettings.RobotTracker.VisibilityHalfLifeSeconds,
            currentSettings.BallTracker.OutputVisibilityThreshold,
            currentSettings.BallTracker.VisibilityHalfLifeSeconds,
            currentSettings.BallTracker.TrackLifetimeNs);
    }

    private bool ShouldLogTrackerDiagnostics(
        DateTimeOffset receivedAt,
        SSL_DetectionFrame? detection,
        TrackerFrame newestFrame)
    {
        if (receivedAt - lastTrackerDiagnosticsLogAt >= TrackerDiagnosticsLogInterval)
        {
            return true;
        }

        return (detection?.Balls.Count ?? 0) > 1
            || newestFrame.Balls.Count > 1;
    }

    private static string FormatRawBalls(IEnumerable<SSL_DetectionBall>? balls)
    {
        return FormatItems(
            balls,
            ball => FormattableString.Invariant(
                $"x={ball.X:0.#},y={ball.Y:0.#},z={ball.Z:0.#},c={ball.Confidence:0.###}"));
    }

    private static string FormatRawRobots(
        IEnumerable<SSL_DetectionRobot>? robots,
        TrackerTeam team)
    {
        var teamPrefix = team == TrackerTeam.Blue ? "B" : "Y";
        return FormatItems(
            robots,
            robot => FormattableString.Invariant(
                $"{teamPrefix}{robot.RobotId}:x={robot.X:0.#},y={robot.Y:0.#},o={robot.Orientation:0.###},c={robot.Confidence:0.###}"));
    }

    private static string FormatTrackedBalls(IEnumerable<TrackedBallState> balls)
    {
        return FormatItems(
            balls,
            ball => FormattableString.Invariant(
                $"#{ball.InternalTrackId}:x={ball.XMm:0.#},y={ball.YMm:0.#},z={ball.ZMm:0.#},vis={ball.Visibility:0.###},q={ball.Quality:0.###},cams={string.Join("/", ball.SourceCameraIds)}"));
    }

    private static string FormatTrackedRobots(IEnumerable<TrackedRobotState> robots)
    {
        return FormatItems(
            robots,
            robot => FormattableString.Invariant(
                $"{FormatTeam(robot.Team)}{robot.RobotId}:x={robot.XMm:0.#},y={robot.YMm:0.#},vis={robot.Visibility:0.###},q={robot.Quality:0.###}"));
    }

    private static string FormatTeam(TrackerTeam team)
    {
        return team switch
        {
            TrackerTeam.Blue => "B",
            TrackerTeam.Yellow => "Y",
            _ => "?",
        };
    }

    private static string FormatItems<T>(
        IEnumerable<T>? items,
        Func<T, string> formatter)
    {
        if (items is null)
        {
            return "";
        }

        var formattedItems = items.Take(16).Select(formatter).ToList();
        return formattedItems.Count == 0 ? "" : string.Join("; ", formattedItems);
    }

    private TrackerProfileSwitchRequest? PromotePendingRequest()
    {
        if (inFlightRequest is not null || pendingRequest is null)
        {
            return null;
        }

        inFlightRequest = pendingRequest;
        pendingRequest = null;
        return new TrackerProfileSwitchRequest
        {
            RequestVersion = inFlightRequest.RequestVersion,
            ProfileName = inFlightRequest.TargetOptions.EngineSettings.ProfileName,
            ResolvedBaseSettings = CloneSettings(inFlightRequest.TargetOptions.EngineSettings),
            RuntimeOverrides = CloneRuntimeOverrides(inFlightRequest.RuntimeOverrides),
        };
    }

    private static bool AreResolvedOptionsEquivalent(
        TrackerResolvedOptions left,
        TrackerResolvedOptions right)
    {
        return left.Enabled == right.Enabled
            && left.EngineSettings.ProfileName == right.EngineSettings.ProfileName
            && left.EngineSettings.ReorderWindowNs == right.EngineSettings.ReorderWindowNs
            && left.EngineSettings.MergeWindowNs == right.EngineSettings.MergeWindowNs
            && left.EngineSettings.GeometryResetFieldLengthThresholdMm == right.EngineSettings.GeometryResetFieldLengthThresholdMm
            && left.EngineSettings.GeometryResetFieldWidthThresholdMm == right.EngineSettings.GeometryResetFieldWidthThresholdMm
            && AreRobotTrackerOverridesEquivalent(left.EngineSettings.RobotTracker, right.EngineSettings.RobotTracker)
            && AreBallTrackerOverridesEquivalent(left.EngineSettings.BallTracker, right.EngineSettings.BallTracker)
            && AreKickDetectorOverridesEquivalent(left.EngineSettings.KickDetector, right.EngineSettings.KickDetector)
            && left.PublisherOptions.PublishUdp == right.PublisherOptions.PublishUdp
            && left.PublisherOptions.MulticastAddress == right.PublisherOptions.MulticastAddress
            && left.PublisherOptions.Port == right.PublisherOptions.Port
            && left.PublisherOptions.SourceName == right.PublisherOptions.SourceName
            && left.PublisherOptions.Uuid == right.PublisherOptions.Uuid;
    }

    private static bool AreRuntimeOverridesEquivalent(
        TrackerRuntimeOverrides left,
        TrackerRuntimeOverrides right)
    {
        return left.Publish.MulticastAddress == right.Publish.MulticastAddress
            && left.Publish.Port == right.Publish.Port
            && left.Publish.SourceName == right.Publish.SourceName
            && left.Publish.Uuid == right.Publish.Uuid
            && AreRobotTrackerOverridesEquivalent(left.RobotTracker, right.RobotTracker)
            && AreBallTrackerOverridesEquivalent(left.BallTracker, right.BallTracker)
            && AreKickDetectorOverridesEquivalent(left.KickDetector, right.KickDetector);
    }

    private static bool AreRobotTrackerOverridesEquivalent(
        TrackerRobotTrackerOverrides left,
        TrackerRobotTrackerOverrides right)
    {
        return left.ProcessNoise == right.ProcessNoise
            && left.MeasurementNoise == right.MeasurementNoise
            && left.VisibilityHalfLifeSeconds == right.VisibilityHalfLifeSeconds
            && left.OutputVisibilityThreshold == right.OutputVisibilityThreshold
            && left.Gate == right.Gate
            && left.OutlierLimitMm == right.OutlierLimitMm;
    }

    private static bool AreBallTrackerOverridesEquivalent(
        TrackerBallTrackerOverrides left,
        TrackerBallTrackerOverrides right)
    {
        return left.ProcessNoise == right.ProcessNoise
            && left.MeasurementNoise == right.MeasurementNoise
            && left.VisibilityHalfLifeSeconds == right.VisibilityHalfLifeSeconds
            && left.OutputVisibilityThreshold == right.OutputVisibilityThreshold
            && left.Gate == right.Gate
            && left.OutlierLimitMm == right.OutlierLimitMm
            && left.TrackLifetimeNs == right.TrackLifetimeNs;
    }

    private static bool AreKickDetectorOverridesEquivalent(
        TrackerKickDetectorOverrides left,
        TrackerKickDetectorOverrides right)
    {
        return left.KickSpeedThresholdMmPerS == right.KickSpeedThresholdMmPerS
            && left.ChipHeightThresholdMm == right.ChipHeightThresholdMm
            && left.ContactMarginMm == right.ContactMarginMm;
    }

    private static TrackerResolvedOptions CloneResolvedOptions(TrackerResolvedOptions options)
    {
        return new TrackerResolvedOptions
        {
            Enabled = options.Enabled,
            EngineSettings = CloneSettings(options.EngineSettings),
            PublisherOptions = ClonePublisherOptions(options.PublisherOptions),
        };
    }

    private static TrackerEngineSettings CloneSettings(TrackerEngineSettings settings)
    {
        return new TrackerEngineSettings
        {
            ProfileName = settings.ProfileName,
            ReorderWindowNs = settings.ReorderWindowNs,
            MergeWindowNs = settings.MergeWindowNs,
            GeometryResetFieldLengthThresholdMm = settings.GeometryResetFieldLengthThresholdMm,
            GeometryResetFieldWidthThresholdMm = settings.GeometryResetFieldWidthThresholdMm,
            RobotTracker = CloneRobotTracker(settings.RobotTracker),
            BallTracker = CloneBallTracker(settings.BallTracker),
            KickDetector = CloneKickDetector(settings.KickDetector),
        };
    }

    private static TrackerPublisherOptions ClonePublisherOptions(TrackerPublisherOptions options)
    {
        return new TrackerPublisherOptions
        {
            PublishUdp = options.PublishUdp,
            MulticastAddress = options.MulticastAddress,
            Port = options.Port,
            SourceName = options.SourceName,
            Uuid = options.Uuid,
        };
    }

    private static TrackerRuntimeOverrides CloneRuntimeOverrides(TrackerRuntimeOverrides overrides)
    {
        return new TrackerRuntimeOverrides
        {
            Publish = new TrackerPublishOverrides
            {
                MulticastAddress = overrides.Publish.MulticastAddress,
                Port = overrides.Publish.Port,
                SourceName = overrides.Publish.SourceName,
                Uuid = overrides.Publish.Uuid,
            },
            RobotTracker = CloneRobotTracker(overrides.RobotTracker),
            BallTracker = CloneBallTracker(overrides.BallTracker),
            KickDetector = CloneKickDetector(overrides.KickDetector),
        };
    }

    private static TrackerRobotTrackerOverrides CloneRobotTracker(TrackerRobotTrackerOverrides tracker)
    {
        return new TrackerRobotTrackerOverrides
        {
            ProcessNoise = tracker.ProcessNoise,
            MeasurementNoise = tracker.MeasurementNoise,
            VisibilityHalfLifeSeconds = tracker.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = tracker.OutputVisibilityThreshold,
            Gate = tracker.Gate,
            OutlierLimitMm = tracker.OutlierLimitMm,
        };
    }

    private static TrackerBallTrackerOverrides CloneBallTracker(TrackerBallTrackerOverrides tracker)
    {
        return new TrackerBallTrackerOverrides
        {
            ProcessNoise = tracker.ProcessNoise,
            MeasurementNoise = tracker.MeasurementNoise,
            VisibilityHalfLifeSeconds = tracker.VisibilityHalfLifeSeconds,
            OutputVisibilityThreshold = tracker.OutputVisibilityThreshold,
            Gate = tracker.Gate,
            OutlierLimitMm = tracker.OutlierLimitMm,
            TrackLifetimeNs = tracker.TrackLifetimeNs,
        };
    }

    private static TrackerKickDetectorOverrides CloneKickDetector(TrackerKickDetectorOverrides detector)
    {
        return new TrackerKickDetectorOverrides
        {
            KickSpeedThresholdMmPerS = detector.KickSpeedThresholdMmPerS,
            ChipHeightThresholdMm = detector.ChipHeightThresholdMm,
            ContactMarginMm = detector.ContactMarginMm,
        };
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

    private sealed record PendingProfileSwitchRequest(
        int RequestVersion,
        TrackerResolvedOptions TargetOptions,
        TrackerRuntimeOverrides RuntimeOverrides);
}
