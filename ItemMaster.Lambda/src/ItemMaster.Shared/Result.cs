namespace ItemMaster.Shared;

public class Result
{
    protected Result(bool isSuccess, string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; protected set; }
    public string? ErrorMessage { get; protected set; }

    public static Result Success()
    {
        return new Result(true);
    }

    public static Result Failure(string errorMessage)
    {
        return new Result(false, errorMessage);
    }
}