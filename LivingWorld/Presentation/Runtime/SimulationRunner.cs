using System.Collections.Concurrent;
using System.Diagnostics;
using Thread = System.Threading.Thread;
using LivingWorld.Definitions;
using LivingWorld.Infrastructure;
using LivingWorld.Simulation;

namespace LivingWorld.Presentation;

// Exactly one worker owns the mutable simulation. Godot only receives immutable publications.
public sealed class SimulationRunner : IDisposable
{
    public const int BaseTicksPerSecond = 24;
    public const int PublicationsPerSecond = 20;
    private sealed record Command(Action Run, Action Cancel);
    private readonly ConcurrentQueue<Command> _commands = new();
    private readonly ConcurrentQueue<string> _errors = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly object _lifecycle = new();
    private readonly Thread _thread;
    private readonly DefinitionCatalog _definitions;
    private readonly SaveService _saves = new();
    private volatile bool _stopping;
    private SimulationSession? _session;
    private RenderSnapshotBuilder _builder = new();
    private RenderSnapshot? _latest;
    private ViewRequest _request = new();
    private bool _paused = true, _faulted;
    private int _speed = 1;
    private long _generation, _previousTime, _lastPublication, _sampleTime, _sampleTick;
    private double _credit, _ticksPerSecond;

    public SimulationRunner(DefinitionCatalog definitions)
    {
        _definitions = definitions;
        _thread = new Thread(Run) { IsBackground = true, Name = "LivingWorld simulation" };
        _thread.Start();
    }

    public RenderSnapshot? Latest => Volatile.Read(ref _latest);
    public bool TryGetError(out string message) => _errors.TryDequeue(out message!);

    public void SetView(ViewRequest request)
    {
        lock (_lifecycle)
        {
            if (_stopping || request == _request) return;
            Volatile.Write(ref _request, request);
            _wake.Set();
        }
    }

    public Task SetControlsAsync(bool paused, int speed) => Submit(() =>
    {
        if (speed is not (1 or 4 or 12 or 32)) throw new ArgumentOutOfRangeException(nameof(speed));
        _paused = paused;
        _speed = speed;
        ResetTiming();
        Publish();
        return true;
    });

    public Task NewWorldAsync(int seed, int size, int people) => Submit(() =>
    {
        var state = new WorldGenerator().Generate(_definitions, seed, size, people);
        Install(new SimulationSession(state, _definitions), paused: false);
        return true;
    });

    public Task LoadAsync(string path) => Submit(() =>
    {
        var loaded = _saves.Load(path, _definitions);
        Install(loaded, paused: true);
        return true;
    });

    public Task SaveAsync(string path) => Submit(() =>
    {
        _saves.Save(RequireSession(), path);
        // Disk I/O does not create a burst of simulation debt.
        ResetTiming();
        return true;
    });

    public Task<string> StateHashAsync() => Submit(() => _saves.Hash(RequireSession()));

    // Useful for deterministic regression runs: real speed and publications cannot alter tick results.
    public Task AdvanceAsync(int ticks) => Submit(() =>
    {
        if (!_paused) throw new InvalidOperationException("Pause before advancing an exact number of ticks.");
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        var session = RequireSession();
        for (var i = 0; i < ticks; i++)
        {
            if (_stopping) throw new ObjectDisposedException(nameof(SimulationRunner));
            session.Step();
        }
        ResetTiming();
        Publish(force: true);
        return true;
    });

    private SimulationSession RequireSession() => _session ?? throw new InvalidOperationException("World is not loaded.");

    private Task<T> Submit<T>(Func<T> action)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_lifecycle)
        {
            if (_stopping) return Task.FromException<T>(new ObjectDisposedException(nameof(SimulationRunner)));
            _commands.Enqueue(new(() =>
            {
                try { completion.TrySetResult(action()); }
                catch (Exception ex) { completion.TrySetException(ex); }
            }, () => completion.TrySetException(new ObjectDisposedException(nameof(SimulationRunner)))));
            _wake.Set();
        }
        return completion.Task;
    }

    private void Install(SimulationSession session, bool paused)
    {
        _session = session;
        _builder = new();
        _generation++;
        _paused = paused;
        _faulted = false;
        ResetTiming();
        Publish(force: true);
    }

    private void ResetTiming()
    {
        _credit = 0;
        _previousTime = _sampleTime = Stopwatch.GetTimestamp();
        _sampleTick = _session?.State.Clock.Tick ?? 0;
        _ticksPerSecond = 0;
    }

    private void Publish(bool force = false)
    {
        if (_session is null || _faulted) return;
        var snapshot = _builder.Capture(_session, _generation, _paused, _speed,
            _ticksPerSecond, Volatile.Read(ref _request), force);
        Volatile.Write(ref _latest, snapshot);
        _lastPublication = Stopwatch.GetTimestamp();
    }

    private void Run()
    {
        ResetTiming();
        try
        {
            while (!_stopping)
            {
                while (!_stopping && _commands.TryDequeue(out var command)) command.Run();
                if (_stopping) break;
                try
                {
                    var now = Stopwatch.GetTimestamp();
                    var elapsed = Stopwatch.GetElapsedTime(_previousTime, now).TotalSeconds;
                    _previousTime = now;
                    if (_session is not null && !_paused && !_faulted)
                    {
                        // Bound wall-time backlog after a machine stall. Never skip a simulation tick.
                        _credit = Math.Min(_credit + elapsed * BaseTicksPerSecond * _speed, BaseTicksPerSecond * _speed * 2);
                        var batchStart = Stopwatch.GetTimestamp();
                        var batch = 0;
                        while (_credit >= 1 && batch < 64 && !_stopping && _commands.IsEmpty)
                        {
                            _session.Step();
                            _credit--;
                            batch++;
                            if (Stopwatch.GetElapsedTime(batchStart).TotalMilliseconds >= 8) break;
                        }
                    }
                    else _credit = 0;
                    now = Stopwatch.GetTimestamp();
                    var sampleSeconds = Stopwatch.GetElapsedTime(_sampleTime, now).TotalSeconds;
                    if (sampleSeconds >= .5)
                    {
                        var tick = _session?.State.Clock.Tick ?? 0;
                        _ticksPerSecond = _paused ? 0 : (tick - _sampleTick) / sampleSeconds;
                        _sampleTick = tick;
                        _sampleTime = now;
                    }
                    if (_session is not null && !_faulted &&
                        (Stopwatch.GetElapsedTime(_lastPublication, now).TotalSeconds >= 1.0 / PublicationsPerSecond ||
                        (Latest is { } last && last.Paused != _paused))) Publish();
                }
                catch (Exception ex)
                {
                    _paused = true;
                    _faulted = true;
                    _credit = 0;
                    _errors.Enqueue(ex.ToString());
                }
                if (!_paused && !_faulted && _credit >= 1) Thread.Yield();
                else
                {
                    var wait = _paused || _session is null || _faulted ? 20 :
                        Math.Clamp((int)Math.Ceiling((1 - _credit) * 1000 / (BaseTicksPerSecond * _speed)), 1, 20);
                    _wake.WaitOne(wait);
                }
            }
        }
        finally
        {
            while (_commands.TryDequeue(out var pending)) pending.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_stopping) return;
            _stopping = true;
            _wake.Set();
        }
        _thread.Join();
        _wake.Dispose();
    }
}
