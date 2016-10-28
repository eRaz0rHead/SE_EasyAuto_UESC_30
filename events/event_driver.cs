//@ commons
public class EventDriver
{
    public struct FutureTickAction : IComparable<FutureTickAction>
    {
        public ulong When;
        public Action<ZACommons, EventDriver> Action;

        public FutureTickAction(ulong when, Action<ZACommons, EventDriver> action = null)
        {
            When = when;
            Action = action;
        }

        public int CompareTo(FutureTickAction other)
        {
            return When.CompareTo(other.When);
        }
    }

    public struct FutureTimeAction : IComparable<FutureTimeAction>
    {
        public TimeSpan When;
        public Action<ZACommons, EventDriver> Action;

        public FutureTimeAction(TimeSpan when, Action<ZACommons, EventDriver> action = null)
        {
            When = when;
            Action = action;
        }

        public int CompareTo(FutureTimeAction other)
        {
            return When.CompareTo(other.When);
        }
    }

    private const ulong TicksPerSecond = 60;
    // Time events trigger half a frame earlier
    // Avoid problems with accumulating time when running on Trigger Now
    private readonly TimeSpan TIMESPAN_FUDGE = TimeSpan.FromMilliseconds(500.0 / TicksPerSecond);

    // Why is there no standard priority queue implementation?
    private readonly LinkedList<FutureTickAction> TickQueue = new LinkedList<FutureTickAction>();
    private readonly LinkedList<FutureTimeAction> TimeQueue = new LinkedList<FutureTimeAction>();
    private readonly string TimerName, TimerGroup;
    private ulong Ticks; // Not a reliable measure of time because of variable timer delay.

    public TimeSpan TimeSinceStart { get; private set; }

    // If neither timerName nor timerGroup are given, it's assumed the timer will kick itself
    // Note that using only timerGroup is unreliable, especially when grids
    // detach/merge (via connectors, merge blocks, etc.)
    public EventDriver(string timerName = null, string timerGroup = null)
    {
        TimerName = timerName;
        TimerGroup = timerGroup;

        TimeSinceStart = TimeSpan.FromSeconds(0);
    }

    private void KickTimer(ZACommons commons)
    {
        IMyTimerBlock timer = null;
        // Name takes priority over group
        if (TimerName != null)
        {
            var blocks = ZACommons.SearchBlocksOfName(commons.Blocks, TimerName,
                                                      block => block is IMyTimerBlock &&
                                                      ((IMyTimerBlock)block).Enabled);
            if (blocks.Count > 0)
            {
                timer = blocks[0] as IMyTimerBlock;
            }
        }
        if (timer == null && TimerGroup != null)
        {
            var group = commons.GetBlockGroupWithName(TimerGroup);
            if (group != null)
            {
                var blocks = ZACommons.GetBlocksOfType<IMyTimerBlock>(group.Blocks,
                                                                      block => block.CubeGrid == commons.Me.CubeGrid &&
                                                                      ((IMyTimerBlock)block).Enabled);
                timer = blocks.Count > 0 ? (IMyTimerBlock)blocks[0] : null;
            }
        }

        if (timer != null)
        {
            // Rules are simple. If we have something in the tick queue, trigger now.
            // Otherwise, set timer delay appropriately (minimum 1 second) and kick.
            // If you want sub-second accuracy, always use ticks.
            if (TickQueue.First != null)
            {
                timer.ApplyAction("TriggerNow");
            }
            else if (TimeQueue.First != null)
            {
                var next = (float)(TimeQueue.First.Value.When.TotalSeconds - TimeSinceStart.TotalSeconds);
                // Constrain appropriately (not sure if this will be done for us or if it
                // will just throw). Just do it to be safe.
                next = Math.Max(next, timer.GetMinimum<float>("TriggerDelay"));
                next = Math.Min(next, timer.GetMaximum<float>("TriggerDelay"));

                timer.SetValue<float>("TriggerDelay", next);
                timer.ApplyAction("Start");
            }
            // NB If both queues are empty, we stop running
        }
    }

    public void Tick(ZACommons commons, Action mainAction = null,
                     Action preAction = null,
                     Action postAction = null)
    {
        Ticks++;
        TimeSinceStart += commons.Program.Runtime.TimeSinceLastRun;

        bool runMain = false;

        if (preAction != null) preAction();

        // Process each queue independently
        while (TickQueue.First != null &&
               TickQueue.First.Value.When <= Ticks)
        {
            var action = TickQueue.First.Value.Action;
            TickQueue.RemoveFirst();
            if (action != null)
            {
                action(commons, this);
            }
            else
            {
                runMain = true;
            }
        }

        while (TimeQueue.First != null &&
               TimeQueue.First.Value.When <= TimeSinceStart)
        {
            var action = TimeQueue.First.Value.Action;
            TimeQueue.RemoveFirst();
            if (action != null)
            {
                action(commons, this);
            }
            else
            {
                runMain = true;
            }
        }

        if (runMain && mainAction != null) mainAction();

        if (postAction != null) postAction();

        KickTimer(commons);
    }

    public void Schedule(ulong delay, Action<ZACommons, EventDriver> action = null)
    {
        var future = new FutureTickAction(Ticks + delay, action);
        for (var current = TickQueue.First;
             current != null;
             current = current.Next)
        {
            if (future.CompareTo(current.Value) < 0)
            {
                // Insert before this one
                TickQueue.AddBefore(current, future);
                return;
            }
        }
        // Just add at the end
        TickQueue.AddLast(future);
    }

    public void Schedule(double seconds, Action<ZACommons, EventDriver> action = null)
    {
        var delay = Math.Max(seconds, 0.0);

        var future = new FutureTimeAction(TimeSinceStart + TimeSpan.FromSeconds(delay) - TIMESPAN_FUDGE, action);
        for (var current = TimeQueue.First;
             current != null;
             current = current.Next)
        {
            if (future.CompareTo(current.Value) < 0)
            {
                // Insert before this one
                TimeQueue.AddBefore(current, future);
                return;
            }
        }
        // Just add at the end
        TimeQueue.AddLast(future);
    }
}
