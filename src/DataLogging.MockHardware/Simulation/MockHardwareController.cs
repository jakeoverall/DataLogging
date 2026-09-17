using DataLogging.MockHardware.Abstractions;
using DataLogging.MockHardware.Models;

namespace DataLogging.MockHardware.Simulation;

public sealed class MockHardwareController : IMockHardwareController
{
    private readonly object _gate = new();
    private MockHardwareControlStatus _status = new()
    {
        State = MockHardwareRunState.Running,
        UpdatedAt = DateTimeOffset.UtcNow,
        Reason = "initial"
    };

    public MockHardwareControlStatus GetStatus()
    {
        lock (_gate)
        {
            return _status;
        }
    }

    public MockHardwareControlStatus Start()
    {
        return SetState(MockHardwareRunState.Running, "api-start");
    }

    public MockHardwareControlStatus Pause()
    {
        return SetState(MockHardwareRunState.Paused, "api-pause");
    }

    public MockHardwareControlStatus Stop()
    {
        return SetState(MockHardwareRunState.Stopped, "api-stop");
    }

    private MockHardwareControlStatus SetState(MockHardwareRunState state, string reason)
    {
        lock (_gate)
        {
            _status = new MockHardwareControlStatus
            {
                State = state,
                UpdatedAt = DateTimeOffset.UtcNow,
                Reason = reason
            };
            return _status;
        }
    }
}
