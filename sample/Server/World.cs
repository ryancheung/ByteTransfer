using System.Collections.Concurrent;
using System.Collections.Generic;
using ByteTransfer;

namespace Server
{
    public static class World 
    {
        private static List<BaseSession> Sessions = new List<BaseSession>();
        public static readonly ConcurrentQueue<BaseSession> NewSessions = new ConcurrentQueue<BaseSession>();

        public static void AddSession(BaseSession session)
        {
            NewSessions.Enqueue(session);
        }

        public static void Process()
        {
            BaseSession newSession;
            while(NewSessions.TryDequeue(out newSession))
                Sessions.Add(newSession);

            foreach(var s in Sessions)
                s.Process();
        }
    }
}
