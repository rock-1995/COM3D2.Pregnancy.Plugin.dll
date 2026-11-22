// Test API only. This is not Unity code and must never be installed into the game.
namespace UnityEngine;

public class Object
{
    public static bool operator ==(Object left, Object right) => ReferenceEquals(left, right);
    public static bool operator !=(Object left, Object right) => !ReferenceEquals(left, right);
    public override bool Equals(object other) => ReferenceEquals(this, other);
    public override int GetHashCode() => base.GetHashCode();
}
