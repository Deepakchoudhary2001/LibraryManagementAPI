namespace LibraryManagement.Api.Services
{
    public class BookCacheVersionService
    {
        private long _version;

        public long CurrentVersion => Interlocked.Read(ref _version);

        public void InvalidateLists()
        {
            Interlocked.Increment(ref _version);
        }
    }
}
