using Mmzkworks.muProperty;

namespace Mmzkworks.muSettings
{
    /// <summary>
    /// Produces a <see cref="PropertyTree"/> from an external source.
    /// </summary>
    public interface IPropertyTreeSource
    {
        PropertyTree Load();
    }
}
