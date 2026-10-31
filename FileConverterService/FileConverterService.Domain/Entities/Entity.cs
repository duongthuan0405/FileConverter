namespace FileConverterService.Domain.Entities;

public abstract class Entity
{
    private Guid _id;

    protected Entity()
    {
        Id = Guid.NewGuid();
    }

    public Guid Id
    {
        get => _id;
        private set => _id = value;
    }
}
