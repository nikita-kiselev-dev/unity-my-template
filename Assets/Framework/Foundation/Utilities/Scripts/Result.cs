namespace Framework.Foundation.Utilities
{
    public readonly struct Result<T>
    {
        public readonly T Value;
        public readonly bool HasValue;

        public Result(T value, bool hasValue)
        {
            Value = value;
            HasValue = hasValue;
        }

        public static Result<T> Success(T value) => new(value, true);
        public static Result<T> Failure() => new(default, false);

        public bool TryGet(out T value)
        {
            value = Value;
            return HasValue;
        }

        public T GetValueOrDefault(T fallback = default) => HasValue ? Value : fallback;
    }
}
