using DataLogging.MockHardware.Models;

namespace DataLogging.MockHardware.Abstractions;

public interface IMockHardwareController
{
    MockHardwareControlStatus GetStatus();

    MockHardwareControlStatus Start();

    MockHardwareControlStatus Pause();

    MockHardwareControlStatus Stop();
}
