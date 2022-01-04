using System.Collections.Generic;
using System.Collections.Concurrent;

namespace AudibleSyncService
{
    /// <summary>
    /// [Threadsafe] Aquires locks based on string keys
    /// </summary>
    public class ApiLockService
    {
        private ConcurrentDictionary<string, bool> _locks = new();
        public bool IsLocked(string email)
        {
            return _locks.GetValueOrDefault(email, false);
        }

        public bool TryLock(string email)
        {
            var addSuccessful = _locks.TryAdd(email, true);
            if (addSuccessful)
            {
                return true;
            }

            var updateSuccessful = _locks.TryUpdate(email, true, false);

            return updateSuccessful;
        }

        public bool TryUnlock(string email)
        {
            return _locks.TryUpdate(email, false, true);
        }
    }
}
