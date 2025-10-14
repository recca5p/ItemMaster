namespace ItemMaster.Shared;

public class Result<T> : Result
{
    private Result(bool isSuccess, T? value = default, string? errorMessage = null)
        : base(isSuccess, errorMessage)
    {
        Value = value;
    }

    public T? Value { get; private set; }

    public static Result<T> Success(T value)
    {
        return new Result<T>(true, value);
    }

    public new static Result<T> Failure(string errorMessage)
    {
        return new Result<T>(false, default, errorMessage);
    }
}