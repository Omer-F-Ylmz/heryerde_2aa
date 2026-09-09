namespace HerYerde.Core.Utilities.Results;

public interface IResult
{
    bool Success { get; }
    string Message { get; }
}

public interface IDataResult<out T> : IResult
{
    T? Data { get; }
}

public class Result : IResult
{
    public Result(bool success, string message)
    {
        Success = success;
        Message = message;
    }

    public Result(bool success) : this(success, string.Empty)
    {
    }

    public bool Success { get; }
    public string Message { get; }
}

public class SuccessResult : Result
{
    public SuccessResult(string message) : base(true, message)
    {
    }

    public SuccessResult() : base(true)
    {
    }
}

public class ErrorResult : Result
{
    public ErrorResult(string message) : base(false, message)
    {
    }
}

public class DataResult<T> : Result, IDataResult<T>
{
    public DataResult(T? data, bool success, string message) : base(success, message)
    {
        Data = data;
    }

    public DataResult(T? data, bool success) : base(success)
    {
        Data = data;
    }

    public T? Data { get; }
}

public class SuccessDataResult<T> : DataResult<T>
{
    public SuccessDataResult(T data, string message) : base(data, true, message)
    {
    }

    public SuccessDataResult(T data) : base(data, true)
    {
    }
}

public class ErrorDataResult<T> : DataResult<T>
{
    public ErrorDataResult(string message) : base(default, false, message)
    {
    }
}
