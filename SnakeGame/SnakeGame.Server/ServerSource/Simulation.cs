using Silk.NET.Maths;

namespace SnakeGame;

public class Simulation
{
    public const uint SimPastSize = 100; // how many ticks to keep in the past

    public uint CurrentTick { get; private set; }
    public List<TickEvent> Events { get; private set; }

    public GameState State { get; private set; }
    private GameState OldState { get; set; }

    public Simulation()
    {
        OldState = new GameState();
        State = new GameState();

        Events = new List<TickEvent>();
    }

    /// <summary>
    /// Advances the simulation by one tick and performs all the events pushed to the simulation
    /// </summary>
    /// <param name="action"></param>
    public void Simulate(Action action)
    {
        CurrentTick++;
        action();

        List<TickEvent> processedTicks = Events.Where(e => e.Tick > State.Tick).ToList();
        processedTicks.ForEach(e => e.Perform(State));
        State.Tick = CurrentTick;

        // Advance OldState and remove old events
        List<TickEvent> oldEvents = Events.Where(e => e.Tick < CurrentTick - SimPastSize).ToList();
        oldEvents.ForEach(e => e.Perform(OldState));
        Events.RemoveAll(e => e.Tick < CurrentTick - SimPastSize);
    }

    /// <summary>
    /// Pushes an event to the simulation. MUST BE CALLED IN THE SIMULATE ACTION
    /// </summary>
    /// <param name="e"></param>
    public void PushEvent(TickEvent e)
    {
        e.Tick = CurrentTick;
        Events.Add(e);
    }
}