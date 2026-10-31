# Entity Convention

Domain entities must store their primary state in private backing fields named `_<camelCase>`.
Expose each backing field through a public property with explicit `get` and `set` accessors:

```csharp
private Guid _id;

public Guid Id
{
    get => _id;
    set => _id = value;
}
```

Do not use auto-properties for the entity's primary persisted state. Keep framework, I/O, and infrastructure concerns out of the Domain layer.
