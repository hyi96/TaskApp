using System;

namespace TaskApp.Models.Tags;

public class Tag : IEquatable<Tag>
{
    public Guid Id { get; }
    public string Name { get; set; }

    public Tag(string name, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid();
        Name = name;
    }

    public override bool Equals(object? obj) => Equals(obj as Tag);

    public bool Equals(Tag? other) => other != null && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Name;
}
