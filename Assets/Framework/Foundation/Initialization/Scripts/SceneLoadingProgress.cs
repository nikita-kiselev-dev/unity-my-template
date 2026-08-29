namespace Framework.Foundation.Initialization
{
    public class SceneLoadingProgress
    {
        public string Phase { get; private set; }
        public int Completed { get; private set; } 
        public int Total { get; private set; } 
        public bool IsLoaded { get; private set; }

        public void SetPhase(string phase)
        {
            Phase = phase;
        }

        public void SetCompleted(int completed)
        {
            Completed = completed;

            if (!IsLoaded && Completed >= Total)
            {
                IsLoaded = true;
            }
        }

        public void SetTotal(int total)
        {
            Total = total;
        }
    }
}