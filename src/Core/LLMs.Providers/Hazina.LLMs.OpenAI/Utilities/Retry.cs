public class Retry
{
    public static T Run<T>(Func<T> func, int retries = 3, int initialWait = 200, int waitMultiplier = 5)
    {
        var attempt = 0;
        while (true)
        {
            try
            {
                return func();
            }
            catch (Exception)
            {
                attempt++;
                if (attempt > retries)
                    throw;
                Thread.Sleep((int)Math.Pow(attempt, waitMultiplier) * initialWait);
            }
        }
    }

    public int attemptPow = 6;
    public int attemptInitialMs = 100;

    public Retry(Action<string> _workerlog) { workerLog = _workerlog; }

    public async Task tryx(int x, Func<Task> a)
    {
        int attempt = 0;
        while (attempt < x)
            try
            {
                await a();
                return;
            }
            catch (Exception e)
            {
                workerLog("Attempt " + ++attempt + " failed.");
                workerLog(e.ToString());
                if (attempt < x)
                    Thread.Sleep((int)Math.Pow(attemptPow, attempt) * attemptInitialMs);
                else
                    throw;
            }
    }
    public async Task<T> tryx<T>(int x, Func<Task<T>> a)
    {
        int attempt = 0;
        while (attempt < x)
            try
            {
                return await a();
            }
            catch (Exception e)
            {
                workerLog("Attempt " + ++attempt + " failed.");
                workerLog(e.ToString());
                if (attempt < x)
                    Thread.Sleep((int)Math.Pow(attemptPow, attempt) * attemptInitialMs);
                else
                    throw;
            }
        throw new Exception();
    }
    public async Task try5(Func<Task> a) => await tryx(5, a);
    public async Task<T> try5<T>(Func<Task<T>> a) => await tryx(5, a);
    public Action<string> workerLog = null;
}
