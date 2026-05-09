namespace Tracker.Server.Tracking;

public interface ITrackerPacketPublisher
{
    void Publish(TrackerWrapperPacket packet);
}
