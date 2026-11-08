using FileConverterService.Domain.Enums;

namespace FileConverterService.Domain.Entities;

public sealed class ConversionJob
{
    private Guid _id = Guid.NewGuid();
    private string _originalFileName = "";
    private string _contentType = "";
    private string _inputStorageKey = "";
    private ConversionJobStatus _status;
    private DateTimeOffset _createdAt;
    private DateTimeOffset? _proccessedAt;
    private DateTimeOffset? _completedAt;
    private string? _outputStorageKey;
    private string? _error;

    public ConversionJob()
    {

    }

    public ConversionJob(string originalFileName, string contentType, string inputStorageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputStorageKey);

        OriginalFileName = originalFileName;
        ContentType = contentType;
        InputStorageKey = inputStorageKey;
        CreatedAt = DateTimeOffset.UtcNow;
        Status = ConversionJobStatus.Pending;
    }

    public Guid Id
    {
        get => _id;
        private set => _id = value;
    }

    public string OriginalFileName 
    { 
        get => _originalFileName; 
        private set => _originalFileName = value; 
    }
    public string ContentType 
    { 
        get => _contentType; 
        private set => _contentType = value; 
    }
    public string InputStorageKey 
    { 
        get => _inputStorageKey; 
        private set => _inputStorageKey = value; 
    }
    public ConversionJobStatus Status 
    { 
        get => _status; 
        private set => _status = value; 
    }
    public DateTimeOffset CreatedAt 
    { 
        get => _createdAt; 
        private set => _createdAt = value;
    }
    public DateTimeOffset? CompletedAt 
    { 
        get => _completedAt; 
        private set => _completedAt = value; }
    public string? Error 
    { 
        get => _error; 
        private set => _error = value; 
    }
    public string? OutputStorageKey 
    { 
        get => _outputStorageKey; 
        private set => _outputStorageKey = value; 
    }
    public DateTimeOffset? ProccessedAt 
    { 
        get => _proccessedAt; 
        set => _proccessedAt = value; 
    }

    public void Start() 
    { 
        EnsureStatus(ConversionJobStatus.Pending); 
        Status = ConversionJobStatus.Processing; 
        ProccessedAt = DateTimeOffset.UtcNow;
        Error = null; 
    }
    public void Complete(string outputStorageKey) 
    { 
        ArgumentException.ThrowIfNullOrWhiteSpace(outputStorageKey);
        EnsureStatus(ConversionJobStatus.Processing); 
        Status = ConversionJobStatus.Completed; 
        CompletedAt = DateTimeOffset.UtcNow;
        OutputStorageKey = outputStorageKey;
        Error = null;
    }
    public void Fail(string error) 
    { 
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        EnsureStatus(ConversionJobStatus.Processing); 
        Status = ConversionJobStatus.Failed;
        Error = error;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Interrupt(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        EnsureStatus(ConversionJobStatus.Processing);
        Status = ConversionJobStatus.Interrupted;
        Error = error;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureStatus(ConversionJobStatus expectedStatus)
    {
        if (Status != expectedStatus)
            throw new ArgumentException($"Job '{Id}' must be in status '{expectedStatus}' but is '{Status}'.");
    }
}
