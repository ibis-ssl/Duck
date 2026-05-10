namespace Tracker.Server.Tracking;

public interface ITrackerPacketPublisher
{
    void ApplyConfiguration(TrackerPublisherOptions options);

    void Publish(TrackerWrapperPacket packet);
}
