namespace CnabApi.Services;

/// <summary>
/// Status codes to help controller determine appropriate HTTP response.
/// </summary>
public enum UploadStatusCode
{
    Success = 200,
    BadRequest = 400,
    PayloadTooLarge = 413,
    UnsupportedMediaType = 415,
    UnprocessableEntity = 422
}
