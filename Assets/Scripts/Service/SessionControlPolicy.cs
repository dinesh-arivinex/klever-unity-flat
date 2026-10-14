// Pure transition policy shared by MQTT handling and regression checks.
internal enum SessionTransition { Ignore, Start, Restart, Pause, Resume, Stop }
internal static class SessionControlPolicy
{
    internal static SessionTransition Decide(bool start, bool stop, bool pause,
        bool explicitStart, bool explicitResume, bool running, bool paused, bool reloading)
    {
        if (stop) return SessionTransition.Stop;
        if (explicitStart) return running || reloading ? SessionTransition.Restart : SessionTransition.Start;
        if (running && pause) return SessionTransition.Pause;
        if (running && (explicitResume || (paused && start && !pause))) return SessionTransition.Resume;
        if (start) return running || reloading ? SessionTransition.Restart : SessionTransition.Start;
        return SessionTransition.Ignore;
    }
}
